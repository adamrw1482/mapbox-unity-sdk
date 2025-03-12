using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mapbox.BaseModule.Data.Interfaces;
using Mapbox.BaseModule.Data.Tiles;
using Mapbox.BaseModule.Unity;
using Mapbox.BaseModule.Utilities;
using UnityEngine;

namespace Mapbox.BaseModule.Map
{
    [Serializable]
    public class MapboxMapVisualizer : IMapVisualizer
    {
        public List<ILayerModule> LayerModules;
        public Dictionary<UnwrappedTileId, UnityMapTile> ActiveTiles { get; private set; }
        protected UnityContext _unityContext;
        protected IMapInformation _mapInformation;
        protected ITileCreator _tileCreator;

        private HashSet<UnwrappedTileId> _toRemove;
        private HashSet<CanonicalTileId> _retainedTiles;

        private int _tilePerFrameLimit = 20;
        private int _tileCreatedThisFrame = 0;

        public MapboxMapVisualizer(IMapInformation mapInformation, UnityContext unityContext, ITileCreator tileCreator)
        {
            _unityContext = unityContext;
            _mapInformation = mapInformation;
            _tileCreator = tileCreator;
            ActiveTiles = new Dictionary<UnwrappedTileId, UnityMapTile>(100);
            LayerModules = new List<ILayerModule>();

            _mapInformation.WorldScaleChanged += RepositionAllTiles;
            
            _toRemove = new HashSet<UnwrappedTileId>();
            _retainedTiles = new HashSet<CanonicalTileId>();
        }

        public virtual IEnumerator Initialize()
        {
            yield return _tileCreator.Initialize();
            yield return LayerModules.Select(x => x.Initialize()).WaitForAll();
        }
        
        public virtual IEnumerator LoadTileCoverToMemory(TileCover tileCover)
        {
            var hashsetTiles = new HashSet<CanonicalTileId>(tileCover.Tiles.Select(x => x.Canonical));
            var coroutines = LayerModules.Select(x => x.LoadTiles(hashsetTiles));
            yield return coroutines.WaitForAll();
        }
      
        public virtual void Load(TileCover tileCover)
        {
            _tileCreatedThisFrame = 0;
            _toRemove.Clear();
            _retainedTiles.Clear();
            
            foreach (var tile in ActiveTiles.Values)
            {
                _toRemove.Add(tile.UnwrappedTileId);
            }

            foreach (var tileId in tileCover.Tiles)
            {
                _retainedTiles.Add(tileId.Canonical);
                UnityMapTile unityMapTile = null;
                _toRemove.Remove(tileId);

                if (ActiveTiles.TryGetValue(tileId, out unityMapTile))
                {
                    if (unityMapTile.IsTemporary)
                    {
                        FinalizeTempTile(unityMapTile);
                    }
                    
                    ShowTile(unityMapTile);
                    continue;
                }
                else
                {
                    //TileLoading(tileId);
                }

                if (_tileCreatedThisFrame < _tilePerFrameLimit)
                {
                    if (CreateTileInstant(tileId, out unityMapTile))
                    {
                        ShowTile(unityMapTile);
                        _tileCreatedThisFrame++;
                        continue;
                    }
                    else
                    {
                        var coveredByQuadrants = DelveInto(tileId, recursiveDepth: 1);
                        if (!coveredByQuadrants)
                        {
                            CreateTempTile(tileId, out unityMapTile);
                            ShowTile(unityMapTile);
                        }
                    }
                }
            }
            
            foreach (var tileId in _toRemove)
            {
                if (ActiveTiles.ContainsKey(tileId))
                {
                    TileUnloading(ActiveTiles[tileId]);
                    PoolTile(ActiveTiles[tileId]);
                }
                else
                {
                    Debug.LogError($"Could not find tile {tileId}");
                }
            }
            
            foreach (var visualization in LayerModules)
            {
                visualization.RetainTiles(_retainedTiles, ActiveTiles);
            }
        }

        

        public void OnDestroy()
        {
            foreach (var layerModule in LayerModules)
            {
                layerModule.OnDestroy();
            }
        }

