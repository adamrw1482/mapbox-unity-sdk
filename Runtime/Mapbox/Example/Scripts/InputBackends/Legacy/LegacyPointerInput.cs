using Mapbox.Example.Scripts.MapInput;
using UnityEngine;

namespace Mapbox.Example.Scripts.InputBackends.Legacy
{
	/// <summary>
	/// <see cref="IPointerInput"/> backed by the legacy <c>UnityEngine.Input</c> API.
	/// Compiled only when <c>ENABLE_LEGACY_INPUT_MANAGER</c> is defined (Active Input
	/// Handling = "Old" or "Both") via this asmdef's defineConstraints.
	/// </summary>
	internal sealed class LegacyPointerInput : IPointerInput
	{
		public void EnableTouchSupport() { /* legacy touch is auto-enabled */ }

		public int TouchCount => Input.touchCount;
		public Vector2 GetTouchPosition(int index) => Input.GetTouch(index).position;
		public float GetTouchDeltaY(int index) => Input.GetTouch(index).deltaPosition.y;
		public int GetTouchId(int index) => Input.GetTouch(index).fingerId;
		public int GetTouchPointerId(int index) => Input.GetTouch(index).fingerId;

		public PointerTouchPhase GetTouchPhase(int index)
		{
			switch (Input.GetTouch(index).phase)
			{
				case TouchPhase.Began: return PointerTouchPhase.Began;
				case TouchPhase.Moved: return PointerTouchPhase.Moved;
				case TouchPhase.Stationary: return PointerTouchPhase.Stationary;
				case TouchPhase.Ended: return PointerTouchPhase.Ended;
				case TouchPhase.Canceled: return PointerTouchPhase.Canceled;
				default: return PointerTouchPhase.None;
			}
		}

		public Vector3 MousePosition => Input.mousePosition;
		public bool MouseLeftPressedThisFrame => Input.GetMouseButtonDown(0);
		public bool MouseLeftHeld => Input.GetMouseButton(0);
		public bool MouseRightPressedThisFrame => Input.GetMouseButtonDown(1);
		public bool MouseRightHeld => Input.GetMouseButton(1);

		// Input.GetAxis("Mouse ScrollWheel") returns 0 when not scrolling, so we
		// don't need the historic Mathf.Abs(mouseScrollDelta) > 0 short-circuit.
		public float MouseScrollY => Input.GetAxis("Mouse ScrollWheel");
	}
}
