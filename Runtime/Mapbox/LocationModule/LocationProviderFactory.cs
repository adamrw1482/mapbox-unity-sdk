using System;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Scripting;

namespace Mapbox.LocationModule
{
	/// <summary>
	/// Singleton factory to allow easy access to various LocationProviders.
	/// This is meant to be attached to a game object.
	/// </summary>
	public class LocationProviderFactory : MonoBehaviour
	{
		public bool IsLocationProviderReady = false;
		public Action<LocationProviderFactory> OnLocationProviderReady = (f) => { };
		
		[SerializeField]
		[Tooltip("Custom native Android location provider. If this is not set above provider is used")]
		public CommonAndroidDeviceLocationProvider _deviceLocationProviderAndroid = null;
		
		[SerializeField]
		[Tooltip("Provider using Unity's builtin 'Input.Location' service")]
		AbstractLocationProvider _deviceLocationProviderUnity;

		[SerializeField]
		AbstractLocationProvider _editorLocationProvider;

		[SerializeField]
		AbstractLocationProvider _transformLocationProvider;

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
		/// <example>
		/// Fetch location to set a transform's position:
		/// <code>
		/// void Update()
		/// {
		///     var locationProvider = LocationProviderFactory.Instance.DefaultLocationProvider;
		///     transform.position = Conversions.GeoToWorldPosition(locationProvider.Location,
		///                                                         MapController.ReferenceTileRect.Center,
		///                                                         MapController.WorldScaleFactor).ToVector3xz();
		/// }
		/// </code>
		/// </example>
		public ILocationProvider DefaultLocationProvider { get; set; }

		/// <summary>
		/// Returns the serialized <see cref="T:Mapbox.Unity.Location.TransformLocationProvider"/>.
		/// </summary>
		public ILocationProvider TransformLocationProvider => _transformLocationProvider;

		/// <summary>
		/// Returns the serialized <see cref="T:Mapbox.LocationModule.EditorLocationProvider"/>.
		/// </summary>
		public ILocationProvider EditorLocationProvider => _editorLocationProvider;

		/// <summary>
		/// Returns the serialized <see cref="T:Mapbox.LocationModule.DeviceLocationProvider"/>
		/// </summary>
		public ILocationProvider DeviceLocationProvider => _deviceLocationProviderUnity;

		/// <summary>
		/// Create singleton instance and inject the DefaultLocationProvider upon initialization of this component. 
		/// </summary>
		protected virtual void Awake()
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
			InjectDeviceLocationProvider();
			Debug.LogFormat("MAPBOX_UNITY_SDK:  LocationProviderFactory: Injected DEVICE Location Provider - {0}", DefaultLocationProvider.GetType());
#endif
			
			IsLocationProviderReady = true;
			OnLocationProviderReady(this);
		}

		/// <summary>
		/// Injects the device location provider.
		/// Depending on the platform, this method and calls to it will be stripped during compile.
		/// </summary>
		[Preserve]
		void InjectDeviceLocationProvider()
		{
			int AndroidApiVersion = 0;
			var regex = new Regex(@"(?<=API-)-?\d+");
			Match match = regex.Match(SystemInfo.operatingSystem); // eg 'Android OS 8.1.0 / API-27 (OPM2.171019.029/4657601)'
			if (match.Success) { int.TryParse(match.Groups[0].Value, out AndroidApiVersion); }
			Debug.LogFormat("MAPBOX_UNITY_SDK: {0} => API version: {1}", SystemInfo.operatingSystem, AndroidApiVersion);

#if UNITY_ANDROID && !UNITY_EDITOR
			if (_deviceLocationProviderAndroid != null
				&& _deviceLocationProviderAndroid.enabled
				&& _deviceLocationProviderAndroid.transform.gameObject.activeInHierarchy
				// API version 24 => Android 7 (Nougat): we are using GnssStatus 'https://developer.android.com/reference/android/location/GnssStatus.html'
				// in the native plugin.
				// GnssStatus is not available with versions lower than 24
				&& AndroidApiVersion >= 24
			)
			{
				DefaultLocationProvider = _deviceLocationProviderAndroid;
			}
			else
			{
				DefaultLocationProvider = _deviceLocationProviderUnity;
			}
#else
			DefaultLocationProvider = _deviceLocationProviderUnity;
#endif
		}
	}
}
