//-----------------------------------------------------------------------
// <copyright file="SearchContext.cs" company="Mapbox">
//     Copyright (c) 2024 Mapbox. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using Newtonsoft.Json;

namespace Mapbox.SearchApi.Response
{
    /// <summary>Administrative context of a search result.</summary>
    [Serializable]
    public class SearchContext
    {
        [JsonProperty("country")]  public SearchContextCountry  Country;
        [JsonProperty("region")]   public SearchContextRegion   Region;
        [JsonProperty("postcode")] public SearchContextItem     Postcode;
        [JsonProperty("district")] public SearchContextItem     District;
        [JsonProperty("place")]    public SearchContextItem     Place;
        [JsonProperty("locality")] public SearchContextItem     Locality;
        [JsonProperty("neighborhood")] public SearchContextItem Neighborhood;
        [JsonProperty("street")]   public SearchContextItem     Street;
        [JsonProperty("address")]  public SearchContextAddress  Address;
    }

    /// <summary>Base context layer with an id and display name.</summary>
    [Serializable]
    public class SearchContextItem
    {
        [JsonProperty("id")]   public string Id;
        [JsonProperty("name")] public string Name;
    }

    /// <summary>Country context layer. Includes ISO 3166-1 codes.</summary>
    [Serializable]
    public class SearchContextCountry : SearchContextItem
    {
        [JsonProperty("country_code")]       public string CountryCode;
        [JsonProperty("country_code_alpha_3")] public string CountryCodeAlpha3;
    }

    /// <summary>Region (state/province) context layer. Includes ISO 3166-2 codes.</summary>
    [Serializable]
    public class SearchContextRegion : SearchContextItem
    {
        [JsonProperty("region_code")]      public string RegionCode;
        [JsonProperty("region_code_full")] public string RegionCodeFull;
    }

    /// <summary>Address context layer. Includes street number and street name.</summary>
    [Serializable]
    public class SearchContextAddress : SearchContextItem
    {
        [JsonProperty("address_number")] public string AddressNumber;
        [JsonProperty("street_name")]    public string StreetName;
    }
}
