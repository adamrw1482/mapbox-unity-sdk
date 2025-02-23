using System;
using Mapbox.BaseModule.Data.DataFetchers;
using Mapbox.BaseModule.Data.Tiles;
using UnityEngine;

namespace Mapbox.BaseModule.Unity
{
    [Serializable]
    public class UnityTileImageContainer
    {
        public TileContainerState State = TileContainerState.Final;
        private UnityMapTile _unityMapTile;
        private string _mainTexFieldNameID = "_MainTex";
        private string _mainTexStFieldNameID = "_MainTex_ST";
        private string _mainTextureChangeTimeFieldNameID = "_MainTextureChangeTime";
        [SerializeField] public RasterData ImageData;

        public UnityTileImageContainer(UnityMapTile unityMapTile)
        {
            _unityMapTile = unityMapTile;
        }

        public void SetImageData(RasterData imageData, TileContainerState state = TileContainerState.Final)
        {
            State = state;
            if (imageData.Texture == null || imageData.TileId.Z == 0)
            {
                Debug.Log("no texture?");
            }
            ImageData = imageData;
            OnImageryUpdated();
        }

        public void OnImageryUpdated()
        {
            if (ImageData == null)
                return;
        
            var scaleOffset = _unityMapTile.CanonicalTileId.CalculateScaleOffsetAtZoom(ImageData.TileId.Z);
            
            _unityMapTile.Material.SetTexture(_mainTexFieldNameID, ImageData.Texture);
            _unityMapTile.Material.SetVector(_mainTexStFieldNameID, scaleOffset);
            _unityMapTile.Material.SetFloat(_mainTextureChangeTimeFieldNameID, Time.time);
        }

        public RasterData GetAndClearImageData()
        {
            if (ImageData == null)
                return null;

            _unityMapTile.Material.SetTexture(_mainTexFieldNameID, null);
            var rd = ImageData;
            ImageData = null;
            return rd;
        }

        public void DisableImagery()
        {
            State = TileContainerState.Final;
            _unityMapTile.Material.SetTexture(_mainTexFieldNameID, null);
        }
        
        public void OnDestroy()
        {
            //anything to finalize here?
        }
    }
}