using System;
using System.ComponentModel;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Unity;
using Mapbox.VectorModule.MeshGeneration.MeshModifiers;
using Mapbox.VectorModule.Unity;
using UnityEngine;

namespace Mapbox.VectorModule.BuildingLayerVisualizer
{
    [DisplayName("Building Component")]
    [CreateAssetMenu(menuName = "Mapbox/Modifiers/Buildings Visualizer")]
    public class BuildingLayerVisualizerObject : LayerVisualizerConstructor, IPerformanceLayerVisualizer
    {
        public BuildingVisualizerSettings Settings;
        public bool IsActive = true;

        private BuildingLayerVisualizer _layerVisualizer;
		
        public IVectorLayerVisualizer GetLayerVisualizer()
        {
            return _layerVisualizer;
        }
		
        public override IVectorLayerVisualizer ConstructLayerVisualizer(IMapInformation mapInformation, UnityContext unityContext)
        {
            _layerVisualizer = new BuildingLayerVisualizer("building", mapInformation, unityContext, Settings);
            _layerVisualizer.Active = IsActive;

            _layerVisualizer.OnVectorMeshCreated += OnVectorMeshCreated;
            _layerVisualizer.OnVectorMeshDestroyed += OnVectorMeshDestroyed;
			
            return _layerVisualizer;
        }

        public Action<GameObject> OnVectorMeshCreated = list => { };
        public Action<GameObject> OnVectorMeshDestroyed = go => { };
    }

    [Serializable]
    public class BuildingVisualizerSettings
    {
        public bool EnableTerrainSnapping = false;
        public ChamferModifierSettings ChamferModifierSettings;
        public Material Material;

        public BuildingVisualizerSettings()
        {
            EnableTerrainSnapping = false;
            ChamferModifierSettings = new ChamferModifierSettings() { FlatTops = true, OffsetInMeters = 1 };
        }
    }
    
    public interface IPerformanceLayerVisualizer
    {
        
    }
}