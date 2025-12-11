using System;
using System.Collections.Generic;
using Mapbox.BaseModule.Data.Tiles;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Unity;
using Mapbox.BaseModule.Utilities;
using Mapbox.VectorModule.ComponentSystem.Data;
using Mapbox.VectorModule.ComponentSystem.Modifiers;
using Mapbox.VectorTile.Contants;
using Mapbox.VectorTile.Geometry;
using UnityEngine;
using DecodeGeometry = Mapbox.VectorModule.ComponentSystem.Data.DecodeGeometry;

namespace Mapbox.VectorModule.ComponentSystem.AreaComponent
{
    public class AreaComponentVisualizer : MapboxComponentVisualizer
    {
        private AreaComponentSettings _settings;

        public AreaComponentVisualizer(string name, IMapInformation mapInformation,
            UnityContext unityContext = null, AreaComponentSettings settings = null) : base(name, mapInformation,
            unityContext)
        {
            _settings = settings ?? new AreaComponentSettings();
            _layerRootObject.transform.position = new Vector3(
                _layerRootObject.transform.position.x, 
                _settings.GameObjectOffset, 
                _layerRootObject.transform.position.z);
        }

        public override MeshData CreateMesh(CanonicalTileId tileId, VectorTileLayer layer)
        {
            var arrayPolygon = new ArrayPolygon();
            var featureCount = layer.FeatureCount();
            var info = new StackMeshInfo(featureCount);

            var tileSize = Conversions.TileSizeInUnitySpace(tileId.Z, _mapInformation.Scale);
            var featureArray = new AreaFeatureUnity[featureCount];

            for (var i = 0; i < featureCount; i++)
            {
                var feature = GetFeature(layer, i);
                if (feature == null) continue;
                featureArray[i] = feature;

                info.vertexRanges[i] = info.TotalPointCount;
                var vertNeeded = feature.VertexData.VertexCount * 5;
                info.vertexSize[i] = vertNeeded;
                info.TotalPointCount += vertNeeded;
            }

            var meshData = new MeshData(info, info.TotalPointCount);

            var poolSize = 10002;
            var triangles = new List<int[]>();
            for (var i = 0; i < _settings.StyleDictionary.Count; i++)
            {
                triangles.Add(new int[poolSize]);
            }
            var triIndex = 0;
            var baseTriIndex = 0;

            AreaStyle style = null;
            for (int i = 0; i < featureCount; i++)
            {
                var featureResult = featureArray[i];
                if (featureResult == null)
                {
                    //ArrayPool<Vector3>.Shared.Return(featureResult.VertexData.Vertices);
                    continue;
                }

                
                if (_settings.LayerName == "water")
                {
                    style = _settings.Styles[0];
                }
                else if (!_settings.StyleDictionary.TryGetValue(featureResult.Class, out style))
                {
                    continue;
                }

                var featurePreTriIndex = triIndex;

                var vertices = meshData.Vertices.AsSpan(meshData.MeshInfo.vertexRanges[i], meshData.MeshInfo.vertexSize[i]);
                var normals = meshData.Normals.AsSpan(meshData.MeshInfo.vertexRanges[i], meshData.MeshInfo.vertexSize[i]);
                var vertexAnchorIndex = meshData.MeshInfo.vertexRanges[i];

                var triList = triangles[style.Index];
                triIndex = arrayPolygon.Polygonize(vertices, normals, vertexAnchorIndex, triList, triIndex, featureResult.VertexData, style.PushUp);
                if (triIndex == -1)
                {
                    for (int j = featurePreTriIndex; j < triList.Length; j++)
                    {
                        triList[j] = 0;
                    }
                    
                    meshData.Triangles.Add(triList);
                    meshData.Materials.Add(style.Material);
                    triangles[style.Index] = new int[poolSize];
                    baseTriIndex += triList.Length;
                    triIndex = 0;
                    i--;
                    //ArrayPool<Vector3>.Shared.Return(featureResult.VertexData.Vertices);
                    continue;
                }
                
                info.triRanges[i] = baseTriIndex + triIndex;
                //ArrayPool<Vector3>.Shared.Return(featureResult.VertexData.Vertices);
            }

            for (var i = 0; i < triangles.Count; i++)
            {
                var submeshTri = triangles[i];
                meshData.Triangles.Add(submeshTri);
                meshData.Materials.Add(_settings.Styles[i].Material);
            }

            return meshData;
        }

