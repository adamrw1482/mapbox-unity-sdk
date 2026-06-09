using Mapbox.Example.Scripts.MapInput;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Mapbox.Example.Scripts.InputBackends.NewInputSystem
{
	/// <summary>
	/// <see cref="IKeyInput"/> backed by the new Input System. Maps <see cref="KeyCode"/>
	/// onto <see cref="Key"/> for the benchmark's small set of accepted hotkeys —
	/// extend the switch as new bindings are added rather than a generic mapping table,
	/// to keep the surface explicit.
	/// </summary>
	internal sealed class NewInputKeyInput : IKeyInput
	{
		public bool WasKeyPressedThisFrame(KeyCode key)
		{
			if (Keyboard.current == null) return false;
			var mapped = KeyCodeToKey(key);
			return mapped != Key.None && Keyboard.current[mapped].wasPressedThisFrame;
		}

		private static Key KeyCodeToKey(KeyCode kc)
		{
			switch (kc)
			{
				case KeyCode.F5: return Key.F5;
				case KeyCode.F6: return Key.F6;
				case KeyCode.F7: return Key.F7;
				case KeyCode.F8: return Key.F8;
				case KeyCode.Space: return Key.Space;
				case KeyCode.Return: return Key.Enter;
				default: return Key.None;
			}
		}
	}
}
