//-----------------------------------------------------------------------
// <copyright file="SearchCoordinates.cs" company="Mapbox">
//     Copyright (c) 2024 Mapbox. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using Newtonsoft.Json;

namespace Mapbox.SearchApi.Response
{
    /// <summary>
    /// Geographic coordinates of a search result as returned by <c>/retrieve</c>,
    /// <c>/forward</c>, <c>/reverse</c>, and <c>/category</c> endpoints.
    /// Unlike the GeoJSON <c>geometry.coordinates</c> array, this object uses named fields.
    /// </summary>
    [Serializable]
    public class SearchCoordinates
    {
        /// <summary>Longitudinal coordinate of the result.</summary>
        [JsonProperty("longitude")] public double Longitude;

        /// <summary>Latitudinal coordinate of the result.</summary>
        [JsonProperty("latitude")] public double Latitude;

        /// <summary>
        /// Accuracy of the coordinate. Available for address results.
        /// Values: rooftop, parcel, point, interpolated, intersection, approximate, street.
        /// </summary>
        [JsonProperty("accuracy")] public string Accuracy;
    }
}
