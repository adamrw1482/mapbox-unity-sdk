using System;
using UnityEngine;

namespace Mapbox.Example.Scripts.MapInput
{
	/// <summary>
	/// Backend-agnostic key polling. Used by example utilities that need a tiny slice
	/// of keyboard input (e.g. TileProviderBenchmark's F5/F6 toggle) without pulling
	/// in the full <see cref="IPointerInput"/> abstraction. Concrete implementations
	/// live alongside their pointer counterparts in the backend sub-asmdefs.
	/// </summary>
	public interface IKeyInput
	{
		/// <summary>
		/// True on the frame <paramref name="key"/> transitions from up to down.
		/// Both backends interpret <see cref="KeyCode"/> the same way; the new-input
		/// implementation maps the KeyCode onto <c>UnityEngine.InputSystem.Key</c>
		/// internally.
		/// </summary>
		bool WasKeyPressedThisFrame(KeyCode key);
	}

	/// <summary>
	/// Priority-keyed registry mirroring <see cref="PointerInputFactory"/>. See that
	/// type's docs for the rationale (Legacy=0, NewInputSystem=10, highest wins).
	/// </summary>
	public static class KeyInputFactory
	{
		private static int _registeredPriority = int.MinValue;
		private static Func<IKeyInput> _factory;

		public static void Register(int priority, Func<IKeyInput> factory)
		{
			if (factory == null) return;
			if (priority >= _registeredPriority)
			{
				_registeredPriority = priority;
				_factory = factory;
			}
		}

		public static IKeyInput Create()
		{
			if (_factory != null) return _factory();
			Debug.LogError(
				"KeyInputFactory: no IKeyInput backend registered. " +
				"Ensure either MapboxExamples.LegacyInput or MapboxExamples.NewInputSystem " +
				"is compiling (Project Settings → Player → Active Input Handling).");
			return new NullKeyInput();
		}

		private sealed class NullKeyInput : IKeyInput
		{
			public bool WasKeyPressedThisFrame(KeyCode key) => false;
		}
	}
}
