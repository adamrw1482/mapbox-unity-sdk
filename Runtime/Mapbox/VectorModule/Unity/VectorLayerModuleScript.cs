using System.Collections.Generic;
using Mapbox.BaseModule.Data.Interfaces;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Unity;
using Mapbox.BaseModule.Utilities;
using UnityEngine;

namespace Mapbox.VectorModule.Unity
{
	public class VectorLayerModuleScript : ModuleConstructorScript
	{
		[SerializeField] private VectorModuleSettings vectorModuleSettings;
		
		[SerializeField] private List<VectorLayerVisualizerObject> _layerVisualizers;
		public override ILayerModule ModuleImplementation { get; protected set; }

		public void Start()
		{
			
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
		
		private VectorLayerModule GetVectorLayerModule(IMapInformation mapInformation, UnityContext unityContext,
			MapService service, Dictionary<string, IVectorLayerVisualizer> dictionary)
		{
			if (vectorModuleSettings.SourceType != VectorSourceType.Custom)
			{
				vectorModuleSettings.DataSettings.TilesetId = MapboxDefaultVector.GetParameters(vectorModuleSettings.SourceType).Id;
			}
			else
			{
				vectorModuleSettings.DataSettings.TilesetId = vectorModuleSettings.CustomSourceId;
			}
			
			return new VectorLayerModule(mapInformation, service.GetVectorSource(vectorModuleSettings.DataSettings), unityContext, dictionary, vectorModuleSettings);
		}

		public override void OnDestroy()
		{
			ModuleImplementation?.OnDestroy();
		}
	}
}