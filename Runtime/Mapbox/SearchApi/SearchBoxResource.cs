//-----------------------------------------------------------------------
// <copyright file="SearchBoxResource.cs" company="Mapbox">
//     Copyright (c) 2024 Mapbox. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Mapbox.BaseModule.Data;
using Mapbox.BaseModule.Data.Platform;
using Mapbox.BaseModule.Data.Vector2d;

namespace Mapbox.SearchApi
{
    /// <summary>
    /// Abstract base for all Search Box API resources.
    /// Provides the shared endpoint path and optional query parameters common across endpoints.
    /// Tokens are never included here — they are appended automatically by <c>IFileSource</c>.
    /// </summary>
    public abstract class SearchBoxResource : Resource
    {
        /// <summary>Valid feature types accepted by the <c>types</c> parameter.</summary>
        public static readonly List<string> ValidTypes = new List<string>
        {
            "country", "region", "postcode", "district", "place", "city",
            "locality", "neighborhood", "street", "address", "poi", "category"
        };

        private string[] _types;
        private string[] _country;

        /// <summary>The shared base path for all Search Box API endpoints.</summary>
        public override string ApiEndpoint => "search/searchbox/v1/";

        // ── Optional parameters shared across multiple endpoints ──────────────

        /// <summary>
        /// The ISO language code for results (e.g. "en", "fr", "ja").
        /// Defaults to English when omitted.
        /// </summary>
        [UnityEngine.Tooltip("ISO language code for results, e.g. \"en\", \"fr\", \"ja\". Defaults to English.")]
        public string Language;

        /// <summary>
        /// Maximum number of results to return. Endpoint-specific upper limits apply
        /// (10 for suggest/forward/reverse, 25 for category).
        /// </summary>
        [UnityEngine.Tooltip("Maximum number of results to return (up to 10 for most endpoints, 25 for category).")]
        public int? Limit;

        /// <summary>
        /// Bias results toward this location. Set to a lat/lng to prefer nearby results.
        /// <c>Vector2d.x</c> = latitude, <c>Vector2d.y</c> = longitude.
        /// </summary>
        [UnityEngine.Tooltip("Bias results toward this location. x=latitude, y=longitude.")]
        public Vector2d? Proximity;

        /// <summary>
        /// Restrict results to within this bounding box.
        /// Cannot cross the 180th meridian.
        /// </summary>
        [UnityEngine.Tooltip("Restrict results to within this bounding box. Cannot cross the 180th meridian.")]
        public LatitudeLongitudeBounds? Bbox;

        /// <summary>
        /// Restrict results to these ISO 3166-1 alpha-2 country codes (e.g. "us", "gb").
        /// </summary>
        public string[] Country
        {
            get => _country;
            set
            {
                if (value != null)
                {
                    foreach (var code in value)
                    {
                        if (string.IsNullOrEmpty(code) || code.Length != 2)
                            throw new ArgumentException(
                                $"Invalid country code \"{code}\". Must be an ISO 3166-1 alpha-2 code (two letters).");
                    }
                }
                _country = value;
            }
        }

        /// <summary>
        /// Restrict results to these feature types. Valid values:
        /// country, region, postcode, district, place, city, locality, neighborhood,
        /// street, address, poi, category.
        /// </summary>
        public string[] Types
        {
            get => _types;
            set
            {
                if (value != null)
                {
                    foreach (var t in value)
                    {
                        if (!ValidTypes.Contains(t))
                            throw new ArgumentException(
                                $"Invalid type \"{t}\". Must be one of: {string.Join(", ", ValidTypes)}.");
                    }
                }
                _types = value;
            }
        }

        /// <summary>
        /// Restrict POI results to these canonical category IDs (e.g. "coffee", "restaurant").
        /// </summary>
        [UnityEngine.Tooltip("Canonical POI category IDs to restrict results to, e.g. \"coffee\", \"restaurant\".")]
        public string[] PoiCategory;

        // ── Helpers for building query strings ────────────────────────────────

        /// <summary>
        /// Appends all non-null shared optional parameters to the given dictionary.
        /// Call this from <see cref="Resource.GetUrl"/> implementations.
        /// </summary>
        protected void AddSharedOptions(Dictionary<string, string> opts)
        {
            if (!string.IsNullOrEmpty(Language))
                opts["language"] = Language;
            if (Limit.HasValue)
                opts["limit"] = Limit.Value.ToString();
            if (Proximity.HasValue)
                // Vector2d.ToString() formats as "{y},{x}" (lon,lat) via NumberFormatInfo.InvariantInfo —
                // culture-safe, unlike interpolating the raw doubles directly.
                opts["proximity"] = Proximity.Value.ToString();
            if (Bbox.HasValue)
                // SW/NE corners individually formatted lon,lat (invariant culture) via ToStringLonLat(),
                // matching the API's documented min_lon,min_lat,max_lon,max_lat bbox order.
                opts["bbox"] = $"{Bbox.Value.SouthWest.ToStringLonLat()},{Bbox.Value.NorthEast.ToStringLonLat()}";
            if (Country != null && Country.Length > 0)
                opts["country"] = GetUrlQueryFromArray(Country);
            if (Types != null && Types.Length > 0)
                opts["types"] = GetUrlQueryFromArray(Types);
            if (PoiCategory != null && PoiCategory.Length > 0)
                opts["poi_category"] = GetUrlQueryFromArray(PoiCategory);
        }
    }
}
