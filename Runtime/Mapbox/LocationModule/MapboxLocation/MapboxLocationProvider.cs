using System;
using Mapbox.LocationModule.Scripts;

namespace Mapbox.LocationModule.MapboxLocation
{
    public class MapboxLocationProvider : AbstractLocationProvider
    {
        public event Action<Location> LocationUpdated;
        public event Action<MapboxLocationServiceStatus> AuthorizationChanged;
        public event Action<AccuracyAuthorization> AccuracyAuthorizationChanged;
        public event Action<bool> AvailabilityChanged;
        
        public MapboxLocationSettings Settings;
        private IMapboxDeviceLocation _mapboxDeviceLocation;

        public MapboxLocationProvider(MapboxLocationSettings settings)
        {
            Settings = settings;
#if UNITY_IOS &&  !UNITY_EDITOR
            _mapboxDeviceLocation = new MapboxLocationIos(Settings);
#elif UNITY_ANDROID && !UNITY_EDITOR
            _mapboxDeviceLocation = new MapboxLocationAndroid(Settings);
#endif

            if (_mapboxDeviceLocation != null)
            {
                _mapboxDeviceLocation.LocationUpdated += (s) =>
                {
                    _currentLocation = s;
                    SendLocation(s);
                };
                _mapboxDeviceLocation.AvailabilityChanged += AvailabilityChanged;
                _mapboxDeviceLocation.AuthorizationChanged += AuthorizationChanged;
                _mapboxDeviceLocation.AccuracyAuthorizationChanged += AccuracyAuthorizationChanged;

                _mapboxDeviceLocation.Initialize();
            }
        }

        public override void Update()
        {
            _mapboxDeviceLocation?.Update();
        }

        public override void OnDestroy()
        {
            _mapboxDeviceLocation?.OnDestroy();
        }
    }
}