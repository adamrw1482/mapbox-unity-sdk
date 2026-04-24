using System;
using System.Collections;
using System.Collections.Generic;
using Mapbox.BaseModule.Data.DataFetchers;
using Mapbox.BaseModule.Data.Tiles;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Unity;
using Mapbox.BaseModule.Utilities;
using Mapbox.ImageModule.Terrain.Settings;
using Unity.Jobs;
using UnityEngine;
using TerrainData = Mapbox.BaseModule.Data.DataFetchers.TerrainData;

namespace Mapbox.ImageModule.Terrain.TerrainStrategies
{
	public class MeshDataArray
	{
		public Vector3[] Vertices;
		public Vector3[] Normals;
		public List<int[]> Triangles;
		public Vector2[] Uvs;

		public MeshDataArray()
		{
			Triangles = new List<int[]>();
		}
	}

	public class ElevatedTerrainStrategy : TerrainStrategy, IElevationBasedTerrainStrategy
	{
		[SerializeField]
		protected ElevationLayerProperties _elevationOptions = new ElevationLayerProperties();
		
		private MeshDataArray _baseMesh;
		private List<Vector3> _newVertexList;
		private List<Vector3> _newNormalList;
		private List<Vector2> _newUvList;

		private bool _useTileSkirts = false;
		private float _skirtSize = 1;

		private int _sideVertexCount;
		private int _requiredVertexCount;

		private TerrainColliderOptions _colliderOptions;

		// Collider geometry is reused across every tile's collider build. Lazily sized to
		// match modificationOptions.sampleCount the first time BuildAndAssignCollider runs.
		private Vector3[] _colliderVertices;
		private int[] _colliderTriangles;

		// Shared flat render mesh used by every tile in shader mode. All render tiles have
		// byte-identical CPU vertex data in that mode (the per-tile _HeightTexture_ST
		// sub-region determines the actual surface); pointing every MeshFilter at this
		// single Mesh avoids per-tile Mesh allocation and upload.
		private const string SharedFlatMeshName = "TerrainSharedFlat";
		private Mesh _sharedFlatMesh;

		// Tracks the (TerrainData, CanonicalTileId) last used to build each MeshCollider's
		// sharedMesh. Temp-tile → final-tile transitions can trigger RegisterTile with the
		// same data; skipping the rebuild avoids a redundant PhysX re-cook.
		private readonly Dictionary<MeshCollider, (TerrainData data, CanonicalTileId tileId)>
			_lastColliderBuild = new Dictionary<MeshCollider, (TerrainData, CanonicalTileId)>();
		
		public override int RequiredVertexCount
		{
			get
			{
				return _requiredVertexCount;
			}
		}

		public override void Initialize(ElevationLayerProperties elOptions)
		{
			if (elOptions != null)
			{
				_elevationOptions = elOptions;
			}

			_useTileSkirts = elOptions.sideWallOptions.isActive;
			 _sideVertexCount = _useTileSkirts
				? _elevationOptions.modificationOptions.sampleCount + 3
				: _elevationOptions.modificationOptions.sampleCount + 1;
			_skirtSize = elOptions.sideWallOptions.wallHeight;
			_colliderOptions = elOptions.colliderOptions;
			
			_newVertexList = new List<Vector3>(_requiredVertexCount);
			_newNormalList = new List<Vector3>(_requiredVertexCount);
			_newUvList = new List<Vector2>(_requiredVertexCount);
			
			_baseMesh = CreateBaseMesh(_elevationOptions.TileMeshSize, _sideVertexCount);
			_requiredVertexCount = _baseMesh.Vertices.Length;

			// Build the shader-mode shared render mesh from the base data once. Every
			// shader-mode tile's MeshFilter will point at this single instance.
			_sharedFlatMesh = new Mesh { name = SharedFlatMeshName };
			_sharedFlatMesh.subMeshCount = 2;
			_sharedFlatMesh.vertices = _baseMesh.Vertices;
			_sharedFlatMesh.normals = _baseMesh.Normals;
			for (var i = 0; i < _baseMesh.Triangles.Count; i++)
			{
				_sharedFlatMesh.SetTriangles(_baseMesh.Triangles[i], i);
			}
			_sharedFlatMesh.uv = _baseMesh.Uvs;
			_sharedFlatMesh.UploadMeshData(markNoLongerReadable: false);
		}

