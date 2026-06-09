using Mapbox.Example.Scripts.MapInput;
using UnityEngine;
using UnityEngine.InputSystem;
using NewTouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using NewTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Mapbox.Example.Scripts.InputBackends.NewInputSystem
{
	/// <summary>
	/// <see cref="IPointerInput"/> backed by the <c>com.unity.inputsystem</c> package.
	/// Compiled only when <c>ENABLE_INPUT_SYSTEM</c> is defined (Active Input Handling
	/// = "New" or "Both") via this asmdef's defineConstraints — Unity won't allow
	/// either mode without the package installed, so the InputSystem reference
	/// always resolves when this asmdef is included in compilation.
	/// </summary>
	internal sealed class NewInputPointerInput : IPointerInput
	{
		public void EnableTouchSupport()
		{
			if (!UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.enabled)
				UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.Enable();
		}

		public int TouchCount => NewTouch.activeTouches.Count;
		public Vector2 GetTouchPosition(int index) => NewTouch.activeTouches[index].screenPosition;
		public float GetTouchDeltaY(int index) => NewTouch.activeTouches[index].delta.y;
		public int GetTouchId(int index) => NewTouch.activeTouches[index].touchId;
		public int GetTouchPointerId(int index) => NewTouch.activeTouches[index].finger.index;

		public PointerTouchPhase GetTouchPhase(int index)
		{
			switch (NewTouch.activeTouches[index].phase)
			{
				case NewTouchPhase.Began: return PointerTouchPhase.Began;
				case NewTouchPhase.Moved: return PointerTouchPhase.Moved;
				case NewTouchPhase.Stationary: return PointerTouchPhase.Stationary;
				case NewTouchPhase.Ended: return PointerTouchPhase.Ended;
				case NewTouchPhase.Canceled: return PointerTouchPhase.Canceled;
				default: return PointerTouchPhase.None;
			}
		}

		public Vector3 MousePosition =>
			Mouse.current != null ? (Vector3)Mouse.current.position.ReadValue() : Vector3.zero;

		public bool MouseLeftPressedThisFrame =>
			Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
		public bool MouseLeftHeld =>
			Mouse.current != null && Mouse.current.leftButton.isPressed;

		public bool MouseRightPressedThisFrame =>
			Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
		public bool MouseRightHeld =>
			Mouse.current != null && Mouse.current.rightButton.isPressed;

		// Raw scroll.y is ~120 per notch on Windows; legacy Input.GetAxis returns
		// ~0.1 per notch. /1200f normalizes to the legacy scale so both backends feed
		// the same magnitude into ZoomSensitivity.
		public float MouseScrollY =>
			Mouse.current != null ? Mouse.current.scroll.ReadValue().y / 1200f : 0f;
	}
}
