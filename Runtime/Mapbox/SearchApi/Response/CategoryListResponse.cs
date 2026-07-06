//-----------------------------------------------------------------------
// <copyright file="CategoryListResponse.cs" company="Mapbox">
//     Copyright (c) 2024 Mapbox. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mapbox.SearchApi.Response
{
    /// <summary>Response from the Search Box API <c>/list/category</c> endpoint.</summary>
    [Serializable]
    public class CategoryListResponse
    {
        /// <summary>All available POI categories with their canonical IDs and localized names.</summary>
        [JsonProperty("listItems")] public List<CategoryListItem> ListItems;

        /// <summary>Attribution string required for display per Mapbox Terms of Service.</summary>
        [JsonProperty("attribution")] public string Attribution;

        /// <summary>Service version information.</summary>
        [JsonProperty("version")] public string Version;
    }
}
