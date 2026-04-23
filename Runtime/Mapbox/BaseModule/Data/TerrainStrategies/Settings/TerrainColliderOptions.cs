using System;
using Mapbox.BaseModule.Utilities.Attributes;
using UnityEngine;

namespace Mapbox.ImageModule.Terrain.Settings
{
	/// <summary>
	/// Inspector options that control whether each terrain tile gets a MeshCollider. The
	/// collider mesh is generated lazily from the CPU elevation data once it arrives, with
	/// a grid resolution that mirrors the terrain render mesh
	/// (<see cref="ElevationModificationOptions.SimplificationFactor"/>).
	/// </summary>
	[Serializable]
	public class TerrainColliderOptions
	{
		[Tooltip("Attach a MeshCollider to each terrain tile so physics and raycasts can hit the map surface. Forces ExtractCpuElevationData on in the parent module. The collider grid resolution mirrors the render mesh's SimplificationFactor; coarser render = coarser collider. Default: off.")]
		public bool addCollider = false;

		[Tooltip("Put the MeshCollider on a dedicated child GameObject with its own Unity Layer, so physics raycast filtering can be independent of the tile's render layer. Adds one extra GameObject per tile. When off, the collider sits on the tile GameObject and shares its Unity Layer. Default: off.")]
		public bool useDedicatedColliderLayer = false;

		[GameObjectLayer]
		[Tooltip("Unity Layer the child collider GameObject is placed on when Use Dedicated Collider Layer is enabled. Typical pattern: assign a \"Terrain\" layer here and filter your gameplay raycasts to that layer.")]
		public int colliderLayerId = 0;
	}
}
