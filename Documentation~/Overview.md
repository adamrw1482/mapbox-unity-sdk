# Mapbox Unity SDK — Overview

Map rendering, location services, directions, geocoding, and 3D landmark features for Unity, distributed as a Unity Package Manager (UPM) package.

| | |
| --- | --- |
| **Package name** | `com.mapbox.sdk` |
| **Current version** | `3.1.0` |
| **Vendor** | Mapbox — <https://www.mapbox.com/> |
| **Minimum Unity** | `2022.3` LTS |
| **Render pipeline** | Universal Render Pipeline (URP) — required |

For full release notes see [`CHANGELOG.md`](../CHANGELOG.md). For installation and demo-scene walkthrough see [`README.md`](../README.md).

---

## Supported Unity editor versions

The package's `package.json` declares `unity: "2022.3"` as the minimum.

| Editor version | Status | Notes |
| --- | --- | --- |
| **2022.3 LTS** | Primary target. Android build-settings folder shipped: `Runtime/AndroidBuildSettings/2022.3.38f1/`. |
| **2021.3 LTS** | Best-effort. Android build-settings folder shipped: `Runtime/AndroidBuildSettings/2021.3.41f1/`. Not the primary target — use 2022.3+ when possible. |
| **6000.x (Unity 6)** | Best-effort. Android build-settings folder shipped: `Runtime/AndroidBuildSettings/6000/`. |
| **2023.x non-LTS** | Untested. Should work but no shipped Android settings folder. |
| **< 2021.3** | Unsupported. |

