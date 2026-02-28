using System;
using System.Collections;
using System.IO;
using Mapbox.BaseModule;
using Mapbox.BaseModule.Telemetry;
using Mapbox.BaseModule.Utilities;
using UnityEngine;

namespace Mapbox.BaseModule.Map
{
    public class MapboxContext : IMapboxContext
    {
        public MapboxConfiguration Configuration;
        private ITelemetryLibrary _telemetryLibrary;

        private MapboxToken _mapboxToken;
        private string _tokenNotSetErrorMessage = "No configuration file found! Configure your access token from the Mapbox > Setup menu.";

        public MapboxContext()
        {
            // Fast-path configuration loading for editor/offline use.
            // WARNING: Token validation is fire-and-forget here - Configuration is set immediately
            // without waiting for validation. For proper token validation, call Initialize() instead.
            LoadConfiguration();
        }

        public IEnumerator Initialize()
        {
            // Proper initialization path: waits for token validation before setting Configuration.
            // Use this in production to ensure token is valid before proceeding.
            yield return LoadConfigurationCoroutine();
        }

        public string GetAccessToken()
        {
            return Configuration.AccessToken;
        }

        public string GetSkuToken()
        {
            return Configuration.GetMapsSkuToken();
        }

        public MapboxTokenStatus TokenStatus()
        {
            if (_mapboxToken == null)
                return MapboxTokenStatus.StatusNotYetSet;
            
            return _mapboxToken.Status;
        }
        
        /// <summary>
        /// Synchronous configuration loading (editor/offline fast-path).
        /// Sets Configuration immediately without waiting for token validation.
        /// Token validation happens asynchronously in background.
        /// </summary>
        private void LoadConfiguration()
        {
            TextAsset configurationTextAsset = Resources.Load<TextAsset>(Constants.Path.MAPBOX_RESOURCES_RELATIVE);
            if (null == configurationTextAsset)
            {
                Debug.LogError("Need Mapbox Access Token");
                throw new Exception();
            }

            var config = JsonUtility.FromJson<MapboxConfiguration>(configurationTextAsset.text);
            config.Initialize();
            var tokenValidator = new MapboxTokenApi();
            tokenValidator.Retrieve(config.GetMapsSkuToken, config.AccessToken, (response) =>
            {
                _mapboxToken = response;
                if (_mapboxToken.Status != MapboxTokenStatus.TokenValid)
                {
                    config.AccessToken = string.Empty;
                    Debug.LogError("Invalid Token");
                }
                else
                {
                    ConfigureTelemetry();
                }
            });

            // WARNING: Configuration set before token validation completes
            Configuration = config;
        }
        
        /// <summary>
        /// Coroutine-based configuration loading (production path).
        /// Waits for token validation to complete before setting Configuration.
        /// Only sets Configuration if token is valid.
        /// </summary>
        private IEnumerator LoadConfigurationCoroutine()
        {
            TextAsset configurationTextAsset = Resources.Load<TextAsset>(Constants.Path.MAPBOX_RESOURCES_RELATIVE);
            if (null == configurationTextAsset)
            {
                Debug.LogError("Need Mapbox Access Token");
                throw new Exception();
            }

            var config = JsonUtility.FromJson<MapboxConfiguration>(configurationTextAsset.text);
            config.Initialize();
            var tokenValidator = new MapboxTokenApi();
            var configLoaded = false;
            tokenValidator.Retrieve(config.GetMapsSkuToken, config.AccessToken, (response) =>
            {
                _mapboxToken = response;
                if (_mapboxToken.Status != MapboxTokenStatus.TokenValid)
                {
                    config.AccessToken = string.Empty;
                    Debug.LogError("Invalid Token");
                }
                else
                {
                    // Only set Configuration if token is valid
                    Configuration = config;
                    ConfigureTelemetry();
                }
                configLoaded = true;
            });

            // Wait for token validation to complete
            while (!configLoaded)
            {
                yield return null;
            }
        }

        private void ConfigureTelemetry()
        {
            //TODO: enable after token validation has been made async
            if (
            	null == Configuration ||
                string.IsNullOrEmpty(Configuration.AccessToken) ||
                _mapboxToken.Status != MapboxTokenStatus.TokenValid
            )
            {
            	Debug.LogError(_tokenNotSetErrorMessage);
            	return;
            }
            try
            {
                _telemetryLibrary = TelemetryFactory.GetTelemetryInstance();
                _telemetryLibrary.Initialize(Configuration.AccessToken);
                _telemetryLibrary.SetLocationCollectionState(Configuration.TelemetryEnabled);
                _telemetryLibrary.SendTurnstile();
                _telemetryLibrary.SendSdkEvent();
            }
            catch (Exception ex)
            {
                Debug.LogErrorFormat("Error initializing telemetry: {0}", ex);
            }
        }

        public void ValidateToken(Action callback = null)
        {
            var tokenValidator = new MapboxTokenApi();
            tokenValidator.Retrieve(GetSkuToken, GetAccessToken(), (response) =>
            {
                _mapboxToken = response;
                if (_mapboxToken.Status != MapboxTokenStatus.TokenValid)
                {
                    Debug.LogError("Invalid Token");
                }
                callback?.Invoke();
            });
        }

        public bool GetTelemetryCollectiongState()
        {
            return Configuration.TelemetryEnabled;
        }
        
        public bool SetTelemetryCollectionState(bool state)
        {
            Configuration.TelemetryEnabled = state;
            _telemetryLibrary.SetLocationCollectionState(Configuration.TelemetryEnabled);

            try
            {
                var configurationFilePath = Constants.Path.MAPBOX_CONFIG_ABSOLUTE;
                var json = JsonUtility.ToJson(Configuration);
                File.WriteAllText(configurationFilePath, json);
                Debug.Log("Successfully set telemetry collection state to " + state);
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }
        }
    }

    public interface IMapboxContext
    {
        public string GetAccessToken();
        public string GetSkuToken();
    }
}