        public bool TryGetLayerModule<T>(Type type, out T module) where T : ILayerModule
        {
            module = (T)LayerModules.FirstOrDefault(x => x.GetType() == type);
            return module != null;
        }
        
        
        
        
        protected bool DelveInto(UnwrappedTileId tileId, int recursiveDepth = 3)
        {
            var quadrantCheck = new bool[4] { false, false, false, false };
            var quadrants = new UnwrappedTileId[4]
            {
                tileId.Quadrant(0),
                tileId.Quadrant(1),
                tileId.Quadrant(2),
                tileId.Quadrant(3),
            };
            for (int i = 0; i < 4; i++)
            {
                var quadrant = quadrants[i];
                if (ActiveTiles.TryGetValue(quadrant, out var unityMapTile))
                {
                    _toRemove.Remove(quadrant);
                    //_retainedTiles.Add(quadrant.Canonical);
                    ShowTile(unityMapTile);
                    quadrantCheck[i] = true;
                }
            }

            if (recursiveDepth > 0)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (quadrantCheck[i] == false && tileId.Z < 22)
                    {
                        quadrantCheck[i] = DelveInto(quadrants[i], recursiveDepth - 1);
                    }
                }
            }

            if (quadrantCheck.Any(x => x))
            {
                for (int i = 0; i < 4; i++)
                {
                    if (quadrantCheck[i] == false)
                    {
                        CreateTempTile(quadrants[i], out var unityMapTile);
                        _mapInformation.PositionObjectFor(unityMapTile.gameObject, unityMapTile.CanonicalTileId);
                        ShowTile(unityMapTile);
                        quadrantCheck[i] = true;
                    }
                }

                return true;
            }

            return false;
        }

        protected void ShowTile(UnityMapTile unityTile)
        {
            unityTile.gameObject.SetActive(true);
            _mapInformation.PositionObjectFor(unityTile.gameObject, unityTile.CanonicalTileId);
        }
        
        protected void PoolTile(UnityMapTile tile)
        {
            ActiveTiles.Remove(tile.UnwrappedTileId);
            tile.Recycle();
            tile.IsTemporary = false;
            _tileCreator.PutTile(tile);
        }

        protected void CreateTempTile(UnwrappedTileId tileId, out UnityMapTile tile)
        {
            var rectd = Conversions.TileBoundsInUnitySpace(tileId, _mapInformation.CenterMercator, _mapInformation.Scale);
            tile = null;
            tile = _tileCreator.GetTile();
            tile.transform.position = new Vector3((float) rectd.Center.x, 0, (float) rectd.Center.y);
            tile.transform.localScale = Vector3.one * (float) rectd.Size.x;
            tile.Initialize(tileId, (float) rectd.Size.x * _mapInformation.Scale);
            
            foreach (var module in LayerModules)
            {
                module.LoadTempTile(tile);
            }
            
            tile.IsTemporary = true;
            ActiveTiles.Add(tileId, tile);
        }
        
        protected void FinalizeTempTile(UnityMapTile unityMapTile)
        {
            var tileFinished = true;
            foreach (var module in LayerModules)
            {
                var moduleFinished = module.LoadInstant(unityMapTile);
                tileFinished &= moduleFinished;
                if (!moduleFinished) break;
            }

            if (tileFinished)
            {
                unityMapTile.IsTemporary = false;
                TileLoaded(unityMapTile);
            }
        }
        
        protected bool CreateTileInstant(UnwrappedTileId tileId, out UnityMapTile tile)
        {
            var rectd = Conversions.TileBoundsInUnitySpace(tileId, _mapInformation.CenterMercator, _mapInformation.Scale);
            tile = null;
            tile = _tileCreator.GetTile();
            tile.transform.position = new Vector3((float) rectd.Center.x, 0, (float) rectd.Center.y);
            tile.transform.localScale = Vector3.one * (float) rectd.Size.x;
            tile.Initialize(tileId, (float) rectd.Size.x * _mapInformation.Scale); 
            
            var loaded = true;
            foreach (var module in LayerModules)
            {
                var moduleFinished = module.LoadInstant(tile);
                loaded &= moduleFinished;
            }

            if (!loaded)
            {
                PoolTile(tile);
                return false;
            }
            
            tile.IsTemporary = false;
            ActiveTiles.Add(tileId, tile);
            TileLoaded(tile);

            return true;
        }

        protected void RepositionAllTiles(IMapInformation mapInformation)
        {
            foreach (var tilePair in ActiveTiles)
            {
                ShowTile(tilePair.Value);
            }

            foreach (var module in LayerModules)
            {
                module.UpdatePositioning(mapInformation);
            }
        }
        
        public event Action<UnityMapTile> TileLoaded = (tile) => { };
        public event Action<UnwrappedTileId> TileLoading = (tile) => { };
        public event Action<UnityMapTile> TileUnloading = (tile) => { };
        
    }
}