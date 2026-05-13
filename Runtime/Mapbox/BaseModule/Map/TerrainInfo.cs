using System;

namespace Mapbox.BaseModule.Map
{
    /// <summary>
    /// Runtime terrain elevation information for the current map view.
    /// Updated automatically as terrain tiles load and unload.
    /// </summary>
    [Serializable]
    public class TerrainInfo
    {
        /// <summary>
        /// Lowest observed elevation in meters across currently loaded terrain tiles.
        /// </summary>
        public float MinElevation;

        /// <summary>
        /// Highest observed elevation in meters across currently loaded terrain tiles.
        /// </summary>
        public float MaxElevation;
    }
}
