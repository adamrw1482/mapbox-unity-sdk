using System;

namespace Mapbox.LocationModule
{
    public class MapboxLocationProvider : AbstractLocationProvider, IMapboxDeviceLocation
    {
        public event Action<Location> LocationUpdated;
        public event Action<MapboxLocationServiceStatus> AuthorizationChanged;
        public event Action<AccuracyAuthorization> AccuracyAuthorizationChanged;
        public event Action<bool> AvailabilityChanged;
        
        public MapboxLocationSettings Settings;
        private IMapboxDeviceLocation _mapboxDeviceLocation;

        public void Initialize()
        {
#if UNITY_IOS &&  !UNITY_EDITOR
            _mapboxDeviceLocation = new MapboxLocationIos(Settings);
#elif UNITY_ANDROID && !UNITY_EDITOR
            _mapboxDeviceLocation = new MapboxLocationAndroid(Settings);
#endif

            _mapboxDeviceLocation.LocationUpdated += (s) => { SendLocation(s); };
            _mapboxDeviceLocation.AvailabilityChanged += AvailabilityChanged;
            _mapboxDeviceLocation.AuthorizationChanged += AuthorizationChanged;
            _mapboxDeviceLocation.AccuracyAuthorizationChanged += AccuracyAuthorizationChanged;

            _mapboxDeviceLocation.Initialize();
        }

        public void Update()
        {
            _mapboxDeviceLocation.Update();
        }

        public void OnDestroy()
        {
            _mapboxDeviceLocation.OnDestroy();
        }
    }
}