
# Camera System

The Mapbox SDK includes a camera system for navigating the map with mouse and touch input. There are two camera modes, each suited to different use cases.

---

## Camera Modes

### 3D Camera (Moving3dCameraBehaviour)

The camera moves through 3D space while the map stays in place. Best for:
- 3D city exploration
- Games and simulations
- Scenes where the camera needs to orbit around a point of interest

The camera orbits a target point on the map. Panning moves the camera, zooming adjusts how far the camera is from the ground, and rotation orbits around the target.

### Slippy Map Camera (SlippyMapCameraBehaviour)

The camera stays in a fixed position while the map moves underneath. Best for:
- Traditional 2D map interfaces
- Navigation and routing views
- Data visualization overlays
- AR/VR applications where the camera is controlled by head tracking

Panning shifts the map's geographic center. Zooming changes the map's scale level. The camera itself doesn't move — the world moves under it.

---

## Setting Up

1. Your scene needs a **MapboxMapBehaviour** on a GameObject (this is the map itself).
2. Add a camera behaviour component to any GameObject in the scene:
   - **Moving3dCameraBehaviour** for 3D camera, or
   - **SlippyMapCameraBehaviour** for slippy map camera
3. Assign the **Map Behaviour** field to the GameObject that has MapboxMapBehaviour.
4. Assign the **Camera** field to the scene camera used for rendering. If left empty, the main camera is used automatically.

The camera will initialize itself once the map is ready. No additional setup is required.

---

## Settings Reference

### 3D Camera Settings

| Setting | Description |
|---------|-------------|
| **Pitch** | Camera tilt angle. 90 = looking straight down, 15 = near the horizon. |
| **Bearing** | Camera rotation in degrees. 0 = north is up. |
| **Camera Curve** | An animation curve that controls how camera height changes with zoom level. The X axis is zoom, Y axis is distance from the ground. |
| **Distance Scale** | Multiplier on the camera curve output. Increase to raise the camera without re-editing the curve. Default: 2. |
| **Zoom Sensitivity** | How much each scroll step changes the zoom level. Default: 0.25. |
| **Rotation Speed** | How fast right-click drag rotates the camera. Default: 50. |

### Slippy Map Camera Settings

| Setting | Description |
|---------|-------------|
| **Center Latitude Longitude** | Initial map center as a geographic coordinate. Example: 40.7128, -74.0060 for New York. |
| **Pitch** | Camera tilt angle. 90 = looking straight down, 15 = near the horizon. |
| **Bearing** | Camera rotation in degrees. 0 = north is up. |
| **Camera Distance** | How far the camera is from the ground target. Typical range: 100 to 1000 depending on your map's scale. Default: 200. |
| **Pan Enabled** | Toggle left-click / single-finger drag to pan the map. |

**Rotation Settings** (nested):

| Setting | Description |
|---------|-------------|
| **Enabled** | Toggle right-click drag to rotate. |
| **Speed** | Rotation sensitivity. Default: 50. |
| **Rotation Mode** | *Rotate The Camera* orbits the camera around the map. *Rotate The Map* keeps the camera fixed and rotates the map underneath — useful for navigation-style views. |
| **Map Root** | The root transform of the map. Auto-detected if left empty. |

**Zoom Settings** (nested):

| Setting | Description |
|---------|-------------|
| **Enabled** | Toggle scroll wheel / pinch zoom. |
| **Speed** | Zoom sensitivity per step. Default: 0.25. |
| **Zoom At Cursor** | When enabled, the map zooms toward the cursor or pinch position. When disabled, zoom is always centered on the map. |

---

## Input Controls

Both cameras support mouse and touch input. The controls are:

| Action | Mouse | Touch |
|--------|-------|-------|
| Pan | Left-click drag | Single-finger drag |
| Rotate | Right-click drag | Not available via touch |
| Zoom | Scroll wheel | Two-finger pinch |
| Tilt | Not available via mouse | Two-finger vertical drag |

When using two fingers, the system automatically detects whether you are pinching (zoom) or dragging vertically (tilt) and applies the dominant gesture. Both gestures will not activate at the same time.

Zoom at cursor works with both mouse and touch — when using pinch, the zoom centers on the midpoint between your two fingers.

---

## Input System Support

The cameras work with both Unity's legacy Input Manager and the new Input System package. The active path is selected at compile time:

- **Default (no Input System package installed)** — uses the legacy `UnityEngine.Input` API. No action needed.
- **With `com.unity.inputsystem` installed** — the example asmdef declares a `versionDefines` entry that automatically defines `MAPBOX_NEW_INPUT_SYSTEM`. The new-input branch activates and reads pointer state via `Mouse.current` / `Touch.activeTouches`.

The Input System package is *not* a hard dependency of the SDK — projects that don't install it keep the legacy path with no extra setup. Projects that set **Active Input Handling** to "Input System Package (New)" only must install the package; otherwise `UnityEngine.Input` is unavailable at runtime.

---

## Writing a Custom Camera

If neither camera mode fits your needs, you can create your own by writing a class that extends `MapInput` and a MonoBehaviour that extends `MapCameraBehaviour<T>`.

Your custom camera class needs to implement one method — `UpdateCamera` — where you handle input and return camera state (center, zoom, pitch, bearing, scale). The base class provides ready-made helpers for input detection, coordinate conversion, and value clamping.

The behaviour wrapper handles initialization and communicating changes to the map automatically. A minimal custom camera behaviour only needs a few lines of code beyond the camera logic itself.
