//-----------------------------------------------------------------------
// <copyright file="Suggestion.cs" company="Mapbox">
//     Copyright (c) 2024 Mapbox. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mapbox.SearchApi.Response
{
    /// <summary>
    /// A single autocomplete suggestion returned by the <c>/suggest</c> endpoint.
    /// Suggestions do NOT include geographic coordinates — call <c>/retrieve</c>
    /// with the <see cref="MapboxId"/> to get coordinates when the user selects this result.
    /// </summary>
    [Serializable]
    public class Suggestion
    {
        [JsonProperty("name")]            public string Name;
        [JsonProperty("name_preferred")]  public string NamePreferred;

        /// <summary>
        /// Pass this to <see cref="RetrieveResource"/> to get full feature details
        /// (including coordinates) when the user selects this suggestion.
        /// </summary>
        [JsonProperty("mapbox_id")]       public string MapboxId;

        [JsonProperty("feature_type")]    public string FeatureType;

        /// <summary>Address number and street name (e.g. "1201 S Main St").</summary>
        [JsonProperty("address")]         public string Address;

        /// <summary>Full address string combining address and place context.</summary>
        [JsonProperty("full_address")]    public string FullAddress;

        /// <summary>Formatted place context (city, region, country, postcode).</summary>
        [JsonProperty("place_formatted")] public string PlaceFormatted;

        [JsonProperty("context")]         public SearchContext Context;

        /// <summary>IETF language tag of the result (e.g. "en").</summary>
        [JsonProperty("language")]        public string Language;

        /// <summary>Maki icon identifier for this result type (e.g. "marker", "restaurant").</summary>
        [JsonProperty("maki")]            public string Maki;

        [JsonProperty("poi_category")]     public List<string> PoiCategory;
        [JsonProperty("poi_category_ids")] public List<string> PoiCategoryIds;
        [JsonProperty("brand")]            public List<string> Brand;
        [JsonProperty("brand_id")]         public List<string> BrandId;
        [JsonProperty("external_ids")]     public Dictionary<string, string> ExternalIds;

        /// <summary>
        /// Approximate distance to the <c>proximity</c> or <c>origin</c> point, in metres.
        /// Only present when a proximity/origin is supplied in the request.
        /// </summary>
        [JsonProperty("distance")] public double? Distance;

        /// <summary>
        /// Estimated time of arrival from origin/proximity point, in minutes.
        /// Only present when ETA parameters are supplied.
        /// </summary>
        [JsonProperty("eta")] public double? Eta;
    }
}
