//-----------------------------------------------------------------------
// <copyright file="CategoryListItem.cs" company="Mapbox">
//     Copyright (c) 2024 Mapbox. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using Newtonsoft.Json;

namespace Mapbox.SearchApi.Response
{
    /// <summary>A single POI category entry returned by the <c>/list/category</c> endpoint.</summary>
    [Serializable]
    public class CategoryListItem
    {
        /// <summary>
        /// The canonical category ID to use with <see cref="CategorySearchResource"/>
        /// (e.g. "coffee", "gas_station", "restaurant").
        /// </summary>
        [JsonProperty("canonical_id")] public string CanonicalId;

        /// <summary>Maki icon identifier for this category.</summary>
        [JsonProperty("icon")] public string Icon;

        /// <summary>Human-readable category name in the requested language.</summary>
        [JsonProperty("name")] public string Name;
    }
}
