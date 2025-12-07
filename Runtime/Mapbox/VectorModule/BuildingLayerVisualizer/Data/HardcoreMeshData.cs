using System.Collections.Generic;
using UnityEngine;

namespace Mapbox.VectorModule.BuildingLayerVisualizer
{
    public class HardcoreMeshData
    {
        public Vector3[] Vertices;
        public Vector3[] Normals;
        public Vector2[] UVs;
        public List<int[]> Triangles;
        public StackMeshInfo MeshInfo;
        
        public HardcoreMeshData(StackMeshInfo meshInfo, int featureCount)
        {
            MeshInfo = meshInfo;
            Vertices = new Vector3[featureCount];
            Normals = new Vector3[featureCount];
            Triangles = new List<int[]>(15);
        }
    }
}