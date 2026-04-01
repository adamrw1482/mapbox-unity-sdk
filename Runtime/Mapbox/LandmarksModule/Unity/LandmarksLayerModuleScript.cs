using System;
using System.Collections;
using System.Collections.Generic;
using Mapbox.BaseModule;
using Mapbox.BaseModule.Data.Interfaces;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Unity;
using Mapbox.LandmarkModule;
using UnityEngine;
using UnityEngine.Events;

namespace Mapbox.LandmarkModule.Unity
{
	public class LandmarksLayerModuleScript : ModuleConstructorScript
	{ 
		public string CustomSourceTilesetName = "mapbox.mapbox-3dbuildings-v1-meshopt";
		public Material BaseMaterial;
		
		public override ILayerModule ModuleImplementation { get; protected set; }

		private void Start()
		{
			
		}

		public override ILayerModule ConstructModule(MapService service, IMapInformation mapInformation, UnityContext unityContext)
		{
			var module = GetLayerModule(unityContext, mapInformation, service);
			
			ModuleImplementation = module;
			return ModuleImplementation;
		}
		
		private LandmarksLayerModule GetLayerModule(UnityContext unityContext, IMapInformation mapInformation, MapService service)
		{
			var model = new LandmarksLayerModule(unityContext, mapInformation, service, CustomSourceTilesetName, BaseMaterial);
			return model;
		}
	}
}