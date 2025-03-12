using System;
using System.Collections;
using Mapbox.BaseModule.Data.Interfaces;
using Mapbox.BaseModule.Data.Tiles;
using Mapbox.BaseModule.Data.Vector2d;
using Mapbox.BaseModule.Unity;
using Mapbox.BaseModule.Utilities;
using UnityEngine;
using UnityEngine.Serialization;

namespace Mapbox.BaseModule.Map
{
    [Serializable]
    public sealed class MapboxMap
    {
        [NonSerialized] public IMapInformation MapInformation;
        [NonSerialized] public IMapVisualizer MapVisualizer;
        [NonSerialized] public UnityContext UnityContext;
        [NonSerialized] public TileCover TileCover;
        [NonSerialized] public InitializationStatus Status = InitializationStatus.WaitingForInitialization;

        public MapService MapService { get; private set; }

        public MapboxMap(IMapInformation information, UnityContext unityContext, MapService mapMapService)
        {
            MapInformation = information;
            UnityContext = unityContext;
            TileCover = new TileCover();
            MapService = mapMapService;
        }

        public IEnumerator Initialize()
        {
            if (Status != InitializationStatus.WaitingForInitialization)
                yield break;

            Status = InitializationStatus.Initializing;
            
            yield return MapVisualizer.Initialize();
            MapVisualizer.TileLoaded += (t) => { TileLoaded(t); };
            MapVisualizer.TileUnloading += (t) => { TileUnloading(t); };
            //MapVisualizer.TileLoading += TileLoading;
            
            Status = InitializationStatus.Initialized;
            Initialized();
        }

        public void MapUpdated()
        {
            MapService.TileCover(MapInformation, TileCover);
            MapVisualizer.Load(TileCover);
        }

        public void LoadMapView(Action callback)
        {
            Runnable.Instance.StartCoroutine(LoadMapViewCoroutine(callback));
        }

        public IEnumerator LoadMapViewCoroutine(Action callback)
        {
            var tileCover = new TileCover();
            MapService.TileCover(MapInformation, tileCover);
            yield return MapVisualizer.LoadTileCoverToMemory(tileCover);
            if (Status == InitializationStatus.Initialized)
            {
                Status = InitializationStatus.ViewLoaded;
                OnFirstViewCompleted();
            }

            callback();
            Status = InitializationStatus.ReadyForUpdates;
        }

        public void ChangeView(LatitudeLongitude? latlng = null, float? zoom = null, float? pitch = null, float? bearing = null)
        {
            MapInformation.SetInformation(latlng, zoom, pitch, bearing);
        }
        
        public Action Initialized = () => {};
        public Action OnFirstViewCompleted = () => { };
        
        public void OnDestroy()
        {
            MapVisualizer?.OnDestroy();
            MapService.OnDestroy();
        }

        public void UpdateTileCover() => MapService.TileCover(MapInformation, TileCover);
        
        public Action<UnityMapTile> TileLoaded = (tile) => { };
        //public Action<UnwrappedTileId> TileLoading = (tile) => { };
        public Action<UnityMapTile> TileUnloading = (tile) => { };
    }
}

