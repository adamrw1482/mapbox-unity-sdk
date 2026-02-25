using Mapbox.BaseModule.Data.DataFetchers;
using Mapbox.BaseModule.Data.Platform.Cache;
using Mapbox.BaseModule.Data.Tiles;
using Mapbox.BaseModule.Map;
using Mapbox.UnityMapService.DataSources;

namespace Mapbox.CustomImageryModule
{
    public class CustomTerrainSource : TerrainSource
    {
        protected CustomSourceSettings _customSourceSettings;
        protected ImageSourceSettings _settings;
        
        public CustomTerrainSource(CustomSourceSettings customSettings, DataFetchingManager dataFetchingManager, MapboxCacheManager mapboxCacheManager, ImageSourceSettings settings) 
            : base(dataFetchingManager, mapboxCacheManager, settings)
        {
            _customSourceSettings = customSettings;
            _settings = settings;
        }

        protected override RasterTile CreateTile(CanonicalTileId tileId, string tilesetId)
        {
            if (_customSourceSettings.InvertY)
            {
                return new CustomTMSTile(
                    _customSourceSettings.UrlFormat,
                    tileId, tilesetId, true);
            }
            else
            {
                return new RasterTile(tileId, tilesetId, true);
            }
        }
        
        protected override TerrainData CreateRasterDataWrapper(RasterTile tile)
        {
            TerrainData rasterData;
            rasterData = new TerrainData()
            {
                TileId = tile.Id,
                TilesetId = tile.TilesetId,
                Texture = tile.Texture2D,
                CacheType = tile.FromCache,
                Data = tile.Data,
                ETag = tile.ETag,
                ExpirationDate = tile.ExpirationDate
            };

            return rasterData;
        }
    }
}