        public override List<GameObject> CreateGo(CanonicalTileId tileId, MeshData meshData)
        {
            var objectList = new List<GameObject>();
            var entity = _buildingObjectPool.GetObject();
            var mats = new Material[meshData.Triangles.Count];
            // for (int i = 0; i < meshData.Triangles.Count; i++)
            // {
            //     mats[i] = _settings.Material;
            // }
            for (var i = 0; i < meshData.Triangles.Count; i++)
            {
                mats[i] = meshData.Materials[i];
            }

            entity.MeshRenderer.materials = mats;

            entity.GameObject.transform.SetParent(_layerRootObject);
            entity.StackId = 0;

            var mesh = entity.Mesh;
            mesh.Clear();
            mesh.SetVertices(meshData.Vertices);
            mesh.SetNormals(meshData.Normals);
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.subMeshCount = meshData.Triangles.Count;
            for (var index = 0; index < meshData.Triangles.Count; index++)
            {
                var submesh = meshData.Triangles[index];
                mesh.SetTriangles(submesh, index);
            }

            entity.MeshFilter.sharedMesh = mesh;
            objectList.Add(entity.GameObject);

            if (!_results.ContainsKey(tileId))
                _results.Add(tileId, new List<VectorEntity>());
            _results[tileId].Add(entity);
            OnBuildingMeshCreated(tileId, entity.GameObject, meshData);

            return objectList;
        }

        private AreaFeatureUnity GetFeature(VectorTileLayer layer, int i)
        {
            var view = layer.GetViewFor(i);
            var layerData = layer.Data.Slice(view.x, view.y);
            var featureReader = new PbfReader(layerData);
            var feature = new AreaFeatureUnity();
            bool geomTypeSet = false;
            while (featureReader.NextByte())
            {
                int featureType = featureReader.Tag;
                switch ((FeatureType)featureType)
                {
                    case FeatureType.Id:
                        feature.Id = (ulong)featureReader.Varint();
                        break;
                    case FeatureType.Tags:
                        var tags = featureReader.GetPackedInt();
                        feature.Tags = tags;
                        break;
                    case FeatureType.Type:
                        int geomType = (int)featureReader.Varint();
                        feature.GeometryType = (GeomType)geomType;
                        geomTypeSet = true;
                        break;
                    case FeatureType.Geometry:
                        if (null != feature.GeometryCommands)
                        {
                            throw new System.Exception(string.Format("Layer [{0}], feature already has a geometry",
                                layer.Name));
                        }

                        //get raw array of commands and coordinates
                        feature.GeometryCommands = featureReader.GetPackedUnit32();
                        break;
                    default:
                        featureReader.Skip();
                        break;
                }
            }

            var layerExtent = (float)layer.Extent;
            feature.SetProperties(ref layer);
            feature.VertexData = feature.Geometry(new Vector3(layerExtent, 0, -layerExtent));
            return feature;
        }

        private class AreaFeatureUnity
        {
            public ulong Id;
            public GeomType GeometryType;
            public uint[] GeometryCommands;
            public FeatureVertexData VertexData;
            public int[] Tags;
            public string Class;
            public string Type;

            public void SetProperties(ref VectorTileLayer layer)
            {
                if (Tags == null) //water layer has no tags
                    return;
                
                var tagCount = Tags.Length;
                //some features have odd number of tags
                //not sure if it's a bug or data issue
                //so -1 here to skip last single tag
                for (int i = 0; i < tagCount - 1; i += 2)
                {
                    if (layer.Keys[Tags[i]] == "class")
                    {
                        Class = layer.Values[Tags[i + 1]].ToString();
                    }
                    else if (layer.Keys[Tags[i]] == "type")
                    {
                        Type = layer.Values[Tags[i + 1]].ToString();
                    }
                }
            }

            public FeatureVertexData Geometry(Vector3 scale)
            {
                return DecodeGeometry.GetGeometry(GeometryCommands, scale);
            }
        }
    }
}