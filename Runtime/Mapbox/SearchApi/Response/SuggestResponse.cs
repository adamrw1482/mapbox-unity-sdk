//-----------------------------------------------------------------------
// <copyright file="SuggestResponse.cs" company="Mapbox">
//     Copyright (c) 2024 Mapbox. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mapbox.SearchApi.Response
{
    /// <summary>
    /// Response from the Search Box API <c>/suggest</c> endpoint.
    /// Contains a list of suggestions without geographic coordinates.
    /// Call <c>/retrieve</c> with the selected suggestion's <c>MapboxId</c> to get coordinates.
    /// </summary>
    [Serializable]
    public class SuggestResponse
    {
        /// <summary>The list of autocomplete suggestions.</summary>
        [JsonProperty("suggestions")] public List<Suggestion> Suggestions;

        /// <summary>Attribution string required for display per Mapbox Terms of Service.</summary>
        [JsonProperty("attribution")] public string Attribution;
    }
}
