using System;
using System.Collections.Generic;
using Mapbox.BaseModule.Data.Tiles;
using Mapbox.BaseModule.Utilities;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mapbox.BaseModule.Unity
{
	public class UnityMapTile : MonoBehaviour
	{
		private Action<UnityMapTile> OnElevationValuesUpdated = (t) => { };
		public Action<UnwrappedTileId> OnDataDisposed = (t) => { };
		public UnwrappedTileId UnwrappedTileId { get; private set; }
		public CanonicalTileId CanonicalTileId { get; private set; }
		public float TileScale { get; private set; }

		//change this with list T : containers?
		public UnityTileTerrainContainer TerrainContainer;
		public UnityTileImageContainer ImageContainer;
		public UnityTileVectorContainer VectorContainer;
		
		private static readonly int TileScalePropertyId = Shader.PropertyToID("_TileScale");
		
		private MeshRenderer _meshRenderer;
		public MeshRenderer MeshRenderer => _meshRenderer;

		public Material Material;
		private MeshFilter _meshFilter;
		public MeshFilter MeshFilter => _meshFilter;
		public List<UnityMapTile> Children;

		public int MeshVertexCount = 0;

		//public bool IsTemporary = false;

		public LoadingState LoadingState;

		public void Awake()
		{
			ImageContainer = new UnityTileImageContainer(this, DataDisposed);
			VectorContainer = new UnityTileVectorContainer(this);
			TerrainContainer = new UnityTileTerrainContainer(this, ElevationUpdatedCallback, DataDisposed);
			
			_meshRenderer = gameObject.AddComponent<MeshRenderer>();
			_meshFilter = gameObject.AddComponent<MeshFilter>();
			_meshFilter.sharedMesh = new Mesh();
		}

		public void Initialize(UnwrappedTileId tileId, float scale)
		{
			TileScale = 1 / scale;
			UnwrappedTileId = tileId;
			CanonicalTileId = tileId.Canonical;
#if UNITY_EDITOR
			gameObject.name = tileId.ToString();
#endif
			
			Material.SetFloat(TileScalePropertyId, TileScale);
		}
		
		public void ElevationUpdatedCallback()
		{
			if (MeshFilter != null)
			{
				var centerHeight = (TerrainContainer.TerrainData.MaxElevation + TerrainContainer.TerrainData.MinElevation) / 2 * TileScale;
				var boxHeight = (TerrainContainer.TerrainData.MaxElevation - TerrainContainer.TerrainData.MinElevation)  * TileScale;
				_meshFilter.mesh.bounds = new Bounds(new Vector3(.5f, centerHeight, -.5f), new Vector3(1, boxHeight, 1));
			}
			OnElevationValuesUpdated(this);
		}
		
		public void Recycle()
		{
			gameObject.SetActive(false);
			ImageContainer.GetAndClearImageData();
			TerrainContainer.GetAndClearTerrainData();
			VectorContainer.GetAndClearVectorData();
		}

		private void DataDisposed()
		{
			OnDataDisposed(this.UnwrappedTileId);
		}
		
		private void OnDestroy()
		{
			ImageContainer.OnDestroy();
			TerrainContainer.OnDestroy();
			VectorContainer.OnDestroy();
		}
	}

	public enum LoadingState
	{
		None,
		Temporary,
		Finished,
		Filler
	}
}
