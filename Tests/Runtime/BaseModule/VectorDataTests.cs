using System.Collections;
using System.Collections.Generic;
using Mapbox.BaseModule.Data.DataFetchers;
using Mapbox.BaseModule.Data.Interfaces;
using Mapbox.BaseModule.Data.Platform.Cache;
using Mapbox.BaseModule.Data.Platform.Cache.SQLiteCache;
using Mapbox.BaseModule.Data.Tasks;
using Mapbox.BaseModule.Data.Tiles;
using Mapbox.BaseModule.Data.Vector2d;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Unity;
using Mapbox.BaseModule.Utilities;
using Mapbox.MapDebug.Scripts.Logging;
using Mapbox.UnityMapService;
using NUnit.Framework;
using UnityEditor.VersionControl;
using UnityEngine.TestTools;

namespace Mapbox.BaseModuleTests.DataTests
{
    [TestFixture]
    internal class VectorDataTests
    {
        private MockTaskManager _taskManager;
        private MapboxContext _mapboxContext;
        private DataFetchingManager _dataFetchingManager;
        private UnityContext _unityContext;
        private FileCache _fileCache;
        private ISqliteCache _sqliteCache;
        private UnwrappedTileId _tileId;
        private HashSet<CanonicalTileId> _testTileHashset;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            _taskManager = new MockTaskManager();
            _mapboxContext = new MapboxContext();
            _mapboxContext.LoadConfigurationWithoutValidation();
            _dataFetchingManager = new DataFetchingManager(_mapboxContext.GetAccessToken(), _mapboxContext.GetSkuToken);
            _unityContext = new UnityContext();
            _unityContext.Initialize(_taskManager);
            _fileCache = new MockFileCache(_taskManager);
            _fileCache.ClearAll();
            _sqliteCache = new MockSqliteCache(_taskManager);
            _sqliteCache.ClearDatabase();
            _sqliteCache.ReadySqliteDatabase();
            Runnable.EnableRunnableInEditor();
        
            var lonLat = new LatitudeLongitude(24.937700, 60.163200);
            _tileId = Conversions.LatitudeLongitudeToTileId(lonLat, 16);
            
            _testTileHashset = new HashSet<CanonicalTileId>()
            {
                _tileId.Canonical,
                new CanonicalTileId(_tileId.Z, _tileId.X + 1, _tileId.Y),
                new CanonicalTileId(_tileId.Z, _tileId.X + 2, _tileId.Y),
                new CanonicalTileId(_tileId.Z, _tileId.X + 3, _tileId.Y),
            };
        }
    
        [SetUp]
        public void SetUp()
        {
        
        }

        [UnityTest, Order(4)]
        public IEnumerator RequestTileList()
        {
            var mapboxContext1 = new MapboxContext();
            mapboxContext1.LoadConfigurationWithoutValidation();
            var mapService = new MapUnityService(_unityContext, mapboxContext1, null,
                new MapboxCacheManager(_unityContext, new MemoryCache(), _fileCache, _sqliteCache));

            var vectorTileset = MapboxDefaultVector.GetParameters(VectorSourceType.MapboxStreetsV8);
            var vectorSource = mapService.GetVectorSource(new VectorSourceSettings(){ TilesetId = vectorTileset.Id});

            List<VectorData> loadedTiles = null;
            var coroutine = Runnable.Instance.StartCoroutine(vectorSource.LoadTilesCoroutine(_testTileHashset, (data) =>
            {
                loadedTiles = data;
            }));
            yield return coroutine;
        
            Assert.NotNull(loadedTiles);
            Assert.IsNotEmpty(loadedTiles);
            foreach (var vectorData in loadedTiles)
            {
                var dataFromSqlite = _sqliteCache.Get<VectorData>(vectorTileset.Id, vectorData.TileId);
                Assert.NotNull(dataFromSqlite);
            }
        }

        
        [UnityTest, Order(3)]
        public IEnumerator RequestWithAllCaches()
        {
            var mapboxContext2 = new MapboxContext();
            mapboxContext2.LoadConfigurationWithoutValidation();
            var mapService = new MapUnityService(_unityContext, mapboxContext2, null,
                new MapboxCacheManager(_unityContext, new MemoryCache(), _fileCache, _sqliteCache));
            var vectorTileset = MapboxDefaultVector.GetParameters(VectorSourceType.MapboxStreetsV8);
            var vectorSource = mapService.GetVectorSource(new VectorSourceSettings(){ TilesetId = vectorTileset.Id});

            VectorData vectorData = null;
            var coroutineId = Runnable.Instance.StartCoroutine(vectorSource.LoadTileCoroutine(_tileId.Canonical, (data) =>
            {
                vectorData = data;
            }));
            yield return coroutineId;
        
            Assert.NotNull(vectorData.Data);
            Assert.AreEqual(0, _fileCache.GetFileList().Count, "File Cache should have zero tile");
            Assert.AreEqual(1, _sqliteCache.GetAllTiles().Count, "Sqlite should have more than one tiles");
        }
    
        [UnityTest, Order(2)]
        public IEnumerator RequestNoFileYesSqliteCache()
        {
            var mapboxContext3 = new MapboxContext();
            mapboxContext3.LoadConfigurationWithoutValidation();
            var mapService = new MapUnityService(_unityContext, mapboxContext3, null,
                new MapboxCacheManager(_unityContext, new MemoryCache(), null, _sqliteCache));
            var vectorTileset = MapboxDefaultVector.GetParameters(VectorSourceType.MapboxStreetsV8);
            var vectorSource = mapService.GetVectorSource(new VectorSourceSettings(){ TilesetId = vectorTileset.Id});
        
            VectorData vectorData = null;
            var coroutineId = Runnable.Run(vectorSource.LoadTileCoroutine(_tileId.Canonical, (data) =>
            {
                vectorData = data;
            }));

            while (Runnable.IsRunning(coroutineId))
            {
                yield return null;
            }
        
            Assert.NotNull(vectorData.Data);
            var sqliteTileCount = _sqliteCache.GetAllTiles().Count;
            Assert.AreEqual(1, sqliteTileCount, "Sqlite should have 1, has " + sqliteTileCount);
        }
    
        [UnityTest, Order(1)]
        public IEnumerator RequestNoFileNoSqliteCaches()
        {
            var cacheManager = new MapboxCacheManager(_unityContext, new MemoryCache(), null, null);
            var mapboxContext4 = new MapboxContext();
            mapboxContext4.LoadConfigurationWithoutValidation();
            var mapService = new MapUnityService(_unityContext, mapboxContext4, null, cacheManager);
            var vectorTileset = MapboxDefaultVector.GetParameters(VectorSourceType.MapboxStreetsV8);
            var vectorSource = mapService.GetVectorSource(new VectorSourceSettings(){ TilesetId = vectorTileset.Id});
        
            VectorData vectorData = null;
            var coroutineId = Runnable.Run(vectorSource.LoadTileCoroutine(_tileId.Canonical, (data) =>
            {
                vectorData = data;
            }));

            while (Runnable.IsRunning(coroutineId))
            {
                yield return null;
            }
        
            Assert.NotNull(vectorData.Data);
            Assert.AreEqual(vectorData.CacheType, CacheType.NoCache);
        }
    }
}