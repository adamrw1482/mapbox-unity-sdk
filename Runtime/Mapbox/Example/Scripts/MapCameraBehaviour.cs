using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Utilities;
using Mapbox.Example.Scripts.Map;
using UnityEngine;

namespace Mapbox.Example.Scripts.MapInput
{
    /// <summary>
    /// Generic base MonoBehaviour for any MapInput-based camera.
    /// Handles initialization, the Update loop, and ChangeView dispatch.
    /// Subclass with a concrete camera type, or use directly if no extra behaviour is needed.
    /// </summary>
    public abstract class MapCameraBehaviour<T> : MonoBehaviour where T : MapInput
    {
        [Tooltip("Reference to the map behaviour that provides map state and tile management")]
        public MapBehaviourCore MapBehaviour;
        [Tooltip("Camera used for map rendering and input raycasting. Falls back to Camera.main if not set")]
        public Camera Camera;

        protected MapboxMap Map;
        protected bool IsInitialized;

        public abstract T Core { get; }

        protected virtual void Awake()
        {
            if (MapBehaviour == null) MapBehaviour = FindObjectOfType<MapBehaviourCore>();
            if (Camera == null) Camera = Camera.main;

            MapBehaviour.Initialized += OnMapInitialized;
        }

        protected virtual void OnDestroy()
        {
            if (MapBehaviour != null)
                MapBehaviour.Initialized -= OnMapInitialized;
        }

        protected virtual void OnMapInitialized(MapboxMap map)
        {
            Map = map;
            IsInitialized = true;
            Core.Initialize(Camera, map.MapInformation);
        }

        protected virtual void Update()
        {
            if (!IsInitialized || Map.MapInformation == null)
                return;

            var output = Core.UpdateCamera(Map.MapInformation);
            if (output.HasChanged)
                Map.ChangeView(output.Center, output.Zoom, output.Pitch, output.Bearing, output.Scale);
        }
    }
}