		/// <summary>
		/// Cascaded from <c>TerrainLayerModule.OnDestroy</c>. Releases the shared render
		/// mesh; per-tile collider meshes are released from <c>UnityMapTile.OnDestroy</c>.
		/// </summary>
		public override void OnDestroy()
		{
			if (_sharedFlatMesh != null)
			{
				UnityEngine.Object.Destroy(_sharedFlatMesh);
				_sharedFlatMesh = null;
			}
		}

		public override void RegisterTile(UnityMapTile tile, bool createElevatedMesh)
		{
			if (_elevationOptions.unityLayerOptions.addToLayer && tile.gameObject.layer != _elevationOptions.unityLayerOptions.layerId)
			{
				tile.gameObject.layer = _elevationOptions.unityLayerOptions.layerId;
			}

			if (!createElevatedMesh)
			{
				// Shader mode: point the tile at the shared flat mesh. Byte-identical
				// vertex data across tiles means a single Mesh covers the whole pool; the
				// shader picks the right sub-region via per-tile _HeightTexture_ST.
				if (tile.MeshFilter.sharedMesh != _sharedFlatMesh)
				{
					var previous = tile.MeshFilter.sharedMesh;
					tile.MeshFilter.sharedMesh = _sharedFlatMesh;
					// The mesh UnityMapTile.Awake allocated is now orphaned; destroy it
					// unless something else (not us) already put the shared mesh here.
					if (previous != null && previous != _sharedFlatMesh && previous.name != SharedFlatMeshName)
					{
						UnityEngine.Object.Destroy(previous);
					}
				}
				tile.MeshVertexCount = _sharedFlatMesh.vertexCount;
			}
			else if (tile.MeshVertexCount != RequiredVertexCount)
			{
				// CPU mode: each tile needs its own unique vertex buffer since the Y
				// displacement is per-tile. Reset the tile's Awake-allocated mesh from the
				// base template.
				Mesh sharedMesh;
				(sharedMesh = tile.MeshFilter.sharedMesh).Clear();
				var newMesh = _baseMesh;
				sharedMesh.subMeshCount = 2;
				sharedMesh.vertices = newMesh.Vertices;
				sharedMesh.normals = newMesh.Normals;
				for (var i = 0; i < newMesh.Triangles.Count; i++)
				{
					var triangle = newMesh.Triangles[i];
					sharedMesh.SetTriangles(triangle, i);
				}
				sharedMesh.uv = newMesh.Uvs;
				tile.MeshVertexCount = newMesh.Vertices.Length;
				tile.ElevationUpdatedCallback();
			}

			if (createElevatedMesh)
			{
				CreateElevatedMesh(tile);
			}

			if (_colliderOptions != null && _colliderOptions.addCollider)
			{
				RegisterCollider(tile);
			}
		}

		/// <summary>
		/// Ensures <paramref name="tile"/> has a <see cref="MeshCollider"/> backed by a
		/// CPU-elevated mesh. Builds immediately when elevation data is already decoded,
		/// otherwise defers until <see cref="TerrainData.ElevationValuesUpdated"/> fires
		/// (async GPU-readback path). Short-circuits when the tile's existing collider was
		/// already built from the same data + tileId (temp→final transitions trigger
		/// RegisterTile multiple times without the underlying data changing).
		/// </summary>
		private void RegisterCollider(UnityMapTile tile)
		{
			var data = tile.TerrainContainer != null ? tile.TerrainContainer.TerrainData : null;
			if (data == null)
			{
				return;
			}

			var existing = FindExistingCollider(tile);
			if (existing != null &&
			    _lastColliderBuild.TryGetValue(existing, out var last) &&
			    ReferenceEquals(last.data, data) &&
			    last.tileId.Equals(tile.CanonicalTileId))
			{
				return;
			}

			if (data.IsElevationDataReady)
			{
				BuildAndAssignCollider(tile);
			}
			else
			{
				// Defer until values arrive. The callback self-unsubscribes on fire and
				// no-ops if the tile has since been recycled onto different data, so a late
				// async readback does not stomp a freshly-reassigned tile.
				Action rebuild = null;
				rebuild = () =>
				{
					if (tile == null || tile.TerrainContainer == null || tile.TerrainContainer.TerrainData != data)
					{
						data.ElevationValuesUpdated -= rebuild;
						return;
					}
					BuildAndAssignCollider(tile);
					data.ElevationValuesUpdated -= rebuild;
				};
				data.ElevationValuesUpdated += rebuild;
			}
		}

