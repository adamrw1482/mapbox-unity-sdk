using System;
using System.Runtime.CompilerServices;
using Mapbox.BaseModule.Data.Interfaces;
using Mapbox.BaseModule.Map;
using Mapbox.VectorModule.MeshGeneration.MeshModifiers;
using UnityEngine;

namespace Mapbox.VectorModule.BuildingLayerVisualizer
{
    public class ArrayHeight : IPerformanceExtrusion
    {
        private GeometryExtrusionOptions _settings;
        private int _startIndex;
        
        public ArrayHeight(GeometryExtrusionOptions settings)
        {
            _settings = settings;
        }
        
        public int Run(Span<Vector3> vertices, Span<Vector3> normals, int vertexAnchorIndex, int[] triList, int triIndex, PerfVectorFeatureUnity feature, float tileSizeX, IMapInformation mapInformation)
        {
            if (feature == null || feature.VertexData.Submeshes.Count < 1)
                return triIndex;

            _startIndex = vertexAnchorIndex;
            
            var height = feature.Height;
            var minHeight = feature.MinHeight;

            height = (height / mapInformation.Scale) / tileSizeX;
            minHeight = (minHeight / mapInformation.Scale) / tileSizeX;

            var max = 0f;
            var min = 0f;
            for (int i = 0; i < vertices.Length; i++)
            {
                if (vertices[i].y > max)
                    max = vertices[i].y;
                else if (vertices[i].y < min)
                    min = vertices[i].y;
            }
            height = max + height;
            
            GenerateRoofMesh(vertices, height);
            triIndex = GenerateWallMesh(vertices, normals, feature, triList, triIndex, height);
            return triIndex;
        }

        private int GenerateWallMesh(Span<Vector3> vertices, Span<Vector3> normals, PerfVectorFeatureUnity feature, int[] trilist, int triIndex, float height)
        {
            Vector3 curr, next;
            int addedPointCount = 0;
            int topPolygonVertexCount = feature.VertexData.VertexCount;
            
            var verts = feature.VertexData.Vertices;
            var submeshes = feature.VertexData.Submeshes;
            
            for (int i = 1; i < submeshes.Count; i++)
            {
                int start = submeshes[i - 1];
                int end = submeshes[i];
                int count = end - start;
                var subSpan = verts.AsSpan(start, count);
                var vertSpan = vertices.Slice(topPolygonVertexCount + start * 4, count * 4);
                var normSpan = normals.Slice(topPolygonVertexCount + start * 4, count * 4);
                Vector3 v1, n1;
                
                for (int j = 0; j < count; j++)
                {
                    int jNext = (j == count - 1) ? 0 : j + 1;
                    
                    curr = subSpan[j];
                    next = subSpan[jNext];

                    v1.x = next.x - curr.x;
                    v1.y = 0f;
                    v1.z = next.z - curr.z;
                    v1 = NormalizeXZ(v1);
                    n1 = PerpXZ(v1);
                    
                    int vertBase = (4*j);
                    
                    Vector3 vertCurr = vertices[start + j];
                    float yTop = vertCurr.y;
                    float yMin = 0;
                    
                    //next(1)---------curr(0)
                    // |                |
                    //nextBot(3)----currBot(2)
                    vertSpan[vertBase]     = new Vector3(curr.x, yTop, curr.z);
                    vertSpan[vertBase + 1] = new Vector3(next.x, yTop, next.z);
                    vertSpan[vertBase + 2] = new Vector3(curr.x, yMin, curr.z);
                    vertSpan[vertBase + 3] = new Vector3(next.x, yMin, next.z);
                    
                    normSpan[vertBase]     = n1;
                    normSpan[vertBase + 1] = n1;
                    normSpan[vertBase + 2] = n1;
                    normSpan[vertBase + 3] = n1;
                    
                    // ---- triangles ----
                    int si = _startIndex;
                    int baseA = si + topPolygonVertexCount + addedPointCount;
                    int baseB = si + topPolygonVertexCount + addedPointCount + vertBase;
                    
                    bool notLast = j < count - 1;
                    if (notLast)
                    {
                        trilist[triIndex++] = baseB;
                        trilist[triIndex++] = baseB + 2;
                        trilist[triIndex++] = baseB + 1;
                        
                        trilist[triIndex++] = baseB + 1;
                        trilist[triIndex++] = baseB + 2;
                        trilist[triIndex++] = baseB + 3;
                    }
                    else
                    {
                        trilist[triIndex++] = baseB;
                        trilist[triIndex++] = baseB + 2;
                        trilist[triIndex++] = baseA;
                        
                        trilist[triIndex++] = baseA;
                        trilist[triIndex++] = baseB + 2;
                        trilist[triIndex++] = baseA + 2;
                    }
                }

                addedPointCount += 4 * subSpan.Length;
            }

            return triIndex;
        }

        public void GenerateRoofMesh(Span<Vector3> vertices, float maxHeight)
        {
            var counter = vertices.Length;
            for (int i = 0; i < counter; i++)
            {
                vertices[i] = new Vector3(vertices[i].x, vertices[i].y + maxHeight, vertices[i].z);
            }
        }
        
        public int CalculateTriCountFor(int totalPointCount)
        {
            return totalPointCount * 6;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 NormalizeXZ(Vector3 v)
        {
            float invLen = 1.0f / Mathf.Sqrt(v.x * v.x + v.z * v.z + 1e-12f);
            v.x *= invLen;
            v.z *= invLen;
            return v;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 PerpXZ(Vector3 v) => new Vector3(-v.z, 0f, v.x);
    }
}