//-----------------------------------------------------------------------
// <copyright file="CategorySearchResource.cs" company="Mapbox">
//     Copyright (c) 2024 Mapbox. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Mapbox.BaseModule.Utilities;

namespace Mapbox.SearchApi
{
    /// <summary>
    /// Resource for the Search Box API <c>/category/{id}</c> endpoint.
    /// Returns POIs matching a canonical category ID around a given location.
    /// Use <see cref="ListCategoryResource"/> to fetch the full list of valid category IDs.
    /// </summary>
    public sealed class CategorySearchResource : SearchBoxResource
    {
        private readonly string _categoryId;

        /// <summary>
        /// Initializes a new category search request.
        /// </summary>
        /// <param name="categoryId">
        /// Canonical category ID, e.g. "coffee", "gas_station", "restaurant".
        /// Retrieve valid IDs via <see cref="ListCategoryResource"/>.
        /// </param>
        public CategorySearchResource(string categoryId)
        {
            if (string.IsNullOrEmpty(categoryId))
                throw new ArgumentException("CategoryId must not be null or empty.", nameof(categoryId));
            _categoryId = categoryId;
        }

        /// <summary>Builds the complete /category/{id} request URL.</summary>
        public override string GetUrl()
        {
            var opts = new Dictionary<string, string>();
            AddSharedOptions(opts);
            return Constants.Map.BaseAPI
                + ApiEndpoint
                + "category/"
                + Uri.EscapeDataString(_categoryId)
                + EncodeQueryString(opts);
        }
    }
}
