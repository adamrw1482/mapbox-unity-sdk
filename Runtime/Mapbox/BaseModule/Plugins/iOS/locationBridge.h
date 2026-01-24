#import <MapboxCommon/MapboxLocation.h>
#import <MapboxCommon/MBXLocationService_Internal.h>
#import <MapboxCommon/MBXLocationServiceFactory_Internal.h>

extern "C" {
    // Callback type for location updates
    typedef void (*LocationUpdateCallback)(
        double latitude, double longitude,
        float accuracy, double timestamp,
        double altitude, float speed, float bearing);

    // Create location service and start updates
    void* startLocationUpdatesWithSettings(
        long long minimumInterval, long long maximumInterval, long long interval,
        int accuracyLevel, float displacement,
        LocationUpdateCallback callback);

    // Stop location updates
    void stopLocationUpdates(void* providerPtr);
}