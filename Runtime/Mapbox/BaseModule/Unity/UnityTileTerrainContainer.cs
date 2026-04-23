using System;
using Mapbox.BaseModule.Data.DataFetchers;
using Mapbox.BaseModule.Data.Tiles;
using UnityEngine;
using TerrainData = Mapbox.BaseModule.Data.DataFetchers.TerrainData;

namespace Mapbox.BaseModule.Unity
{
    [Serializable]
    public class UnityTileTerrainContainer
    {
        public TileContainerState State = TileContainerState.Final;
        
        private Action ElevationValuesUpdated;
        private Action _onDisposeCallback;
        private const string ElevationMultiplierFieldNameID = "_ElevationMultiplier";
        private const string ElevationChangeTimerFieldNameID = "_ElevationChangeTime";
        private const string ShaderElevationTextureScaleOffsetFieldNameID = "_HeightTexture_ST";
        private const string ShaderElevationTextureFieldNameID = "_HeightTexture";
		
        private static readonly int ElevationMultiplier = Shader.PropertyToID(ElevationMultiplierFieldNameID);
        private static readonly int ElevationChangeTime = Shader.PropertyToID(ElevationChangeTimerFieldNameID);
        private static readonly int HeightTextureST = Shader.PropertyToID(ShaderElevationTextureScaleOffsetFieldNameID);
        private static readonly int HeightTexture = Shader.PropertyToID(ShaderElevationTextureFieldNameID);
        
        
        private UnityMapTile _unityMapTile;
        [SerializeField] public TerrainData TerrainData;
        private Vector4 _terrainTextureScaleOffset;

        public UnityTileTerrainContainer(UnityMapTile unityMapTile, Action elevationUpdatedCallback, Action onDisposeCallback)
        {
            _unityMapTile = unityMapTile;
            _onDisposeCallback = onDisposeCallback;
            ElevationValuesUpdated = elevationUpdatedCallback;
        }

        /// <summary>
        /// Assigns terrain data to this tile: wires the material height texture, applies an
        /// optional conservative fallback bounds (so shader-displaced verts don't get
        /// frustum-culled before real Min/MaxElevation arrive), and subscribes to future
        /// elevation updates.
        /// </summary>
        /// <param name="terrainData">Raster + optional decoded elevation for this tile.</param>
        /// <param name="useShaderElevation">When true, the material's <c>_ElevationMultiplier</c> is set to 1 so the vertex shader samples <c>_HeightTexture</c>; otherwise 0 (CPU-displaced verts).</param>
        /// <param name="state">Marks the tile container as temporary (using a parent's data while its own loads) or final.</param>
        /// <param name="fallbackMaxElevationMeters">Pre-extraction bounds padding in meters. Pass 0 to skip fallback bounds entirely; otherwise the mesh's Y bounds are expanded to cover <c>[0, value * TileScale]</c>.</param>
        public void SetTerrainData(TerrainData terrainData, bool useShaderElevation, TileContainerState state = TileContainerState.Final, float fallbackMaxElevationMeters = 0f)
        {
            // Detach from the previous TerrainData (if any) before swapping. Without this,
            // a reassignment leaks our subscription on the old data and causes spurious
            // bounds updates if that data is shared with other tiles.
            if (TerrainData != null)
            {
                TerrainData.ElevationValuesUpdated -= OnElevationValuesUpdated;
            }

            terrainData?.SetDisposeCallback(null);

            State = state;
            if (terrainData.Texture == null || terrainData.TileId.Z == 0)
            {
                Debug.Log("no texture?");
            }
            TerrainData = terrainData;
            TerrainData.SetDisposeCallback(_onDisposeCallback);

            OnTerrainUpdated();

            // Apply a generous fallback bounds up front so shader-displaced geometry does not
            // get frustum-culled before real Min/MaxElevation arrive (or forever, if CPU
            // elevation extraction is disabled). OnElevationValuesUpdated later tightens the
            // bounds to the actual elevation range if the float[] is decoded.
            if (fallbackMaxElevationMeters > 0f)
            {
                _unityMapTile.SetFallbackMeshBounds(fallbackMaxElevationMeters);
            }

            // Subscribe multicast-safe so other listeners (e.g. ElevatedTerrainStrategy's
            // deferred collider rebuild) can coexist.
            TerrainData.ElevationValuesUpdated += OnElevationValuesUpdated;

            if (TerrainData.IsElevationDataReady)
            {
                OnElevationValuesUpdated();
            }

            _unityMapTile.Material.SetFloat(ElevationMultiplier, useShaderElevation ? 1 : 0);
        }

        public void OnTerrainUpdated()
        {
            if (TerrainData == null)
                return;
        
            _terrainTextureScaleOffset = _unityMapTile.CanonicalTileId.CalculateScaleOffsetAtZoom(TerrainData.TileId.Z);
            
            _unityMapTile.Material.SetVector(HeightTextureST, _terrainTextureScaleOffset);
            _unityMapTile.Material.SetTexture(HeightTexture, TerrainData.Texture);

            //_unityMapTile._material.SetFloat(_tileScaleFieldNameID, _unityMapTile.TileScale);
            _unityMapTile.Material.SetFloat("_IsFallbackTexture", 0);
            _unityMapTile.Material.SetFloat(ElevationChangeTime, Time.time);
        }

        public void OnElevationValuesUpdated()
        {
            if (TerrainData == null)
            {
                Debug.Log("TerrainData is null, missing a isRecycled check?");
                return;
            }
            TerrainData.IsElevationDataReady = true;
            ElevationValuesUpdated();
        }

        public TerrainData GetAndClearTerrainData()
        {
            if (TerrainData == null)
                return null;

            TerrainData.ElevationValuesUpdated -= OnElevationValuesUpdated;
            _unityMapTile.Material.SetTexture(HeightTexture, Texture2D.grayTexture);
            var rd = TerrainData;
            TerrainData = null;
            return rd;
        }
        
        public float QueryHeightData(float x, float y)
        {
            if (TerrainData != null && TerrainData.ElevationValues.Length > 0)
            {
                var width = (int)Mathf.Sqrt(TerrainData.ElevationValues.Length);
                var sectionWidth = width * _terrainTextureScaleOffset.x - 1;
                var padding = width * new Vector2(_terrainTextureScaleOffset.z, _terrainTextureScaleOffset.w);
                
                var xx = padding.x + (x * sectionWidth);
                var yy = padding.y + (y * sectionWidth);

                var index = (int) yy * width
                            + (int) xx;
                if (TerrainData.ElevationValues.Length <= index)
                {
                    return 0;
                }
                else
                {
                    return TerrainData.ElevationValues[(int) yy * width + (int) xx];
                }

            }
            return 0;
        }

        /// <summary>
        /// Hard-disable terrain for this tile: zeroes the shader elevation multiplier,
        /// unsubscribes from <see cref="TerrainData.ElevationValuesUpdated"/>, and drops the
        /// cached <see cref="TerrainData"/> reference. Call when the tile's zoom falls
        /// outside the supported range so nothing lingers on it.
        /// </summary>
        public void DisableTerrain()
        {
            State = TileContainerState.Final;
            GetAndClearTerrainData();
            _unityMapTile.Material.SetFloat(ElevationMultiplier, 0);
        }

        public void OnDestroy()
        {
            if (TerrainData != null)
            {
                TerrainData.ElevationValuesUpdated -= OnElevationValuesUpdated;
                TerrainData = null;
            }
        }
    }
    
    public enum TileContainerState
    {
        Temporary,
        Final
    }
}