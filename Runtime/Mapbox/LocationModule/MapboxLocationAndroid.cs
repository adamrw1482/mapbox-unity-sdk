//#if !UNITY_EDITOR && UNITY_ANDROID
using System;
using System.Collections.Generic;
using Mapbox.BaseModule.Data.Vector2d;
using Mapbox.BaseModule.Utilities;
using UnityEngine;
using UnityEngine.Scripting;

namespace Mapbox.LocationModule
{
    [Preserve]
    public class MapboxLocationAndroid : IMapboxDeviceLocation
    {
        [Preserve] public event Action<Location> LocationUpdated;
        [Preserve] public event Action<MapboxLocationServiceStatus> AuthorizationChanged;
        [Preserve] public event Action<AccuracyAuthorization> AccuracyAuthorizationChanged;
        [Preserve] public event Action<bool> AvailabilityChanged;

        public MapboxLocationSettings _mapboxLocationSettings;

        private readonly Queue<Action> _mainThreadQueue = new Queue<Action>();
        private readonly object _queueLock = new object();

        private AndroidJavaObject _permissionsManager;
        private AndroidJavaObject _unityActivity;

        private string _mapboxLocationServiceFactoryClassName = "com.mapbox.common.location.LocationServiceFactory";
        private string _mapboxLocationServiceFactoryGetMethodName = "getOrCreate";
        private string _mapboxLocationServiceGetProviderMethodName = "getDeviceLocationProvider";
        private string _mapboxLocationProviderRequestClassName = "com.mapbox.common.location.LocationProviderRequest";
        private string _mapboxLocationInvervalClassName = "com.mapbox.common.location.IntervalSettings";


        private AndroidJavaClass _locationServiceFactory;
        private AndroidJavaObject _locationService;
        private AndroidJavaObject _locationProvider;
        private MapboxLocationObserverProxy _locationObserver;
        private MapboxLocationServiceObserverProxy _serviceObserver;
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

        private string _addServiceObserverMethodName = "registerObserver";
        private string _addObserverMethodName = "addLocationObserver";

        private string javaLangLong = "java.lang.Long";
        private string javaLangFloat = "java.lang.Float";

        public MapboxLocationAndroid(MapboxLocationSettings settings)
        {
            _mapboxLocationSettings = settings;
            Debug.Log("[Android] MapboxLocationAndroid constructor called");

            // Get Unity activity
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                _unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            }

            // Check if permissions are already granted
            using (var permissionsManagerClass = new AndroidJavaClass("com.mapbox.android.core.permissions.PermissionsManager"))
            {
                bool hasPermission = permissionsManagerClass.CallStatic<bool>("areLocationPermissionsGranted", _unityActivity);
                Debug.Log($"[Android] Location permissions granted: {hasPermission}");

                if (hasPermission)
                {
                    InitializeMapboxLocation();
                }
                else
                {
                    RequestLocationPermissions();
                }
            }
        }

        private void RequestLocationPermissions()
        {
            Debug.Log("[Android] Requesting location permissions via PermissionsManager");

            var permissionsListenerProxy = new PermissionsListenerProxy(
                onExplanation: (permissions) =>
                {
                    Debug.Log($"[Android] Permission explanation needed for: {string.Join(", ", permissions)}");
                },
                onResult: (granted) =>
                {
                    Debug.Log($"[Android] Permission result: {granted}");
                    if (granted)
                    {
                        InitializeMapboxLocation();
                    }
                    else
                    {
                        Debug.LogError("[Android] Location permissions denied");
                    }
                });

            _permissionsManager = new AndroidJavaObject("com.mapbox.android.core.permissions.PermissionsManager", permissionsListenerProxy);
            _permissionsManager.Call("requestLocationPermissions", _unityActivity);
        }

