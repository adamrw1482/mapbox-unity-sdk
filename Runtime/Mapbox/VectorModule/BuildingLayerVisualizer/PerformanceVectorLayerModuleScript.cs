using System.Collections.Generic;
using Mapbox.BaseModule.Data.Interfaces;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Unity;
using Mapbox.BaseModule.Utilities;
using Mapbox.VectorModule.Unity;
using UnityEngine;

namespace Mapbox.VectorModule.BuildingLayerVisualizer
{
    public class PerformanceVectorLayerModuleScript : ModuleConstructorScript
    {
        [SerializeField] private List<LayerVisualizerConstructor> _layerVisualizers;
        public override ILayerModule ModuleImplementation { get; protected set; }

        public void Start()
        {
			
        }
        
        protected VectorLayerModule GetVectorLayerModule(IMapInformation mapInformation, UnityContext unityContext, MapService service, Dictionary<string, IVectorLayerVisualizer> dictionary)
        {
            var tilesetId = MapboxDefaultVector.GetParameters(VectorSourceType.MapboxStreetsV8).Id;
            var settings = new VectorModuleSettings()
            {
                DataSettings = new VectorSourceSettings()
                {
                    TilesetId = tilesetId,
                    CacheSize = 100,
                    ClampDataLevelToMax = 15
                }
            };
            return new PerformanceVectorLayerModule(mapInformation, service.GetVectorSource(settings.DataSettings), unityContext, dictionary, settings);
        }

        public override ILayerModule ConstructModule(MapService service, IMapInformation mapInformation, UnityContext unityContext)
        {
            var dictionary = new Dictionary<string, IVectorLayerVisualizer>();
            foreach (var visualizerObject in _layerVisualizers)
            {
                if(visualizerObject == null) continue;
                var visualizer = visualizerObject.ConstructLayerVisualizer(mapInformation, unityContext);
                dictionary.Add(visualizer.VectorLayerName, visualizer);
            }
            ModuleImplementation = GetVectorLayerModule(mapInformation, unityContext, service, dictionary);
            return ModuleImplementation;
        }
    }
}