		// Our generated grid has no duplicate verts, no degenerate triangles, and doesn't
		// need welding, so we tell PhysX to skip those cook stages. Keeps
		// CookForFasterSimulation on for faster runtime queries against the static terrain.
		private const MeshColliderCookingOptions TerrainColliderCookingOptions =
			MeshColliderCookingOptions.CookForFasterSimulation |
			MeshColliderCookingOptions.UseFastMidphase;

		// Name of the dedicated child GameObject that holds the MeshCollider when
		// useDedicatedColliderLayer is enabled. Keyed by name so we can locate + reuse it
		// across pool cycles without maintaining a per-tile dictionary.
		private const string ColliderChildName = "TerrainCollider";

		/// <summary>
		/// Looks up the existing <see cref="MeshCollider"/> for this tile without creating
		/// one, mirroring the location rule in <see cref="GetOrCreateCollider"/>. Used by
		/// the rebuild-short-circuit check.
		/// </summary>
		private MeshCollider FindExistingCollider(UnityMapTile tile)
		{
			if (_colliderOptions.useDedicatedColliderLayer)
			{
				var childTransform = tile.transform.Find(ColliderChildName);
				return childTransform != null ? childTransform.GetComponent<MeshCollider>() : null;
			}
			return tile.GetComponent<MeshCollider>();
		}

		/// <summary>
		/// Returns the <see cref="MeshCollider"/> the collider mesh should be assigned to.
		/// When <see cref="TerrainColliderOptions.useDedicatedColliderLayer"/> is enabled
		/// the collider lives on a child GameObject so it can sit on its own Unity Layer
		/// independently of the tile's render layer; otherwise it's attached to the tile
		/// itself. Applies our tuned <see cref="TerrainColliderCookingOptions"/> on first
		/// creation.
		/// </summary>
		private MeshCollider GetOrCreateCollider(UnityMapTile tile)
		{
			if (_colliderOptions.useDedicatedColliderLayer)
			{
				var childTransform = tile.transform.Find(ColliderChildName);
				GameObject childGo;
				if (childTransform == null)
				{
					childGo = new GameObject(ColliderChildName);
					childGo.transform.SetParent(tile.transform, worldPositionStays: false);
				}
				else
				{
					childGo = childTransform.gameObject;
				}
				childGo.layer = _colliderOptions.colliderLayerId;

				var childCollider = childGo.GetComponent<MeshCollider>();
				if (childCollider == null)
				{
					childCollider = childGo.AddComponent<MeshCollider>();
					childCollider.cookingOptions = TerrainColliderCookingOptions;
				}
				return childCollider;
			}

			var meshCollider = tile.GetComponent<MeshCollider>();
			if (meshCollider == null)
			{
				meshCollider = tile.gameObject.AddComponent<MeshCollider>();
				meshCollider.cookingOptions = TerrainColliderCookingOptions;
			}
			return meshCollider;
		}

