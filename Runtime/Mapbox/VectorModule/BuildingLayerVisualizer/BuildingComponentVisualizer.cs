using System;
using System.Collections.Generic;
using Mapbox.BaseModule.Data.Tiles;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Unity;
using Mapbox.BaseModule.Utilities;
using Mapbox.VectorTile.Contants;
using Mapbox.VectorTile.Geometry;
using UnityEngine;

namespace Mapbox.VectorModule.BuildingLayerVisualizer
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

        public override HardcoreMeshData CreateMesh(CanonicalTileId tileId, PerfVectorTileLayer layer)
        {
            var arrayPolygon = new ArrayPolygon();
            var arraySnapTerrain = (_settings.EnableTerrainSnapping) ? new ArraySnapTerrain(_mapInformation) : null;
            IPerformanceExtrusion arrayHeight = _settings.RoundBuildingCorners
                ? new ArrayChamferHeight(_settings.ChamferExtrusionSettings)
                : new ArrayHeight(_settings.BasicExtrusionSettings);
            var featureCount = layer.FeatureCount();
            var info = new StackMeshInfo(featureCount);

            //var offsetTo14 = tileId.CalculateTopLeftScaleOffsetAtZoom(14);
            var tileSize = Conversions.TileSizeInUnitySpace(tileId.Z, _mapInformation.Scale);
            var featureArray = new PerfVectorFeatureUnity[featureCount];

            for (var i = 0; i < featureCount; i++)
            {
                var feature = GetFeature(layer, i);
                if (feature == null) continue;
                feature.TileId = tileId;
                featureArray[i] = feature;

                info.vertexRanges[i] = info.TotalPointCount;
                info.TotalPointCount += feature.VertexData.VertexCount * 5;
            }

            var meshData = new HardcoreMeshData(info, info.TotalPointCount);

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

                var vertices =
                    meshData.Vertices.AsSpan(meshData.MeshInfo.vertexRanges[i], meshData.MeshInfo.VertexCount(i));
                var normals =
                    meshData.Normals.AsSpan(meshData.MeshInfo.vertexRanges[i], meshData.MeshInfo.VertexCount(i));
                var vertexAnchorIndex = meshData.MeshInfo.vertexRanges[i];

                triIndex = arrayPolygon.Polygonize(vertices, normals, vertexAnchorIndex, triList, triIndex,
                    featureResult);
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
                    arraySnapTerrain.SnapTerrain(vertices, vertices.Length / 5, featureResult, tileSize);
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
                    triIndex = arrayHeight.Run(vertices, normals, vertexAnchorIndex, triList, triIndex, featureResult,
                        tileSize, _mapInformation);
                }

                info.triRanges[i] = baseTriIndex + triIndex;
                //ArrayPool<Vector3>.Shared.Return(featureResult.VertexData.Vertices);
            }

            meshData.Triangles.Add(triList);
            return meshData;
        }

        public override List<GameObject> CreateGo(CanonicalTileId tileId, HardcoreMeshData meshData)
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

        private PerfVectorFeatureUnity GetFeature(PerfVectorTileLayer layer, int i)
        {
            var view = layer.GetViewFor(i);
            var layerData = layer.Data.Slice(view.x, view.y);
            var featureReader = new PerfPbfReader(layerData);
            var feature = new PerfVectorTileFeature();
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
            feature.SetTags(ref layer);
            var featureResult = new PerfVectorFeatureUnity
            {
                Height = feature.Height,
                MinHeight = feature.MinHeight,
                DoExtrude = feature.DoExtrude,
                //Properties = feature.GetProperties(ref layer),
                VertexData = feature.Geometry(new Vector3(layerExtent, 0, -layerExtent))
            };

            if (featureResult.VertexData.VertexCount < 1)
            {
                return null;
            }

            return featureResult;
        }

        private ref struct PerfVectorTileFeature
        {
            public ulong Id;
            public GeomType GeometryType;
            public uint[] GeometryCommands;

            public float Height;
            public float MinHeight;
            public bool DoExtrude;

            /// <summary>Tags to resolve properties https://github.com/mapbox/vector-tile-spec/tree/master/2.1#44-feature-attributes</summary>
            public int[] Tags;

            public MeshVertexData Geometry(Vector3 scale)
            {
                return PerformanceDecodeGeometry.GetGeometry(GeometryCommands, scale);
            }

            public MeshVertexData Geometry(Vector3 scale, Vector4 offsetTo14)
            {
                return PerformanceDecodeGeometry.GetGeometry(GeometryCommands, scale, offsetTo14);
            }

            /// <summary>
            /// Get properties of this feature. Throws exception if there is an uneven number of feature tag ids
            /// </summary>
            /// <returns>Dictionary of this feature's properties</returns>
            public Dictionary<string, object> GetProperties(ref PerfVectorTileLayer layer)
            {
                if (0 != Tags.Length % 2)
                {
                    throw new Exception(string.Format("Layer [{0}]: uneven number of feature tag ids", layer.Name));
                }

                int tagCount = Tags.Length;
                Dictionary<string, object> properties = new Dictionary<string, object>(tagCount / 2);
                for (int i = 0; i < tagCount; i += 2)
                {
                    properties.Add(layer.Keys[Tags[i]], layer.Values[Tags[i + 1]]);
                }

                return properties;
            }

            public void SetTags(ref PerfVectorTileLayer layer)
            {
                for (int i = 0; i < Tags.Length; i += 2)
                {
                    if (Tags[i] == layer.HeightTag)
                    {
                        Height = Convert.ToSingle(layer.Values[Tags[i + 1]]);
                    }
                    else if (Tags[i] == layer.MinHeightTag)
                    {
                        MinHeight = Convert.ToSingle(layer.Values[Tags[i + 1]]);
                    }
                    else if (Tags[i] == layer.ExtrudeTag)
                    {
                        DoExtrude = Convert.ToBoolean(layer.Values[Tags[i + 1]]);
                    }
                }
            }
        }
    }
}