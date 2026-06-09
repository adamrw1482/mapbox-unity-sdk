using Mapbox.Example.Scripts.MapInput;
using UnityEngine;

namespace Mapbox.Example.Scripts.InputBackends.Legacy
{
	/// <summary>
	/// <see cref="IKeyInput"/> backed by the legacy <c>UnityEngine.Input</c> API.
	/// Compiled only when <c>ENABLE_LEGACY_INPUT_MANAGER</c> is defined.
	/// </summary>
	internal sealed class LegacyKeyInput : IKeyInput
	{
		public bool WasKeyPressedThisFrame(KeyCode key) => Input.GetKeyDown(key);
	}
}
