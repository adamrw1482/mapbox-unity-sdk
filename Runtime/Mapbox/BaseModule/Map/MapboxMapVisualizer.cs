using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mapbox.BaseModule.Data.Interfaces;
using Mapbox.BaseModule.Data.Tiles;
using Mapbox.BaseModule.Unity;
using Mapbox.BaseModule.Utilities;
using UnityEngine;
using TerrainData = Mapbox.BaseModule.Data.DataFetchers.TerrainData;

namespace Mapbox.BaseModule.Map
{
    /// <summary>
    /// The primary object responsible for preparing the data and generating the visuals of the map.
    /// </summary>
    [Serializable]
    public class MapboxMapVisualizer : IMapVisualizer
    {
        // Upper bound of the Mercator tile pyramid. Used by DelveInto and any other
        // recursion that needs an absolute hard cap independent of user-configurable
        // MaximumZoomLevel settings (which represent LOD intent, not pyramid depth).
        public const int MaxMercatorZoom = 22;

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
        // Stores (tile, generation-snapshot) so a flush can detect tiles that got
        // synchronously pool-recycled and re-issued via GetTile() between queue and flush.
        private struct PendingPoolEntry
        {
            public UnityMapTile Tile;
            public int Generation;
        }
        private readonly List<PendingPoolEntry> _pendingPool = new List<PendingPoolEntry>();

        private void QueuePool(UnityMapTile tile)
        {
            if (tile == null) return;
            _pendingPool.Add(new PendingPoolEntry { Tile = tile, Generation = tile.Generation });
        }

        private void FlushPendingPool()
        {
            for (var i = 0; i < _pendingPool.Count; i++)
            {
                var entry = _pendingPool[i];
                // Skip entries whose tile was already pool-recycled (and possibly reused
                // for a different id) between queueing and now. PoolTile bumps Generation,
                // so a mismatch means we're stale.
                if (entry.Tile == null || entry.Tile.Generation != entry.Generation)
                {
                    continue;
                }
                PoolTile(entry.Tile);
            }
            _pendingPool.Clear();
        }

        // Explicit stack for the iterative DelveInto (was recursive with per-call new bool[4]
        // and new UnwrappedTileId[4] allocations). Bounded by recursion depth × 4, so the
        // List reaches its peak size after one large call and never re-allocates after.
        private struct DelveFrame
        {
            public UnwrappedTileId TileId;
            public int Depth;
            public int Found;       // bitmask: bit i set if quadrant i is satisfied (here or via descendants)
            public int NextChild;   // 0..4
            public int ParentFrame; // index in _delveStack, or -1 for root
            public int ParentSlot;  // which slot of the parent we are filling (0..3)
        }
        private readonly List<DelveFrame> _delveStack = new List<DelveFrame>();

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
                        // Invalidate any in-flight pending-pool entry that snapshotted this
                        // child's prior generation — but only for children that are still in
                        // Filler state. Bumping unconditionally would also invalidate a
                        // pending entry queued for a different (legitimate) reason such as
                        // parent-finished child-pool.
                        if (child.LoadingState == LoadingState.Filler)
                        {
                            child.Generation++;
                        }
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

                    // Defer pooling by one frame for the iOS-flicker rationale. A subsequent
                    // Load before the flush may re-queue the same tile via the same code
                    // path — that's harmless because PoolTile bumps Generation and the flush
                    // filters by gen-snapshot match (second queue is skipped).
                    QueuePool(tile);
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
            // Drain the terrain-bounds dirty flag once per tick. Recompute is O(ActiveTiles)
            // so doing it here (and not on every PoolTile / CreateTile / async-decode event)
            // turns what was O(N²) for a Load into a single O(N) pass per frame.
            if (_terrainBoundsDirty)
            {
                RecomputeTerrainBounds();
                _terrainBoundsDirty = false;
            }

            // Flush tiles deferred from the previous tick. Holding the children active for
            // one extra frame after the parent's ShowTile lets the parent finish rendering
            // before the children deactivate, which avoids a one-frame transparent gap on
            // iOS (see project_ios_tile_flicker note). FlushPendingPool filters out entries
            // whose tile was already pool-recycled and re-issued between queue and flush.
            FlushPendingPool();

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
                        for (int c = 0; c < tilePair.Children.Count; c++)
                        {
                            QueuePool(tilePair.Children[c]);
                        }
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

                    QueuePool(tile);
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

