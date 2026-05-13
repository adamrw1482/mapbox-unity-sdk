using Mapbox.BaseModule.Data.Vector2d;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Utilities;
using UnityEngine;
using UnityEngine.EventSystems;
#if MAPBOX_NEW_INPUT_SYSTEM
using UnityEngine.InputSystem;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;
#endif

namespace Mapbox.Example.Scripts.MapInput
{
	/// <summary>
	/// Reusable output from UpdateCamera. Populated each frame by the camera implementation.
	/// Passed to MapboxMap.ChangeView — null fields mean "no change".
	/// </summary>
	public class CameraOutput
	{
		public LatitudeLongitude? Center;
		public float? Zoom;
		public float? Pitch;
		public float? Bearing;
		public float? Scale;
		public bool HasChanged;

		public void Reset()
		{
			Center = null;
			Zoom = null;
			Pitch = null;
			Bearing = null;
			Scale = null;
			HasChanged = false;
		}
	}

	public abstract class MapInput
	{
		protected Camera _camera;
		protected Plane _controlPlane = new Plane(Vector3.up, Vector3.zero);
		protected readonly CameraOutput _output = new CameraOutput();

		// Pinch state tracking
		private float _previousPinchDistance;
		private bool _pinchActive;
		private int _previousTouchCount;
		// Track the (touchId, touchId) of the pair we're measuring. EnhancedTouch.
		// activeTouches reorders on add/remove, so 2→3→2 transitions can leave us
		// comparing distance against a different physical pair. Reseed on change.
		private int _previousTouch0Id = int.MinValue;
		private int _previousTouch1Id = int.MinValue;

		// True on the frame the active touch count drops (e.g. 2→1 when one finger
		// lifts during a pinch). Cameras should treat this as a fresh drag start so
		// _dragOrigin is reset to the surviving finger's position instead of the old
		// touches[0] — without this, single-finger pan jumps after a pinch ends.
		protected bool TouchCountDecreasedThisFrame { get; private set; }

		public enum TwoFingerGesture
		{
			None,
			Pinch,
			Tilt
		}

		// Per-frame two-finger gesture decision. Computed once in UpdateInputState so
		// pinch and tilt detectors read consistent state and exactly one of them can
		// fire per frame — without this, both helpers update _previousPinchDistance
		// independently and tilt's pinch-comparison sees stale (post-update) state.
		protected TwoFingerGesture CurrentTwoFingerGesture { get; private set; }
		private float _pinchDeltaThisFrame;
		private float _tiltAvgDeltaYThisFrame;

		public virtual void Initialize(Camera camera, IMapInformation mapInfo)
		{
			_camera = camera ? camera : Camera.main;
#if MAPBOX_NEW_INPUT_SYSTEM
			if (!UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.enabled)
				UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.Enable();
#endif
		}

		/// <summary>
		/// Called from the owning behaviour's OnDestroy. Override to unsubscribe from
		/// <see cref="IMapInformation"/> events you wired in <see cref="Initialize"/> —
		/// IMapInformation can outlive the camera (DontDestroyOnLoad / scene reload),
		/// and event closures otherwise root the destroyed camera + its Camera Transform.
		/// </summary>
		public virtual void Teardown(IMapInformation mapInfo) { }

		public abstract CameraOutput UpdateCamera(IMapInformation mapInfo);

		#region Plane Intersection

		protected bool GetPlaneIntersection(Vector3 screenPosition, out Vector3 hit)
		{
			return _camera.GetPlaneIntersection(_controlPlane, screenPosition, out hit);
		}

		#endregion

		#region Camera Utilities

		public Plane[] GetFrustumPlanes()
		{
			return GeometryUtility.CalculateFrustumPlanes(_camera);
		}

		public Transform GetTransform()
		{
			return _camera.transform;
		}

		#endregion

		#region Value Clamping

		protected static float ClampZoom(float zoom, float min = 0f, float max = 22f)
		{
			return Mathf.Clamp(zoom, min, max);
		}

		protected static float ClampPitch(float pitch, float min = 15f, float max = 90f)
		{
			return Mathf.Clamp(pitch, min, max);
		}

		protected static float ClampBearing(float bearing)
		{
			bearing %= 360f;
			if (bearing > 180f) bearing -= 360f;
			if (bearing < -180f) bearing += 360f;
			return bearing;
		}

		#endregion

		#region Input Helpers

		/// <summary>
		/// Returns the number of active touches, or 0 if no touch device is available.
		/// </summary>
		private int GetTouchCount()
		{
#if MAPBOX_NEW_INPUT_SYSTEM
			return Touch.activeTouches.Count;
#else
			return Input.touchCount;
#endif
		}

