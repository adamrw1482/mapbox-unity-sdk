using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Utilities;
using Mapbox.LocationModule.Scripts;
using UnityEngine;
//using Mapbox.LocationModule.Scripts;

namespace Mapbox.LocationModule.Helpers
{
    public class SnapMapToLocationProvider : MonoBehaviour
    {
        public bool InitializeMap = true;
        public bool ContinueAfterInitialization = false;
        [SerializeField]
        private LocationProviderFactory _locationProvider;

        [SerializeField] private MapBehaviourCore _map;
        private bool _initializeStarted = false;
        
        private void Start()
        {
            if(_locationProvider == null)
            {
                Debug.LogError("SnapMapToLocationProvider: _locationProvider is null. Component will not function.", this);
                return;
            }

            if(_locationProvider.DefaultLocationProvider == null)
            {
                Debug.LogError("SnapMapToLocationProvider: DefaultLocationProvider is null. Component will not function.", this);
                return;
            }

            if(!enabled)
                return;
        
            UnityEngine.Input.location.Start();
            _locationProvider.DefaultLocationProvider.OnLocationUpdated += (s) =>
            {
                if (_map.InitializationStatus == InitializationStatus.WaitingForInitialization && InitializeMap && !_initializeStarted)
                {
                    _initializeStarted = true;
                    _map.MapInformation.Initialize(s.LatitudeLongitude);
                    _map.Initialize();
                }
                if (ContinueAfterInitialization)
                {
                    _map.MapInformation.SetInformation(s.LatitudeLongitude);
                }
                
            };
        }
    }
}