            // Detach any pending async-decode subscriptions so the closures don't keep
            // shared TerrainData rooted past the visualizer's lifetime.
            for (int i = 0; i < _pendingElevationWatches.Count; i++)
            {
                var (data, handler) = _pendingElevationWatches[i];
                if (data != null && handler != null)
                {
                    data.ElevationValuesUpdated -= handler;
                }
            }
            _pendingElevationWatches.Clear();

            // Flush any deferred pool entries so each pending tile gets its Recycle()
            // (returns elevation arrays to the pool, restores shared meshes, etc).
            FlushPendingPool();

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
            // Iterative replacement for the prior recursive implementation. Same semantics:
            // at each level we look up the 4 quadrants in ActiveTiles; missing ones recurse
            // deeper up to `recursiveDepth`; if any quadrant at a level is satisfied (here
            // or via a descendant), the remaining slots at THAT level get temp filler tiles.
            _delveStack.Clear();
            _delveStack.Add(new DelveFrame
            {
                TileId = tileId,
                Depth = recursiveDepth,
                Found = 0,
                NextChild = 0,
                ParentFrame = -1,
                ParentSlot = -1,
            });

            bool rootResult = false;

            while (_delveStack.Count > 0)
            {
                int frameIdx = _delveStack.Count - 1;
                var frame = _delveStack[frameIdx];

                if (frame.NextChild < 4)
                {
                    int slot = frame.NextChild;
                    var quadrant = frame.TileId.Quadrant(slot);
                    frame.NextChild++;

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
                        frame.Found |= 1 << slot;
                        _delveStack[frameIdx] = frame;
                    }
                    else if (frame.Depth > 0 && frame.TileId.Z < MaxMercatorZoom)
                    {
                        // Save the advanced NextChild before pushing the child frame —
                        // we'll resume this slot's evaluation after the child finishes.
                        _delveStack[frameIdx] = frame;
                        _delveStack.Add(new DelveFrame
                        {
                            TileId = quadrant,
                            Depth = frame.Depth - 1,
                            Found = 0,
                            NextChild = 0,
                            ParentFrame = frameIdx,
                            ParentSlot = slot,
                        });
                    }
                    else
                    {
                        // Leaf miss: nothing found here, no deeper recursion allowed.
                        _delveStack[frameIdx] = frame;
                    }
                }
                else
                {
                    bool anyFound = frame.Found != 0;
                    if (anyFound)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            if ((frame.Found & (1 << i)) == 0)
                            {
                                var q = frame.TileId.Quadrant(i);
                                CreateTempTile(q, out var unityMapTile);
                                unityMapTile.LoadingState = LoadingState.Filler;
                                ActiveTiles[q] = unityMapTile;
                                ShowTile(unityMapTile);
                                activeChildren.Add(unityMapTile);
                            }
                        }
                    }