When building for Android, copy the `Plugins/` folder from the matching (or closest lower) editor version's subfolder under `Runtime/AndroidBuildSettings/` into your project's `Assets/`. See [`README.md`](../README.md#building-your-map-application-for-android) for the full procedure.

---

## Supported build platforms

Only **iOS** and **Android** are supported build targets. Use the Unity Editor (Windows or macOS) for development and Play-mode testing; all other build targets (Standalone, WebGL, console platforms, VisionOS, etc.) are not supported.

| Platform | Status | Notes |
| --- | --- | --- |
| **iOS** | Supported | XCFrameworks shipped under `Runtime/Mapbox/BaseModule/Plugins/iOS/` (MapboxCommon, Turf). ARM64 device + simulator. IL2CPP required. |
| **Android** | Supported | Native libs under `Runtime/Mapbox/BaseModule/Plugins/sqlite/Android/libs/` for `arm64-v8a`, `armeabi-v7a`, and `x86`. **See [Android build settings](#android-build-settings) below** for required project-side configuration. |
| **Unity Editor (Play mode)** | Supported on Windows and macOS for development and testing. Not a shipping target. |
| **All other platforms** | Not supported. Includes Standalone (Windows/macOS/Linux), WebGL, console platforms, and VisionOS. |

---

## Required dependencies

These are declared in `package.json` and resolved automatically by UPM:

| Package | Version | Used for |
| --- | --- | --- |
| `com.unity.render-pipelines.universal` | `10.10.1` | Terrain and building shaders ship as URP Shader Graphs. |
| `com.unity.nuget.newtonsoft-json` | `3.2.1` | JSON parsing for tile metadata, geocoding/directions API responses. |
| `com.unity.burst` | `1.8.12` | Burst-compiled terrain-RGB decode and collider vertex-fill jobs. First-time domain reload pays a one-shot AOT compile cost. |

## Optional dependencies

| Package | Behaviour if installed | Behaviour if absent |
| --- | --- | --- |
| `com.unity.inputsystem` | Camera input uses `UnityEngine.InputSystem` (new Input System). The `MAPBOX_NEW_INPUT_SYSTEM` define is auto-set via `versionDefines` in `MapboxExamples.asmdef`. | Camera input falls back to legacy `UnityEngine.Input`. No project-side action required either way. See [`CameraSystem.md`](CameraSystem.md). |

---

## Android build settings

### Managed Stripping Level

The SQLite tile cache uses sqlite-net, which populates row values via `PropertyInfo.SetValue` reflection. IL2CPP's managed-code stripping at **Medium / High** strips auto-property accessor methods that aren't seen as called via static analysis, breaking the row mapper.

**As of v3.1.0** the four SQLite model types (`tiles`, `tilesets`, `offlineMaps`, `tile2offline`) and every persisted property carry `[UnityEngine.Scripting.Preserve]`, and `Plugins/link.xml` declares them as a backup preservation path. So the SDK *should* work at any stripping level.

If you still observe `Error inserting … (extended=ConstraintForeignKey)` in logcat on an Android device build:

1. Set **Project Settings → Player → Android tab → Other Settings → Optimization → Managed Stripping Level** to **Minimal** or **Disabled**.
2. Report the issue — the `[Preserve]` and `link.xml` combination should have covered it.

When the cache is broken, map rendering still works (the in-memory cache covers) but tiles are silently re-fetched every session instead of being served from disk.

### Required Android plugin folder

For Android builds, copy `Runtime/AndroidBuildSettings/<closest-editor-version>/Plugins/` into your project's `Assets/`. This ships gradle templates and manifest entries the SDK relies on at build time.

### Permissions

Location features require:
- `android.permission.ACCESS_FINE_LOCATION`
- `android.permission.ACCESS_COARSE_LOCATION`

Runtime permission prompts are handled by `Mapbox.BaseModule.Plugins.Android.UniAndroidPermission`.

---

## iOS build settings

| Setting | Value |
| --- | --- |
| **Scripting Backend** | IL2CPP (required) |
| **Minimum iOS Version** | 11.0+ recommended |
| **Architecture** | ARM64 |
| **Location permission** | Set `NSLocationWhenInUseUsageDescription` (or `NSLocationAlwaysUsageDescription`) in Info.plist before requesting location. |

XCFrameworks (`MapboxCommon`, `Turf`) ship multi-slice and are picked up automatically by Unity's plugin importer.

---

## Modules

The SDK is organized into module assemblies. Each is a separate `.asmdef` and can be referenced or excluded independently:

| Assembly | Path | Responsibility |
| --- | --- | --- |
| `MapboxBaseModule` | `Runtime/Mapbox/BaseModule/` | Tile management, caching (memory/file/SQLite), data fetching, coordinate conversions. Foundation for every other module. |
| `UnityMapModule` | `Runtime/Mapbox/UnityMapService/` | Unity-specific map service (`MapUnityService`), quadtree tile provider. |
| `MapboxLocationModule` | `Runtime/Mapbox/LocationModule/` | GPS / compass via `AbstractLocationProvider`. Unity + static (editor) implementations. |
| `MapboxImageModule` | `Runtime/Mapbox/ImageModule/` | Raster tile imagery (satellite, streets). Terrain strategies (flat / elevated). |
| `MapboxVectorModule` | `Runtime/Mapbox/VectorModule/` | Vector tile rendering — buildings, roads, areas. Modifier-stack-based mesh generation. Component System. |
| `MapboxCustomImageryModule` | `Runtime/Mapbox/CustomImageryModule/` | Custom tileset support (non-Mapbox sources via TMS or arbitrary URL templates). |
| `MapboxDirections` | `Runtime/Mapbox/DirectionsApi/` | Directions API wrapper. |
| `MapboxGeocoding` | `Runtime/Mapbox/GeocodingApi/` | Forward / reverse geocoding API wrapper. |
| `MapboxDebug` | `Runtime/Mapbox/MapDebug/` | Debug logging + benchmarking pipelines. |
| `MapboxExamples` | `Runtime/Mapbox/Example/` | Camera behaviours, input handling, demo support scripts. |

Editor-only counterparts exist for several modules (e.g. `MapboxBaseModule.Editor`, `MapboxImageModule.Editor`).

---

## Samples

Imported via Package Manager → select the SDK → **Samples** tab → **Import**. Five samples ship:

| Sample | Path | What it demonstrates |
| --- | --- | --- |
| **WorldMapViewer** | `Samples~/WorldMapViewer` | Minimal map setup — pan/zoom an interactive world map. |
| **LocationBasedGame** | `Samples~/LocationBasedGame` | Device GPS → in-scene avatar movement. The classic Pokemon-Go-style starting point. |
| **MapboxComponents** | `Samples~/MapboxComponents` | Component-system-based layer rendering (Buildings + Areas + Roads). |
| **DirectionsApiDemo** | `Runtime/Mapbox/DirectionsApi/Samples~/DirectionsApiDemo` | Calling Directions API + rendering a route on the map. |
| **GeocodingApiDemo** | `Runtime/Mapbox/GeocodingApi/Samples~/GeocodingApiDemo` | Forward and reverse geocoding. |

---

## Other documentation

Located in `Documentation~/`:

- [`GettingStartedWithMapboxMapObject.md`](GettingStartedWithMapboxMapObject.md) — first-time setup and the core `MapboxMap` object.
- [`AccessingTheMapObject.md`](AccessingTheMapObject.md) — getting a reference to the map at runtime.
- [`WorkingWithMapObject.md`](WorkingWithMapObject.md) — runtime API.
- [`WorkingWithModules.md`](WorkingWithModules.md) — module composition pattern. Includes architecture diagrams.
- [`ChangingMapLocation.md`](ChangingMapLocation.md) — moving the map to a new lat/lon.
- [`ChangeImageryStyleOnRuntime.md`](ChangeImageryStyleOnRuntime.md) — switching tile styles at runtime.
- [`CameraSystem.md`](CameraSystem.md) — camera behaviours, touch/mouse input, Input System soft-dependency model.
- [`CoordinateConversions.md`](CoordinateConversions.md) — lat/lon ↔ Mercator ↔ Unity world space.
- [`VectorLayerModule.md`](VectorLayerModule.md) — building/road/area visualizers, modifier stacks.
- [`ComponentsModule.md`](ComponentsModule.md) — the newer component-based vector pipeline.
- [`WorkingWithPois.md`](WorkingWithPois.md) — point-of-interest features.
- [`UsingDirectionsApi.md`](UsingDirectionsApi.md) / [`UsingGeocodingApi.md`](UsingGeocodingApi.md) / [`UsingMapMatchingApi.md`](UsingMapMatchingApi.md) — API wrappers.

---

## Access token

Every Mapbox API call (tiles, geocoding, directions) requires a Mapbox access token.

- Set it via the editor menu: **Mapbox → Setup**.
- Stored at `Assets/Resources/Mapbox/MapboxConfiguration.txt` (read at runtime via `Resources.Load`).
- An SKU token is appended automatically to authenticated requests.
- Get a token at <https://account.mapbox.com/access-tokens>.

---

## Reporting issues / contributing

This package is maintained by Mapbox internally. Issues and feature requests should be reported through your standard Mapbox support channel.
