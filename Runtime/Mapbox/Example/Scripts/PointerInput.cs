using System;
using UnityEngine;

namespace Mapbox.Example.Scripts.MapInput
{
	/// <summary>
	/// Backend-agnostic touch phase. Both legacy <c>UnityEngine.TouchPhase</c> and
	/// <c>UnityEngine.InputSystem.TouchPhase</c> are mapped onto this enum so the
	/// MapInput call-sites don't need to know which input system is compiled in.
	/// </summary>
	public enum PointerTouchPhase
	{
		None,
		Began,
		Moved,
		Stationary,
		Ended,
		Canceled
	}

	/// <summary>
	/// Thin abstraction over Unity's two input backends. Concrete implementations
	/// live in the sibling <c>MapboxExamples.LegacyInput</c> / <c>MapboxExamples.NewInputSystem</c>
	/// asmdefs, each gated by Active Input Handling defines, and self-register with
	/// <see cref="PointerInputFactory"/> at <c>SubsystemRegistration</c>. MapInput
	/// consumes <see cref="IPointerInput"/> via the factory and stays free of any
	/// #if branching on the input backend.
	/// </summary>
	public interface IPointerInput
	{
		/// <summary>
		/// Called once from <c>MapInput.Initialize</c>. Legacy is a no-op; new-input
		/// path enables <c>EnhancedTouchSupport</c>.
		/// </summary>
		void EnableTouchSupport();

		int TouchCount { get; }
		Vector2 GetTouchPosition(int index);
		float GetTouchDeltaY(int index);
		int GetTouchId(int index);
		PointerTouchPhase GetTouchPhase(int index);
		/// <summary>
		/// Identifier suitable for <c>EventSystem.IsPointerOverGameObject(int)</c>.
		/// Returns <c>finger.index</c> on the new system and <c>fingerId</c> on legacy
		/// — both are what their respective EventSystem expects.
		/// </summary>
		int GetTouchPointerId(int index);

		Vector3 MousePosition { get; }
		bool MouseLeftPressedThisFrame { get; }
		bool MouseLeftHeld { get; }
		bool MouseRightPressedThisFrame { get; }
		bool MouseRightHeld { get; }
		/// <summary>
		/// Scroll-wheel delta normalized so both backends feed approximately the same
		/// magnitude into ZoomSensitivity (~0.1 per Windows notch). Returns 0 when not
		/// scrolling and when the mouse device is unavailable.
		/// </summary>
		float MouseScrollY { get; }
	}

	/// <summary>
	/// Priority-keyed registry for <see cref="IPointerInput"/> implementations.
	/// Each backend asmdef (Legacy=0, NewInputSystem=10) registers itself from a
	/// <c>RuntimeInitializeOnLoadMethod</c>. Under Active Input Handling = "Both",
	/// both register and the higher priority wins — load-order between assemblies
	/// is not relied upon.
	/// </summary>
	public static class PointerInputFactory
	{
		private static int _registeredPriority = int.MinValue;
		private static Func<IPointerInput> _factory;

		public static void Register(int priority, Func<IPointerInput> factory)
		{
			if (factory == null) return;
			// >= so re-registration with same priority (e.g. Enter-Play-Mode with
			// domain reload disabled re-runs the registrar) replaces cleanly.
			if (priority >= _registeredPriority)
			{
				_registeredPriority = priority;
				_factory = factory;
			}
		}

		public static IPointerInput Create()
		{
			if (_factory != null) return _factory();
			Debug.LogError(
				"PointerInputFactory: no IPointerInput backend registered. " +
				"Ensure either MapboxExamples.LegacyInput or MapboxExamples.NewInputSystem " +
				"is compiling (Project Settings → Player → Active Input Handling).");
			return new NullPointerInput();
		}

		// Returned when no backend asmdef has registered. Keeps cameras alive
		// (no NREs) so the missing-backend error message is the only symptom.
		private sealed class NullPointerInput : IPointerInput
		{
			public void EnableTouchSupport() { }
			public int TouchCount => 0;
			public Vector2 GetTouchPosition(int index) => Vector2.zero;
			public float GetTouchDeltaY(int index) => 0f;
			public int GetTouchId(int index) => 0;
			public int GetTouchPointerId(int index) => 0;
			public PointerTouchPhase GetTouchPhase(int index) => PointerTouchPhase.None;
			public Vector3 MousePosition => Vector3.zero;
			public bool MouseLeftPressedThisFrame => false;
			public bool MouseLeftHeld => false;
			public bool MouseRightPressedThisFrame => false;
			public bool MouseRightHeld => false;
			public float MouseScrollY => 0f;
		}
	}
}
