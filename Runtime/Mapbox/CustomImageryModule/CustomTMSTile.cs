using System;
using Mapbox.BaseModule.Data.DataFetchers;
using Mapbox.BaseModule.Data.Platform;
using Mapbox.BaseModule.Data.Tiles;
using UnityEngine;

namespace Mapbox.CustomImageryModule
{
    /// <summary>
    /// TMS tile in the name means Y axis is inverted in the tile ids
    /// </summary>
    public class CustomTMSTile : RasterTile
    {
        private string _urlFormat;
        public CustomTMSTile(string urlFormat, CanonicalTileId tileId, string tilesetId, bool useNonReadableTexture) : base(tileId, tilesetId, useNonReadableTexture)
        {
            _urlFormat = urlFormat;
        }

        public override void Initialize(IFileSource fileSource, Action<DataFetchingResult> p)
        {
            TileState = TileState.Loading;
            _callback = p;

            var invertY = (Mathf.Pow(2, Id.Z))- Id.Y - 1;
            _generatedUrl = string.Format(_urlFormat, Id.Z, Id.X, (int)invertY);
            DoTheRequest(fileSource);
        }
        
        protected override void DoTheRequest(IFileSource fileSource)
        {
            _webRequest = fileSource.CustomImageRequest(_generatedUrl, HandleTileResponse, ETag, 10, IsTextureNonreadable);
        }
    }
}