using Mapbox.Example.Scripts.MapInput;
using UnityEngine;
using UnityEngine.Scripting;

namespace Mapbox.Example.Scripts.InputBackends.NewInputSystem
{
	/// <summary>
	/// Registers the new-input backend with <see cref="PointerInputFactory"/> and
	/// <see cref="KeyInputFactory"/> at <c>SubsystemRegistration</c>. Priority 10
	/// — wins over the legacy registrar (priority 0) when both compile under
	/// Active Input Handling = "Both". <c>[Preserve]</c> guards against IL2CPP
	/// managed-code stripping.
	/// </summary>
	[Preserve]
	internal static class NewInputSystemRegistrar
	{
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void Register()
		{
			PointerInputFactory.Register(10, () => new NewInputPointerInput());
			KeyInputFactory.Register(10, () => new NewInputKeyInput());
		}
	}
}
