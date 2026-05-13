using Mapbox.BaseModule.Map;
using UnityEngine;
using UnityEngine.Serialization;

namespace Mapbox.Example.Scripts.MapInput
{
	public class SlippyMapCameraBehaviour : MapCameraBehaviour<SlippyMapCamera>
	{
		[Tooltip("Slippy map camera settings. Camera stays static while the map moves underneath")]
		[FormerlySerializedAs("Core")]
		[SerializeField] private SlippyMapCamera _core;

		public override SlippyMapCamera Core => _core;

		protected override void OnMapInitialized(MapboxMap map)
		{
			Map = map;
			IsInitialized = true;
			_core.Initialize(Camera, map.MapInformation, new Plane(MapBehaviour.transform.up, MapBehaviour.transform.position));
		}
	}
}
