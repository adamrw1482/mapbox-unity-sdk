using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using Mapbox.BaseModule.Data.Vector2d;
using Mapbox.BaseModule.Utilities;

#if UNITY_IOS
namespace Mapbox.LocationModule
{
    public interface IMapboxDeviceLocation
    {
        event Action<Location> LocationUpdated;
        void Update();
        void OnDestroy();
    }
    
    public class MapboxLocationIos : IMapboxDeviceLocation
    {
        public event Action<Location> LocationUpdated = delegate { };
        public MapboxLocationSettings _mapboxLocationSettings;

        private Location _currentLocation;
        // Static reference for IL2CPP callback compatibility
        private static MapboxLocationIos _instance;
        private GCHandle _gcHandle;

        // Queue for marshaling callbacks to main thread
        private Queue<Action> _mainThreadQueue = new Queue<Action>();
        private object _queueLock = new object();

        // Callback delegate for location updates
        private delegate void LocationUpdateCallback(
            double latitude, double longitude,
            float accuracy, double timestamp,
            double altitude, float speed, float bearing);

        // DllImport declarations
        [DllImport("__Internal")]
        private static extern void requestLocationAuthorization();

        [DllImport("__Internal")]
        private static extern IntPtr startLocationUpdatesWithSettings(
            long minimumInterval, long maximumInterval, long interval,
            int accuracyLevel, float displacement,
            LocationUpdateCallback callback);

        [DllImport("__Internal")]
        private static extern void stopLocationUpdates(IntPtr provider);

        // Implementation details
        private IntPtr _locationProvider;
        private static LocationUpdateCallback _callback;
        private bool _isStarted = false;

        public MapboxLocationIos(MapboxLocationSettings settings)
        {
            _mapboxLocationSettings = settings;
            // Request location authorization first
            requestLocationAuthorization();

            // Keep reference for static callback (IL2CPP requirement)
            _instance = this;
            _gcHandle = GCHandle.Alloc(this);

            // Create static callback delegate
            _callback = OnLocationUpdateStatic;

            // Map accuracy level enum to integer
            int accuracyLevel = (int)_mapboxLocationSettings.AccuracyLevel;

            // Start location updates with settings
            _locationProvider = startLocationUpdatesWithSettings(
                _mapboxLocationSettings.MinimumInterval,
                _mapboxLocationSettings.MaximumInterval,
                _mapboxLocationSettings.Interval,
                accuracyLevel,
                _mapboxLocationSettings.Displacement,
                _callback);

            if (_locationProvider != IntPtr.Zero)
            {
                _isStarted = true;

                // Initialize location struct
                _currentLocation.IsLocationServiceInitializing = false;
                _currentLocation.IsLocationServiceEnabled = true;
                _currentLocation.Provider = "MapboxCommon";
                _currentLocation.ProviderClass = "LocationIos";
            }
            else
            {
                Debug.LogError("Failed to start iOS location service");
                _currentLocation.IsLocationServiceInitializing = false;
                _currentLocation.IsLocationServiceEnabled = false;
            }
        }

        // Static callback for IL2CPP compatibility
        [AOT.MonoPInvokeCallback(typeof(LocationUpdateCallback))]
        private static void OnLocationUpdateStatic(
            double latitude, double longitude,
            float accuracy, double timestamp,
            double altitude, float speed, float bearing)
        {
            if (_instance != null)
            {
                _instance.OnLocationUpdate(latitude, longitude, accuracy, timestamp, altitude, speed, bearing);
            }
        }

        // Instance method to handle the actual update
        private void OnLocationUpdate(double latitude, double longitude, float accuracy, double timestamp, double altitude, float speed, float bearing)
        {
            // Queue the update to be processed on the main thread
            lock (_queueLock)
            {
                _mainThreadQueue.Enqueue(() =>
                {
                    try
                    {
                        // Update location struct
                        _currentLocation.LatitudeLongitude = new LatitudeLongitude(latitude, longitude);
                        _currentLocation.Accuracy = accuracy;
                        _currentLocation.Timestamp = timestamp;
                        _currentLocation.TimestampDevice = UnixTimestampUtils.To(DateTime.UtcNow);

                        // Set optional properties
                        _currentLocation.SpeedMetersPerSecond = speed > 0 ? speed : (float?)null;
                        _currentLocation.UserHeading = bearing;
                        _currentLocation.IsUserHeadingUpdated = bearing > 0;

                        _currentLocation.IsLocationUpdated = true;
                        _currentLocation.IsLocationServiceEnabled = true;
                        _currentLocation.IsLocationServiceInitializing = false;

                        // Send location update event
                        LocationUpdated(_currentLocation);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Exception in iOS location update: {ex.Message}");
                    }
                });
            }
        }

        public void Update()
        {
            // Process queued location updates on the main thread
            lock (_queueLock)
            {
                while (_mainThreadQueue.Count > 0)
                {
                    var action = _mainThreadQueue.Dequeue();
                    action?.Invoke();
                }
            }
        }

        public void OnDestroy()
        {
            if (_isStarted && _locationProvider != IntPtr.Zero)
            {
                stopLocationUpdates(_locationProvider);
                _isStarted = false;
            }

            if (_gcHandle.IsAllocated)
            {
                _gcHandle.Free();
            }

            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
#endif