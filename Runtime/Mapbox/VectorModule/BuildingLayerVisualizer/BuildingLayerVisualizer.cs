using System;
using System.Collections.Generic;
using Mapbox.BaseModule.Data.Tiles;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Unity;
using Mapbox.BaseModule.Utilities;
using Mapbox.VectorModule.MeshGeneration;
using Mapbox.VectorModule.MeshGeneration.MeshModifiers;
using Mapbox.VectorModule.Unity;
using Mapbox.VectorTile.Geometry;
using UnityEngine;

namespace Mapbox.VectorModule.BuildingLayerVisualizer
{
    public class BuildingLayerVisualizer : VectorLayerVisualizer
    {
        private BuildingVisualizerSettings _settings;
        private ObjectPool<VectorEntity> _buildingObjectPool;
        
        public BuildingLayerVisualizer(string name, IMapInformation mapInformation, UnityContext unityContext = null, BuildingVisualizerSettings settings = null) : base(name, mapInformation, unityContext, null)
        {
            _settings = settings ?? new BuildingVisualizerSettings();
            _buildingObjectPool = new ObjectPool<VectorEntity>(VectorEntityGenerator, 20);
        }
        
        public HardcoreMeshData CreateMesh(CanonicalTileId tileId, PerfVectorTileLayer layer)
        {
            var arrayPolygon = new ArrayPolygon();
            var arraySnapTerrain = (_settings.EnableTerrainSnapping) ? new ArraySnapTerrain(_mapInformation) : null;
            IPerformanceExtrusion arrayHeight = _settings.RoundBuildingCorners
                                                    ? new ArrayChamferHeight(_settings.ChamferExtrusionSettings)
                                                    : new ArrayHeight(_settings.BasicExtrusionSettings);
            var featureCount = layer.FeatureCount();
            var info = new StackMeshInfo(featureCount);
            
            //var offsetTo14 = tileId.CalculateTopLeftScaleOffsetAtZoom(14);
            var tileSize = Conversions.TileSizeInUnitySpace(tileId.Z, _mapInformation.Scale);
            var featureArray = new PerfVectorFeatureUnity[featureCount];
            
            for (var i = 0; i < featureCount; i++)
            {
                var feature = GetFeature(layer, i);
                if (feature == null) continue;
                feature.TileId = tileId;
                featureArray[i] = feature;

                info.vertexRanges[i] = info.TotalPointCount;
                info.TotalPointCount += feature.VertexData.VertexCount * 5;
            }

            var meshData = new HardcoreMeshData(info, info.TotalPointCount);

            var poolSize = 10002;
            var triList = new int[poolSize];
            var triIndex = 0;
            var baseTriIndex = 0;
            for (int i = 0; i < featureCount; i++)
            {
                var featureResult = featureArray[i];
                if (featureResult == null || !featureResult.DoExtrude)
                {
                    //ArrayPool<Vector3>.Shared.Return(featureResult.VertexData.Vertices);
                    continue;
                }

                var featurePreTriIndex = triIndex;
                
                var vertices = meshData.Vertices.AsSpan(meshData.MeshInfo.vertexRanges[i], meshData.MeshInfo.VertexCount(i));
                var normals = meshData.Normals.AsSpan(meshData.MeshInfo.vertexRanges[i], meshData.MeshInfo.VertexCount(i));
                var vertexAnchorIndex = meshData.MeshInfo.vertexRanges[i];
                
                triIndex = arrayPolygon.Polygonize(vertices, normals, vertexAnchorIndex, triList, triIndex, featureResult);
                if (triIndex == -1)
                {
                    for (int j = featurePreTriIndex; j < triList.Length; j++)
                    {
                        triList[j] = 0;
                    }
                    meshData.Triangles.Add(triList);
                    baseTriIndex += triList.Length;
                    triList = new int[poolSize];
                    triIndex = 0;
                    i--;
                    //ArrayPool<Vector3>.Shared.Return(featureResult.VertexData.Vertices);
                    continue;
                }
                
                if (_settings.EnableTerrainSnapping)
                {
                    arraySnapTerrain.SnapTerrain(vertices, featureResult, tileSize);
                }

                var triSpaceRequired = arrayHeight.CalculateTriCountFor(featureResult.VertexData.VertexCount);
                if (triSpaceRequired + triIndex >= triList.Length)
                {
                    for (int j = featurePreTriIndex; j < triList.Length; j++)
                    {
                        triList[j] = 0;
                    }
                    meshData.Triangles.Add(triList);
                    baseTriIndex += triList.Length;
                    triList = new int[triSpaceRequired + triIndex + 3];
                    triIndex = 0;
                    i--;
                    //ArrayPool<Vector3>.Shared.Return(featureResult.VertexData.Vertices);
                    continue;
                }
                else
                {
                    triIndex = arrayHeight.Run(vertices, normals, vertexAnchorIndex, triList, triIndex, featureResult, tileSize, _mapInformation);    
                }
                info.triRanges[i] = baseTriIndex + triIndex;
                //ArrayPool<Vector3>.Shared.Return(featureResult.VertexData.Vertices);
            }
            meshData.Triangles.Add(triList);
            return meshData;
        }
        
