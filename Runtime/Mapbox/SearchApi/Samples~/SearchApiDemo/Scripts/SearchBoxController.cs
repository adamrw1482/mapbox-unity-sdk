using System;
using System.Collections;
using System.Collections.Generic;
using Mapbox.BaseModule.Data.Platform;
using Mapbox.BaseModule.Data.Vector2d;
using Mapbox.BaseModule.Map;
using Mapbox.SearchApi.Response;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Mapbox.SearchApi.Demo
{
    /// <summary>
    /// Reusable component that wires a TMP InputField to the Search Box API autocomplete flow.
    /// <list type="bullet">
    ///   <item>Debounces keystrokes and calls <c>/suggest</c> once the query is long enough.</item>
    ///   <item>Shows a live dropdown of suggestions.</item>
    ///   <item>On row selection, calls <c>/retrieve</c> and raises <see cref="OnResultSelected"/>.</item>
    /// </list>
    /// The controller is <b>map-agnostic</b>: it builds its own token-bearing file source from
    /// <c>MapboxContext</c> at startup, or you can inject a map's file source via
    /// <see cref="SetFileSource"/>.
    /// </summary>
    public class SearchBoxController : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────

        [SerializeField]
        [Tooltip("TMP InputField where the user types their search query.")]
        private TMP_InputField _inputField;

        [SerializeField]
        [Tooltip("Container (e.g. a VerticalLayoutGroup) that holds suggestion rows.")]
        private RectTransform _resultsContainer;

        [SerializeField]
        [Tooltip("Root panel (background + results container) shown while there are active suggestions " +
                 "and hidden automatically when there aren't. Optional — falls back to toggling " +
                 "_resultsContainer directly if left unassigned.")]
        private GameObject _suggestionPanel;

        [SerializeField]
        [Tooltip("Prefab for a single suggestion row. Must have a SearchResultRow component.")]
        private SearchResultRow _rowPrefab;

        [SerializeField]
        [Tooltip("Seconds to wait after the last keystroke before issuing a /suggest request (0.1–1.0 recommended).")]
        private float _debounceSeconds = 0.3f;

        [SerializeField]
        [Tooltip("Minimum number of characters before autocomplete starts.")]
        private int _minQueryLength = 2;

        [SerializeField]
        [Tooltip("Maximum number of suggestions to request (1–10).")]
        private int _limit = 5;

        [SerializeField]
        [Tooltip("ISO language code for results, e.g. \"en\", \"fr\".")]
        private string _language = "en";

        [SerializeField]
        [Tooltip("Restrict results to these ISO 3166-1 alpha-2 country codes, e.g. \"us\".")]
        private string[] _country;

        // ── Events ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Raised when the user selects a suggestion and <c>/retrieve</c> completes.
        /// Wire this in the Inspector or via code to react to a chosen location.
        /// </summary>
        public UnityEvent<SearchSelection> OnResultSelected;

        // ── Internal state ─────────────────────────────────────────────────────

        private MapboxSearchApi _searchApi;
        private SearchSession   _session;
        private IFileSource     _fileSource;

        private float           _debounceTimer;
        private bool            _debouncing;
        private string          _pendingQuery;
        private readonly List<SearchResultRow> _activeRows = new List<SearchResultRow>();

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Start()
        {
            if (_fileSource == null)
                StartCoroutine(InitializeStandalone());
            else
                Initialize(_fileSource);
        }

        /// <summary>
        /// Inject an <see cref="IFileSource"/> from a running map (or any other source).
        /// The SKU token is suppressed automatically for all Search Box API requests,
        /// so passing <c>map.MapService.FileSource</c> is safe and correct.
        /// Call before <c>Start</c> to skip standalone initialization, or at any time to swap sources.
        /// </summary>
        public void SetFileSource(IFileSource fileSource)
        {
            _fileSource = fileSource ?? throw new ArgumentNullException(nameof(fileSource));
            if (_searchApi != null)
                Initialize(_fileSource); // re-init if already running
        }

        private IEnumerator InitializeStandalone()
        {
            // Load the Mapbox config from Resources without network token validation
            // (token is validated implicitly on the first API call).
            var context = new MapboxContext();
            bool failed = false;

            // Manually pump the sub-coroutine: `yield return` can't appear inside a try/catch,
            // so the try only wraps MoveNext() and the yield happens outside it.
            var configRoutine = context.LoadConfigurationCoroutine(validateToken: false);
            while (true)
            {
                object current;
                try
                {
                    if (!configRoutine.MoveNext())
                        break;
                    current = configRoutine.Current;
                }
                catch (Exception e)
                {
                    Debug.LogError("[SearchBoxController] Failed to load Mapbox configuration. " +
                                   "Make sure your access token is set via Mapbox > Setup in the menu.\n" + e, this);
                    failed = true;
                    break;
                }
                yield return current;
            }

            if (!failed)
            {
                _fileSource = new FileSource(context.GetSkuToken, context.GetAccessToken());
                Initialize(_fileSource);
            }
        }

        private void Initialize(IFileSource fileSource)
        {
            if (_inputField == null)
            {
                Debug.LogError("[SearchBoxController] _inputField is not assigned in the Inspector.", this);
                return;
            }
            if (_resultsContainer == null)
            {
                Debug.LogError("[SearchBoxController] _resultsContainer is not assigned in the Inspector.", this);
                return;
            }
            if (_rowPrefab == null)
            {
                Debug.LogError("[SearchBoxController] _rowPrefab is not assigned in the Inspector.", this);
                return;
            }
            if (_suggestionPanel == gameObject)
            {
                Debug.LogError("[SearchBoxController] _suggestionPanel must not be this component's own " +
                                "GameObject — hiding suggestions would disable this script itself, permanently " +
                                "stopping input handling. Assign a child object that wraps just the dropdown " +
                                "(background + results container) instead.", this);
                return;
            }

            _searchApi = new MapboxSearchApi(fileSource);
            _session   = new SearchSession();

            _inputField.onValueChanged.RemoveAllListeners();
            _inputField.onValueChanged.AddListener(OnInputChanged);

            HideResults();
            Debug.Log("[SearchBoxController] Initialized. Ready to search.");
        }

        private void Update()
        {
            // Checked unconditionally (not nested under the debounce branch below) so idle
            // rotation actually fires during real idle periods, not just the ~0.3s debounce
            // window right after a keystroke. _session is null until Initialize() completes
            // (Start() kicks off an async config-load coroutine first), hence the null check.
            if (_session != null && _session.IsIdleTimeoutExpired(Time.time))
                _session.Rotate();

            if (!_debouncing) return;

            _debounceTimer -= Time.deltaTime;
            if (_debounceTimer <= 0f)
            {
                _debouncing = false;
                IssueSearch(_pendingQuery);
            }
        }

        // ── Input handling ─────────────────────────────────────────────────────

        private void OnInputChanged(string query)
        {
            if (string.IsNullOrEmpty(query) || query.Length < _minQueryLength)
            {
                _debouncing = false;
                _session.CancelPendingRequest();
                HideResults();
                return;
            }

            _pendingQuery  = query;
            _debounceTimer = _debounceSeconds;
            _debouncing    = true;
        }

        private void IssueSearch(string query)
        {
            if (_searchApi == null) return;

            var resource = new SuggestResource(query, _session.SessionToken)
            {
                Language    = _language,
                Limit       = _limit,
                Country     = _country
            };

            var request = _searchApi.Suggest(resource, OnSuggestResponse);
            _session.SetPendingRequest(request);
            _session.OnSuggestIssued(Time.time);
        }

        // ── Response handling ──────────────────────────────────────────────────

        private void OnSuggestResponse(SuggestResponse response)
        {
            ClearRows();

            if (response?.Suggestions == null || response.Suggestions.Count == 0)
            {
                HideResults();
                return;
            }

            foreach (var suggestion in response.Suggestions)
            {
                var suggestion1 = suggestion; // capture for closure
                var row = Instantiate(_rowPrefab, _resultsContainer);
                row.Bind(suggestion1, () => OnRowSelected(suggestion1));
                _activeRows.Add(row);
            }

            SetSuggestionsVisible(true);
        }

        private void OnRowSelected(Suggestion suggestion)
        {
            _inputField.SetTextWithoutNotify(suggestion.Name ?? string.Empty);

            var retrieveResource = new RetrieveResource(suggestion.MapboxId, _session.SessionToken);
            _searchApi.Retrieve(retrieveResource, OnRetrieveResponse);

            // Defer hiding/destroying the rows to next frame. Deactivating or destroying an
            // ancestor of the button whose onClick is currently executing — even via
            // SetActive(false), which runs OnDisable synchronously — corrupts the
            // EventSystem's per-pointer click-eligibility state (most visible with
            // InputSystemUIInputModule) and swallows clicks on other buttons afterwards.
            // Waiting a frame lets this click finish processing untouched first.
            StartCoroutine(HideResultsNextFrame());
        }

        private IEnumerator HideResultsNextFrame()
        {
            yield return null;
            HideResults();
        }

        private void OnRetrieveResponse(SearchFeatureCollection collection)
        {
            _session.RotateAfterRetrieve();

            if (collection?.Features == null || collection.Features.Count == 0) return;

            var feature    = collection.Features[0];
            var props      = feature.Properties;
            var coords     = props?.Coordinates;

            var selection = new SearchSelection
            {
                Name        = props?.Name        ?? string.Empty,
                FullAddress = props?.FullAddress  ?? string.Empty,
                Coordinates = coords != null
                    ? new LatitudeLongitude(coords.Latitude, coords.Longitude)
                    : default,
                Feature     = feature
            };

            OnResultSelected?.Invoke(selection);
        }

        // ── UI helpers ─────────────────────────────────────────────────────────

        private void HideResults()
        {
            ClearRows();
            SetSuggestionsVisible(false);
        }

        /// <summary>
        /// Shows or hides <see cref="_suggestionPanel"/> (or, if unassigned, <see cref="_resultsContainer"/>
        /// directly) so the dropdown only appears while there are active suggestions.
        /// </summary>
        private void SetSuggestionsVisible(bool visible)
        {
            if (_suggestionPanel != null)
                _suggestionPanel.SetActive(visible);
            else if (_resultsContainer != null)
                _resultsContainer.gameObject.SetActive(visible);
        }

        private void ClearRows()
        {
            foreach (var row in _activeRows)
                if (row != null)
                    Destroy(row.gameObject);
            _activeRows.Clear();
        }
    }
}
