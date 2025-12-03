using System;
using System.Collections.Generic;
using Mapbox.BaseModule.Data.Tiles;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Unity;
using Mapbox.BaseModule.Utilities;
using Mapbox.VectorModule.MeshGeneration;
using Mapbox.VectorModule.MeshGeneration.MeshModifiers;
using Mapbox.VectorModule.Unity;
using Mapbox.VectorTile.Contants;
using Mapbox.VectorTile.Geometry;
using UnityEngine;

namespace Mapbox.VectorModule.BuildingLayerVisualizer
{
    public abstract class MapboxComponentVisualizer : VectorLayerVisualizer
    {
        protected ObjectPool<VectorEntity> _buildingObjectPool;
        
        public MapboxComponentVisualizer(string name, IMapInformation mapInformation, UnityContext unityContext = null) : base(name, mapInformation, unityContext, null)
        {
            _buildingObjectPool = new ObjectPool<VectorEntity>(VectorEntityGenerator, 20);
        }
        
        public virtual HardcoreMeshData CreateMesh(CanonicalTileId tileId, PerfVectorTileLayer layer)
        {
            return null;
        }
        
        public virtual List<GameObject> CreateGo(CanonicalTileId tileId, HardcoreMeshData meshData)
        {
            return null;
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