        private PerfVectorFeatureUnity GetFeature(PerfVectorTileLayer layer, int i)
        {
            var feature = layer.GetFeature(i);
            var layerExtent = (float)layer.Extent;
            feature.SetTags(ref layer);
            var featureResult = new PerfVectorFeatureUnity
            {
                Height = feature.Height,
                MinHeight = feature.MinHeight,
                DoExtrude = feature.DoExtrude,
                //Properties = feature.GetProperties(ref layer),
                VertexData = feature.Geometry(new Vector3(layerExtent, 0, -layerExtent))
            };

            if (featureResult.VertexData.VertexCount < 1)
            {
                return null;
            }
            return featureResult;
        }
        
        private PerfVectorFeatureUnity GetFeature(PerfVectorTileLayer layer, int i, Vector4 offsetTo14)
        {
            var feature = layer.GetFeature(i);
            var layerExtent = (float)layer.Extent;
            feature.SetTags(ref layer);
            var featureResult = new PerfVectorFeatureUnity
            {
                Height = feature.Height,
                MinHeight = feature.MinHeight,
                DoExtrude = feature.DoExtrude,
                //Properties = feature.GetProperties(ref layer),
                VertexData = feature.Geometry(new Vector3(layerExtent, 0, -layerExtent), offsetTo14)
            };

            if (featureResult.VertexData.VertexCount < 1)
            {
                return null;
            }
            return featureResult;
        }
        
        public List<GameObject> CreateGo(CanonicalTileId tileId, HardcoreMeshData meshData)
        {
            var objectList = new List<GameObject>();
            var entity = _buildingObjectPool.GetObject();
            var mats = new Material[meshData.Triangles.Count];
            for (int i = 0; i < meshData.Triangles.Count; i++)
            {
                mats[i] = _settings.Material;
            }
            entity.MeshRenderer.materials = mats;
            
            entity.GameObject.transform.SetParent(_layerRootObject);
            entity.StackId = 0;

            var mesh = entity.Mesh;
            mesh.Clear();
            mesh.SetVertices(meshData.Vertices);
            mesh.SetNormals(meshData.Normals);
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.subMeshCount = meshData.Triangles.Count;
            for (var index = 0; index < meshData.Triangles.Count; index++)
            {
                var submesh = meshData.Triangles[index];
                mesh.SetTriangles(submesh, index);
            }

            entity.MeshFilter.sharedMesh = mesh;
            objectList.Add(entity.GameObject);
            
            if(!_results.ContainsKey(tileId))
                _results.Add(tileId, new List<VectorEntity>());
            _results[tileId].Add(entity);
            OnBuildingMeshCreated(tileId, entity.GameObject, meshData);
            
            return objectList;
        }

        public override void UpdateForView(CanonicalTileId canonicalTileId, IMapInformation information)
        {
            if (_results.TryGetValue(canonicalTileId, out var visuals))
            {
                foreach (var entity in visuals)
                {
                    _mapInformation.PositionObjectFor(canonicalTileId, out var position, out var scale);
                    entity.GameObject.transform.localPosition = position;
                    entity.GameObject.transform.localScale = scale;
                }
            }
        }
        
        public override void UnregisterTile(CanonicalTileId tileId)
        {
            if (_results.ContainsKey(tileId))
            {
                foreach (var entity in _results[tileId])
                {
                    entity.GameObject.SetActive(false);
                    _buildingObjectPool.Put(entity);
                    //TODO call finalize for gameobject modifiers here
                    
                    OnBuildingMeshDestroyed(tileId, entity.GameObject);
                }

                _results.Remove(tileId);
            }

            //TODO call unregister for gameobject modifiers here
        }

        public override void SetActive(CanonicalTileId canonicalTileId, bool isActive, IMapInformation mapInformation)
        {
            if (isActive)
            {
                if (_results.TryGetValue(canonicalTileId, out var visuals))
                {
                    foreach (var entity in visuals)
                    {
                        entity.GameObject.SetActive(true);
                    }
                }
            }
            else
            {
                UnregisterTile(canonicalTileId);
            }
        }
        
        public override void ClearCaches()
        {
            base.ClearCaches();
        }
        
        private VectorEntity VectorEntityGenerator()
        {
            var go = new GameObject();
            go.transform.SetParent(_layerRootObject);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = new Mesh();
            var mr = go.AddComponent<MeshRenderer>();
            var tempVectorEntity = new VectorEntity()
            {
                GameObject = go,
                Transform = go.transform,
                MeshFilter = mf,
                MeshRenderer = mr,
                Mesh = mf.sharedMesh
            };
            return tempVectorEntity;
        }
        
        public Action<CanonicalTileId, GameObject, HardcoreMeshData> OnBuildingMeshCreated = (id, list, info) => { };
        public Action<CanonicalTileId, GameObject> OnBuildingMeshDestroyed = (id, list) => { };
    }

    public class PerfVectorFeatureUnity : VectorFeatureUnity
    {
        public MeshVertexData VertexData;
        public float Height;
        public float MinHeight;
        public bool DoExtrude;
    }
}