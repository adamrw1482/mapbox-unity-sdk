using System;

namespace Mapbox.LocationModule
{
	/// <summary>
	/// Implement ILocationProvider to send Heading and Location updates.
	/// </summary>
	public interface ILocationProvider
	{
		event Action<Location> OnLocationUpdated;
		Location CurrentLocation { get; }

		void Update();
		void OnDestroy();
	}
}