using Mapbox.BaseModule.Data.Vector2d;
using Mapbox.SearchApi.Response;

namespace Mapbox.SearchApi.Demo
{
    /// <summary>
    /// Data passed to <see cref="SearchBoxController.OnResultSelected"/> when the user
    /// picks a search result. Contains the display name, coordinates, and the raw feature.
    /// </summary>
    public struct SearchSelection
    {
        /// <summary>Display name of the selected place (e.g. "Michigan Stadium").</summary>
        public string Name;

        /// <summary>Full formatted address of the selected place.</summary>
        public string FullAddress;

        /// <summary>
        /// Geographic position of the selected place.
        /// <c>Coordinates.x</c> = latitude, <c>Coordinates.y</c> = longitude
        /// (matches the SDK's <c>LatitudeLongitude</c> convention).
        /// </summary>
        public LatitudeLongitude Coordinates;

        /// <summary>The raw GeoJSON feature returned by <c>/retrieve</c>.</summary>
        public SearchFeature Feature;
    }
}
