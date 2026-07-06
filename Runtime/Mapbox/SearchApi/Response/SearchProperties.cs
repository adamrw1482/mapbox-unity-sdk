//-----------------------------------------------------------------------
// <copyright file="SearchProperties.cs" company="Mapbox">
//     Copyright (c) 2024 Mapbox. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Mapbox.BaseModule.Data;
using Mapbox.BaseModule.Data.Vector2d;
using Mapbox.BaseModule.Utilities.JsonConverters;
using Newtonsoft.Json;

namespace Mapbox.SearchApi.Response
{
    /// <summary>
    /// Feature properties returned by <c>/retrieve</c>, <c>/forward</c>,
    /// <c>/reverse</c>, and <c>/category</c> endpoints.
    /// </summary>
    [Serializable]
    public class SearchProperties
    {
        [JsonProperty("name")]            public string Name;
        [JsonProperty("name_preferred")]  public string NamePreferred;
        [JsonProperty("mapbox_id")]       public string MapboxId;
        [JsonProperty("feature_type")]    public string FeatureType;

        /// <summary>Address number and street name combined (e.g. "1201 S Main St").</summary>
        [JsonProperty("address")]         public string Address;

        /// <summary>Full formatted address concatenating <see cref="Address"/> and <see cref="PlaceFormatted"/>.</summary>
        [JsonProperty("full_address")]    public string FullAddress;

        /// <summary>Place, region, country, and postcode portion of the address.</summary>
        [JsonProperty("place_formatted")] public string PlaceFormatted;

        [JsonProperty("context")]         public SearchContext Context;

        /// <summary>Geographic coordinates. Use this for routing and map display.</summary>
        [JsonProperty("coordinates")]     public SearchCoordinates Coordinates;

        /// <summary>
        /// Bounding box: [min_lon, min_lat, max_lon, max_lat].
        /// Converted to <see cref="LatitudeLongitudeBounds"/> by the shared bbox converter.
        /// </summary>
        [JsonConverter(typeof(BboxToVector2dBoundsConverter))]
        [JsonProperty("bbox", NullValueHandling = NullValueHandling.Ignore)]
        public LatitudeLongitudeBounds? Bbox;

        [JsonProperty("language")] public string Language;

        /// <summary>Maki icon identifier (e.g. "restaurant", "marker").</summary>
        [JsonProperty("maki")]     public string Maki;

        [JsonProperty("poi_category")]     public List<string> PoiCategory;
        [JsonProperty("poi_category_ids")] public List<string> PoiCategoryIds;
        [JsonProperty("brand")]            public List<string> Brand;
        [JsonProperty("brand_id")]         public List<string> BrandId;
        [JsonProperty("external_ids")]     public Dictionary<string, string> ExternalIds;
    }
}
