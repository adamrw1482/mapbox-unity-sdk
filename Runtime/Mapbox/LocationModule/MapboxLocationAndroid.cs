//#if !UNITY_EDITOR && UNITY_ANDROID
using System;
using Mapbox.BaseModule.Data.Vector2d;
using Mapbox.BaseModule.Utilities;
using UnityEngine;

namespace Mapbox.LocationModule
{
    public class MapboxLocationAndroid : IMapboxDeviceLocation
    {
        public event Action<Location> LocationUpdated = delegate { };
        public MapboxLocationSettings _mapboxLocationSettings;

        private string _mapboxLocationServiceFactoryClassName = "com.mapbox.common.location.LocationServiceFactory";
        private string _mapboxLocationServiceFactoryGetMethodName = "getOrCreate";
        private string _mapboxLocationServiceGetProviderMethodName = "getDeviceLocationProvider";
        private string _mapboxLocationProviderRequestClassName = "com.mapbox.common.location.LocationProviderRequest";
        private string _mapboxLocationInvervalClassName = "com.mapbox.common.location.IntervalSettings";


        private AndroidJavaClass _locationServiceFactory;
        private AndroidJavaObject _locationService;
        private AndroidJavaObject _locationProvider;
        private MapboxLocationObserverProxy _observer;
        private AndroidJavaObject _accuracyLevelHigh;

        private string _intervalSettingsBuilderClassName = "com.mapbox.common.location.IntervalSettings$Builder";
        private string _minimumIntervalFieldName = "minimumInterval";
        private string _maximumIntervalFieldName = "maximumInterval";
        private string _intervalFieldName = "interval";

        private string _locationProviderRequestBuilderClassName =
            "com.mapbox.common.location.LocationProviderRequest$Builder";

        private string _accuracyLevelClassName = "com.mapbox.common.location.AccuracyLevel";
        private string _accuracyFieldName = "accuracy";
        private string _displacementFieldName = "displacement";
        private string _intervalSettingFieldName = "interval";

        private string _addObserverMethodName = "addLocationObserver";

        private string javaLangLong = "java.lang.Long";
        private string javaLangFloat = "java.lang.Float";

        public MapboxLocationAndroid(MapboxLocationSettings settings)
        {
            _mapboxLocationSettings = settings;
            _locationServiceFactory = new AndroidJavaClass(_mapboxLocationServiceFactoryClassName);
            _locationService =
                _locationServiceFactory.CallStatic<AndroidJavaObject>(_mapboxLocationServiceFactoryGetMethodName);


            var intervalSettings = new AndroidJavaObject(_intervalSettingsBuilderClassName)
                .Call<AndroidJavaObject>(_minimumIntervalFieldName,
                    new AndroidJavaObject(javaLangLong, _mapboxLocationSettings.MinimumInterval))
                .Call<AndroidJavaObject>(_maximumIntervalFieldName,
                    new AndroidJavaObject(javaLangLong, _mapboxLocationSettings.MaximumInterval))
                .Call<AndroidJavaObject>(_intervalFieldName,
                    new AndroidJavaObject(javaLangLong, _mapboxLocationSettings.Interval))
                .Call<AndroidJavaObject>("build");

            GetAccuracyLevel();

            var requestSettings = new AndroidJavaObject(_locationProviderRequestBuilderClassName)
                .Call<AndroidJavaObject>(_accuracyFieldName, _accuracyLevelHigh)
                .Call<AndroidJavaObject>(_displacementFieldName,
                    new AndroidJavaObject(javaLangFloat, _mapboxLocationSettings.Displacement))
                .Call<AndroidJavaObject>(_intervalSettingFieldName, intervalSettings)
                .Call<AndroidJavaObject>("build");

            var expected = _locationService.Call<AndroidJavaObject>(_mapboxLocationServiceGetProviderMethodName, requestSettings);
            var hasValue = expected.Call<bool>("isValue");

            if (!hasValue)
            {
                AndroidJavaObject error = expected.Call<AndroidJavaObject>("getError");
                Debug.LogError("Mapbox error: " + error.Call<string>("toString"));
                return;
            }

            _locationProvider = expected.Call<AndroidJavaObject>("getValue");
            _observer = new MapboxLocationObserverProxy(LocationUpdated);
            _locationProvider.Call(_addObserverMethodName, _observer);
        }

        public void Update()
        {

        }

        public void OnDestroy()
        {

        }

        private void GetAccuracyLevel()
        {
            var accuracyLevel = new AndroidJavaClass(_accuracyLevelClassName);
            var accuArray = accuracyLevel.CallStatic<AndroidJavaObject>("values");
            var convertedArray = AndroidJNIHelper.ConvertFromJNIArray<AndroidJavaObject[]>(accuArray.GetRawObject());
            foreach (var javaEnumObject in convertedArray)
            {
                var enumString = javaEnumObject.Call<string>("toString");
                if (enumString == _mapboxLocationSettings.AccuracyLevel.ToString())
                {
                    _accuracyLevelHigh = javaEnumObject;
                    break;
                }
            }
        }

        private string GetJavaClassName(AndroidJavaObject obj)
        {
            if (obj == null)
                return "<null>";

            using var clazz = obj.Call<AndroidJavaObject>("getClass");
            return clazz.Call<string>("getName");
        }
    }

    public class MapboxLocationObserverProxy : AndroidJavaProxy
    {
        private Action<Location> _sendLocation;
        private Location _location;

        public MapboxLocationObserverProxy(Action<Location> sendLocation) : base("com.mapbox.common.location.LocationObserver")
        {
            _sendLocation = sendLocation;
        }

        void onLocationUpdateReceived(AndroidJavaObject locations)
        {
            if (locations == null)
                return;

            var size = locations.Call<int>("size");
            for (var i = 0; i < size; i++)
            {
                var loc = locations.Call<AndroidJavaObject>("get", i);

                if (loc == null)
                    continue;

                var bearing = ReadOptionalValue(loc, "bearing");
                var speed = ReadOptionalValue(loc, "speed");

                _location.LatitudeLongitude =
                    new LatitudeLongitude(loc.Call<double>("getLatitude"), loc.Call<double>("getLongitude"));
                _location.UserHeading = bearing.HasValue ? (float)bearing.Value : 0;
                _location.SpeedMetersPerSecond = speed.HasValue ? (float)speed.Value : 0;
                _location.TimestampDevice = UnixTimestampUtils.To(DateTime.UtcNow);

                _sendLocation(_location);
            }
        }

        double? ReadOptionalValue(AndroidJavaObject location, string fieldName)
        {
            using var bearingOpt =
                location.Call<AndroidJavaObject>(fieldName);

            if (!bearingOpt.Call<bool>("isPresent"))
                return null;

            return bearingOpt.Call<double>("get");
        }
    }
}
//#endif