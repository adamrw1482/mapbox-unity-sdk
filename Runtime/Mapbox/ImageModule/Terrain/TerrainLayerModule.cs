using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mapbox.BaseModule.Data.DataFetchers;
using Mapbox.BaseModule.Data.Interfaces;
using Mapbox.BaseModule.Data.Tiles;
using Mapbox.BaseModule.Data.Vector2d;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Unity;
using Mapbox.BaseModule.Utilities;
using Mapbox.ImageModule.Terrain.TerrainStrategies;
using UnityEngine;
using TerrainData = Mapbox.BaseModule.Data.DataFetchers.TerrainData;

namespace Mapbox.ImageModule.Terrain
{
    public class TerrainLayerModule : ITerrainLayerModule
    {
        private TerrainLayerModuleSettings _settings;
        private Source<TerrainData> _rasterSource;
        private HashSet<CanonicalTileId> _retainedTerrainTiles;
        private TerrainStrategy _terrainStrategy;
        
        public TerrainLayerModule(Source<TerrainData> source, TerrainLayerModuleSettings settings) : base()
        {
            _settings = settings;
            _retainedTerrainTiles = new HashSet<CanonicalTileId>();
            _rasterSource = source;
            _terrainStrategy = new ElevatedTerrainStrategy();
        }
        
        public virtual IEnumerator Initialize()
        {
            yield return _rasterSource.Initialize();
            _terrainStrategy.Initialize(_settings.ElevationLayerProperties);
            if(_settings.LoadBackgroundTextures)
            {
                _rasterSource?.DownloadAndCacheBaseTiles();
            }
        }

        public virtual void LoadTempTile(UnityMapTile unityTile)
        {
            if (IsZinSupportedRange(unityTile.CanonicalTileId.Z) == false)
            {
                unityTile.TerrainContainer.DisableTerrain();
                return;
            }
            
            var targetTileId = GetDataId(unityTile.CanonicalTileId);
            var parentTileId = targetTileId;
            for (int i = parentTileId.Z; i >= 2; i--)
            {
                parentTileId.MoveToParent();
                if (_rasterSource.GetInstantData(parentTileId, out var instantData)  && instantData.IsElevationDataReady)
                {
                    unityTile.TerrainContainer.SetTerrainData(instantData, _settings.UseShaderTerrain, TileContainerState.Temporary);
                    _terrainStrategy.RegisterTile(unityTile, !_settings.UseShaderTerrain);
                    return;
                }
            }
        }
        
        public virtual bool LoadInstant(UnityMapTile unityTile)
        {
            if (IsZinSupportedRange(unityTile.CanonicalTileId.Z) == false)
            {
                unityTile.TerrainContainer.DisableTerrain();
                return true;
            }
            
            var targetTileId = GetDataId(unityTile.CanonicalTileId);
            if (_rasterSource.GetInstantData(targetTileId, out var instantData) && instantData.IsElevationDataReady)
            {
                unityTile.TerrainContainer.SetTerrainData(instantData, _settings.UseShaderTerrain);
                _terrainStrategy.RegisterTile(unityTile, !_settings.UseShaderTerrain);
                return true;
            }
            
            return false;
        }

        public virtual bool RetainTiles(HashSet<CanonicalTileId> retainedTiles)
        {
            var isReady = true;
            _retainedTerrainTiles.Clear();
            foreach (var tileId in retainedTiles)
            {
                _retainedTerrainTiles.Add(GetDataId(tileId));
            }
            
            isReady = _rasterSource.RetainTiles(_retainedTerrainTiles);
            return isReady;
        }
        
        public void UpdatePositioning(IMapInformation mapInfo)
        {
            
        }
                
        public void OnDestroy()
        {
            _rasterSource.OnDestroy();
        }
        
