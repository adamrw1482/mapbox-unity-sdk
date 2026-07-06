//-----------------------------------------------------------------------
// <copyright file="ForwardSearchResource.cs" company="Mapbox">
//     Copyright (c) 2024 Mapbox. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Mapbox.BaseModule.Utilities;

namespace Mapbox.SearchApi
{
    /// <summary>
    /// Resource for the Search Box API <c>/forward</c> endpoint.
    /// One-off text search that returns a GeoJSON FeatureCollection with coordinates directly.
    /// Unlike <see cref="SuggestResource"/>/<see cref="RetrieveResource"/>, this is billed per request.
    /// Enable <see cref="AutoComplete"/> to get partial/fuzzy matches while the user types.
    /// </summary>
    public sealed class ForwardSearchResource : SearchBoxResource
    {
        private readonly string _query;

        /// <summary>
        /// When <c>true</c>, enables autocomplete mode — partial and fuzzy matches are included.
        /// Suitable for type-ahead implementations that don't require the suggest/retrieve session flow.
        /// </summary>
        [UnityEngine.Tooltip("Enable autocomplete mode to include partial and fuzzy matches.")]
        public bool? AutoComplete;

        /// <summary>
        /// Initializes a new forward search request.
        /// </summary>
        /// <param name="query">The search text. Limited to 256 characters.</param>
        public ForwardSearchResource(string query)
        {
            if (string.IsNullOrEmpty(query))
                throw new ArgumentException("Query must not be null or empty.", nameof(query));
            _query = query;
        }

        /// <summary>Builds the complete /forward request URL.</summary>
        public override string GetUrl()
        {
            var opts = new Dictionary<string, string> { { "q", _query } };
            if (AutoComplete.HasValue)
                opts["auto_complete"] = AutoComplete.Value.ToString().ToLower();
            AddSharedOptions(opts);
            return Constants.Map.BaseAPI + ApiEndpoint + "forward" + EncodeQueryString(opts);
        }
    }
}
