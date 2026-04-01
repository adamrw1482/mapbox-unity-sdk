// using System.Collections.Generic;
// using System.Linq;
// using Mapbox.BaseModule.Data.Tiles;
// using Mapbox.BaseModule.Map;
// using Mapbox.BaseModule.Utilities;
// using Mapbox.Example.Scripts.ModuleBehaviours;
// using Mapbox.VectorModule;
// using Mapbox.VectorModule.Filters;
// using Mapbox.VectorModule.Unity;
// using UnityEngine;
// using UnityEngine.Serialization;
//
// namespace Mapbox.LandmarkModule
// {
//     public class LandmarkToBuildingFilter : MonoBehaviour
//     {
//         public MapBehaviourCore MapBehaviour;
//         public LandmarksLayerModuleScript LandmarkModuleScript;
//         private LandmarksLayerModule _landmarksLayerModule;
//         public VectorLayerModuleScript VectorModuleScript;
//         public PolygonCollisionFilterObject BuildingFilter;
//
//         private VectorLayerModule _vectorLayerModule;
//         private IMapInformation _mapInformation;
//     
//         //private Dictionary<CanonicalTileId, BuildingData> _buildingsPerTile;
//         private Dictionary<CanonicalTileId, LandmarkData> _landmarksPerTile;
//         
//         void Start()
//         {
//             if (LandmarkModuleScript == null || LandmarkModuleScript.enabled == false) return; 
//         
//             if (MapBehaviour.InitializationStatus == InitializationStatus.Initialized)
//             {
//                 _mapInformation = MapBehaviour.MapboxMap.MapInformation;
//                 _landmarksLayerModule = ((LandmarksLayerModule)LandmarkModuleScript.ModuleImplementation);
//                 if (_landmarksLayerModule != null)
//                 {
//                     //_landmarksLayerModule.LandmarkMeshResults += LandmarkMeshResults;
//                     _landmarksLayerModule.LandmarkFootprintCreated += LandmarkMeshResults;
//                     _vectorLayerModule = (VectorLayerModule)VectorModuleScript.ModuleImplementation;
//                 }
//             }
//             else
//             {
//                 MapBehaviour.Initialized += map =>
//                 {
//                     _mapInformation = map.MapInformation;
//                     _landmarksLayerModule = ((LandmarksLayerModule)LandmarkModuleScript.ModuleImplementation);
//                     if (_landmarksLayerModule != null)
//                     {
//                         //_landmarksLayerModule.LandmarkMeshResults += LandmarkMeshResults;
//                         _landmarksLayerModule.LandmarkFootprintCreated += LandmarkMeshResults;
//                         _vectorLayerModule = (VectorLayerModule)VectorModuleScript.ModuleImplementation;
//                     }
//                 };
//             }
//         }
//
//         private void LandmarkMeshResults(CanonicalTileId id, List<Vector3[]> polygons)
//         {
//             foreach (var polygon in polygons)
//             {
//                 BuildingFilter.AddMeshCollider8192(polygon.ToList(), id);    
//             }
//         }
//
//         private IEnumerable<CanonicalTileId> AddLandmarkData(CanonicalTileId id, List<Vector3[]> polygons)
//         {
//             var changedTiles = new HashSet<CanonicalTileId>();
//             foreach (var polygon in polygons)
//             {
//                 var minX = float.MaxValue;
//                 var maxX = float.MinValue;
//                 var minZ = float.MaxValue;
//                 var maxZ = float.MinValue;
//                 for (var i = 0; i < polygon.Length; i++)
//                 {
//                     if (polygon[i].x < minX) minX = polygon[i].x;
//                     if (polygon[i].x > maxX) maxX = polygon[i].x;
//                     if (polygon[i].z < minZ) minZ = polygon[i].z;
//                     if (polygon[i].z > maxZ) maxZ = polygon[i].z;
//                 }
//
//                 var bb = new Vector3[4]
//                 {
//                     new Vector3(minX, 0, minZ),
//                     new Vector3(maxX, 0, minZ),
//                     new Vector3(minX, 0, maxZ),
//                     new Vector3(maxX, 0, maxZ)
//                 };
//
//                 if (!_landmarksPerTile.ContainsKey(id)) _landmarksPerTile.Add(id, new LandmarkData(id));
//                 _landmarksPerTile[id].BoundingBoxes.Add(bb);
//                 _landmarksPerTile[id].Polygons.Add(polygon);
//                 changedTiles.Add(id);
//
//                 CanonicalTileId neighbourId;
//                 if (minX < 0)
//                 {
//                     neighbourId = new CanonicalTileId(id.Z, id.X - 1, id.Y);
//                     AddPolyTo(id, neighbourId, polygon, bb);
//                     changedTiles.Add(neighbourId);
//                 }
//
//                 if (maxX > 1)
//                 {
//                     neighbourId = new CanonicalTileId(id.Z, id.X + 1, id.Y);
//                     AddPolyTo(id, neighbourId, polygon, bb);
//                     changedTiles.Add(neighbourId);
//                 }
//
//                 if (maxZ > 0)
//                 {
//                     neighbourId = new CanonicalTileId(id.Z, id.X, id.Y - 1);
//                     AddPolyTo(id, neighbourId, polygon, bb);
//                     changedTiles.Add(neighbourId);
//                 }
//
//                 if (minZ < -1)
//                 {
//                     neighbourId = new CanonicalTileId(id.Z, id.X, id.Y + 1);
//                     AddPolyTo(id, neighbourId, polygon, bb);
//                     changedTiles.Add(neighbourId);
//                 }
//             }
//
//             return changedTiles;
//         }
//
//         private void AddPolyTo(CanonicalTileId originalId, CanonicalTileId neighbourId, Vector3[] polygon, Vector3[] bb)
//         {
//             var dif = new Vector3(neighbourId.X - originalId.X, 0, neighbourId.Y - originalId.Y);
//             var newPoly = new Vector3[polygon.Length];
//             for (var i = 0; i < polygon.Length; i++)
//             {
//                 newPoly[i] = new Vector3(polygon[i].x - dif.x, polygon[i].y, polygon[i].z + dif.z);
//             }
//
//             var newBB = new Vector3[bb.Length];
//             for (var i = 0; i < bb.Length; i++)
//             {
//                 newBB[i] = new Vector3(bb[i].x - dif.x, bb[i].y, bb[i].z + dif.z);
//             }
//
//             if (!_landmarksPerTile.ContainsKey(neighbourId))
//                 _landmarksPerTile.Add(neighbourId, new LandmarkData(neighbourId));
//             _landmarksPerTile[neighbourId].BoundingBoxes.Add(newPoly);
//             _landmarksPerTile[neighbourId].Polygons.Add(newBB);
//         }
//
//         private void LandmarkMeshResults(CanonicalTileId id, List<Mesh> meshes)
//         {
//             var tilesToUpdate = new HashSet<CanonicalTileId>();
//             foreach (var mesh in meshes)
//             {
//                 mesh.RecalculateBounds();
//                 var bounds = mesh.bounds;
//                 var rootBounds = Conversions.TileBoundsInUnitySpace(id, MapBehaviour.MapInformation.CenterMercator, MapBehaviour.MapInformation.Scale);
//                 bounds = new Bounds((bounds.center * (float)rootBounds.Size.x) + rootBounds.TopLeft.ToVector3xz(), bounds.size * (float)rootBounds.Size.x);
//             
//                 for (int x = -1; x <= 1; x++)
//                 {
//                     for (int y = -1; y <= 1; y++)
//                     {
//                         var tileId = new CanonicalTileId(id.Z, id.X + x, id.Y + y);
//                         var parentBounds = Conversions.TileBoundsInUnitySpace(tileId, MapBehaviour.MapInformation.CenterMercator, MapBehaviour.MapInformation.Scale);
//                         var parentBound = new Bounds(parentBounds.Center.ToVector3xz(), parentBounds.Size.ToVector3xz());
//                         if (!bounds.Intersects(parentBound))
//                         {
//                             continue;
//                         }
//
//                         for (int i = 0; i < 4; i++)
//                         {
//                             var child = tileId.Quadrant(i);
//                             var tileBounds = Conversions.TileBoundsInUnitySpace(child, MapBehaviour.MapInformation.CenterMercator, MapBehaviour.MapInformation.Scale);
//                             var tileBound = new Bounds(tileBounds.Center.ToVector3xz(), tileBounds.Size.ToVector3xz());
//                             if (bounds.Intersects(tileBound))
//                             {
//                                 tilesToUpdate.Add(child);
//                                 var scaleOffset = child.CalculateScaleOffsetAtZoom(tileId.Z);
//                                 var vertices = new List<Vector3>();
//                                 foreach (var vertex in mesh.vertices)
//                                 {
//                                     var newX = (vertex.x - scaleOffset[2] - x) / scaleOffset[0];
//                                     var newY = (vertex.z + scaleOffset[3] + y) / scaleOffset[1];
//                                     vertices.Add(new Vector3(newX, 0, newY));
//                                 }
//
//                                 BuildingFilter?.AddMeshCollider8192(vertices, child);
//                             }
//                         }
//                     }
//                 }
//             }
//
//             foreach (var tileId in tilesToUpdate)
//             {
//                 _vectorLayerModule.ReloadTile(tileId);
//             }
//         }
//         
//         private void OnDestroy()
//         {
//             if (_landmarksLayerModule != null)
//             {
//                 _landmarksLayerModule.LandmarkMeshResults -= LandmarkMeshResults;
//             }
//         }
//         
//         private class LandmarkData
//         {
//             public CanonicalTileId TileId;
//             public List<Vector3[]> BoundingBoxes;
//             public List<Vector3[]> Polygons;
//             public LandmarkData(CanonicalTileId id)
//             {
//                 TileId = id;
//                 BoundingBoxes = new List<Vector3[]>();
//                 Polygons = new List<Vector3[]>();
//             }
//         }
//     }
// }
