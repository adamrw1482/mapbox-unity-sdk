using System;
using System.Collections.Generic;
using Mapbox.BaseModule.Data.Tiles;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Unity;
using Mapbox.BaseModule.Utilities;
using Mapbox.VectorModule.MeshGeneration;
using Mapbox.VectorModule.MeshGeneration.MeshModifiers;
using Mapbox.VectorModule.Unity;
using Mapbox.VectorTile.Contants;
using Mapbox.VectorTile.Geometry;
using UnityEngine;

namespace Mapbox.VectorModule.BuildingLayerVisualizer
{
    public class RoadComponentVisualizer : MapboxComponentVisualizer
    {
        private RoadComponentSettings _settings;
        
        public RoadComponentVisualizer(string name, IMapInformation mapInformation, UnityContext unityContext = null, RoadComponentSettings settings = null) : base(name, mapInformation, unityContext)
        {
            _settings = settings ?? new RoadComponentSettings();
        }

        public override HardcoreMeshData CreateMesh(CanonicalTileId tileId, PerfVectorTileLayer layer)
        {
            var featureCount = layer.FeatureCount();
            var info = new StackMeshInfo(featureCount);
            var tileSize = Conversions.TileSizeInUnitySpace(tileId.Z, _mapInformation.Scale);
            var scaledRoadWidth = _settings.RoadWidth / _mapInformation.Scale / tileSize;
            var featureArray = new PerfVectorFeatureUnity[featureCount];
            
            for (var i = 0; i < featureCount; i++)
            {
                var feature = GetFeature(layer, i);
                if (feature == null || feature.VertexData.VertexCount <= 1) continue;
                feature.TileId = tileId;
                featureArray[i] = feature;

                info.triRanges[i] = info.TotalTriangleCount;
                info.vertexRanges[i] = info.TotalPointCount;
                
                var vertNeeded = VertexNeed(feature.VertexData);
                info.TotalPointCount += vertNeeded;
                info.vertexSize[i] = vertNeeded;
                if (feature.VertexData.VertexCount == 2)
                {
                    info.triSize[i] = 6;
                    info.TotalTriangleCount += 6; //2 tri 3 vert
                }
                else
                {
                    //2 start 2 end
                    // each turn will take 2 tris 3 vert per
                    var triSize = 6 + ((feature.VertexData.VertexCount - 2) * 12);
                    info.triSize[i] = triSize;
                    info.TotalTriangleCount += triSize;
                }
            }
            
            var meshData = new HardcoreMeshData(info, info.TotalPointCount);
            var triList = new int[info.TotalTriangleCount];
            var featureTriIndex = 0;
            
            for (int i = 0; i < featureArray.Length; i++)
            {
                var featureResult = featureArray[i];
                if(featureResult == null) continue;
                
                var iterativeSubVertIndex = 0;
                var iterativeSubTriIndex = 0;

                var designedVertCount = meshData.MeshInfo.vertexSize[i];
                if(designedVertCount <= 2 || meshData.MeshInfo.triSize[i] < 3) continue;
                
                var vertices = meshData.Vertices.AsSpan(meshData.MeshInfo.vertexRanges[i], designedVertCount);
                var normals = meshData.Normals.AsSpan(meshData.MeshInfo.vertexRanges[i], designedVertCount);
                var tris = triList.AsSpan(meshData.MeshInfo.triRanges[i], meshData.MeshInfo.triSize[i]);
                int turnState = 0;
                var prevFirst = -2;
                var prevSecond = -1;
                
                for (var j = 0; j < featureResult.VertexData.Submeshes.Count - 1; j++)
                {
                    var subSize = featureResult.VertexData.Submeshes[j + 1] - featureResult.VertexData.Submeshes[j];
                    var startVertex = featureResult.VertexData.Submeshes[j];
                    var endVertex = featureResult.VertexData.Submeshes[j + 1];

                    Vector3 current = new Vector3(0, 0, 0);
                    Vector3 sideNormal = new Vector3(0, 0, 0);
                    for (int k = 0; k < Math.Max(2, subSize - 1); k++)
                    {
                        current = featureResult.VertexData.Vertices[startVertex + k];
                        var next = featureResult.VertexData.Vertices[startVertex + k + 1];
                        
                        //first point
                        if (k == 0)
                        {
                            var dir = next - current;
                            var dirNormalized = dir.normalized * scaledRoadWidth;
                            sideNormal = new Vector3(dirNormalized.z, 0, -dirNormalized.x);
                            vertices[iterativeSubVertIndex] = new Vector3(current.x + sideNormal.x, current.y, current.z + sideNormal.z);
                            vertices[iterativeSubVertIndex + 1] = new Vector3(current.x - sideNormal.x, current.y, current.z - sideNormal.z);
                            
                            normals[iterativeSubVertIndex] = Vector3.up;
                            normals[iterativeSubVertIndex + 1] = Vector3.up;

                            iterativeSubVertIndex += 2;
                            turnState = 0;
                        }
                        else if (subSize == 2)
                        {
                            if (turnState == 2 || turnState == 0)
                            {
                                prevFirst = -1;
                                prevSecond = -2;
                            }
                            else if(turnState == 1)
                            {
                                prevFirst = -2;
                                prevSecond = -1;
                            }
                            
                            vertices[iterativeSubVertIndex] = new Vector3(current.x + sideNormal.x, current.y, current.z + sideNormal.z);
                            vertices[iterativeSubVertIndex + 1] = new Vector3(current.x - sideNormal.x, current.y, current.z - sideNormal.z);
                            
                            normals[iterativeSubVertIndex] = Vector3.up;
                            normals[iterativeSubVertIndex + 1] = Vector3.up;
                            
                            tris[iterativeSubTriIndex++] = featureTriIndex + iterativeSubVertIndex + prevSecond;
                            tris[iterativeSubTriIndex++] = featureTriIndex + iterativeSubVertIndex + prevFirst;
                            tris[iterativeSubTriIndex++] = featureTriIndex + iterativeSubVertIndex + 0;

                            tris[iterativeSubTriIndex++] = featureTriIndex + iterativeSubVertIndex + 0;
                            tris[iterativeSubTriIndex++] = featureTriIndex + iterativeSubVertIndex + prevFirst;
                            tris[iterativeSubTriIndex++] = featureTriIndex + iterativeSubVertIndex + 1;
                            
                            iterativeSubVertIndex += 2;
                        }
                        else
                        {
                            var prev = featureResult.VertexData.Vertices[startVertex + k - 1];
                            var dirNext = (next - current).normalized;
                            var dirPrev = (prev - current).normalized;
                            var dirInside = ((dirNext + dirPrev)/2).normalized * scaledRoadWidth;
                            
                            var prevSideNormal = sideNormal;
                            
                            sideNormal = new Vector3(dirNext.z, 0, -dirNext.x).normalized * scaledRoadWidth;
                            var isRight = Vector3.Dot(prevSideNormal, dirNext) > 0;

                            
                            if (isRight)
                            {
                                if (turnState == 1)
                                {
                                    prevFirst = -1;
                                    prevSecond = -2;
                                }
                                else if(turnState == 2 || turnState == 0)
                                {
                                    prevFirst = -2;
                                    prevSecond = -1;
                                }
                                
                                vertices[iterativeSubVertIndex] = new Vector3(current.x - prevSideNormal.x, current.y, current.z - prevSideNormal.z);
                                vertices[iterativeSubVertIndex + 1] = new Vector3(current.x - dirInside.x, current.y, current.z - dirInside.z);
                                vertices[iterativeSubVertIndex + 2] = new Vector3(current.x - sideNormal.x, current.y, current.z - sideNormal.z);
                                vertices[iterativeSubVertIndex + 3] = new Vector3(current.x + dirInside.x, current.y , current.z + dirInside.z);

                                normals[iterativeSubVertIndex] = Vector3.up;
                                normals[iterativeSubVertIndex + 1] = Vector3.up;
                                normals[iterativeSubVertIndex + 2] = Vector3.up;
                                normals[iterativeSubVertIndex + 3] = Vector3.up;
                                
                                //prev tris
                                tris[iterativeSubTriIndex++] = featureTriIndex + iterativeSubVertIndex + prevFirst;
                                tris[iterativeSubTriIndex++] = featureTriIndex + iterativeSubVertIndex + prevSecond;
                                tris[iterativeSubTriIndex++] = featureTriIndex + iterativeSubVertIndex + 0;
                                
                                tris[iterativeSubTriIndex++] = featureTriIndex + iterativeSubVertIndex + 0;
                                tris[iterativeSubTriIndex++] = featureTriIndex + iterativeSubVertIndex + 3;
                                tris[iterativeSubTriIndex++] = featureTriIndex + iterativeSubVertIndex + prevFirst;
                                //prev tris

                                //corner tris
                                tris[iterativeSubTriIndex++] = featureTriIndex + iterativeSubVertIndex + 0;
                                tris[iterativeSubTriIndex++] = featureTriIndex + iterativeSubVertIndex + 1;
                                tris[iterativeSubTriIndex++] = featureTriIndex + iterativeSubVertIndex + 3;
                                
                                tris[iterativeSubTriIndex++] = featureTriIndex + iterativeSubVertIndex + 1;
                                tris[iterativeSubTriIndex++] = featureTriIndex + iterativeSubVertIndex + 2;
                                tris[iterativeSubTriIndex++] = featureTriIndex + iterativeSubVertIndex + 3;
                                //corner tris

                                turnState = 1;
                            }
                            else
                            {
                                if (turnState == 1)
                                {
                                    prevFirst = -1;
                                    prevSecond = -2;
                                }
                                else if(turnState == 2 || turnState == 0)
                                {
                                    prevFirst = -2;
                                    prevSecond = -1;
                                }

                                vertices[iterativeSubVertIndex] = new Vector3(current.x + prevSideNormal.x, current.y, current.z + prevSideNormal.z);
                                vertices[iterativeSubVertIndex + 1] = new Vector3(current.x - dirInside.x, current.y , current.z - dirInside.z);
                                vertices[iterativeSubVertIndex + 2] = new Vector3(current.x + sideNormal.x, current.y, current.z + sideNormal.z);
                                vertices[iterativeSubVertIndex + 3] = new Vector3(current.x + dirInside.x, current.y, current.z + dirInside.z);
                                

                                normals[iterativeSubVertIndex] = Vector3.up;
                                normals[iterativeSubVertIndex + 1] = Vector3.up;
                                normals[iterativeSubVertIndex + 2] = Vector3.up;
                                normals[iterativeSubVertIndex + 3] = Vector3.up;


                                //prev tris
                                tris[iterativeSubTriIndex++] = featureTriIndex + iterativeSubVertIndex + 0;
                                tris[iterativeSubTriIndex++] = featureTriIndex + iterativeSubVertIndex + prevFirst;
                                tris[iterativeSubTriIndex++] = featureTriIndex + iterativeSubVertIndex + prevSecond;
                                

                                tris[iterativeSubTriIndex++] = featureTriIndex + iterativeSubVertIndex + 3;
                                tris[iterativeSubTriIndex++] = featureTriIndex + iterativeSubVertIndex + 0;
                                tris[iterativeSubTriIndex++] = featureTriIndex + iterativeSubVertIndex + prevSecond;
                                
                                //prev tris

                                //corner tris
                                tris[iterativeSubTriIndex++] = featureTriIndex + iterativeSubVertIndex + 0;
                                tris[iterativeSubTriIndex++] = featureTriIndex + iterativeSubVertIndex + 3;
                                tris[iterativeSubTriIndex++] = featureTriIndex + iterativeSubVertIndex + 1;

                                tris[iterativeSubTriIndex++] = featureTriIndex + iterativeSubVertIndex + 2;
                                tris[iterativeSubTriIndex++] = featureTriIndex + iterativeSubVertIndex + 1;
                                tris[iterativeSubTriIndex++] = featureTriIndex + iterativeSubVertIndex + 3;
                                //corner tris 

                                turnState = 2;
                            }
                            
                            iterativeSubVertIndex += 4;
                            
                            if (k == subSize - 2)
                            {
                                if (turnState == 2 || turnState == 0)
                                {
                                    prevFirst = -1;
                                    prevSecond = -2;
                                }
                                else if(turnState == 1)
                                {
                                    prevFirst = -2;
                                    prevSecond = -1;
                                }

                                current = next;
                                vertices[iterativeSubVertIndex] = new Vector3(current.x + sideNormal.x, current.y, current.z + sideNormal.z);
                                vertices[iterativeSubVertIndex + 1] = new Vector3(current.x - sideNormal.x, current.y, current.z - sideNormal.z);
                            
                                normals[iterativeSubVertIndex] = Vector3.up;
                                normals[iterativeSubVertIndex + 1] = Vector3.up;
                            
                                tris[iterativeSubTriIndex++] = featureTriIndex + iterativeSubVertIndex + prevSecond;
                                tris[iterativeSubTriIndex++] = featureTriIndex + iterativeSubVertIndex + prevFirst;
                                tris[iterativeSubTriIndex++] = featureTriIndex + iterativeSubVertIndex + 0;

                                tris[iterativeSubTriIndex++] = featureTriIndex + iterativeSubVertIndex + 0;
                                tris[iterativeSubTriIndex++] = featureTriIndex + iterativeSubVertIndex + prevFirst;
                                tris[iterativeSubTriIndex++] = featureTriIndex + iterativeSubVertIndex + 1;
                            
                                iterativeSubVertIndex += 2;
                            }
                        }
                    }
                }

                featureTriIndex += designedVertCount;
            }
            
            meshData.Triangles.Add(triList);
            return meshData;
        }

