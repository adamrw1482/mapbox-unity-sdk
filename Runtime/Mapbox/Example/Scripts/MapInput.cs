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

		public virtual void Initialize(Camera camera, IMapInformation mapInfo)
		{
			_camera = camera ? camera : Camera.main;
#if MAPBOX_NEW_INPUT_SYSTEM
			if (!UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.enabled)
				UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.Enable();
#endif
		}

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
			if (GetTouchCount() < 2)
				_pinchActive = false;
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
		/// Detects pinch-to-zoom (two fingers) or mouse scroll wheel.
		/// Returns true if zoom input is active, with the delta as a zoom level change.
		/// Positive = zoom in, negative = zoom out.
		/// When two fingers are active, only returns true if pinch is the dominant gesture (vs tilt).
		/// </summary>
		protected bool GetPinchZoomDelta(out float zoomDelta, float pinchSensitivity = 5f)
		{
			zoomDelta = 0f;

#if MAPBOX_NEW_INPUT_SYSTEM
			var touchCount = Touch.activeTouches.Count;
			if (touchCount >= 2)
			{
				var touch0 = Touch.activeTouches[0];
				var touch1 = Touch.activeTouches[1];
				var currentDistance = Vector2.Distance(touch0.screenPosition, touch1.screenPosition);

				if (!_pinchActive)
				{
					_pinchActive = true;
					_previousPinchDistance = currentDistance;
					return false;
				}

				var pinchDelta = currentDistance - _previousPinchDistance;
				_previousPinchDistance = currentDistance;

				var avgDeltaY = (touch0.delta.y + touch1.delta.y) / 2f;
				var pinchMagnitude = Mathf.Abs(pinchDelta);
				var tiltMagnitude = Mathf.Abs(avgDeltaY);

				if (pinchMagnitude > tiltMagnitude && pinchMagnitude > 0.01f)
				{
					zoomDelta = pinchDelta / Screen.height * pinchSensitivity;
					return true;
				}

				return false;
			}

			if (Mouse.current != null)
			{
				var scroll = Mouse.current.scroll.ReadValue();
				if (Mathf.Abs(scroll.y) > 0)
				{
					zoomDelta = scroll.y / 120f;
					return true;
				}
			}

			return false;
#else
			if (Input.touchCount >= 2)
			{
				var touch0 = Input.GetTouch(0);
				var touch1 = Input.GetTouch(1);
				var currentDistance = Vector2.Distance(touch0.position, touch1.position);

				if (!_pinchActive)
				{
					_pinchActive = true;
					_previousPinchDistance = currentDistance;
					return false;
				}

				var pinchDelta = currentDistance - _previousPinchDistance;
				_previousPinchDistance = currentDistance;

				// Compare pinch magnitude vs tilt magnitude to pick the dominant gesture
				var avgDeltaY = (touch0.deltaPosition.y + touch1.deltaPosition.y) / 2f;
				var pinchMagnitude = Mathf.Abs(pinchDelta);
				var tiltMagnitude = Mathf.Abs(avgDeltaY);

				if (pinchMagnitude > tiltMagnitude && pinchMagnitude > 0.01f)
				{
					zoomDelta = pinchDelta / Screen.height * pinchSensitivity;
					return true;
				}

				return false;
			}

			if (Input.mouseScrollDelta.magnitude > 0)
			{
				zoomDelta = Input.GetAxis("Mouse ScrollWheel");
				return true;
			}

			return false;
#endif
		}

		/// <summary>
		/// Detects two-finger vertical drag for pitch/tilt control.
		/// Both fingers moving in the same vertical direction = tilt.
		/// Only returns true if tilt is the dominant gesture (vs pinch).
		/// </summary>
		protected bool GetTwoFingerTiltDelta(out float tiltDelta, float tiltSensitivity = 0.5f)
		{
			tiltDelta = 0f;

#if MAPBOX_NEW_INPUT_SYSTEM
			if (Touch.activeTouches.Count < 2)
				return false;

			var touch0 = Touch.activeTouches[0];
			var touch1 = Touch.activeTouches[1];

			var deltaY0 = touch0.delta.y;
			var deltaY1 = touch1.delta.y;

			if (deltaY0 * deltaY1 <= 0)
				return false;

			var avgDeltaY = (deltaY0 + deltaY1) / 2f;
			if (Mathf.Abs(avgDeltaY) < 1f)
				return false;

			var currentDistance = Vector2.Distance(touch0.screenPosition, touch1.screenPosition);
			var pinchDelta = _pinchActive ? Mathf.Abs(currentDistance - _previousPinchDistance) : 0f;

			if (Mathf.Abs(avgDeltaY) <= pinchDelta)
				return false;

			tiltDelta = avgDeltaY / Screen.height * tiltSensitivity;
			return true;
#else
			if (Input.touchCount < 2)
				return false;

			var touch0 = Input.GetTouch(0);
			var touch1 = Input.GetTouch(1);

			var deltaY0 = touch0.deltaPosition.y;
			var deltaY1 = touch1.deltaPosition.y;

			// Both fingers must move in the same vertical direction
			if (deltaY0 * deltaY1 <= 0)
				return false;

			var avgDeltaY = (deltaY0 + deltaY1) / 2f;
			if (Mathf.Abs(avgDeltaY) < 1f)
				return false;

			// Compare tilt magnitude vs pinch magnitude to pick the dominant gesture
			var currentDistance = Vector2.Distance(touch0.position, touch1.position);
			var pinchDelta = _pinchActive ? Mathf.Abs(currentDistance - _previousPinchDistance) : 0f;

			if (Mathf.Abs(avgDeltaY) <= pinchDelta)
				return false;

			tiltDelta = avgDeltaY / Screen.height * tiltSensitivity;
			return true;
#endif
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
