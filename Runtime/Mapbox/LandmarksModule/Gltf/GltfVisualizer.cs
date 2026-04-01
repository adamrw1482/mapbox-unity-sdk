using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GLTFast;
using GLTFast.Logging;
using Mapbox.BaseModule.Data.DataFetchers;
using Mapbox.BaseModule.Data.Tasks;
using Mapbox.BaseModule.Data.Tiles;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Unity;
using Mapbox.BaseModule.Utilities;
using Mapbox.LandmarksModule;
using UnityEngine;
using Material = UnityEngine.Material;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

namespace Mapbox.LandmarkModule
{
    public class GltfVisualizer
    {
        // public Action<CanonicalTileId, List<Mesh>> LandmarkMeshResults = (id, results) => { };
        // public Action<CanonicalTileId, List<Vector3[]>> LandmarkFootprintCreated = (id, results) => { };
        
        private Material _baseMaterial;
        private Dictionary<CanonicalTileId, TaskWrapper> _activeTasks;
        private ObjectPool<GameObject> _objectPool;
        private Transform _gltfRoot;
        private IMapInformation _mapInformation;
        

        public bool IsInWork(CanonicalTileId tileId) { return _activeTasks.ContainsKey(tileId); }
		
        public GltfVisualizer(UnityContext unityContext, IMapInformation mapInformation, Material baseMaterial)
        {
            _mapInformation = mapInformation;
            _baseMaterial = baseMaterial;
            _activeTasks = new Dictionary<CanonicalTileId, TaskWrapper>();
            _objectPool = new ObjectPool<GameObject>(PoolObjectGenerator);
            var go = new GameObject("gltf root");
            go.transform.SetParent(unityContext.RuntimeGenerationRoot);
            _gltfRoot = go.transform;
        }
        
        public IEnumerator Initialize()
        {
            return _objectPool.InitializeItems(20);
        }
		
        public async Task Generate(BuildingData vectorData, Action<GltfGenerationTaskResult> callback)
        {
            if (_activeTasks.ContainsKey(vectorData.TileId))
            {
                return;
            }
			
            if (vectorData.Data == null || vectorData.Data.Length == 0)
            {
                callback(new GltfGenerationTaskResult(TaskResultType.Success, Enumerable.Empty<GameObject>()));
                return;
            }

            _activeTasks.Add(vectorData.TileId, null);

            var gltf = new MapboxGltfImport(null, null, new MapboxGltfMaterialGenerator(_baseMaterial), new ConsoleLogger());
            var buildingRoot = _objectPool.GetObject(); //new GameObject("building " + vectorData.TileId);
            
            var gameObjectInstantiator = new MapboxGltfInstantiator(gltf, _mapInformation, buildingRoot.transform);
            
            await gltf.Load(vectorData.Data).ContinueWith(async (t) =>
            {
                if (t.Status == TaskStatus.Faulted)
                {
                    _activeTasks.Remove(vectorData.TileId);
                    callback(new GltfGenerationTaskResult(TaskResultType.DataProcessingFailure, Enumerable.Empty<GameObject>()));
                    return;
                }
                var success = await gltf.InstantiateSceneAsync(gameObjectInstantiator, 0);
                if (success)
                {
                    
                    _activeTasks.Remove(vectorData.TileId);
                    //LandmarkMeshResults(vectorData.TileId, footPrints);
                    callback(new GltfGenerationTaskResult(TaskResultType.Success, new List<GameObject>() {buildingRoot}, gameObjectInstantiator.FootprintResults));
                }
                else
                {
                    _activeTasks.Remove(vectorData.TileId);
                    callback(new GltfGenerationTaskResult(TaskResultType.GameObjectFailure, Enumerable.Empty<GameObject>()));
                }
                
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }
        
        

        private GameObject PoolObjectGenerator()
        {
            var go = new GameObject("gltf visualizer");
            go.transform.SetParent(_gltfRoot);
            return go;
        }
        
        private class MapboxGltfInstantiator : GameObjectInstantiator
        {
            public List<Vector3[]> FootprintResults = new List<Vector3[]>();
            private MapboxGltfImport _gltf;
            private IMapInformation _mapInformation;
            
            public MapboxGltfInstantiator(MapboxGltfImport gltf, IMapInformation mapInformation, Transform parent, ICodeLogger logger = null, InstantiationSettings settings = null) : base(gltf, parent, logger, settings)
            {
                _gltf = gltf;
                _mapInformation = mapInformation;
            }

            public override void AddPrimitive(uint nodeIndex, string meshName, MeshResult meshResult, uint[] joints = null, uint? rootJoint = null,
                float[] morphTargetWeights = null, int meshNumeration = 0)
            {
                base.AddPrimitive(nodeIndex, meshName, meshResult, joints, rootJoint, morphTargetWeights, meshNumeration);
                var node = _gltf.GetSourceRoot().nodes[nodeIndex];
            
                FlipYZ(node.matrix, meshResult);
            }

            public override void EndScene(uint[] rootNodeIndices)
            {
                FootprintResults.Clear();
                var sourceNode = _gltf.GetSourceRoot();
                //var footPrints = new List<Vector3[]>();
                
                if (rootNodeIndices != null)
                {
                    foreach (var nodeIndex in rootNodeIndices)
                    {
                        var node = sourceNode.nodes[nodeIndex];
                        var isFootprint = IsFootprint(node);
                        var go = m_Nodes[nodeIndex];
                        go.SetActive(!isFootprint);
                        if (isFootprint)
                        {
                            FootprintResults.Add(_gltf.GetMesh(node.mesh, 0).vertices);
                        }
                        else
                        {
                            var tr = go.transform;
                            tr.localPosition = Vector3.zero;
                            tr.localScale = Vector3.one;
                            tr.localRotation = Quaternion.identity;
                        }
                    }
                }
            }
            
            private void FlipYZ(float[] matrix, MeshResult meshResult)
            {
                var mesh = meshResult.mesh;
                var verts = mesh.vertices;
                var norms = mesh.normals;
                
                for (int i = 0; i < verts.Length; i ++)
                {
                    var vert = verts[i];
                    verts[i] = new Vector3(
                        (matrix[12] / 8192) + (-vert.x / 8192) * matrix[0], 
                        vert.z / (_mapInformation.Scale * (float)Conversions.LatitudeElevationCompensation(_mapInformation.LatitudeLongitude.Latitude)), 
                        (-matrix[13] / 8192) + (vert.y / 8192) * -matrix[5]);
                    norms[i] = new Vector3(-norms[i].x, norms[i].z, norms[i].y);
                }

                mesh.normals = norms;
                mesh.vertices = verts;
                mesh.RecalculateBounds();
            }
            
            private bool IsFootprint(GLTFast.Newtonsoft.Schema.Node node)
            {
                return node.extras.TryGetValue<string>("mapbox:footprint:id", out var value);
            }
        }
    }

    
}