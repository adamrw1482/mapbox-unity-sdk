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
    /// <summary>
    /// The primary object responsible for preparing the data and generating the visuals of the map.
    /// </summary>
    [Serializable]
    public class MapboxMapVisualizer : IMapVisualizer
    {
        public List<ILayerModule> LayerModules;
        public Dictionary<UnwrappedTileId, UnityMapTile> ActiveTiles { get; private set; }
        public List<UnityMapTile> TempTiles { get; private set; }
        protected UnityContext _unityContext;
        protected IMapInformation _mapInformation;
        protected ITileCreator _tileCreator;

        private HashSet<UnwrappedTileId> _toRemove;
        private HashSet<CanonicalTileId> _retainedTiles;
        private Coroutine _internalUpdateCoroutine;
        private bool _destroyed;

        // Reusable scratch array to avoid per-frame allocation in InternalUpdateCoroutine
        private readonly UnwrappedTileId[] _quadrants = new UnwrappedTileId[4];

        // Tiles deferred for pooling on the next coroutine tick.
        // Pooling a child the same frame the parent first becomes visible flickers on iOS.
        private readonly List<UnityMapTile> _pendingPool = new List<UnityMapTile>();

        public MapboxMapVisualizer(IMapInformation mapInformation, UnityContext unityContext, ITileCreator tileCreator)
        {
            _unityContext = unityContext;
            _mapInformation = mapInformation;

            ActiveTiles = new Dictionary<UnwrappedTileId, UnityMapTile>(100);
            TempTiles = new List<UnityMapTile>();
            LayerModules = new List<ILayerModule>();

            _tileCreator = tileCreator;
            _tileCreator.OnTileBroken += (tt) =>
            {
                if (ActiveTiles.TryGetValue(tt, out var mapTile))
                {
                    PoolTile(mapTile);
                }
            };

            _mapInformation.WorldScaleChanged += RepositionAllTiles;
            _mapInformation.LatitudeLongitudeChanged += RepositionAllTiles;

            _toRemove = new HashSet<UnwrappedTileId>();
            _retainedTiles = new HashSet<CanonicalTileId>();

            _internalUpdateCoroutine = Runnable.Instance.StartCoroutine(InternalUpdate());
        }

        public virtual IEnumerator Initialize()
        {
            var existsTerrainModule = LayerModules.Any(x => x is ITerrainLayerModule);

            if (existsTerrainModule)
            {
                yield return _tileCreator.Initialize();
            }
            else
            {
                var terrainStrategy = new FlatTerrainStrategy();
                yield return _tileCreator.Initialize(terrainStrategy);
            }

            yield return LayerModules.Select(x => x.Initialize()).WaitForAll();
        }

        /// <summary>
        /// Prepare data and visuals for given tile cover. It loads the data to memory, generates vector feature visuals
        /// and prepare it all to ensure following tile requests will be finished in single frame.
        /// So this method doesn't create the tile, it prepares everything inside a tile.
        /// </summary>
        /// <param name="tileCover"></param>
        /// <returns></returns>
        public virtual IEnumerator LoadTileCoverToMemory(TileCover tileCover)
        {
            var hashsetTiles = new HashSet<CanonicalTileId>(tileCover.Tiles.Select(x => x.Canonical));
            var coroutines = LayerModules.SelectMany(x => x.GetTileCoverCoroutines(hashsetTiles).Where(x => x != null));
            yield return coroutines.WaitForAll();
        }

        public virtual void Load(TileCover tileCover)
        {
            RemoveUnnecessaryTiles(tileCover);

            // Protect filler children of temp tiles that are still loading.
            // Without this, fillers get pooled one frame after creation (DelveInto only
            // rescues them on the frame the temp tile is first added).
            for (var i = TempTiles.Count - 1; i >= 0; i--)
            {
                var tempTile = TempTiles[i];
                if (tempTile.Children != null && ActiveTiles.ContainsKey(tempTile.UnwrappedTileId))
                {
                    foreach (var child in tempTile.Children)
                    {
                        _toRemove.Remove(child.UnwrappedTileId);
                    }
                }
            }

            foreach (var tileId in tileCover.Tiles)
            {
                if (ActiveTiles.ContainsKey(tileId))
                {
                    continue;
                }

                UnityMapTile unityMapTile = null;
                {
                    if (CreateTileInstant(tileId, out unityMapTile))
                    {
                        ShowTile(unityMapTile);
                        continue;
                    }
                    else
                    {
                        CreateTempTile(tileId, out unityMapTile);
                        // Reuse the tile's own Children list across pool/reuse cycles instead of
                        // allocating one per cache miss. PoolTile clears it on return.
                        if (unityMapTile.Children == null)
                            unityMapTile.Children = new List<UnityMapTile>();
                        else
                            unityMapTile.Children.Clear();
                        var coveredByQuadrants = DelveInto(tileId, unityMapTile.Children, recursiveDepth: 1);
                        ActiveTiles.Add(tileId, unityMapTile);
                        TempTiles.Add(unityMapTile);
                        if (!coveredByQuadrants)
                        {
                            ShowTile(unityMapTile);
                        }
                    }
                }
            }

            foreach (var tileId in _toRemove)
            {
                if (ActiveTiles.TryGetValue(tileId, out var tile))
                {
                    if (tile.LoadingState == LoadingState.Temporary)
                    {
                        TempTiles.Remove(tile);
                    }

                    PoolTile(tile);
                }
            }

            _retainedTiles.Clear();
            foreach (var tile in tileCover.Tiles)
            {
                _retainedTiles.Add(tile.Canonical);
            }

            foreach (var visualization in LayerModules)
            {
                visualization.RetainTiles(_retainedTiles);
            }
        }

        /// <summary>
        /// Create the map in given tileCover area. Makes decision to load or unload tiles and handle temporary filler
        /// tiles until actual tiles are loaded.
        /// </summary>
        public virtual void InternalUpdateCoroutine()
        {
            // Flush tiles deferred from the previous tick. Holding the children active for
            // one extra frame after the parent's ShowTile lets the parent finish rendering
            // before the children deactivate, which avoids a one-frame transparent gap on
            // iOS (see project_ios_tile_flicker note).
            for (var i = 0; i < _pendingPool.Count; i++)
            {
                PoolTile(_pendingPool[i]);
            }
            _pendingPool.Clear();

            //finish temp tiles from tempTiles list
            _toRemove.Clear();
            for (var index = TempTiles.Count - 1; index >= 0; index--)
            {
                var tilePair = TempTiles[index];
                if (!ActiveTiles.ContainsKey(tilePair.UnwrappedTileId))
                {
                    TempTiles.RemoveAt(index);
                    continue;
                }

                if (tilePair.LoadingState == LoadingState.Temporary && CreateTile(tilePair))
                {
                    ShowTile(tilePair);

                    if (tilePair.Children != null && tilePair.Children.Count > 0)
                    {
                        _pendingPool.AddRange(tilePair.Children);
                        tilePair.Children.Clear();
                    }

                    TempTiles.RemoveAt(index);

                    _quadrants[0] = tilePair.UnwrappedTileId.Quadrant(0);
                    _quadrants[1] = tilePair.UnwrappedTileId.Quadrant(1);
                    _quadrants[2] = tilePair.UnwrappedTileId.Quadrant(2);
                    _quadrants[3] = tilePair.UnwrappedTileId.Quadrant(3);
                    for (int i = 0; i < 4; i++)
                    {
                        if (ActiveTiles.TryGetValue(_quadrants[i], out _))
                        {
                            _toRemove.Add(_quadrants[i]);
                        }
                    }
                }
            }

            foreach (var tileId in _toRemove)
            {
                if (ActiveTiles.TryGetValue(tileId, out var tile))
                {
                    if (tile.LoadingState == LoadingState.Temporary)
                    {
                        TempTiles.Remove(tile);
                    }

                    _pendingPool.Add(tile);
                }
            }
        }


        /// <summary>
        /// Minimal function that'll try to load view with whatever data is available.
        /// It will not unload any tiles, it will not trigger any data fetching.
        /// It'll only organize and use data already in memory.
        /// If resources required for the requested tile aren't ready, it'll use whatever available
        /// and create a "temporary tile".
        /// </summary>
        /// <param name="tileCover"></param>
        public void LoadSnapshot(TileCover tileCover)
        {
            foreach (var tileId in tileCover.Tiles)
            {
                if (ActiveTiles.ContainsKey(tileId))
                    continue;

                UnityMapTile unityMapTile = null;
                if (!CreateTileInstant(tileId, out unityMapTile)) //if we can't fully load the tile
                {
                    CreateTempTile(tileId, out unityMapTile); //we load it whatever data we can find
                    ActiveTiles.Add(tileId, unityMapTile);
                    TempTiles.Add(unityMapTile);
                }

                ShowTile(unityMapTile);
            }
        }

        public void OnDestroy()
        {
            _destroyed = true;
            if (_internalUpdateCoroutine != null && Runnable.Instance != null)
            {
                Runnable.Instance.StopCoroutine(_internalUpdateCoroutine);
                _internalUpdateCoroutine = null;
            }

            foreach (var layerModule in LayerModules)
            {
                layerModule.OnDestroy();
            }
        }

        /// <summary>
        /// Find a LayerModule by given type. LayerModules are kept as ILayerModule and this method queries by concrete
        /// so it might cause unexpected issues if there are multiple layer modules of same type. This method will simply
        /// return the first one found.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="module"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public bool TryGetLayerModule<T>(out T module) where T : ILayerModule
        {
            module = (T)LayerModules.FirstOrDefault(x => x is T);
            if (module == null)
            {
                var composite = LayerModules.FirstOrDefault(x => x is CompositeLayerModule) as CompositeLayerModule;
                if (composite != null)
                {
                    module = (T)composite.LayerModules.FirstOrDefault(x => x is T);
                }
            }
            return module != null;
        }


        private IEnumerator InternalUpdate()
        {
            while (!_destroyed)
            {
                InternalUpdateCoroutine();
                yield return null;
            }
        }

        private void RemoveUnnecessaryTiles(TileCover tileCover)
        {
            // Fillers used to be exempt from removal here, but that caused orphaned filler
            // z-fighting when their logical parent transitioned out of TempTile state. They
            // are now eligible for pooling; visual continuity is preserved by the temp-tile
            // filler protection pass in Load() above.
            _toRemove.Clear();
            foreach (var tilePair in ActiveTiles)
            {
                if (!tileCover.Tiles.Contains(tilePair.Key))
                {
                    _toRemove.Add(tilePair.Key);
                }
            }
        }


        protected bool DelveInto(UnwrappedTileId tileId, List<UnityMapTile> activeChildren, int recursiveDepth = 3)
        {
            var quadrantCheck = new bool[4];
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
                    unityMapTile.LoadingState = LoadingState.Filler;
                    activeChildren.Add(unityMapTile);
                    if (unityMapTile.Children != null && unityMapTile.Children.Count > 0)
                    {
                        foreach (var subchild in unityMapTile.Children)
                        {
                            activeChildren.Add(subchild);
                        }
                        unityMapTile.Children.Clear();
                    }

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
                        quadrantCheck[i] = DelveInto(quadrants[i], activeChildren, recursiveDepth - 1);
                    }
                }
            }

            if (quadrantCheck[0] || quadrantCheck[1] || quadrantCheck[2] || quadrantCheck[3])
            {
                for (int i = 0; i < 4; i++)
                {
                    if (quadrantCheck[i] == false)
                    {
                        CreateTempTile(quadrants[i], out var unityMapTile);
                        unityMapTile.LoadingState = LoadingState.Filler;
                        ActiveTiles[quadrants[i]] = unityMapTile;
                        ShowTile(unityMapTile);
                        activeChildren.Add(unityMapTile);
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
            if (tile.LoadingState == LoadingState.None)
                return;

            // Snapshot whether this tile is currently holding either bounds value.
            // Recycle() clears TerrainData below, so we have to check first.
            var terrain = _mapInformation.Terrain;
            var terrainData = tile.TerrainContainer?.TerrainData;
            bool contributedBound = terrainData != null && terrainData.IsElevationDataReady &&
                (Mathf.Approximately(terrainData.MaxElevation, terrain.MaxElevation) ||
                 Mathf.Approximately(terrainData.MinElevation, terrain.MinElevation));

            TileUnloading(tile);
            ActiveTiles.Remove(tile.UnwrappedTileId);
            tile.Recycle();
            tile.LoadingState = LoadingState.None;
            _tileCreator.PutTile(tile);

            if (tile.Children != null)
            {
                foreach (var tileChild in tile.Children)
                {
                    PoolTile(tileChild);
                }

                tile.Children.Clear();
            }

            if (contributedBound)
            {
                RecomputeTerrainBounds();
            }
        }

        private void RecomputeTerrainBounds()
        {
            var terrain = _mapInformation.Terrain;
            float max = 0f;
            float min = 0f;
            bool any = false;
            foreach (var kv in ActiveTiles)
            {
                var td = kv.Value.TerrainContainer?.TerrainData;
                if (td == null || !td.IsElevationDataReady) continue;
                if (!any)
                {
                    max = td.MaxElevation;
                    min = td.MinElevation;
                    any = true;
                }
                else
                {
                    if (td.MaxElevation > max) max = td.MaxElevation;
                    if (td.MinElevation < min) min = td.MinElevation;
                }
            }
            terrain.MaxElevation = any ? max : 0f;
            terrain.MinElevation = any ? min : 0f;
        }

        protected void CreateTempTile(UnwrappedTileId tileId, out UnityMapTile tile)
        {
            //we need to do positioning and scaling before mesh gen for now
            GetMapTile(tileId, out tile);

            foreach (var module in LayerModules)
            {
                module.LoadTempTile(tile);
            }

            tile.LoadingState = LoadingState.Temporary;
            // ActiveTiles.Add(tileId, tile);
            // TempTiles.Add(tile);
        }

        protected bool CreateTileInstant(UnwrappedTileId tileId, out UnityMapTile tile)
        {
            GetMapTile(tileId, out tile);

            var result = CreateTile(tile);

            if (!result)
            {
                tile.Recycle();
                tile.LoadingState = LoadingState.None;
                _tileCreator.PutTile(tile);
            }

            return result;
        }

        protected void GetMapTile(UnwrappedTileId tileId, out UnityMapTile tile)
        {
            var rectd = Conversions.TileBoundsInUnitySpace(tileId, _mapInformation.CenterMercator,
                _mapInformation.Scale);
            tile = null;
            tile = _tileCreator.GetTile();
            tile.transform.position = new Vector3((float)rectd.Center.x, 0, (float)rectd.Center.y);
            tile.transform.localScale = Vector3.one * (float)rectd.Size.x;
            tile.Initialize(tileId, (float)rectd.Size.x * _mapInformation.Scale);
        }

        protected bool CreateTile(UnityMapTile unityMapTile)
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
                unityMapTile.LoadingState = LoadingState.Finished;
                if (!ActiveTiles.ContainsKey(unityMapTile.UnwrappedTileId))
                {
                    ActiveTiles.Add(unityMapTile.UnwrappedTileId, unityMapTile);
                }

                var terrainData = unityMapTile.TerrainContainer?.TerrainData;
                if (terrainData != null && terrainData.IsElevationDataReady)
                {
                    // Recompute from ActiveTiles rather than widening against the existing
                    // Min/Max: the prior accumulate-only path left MinElevation pinned at
                    // its initial 0 whenever every loaded tile was at or above sea level,
                    // since (positive < 0) never fires.
                    RecomputeTerrainBounds();
                }

                TileLoaded(unityMapTile);
            }

            return tileFinished;
        }

        /// <summary>
        /// Triggers the repositioning for all tiles per module. This is necessary for vector module to move feature visuals
        /// if (and only if) map settings are such that camera is static and map&tiles are moving (slippy map).
        /// </summary>
        /// <param name="mapInformation"></param>
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

        /// <summary>
        /// Map tile finished loading with targeted detail level data. This tile isn't temporary anymore, it'll be in
        /// ActiveTiles list.
        /// </summary>
        public event Action<UnityMapTile> TileLoaded = (tile) => { };

        /// <summary>
        /// Map tile unloading event fires for tiles that are still in active tiles list but not in the latest tileCover.
        /// UnityMapTile object attached to event will be pooled after the event call.
        /// </summary>
        public event Action<UnityMapTile> TileUnloading = (tile) => { };
    }
}