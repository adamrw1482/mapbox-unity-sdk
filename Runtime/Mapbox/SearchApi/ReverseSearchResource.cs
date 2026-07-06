//-----------------------------------------------------------------------
// <copyright file="ReverseSearchResource.cs" company="Mapbox">
//     Copyright (c) 2024 Mapbox. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;
using Mapbox.BaseModule.Utilities;

namespace Mapbox.SearchApi
{
    /// <summary>
    /// Resource for the Search Box API <c>/reverse</c> endpoint.
    /// Returns addresses and POIs around a given coordinate pair.
    /// </summary>
    public sealed class ReverseSearchResource : SearchBoxResource
    {
        private readonly double _longitude;
        private readonly double _latitude;

        /// <summary>
        /// Initializes a new reverse lookup request.
        /// </summary>
        /// <param name="longitude">The longitudinal coordinate to reverse geocode.</param>
        /// <param name="latitude">The latitudinal coordinate to reverse geocode.</param>
        public ReverseSearchResource(double longitude, double latitude)
        {
            if (longitude < -180 || longitude > 180)
                throw new ArgumentOutOfRangeException(nameof(longitude), "Longitude must be between -180 and 180.");
            if (latitude < -90 || latitude > 90)
                throw new ArgumentOutOfRangeException(nameof(latitude), "Latitude must be between -90 and 90.");

            _longitude = longitude;
            _latitude = latitude;
        }

        /// <summary>Builds the complete /reverse request URL.</summary>
        public override string GetUrl()
        {
            var opts = new Dictionary<string, string>
            {
                { "longitude", _longitude.ToString("G", CultureInfo.InvariantCulture) },
                { "latitude", _latitude.ToString("G", CultureInfo.InvariantCulture) }
            };
            AddSharedOptions(opts);
            return Constants.Map.BaseAPI + ApiEndpoint + "reverse" + EncodeQueryString(opts);
        }
    }
}