        private List<int> GetSubmeshRanges(MeshVertexData data)
        {
            List<int> submeshStarts = new List<int>();
            submeshStarts.Add(0);
            var total = 0;
            for (var i = 0; i < data.Submeshes.Count - 1; i++)
            {
                var size = data.Submeshes[i + 1] - data.Submeshes[i];
                total += 4 * size - 4;
                submeshStarts.Add(total);  //(size - 2) * 4 + (2 * 2); //two vertices for cap, 4 for each mid vertex
            }

            return submeshStarts;
        }

        private int VertexNeed(MeshVertexData data)
        {
            var total = 0;
            for (var i = 0; i < data.Submeshes.Count - 1; i++)
            {
                var size = data.Submeshes[i + 1] - data.Submeshes[i];
                total += 4 * size - 4;  //(size - 2) * 4 + (2 * 2); //two vertices for cap, 4 for each mid vertex
            }

            return total;
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

            // foreach (var vertex in meshData.Vertices)
            // {
            //     var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            //     go.transform.position = vertex;
            //     go.transform.localScale = Vector3.one * 0.001f;
            //     go.transform.SetParent(entity.Transform);
            // }
            
            return objectList;
        }

        private PerfVectorFeatureUnity GetFeature(PerfVectorTileLayer layer, int i)
        {
            var view = layer.GetViewFor(i);
            var layerData = layer.Data.Slice(view.x, view.y);
            var featureReader = new PerfPbfReader(layerData);
            var feature = new RoadFeatureUnity();
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

        private ref struct RoadFeatureUnity
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
                // for (int i = 0; i < Tags.Length; i += 2)
                // {
                //     if (Tags[i] == layer.HeightTag)
                //     {
                //         Height = Convert.ToSingle(layer.Values[Tags[i + 1]]);
                //     }
                //     else if (Tags[i] == layer.MinHeightTag)
                //     {
                //         MinHeight = Convert.ToSingle(layer.Values[Tags[i + 1]]);
                //     }
                //     else if (Tags[i] == layer.ExtrudeTag)
                //     {
                //         DoExtrude = Convert.ToBoolean(layer.Values[Tags[i + 1]]);
                //     }
                // }
            }
        }
    }
}