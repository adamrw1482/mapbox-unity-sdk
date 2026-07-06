//-----------------------------------------------------------------------
// <copyright file="SuggestResource.cs" company="Mapbox">
//     Copyright (c) 2024 Mapbox. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Mapbox.BaseModule.Utilities;

namespace Mapbox.SearchApi
{
    /// <summary>
    /// Resource for the Search Box API <c>/suggest</c> endpoint.
    /// Use this together with <see cref="RetrieveResource"/> to build an interactive
    /// autocomplete search experience. Billed per session (not per request).
    /// </summary>
    public sealed class SuggestResource : SearchBoxResource
    {
        private readonly string _query;
        private readonly string _sessionToken;

        /// <summary>
        /// Initializes a new suggest request.
        /// </summary>
        /// <param name="query">
        /// The user's partial or full search text. Limited to 256 characters.
        /// </param>
        /// <param name="sessionToken">
        /// A UUIDv4 session token grouping related suggest/retrieve calls for billing.
        /// Each concurrent search session must use a distinct value.
        /// </param>
        public SuggestResource(string query, string sessionToken)
        {
            if (string.IsNullOrEmpty(query))
                throw new ArgumentException("Query must not be null or empty.", nameof(query));
            if (string.IsNullOrEmpty(sessionToken))
                throw new ArgumentException("SessionToken must not be null or empty.", nameof(sessionToken));

            _query = query;
            _sessionToken = sessionToken;
        }

        /// <summary>Builds the complete /suggest request URL.</summary>
        public override string GetUrl()
        {
            var opts = new Dictionary<string, string>
            {
                { "q", _query },
                { "session_token", _sessionToken }
            };
            AddSharedOptions(opts);
            return Constants.Map.BaseAPI + ApiEndpoint + "suggest" + EncodeQueryString(opts);
        }
    }
}
