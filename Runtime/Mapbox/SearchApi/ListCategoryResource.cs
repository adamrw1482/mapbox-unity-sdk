//-----------------------------------------------------------------------
// <copyright file="ListCategoryResource.cs" company="Mapbox">
//     Copyright (c) 2024 Mapbox. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System.Collections.Generic;
using Mapbox.BaseModule.Data.Platform;
using Mapbox.BaseModule.Utilities;

namespace Mapbox.SearchApi
{
    /// <summary>
    /// Resource for the Search Box API <c>/list/category</c> endpoint.
    /// Returns all available POI category IDs and their localized names.
    /// Use the returned <c>canonical_id</c> values with <see cref="CategorySearchResource"/>.
    /// </summary>
    public sealed class ListCategoryResource : Resource
    {
        /// <summary>
        /// The ISO language code for category names in the response (e.g. "en", "fr").
        /// Defaults to English when omitted.
        /// </summary>
        [UnityEngine.Tooltip("ISO language code for returned category names, e.g. \"en\", \"fr\". Defaults to English.")]
        public string Language;

        public override string ApiEndpoint => "search/searchbox/v1/";

        /// <summary>Builds the complete /list/category request URL.</summary>
        public override string GetUrl()
        {
            var opts = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(Language))
                opts["language"] = Language;
            return Constants.Map.BaseAPI + ApiEndpoint + "list/category" + EncodeQueryString(opts);
        }
    }
}
