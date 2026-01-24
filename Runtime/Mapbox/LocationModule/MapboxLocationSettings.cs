using System;
using UnityEngine;

namespace Mapbox.LocationModule
{
    [Serializable]
    public class MapboxLocationSettings
    {
        // The accuracy of the observed location
        [Tooltip("The accuracy of the observed location")]
        public MapboxLocationAccuracyLevel AccuracyLevel;
        
        // Minimum displacement between location updates in meters.
        [Tooltip("Minimum displacement between location updates in meters.")]
        public float Displacement;
        
        /// <summary>
        /// The fastest rate at which the application will receive location updates, which might be faster than the `Interval`.
        /// Unlike `Interval` this parameter is exact.
        /// </summary>
        [Tooltip("The fastest rate at which the application will receive location updates, which might be faster than the `Interval`. Unlike `Interval` this parameter is exact.")]
        public long MinimumInterval;
        /// <summary>
        /// Maximum wait time for location updates. If it's at least 2x larger then `Interval`, then location delivery may be delayed and multiple locations can be delivered at once.
        /// </summary>
        [Tooltip("Maximum wait time for location updates. If it's at least 2x larger then `Interval`, then location delivery may be delayed and multiple locations can be delivered at once.")]
        public long MaximumInterval;
        /// <summary>
        /// Desired interval for active location updates.
        /// </summary>
        [Tooltip("Desired interval for active location updates.")]
        public long Interval;
    }

    public enum MapboxLocationAccuracyLevel
    {
        Passive,
        // Low accuracy requirement (typically greater than 500 meters).
        Low,
        // Medium accuracy requirement (typically between 100 and 500 meters). 
        Medium,
        // High accuracy requirement.
        High,
        //The highest possible accuracy requirement that uses additional sensors (if possible) to facilitate navigation use case. 
        Highest
    }
}