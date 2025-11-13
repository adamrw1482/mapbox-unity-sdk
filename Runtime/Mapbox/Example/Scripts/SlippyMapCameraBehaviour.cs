using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Utilities;
using Mapbox.Example.Scripts.Map;
using UnityEngine;

namespace Mapbox.Example.Scripts.MapInput
{
	public class SlippyMapCameraBehaviour : MonoBehaviour
	{
		public MapBehaviourCore MapBehaviour;
		public Camera Camera;
		public SlippyMapCamera Core;

		private MapboxMap _map;
		private bool _isInitialized = false;
 
		private void Awake()
		{
			MapBehaviour.Initialized += (map) =>
			{
				_map = map;
				_isInitialized = true;
				Core.Initialize(Camera, _map.MapInformation, new Plane(MapBehaviour.transform.up, MapBehaviour.transform.position));
			};
		}

		public void Update()
		{
			if (_isInitialized && _map.MapInformation != null && Core.UpdateCamera(_map.MapInformation))
			{
				_map.ChangeView(Core.CenterLatitudeLongitude, Core.ZoomValue, Core.Pitch, Core.Bearing, Core.ScaleValue);
			}
		}
	}
}