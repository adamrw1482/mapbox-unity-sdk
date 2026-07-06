//-----------------------------------------------------------------------
// <copyright file="SearchFeature.cs" company="Mapbox">
//     Copyright (c) 2024 Mapbox. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using Newtonsoft.Json;

namespace Mapbox.SearchApi.Response
{
    /// <summary>
    /// A GeoJSON Feature returned by <c>/retrieve</c>, <c>/forward</c>,
    /// <c>/reverse</c>, and <c>/category</c> endpoints.
    /// </summary>
    [Serializable]
    public class SearchFeature
    {
        /// <summary>Always "Feature".</summary>
        [JsonProperty("type")] public string Type;

        /// <summary>
        /// GeoJSON Point geometry.
        /// <c>Geometry.Coordinates.x</c> = latitude, <c>Geometry.Coordinates.y</c> = longitude.
        /// </summary>
        [JsonProperty("geometry")] public SearchGeometry Geometry;

        /// <summary>Feature metadata including name, address, coordinates, and context.</summary>
        [JsonProperty("properties")] public SearchProperties Properties;
    }
}