		/// <summary>
		/// Builds a dedicated CPU-elevated collider mesh for <paramref name="tile"/> and
		/// assigns it to the tile's <see cref="MeshCollider"/>. Grid resolution mirrors the
		/// render mesh so physics stays visually aligned with the terrain surface.
		/// </summary>
		private void BuildAndAssignCollider(UnityMapTile tile)
		{
			var meshCollider = GetOrCreateCollider(tile);

			// Unity auto-populates sharedMesh on a newly added MeshCollider from the
			// GameObject's MeshFilter.sharedMesh. We must NOT write into that mesh — it is
			// the render mesh. Detect and allocate a dedicated collider Mesh instead.
			var mesh = meshCollider.sharedMesh;
			if (mesh == null || mesh == tile.MeshFilter.sharedMesh)
			{
				mesh = new Mesh { name = "TerrainCollider" };
				mesh.MarkDynamic();
			}

			// Grid resolution matches the render mesh so the collision surface aligns with
			// the visible terrain. If the user picks a coarser SimplificationFactor, the
			// collider follows.
			var sampleCount = _elevationOptions.modificationOptions.sampleCount;
			var side = sampleCount + 1;
			var size = _elevationOptions.TileMeshSize;
			var scale = tile.TileScale;

			var vertexCount = side * side;
			var triangleCount = sampleCount * sampleCount * 6;
			if (_colliderVertices == null || _colliderVertices.Length != vertexCount)
			{
				_colliderVertices = new Vector3[vertexCount];
			}
			if (_colliderTriangles == null || _colliderTriangles.Length != triangleCount)
			{
				_colliderTriangles = new int[triangleCount];
			}
			var vertices = _colliderVertices;
			var triangles = _colliderTriangles;

			// Lift the elevation-sampling math out of the inner loop: QueryHeightData
			// otherwise recomputes width / reads scale-offset per call. Doing it once per
			// build saves a function call + Mathf.Sqrt + Vector2 construction per vertex.
			var container = tile.TerrainContainer;
			var elevationValues = container.TerrainData.ElevationValues;
			var dataWidth = (int)Mathf.Sqrt(elevationValues.Length);
			var scaleOffset = container.TerrainTextureScaleOffset;
			var sectionWidth = dataWidth * scaleOffset.x - 1f;
			var paddingX = dataWidth * scaleOffset.z;
			var paddingY = dataWidth * scaleOffset.w;
			var invSampleCount = 1f / sampleCount;
			var maxIndex = dataWidth - 1;

			for (int y = 0; y < side; y++)
			{
				var yrat = y * invSampleCount;
				var sampleYf = paddingY + yrat * sectionWidth;
				if (sampleYf < 0f) sampleYf = 0f; else if (sampleYf > maxIndex) sampleYf = maxIndex;
				var y0 = (int)sampleYf;
				var y1 = y0 + 1; if (y1 > maxIndex) y1 = maxIndex;
				var fy = sampleYf - y0;
				var row0 = y0 * dataWidth;
				var row1 = y1 * dataWidth;
				var yy = (1f - yrat) * size;
				for (int x = 0; x < side; x++)
				{
					var xrat = x * invSampleCount;
					var sampleXf = paddingX + xrat * sectionWidth;
					if (sampleXf < 0f) sampleXf = 0f; else if (sampleXf > maxIndex) sampleXf = maxIndex;
					var x0 = (int)sampleXf;
					var x1 = x0 + 1; if (x1 > maxIndex) x1 = maxIndex;
					var fx = sampleXf - x0;

					// Bilinear: the render tile's vertex grid is routinely denser than the
					// shared data tile's pixel sub-region, so nearest-neighbor steps visibly.
					var h00 = elevationValues[row0 + x0];
					var h10 = elevationValues[row0 + x1];
					var h01 = elevationValues[row1 + x0];
					var h11 = elevationValues[row1 + x1];
					var h0 = h00 + (h10 - h00) * fx;
					var h1 = h01 + (h11 - h01) * fx;
					var sample = h0 + (h1 - h0) * fy;

					vertices[y * side + x] = new Vector3(xrat * size, sample * scale, -yy);
				}
			}

			int ti = 0;
			for (int y = 0; y < sampleCount; y++)
			{
				for (int x = 0; x < sampleCount; x++)
				{
					int vertA = y * side + x;
					int vertB = vertA + side + 1;
					int vertC = vertA + side;
					triangles[ti++] = vertA;
					triangles[ti++] = vertC;
					triangles[ti++] = vertB;

					vertA = y * side + x;
					vertB = vertA + 1;
					vertC = vertA + side + 1;
					triangles[ti++] = vertA;
					triangles[ti++] = vertC;
					triangles[ti++] = vertB;
				}
			}

			mesh.Clear();
			// SetVertices/SetTriangles accept the shared buffer directly and skip the
			// validation that the .vertices / .triangles property setters perform.
			mesh.SetVertices(vertices);
			mesh.SetTriangles(triangles, 0, triangles.Length, 0, calculateBounds: false);
			mesh.RecalculateBounds();

			// Record what we just built so RegisterCollider can short-circuit on
			// redundant subsequent RegisterTile calls (temp→final transitions).
			_lastColliderBuild[meshCollider] = (tile.TerrainContainer.TerrainData, tile.CanonicalTileId);

			if (_colliderOptions.asyncBakeCollider)
			{
				// Move the PhysX cook to a worker thread. The assignment (and thus any
				// implicit recook) happens one frame later once BakeMesh has populated the
				// native cooked-data cache for this mesh id. PhysX reuses that cache when
				// sharedMesh is assigned, so the main-thread step is near-free.
				var handle = new BakeColliderJob
				{
					MeshId = mesh.GetInstanceID(),
					CookingOptions = TerrainColliderCookingOptions
				}.Schedule();
				Runnable.Instance.StartCoroutine(CompleteBakeAndAssign(meshCollider, mesh, handle));
			}
			else
			{
				// Null-then-reassign forces PhysX to re-cook collision data; a same-reference
				// reassignment is a no-op.
				meshCollider.sharedMesh = null;
				meshCollider.sharedMesh = mesh;
			}
		}

