using System;
using System.ComponentModel;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Unity;
using Mapbox.VectorModule.MeshGeneration.MeshModifiers;
using Mapbox.VectorModule.Unity;
using UnityEngine;
using UnityEngine.Serialization;

namespace Mapbox.VectorModule.BuildingLayerVisualizer
{
    [DisplayName("Road Component")]
    [CreateAssetMenu(menuName = "Mapbox/Layer Visualizers/Road Component Visualizer")]
    public class RoadLayerVisualizerObject : LayerVisualizerConstructor, IPerformanceLayerVisualizer
    {
        public RoadComponentSettings Settings;
        public bool IsActive = true;

        private RoadComponentVisualizer _layerVisualizer;
		
        public IVectorLayerVisualizer GetLayerVisualizer()
        {
            return _layerVisualizer;
        }
		
        public override IVectorLayerVisualizer ConstructLayerVisualizer(IMapInformation mapInformation, UnityContext unityContext)
        {
            _layerVisualizer = new RoadComponentVisualizer("road", mapInformation, unityContext, Settings);
            _layerVisualizer.Active = IsActive;

            _layerVisualizer.OnVectorMeshCreated += OnVectorMeshCreated;
            _layerVisualizer.OnVectorMeshDestroyed += OnVectorMeshDestroyed;
			
            return _layerVisualizer;
        }

        public Action<GameObject> OnVectorMeshCreated = list => { };
        public Action<GameObject> OnVectorMeshDestroyed = go => { };
    }

    [Serializable]
    public class RoadComponentSettings
    {
        public float RoadWidth = 2;
        public Material Material;
        
        public RoadComponentSettings()
        {
        }
    }
}