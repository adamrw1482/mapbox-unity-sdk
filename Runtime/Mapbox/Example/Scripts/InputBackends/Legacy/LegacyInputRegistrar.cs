using Mapbox.Example.Scripts.MapInput;
using UnityEngine;
using UnityEngine.Scripting;

namespace Mapbox.Example.Scripts.InputBackends.Legacy
{
	/// <summary>
	/// Registers the legacy backend with <see cref="PointerInputFactory"/> and
	/// <see cref="KeyInputFactory"/> at <c>SubsystemRegistration</c> — earlier than
	/// any <c>Awake</c>, so MapInput / TileProviderBenchmark see a populated factory.
	/// Priority 0; the new-input registrar (priority 10) overrides under "Both".
	/// <c>[Preserve]</c> guards against IL2CPP managed-code stripping removing the
	/// type before its <c>RuntimeInitializeOnLoadMethod</c> can fire.
	/// </summary>
	[Preserve]
	internal static class LegacyInputRegistrar
	{
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void Register()
		{
			PointerInputFactory.Register(0, () => new LegacyPointerInput());
			KeyInputFactory.Register(0, () => new LegacyKeyInput());
		}
	}
}