		/// <summary>
		/// IJob wrapper around <c>Physics.BakeMesh</c> so the PhysX cook can run on a
		/// worker thread. The cooking options must match what the MeshCollider is
		/// configured with, otherwise PhysX discards the cached data and re-cooks on
		/// assignment.
		/// </summary>
		private struct BakeColliderJob : IJob
		{
			public int MeshId;
			public MeshColliderCookingOptions CookingOptions;

			public void Execute()
			{
				Physics.BakeMesh(MeshId, false, CookingOptions);
			}
		}

		/// <summary>
		/// Waits for an async <see cref="BakeColliderJob"/> to complete, then assigns the
		/// pre-cooked mesh to its <see cref="MeshCollider"/>. Handles: (a) the tile or
		/// collider got destroyed during the bake — we free the orphaned mesh so it doesn't
		/// leak; (b) a previous <c>TerrainCollider</c> mesh was attached to this collider —
		/// we destroy it once we've swapped in the new one, since each async build
		/// allocates a fresh Mesh.
		/// </summary>
		private static IEnumerator CompleteBakeAndAssign(MeshCollider meshCollider, Mesh mesh, JobHandle handle)
		{
			while (!handle.IsCompleted)
			{
				yield return null;
			}
			handle.Complete();

			if (meshCollider == null)
			{
				if (mesh != null)
				{
					UnityEngine.Object.Destroy(mesh);
				}
				yield break;
			}

			var previous = meshCollider.sharedMesh;
			meshCollider.sharedMesh = null;
			meshCollider.sharedMesh = mesh;
			if (previous != null && previous != mesh && previous.name == "TerrainCollider")
			{
				UnityEngine.Object.Destroy(previous);
			}
		}

