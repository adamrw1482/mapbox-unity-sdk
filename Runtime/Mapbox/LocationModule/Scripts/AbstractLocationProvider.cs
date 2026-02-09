using System;
using UnityEngine;

namespace Mapbox.LocationModule
{
	public abstract class AbstractLocationProvider : ILocationProvider
	{
		protected Location _currentLocation;

		/// <summary>
		/// Gets the last known location.
		/// </summary>
		/// <value>The current location.</value>
		public Location CurrentLocation
		{
			get
			{
				return _currentLocation;
			}
		}

		public event Action<Location> OnLocationUpdated = delegate { };

		protected virtual void SendLocation(Location location)
		{
			OnLocationUpdated(location);
		}

		public virtual void Update()
		{
			
		}
		
		public virtual void OnDestroy()
		{
		}
	}
}