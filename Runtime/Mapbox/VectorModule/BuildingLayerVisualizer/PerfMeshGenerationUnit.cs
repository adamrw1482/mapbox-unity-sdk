// using System;
// using System.Collections.Generic;
// using Mapbox.BaseModule.Data.DataFetchers;
// using Mapbox.BaseModule.Data.Tasks;
// using Mapbox.BaseModule.Unity;
// using Mapbox.BaseModule.Utilities;
// using Mapbox.VectorModule.MeshGeneration;
// using UnityEngine;
//
// namespace Mapbox.VectorModule.BuildingLayerVisualizer
// {
// 	public class PerfMeshGenerationUnit : MeshGenerationUnit
//     {
//         public PerfMeshGenerationUnit(UnityContext unityContext) : base(unityContext)
//         {
//         }
//         
//         public override void MeshGeneration(VectorData data, Action<MeshGenerationTaskResult> callback)
//         {
//             if (data.Data == null)
//             {
//                 callback(new MeshGenerationTaskResult(TaskResultType.Success));
//                 return;
//             }
//             
//             BuildingMeshGenTask(data, callback);
//         }
//         
//         private void BuildingMeshGenTask(VectorData data, Action<MeshGenerationTaskResult> callback)
//         {
//             var meshTask = new MeshGenTaskWrapper<BuildingLayerTaskResult>()
//             {
//                 TileId = data.TileId,
//                 DataAction = () =>
//                 {
//                     var result = new BuildingLayerTaskResult() { Data = data };
//                     result.MeshData = new List<BuildingLayerDataResult>();
//                     var decompressed = Compression.Decompress(data.Data);
//                     var processedTile = new PerfVectorTile(decompressed);
//                     try
//                     {
//                         foreach (var vectorLayerVisualizer in _LayerVisualizers)
//                         {
//                             var visualizer = (BuildingLayerVisualizer) vectorLayerVisualizer;
//                             if (visualizer == null || !visualizer.Active || visualizer.ContainsVisualFor(data.TileId))
//                                 continue;
//
//                             if (processedTile.TryGetLayer(visualizer.VectorLayerName, out var layer))
//                             {
//                                 var layerData = visualizer.CreateMesh(data.TileId, layer);
//                                 result.MeshData.Add(new BuildingLayerDataResult()
//                                 {
//                                     LayerName = visualizer.VectorLayerName,
//                                     MeshData = layerData
//                                 });
//                             }
//                         }
//                     }
//                     catch (Exception e)
//                     {
//                         result.ResultType = TaskResultType.MeshGenerationFailure;
//                         result.AddException(e);
//                         return result;
//                     }
//                         
//                     result.ResultType = TaskResultType.Success;
//                     return result;
//                 },
//                 DataCompleted = (task, taskResult) => //task may be null
//                 {
//                     if (!_isActive)
//                         return;
//
//                     _activeTasks.Remove(data.TileId);
//
//                     if (taskResult.ResultType == TaskResultType.MeshGenerationFailure)
//                     {
//                         var failResult = new MeshGenerationTaskResult(taskResult.ResultType);
//                         foreach (var e in taskResult.GetExceptions())
//                         {
//                             failResult.AddException(e);
//                         }
//
//                         //Debug.Log(string.Format("{0} mesh gen exception: {1}", data.TileId, task.Exception.Message));
//                         failResult.AddException(new Exception(string.Format("{0} mesh gen exception: {1}",
//                             taskResult.Data.TileId, taskResult.ExceptionsAsString)));
//                         callback(failResult);
//                         return;
//                     }
//                     else if (taskResult.ResultType == TaskResultType.Cancelled)
//                     {
//                         var failResult = new MeshGenerationTaskResult(TaskResultType.Cancelled);
//                         callback(failResult);
//                         return;
//                     }
//
//                     var resultGameObjects = new List<GameObject>();
//                     foreach (var layerData in taskResult.MeshData)
//                     {
//                         foreach (var layerVisualizer in TryGetValue<BuildingLayerVisualizer>(layerData.LayerName))
//                         {
//                             var layerGameObjects = layerVisualizer.CreateGo(taskResult.Data.TileId, layerData.MeshData);
//                             foreach (var gameObject in layerGameObjects)
//                             {
//                                 gameObject.SetActive(true);
//                                 gameObject.name = taskResult.Data.TileId.ToString();
//                                 resultGameObjects.Add(gameObject);
//                             }
//                         }
//                     }
//
//                     callback(new MeshGenerationTaskResult(TaskResultType.Success, resultGameObjects));
//
//                 }
//             };
//             if(!_activeTasks.ContainsKey(data.TileId)) _activeTasks.Add(data.TileId, new List<TaskWrapper>());
//             _activeTasks[data.TileId].Add(meshTask);
//             _unityContext.TaskManager.AddTask(meshTask, 0);
//         }
//     }
//     
//     public class BuildingLayerTaskResult : MeshGenTaskWrapperResult
//     {
//         public List<BuildingLayerDataResult> MeshData;
//     }
//
//     public class BuildingLayerDataResult
//     {
//         public string LayerName;
//         public HardcoreMeshData MeshData;
//     }
// }