		private void CreateElevatedMesh(UnityMapTile tile)
		{
			var mesh = tile.MeshFilter.mesh;
			var vertices = mesh.vertices;
			var sampleCount = (int)Mathf.Sqrt(mesh.vertexCount);
			for (int i = 0; i < vertices.Length; i++)
			{
				var x = i % sampleCount;
				var y = i / sampleCount;
				var dx = (float)x / (sampleCount - 2);
				var dy = (float)y / (sampleCount - 2);
				var elevation = 0f;
				if (!_useTileSkirts)
				{
					elevation = tile.TerrainContainer.QueryHeightData(dx, dy) * tile.TileScale;
				}
				else
				{
					elevation = (x == 0 || y == 0 || x == sampleCount - 1 || y == sampleCount - 1)
						? -_skirtSize
						: tile.TerrainContainer.QueryHeightData(dx, dy) * tile.TileScale;
				}

				vertices[i].Set(vertices[i].x, elevation, vertices[i].z);
			}
			mesh.SetVertices(vertices);
			// Actual displaced bounds are tighter than the min/max-padded fallback bounds
			// set by UnityMapTile.ElevationUpdatedCallback. Mesh normals/tangents are not
			// recalculated: the terrain shader derives the surface normal from the height
			// texture directly in the fragment stage, so the mesh's vertex normals are
			// never read in either mode.
			mesh.RecalculateBounds();
		}

		#region mesh gen
		private MeshDataArray CreateBaseMesh(float tileSize, int sampleCount)
		{
			return
				_useTileSkirts
					? CreateBaseMeshSkirts(tileSize, sampleCount)
					: CreateBaseMeshWithoutSkirts(tileSize, sampleCount);
		}

		private MeshDataArray CreateBaseMeshWithoutSkirts(float size, int sampleCount)
		{
			//TODO use arrays instead of lists
			_newVertexList.Clear();
			_newNormalList.Clear();
			_newUvList.Clear();
			var _newTriangleList = new List<int>();

			//012
			//345
			//678
			for (float y = 0; y < sampleCount; y++)
			{
				var yrat = y / (sampleCount - 1);
				for (float x = 0; x < sampleCount; x++)
				{
					var xrat = x / (sampleCount - 1);

					var xx = Mathf.LerpUnclamped(0, size, xrat);
					//lerp x/y swapped here because of the texture space conversion (y to -y)
					var yy = Mathf.LerpUnclamped(size, 0, yrat);

					var elevation = 0;

					_newVertexList.Add(new Vector3(
						xx,
						elevation,
						-1 * yy));
					_newNormalList.Add(Constants.Math.Vector3Up);
					_newUvList.Add(new Vector2(x * 1f / (sampleCount - 1), (y * 1f / (sampleCount - 1))));
				}
			}

			int vertA, vertB, vertC;

			for (int y = 0; y < sampleCount - 1; y++)
			{
				for (int x = 0; x < sampleCount - 1; x++)
				{
					vertA = (y * sampleCount) + x;
					vertB = (y * sampleCount) + x + sampleCount + 1;
					vertC = (y * sampleCount) + x + sampleCount;
					_newTriangleList.Add(vertA);
					_newTriangleList.Add(vertC);
					_newTriangleList.Add(vertB);

					vertA = (y * sampleCount) + x;
					vertB = (y * sampleCount) + x + 1;
					vertC = (y * sampleCount) + x + sampleCount + 1;
					_newTriangleList.Add(vertA);
					_newTriangleList.Add(vertC);
					_newTriangleList.Add(vertB);
				}
			}

			var mesh = new MeshDataArray();
			mesh.Vertices = _newVertexList.ToArray();
			mesh.Normals = _newNormalList.ToArray();
			mesh.Uvs = _newUvList.ToArray();
			mesh.Triangles.Add(_newTriangleList.ToArray());
			return mesh;
		}

		/// <summary>
		/// Fraction of tile size that the outer skirt ring extends past the tile boundary.
		/// Kept constant so coarse meshes (e.g. <c>sampleCount=2</c> with
		/// <c>SimplificationFactor=64</c>) don't balloon the skirt half-way into neighboring
		/// tiles. 1% is narrow enough to be invisible at typical zoom levels and wide enough
		/// to hide seam artifacts from float-precision mismatches.
		/// </summary>
		private const float SkirtOuterOffsetFraction = 0.01f;

