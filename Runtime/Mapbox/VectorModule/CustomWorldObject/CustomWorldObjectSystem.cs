using System;
using System.Collections.Generic;
using System.Linq;
using Mapbox.BaseModule.Data.Tiles;
using Mapbox.BaseModule.Data.Vector2d;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Utilities;
using Mapbox.VectorModule;
using Mapbox.VectorModule.Filters;
using UnityEngine;

namespace Samples.CustomObjects
{
    public class CustomWorldObjectSystem : MonoBehaviour
    {
        public Action<CustomWorldVisualData, CustomObjectEvent> FootprintEvent = (d, e) => { };
        public MapBehaviourCore MapCore;
        private VectorLayerModule _vectorLayerModule;
    
        public List<CustomWorldObjectData> Samples;
        public Vector2 RelevantZoomRange = new Vector2(14, 16);

        private IMapInformation _mapInformation;
        private List<CustomWorldVisualData> _maintainThese;
    
        private void Start()
        {
            _maintainThese = new List<CustomWorldVisualData>();
        
            MapCore.Initialized += map =>
            {
                _mapInformation = map.MapInformation;
            
                if(map.MapVisualizer.TryGetLayerModule<VectorLayerModule>(typeof(VectorLayerModule), out var vectorLayerModule))
                {
                    _vectorLayerModule = vectorLayerModule;
                }

                _mapInformation.LatitudeLongitudeChanged += UpdateVisualPositions;
            };
        }

        [ContextMenu("Load Samples")]
        public void LoadModels()
        {
            var models = GetModels();
            var allRelevantTiles = new HashSet<CanonicalTileId>();
            foreach (var sampleData in models)
            {
                var relevantTiles = new HashSet<CanonicalTileId>();
                var visual = new CustomWorldVisualData(sampleData);
                visual.Footprint = GenerateFootprint(sampleData);
                foreach (var point in visual.Footprint)
                {
                    for (int i = (int)RelevantZoomRange.x; i <= RelevantZoomRange.y; i++)
                    {
                        var tileId = Conversions.LatitudeLongitudeToTileId(point, i).Canonical;
                        relevantTiles.Add(tileId);
                        allRelevantTiles.Add(tileId);
                    }
                }

                FootprintEvent(visual, CustomObjectEvent.Created);
                _maintainThese.Add(visual);
            }

            foreach (var tileId in allRelevantTiles)
            {
                _vectorLayerModule.ReloadTile(tileId);
            }

            //you can instantiate whenever necessary but I'm doing it right away 
            foreach (var visualData in _maintainThese)
            {
                if (visualData.GeneratedVisual == null)
                {
                    var go = GameObject.Instantiate(visualData.Data.Prefab);
                    go.transform.position = _mapInformation.ConvertLatLngToPosition(visualData.Data.LatLng);
                    go.transform.rotation = Quaternion.Euler(visualData.Data.RotationEuler);
                    go.transform.localScale = (visualData.Data.Scale / _mapInformation.Scale) * Conversions.LatitudeElevationCompensation((float)visualData.Data.LatLng.Latitude);
                    
                }
            }
        }

        private void UpdateVisualPositions(IMapInformation mapInformation)
        {
            foreach (var wrapper in _maintainThese)
            {
                var position = mapInformation.ConvertLatLngToPosition(wrapper.Data.LatLng);
                wrapper.GeneratedVisual.transform.position = position;
            }
        }
    
        private List<CustomWorldObjectData> GetModels()
        {
            return Samples;
        }
    
        private List<LatitudeLongitude> GenerateFootprint(CustomWorldObjectData sample)
        {
            var latlngCompensation = Conversions.LatitudeElevationCompensation((float)sample.LatLng.Latitude);
            var vertices = sample.Prefab.GetComponentInChildren<MeshFilter>().sharedMesh.vertices.Select(
                x =>
                {
                    var rotated = Quaternion.Euler(sample.RotationEuler) * x;
                    var tr = new Vector3(rotated.x * sample.Scale.x, rotated.y * sample.Scale.y, rotated.z * sample.Scale.z) * latlngCompensation;
                    return new Vector2d(tr.x, tr.z);
                });
            var mercator = Conversions.LatitudeLongitudeToWebMercator(sample.LatLng);
            return (List<LatitudeLongitude>)ConvexHull.ComputeConvexHull(
                vertices.Select(vert => Conversions.WebMercatorToLatLon(mercator + vert)).ToList());
        }
    }
    
    public enum CustomObjectEvent
    {
        Created,
        TurnedVisible,
        TurnedInvisible,
        Destroyed
    }
}