using System;
using System.Collections;
using System.Text.RegularExpressions;
using Mapbox.BaseModule.Data.Vector2d;
using Mapbox.BaseModule.Utilities;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.Serialization;

namespace Mapbox.LocationModule
{
	
	
	/// <summary>
	/// Singleton factory to allow easy access to various LocationProviders.
	/// This is meant to be attached to a game object.
	/// </summary>
	public sealed class LocationProviderFactory : MonoBehaviour
	{
		public LocationProviderType LocationProviderType;
		[NonSerialized] public bool IsLocationProviderReady = false;
		public Action<LocationProviderFactory> OnLocationProviderReady = (f) => { };
		
		[SerializeField]
		[Tooltip("Mapbox location provider for android and ios")]
		private MapboxLocationSettings _mapboxLocationProviderSettings = null;

		[SerializeField]
		[Tooltip("Provider using Unity's builtin 'Input.Location' service")]
		private UnityLocationProviderSettings _unityLocationProviderSettings;

		private AbstractLocationProvider _customLocationProvider;
		
		[SerializeField] private string EditorLatitudeLongitude;

		[SerializeField]
		bool _dontDestroyOnLoad;


		/// <summary>
		/// The singleton instance of this factory.
		/// </summary>
		public static LocationProviderFactory Instance { get; private set; }

		/// <summary>
		/// The default location provider. 
		/// Outside of the editor, this will be a <see cref="T:LocationModule.UnityLocationProvider"/>.
		/// In the Unity editor, this will be an <see cref="T:StaticLocationProvider"/>
		/// </summary>
		public ILocationProvider DefaultLocationProvider { get; set; }
		
		/// <summary>
		/// Create singleton instance and inject the DefaultLocationProvider upon initialization of this component. 
		/// </summary>
		public IEnumerator Initialize()
		{
			if (Instance != null)
			{
				DestroyImmediate(gameObject);
				yield break;
			}
			Instance = this;

			if (_dontDestroyOnLoad)
			{
				DontDestroyOnLoad(gameObject);
			}

			if(LocationProviderType == LocationProviderType.StaticLocationProvider)
			{
				DefaultLocationProvider = new StaticLocationProvider(EditorLatitudeLongitude);
			}
			else if(LocationProviderType == LocationProviderType.MapboxLocationProvider)
			{
				var mapboxLocationProvider = new MapboxLocationProvider(_mapboxLocationProviderSettings);
				mapboxLocationProvider.AvailabilityChanged += b => Debug.Log("MAPBOX_UNITY_SDK: LocationProviderFactory.AvailabilityChanged " + b);
				mapboxLocationProvider.AuthorizationChanged += b => Debug.Log("MAPBOX_UNITY_SDK: AuthorizationChanged " + b);
				mapboxLocationProvider.AccuracyAuthorizationChanged += b => Debug.Log("MAPBOX_UNITY_SDK: AuthorizationChanged " + b);
				DefaultLocationProvider = mapboxLocationProvider;
			}
			else if(LocationProviderType == LocationProviderType.UnityLocationProvider)
			{
				DefaultLocationProvider = new UnityLocationProvider(_unityLocationProviderSettings);
			}
			else if(LocationProviderType == LocationProviderType.CustomLocationProvider)
			{
				DefaultLocationProvider = new UnityLocationProvider(_unityLocationProviderSettings);
			}
			Debug.LogFormat("MAPBOX_UNITY_SDK:  LocationProviderFactory: Injected Location Provider - {0}", DefaultLocationProvider.GetType());

			if (LocationProviderType != LocationProviderType.StaticLocationProvider)
			{
				if (Input.location.status != LocationServiceStatus.Running)
				{
					Input.location.Start();
					
					while (Input.location.status < LocationServiceStatus.Running)
						yield return null;
					Debug.Log("location status " + Input.location.status);
				}
				
				while (DefaultLocationProvider.CurrentLocation.LatitudeLongitude.Latitude == 0 &&
				       DefaultLocationProvider.CurrentLocation.LatitudeLongitude.Longitude == 0)
				{
					yield return null;
				}
				Debug.Log("latlng");
			}

			IsLocationProviderReady = true;
			OnLocationProviderReady(this);
			Debug.Log("ready");
		}

		private void Update()
		{
			DefaultLocationProvider?.Update();
		}

		private void OnDestroy()
		{
			DefaultLocationProvider?.OnDestroy();
		}
	}
	
	public enum LocationProviderType
	{
		[InspectorName("Static Location Provider")]
		StaticLocationProvider,

		[InspectorName("Unity Location Provider")]
		UnityLocationProvider,

		[InspectorName("Mapbox Location Provider (Experimental)")]
		MapboxLocationProvider,

		[InspectorName("Custom Location Provider")]
		CustomLocationProvider
	}
}


