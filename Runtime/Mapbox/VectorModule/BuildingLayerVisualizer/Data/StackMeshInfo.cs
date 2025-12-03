namespace Mapbox.VectorModule.BuildingLayerVisualizer
{
    public class StackMeshInfo
    {
        public int TotalPointCount;
        public int TotalTriangleCount;
        public int[] vertexRanges;
        public int[] vertexSize;
        public int[] triRanges;
        public int[] triSize;

        public StackMeshInfo(int featureCount)
        {
            vertexRanges = new int[featureCount];
            vertexSize = new int[featureCount];
            triRanges = new int[featureCount];
            triSize = new int[featureCount];
            TotalPointCount = 0;
        }

        public int VertexCount(int i)
        {
            if (i < vertexRanges.Length - 1)
            {
                return vertexRanges[i + 1] - vertexRanges[i];
            }
            else
            {
                return TotalPointCount - vertexRanges[i];
            }
        }
        
        public int TriCount(int i)
        {
            if (i < triRanges.Length - 1)
            {
                return triRanges[i + 1] - triRanges[i];
            }
            else
            {
                return TotalTriangleCount - triRanges[i];
            }
        }
    }
}