        //COROUTINE METHODS only used in initialization so far
        #region coroutine methods
        public virtual IEnumerator LoadTileData(CanonicalTileId tileId, Action<TerrainData> callback = null)
        {
            return _rasterSource.LoadTileCoroutine(tileId, callback);
        }

        public virtual IEnumerator LoadTiles(IEnumerable<CanonicalTileId> tiles)
        {
            yield return _rasterSource.LoadTilesCoroutine(GetDataId(tiles));
        }
        
        public IEnumerable<IEnumerator> GetTileCoverCoroutines(IEnumerable<CanonicalTileId> tiles)
        {
            var targetTiles = GetDataId(tiles).Distinct();
            return targetTiles.Select(x => LoadTileData(x)).Where(x => x != null);
        }
        #endregion
        
        
        //PRIVATE METHODS
        private bool IsZinSupportedRange(int targetZ)
        {
            return _settings.RejectTilesOutsideZoom.x <= targetZ && _settings.RejectTilesOutsideZoom.y >= targetZ;
        }
        
        private CanonicalTileId GetDataId(CanonicalTileId tileId)
        {
            var maxZoom = _settings.DataSettings.ClampDataLevelToMax;
            var currentZ = tileId.Z;
            var targetZ = (int)Mathf.Max(currentZ - 2, _settings.RejectTilesOutsideZoom.x);
            if (targetZ >= maxZoom)
            {
                return tileId.ParentAt(maxZoom);
            }
            else
            {
                return tileId.ParentAt(targetZ);;
            }
        }
        
        
        //API
        
        /// <summary>
        /// This method will only search the memory cache for available data.
        /// </summary>
        /// <param name="tileId">TileId of the elevation data</param>
        /// <param name="x">Point in texture coordinate space, origin is bottom left.</param>
        /// <param name="y">Point in texture coordinate space, origin is bottom left.</param>
        /// <returns></returns>
        public float QueryElevation(CanonicalTileId tileId, float x, float y)
        {
            var originalTileId = tileId;
            var targetTileId = tileId;
            for (int i = 0; i < 5; i++)
            {
                if (_rasterSource.GetInstantData(targetTileId, out var instantData))
                {
                    return instantData.QueryHeightData(originalTileId, x, y);
                }
                targetTileId.MoveToParent();
            }
            
            return 0;
        }
        
        public IEnumerable<CanonicalTileId> GetDataId(IEnumerable<CanonicalTileId> tileIdList)
        {
            return tileIdList.Where(x => IsZinSupportedRange(x.Z)).Select(GetDataId).Distinct();
        }

        /// <summary>
        /// This method will cause tile requests if data isn't already available on memory cache. Use with caution.
        /// </summary>
        /// <param name="latLng">Latitude longitude of the location you want to query</param>
        /// <param name="callback">Callback method returning the elevation in float</param>
        /// <returns></returns>
        public IEnumerator GetElevationData(LatitudeLongitude latLng, Action<float> callback = null)
        {
            var tileId = Conversions.LatitudeLongitudeToTileId(latLng, 14).Canonical;
            var dataFound = false;
            for (int i = 14; i >= 2; i--)
            {
                if (_rasterSource.GetInstantData(tileId, out var instantData))
                {
                    var tilePos = Conversions.LatitudeLongitudeToInTile01(latLng, instantData.TileId);
                    var elevation = instantData.QueryHeightData(tilePos);
                    callback?.Invoke(elevation);
                    dataFound = true;
                    break;
                }
                else
                {
                    tileId = tileId.GetParentTileId;
                }
            }

            if (!dataFound)
            {
                tileId = Conversions.LatitudeLongitudeToTileId(latLng, 14).Canonical;
                yield return LoadTileData(tileId, terrainData =>
                {
                    var tilePos = Conversions.LatitudeLongitudeToInTile01(latLng, terrainData.TileId);
                    var elevation = terrainData.QueryHeightData(tilePos);
                    callback?.Invoke(elevation);
                });
            }
        }
    }
}