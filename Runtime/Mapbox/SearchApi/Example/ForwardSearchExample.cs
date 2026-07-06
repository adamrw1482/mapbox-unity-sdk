//-----------------------------------------------------------------------
// <copyright file="ForwardSearchExample.cs" company="Mapbox">
//     Copyright (c) 2024 Mapbox. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Utilities;
using Mapbox.SearchApi.Response;
using UnityEngine;

namespace Mapbox.SearchApi.Example
{
    /// <summary>
    /// In-assembly example demonstrating a one-off <c>/forward</c> text search.
    /// Unlike the suggest/retrieve flow, each call is billed per request (no session token needed).
    /// Results are logged to the Console.
    /// Attach to a GameObject in a scene that also has a <see cref="MapBehaviourCore"/>.
    /// </summary>
    public class ForwardSearchExample : MonoBehaviour
    {
        [Header("Map Reference")]
        [Tooltip("The map behaviour that provides the authenticated IFileSource.")]
        public MapBehaviourCore MapCore;

        [Header("Search Query")]
        [Tooltip("The address or place name to search for.")]
        public string SearchQuery = "1201 S Main St, Ann Arbor, MI";

        [Header("Optional Filters")]
        [Tooltip("ISO language code for results, e.g. \"en\", \"fr\".")]
        public string Language = "en";

        [Tooltip("Maximum number of results to return (1–10).")]
        public int Limit = 5;

        [Tooltip("Enable autocomplete mode to include partial and fuzzy matches.")]
        public bool AutoComplete;

        [Tooltip("Restrict results to these ISO 3166-1 alpha-2 country codes.")]
        public string[] Country;

        private MapboxSearchApi _searchApi;

        void Start()
        {
            if (MapCore == null)
            {
                Debug.LogError("[ForwardSearchExample] MapCore is not assigned.");
                return;
            }
            MapCore.Initialized += OnMapInitialized;
        }

        void OnMapInitialized(MapboxMap map)
        {
            _searchApi = new MapboxSearchApi(map.MapService.FileSource);
            Debug.Log("[ForwardSearchExample] Initialized. Searching for: " + SearchQuery);
            Search();
        }

        /// <summary>Execute a forward search. Safe to call from a Button onClick.</summary>
        public void Search()
        {
            if (_searchApi == null)
            {
                Debug.LogWarning("[ForwardSearchExample] Not initialized yet.");
                return;
            }

            var resource = new ForwardSearchResource(SearchQuery)
            {
                Language    = Language,
                Limit       = Limit,
                AutoComplete = AutoComplete ? (bool?)true : null,
                Country     = Country
            };

            _searchApi.Forward(resource, OnResponse);
        }

        void OnResponse(SearchFeatureCollection collection)
        {
            if (collection?.Features == null || collection.Features.Count == 0)
            {
                Debug.LogWarning("[ForwardSearchExample] No results for: " + SearchQuery);
                return;
            }

            Debug.Log($"[ForwardSearchExample] {collection.Features.Count} result(s):");
            foreach (var f in collection.Features)
            {
                var p = f.Properties;
                Debug.Log($"  • {p.Name}  |  {p.FullAddress}");
                Debug.Log($"    Lat: {p.Coordinates?.Latitude}  Lon: {p.Coordinates?.Longitude}");
            }
        }

        void OnDestroy()
        {
            if (MapCore != null)
                MapCore.Initialized -= OnMapInitialized;
        }
    }
}