        private void InitializeMapboxLocation()
        {
            Debug.Log("[Android] InitializeMapboxLocation called");

            _locationServiceFactory = new AndroidJavaClass(_mapboxLocationServiceFactoryClassName);
            _locationService = _locationServiceFactory.CallStatic<AndroidJavaObject>(_mapboxLocationServiceFactoryGetMethodName);

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

            _serviceObserver = new MapboxLocationServiceObserverProxy(AuthorizationChanged, AccuracyAuthorizationChanged, AvailabilityChanged, EnqueueOnMainThread);

            try
            {
                _locationService.Call(_addServiceObserverMethodName, _serviceObserver);
                Debug.Log("[Android] Service observer registered successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Android] Failed to register service observer: {ex.Message}");
            }

            _locationProvider = expected.Call<AndroidJavaObject>("getValue");
            Debug.Log($"[Android] Location provider obtained: {GetJavaClassName(_locationProvider)}");

            _locationObserver = new MapboxLocationObserverProxy(EnqueueOnMainThread);
            _locationObserver.SendLocation += location =>
            {
                Debug.Log($"[Android] Location observer sent location: {location}");
                LocationUpdated?.Invoke(location);
            };

            try
            {
                Debug.Log($"[Android] About to call {_addObserverMethodName}");
                Debug.Log($"[Android] Provider class: {GetJavaClassName(_locationProvider)}");
                Debug.Log($"[Android] Observer implements: com.mapbox.common.location.LocationObserver");
                Debug.Log($"[Android] Observer hash: {_locationObserver.GetHashCode()}");

                // Keep a strong reference to prevent GC
                System.GC.KeepAlive(_locationObserver);

                _locationProvider.Call(_addObserverMethodName, _locationObserver);

                Debug.Log("[Android] addLocationObserver call completed successfully");
                Debug.Log($"[Android] Observer still valid: {_locationObserver != null}");

                // Verify Android location permissions
                try
                {
                    var permissionClass = new AndroidJavaClass("android.content.pm.PackageManager");
                    int permissionGranted = permissionClass.GetStatic<int>("PERMISSION_GRANTED");

                    string fineLocationPerm = "android.permission.ACCESS_FINE_LOCATION";
                    string coarseLocationPerm = "android.permission.ACCESS_COARSE_LOCATION";

                    int fineStatus = _unityActivity.Call<int>("checkSelfPermission", fineLocationPerm);
                    int coarseStatus = _unityActivity.Call<int>("checkSelfPermission", coarseLocationPerm);

                    Debug.Log($"[Android] FINE_LOCATION permission: {(fineStatus == permissionGranted ? "GRANTED" : "DENIED")}");
                    Debug.Log($"[Android] COARSE_LOCATION permission: {(coarseStatus == permissionGranted ? "GRANTED" : "DENIED")}");

                    if (fineStatus != permissionGranted && coarseStatus != permissionGranted)
                    {
                        Debug.LogError("[Android] No location permissions! This is why observer won't receive updates.");
                    }
                }
                catch (Exception permEx)
                {
                    Debug.LogWarning($"[Android] Could not verify permissions: {permEx.Message}");
                }

                Debug.Log("[Android] Observer registration complete - waiting for location updates");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Android] Failed to add location observer: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public void Initialize()
        {
            // Already initialized in constructor
        }

        public void Update()
        {
            // Process main thread queue
            lock (_queueLock)
            {
                while (_mainThreadQueue.Count > 0)
                {
                    var action = _mainThreadQueue.Dequeue();
                    try
                    {
                        action?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[Android] Error processing main thread action: {ex.Message}");
                    }
                }
            }
        }

        private void EnqueueOnMainThread(Action action)
        {
            lock (_queueLock)
            {
                _mainThreadQueue.Enqueue(action);
            }
        }

        public void GetLastLocation()
        {
            try
            {
                var callback = new GetLocationCallbackProxy((location, isValid) =>
                {
                    if (isValid)
                    {
                        EnqueueOnMainThread(() =>
                        {
                            LocationUpdated?.Invoke(location);
                            Debug.Log($"[Android] getLastLocation: Lat={location.LatitudeLongitude.Latitude}, Lon={location.LatitudeLongitude.Longitude}");
                        });
                    }
                    else
                    {
                        Debug.Log("[Android] getLastLocation: no location available");
                    }
                });

                _locationProvider.Call<AndroidJavaObject>("getLastLocation", callback);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Android] getLastLocation failed: {ex.Message}");
            }
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

    public class MapboxLocationServiceObserverProxy : AndroidJavaProxy
    {
        private readonly Action<MapboxLocationServiceStatus> _authorizationChanged;
        private readonly Action<AccuracyAuthorization> _accuracyAuthorizationChanged;
        private readonly Action<bool> _availabilityChanged;
        private readonly Action<Action> _enqueueOnMainThread;

        public MapboxLocationServiceObserverProxy(
            Action<MapboxLocationServiceStatus> authorizationChanged,
            Action<AccuracyAuthorization> accuracyAuthorizationChanged,
            Action<bool> availabilityChanged,
            Action<Action> enqueueOnMainThread) : base("com.mapbox.common.location.LocationServiceObserver")
        {
            _authorizationChanged = authorizationChanged;
            _accuracyAuthorizationChanged = accuracyAuthorizationChanged;
            _availabilityChanged = availabilityChanged;
            _enqueueOnMainThread = enqueueOnMainThread;
        }


        [Preserve]
        public void onAvailabilityChanged(bool isAvailable)
        {
            Debug.Log($"[Android] onAvailabilityChanged: {isAvailable}");
            _enqueueOnMainThread?.Invoke(() => _availabilityChanged?.Invoke(isAvailable));
        }

        [Preserve]
        public void onPermissionStatusChanged(AndroidJavaObject permission)
        {
            if (permission == null)
            {
                Debug.LogError("[Android] onPermissionStatusChanged received null");
                return;
            }

            int ordinal = permission.Call<int>("ordinal");
            var status = (MapboxLocationServiceStatus)ordinal;
            Debug.Log($"[Android] onPermissionStatusChanged: ordinal={ordinal}, status={status}");
            _enqueueOnMainThread?.Invoke(() => _authorizationChanged?.Invoke(status));
        }

        [Preserve]
        public void onAccuracyAuthorizationChanged(AndroidJavaObject authorization)
        {
            if (authorization == null)
            {
                Debug.LogError("[Android] onAccuracyAuthorizationChanged received null");
                return;
            }

            int ordinal = authorization.Call<int>("ordinal");
            var accuracy = (AccuracyAuthorization)ordinal;
            Debug.Log($"[Android] onAccuracyAuthorizationChanged: ordinal={ordinal}, accuracy={accuracy}");
            _enqueueOnMainThread?.Invoke(() => _accuracyAuthorizationChanged?.Invoke(accuracy));
        }
    }
    
    public class MapboxLocationObserverProxy : AndroidJavaProxy
    {
        public Action<Location> SendLocation;
        private readonly Action<Action> _enqueueOnMainThread;
        private static int _updateCount = 0;
        private static bool _firstCallLogged = false;

        public MapboxLocationObserverProxy(Action<Action> enqueueOnMainThread) : base("com.mapbox.common.location.LocationObserver")
        {
            _enqueueOnMainThread = enqueueOnMainThread;
            Debug.Log("[Android] MapboxLocationObserverProxy created");
            Debug.Log($"[Android] Proxy implements interface: com.mapbox.common.location.LocationObserver");
            Debug.Log($"[Android] Proxy javaInterface: {javaInterface}");
        }

        [Preserve]
        public void onLocationUpdateReceived(AndroidJavaObject locations)
        {
            _updateCount++;

            if (!_firstCallLogged)
            {
                _firstCallLogged = true;
                Debug.Log("========================================");
                Debug.Log("[Android] FIRST CALLBACK RECEIVED!");
                Debug.Log("========================================");
            }

            Debug.Log($"[Android] onLocationUpdateReceived called (update #{_updateCount})");

            if (locations == null)
            {
                Debug.LogWarning("[Android] locations list is null");
                return;
            }

            try
            {
                var size = locations.Call<int>("size");
                Debug.Log($"[Android] Received {size} locations");

                for (var i = 0; i < size; i++)
                {
                    var loc = locations.Call<AndroidJavaObject>("get", i);

                    if (loc == null)
                    {
                        Debug.LogWarning($"[Android] Location at index {i} is null");
                        continue;
                    }

                    double lat = loc.Call<double>("getLatitude");
                    double lon = loc.Call<double>("getLongitude");

                    Location location = new Location
                    {
                        LatitudeLongitude = new LatitudeLongitude(lat, lon),
                        TimestampDevice = UnixTimestampUtils.To(DateTime.UtcNow)
                    };

                    Debug.Log($"[Android] Location update #{_updateCount}: {lat}, {lon}");

                    // Invoke on main thread
                    _enqueueOnMainThread?.Invoke(() =>
                    {
                        SendLocation?.Invoke(location);
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Android] Error in onLocationUpdateReceived: {ex.Message}\n{ex.StackTrace}");
            }
        }

    }

    // Proxy for GetLocationCallback interface
    public class GetLocationCallbackProxy : AndroidJavaProxy
    {
        private readonly Action<Location, bool> _callback;

        public GetLocationCallbackProxy(Action<Location, bool> callback)
            : base("com.mapbox.common.location.GetLocationCallback")
        {
            _callback = callback;
        }

        [Preserve]
        public void run(AndroidJavaObject locationObj)
        {
            if (locationObj == null)
            {
                Debug.Log("[Android] GetLocationCallback: null location");
                _callback?.Invoke(default(Location), false);
                return;
            }

            try
            {
                double lat = locationObj.Call<double>("getLatitude");
                double lon = locationObj.Call<double>("getLongitude");

                Location location = new Location
                {
                    LatitudeLongitude = new LatitudeLongitude(lat, lon),
                    TimestampDevice = UnixTimestampUtils.To(DateTime.UtcNow)
                };

                _callback?.Invoke(location, true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Android] GetLocationCallback error: {ex.Message}");
                _callback?.Invoke(default(Location), false);
            }
        }
    }

    // Proxy for PermissionsListener interface
    public class PermissionsListenerProxy : AndroidJavaProxy
    {
        private readonly Action<string[]> _onExplanation;
        private readonly Action<bool> _onResult;

        public PermissionsListenerProxy(Action<string[]> onExplanation, Action<bool> onResult)
            : base("com.mapbox.android.core.permissions.PermissionsListener")
        {
            _onExplanation = onExplanation;
            _onResult = onResult;
        }

        [Preserve]
        public void onExplanationNeeded(AndroidJavaObject permissionsToExplain)
        {
            if (permissionsToExplain != null)
            {
                int size = permissionsToExplain.Call<int>("size");
                string[] permissions = new string[size];
                for (int i = 0; i < size; i++)
                {
                    permissions[i] = permissionsToExplain.Call<string>("get", i);
                }
                _onExplanation?.Invoke(permissions);
            }
        }

        [Preserve]
        public void onPermissionResult(bool granted)
        {
            _onResult?.Invoke(granted);
        }
    }
}
//#endif