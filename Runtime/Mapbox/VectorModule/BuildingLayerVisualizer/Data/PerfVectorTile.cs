using System;

namespace Mapbox.VectorModule.BuildingLayerVisualizer
{
    public ref struct PerfVectorTile
    {
        private PerfVectorTileReader _VTR;
        
        public PerfVectorTile(ReadOnlySpan<byte> data)
        {
            _VTR = new PerfVectorTileReader(ref data);
        }

        public bool TryGetLayer(string layerName, out PerfVectorTileLayer layer)
        {
	        return _VTR.TryGetLayer(layerName, out layer);
        }
    }
}