		/// <summary>
		/// Call at the start of UpdateCamera to maintain input state between frames.
		/// Resets pinch tracking when fingers are lifted.
		/// </summary>
		protected void UpdateInputState()
		{
			var touchCount = GetTouchCount();
			TouchCountDecreasedThisFrame = touchCount < _previousTouchCount;
			_previousTouchCount = touchCount;

			// Reset gesture state every frame; only re-arm if a valid two-finger
			// pinch/tilt gesture is currently detected.
			CurrentTwoFingerGesture = TwoFingerGesture.None;
			_pinchDeltaThisFrame = 0f;
			_tiltAvgDeltaYThisFrame = 0f;

			if (touchCount < 2)
			{
				_pinchActive = false;
				_previousTouch0Id = int.MinValue;
				_previousTouch1Id = int.MinValue;
				return;
			}

			// Read both touches' positions, Y-deltas, and stable ids. Doing this once
			// here (not redundantly inside each detector) keeps the decision symmetric
			// and prevents stale-state races.
			Vector2 touch0Pos, touch1Pos;
			float touch0DeltaY, touch1DeltaY;
			int t0Id, t1Id;
#if MAPBOX_NEW_INPUT_SYSTEM
			var t0 = Touch.activeTouches[0];
			var t1 = Touch.activeTouches[1];
			touch0Pos = t0.screenPosition;
			touch1Pos = t1.screenPosition;
			touch0DeltaY = t0.delta.y;
			touch1DeltaY = t1.delta.y;
			t0Id = t0.touchId;
			t1Id = t1.touchId;
#else
			var t0 = Input.GetTouch(0);
			var t1 = Input.GetTouch(1);
			touch0Pos = t0.position;
			touch1Pos = t1.position;
			touch0DeltaY = t0.deltaPosition.y;
			touch1DeltaY = t1.deltaPosition.y;
			t0Id = t0.fingerId;
			t1Id = t1.fingerId;
#endif
			var currentDistance = Vector2.Distance(touch0Pos, touch1Pos);

			// Reseed when the pair changes (first two-finger frame OR a 2→3→2 / finger-swap
			// transition that shifted which physical touches occupy slots [0..1]). Comparing
			// distance against a different pair would spike a frame of phantom pinch.
			bool pairChanged = !_pinchActive ||
			                   t0Id != _previousTouch0Id || t1Id != _previousTouch1Id;
			if (pairChanged)
			{
				_pinchActive = true;
				_previousPinchDistance = currentDistance;
				_previousTouch0Id = t0Id;
				_previousTouch1Id = t1Id;
				return;
			}

			var pinchDelta = currentDistance - _previousPinchDistance;
			_previousPinchDistance = currentDistance;

			var pinchMag = Mathf.Abs(pinchDelta);
			var avgDeltaY = (touch0DeltaY + touch1DeltaY) / 2f;
			var tiltMag = Mathf.Abs(avgDeltaY);
			var bothFingersSameDirection = touch0DeltaY * touch1DeltaY > 0f;

			// Normalize both magnitudes to Screen.height so the thresholds are DPI-independent.
			// MinFrac ≈ 1 pixel on a 1080-height screen. Pinch wins ties (pinchFrac >= tiltFrac)
			// to avoid the "None" frame the prior strict-> on both sides produced when the
			// gestures registered identical magnitudes (common on near-pure vertical drags).
			var screenH = Mathf.Max(Screen.height, 1);
			var pinchFrac = pinchMag / screenH;
			var tiltFrac = tiltMag / screenH;
			const float MinFrac = 1f / 1080f;

			if (pinchFrac > MinFrac && pinchFrac >= tiltFrac)
			{
				CurrentTwoFingerGesture = TwoFingerGesture.Pinch;
				_pinchDeltaThisFrame = pinchDelta;
			}
			else if (bothFingersSameDirection && tiltFrac > MinFrac)
			{
				CurrentTwoFingerGesture = TwoFingerGesture.Tilt;
				_tiltAvgDeltaYThisFrame = avgDeltaY;
			}
		}

		/// <summary>
		/// Returns true if the pointer (mouse or primary touch) is over a UI element.
		/// </summary>
		protected bool IsPointerOverUI()
		{
			if (EventSystem.current == null)
				return false;

#if MAPBOX_NEW_INPUT_SYSTEM
			var touchCount = GetTouchCount();
			if (touchCount > 0)
				return EventSystem.current.IsPointerOverGameObject(Touch.activeTouches[0].finger.index);
			return EventSystem.current.IsPointerOverGameObject();
#else
			if (Input.touchCount > 0)
				return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
			return EventSystem.current.IsPointerOverGameObject();
#endif
		}

		/// <summary>
		/// Screen position of the primary pointer (first touch or mouse cursor).
		/// </summary>
		protected Vector3 GetPointerPosition()
		{
#if MAPBOX_NEW_INPUT_SYSTEM
			if (Touch.activeTouches.Count > 0)
				return Touch.activeTouches[0].screenPosition;
			if (Mouse.current != null)
				return Mouse.current.position.ReadValue();
			return Vector3.zero;
#else
			if (Input.touchCount > 0)
				return Input.GetTouch(0).position;
			return Input.mousePosition;
#endif
		}

		/// <summary>
		/// True on the frame the primary pointer goes down (touch began or LMB pressed).
		/// </summary>
		protected bool GetPointerDown()
		{
#if MAPBOX_NEW_INPUT_SYSTEM
			if (Touch.activeTouches.Count > 0)
				return Touch.activeTouches[0].phase == TouchPhase.Began;
			return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
			if (Input.touchCount > 0)
				return Input.GetTouch(0).phase == UnityEngine.TouchPhase.Began;
			return Input.GetMouseButtonDown(0);
#endif
		}

