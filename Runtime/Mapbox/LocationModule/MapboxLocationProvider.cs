namespace Mapbox.LocationModule
{
    public class MapboxLocationProvider : AbstractLocationProvider
    {
        public MapboxLocationSettings Settings;
        private IMapboxDeviceLocation _mapboxDeviceLocation;

        private void Awake()
        {
#if UNITY_IOS &&  !UNITY_EDITOR
            _mapboxDeviceLocation = new LocationIos(Settings);
#elif UNITY_ANDROID && !UNITY_EDITOR
            _mapboxDeviceLocation = new CommonAndroidDeviceLocationProvider(Settings);
#endif

            _mapboxDeviceLocation.LocationUpdated += SendLocation;
        }

        private void Update()
        {
            _mapboxDeviceLocation.Update();
        }

        private void OnDestroy()
        {
            _mapboxDeviceLocation.OnDestroy();
        }
    }
}