                    if (frame.ParentFrame >= 0)
                    {
                        var parent = _delveStack[frame.ParentFrame];
                        if (anyFound)
                        {
                            parent.Found |= 1 << frame.ParentSlot;
                        }
                        _delveStack[frame.ParentFrame] = parent;
                    }
                    else
                    {
                        rootResult = anyFound;
                    }
                    _delveStack.RemoveAt(frameIdx);
                }
            }

            return rootResult;
        }

        protected void ShowTile(UnityMapTile unityTile)
        {
            unityTile.gameObject.SetActive(true);
            _mapInformation.PositionObjectFor(unityTile.gameObject, unityTile.CanonicalTileId);
        }

        // Set whenever an event that could shift Terrain.Min/Max occurs (tile pooled
        // while holding a boundary value, async decode arriving on an already-finished
        // tile, etc). InternalUpdateCoroutine drains the flag once per tick by running
        // RecomputeTerrainBounds — avoids the O(N²) cost of recursive PoolTile each
        // calling Recompute itself.
        private bool _terrainBoundsDirty;

        protected void PoolTile(UnityMapTile tile)
        {
            if (tile.LoadingState == LoadingState.None)
                return;

            // Invalidate any deferred-pool entries that snapshotted this tile's previous
            // generation — they'd otherwise re-pool the tile after it's been reissued via
            // GetTile() for a different id.
            tile.Generation++;

            // Snapshot whether this tile is currently holding either bounds value, OR
            // is the last contributor in ActiveTiles (so the empty-fallback path needs
            // to run). Recycle() clears TerrainData below, so we check first.
            var terrain = _mapInformation.Terrain;
            var terrainData = tile.TerrainContainer?.TerrainData;
            bool contributedMax = terrainData != null && terrainData.IsElevationDataReady &&
                terrainData.MaxElevation != 0f &&
                Mathf.Approximately(terrainData.MaxElevation, terrain.MaxElevation);
            bool contributedMin = terrainData != null && terrainData.IsElevationDataReady &&
                terrainData.MinElevation != 0f &&
                Mathf.Approximately(terrainData.MinElevation, terrain.MinElevation);
            // Also dirty when this is the last tile in ActiveTiles: an all-flat session
            // (every loaded tile had Min=Max=0) wouldn't trip the non-zero checks above,
            // so the empty-fallback in RecomputeTerrainBounds (reset to TerrainInfo
            // defaults) would never run when the map empties out.
            bool wasLastActive = terrainData != null && ActiveTiles.Count == 1 &&
                ActiveTiles.ContainsKey(tile.UnwrappedTileId);

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

            if (contributedMax || contributedMin || wasLastActive)
            {
                _terrainBoundsDirty = true;
            }
        }

        // Tracks the one-shot ElevationValuesUpdated subscriptions we've attached for
        // shader-mode tiles whose CPU decode hasn't arrived yet. Stored by (data,
        // handler) so OnDestroy can detach if the data is disposed before the event fires.
        private readonly List<(TerrainData data, Action handler)> _pendingElevationWatches =
            new List<(TerrainData, Action)>();

        private void WatchForAsyncElevationDecode(TerrainData data)
        {
            // De-dup: shared TerrainData is referenced by up to 16 render tiles. One
            // watch per data is enough — the dirty flag triggers a single recompute.
            for (int i = 0; i < _pendingElevationWatches.Count; i++)
            {
                if (ReferenceEquals(_pendingElevationWatches[i].data, data)) return;
            }
            Action handler = null;
            Action onDispose = null;
            handler = () =>
            {
                data.ElevationValuesUpdated -= handler;
                data.RemoveDisposeCallback(onDispose);
                for (int i = _pendingElevationWatches.Count - 1; i >= 0; i--)
                {
                    if (ReferenceEquals(_pendingElevationWatches[i].data, data) &&
                        ReferenceEquals(_pendingElevationWatches[i].handler, handler))
                    {
                        _pendingElevationWatches.RemoveAt(i);
                        break;
                    }
                }
                _terrainBoundsDirty = true;
            };
            // Also detach on dispose: TerrainData.Dispose doesn't fire ElevationValuesUpdated,
            // so without this hook the watch entry would stay in the list (rooting both
            // data and the closure) until the visualizer's own OnDestroy.
            onDispose = () =>
            {
                data.ElevationValuesUpdated -= handler;
                data.RemoveDisposeCallback(onDispose);
                for (int i = _pendingElevationWatches.Count - 1; i >= 0; i--)
                {
                    if (ReferenceEquals(_pendingElevationWatches[i].data, data) &&
                        ReferenceEquals(_pendingElevationWatches[i].handler, handler))
                    {
                        _pendingElevationWatches.RemoveAt(i);
                        break;
                    }
                }
            };
            _pendingElevationWatches.Add((data, handler));
            data.ElevationValuesUpdated += handler;
            data.AddDisposeCallback(onDispose);
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
            // When no tile is contributing, fall back to TerrainInfo's conservative
            // defaults rather than 0 — a flat AABB would frustum-cull plausible
            // mountains on the next frame before any sample arrives.
            terrain.MaxElevation = any ? max : TerrainInfo.DefaultMaxElevation;
            terrain.MinElevation = any ? min : TerrainInfo.DefaultMinElevation;
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
                // Mirror PoolTile's bump so any pending-pool entry that snapshotted
                // this tile's pre-failure generation is invalidated when the next
                // flush runs. The failure path is otherwise a silent bypass of the
                // re-borrow protection enforced everywhere else.
                tile.Generation++;
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
                if (terrainData != null)
                {
                    if (terrainData.IsElevationDataReady)
                    {
                        // Data already decoded — mark bounds dirty for the next coroutine
                        // tick. Deferring avoids the O(N²) cost of recomputing inside
                        // every CreateTile within a single Load.
                        _terrainBoundsDirty = true;
                    }
                    else
                    {
                        // Shader-mode tiles are "finished" once the texture is ready, but
                        // the CPU decode (and therefore Min/MaxElevation) arrives async
                        // later. Hook a one-shot widening so the bounds catch up.
                        WatchForAsyncElevationDecode(terrainData);
                    }
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