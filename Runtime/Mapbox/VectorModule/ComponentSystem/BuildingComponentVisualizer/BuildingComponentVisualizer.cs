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

namespace Mapbox.VectorModule.ComponentSystem.BuildingComponentVisualizer
{
    public class BuildingComponentVisualizer : MapboxComponentVisualizer
    {
        private BuildingComponentSettings _settings;

        public BuildingComponentVisualizer(string name, IMapInformation mapInformation,
            UnityContext unityContext = null, BuildingComponentSettings settings = null) : base(name, mapInformation,
            unityContext)
        {
            _settings = settings ?? new BuildingComponentSettings();
        }

        public override MeshData CreateMesh(CanonicalTileId tileId, VectorTileLayer layer)
        {
            var arrayPolygon = new ArrayPolygon();
            ArraySnapTerrain arraySnapTerrain = null;
            if (_settings.EnableTerrainSnapping)
            {
                arraySnapTerrain = new ArraySnapTerrain(_mapInformation);
            }
            IPerformanceExtrusion arrayHeight = _settings.RoundBuildingCorners
                ? new ArrayChamferHeight(_settings.ChamferExtrusionSettings)
                : new ArrayHeight(_settings.BasicExtrusionSettings);
            var featureCount = layer.FeatureCount();
            var info = new StackMeshInfo(featureCount);

            var tileSize = Conversions.TileSizeInUnitySpace(tileId.Z, _mapInformation.Scale);
            var featureArray = new BuildingFeatureUnity[featureCount];

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
            var triList = new int[poolSize];
            var triIndex = 0;
            var baseTriIndex = 0;
            for (int i = 0; i < featureCount; i++)
            {
                var featureResult = featureArray[i];
                if (featureResult == null || !featureResult.DoExtrude)
                {
                    //ArrayPool<Vector3>.Shared.Return(featureResult.VertexData.Vertices);
                    continue;
                }

                var featurePreTriIndex = triIndex;

                var vertices = meshData.Vertices.AsSpan(meshData.MeshInfo.vertexRanges[i], meshData.MeshInfo.vertexSize[i]);
                var normals = meshData.Normals.AsSpan(meshData.MeshInfo.vertexRanges[i], meshData.MeshInfo.vertexSize[i]);
                var vertexAnchorIndex = meshData.MeshInfo.vertexRanges[i];

                triIndex = arrayPolygon.Polygonize(vertices, normals, vertexAnchorIndex, triList, triIndex, featureResult.VertexData);
                if (triIndex == -1)
                {
                    for (int j = featurePreTriIndex; j < triList.Length; j++)
                    {
                        triList[j] = 0;
                    }

                    meshData.Triangles.Add(triList);
                    baseTriIndex += triList.Length;
                    triList = new int[poolSize];
                    triIndex = 0;
                    i--;
                    //ArrayPool<Vector3>.Shared.Return(featureResult.VertexData.Vertices);
                    continue;
                }

                if (_settings.EnableTerrainSnapping)
                {
                    arraySnapTerrain.SnapTerrain(vertices, vertices.Length / 5, tileId, tileSize);
                }

                var triSpaceRequired = arrayHeight.CalculateTriCountFor(featureResult.VertexData.VertexCount);
                if (triSpaceRequired + triIndex >= triList.Length)
                {
                    for (int j = featurePreTriIndex; j < triList.Length; j++)
                    {
                        triList[j] = 0;
                    }

                    meshData.Triangles.Add(triList);
                    baseTriIndex += triList.Length;
                    triList = new int[triSpaceRequired + triIndex + 3];
                    triIndex = 0;
                    i--;
                    //ArrayPool<Vector3>.Shared.Return(featureResult.VertexData.Vertices);
                    continue;
                }
                else
                {
                    triIndex = arrayHeight.Run(vertices, 
                        normals, 
                        vertexAnchorIndex, 
                        triList, 
                        triIndex, 
                        featureResult.Height,
                        featureResult.MinHeight,
                        featureResult.VertexData,
                        tileSize, 
                        _mapInformation.Scale);
                }

                info.triRanges[i] = baseTriIndex + triIndex;
                //ArrayPool<Vector3>.Shared.Return(featureResult.VertexData.Vertices);
            }

            meshData.Triangles.Add(triList);
            return meshData;
        }

        public override List<GameObject> CreateGo(CanonicalTileId tileId, MeshData meshData)
        {
            var objectList = new List<GameObject>();
            var entity = _buildingObjectPool.GetObject();
            var mats = new Material[meshData.Triangles.Count];
            for (int i = 0; i < meshData.Triangles.Count; i++)
            {
                mats[i] = _settings.Material;
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

        private BuildingFeatureUnity GetFeature(VectorTileLayer layer, int i)
        {
            var view = layer.GetViewFor(i);
            var layerData = layer.Data.Slice(view.x, view.y);
            var featureReader = new PbfReader(layerData);
            var feature = new BuildingFeatureUnity();
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

        private class BuildingFeatureUnity
        {
            public ulong Id;
            public GeomType GeometryType;
            public uint[] GeometryCommands;
            public FeatureVertexData VertexData;
            public int[] Tags;
            public float Height;
            public float MinHeight;
            public bool DoExtrude;

            public void SetProperties(ref VectorTileLayer layer)
            {
                var tagCount = Tags.Length;
                for (int i = 0; i < tagCount; i += 2)
                {
                    if (layer.Keys[Tags[i]] == "height")
                    {
                        Height = Convert.ToSingle(layer.Values[Tags[i + 1]]);
                    }
                    else if (layer.Keys[Tags[i]] == "min_height")
                    {
                        MinHeight = Convert.ToSingle(layer.Values[Tags[i + 1]]);
                    }
                    else if (layer.Keys[Tags[i]] == "extrude")
                    {
                        DoExtrude = bool.Parse(layer.Values[Tags[i + 1]].ToString());
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