//-----------------------------------------------------------------------
// <copyright file="SearchGeometry.cs" company="Mapbox">
//     Copyright (c) 2024 Mapbox. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using Mapbox.BaseModule.Data.Vector2d;
using Mapbox.BaseModule.Utilities.JsonConverters;
using Newtonsoft.Json;

namespace Mapbox.SearchApi.Response
{
    /// <summary>GeoJSON Point geometry of a search result feature.</summary>
    [Serializable]
    public class SearchGeometry
    {
        /// <summary>Always "Point" for Search Box API results.</summary>
        [JsonProperty("type")] public string Type;

        /// <summary>
        /// Geographic position of the feature.
        /// The API returns <c>[longitude, latitude]</c> (GeoJSON order);
        /// the converter maps this to <c>Vector2d(x: latitude, y: longitude)</c>.
        /// </summary>
        [JsonConverter(typeof(LonLatToVector2dConverter))]
        [JsonProperty("coordinates")]
        public Vector2d Coordinates;
    }
}
