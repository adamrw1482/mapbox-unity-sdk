using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Utilities;
using Mapbox.Example.Scripts.Map;
using UnityEngine;

namespace Mapbox.Example.Scripts.MapInput
{
	public class Moving3dCameraBehaviour : MonoBehaviour
	{
		public MapBehaviourCore MapBehaviour;
		public Camera Camera;
		public Moving3dCamera Core;

		private MapboxMap _map;
		private bool _isInitialized = false;
 
		private void Awake()
		{
			if (MapBehaviour == null) MapBehaviour = FindObjectOfType<MapBehaviourCore>();
			if(Camera == null) Camera = Camera.main;
			
			MapBehaviour.Initialized += (map) =>
			{
				_map = map;
				_isInitialized = true;
				Core.Initialize(Camera, _map.MapInformation);
			};
		}

		public void Update()
		{
			if (_isInitialized && _map.MapInformation != null && Core.UpdateCamera(_map.MapInformation))
			{
				_map.ChangeView(null, Core.ZoomValue, Core.Pitch, Core.Bearing);
			}
		}

		public Vector3 GetViewCenterPosition()
		{
			return Core.GetViewCenterPosition();
		}

		public void Zoom(MapInformation mapInformation, Vector3 point, float zoomAction)
		{
			Core.Zoom(mapInformation, point, zoomAction);
		}
		
		public void Zoom(MapInformation mapInformation, float zoomAction)
		{
			Core.Zoom(mapInformation, GetViewCenterPosition(), zoomAction);
		}
	}
}