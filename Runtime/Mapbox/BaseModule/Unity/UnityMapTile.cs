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
		
		private string _tileScaleFieldNameID = "_TileScale";
		
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
			
			Material.SetFloat(_tileScaleFieldNameID, TileScale);
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

		/// <summary>
		/// Applies a conservative mesh bounds of <c>[0, maxElevationMeters * TileScale]</c>
		/// on Y so shader-displaced vertices stay inside the mesh's frustum culling volume
		/// before real <c>MinElevation</c>/<c>MaxElevation</c> are known (or permanently,
		/// when CPU extraction is disabled). A later call to <see cref="ElevationUpdatedCallback"/>
		/// tightens the bounds if CPU elevation eventually arrives.
		/// </summary>
		/// <param name="maxElevationMeters">Highest elevation (in meters) the camera is expected to view.</param>
		public void SetFallbackMeshBounds(float maxElevationMeters)
		{
			if (_meshFilter == null)
			{
				return;
			}
			var boxHeight = maxElevationMeters * TileScale;
			var centerHeight = boxHeight * 0.5f;
			_meshFilter.mesh.bounds = new Bounds(new Vector3(.5f, centerHeight, -.5f), new Vector3(1, boxHeight, 1));
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

			// Unity does not auto-free runtime-created Meshes when the GameObject is
			// destroyed. Release both the render mesh allocated in Awake and the dedicated
			// collider mesh allocated by ElevatedTerrainStrategy (named "TerrainCollider").
			// The identity + name checks avoid double-freeing if the collider happens to be
			// aliased to the render mesh (FlatTerrainStrategy path). Check children too —
			// the collider may live on a dedicated layer child GameObject.
			var renderMesh = _meshFilter != null ? _meshFilter.sharedMesh : null;
			var colliders = GetComponentsInChildren<MeshCollider>(includeInactive: true);
			foreach (var mc in colliders)
			{
				if (mc.sharedMesh != null &&
				    mc.sharedMesh.name == "TerrainCollider" &&
				    mc.sharedMesh != renderMesh)
				{
					Destroy(mc.sharedMesh);
				}
			}
			if (renderMesh != null)
			{
				Destroy(renderMesh);
			}
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
