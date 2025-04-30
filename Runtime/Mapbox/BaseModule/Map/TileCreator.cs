using System.Collections;
using Mapbox.BaseModule.Unity;
using Mapbox.BaseModule.Utilities;
using Mapbox.ImageModule.Terrain.Settings;
using Mapbox.ImageModule.Terrain.TerrainStrategies;
using UnityEngine;

namespace Mapbox.BaseModule.Map
{
    public interface ITileCreator
    {
        IEnumerator Initialize();
        UnityMapTile GetTile();
        void PutTile(UnityMapTile tile);
    }
    public class TileCreator : ITileCreator
    {
        public Material[] TileMaterials;
        private ObjectPool<UnityMapTile> _tilePool;
        private UnityContext _unityContext;
        private int _cacheSize;

        public UnityMapTile GetTile() => _tilePool.GetObject();
        public void PutTile(UnityMapTile tile) => _tilePool.Put(tile);

        public TileCreator(UnityContext unityContext, Material[] tileMaterials = null, int cacheSize = 25)
        {
            TileMaterials = tileMaterials;
            _unityContext = unityContext;
            _cacheSize = cacheSize;
        }

        public IEnumerator Initialize()
        {
            _tilePool = new ObjectPool<UnityMapTile>(() => CreateTile(_unityContext));
            yield return _tilePool.InitializeItems(_cacheSize);
        }

        private UnityMapTile CreateTile(UnityContext unityContext)
        {
            var tile = new GameObject("TilePoolObject").AddComponent<UnityMapTile>();
            if (_unityContext.BaseTileRoot != null)
            {
                tile.gameObject.layer = _unityContext.BaseTileRoot.gameObject.layer;
                tile.transform.SetParent(_unityContext.BaseTileRoot, false);
            }

            if (TileMaterials?.Length > 0)
            {
                tile.MeshRenderer.materials = TileMaterials;
            }

            tile.Material = tile.MeshRenderer.material;
            tile.gameObject.SetActive(false);
            return tile;
        }
    }
}