namespace Mapbox.VectorModule.BuildingLayerVisualizer
{
    public class StackMeshInfo
    {
        public int TotalPointCount;
        public int[] vertexRanges;
        public int[] triRanges;

        public StackMeshInfo(int featureCount)
        {
            vertexRanges = new int[featureCount];
            triRanges = new int[featureCount];
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
    }
}