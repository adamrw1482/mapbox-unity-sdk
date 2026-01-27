using System;
using System.Text.RegularExpressions;
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
		[NonSerialized] public bool IsLocationProviderReady = false;
		public Action<LocationProviderFactory> OnLocationProviderReady = (f) => { };
		
		[SerializeField]
		[Tooltip("Mapbox location provider for android and ios")]
		public MapboxLocationProvider _mapboxLocationProvider = null;

		[FormerlySerializedAs("_deviceLocationProviderUnity")]
		[SerializeField]
		[Tooltip("Provider using Unity's builtin 'Input.Location' service")]
		AbstractLocationProvider _unityLocationProvider;

		[SerializeField]
		AbstractLocationProvider _editorLocationProvider;

		[SerializeField]
		bool _dontDestroyOnLoad;


		/// <summary>
		/// The singleton instance of this factory.
		/// </summary>
		public static LocationProviderFactory Instance { get; private set; }

		/// <summary>
		/// The default location provider. 
		/// Outside of the editor, this will be a <see cref="T:Mapbox.LocationModule.DeviceLocationProvider"/>.
		/// In the Unity editor, this will be an <see cref="T:Mapbox.LocationModule.EditorLocationProvider"/>
		/// </summary>
		public ILocationProvider DefaultLocationProvider { get; set; }


		/// <summary>
		/// Returns the serialized <see cref="T:Mapbox.LocationModule.EditorLocationProvider"/>.
		/// </summary>
		public ILocationProvider EditorLocationProvider => _editorLocationProvider;

		/// <summary>
		/// Returns the serialized <see cref="T:Mapbox.LocationModule.DeviceLocationProvider"/>
		/// </summary>
		public ILocationProvider UnityLocationProvider => _unityLocationProvider;

		/// <summary>
		/// Create singleton instance and inject the DefaultLocationProvider upon initialization of this component. 
		/// </summary>
		private void Awake()
		{
			if (Instance != null)
			{
				DestroyImmediate(gameObject);
				return;
			}
			Instance = this;

			if (_dontDestroyOnLoad)
			{
				DontDestroyOnLoad(gameObject);
			}

#if UNITY_EDITOR
			
			DefaultLocationProvider = _editorLocationProvider;
			Debug.LogFormat("MAPBOX_UNITY_SDK: LocationProviderFactory: Injected EDITOR Location Provider - {0}", DefaultLocationProvider.GetType());
#else
			if(_mapboxLocationProvider != null)
			{
				DefaultLocationProvider = _mapboxLocationProvider;
				_mapboxLocationProvider.AvailabilityChanged += b => Debug.Log("MAPBOX_UNITY_SDK: LocationProviderFactory.AvailabilityChanged " + b);
				_mapboxLocationProvider.AuthorizationChanged += b => Debug.Log("MAPBOX_UNITY_SDK: AuthorizationChanged " + b);
				_mapboxLocationProvider.AccuracyAuthorizationChanged += b => Debug.Log("MAPBOX_UNITY_SDK: AuthorizationChanged " + b);
				_mapboxLocationProvider.Initialize();
			}
			else
			{
				DefaultLocationProvider = _unityLocationProvider;
			}
			Debug.LogFormat("MAPBOX_UNITY_SDK:  LocationProviderFactory: Injected DEVICE Location Provider - {0}", DefaultLocationProvider.GetType());
#endif
			
			IsLocationProviderReady = true;
			OnLocationProviderReady(this);
		}
	}
}
