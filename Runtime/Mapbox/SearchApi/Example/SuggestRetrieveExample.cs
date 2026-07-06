//-----------------------------------------------------------------------
// <copyright file="SuggestRetrieveExample.cs" company="Mapbox">
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
    /// In-assembly example demonstrating the interactive autocomplete flow:
    /// <c>/suggest</c> → user selects → <c>/retrieve</c>.
    /// Results are logged to the Console; no UI is required.
    /// Attach to a GameObject in a scene that also has a <see cref="MapBehaviourCore"/>.
    /// </summary>
    public class SuggestRetrieveExample : MonoBehaviour
    {
        [Header("Map Reference")]
        [Tooltip("The map behaviour that provides the authenticated IFileSource.")]
        public MapBehaviourCore MapCore;

        [Header("Search Query")]
        [Tooltip("The search text to use for autocomplete suggestions.")]
        public string SearchQuery = "Michigan Stadium";

        [Header("Optional Filters")]
        [Tooltip("ISO language code for results, e.g. \"en\", \"fr\".")]
        public string Language = "en";

        [Tooltip("Maximum number of suggestions to request (1–10).")]
        public int Limit = 5;

        [Tooltip("Restrict results to these ISO 3166-1 alpha-2 country codes.")]
        public string[] Country;

        private MapboxSearchApi _searchApi;
        private SearchSession _session;

        void Start()
        {
            if (MapCore == null)
            {
                Debug.LogError("[SuggestRetrieveExample] MapCore is not assigned.");
                return;
            }
            MapCore.Initialized += OnMapInitialized;
        }

        void OnMapInitialized(MapboxMap map)
        {
            _searchApi = new MapboxSearchApi(map.MapService.FileSource);
            _session   = new SearchSession();
            Debug.Log("[SuggestRetrieveExample] Initialized. Querying: " + SearchQuery);
            PerformSearch();
        }

        /// <summary>Trigger a suggest → retrieve cycle. Safe to call from a Button onClick.</summary>
        public void PerformSearch()
        {
            if (_searchApi == null)
            {
                Debug.LogWarning("[SuggestRetrieveExample] Not initialized yet.");
                return;
            }

            var resource = new SuggestResource(SearchQuery, _session.SessionToken)
            {
                Language = Language,
                Limit    = Limit,
                Country  = Country
            };

            _searchApi.Suggest(resource, OnSuggestResponse);
        }

        void OnSuggestResponse(SuggestResponse response)
        {
            if (response?.Suggestions == null || response.Suggestions.Count == 0)
            {
                Debug.LogWarning("[SuggestRetrieveExample] No suggestions for: " + SearchQuery);
                return;
            }

            Debug.Log($"[SuggestRetrieveExample] {response.Suggestions.Count} suggestion(s):");
            foreach (var s in response.Suggestions)
                Debug.Log($"  • {s.Name}  {s.PlaceFormatted}  [{s.FeatureType}]");

            // Auto-retrieve the first result to demonstrate the full flow.
            var first = response.Suggestions[0];
            Debug.Log($"[SuggestRetrieveExample] Retrieving: {first.Name}");
            _searchApi.Retrieve(
                new RetrieveResource(first.MapboxId, _session.SessionToken),
                OnRetrieveResponse);
        }

        void OnRetrieveResponse(SearchFeatureCollection collection)
        {
            if (collection?.Features == null || collection.Features.Count == 0)
            {
                Debug.LogWarning("[SuggestRetrieveExample] Retrieve returned no features.");
                return;
            }

            var props = collection.Features[0].Properties;
            Debug.Log($"[SuggestRetrieveExample] Retrieved: {props.FullAddress}");
            Debug.Log($"  Lat: {props.Coordinates?.Latitude}  Lon: {props.Coordinates?.Longitude}");
            _session.RotateAfterRetrieve();
        }

        void OnDestroy()
        {
            if (MapCore != null)
                MapCore.Initialized -= OnMapInitialized;
        }
    }
}
