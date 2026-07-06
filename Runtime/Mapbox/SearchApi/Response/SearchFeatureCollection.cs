//-----------------------------------------------------------------------
// <copyright file="SearchFeatureCollection.cs" company="Mapbox">
//     Copyright (c) 2024 Mapbox. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mapbox.SearchApi.Response
{
    /// <summary>
    /// GeoJSON FeatureCollection returned by <c>/retrieve</c>, <c>/forward</c>,
    /// <c>/reverse</c>, and <c>/category</c> endpoints.
    /// Includes geographic coordinates in each feature.
    /// </summary>
    [Serializable]
    public class SearchFeatureCollection
    {
        /// <summary>Always "FeatureCollection".</summary>
        [JsonProperty("type")] public string Type;

        /// <summary>The returned features.</summary>
        [JsonProperty("features")] public List<SearchFeature> Features;

        /// <summary>Attribution string required for display per Mapbox Terms of Service.</summary>
        [JsonProperty("attribution")] public string Attribution;
    }
}
