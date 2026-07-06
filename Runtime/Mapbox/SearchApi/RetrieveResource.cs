//-----------------------------------------------------------------------
// <copyright file="RetrieveResource.cs" company="Mapbox">
//     Copyright (c) 2024 Mapbox. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Mapbox.BaseModule.Utilities;

namespace Mapbox.SearchApi
{
    /// <summary>
    /// Resource for the Search Box API <c>/retrieve/{id}</c> endpoint.
    /// Call this when the user selects a result from a <see cref="SuggestResource"/> response.
    /// Returns a GeoJSON FeatureCollection containing geographic coordinates.
    /// The <c>session_token</c> must match the one used in the preceding suggest call.
    /// </summary>
    public sealed class RetrieveResource : SearchBoxResource
    {
        private readonly string _mapboxId;
        private readonly string _sessionToken;

        /// <summary>
        /// Initializes a new retrieve request.
        /// </summary>
        /// <param name="mapboxId">
        /// The <c>mapbox_id</c> from the suggestion to retrieve. Passed as a URL path segment.
        /// </param>
        /// <param name="sessionToken">
        /// The UUIDv4 session token used in the preceding <c>/suggest</c> calls.
        /// </param>
        public RetrieveResource(string mapboxId, string sessionToken)
        {
            if (string.IsNullOrEmpty(mapboxId))
                throw new ArgumentException("MapboxId must not be null or empty.", nameof(mapboxId));
            if (string.IsNullOrEmpty(sessionToken))
                throw new ArgumentException("SessionToken must not be null or empty.", nameof(sessionToken));

            _mapboxId = mapboxId;
            _sessionToken = sessionToken;
        }

        /// <summary>Builds the complete /retrieve/{id} request URL.</summary>
        public override string GetUrl()
        {
            var opts = new Dictionary<string, string>
            {
                { "session_token", _sessionToken }
            };
            if (!string.IsNullOrEmpty(Language))
                opts["language"] = Language;

            return Constants.Map.BaseAPI
                + ApiEndpoint
                + "retrieve/"
                + Uri.EscapeDataString(_mapboxId)
                + EncodeQueryString(opts);
        }
    }
}