		/// <summary>
		/// Maps a skirt-loop index (<c>-1 .. sideVertexCount-2</c>) to the [0,1] UV range
		/// used for interior vertices, with the outer ring pinned to a fixed offset outside
		/// the tile rather than one grid step. Without this, a 3x3 grid puts the skirt half
		/// a tile outside the edge and overlaps neighboring tiles.
		/// </summary>
		/// <param name="index">Loop index. <c>-1</c> is the left/top outer skirt row; <c>sideVertexCount-2</c> is the right/bottom outer skirt row.</param>
		/// <param name="interiorSteps">Number of grid segments along one tile side (<c>sampleCount</c>).</param>
		/// <param name="sideVertexCount">Total verts per tile axis including skirts.</param>
		private static float RatioForSkirtIndex(int index, int interiorSteps, int sideVertexCount)
		{
			if (index == -1)
			{
				return -SkirtOuterOffsetFraction;
			}
			if (index == sideVertexCount - 2)
			{
				return 1f + SkirtOuterOffsetFraction;
			}
			return (float)index / interiorSteps;
		}

		private MeshDataArray CreateBaseMeshSkirts(float size, int sideVertexCount)
		{
			//TODO use arrays instead of lists
			_newVertexList.Clear();
			_newNormalList.Clear();
			_newUvList.Clear();
			var _newTriangleList = new List<int>();
			var interiorSteps = sideVertexCount - 3;

			//012
			//345
			//678
			for (int y = -1; y < sideVertexCount - 1; y++)
			{
				var yrat = RatioForSkirtIndex(y, interiorSteps, sideVertexCount);
				for (int x = -1; x < sideVertexCount - 1; x++)
				{
					var xrat = RatioForSkirtIndex(x, interiorSteps, sideVertexCount);

					var xx = Mathf.LerpUnclamped(0, size, xrat);
					//lerp x/y swapped here because of the texture space conversion (y to -y)
					var yy = Mathf.LerpUnclamped(size, 0, yrat);

					var elevation = x < 0 || y < 0 || x == sideVertexCount-2 || y == sideVertexCount-2 ? -_skirtSize : 0;

					_newVertexList.Add(new Vector3(
						xx,
						elevation,
						-1 * yy));
					_newNormalList.Add(Constants.Math.Vector3Up);
					_newUvList.Add(new Vector2(xrat, yrat));
					//_newUvList.Add(new Vector2((1f/514) + (xrat * 512)/514, 1 - ((1f/514) + (yrat * 512)/514)));
				}
			}

			int vertA, vertB, vertC;

			var topQuadTris = new List<int>();
			for (int y = 0; y < sideVertexCount - 1; y++)
			{
				for (int x = 0; x < sideVertexCount - 1; x++)
				{
					vertA = (y * sideVertexCount) + x;
					vertB = (y * sideVertexCount) + x + sideVertexCount + 1;
					vertC = (y * sideVertexCount) + x + sideVertexCount;

					if (x == 0 || y == 0 || x == sideVertexCount - 2 || y == sideVertexCount - 2)
					{
						_newTriangleList.Add(vertA);
						_newTriangleList.Add(vertC);
						_newTriangleList.Add(vertB);
					}
					else
					{
						topQuadTris.Add(vertA);
						topQuadTris.Add(vertC);
						topQuadTris.Add(vertB);
					}

					vertA = (y * sideVertexCount) + x;
					vertB = (y * sideVertexCount) + x + 1;
					vertC = (y * sideVertexCount) + x + sideVertexCount + 1;
					
					if (x == 0 || y == 0 || x == sideVertexCount - 2 || y == sideVertexCount - 2)
					{
						_newTriangleList.Add(vertA);
						_newTriangleList.Add(vertC);
						_newTriangleList.Add(vertB);
					}
					else
					{
						topQuadTris.Add(vertA);
						topQuadTris.Add(vertC);
						topQuadTris.Add(vertB);
					}
				}
			}

			var mesh = new MeshDataArray();
			mesh.Vertices = _newVertexList.ToArray();
			mesh.Normals = _newNormalList.ToArray();
			mesh.Uvs = _newUvList.ToArray();
			topQuadTris.AddRange(_newTriangleList);
			mesh.Triangles.Add(topQuadTris.ToArray());
			return mesh;
		}
		#endregion
	}
}
