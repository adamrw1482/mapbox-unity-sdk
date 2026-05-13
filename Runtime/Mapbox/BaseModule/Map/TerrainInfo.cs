using System;

namespace Mapbox.BaseModule.Map
{
    /// <summary>
    /// Runtime terrain elevation information for the current map view.
    /// Updated automatically as terrain tiles load. Provides observed elevation
    /// range and settings useful for styling, object placement, and culling.
    /// </summary>
    [Serializable]
    public class TerrainInfo
    {
        /// <summary>
        /// Whether terrain elevation is enabled. False when using flat terrain strategy.
        /// </summary>
        public bool IsEnabled;

        /// <summary>
        /// Lowest observed elevation in meters across all loaded terrain tiles.
        /// </summary>
        public float MinElevation;

        /// <summary>
        /// Highest observed elevation in meters across all loaded terrain tiles.
        /// </summary>
        public float MaxElevation;

        /// <summary>
        /// Vertical exaggeration factor applied to terrain elevation.
        /// 1.0 = real-world scale. Set by the terrain module during initialization.
        /// </summary>
        public float Exaggeration = 1f;
    }
}
