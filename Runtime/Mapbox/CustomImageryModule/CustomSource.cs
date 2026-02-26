using Mapbox.BaseModule.Data.DataFetchers;
using Mapbox.BaseModule.Data.Platform.Cache;
using Mapbox.BaseModule.Data.Tiles;
using Mapbox.BaseModule.Map;
using Mapbox.UnityMapService.DataSources;

namespace Mapbox.CustomImageryModule
{
    public class CustomSource : ImageSource<RasterData>
    {
        protected CustomSourceSettings _customSourceSettings;
        protected ImageSourceSettings _settings;
        
        public CustomSource(CustomSourceSettings customSourceSettings, DataFetchingManager dataFetchingManager,
            MapboxCacheManager mapboxCacheManager, ImageSourceSettings settings) 
            : base(dataFetchingManager, mapboxCacheManager, settings)
        {
            _settings = settings;
            _customSourceSettings = customSourceSettings;
        }

        protected override RasterTile CreateTile(CanonicalTileId tileId, string tilesetId)
        {
            if (_customSourceSettings.InvertY)
            {
                return new CustomTMSTile(_customSourceSettings.UrlFormat, tileId, tilesetId, true);
            }
            else
            {
                return new RasterTile(tileId, tilesetId, true);
            }
        }

        protected override RasterData CreateRasterDataWrapper(RasterTile tile)
        {
            RasterData rasterData;
            rasterData = new RasterData()
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

        protected override void CheckExpiration(RasterData cacheItem)
        {
            //not checking for expiration and updating for custom sources
            //just using whatever is in cache
            return;
        }
    }
}