		/// <summary>
		/// True while the primary pointer is held (single touch active or LMB held).
		/// Returns false when two or more touches are active (pinch takes priority).
		/// </summary>
		protected bool GetPointerHeld()
		{
#if MAPBOX_NEW_INPUT_SYSTEM
			var touchCount = Touch.activeTouches.Count;
			if (touchCount == 1)
			{
				var phase = Touch.activeTouches[0].phase;
				return phase == TouchPhase.Moved || phase == TouchPhase.Stationary;
			}
			if (touchCount == 0)
				return Mouse.current != null && Mouse.current.leftButton.isPressed;
			return false;
#else
			if (Input.touchCount == 1)
			{
				var phase = Input.GetTouch(0).phase;
				return phase == UnityEngine.TouchPhase.Moved || phase == UnityEngine.TouchPhase.Stationary;
			}
			if (Input.touchCount == 0)
				return Input.GetMouseButton(0);
			return false;
#endif
		}

		/// <summary>
		/// True on the frame the secondary pointer goes down (RMB only, no touch equivalent).
		/// </summary>
		protected bool GetSecondaryDown()
		{
#if MAPBOX_NEW_INPUT_SYSTEM
			if (Touch.activeTouches.Count > 0)
				return false;
			return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
#else
			if (Input.touchCount > 0)
				return false;
			return Input.GetMouseButtonDown(1);
#endif
		}

		/// <summary>
		/// True while the secondary pointer is held (RMB only, no touch equivalent).
		/// </summary>
		protected bool GetSecondaryHeld()
		{
#if MAPBOX_NEW_INPUT_SYSTEM
			if (Touch.activeTouches.Count > 0)
				return false;
			return Mouse.current != null && Mouse.current.rightButton.isPressed;
#else
			if (Input.touchCount > 0)
				return false;
			return Input.GetMouseButton(1);
#endif
		}

		/// <summary>
		/// Returns true if zoom input was detected this frame (pinch or mouse scroll).
		/// Pinch/tilt mutual-exclusion is enforced in <see cref="UpdateInputState"/> —
		/// this helper just reads the cached decision and emits the scaled delta.
		/// </summary>
		protected bool GetPinchZoomDelta(out float zoomDelta, float pinchSensitivity = 5f)
		{
			zoomDelta = 0f;

			if (CurrentTwoFingerGesture == TwoFingerGesture.Pinch)
			{
				zoomDelta = _pinchDeltaThisFrame / Screen.height * pinchSensitivity;
				return true;
			}

			// Mouse scroll fallback only when no two-finger gesture is active.
			if (GetTouchCount() >= 2)
			{
				return false;
			}

#if MAPBOX_NEW_INPUT_SYSTEM
			if (Mouse.current != null)
			{
				var scroll = Mouse.current.scroll.ReadValue();
				if (Mathf.Abs(scroll.y) > 0)
				{
					// Raw scroll.y is ~120 per notch on Windows; legacy Input.GetAxis
					// returns ~0.1 per notch. Divide by 1200 so both paths feed the
					// same magnitude into ZoomSensitivity.
					zoomDelta = scroll.y / 1200f;
					return true;
				}
			}
#else
			if (Input.mouseScrollDelta.magnitude > 0)
			{
				zoomDelta = Input.GetAxis("Mouse ScrollWheel");
				return true;
			}
#endif
			return false;
		}

		/// <summary>
		/// Returns true if a two-finger vertical-tilt gesture was detected this frame.
		/// Pinch/tilt mutual-exclusion is enforced in <see cref="UpdateInputState"/> —
		/// this helper just reads the cached decision and emits the scaled delta.
		/// </summary>
		protected bool GetTwoFingerTiltDelta(out float tiltDelta, float tiltSensitivity = 0.5f)
		{
			if (CurrentTwoFingerGesture == TwoFingerGesture.Tilt)
			{
				tiltDelta = _tiltAvgDeltaYThisFrame / Screen.height * tiltSensitivity;
				return true;
			}
			tiltDelta = 0f;
			return false;
		}

		/// <summary>
		/// Screen position to use as zoom center.
		/// For pinch: midpoint between two fingers. For mouse: cursor position.
		/// </summary>
		protected Vector3 GetZoomCenter()
		{
#if MAPBOX_NEW_INPUT_SYSTEM
			if (Touch.activeTouches.Count >= 2)
			{
				var touch0 = Touch.activeTouches[0];
				var touch1 = Touch.activeTouches[1];
				return ((Vector3)touch0.screenPosition + (Vector3)touch1.screenPosition) / 2f;
			}
			return Mouse.current != null ? (Vector3)Mouse.current.position.ReadValue() : Vector3.zero;
#else
			if (Input.touchCount >= 2)
			{
				var touch0 = Input.GetTouch(0);
				var touch1 = Input.GetTouch(1);
				return (touch0.position + touch1.position) / 2f;
			}
			return Input.mousePosition;
#endif
		}

		#endregion
	}
}
