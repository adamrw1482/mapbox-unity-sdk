
# Using the Mapbox Search Box API

The Mapbox Search Box API provides two ways to find places:
- **Interactive autocomplete**: `/suggest` → user picks a result → `/retrieve` full details. Billed per completed session, not per keystroke.
- **One-off search**: `/forward`, `/reverse`, and `/category` return full results (including coordinates) directly, no session token needed. Each call is billed individually.

For complete API reference and details, see the [official Mapbox Search Box API documentation](https://docs.mapbox.com/api/search/search-box/).

A ready-to-use search box UI (input field + suggestions dropdown + prefab) ships as the **SearchApiDemo** sample — see [Using the pre-built search box](#using-the-pre-built-search-box) below if you just want a drop-in component instead of calling the API directly.

## Interactive Autocomplete (suggest → retrieve)

This is the flow behind a typical search-as-you-type box. `/suggest` returns lightweight suggestions (no coordinates); calling `/retrieve` on the one the user picked returns the full feature.

### Basic Example

```csharp
using UnityEngine;
using Mapbox.BaseModule.Map;
using Mapbox.SearchApi;
using Mapbox.SearchApi.Response;

public class SuggestRetrieveExample : MonoBehaviour
{
    public MapBehaviourCore MapCore;
    private MapboxSearchApi _searchApi;
    private SearchSession _session;

    void Start()
    {
        MapCore.Initialized += map =>
        {
            _searchApi = new MapboxSearchApi(map.MapService.FileSource);
            _session   = new SearchSession();
            Suggest("Michigan Stadium");
        };
    }

    void Suggest(string query)
    {
        var resource = new SuggestResource(query, _session.SessionToken);
        _searchApi.Suggest(resource, OnSuggestResponse);
    }

    void OnSuggestResponse(SuggestResponse response)
    {
        if (response?.Suggestions == null || response.Suggestions.Count == 0) return;

        // User picks response.Suggestions[0] in your UI; then retrieve its full details:
        var picked = response.Suggestions[0];
        _searchApi.Retrieve(
            new RetrieveResource(picked.MapboxId, _session.SessionToken),
            OnRetrieveResponse);
    }

    void OnRetrieveResponse(SearchFeatureCollection collection)
    {
        _session.RotateAfterRetrieve(); // ends this session, per the API's billing model
        if (collection?.Features == null || collection.Features.Count == 0) return;

        var props = collection.Features[0].Properties;
        Debug.Log($"{props.FullAddress}: {props.Coordinates.Latitude}, {props.Coordinates.Longitude}");
    }
}
```

A working version of this (with Console logging instead of UI) ships in-assembly as `Mapbox.SearchApi.Example.SuggestRetrieveExample`.

### Session Tokens

`/suggest` and `/retrieve` calls sharing a `SearchSession` are billed together as **one session**, regardless of how many keystrokes triggered `/suggest`. `SearchSession` manages the token lifecycle for you:

```csharp
var session = new SearchSession();
// ... use session.SessionToken in SuggestResource / RetrieveResource ...

session.OnSuggestIssued(Time.time);   // call after each successful /suggest
session.RotateAfterRetrieve();        // call after a successful /retrieve — ends the session
```

A session rotates to a new token automatically when any of the following happen (per the API spec, each completed session is billed as one unit):
- `/suggest` is followed by `/retrieve` (call `RotateAfterRetrieve()`)
- 180 seconds pass with no `/suggest` call — check with `session.IsIdleTimeoutExpired(Time.time)` (e.g. once per frame) and call `session.Rotate()` if it returns `true`
- 50 consecutive `/suggest` calls share the same token — handled automatically inside `OnSuggestIssued`

`SearchSession` also tracks the latest in-flight request so you can cancel it when the user keeps typing:

```csharp
var request = _searchApi.Suggest(resource, OnSuggestResponse);
_session.SetPendingRequest(request); // cancels any previous pending request automatically
```

### Optional Parameters

`SuggestResource` and `RetrieveResource` both inherit the shared filters from `SearchBoxResource`:

```csharp
var resource = new SuggestResource(query, session.SessionToken)
{
    Language = "en",                          // ISO language code
    Limit = 5,                                // up to 10
    Country = new[] { "us" },                 // ISO 3166-1 alpha-2 codes
    Types = new[] { "poi", "address" },       // restrict feature types
    Proximity = new Vector2d(37.7, -122.4),   // bias toward a location; x=lat, y=lon
    Bbox = new LatitudeLongitudeBounds(sw, ne) // restrict to a bounding box
};
```

### Response Data

`SuggestResponse.Suggestions` is a list of `Suggestion` — lightweight, **no coordinates** (that's why you call `/retrieve`). Key fields: `Name`, `NamePreferred`, `MapboxId`, `FeatureType`, `FullAddress`, `PlaceFormatted`, `Context`, `Maki`, `PoiCategory`, `Distance`, `Eta`.

`SearchFeatureCollection.Features` (returned by `/retrieve`, `/forward`, `/reverse`, `/category`) is a list of `SearchFeature`, each with:
- `Geometry.Coordinates` — a `Vector2d` (x=latitude, y=longitude)
- `Properties` — a `SearchProperties` with the same descriptive fields as `Suggestion`, plus `Coordinates` (a `SearchCoordinates` with named `Latitude`/`Longitude`/`Accuracy`) and `Bbox`

---

## One-off Forward Search

Use this for a single text search with results returned immediately — no session token, no `/retrieve` step.

```csharp
using Mapbox.SearchApi;
using Mapbox.SearchApi.Response;

var searchApi = new MapboxSearchApi(map.MapService.FileSource);
var resource = new ForwardSearchResource("1201 S Main St, Ann Arbor, MI")
{
    Language = "en",
    Limit = 5,
    AutoComplete = true,           // include partial/fuzzy matches
    Country = new[] { "us" }
};

searchApi.Forward(resource, (SearchFeatureCollection collection) =>
{
    if (collection?.Features == null || collection.Features.Count == 0) return;
    foreach (var f in collection.Features)
        Debug.Log($"{f.Properties.Name}: {f.Properties.Coordinates.Latitude}, {f.Properties.Coordinates.Longitude}");
});
```

A working version of this ships in-assembly as `Mapbox.SearchApi.Example.ForwardSearchExample`.

---

## Reverse Search

Converts coordinates into nearby addresses and POIs.

```csharp
var resource = new ReverseSearchResource(longitude: -83.7382, latitude: 42.2654)
{
    Types = new[] { "address" }
};

searchApi.Reverse(resource, (SearchFeatureCollection collection) =>
{
    // same SearchFeatureCollection shape as Forward
});
```

---

## Category Search

Finds POIs matching a canonical category ID (e.g. `"coffee"`, `"restaurant"`) around a location. Use `ListCategoryResource` first if you don't already know the category ID you need.

```csharp
// Discover valid category IDs:
searchApi.ListCategories(new ListCategoryResource { Language = "en" }, (CategoryListResponse response) =>
{
    foreach (var item in response.ListItems)
        Debug.Log($"{item.CanonicalId}: {item.Name}");
});

// Search within a category, biased toward a location:
var resource = new CategorySearchResource("coffee")
{
    Proximity = new Vector2d(42.2654, -83.7382),
    Limit = 10
};
searchApi.Category(resource, (SearchFeatureCollection collection) => { /* ... */ });
```

---

## Using the Pre-built Search Box

The **SearchApiDemo** sample (import via Package Manager → Mapbox Unity SDK → Samples) ships a drag-and-drop `MapboxSearchBox` prefab wrapping the full suggest/retrieve flow, backed by `SearchBoxController`:

```csharp
var searchBox = FindObjectOfType<SearchBoxController>();
searchBox.OnResultSelected.AddListener((SearchSelection selection) =>
{
    Debug.Log($"Selected: {selection.Name} at {selection.Coordinates}");
    // e.g. map.ChangeView(selection.Coordinates);
});
```

By default the prefab builds its own authenticated `IFileSource` from the project's configured access token. To share a running map's token/connection instead, call `SetFileSource` before the prefab's `Start()` runs:

```csharp
searchBox.SetFileSource(map.MapService.FileSource);
```

`OnResultSelected` fires once, after a suggestion is picked and `/retrieve` completes, carrying a `SearchSelection` (`Name`, `FullAddress`, `Coordinates`, and the raw `SearchFeature`).

---

## Common Use Cases

### Search Box Tied to Map Movement
```csharp
searchBox.OnResultSelected.AddListener(selection =>
{
    map.MapboxMap.ChangeView(selection.Coordinates);
});
```

### Nearby POIs Around the Current Map Center
```csharp
var center = map.MapInformation.LatitudeLongitude; // IMapInformation.LatitudeLongitude
var resource = new CategorySearchResource("restaurant")
{
    Proximity = new Vector2d(center.Latitude, center.Longitude), // x=lat, y=lon
    Limit = 10
};
searchApi.Category(resource, DisplayNearbyResults);
```
