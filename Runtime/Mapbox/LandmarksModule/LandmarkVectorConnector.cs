// using System;
// using System.Collections;
// using System.Collections.Generic;
// using System.Numerics;
// using Mapbox.BaseModule.Data.Tiles;
// using Mapbox.BaseModule.Map;
// using Mapbox.BaseModule.Utilities;
// using Mapbox.Example.Scripts.ModuleBehaviours;
// using Mapbox.LandmarkModule;
// using Mapbox.VectorModule;
// using Mapbox.VectorModule.Filters;
// using Mapbox.VectorModule.Unity;
// using UnityEngine;
// using UnityEngine.XR;
// using Vector2 = System.Numerics.Vector2;
// using Vector3 = UnityEngine.Vector3;
// using Vector4 = UnityEngine.Vector4;
//
// public class LandmarkVectorConnector : MonoBehaviour
// {
//     public MapBehaviourCore MapBehaviour;
//     public LandmarksLayerModuleScript LandmarkModuleScript;
//     private LandmarksLayerModule _landmarksLayerModule;
//     public VectorLayerModuleScript VectorModuleScript;
//
//     private VectorLayerModule _vectorLayerModule;
//     private IMapInformation _mapInformation;
//
//     private Dictionary<CanonicalTileId, BuildingData> _buildingsPerTile;
//     private Dictionary<CanonicalTileId, LandmarkData> _landmarksPerTile;
//     
//     void Start()
//     {
//         _landmarksPerTile = new Dictionary<CanonicalTileId, LandmarkData>();
//         _buildingsPerTile = new Dictionary<CanonicalTileId, BuildingData>();
//         
//         if (LandmarkModuleScript == null || LandmarkModuleScript.enabled == false) return; 
//         
//         if (MapBehaviour.InitializationStatus == InitializationStatus.Initialized)
//         {
//             if (LandmarkModuleScript == null || LandmarkModuleScript.ModuleImplementation == null || VectorModuleScript == null || VectorModuleScript.ModuleImplementation == null)
//             {
//                 this.enabled = false;
//                 return;
//             }
//             
//             _mapInformation = MapBehaviour.MapboxMap.MapInformation;
//             _landmarksLayerModule = ((LandmarksLayerModule)LandmarkModuleScript.ModuleImplementation);
//             if (_landmarksLayerModule != null)
//             {
//                 _landmarksLayerModule.LandmarkFootprintCreated += LandmarkFootprintResults;
//                 _vectorLayerModule = (VectorLayerModule)VectorModuleScript.ModuleImplementation;
//                 if(_vectorLayerModule.TryGetLayerVisualizer("building", out var buildingLayer))
//                 {
//                     (buildingLayer as BuildingLayerVisualizer).OnBuildingMeshCreated += (id, o, arg3) =>  OnBuildingMeshCreated(id, o, arg3);
//                 }
//             }
//         }
//         else
//         {
//             MapBehaviour.Initialized += map =>
//             {
//                 if (LandmarkModuleScript == null || LandmarkModuleScript.ModuleImplementation == null || VectorModuleScript == null || VectorModuleScript.ModuleImplementation == null)
//                 {
//                     this.enabled = false;
//                     return;
//                 }
//                 
//                 _mapInformation = map.MapInformation;
//                 _landmarksLayerModule = ((LandmarksLayerModule)LandmarkModuleScript.ModuleImplementation);
//                 if (_landmarksLayerModule != null)
//                 {
//                     _landmarksLayerModule.LandmarkFootprintCreated += LandmarkFootprintResults;
//                     _vectorLayerModule = (VectorLayerModule)VectorModuleScript.ModuleImplementation;
//                     if(_vectorLayerModule.TryGetLayerVisualizer("building", out var buildingLayer))
//                     {
//                         (buildingLayer as BuildingLayerVisualizer).OnBuildingMeshCreated += (id, o, arg3) =>  OnBuildingMeshCreated(id, o, arg3);
//                     }
//                 }
//             };
//         }
//     }
//
//     private void OnBuildingMeshCreated(CanonicalTileId tileId, GameObject go, HardcoreMeshData meshData)
//     {
//         var buildingData = new BuildingData() { TileId = tileId, GameObject = go, MeshData = meshData };
//         _buildingsPerTile.TryAdd(tileId, buildingData);
//
//         var buildingA tZ14 = tileId.ParentAt(14);
//         if (_landmarksPerTile.TryGetValue(buildingAtZ14, out var landmarkData))
//         {
//             HandleCollision(buildingData, landmarkData);
//         }
//     }
//     
//     private void LandmarkFootprintResults(CanonicalTileId id, List<Vector3[]> polygons)
//     {
//         var tiles = AddLandmarkData(id, polygons);
//         
//         foreach (var tile in tiles)
//         {
//             var landmarkData = _landmarksPerTile[tile];
//             foreach (var tileId in AllChildrenOf14(landmarkData.TileId))
//             {
//                 if (_buildingsPerTile.TryGetValue(tileId, out var buildingData))
//                 {
//                     HandleCollision(buildingData, landmarkData);
//                 }
//             }
//         }
//     }
//
//     private IEnumerable<CanonicalTileId> AllChildrenOf14(CanonicalTileId tileId)
//     {
//         for (int i = 0; i < 4; i++)
//         {
//             var child = tileId.Quadrant(i);
//             yield return child;
//
//             for (int j = 0; j < 4; j++)
//             {
//                 var grandchild = child.Quadrant(j);
//                 yield return grandchild;
//             }
//         }
//     }
//
//     private void HandleCollision(BuildingData buildingData, LandmarkData landmarkData)
//     {
//         var meshData = buildingData.MeshData;
//         for (var i = 0; i < meshData.MeshInfo.vertexRanges.Length; i++)
//         {
//             var vertexSpan = meshData.Vertices.AsSpan(meshData.MeshInfo.vertexRanges[i], meshData.MeshInfo.VertexCount(i));
//             var scaleOffsetForBuilding = buildingData.TileId.CalculateTopLeftScaleOffsetAtZoom(landmarkData.TileId.Z);
//             var featureBox = BoundsOfVertices(vertexSpan.Slice(0, vertexSpan.Length / 5));
//             DrawBounds(landmarkData.TileId, featureBox, Color.black, 10);
//             for (var landmarkIndex = 0; landmarkIndex < landmarkData.BoundingBoxes.Count; landmarkIndex++)
//             {
//                 var landmarkBox = BoundsOfVertices(landmarkData.BoundingBoxes[landmarkIndex]);
//                 if (landmarkBox.Intersects(featureBox))
//                 {
//                     DrawBounds(landmarkData.TileId, landmarkBox, Color.blue, 10);
//                     DrawBounds(landmarkData.TileId, featureBox, Color.red, 10);
//                     DrawFootprint(landmarkData.TileId, landmarkData.Polygons[landmarkIndex], Color.green, 10);
//                     //DrawFootprint(landmarkData.TileId, vertexSpan.Slice(0, vertexSpan.Length / 5), Color.magenta, 10);
//                     
//                     // for (int j = 0; j < vertexSpan.Length; j++)
//                     // {
//                     //     vertexSpan[j] = Vector3.zero;
//                     // }
//                     
//                     if (PolygonIntersection2D.ArePolygonsIntersecting(landmarkData.Polygons[landmarkIndex], vertexSpan.Slice(0, vertexSpan.Length / 5)))
//                     {
//                         //DrawBounds(landmarkData.TileId, featureBox, Color.red, 1000);
//                         DrawFootprint(landmarkData.TileId, landmarkData.Polygons[landmarkIndex], Color.magenta, 1000);
//                         DrawFootprint(landmarkData.TileId, vertexSpan.Slice(0, vertexSpan.Length / 5), Color.green, 1000);
//                         
//                         for (int j = 0; j < vertexSpan.Length; j++)
//                         {
//                             vertexSpan[j] = Vector3.zero;
//                         }
//                     
//                         //buildingData.GameObject.GetComponent<MeshFilter>().mesh.SetVertices(meshData.Vertices);
//                     }
//                 }
//             }
//         }
//         
//         buildingData.GameObject.GetComponent<MeshFilter>().mesh.SetVertices(meshData.Vertices);
//     }
//
//     private IEnumerable<CanonicalTileId> AddLandmarkData(CanonicalTileId id, List<Vector3[]> polygons)
//     {
//         var changedTiles = new HashSet<CanonicalTileId>();
//         foreach (var polygon in polygons)
//         {
//             var minX = float.MaxValue;
//             var maxX = float.MinValue;
//             var minZ = float.MaxValue;
//             var maxZ = float.MinValue;
//             for (var i = 0; i < polygon.Length; i++)
//             {
//                 if(polygon[i].x < minX) minX = polygon[i].x;
//                 if(polygon[i].x > maxX) maxX = polygon[i].x;
//                 if(polygon[i].z < minZ) minZ = polygon[i].z;
//                 if(polygon[i].z > maxZ) maxZ = polygon[i].z;
//             }
//
//             var bb = new Vector3[4]
//             {
//                 new Vector3(minX, 0, minZ),
//                 new Vector3(maxX, 0, minZ),
//                 new Vector3(minX, 0, maxZ),
//                 new Vector3(maxX, 0, maxZ)
//             };
//             
//             if(!_landmarksPerTile.ContainsKey(id)) _landmarksPerTile.Add(id, new LandmarkData(id));
//             _landmarksPerTile[id].BoundingBoxes.Add(bb);
//             _landmarksPerTile[id].Polygons.Add(polygon);
//             changedTiles.Add(id);
//
//             CanonicalTileId neighbourId;
//             if (minX < 0)
//             {
//                 neighbourId = new CanonicalTileId(id.Z, id.X - 1, id.Y);
//                 AddPolyTo(id, neighbourId, polygon, bb);
//                 changedTiles.Add(neighbourId);
//             }
//
//             if (maxX > 1)
//             {
//                 neighbourId = new CanonicalTileId(id.Z, id.X + 1, id.Y);
//                 AddPolyTo(id, neighbourId, polygon, bb);
//                 changedTiles.Add(neighbourId);
//             }
//
//             if (maxZ > 0)
//             {
//                 neighbourId = new CanonicalTileId(id.Z, id.X, id.Y - 1);
//                 AddPolyTo(id, neighbourId, polygon, bb);
//                 changedTiles.Add(neighbourId);
//             }
//
//             if (minZ < -1)
//             {
//                 neighbourId = new CanonicalTileId(id.Z, id.X, id.Y + 1);
//                 AddPolyTo(id, neighbourId, polygon, bb);
//                 changedTiles.Add(neighbourId);
//             }
//         }
//
//         return changedTiles;
//     }
//
//     private void AddPolyTo(CanonicalTileId originalId, CanonicalTileId neighbourId, Vector3[] polygon, Vector3[] bb)
//     {
//         var dif = new Vector3(neighbourId.X - originalId.X, 0, neighbourId.Y - originalId.Y);
//         var newPoly = new Vector3[polygon.Length];
//         for (var i = 0; i < polygon.Length; i++)
//         {
//             newPoly[i] = new Vector3(polygon[i].x - dif.x, polygon[i].y, polygon[i].z + dif.z);
//         }
//
//         var newBB = new Vector3[bb.Length];
//         for (var i = 0; i < bb.Length; i++)
//         {
//             newBB[i] = new Vector3(bb[i].x - dif.x, bb[i].y, bb[i].z + dif.z);
//         }
//             
//         if(!_landmarksPerTile.ContainsKey(neighbourId)) _landmarksPerTile.Add(neighbourId, new LandmarkData(neighbourId));
//         _landmarksPerTile[neighbourId].BoundingBoxes.Add(newPoly);
//         _landmarksPerTile[neighbourId].Polygons.Add(newBB);
//     }
//
//
//     private Bounds BoundsOfVertices(Span<Vector3> vertices, Vector4 scaleOffset)
//     {
//         var minX = float.MaxValue;
//         var minY = float.MaxValue;
//         var maxX = float.MinValue;
//         var maxY = float.MinValue;
//             
//         foreach (var vertex in vertices)
//         {
//             if(vertex.x < minX) minX = vertex.x;
//             if(vertex.x > maxX) maxX = vertex.x;
//             if(vertex.z < minY) minY = vertex.z;
//             if(vertex.z > maxY) maxY = vertex.z;
//         }
//
//         var centerX = (maxX + minX) / 2;
//         var centerY = (maxY + minY) / 2;
//         var sizeX = maxX - minX;
//         var sizeY = maxY - minY;
//         
//         var center = new Vector3(centerX * scaleOffset[0] + scaleOffset[2], 0, centerY * scaleOffset[1] - scaleOffset[3]);
//         var size = new Vector3(sizeX * scaleOffset[0], 1, sizeY * scaleOffset[1]);
//         return new Bounds(center, size);
//     }
//     
//     private Bounds BoundsOfVertices(Span<Vector3> vertices)
//     {
//         var minX = float.MaxValue;
//         var minY = float.MaxValue;
//         var maxX = float.MinValue;
//         var maxY = float.MinValue;
//             
//         foreach (var vertex in vertices)
//         {
//             if(vertex.x < minX) minX = vertex.x;
//             if(vertex.x > maxX) maxX = vertex.x;
//             if(vertex.z < minY) minY = vertex.z;
//             if(vertex.z > maxY) maxY = vertex.z;
//         }
//
//         var center = new Vector3((maxX + minX) / 2, 0, (maxY + minY) / 2);
//         var size = new Vector3(maxX - minX, 1, maxY - minY);
//         return new Bounds(center, size);
//     }
//     
//     private Bounds BoundsOfVertices(List<Vector3> vertices)
//     {
//         var minX = float.MaxValue;
//         var minY = float.MaxValue;
//         var maxX = float.MinValue;
//         var maxY = float.MinValue;
//             
//         foreach (var vertex in vertices)
//         {
//             if(vertex.x < minX) minX = vertex.x;
//             if(vertex.x > maxX) maxX = vertex.x;
//             if(vertex.z < minY) minY = vertex.z;
//             if(vertex.z > maxY) maxY = vertex.z;
//         }
//
//         var center = new Vector3((maxX + minX) / 2, 0, (maxY + minY) / 2);
//         var size = new Vector3(maxX - minX, 1, maxY - minY);
//         return new Bounds(center, size);
//     }
//
//     private void DrawBounds(Bounds b, Color color, float delay=0)
//     {
//         // bottom
//         var p1 = new Vector3(b.min.x, b.min.y, b.min.z);
//         var p2 = new Vector3(b.max.x, b.min.y, b.min.z);
//         var p3 = new Vector3(b.max.x, b.min.y, b.max.z);
//         var p4 = new Vector3(b.min.x, b.min.y, b.max.z);
//
//         Debug.DrawLine(p1, p2, color, delay);
//         Debug.DrawLine(p2, p3, color, delay);
//         Debug.DrawLine(p3, p4, color, delay);
//         Debug.DrawLine(p4, p1, color, delay);
//
//         // top
//         var p5 = new Vector3(b.min.x, b.max.y, b.min.z);
//         var p6 = new Vector3(b.max.x, b.max.y, b.min.z);
//         var p7 = new Vector3(b.max.x, b.max.y, b.max.z);
//         var p8 = new Vector3(b.min.x, b.max.y, b.max.z);
//
//         Debug.DrawLine(p5, p6, color, delay);
//         Debug.DrawLine(p6, p7, color, delay);
//         Debug.DrawLine(p7, p8, color, delay);
//         Debug.DrawLine(p8, p5, color, delay);
//
//         // sides
//         Debug.DrawLine(p1, p5, color, delay);
//         Debug.DrawLine(p2, p6, color, delay);
//         Debug.DrawLine(p3, p7, color, delay);
//         Debug.DrawLine(p4, p8, color, delay);
//     }
//
//     private void DrawBounds(CanonicalTileId spaceTile, Bounds b, Color color, int delay)
//     {
//         var rootBounds = Conversions.BoundsInUnitySpace(spaceTile, MapBehaviour.MapInformation.CenterMercator, MapBehaviour.MapInformation.Scale);
//         // bottom
//         var p1 = new Vector3(
//             Mathf.LerpUnclamped(rootBounds.min.x, rootBounds.max.x, b.min.x), 
//             0, 
//             Mathf.LerpUnclamped(rootBounds.max.z, rootBounds.min.z, Math.Abs(b.min.z)));
//         var p2 = new Vector3(
//             Mathf.LerpUnclamped(rootBounds.min.x, rootBounds.max.x, b.max.x), 
//             0, 
//             Mathf.LerpUnclamped(rootBounds.max.z, rootBounds.min.z, Math.Abs(b.min.z)));
//         var p3 = new Vector3(
//             Mathf.LerpUnclamped(rootBounds.min.x, rootBounds.max.x, b.max.x), 
//             0, 
//             Mathf.LerpUnclamped(rootBounds.max.z, rootBounds.min.z, Math.Abs(b.max.z)));
//         var p4 = new Vector3(
//             Mathf.LerpUnclamped(rootBounds.min.x, rootBounds.max.x, b.min.x), 
//             0, 
//             Mathf.LerpUnclamped(rootBounds.max.z, rootBounds.min.z, Math.Abs(b.max.z)));
//
//         Debug.DrawLine(p1, p2, color, delay);
//         Debug.DrawLine(p2, p3, color, delay);
//         Debug.DrawLine(p3, p4, color, delay);
//         Debug.DrawLine(p4, p1, color, delay);
//     }
//     
//     private void DrawBounds(Vector3 first, Vector3 second, Vector3 third, Vector3 fourth, Color color, float delay = 0)
//     {
//         Debug.DrawLine(first, second, color, delay);
//         Debug.DrawLine(second, fourth, color, delay);
//         Debug.DrawLine(fourth, third, color, delay);
//         Debug.DrawLine(third, first, color, delay);
//     }
//     
//     private void DrawFootprint(CanonicalTileId spaceTile, Span<Vector3> vertexSpan, Color color, int duration)
//     {
//         var rootBounds = Conversions.BoundsInUnitySpace(spaceTile, MapBehaviour.MapInformation.CenterMercator, MapBehaviour.MapInformation.Scale);
//         
//         for (var i = 0; i < vertexSpan.Length; i++)
//         {
//             var v1 = new Vector3(
//                 Mathf.LerpUnclamped(rootBounds.min.x, rootBounds.max.x, vertexSpan[i].x), 
//                 0,
//                 Mathf.LerpUnclamped(rootBounds.max.z, rootBounds.min.z, Mathf.Abs(vertexSpan[i].z)));
//             var v2 = new Vector3(
//                 Mathf.LerpUnclamped(rootBounds.min.x, rootBounds.max.x, vertexSpan[(i + 1) % vertexSpan.Length].x), 
//                 0,
//                 Mathf.LerpUnclamped(rootBounds.max.z, rootBounds.min.z, Mathf.Abs(vertexSpan[(i + 1) % vertexSpan.Length].z)));
//             Debug.DrawLine(v1, v2, color, duration);
//         }
//     }
//
//     private class BuildingData
//     {
//         public CanonicalTileId TileId;
//         public GameObject GameObject;
//         public HardcoreMeshData MeshData;
//     }
//     
//     private class LandmarkData
//     {
//         public CanonicalTileId TileId;
//         public List<Vector3[]> BoundingBoxes;
//         public List<Vector3[]> Polygons;
//
//         public LandmarkData(CanonicalTileId id)
//         {
//             TileId = id;
//             BoundingBoxes = new List<Vector3[]>();
//             Polygons = new List<Vector3[]>();
//         }
//     }
// }
