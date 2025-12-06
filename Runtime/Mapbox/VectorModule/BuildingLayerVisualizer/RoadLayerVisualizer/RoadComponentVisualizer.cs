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

        public RoadComponentVisualizer(string name, IMapInformation mapInformation, UnityContext unityContext = null,
            RoadComponentSettings settings = null) : base(name, mapInformation, unityContext)
        {
            _settings = settings ?? new RoadComponentSettings();
        }

        private string[] Skip = new[]
        {
            "path",
            "pedestrian",
            "major_rail"
        };

        private bool ShouldSkip(RoadFeatureUnity feature)
        {
            for (int i = 0; i < Skip.Length; i++)
            {
                if (Skip[i] == feature.Class) return true;
            }

            return false;
        }

        private enum Turn
        {
            Start,
            Right,
            Left
        }
        
        public override HardcoreMeshData CreateMesh(CanonicalTileId tileId, PerfVectorTileLayer layer)
        {
            var featureCount = layer.FeatureCount();
            var info = new StackMeshInfo(featureCount);
            var tileSize = Conversions.TileSizeInUnitySpace(tileId.Z, _mapInformation.Scale);
            var scaledRoadWidth = _settings.RoadWidth / _mapInformation.Scale / tileSize;
            var featureArray = new RoadFeatureUnity[featureCount];

            for (var i = 0; i < featureCount; i++)
            {
                var feature = GetFeature(layer, i);
                if (feature.VertexData.VertexCount <= 1) continue;
                if (ShouldSkip(feature)) continue;

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
                if (featureResult == null) continue;
                
                var designedVertCount = meshData.MeshInfo.vertexSize[i];
                if (designedVertCount <= 2 || meshData.MeshInfo.triSize[i] < 3) continue;

                var vertices = meshData.Vertices.AsSpan(meshData.MeshInfo.vertexRanges[i], designedVertCount);
                var normals = meshData.Normals.AsSpan(meshData.MeshInfo.vertexRanges[i], designedVertCount);
                var tris = triList.AsSpan(meshData.MeshInfo.triRanges[i], meshData.MeshInfo.triSize[i]);

                
                
                var lastTurnWas = Turn.Start;
                var prevFirst = -2;
                var prevSecond = -1;
                var subVertIndex = 0;
                var subTriIndex = 0;
                
                for (var j = 0; j < featureResult.VertexData.Submeshes.Count - 1; j++)
                {
                    var subSize = featureResult.VertexData.Submeshes[j + 1] - featureResult.VertexData.Submeshes[j];
                    var startVertex = featureResult.VertexData.Submeshes[j];

                    var sideNormal = new Vector3(0, 0, 0);
                    var finishLine = false;
                    for (int k = 0; k < subSize - 1; k++)
                    {
                        var current = featureResult.VertexData.Vertices[startVertex + k];
                        var next = featureResult.VertexData.Vertices[startVertex + k + 1];

                        //first point
                        
                        if(k > 0)
                        {
                            var prev = featureResult.VertexData.Vertices[startVertex + k - 1];
                            var dirNext = (next - current).normalized;
                            var dirPrev = (prev - current).normalized;
                            var dirInside = (dirNext + dirPrev).normalized * scaledRoadWidth;

                            var prevSideNormal = sideNormal;

                            sideNormal = new Vector3(dirNext.z  * scaledRoadWidth, 0, -dirNext.x  * scaledRoadWidth);
                            var isRight = Vector3.Dot(prevSideNormal, dirNext) > 0;


                            // decide previous triangle indices based on turnState
                            if (lastTurnWas == Turn.Right)
                            {
                                prevFirst = -1;
                                prevSecond = -2;
                            }
                            else if (lastTurnWas == Turn.Left || lastTurnWas == Turn.Start)
                            {
                                prevFirst = -2;
                                prevSecond = -1;
                            }

                            int baseVertIndex = subVertIndex;
                            int baseTriIndex = featureTriIndex + baseVertIndex;
                            
                            //featureTriIndex is the start of the feature
                            //baseVertIndex is the start of this section (like corner)

                            // precompute common y
                            var y = _settings.PushUp + current.y;

                            // precompute side offsets with sign depending on isRight
                            float sign = isRight ? -1f : 1f;

                            // v0: current ± prevSideNormal
                            float v0x = current.x + sign * prevSideNormal.x;
                            float v0z = current.z + sign * prevSideNormal.z;

                            // v2: current ± sideNormal
                            float v2x = current.x + sign * sideNormal.x;
                            float v2z = current.z + sign * sideNormal.z;

                            // v1: current - dirInside
                            float v1x = current.x - dirInside.x;
                            float v1z = current.z - dirInside.z;

                            // v3: current + dirInside
                            float v3x = current.x + dirInside.x;
                            float v3z = current.z + dirInside.z;

                            // write vertices
                            vertices[baseVertIndex    ] = new Vector3(v0x, y, v0z);
                            vertices[baseVertIndex + 1] = new Vector3(v1x, y, v1z);
                            vertices[baseVertIndex + 2] = new Vector3(v2x, y, v2z);
                            vertices[baseVertIndex + 3] = new Vector3(v3x, y, v3z);

                            // write normals (all up)
                            normals[baseVertIndex    ] =Vector3.up;
                            normals[baseVertIndex + 1] =Vector3.up;
                            normals[baseVertIndex + 2] =Vector3.up;
                            normals[baseVertIndex + 3] = Vector3.up;

                            // handy local vars for indices
                            int i0 = baseTriIndex + 0;
                            int i1 = baseTriIndex + 1;
                            int i2 = baseTriIndex + 2;
                            int i3 = baseTriIndex + 3;
                            int ipf = baseTriIndex + prevFirst;
                            int ips = baseTriIndex + prevSecond;

                            // prev tris + corner tris
                            if (isRight)
                            {
                                // prev tris
                                tris[subTriIndex++] = ipf;
                                tris[subTriIndex++] = ips;
                                tris[subTriIndex++] = i0;

                                tris[subTriIndex++] = i0;
                                tris[subTriIndex++] = i3;
                                tris[subTriIndex++] = ipf;

                                // corner tris
                                tris[subTriIndex++] = i0;
                                tris[subTriIndex++] = i1;
                                tris[subTriIndex++] = i3;

                                tris[subTriIndex++] = i1;
                                tris[subTriIndex++] = i2;
                                tris[subTriIndex++] = i3;

                                lastTurnWas = Turn.Right;
                            }
                            else
                            {
                                // prev tris
                                tris[subTriIndex++] = i0;
                                tris[subTriIndex++] = ipf;
                                tris[subTriIndex++] = ips;

                                tris[subTriIndex++] = i3;
                                tris[subTriIndex++] = i0;
                                tris[subTriIndex++] = ips;

                                // corner tris
                                tris[subTriIndex++] = i0;
                                tris[subTriIndex++] = i3;
                                tris[subTriIndex++] = i1;

                                tris[subTriIndex++] = i2;
                                tris[subTriIndex++] = i1;
                                tris[subTriIndex++] = i3;

                                lastTurnWas = Turn.Left;
                            }


                            subVertIndex += 4;

                            if (k == subSize - 2)
                            {
                                finishLine = true;
                            }
                        }
                        else if (k == 0)
                        {
                            var dir = (next - current).normalized;
                            sideNormal = new Vector3(dir.z * scaledRoadWidth, 0, -dir.x * scaledRoadWidth);

                            var side1x = current.x + sideNormal.x;
                            var side1z = current.z + sideNormal.z;
                            var side2x = current.x - sideNormal.x;
                            var side2z = current.z - sideNormal.z;
                            
                            var y = _settings.PushUp + current.y;
                            vertices[subVertIndex    ] = new Vector3(side1x, y, side1z);
                            vertices[subVertIndex + 1] = new Vector3(side2x, y, side2z);

                            normals[subVertIndex    ] = Vector3.up;
                            normals[subVertIndex + 1] = Vector3.up;

                            subVertIndex += 2;
                            lastTurnWas = 0;

                            if (subSize == 2)
                                finishLine = true;
                        }
                        
                        if (finishLine)
                        {
                            current = featureResult.VertexData.Vertices[startVertex + k + 1]; 
                            
                            if (lastTurnWas == Turn.Left || lastTurnWas == 0)
                            {
                                prevFirst = -1;
                                prevSecond = -2;
                            }
                            else if (lastTurnWas == Turn.Right)
                            {
                                prevFirst = -2;
                                prevSecond = -1;
                            }

                            var side1x = current.x + sideNormal.x;
                            var side1z = current.z + sideNormal.z;
                            var side2x = current.x - sideNormal.x;
                            var side2z = current.z - sideNormal.z;
                            
                            var y = _settings.PushUp + current.y;
                            vertices[subVertIndex    ] = new Vector3(side1x, y, side1z);
                            vertices[subVertIndex + 1] = new Vector3(side2x, y, side2z);

                            normals[subVertIndex    ] = Vector3.up;
                            normals[subVertIndex + 1] = Vector3.up;

                            var baseTriIndex = featureTriIndex + subVertIndex;
                            tris[subTriIndex++] = baseTriIndex + prevSecond;
                            tris[subTriIndex++] = baseTriIndex + prevFirst;
                            tris[subTriIndex++] = baseTriIndex + 0;

                            tris[subTriIndex++] = baseTriIndex + 0;
                            tris[subTriIndex++] = baseTriIndex + prevFirst;
                            tris[subTriIndex++] = baseTriIndex + 1;

                            subVertIndex += 2;
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
                submeshStarts.Add(total); //(size - 2) * 4 + (2 * 2); //two vertices for cap, 4 for each mid vertex
            }

            return submeshStarts;
        }

        private int VertexNeed(MeshVertexData data)
        {
            var total = 0;
            for (var i = 0; i < data.Submeshes.Count - 1; i++)
            {
                var size = data.Submeshes[i + 1] - data.Submeshes[i];
                total += 4 * size - 4; //(size - 2) * 4 + (2 * 2); //two vertices for cap, 4 for each mid vertex
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

        private RoadFeatureUnity GetFeature(PerfVectorTileLayer layer, int i)
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

            feature.SetProperties(layer);
            feature.VertexData = feature.Geometry(new Vector3(layerExtent, 0, -layerExtent));
            return feature;
        }

        private class RoadFeatureUnity
        {
            public ulong Id;
            public GeomType GeometryType;
            public uint[] GeometryCommands;
            public MeshVertexData VertexData;
            public int[] Tags;
            public string Class;
            public string Type;

            public void SetProperties(PerfVectorTileLayer layer)
            {
                var tagCount = Tags.Length;
                for (int i = 0; i < tagCount; i += 2)
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

            public MeshVertexData Geometry(Vector3 scale)
            {
                return PerformanceDecodeGeometry.GetGeometry(GeometryCommands, scale);
            }
        }
    }
}