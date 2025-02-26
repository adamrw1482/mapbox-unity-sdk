using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Mapbox.BaseModule.Data.Tiles;
using Mapbox.BaseModule.Data.Vector2d;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Utilities;
using UnityEngine;

namespace Mapbox.VectorModule.Filters
{
    [CreateAssetMenu(menuName = "Mapbox/Filters/Lat Lng Collision Filter")]
    [DisplayName("Lat Lng Collision Filter")]

    public class LatLngCollisionFilterObject : FilterBaseObject
    {
        [NonSerialized] private LatLngCollisionFilter _filter;
        [Tooltip("This script will only work on a single zoom level")]
        public int ZoomLevel = 15;
        
        public override ILayerFeatureFilterComparer Filter
        {
            get
            {
                if (_filter == null)
                {
                    _filter = new LatLngCollisionFilter(ZoomLevel);
                }
                return _filter;
            }
        }

        public void AddCollisionPolygon(List<LatitudeLongitude> points)
        {
            _filter.AddCollisionPolygon(points);
        }

        public void AddCollisionPolygon(IEnumerable<CanonicalTileId> tiles, List<LatitudeLongitude> points)
        {
            _filter.AddCollisionPolygon(tiles, points);
        }
    }
    
    //TODO this probably should work based on latlng values instead of tileID and vertex positions 
    [Serializable]
    public class LatLngCollisionFilter : FilterBase
    {
        private int _zoomLevel;
        [NonSerialized] private Dictionary<CanonicalTileId, HashSet<List<LatitudeLongitude>>> _colliderByTileId;
        private readonly IMapInformation _mapInformation;

        public LatLngCollisionFilter(int zoomLevel)
        {
            _colliderByTileId = new Dictionary<CanonicalTileId, HashSet<List<LatitudeLongitude>>>();
            _zoomLevel = zoomLevel;
            _mapInformation = GameObject.FindObjectOfType<MapBehaviourCore>().MapboxMap.MapInformation;
        }

        public override bool Try(VectorFeatureUnity feature)
        {
            if (_colliderByTileId.TryGetValue(feature.TileId, out HashSet<List<LatitudeLongitude>> colliders))
            {
                foreach (var submesh in feature.Points)
                {
                    var meshLatlng = submesh.Select(x => Conversions.Tile01ToLatitudeLongitude(x, feature.TileId))
                        .ToList();
                    
                    if (colliders.Any(x => PolygonIntersection2D.ArePolygonsIntersecting(_mapInformation, x, meshLatlng)))
                        return false;
                }
            }

            return true;
        }

        public void AddCollisionPolygon(List<LatitudeLongitude> points)
        {
            foreach (var point in points)
            {
                var tileId = Conversions.LatitudeLongitudeToTileId(point, _zoomLevel).Canonical;
                if(!_colliderByTileId.ContainsKey(tileId))
                    _colliderByTileId.Add(tileId, new HashSet<List<LatitudeLongitude>>());
                if(!_colliderByTileId[tileId].Contains(points))
                    _colliderByTileId[tileId].Add(points);
            }
        }
        
        public void AddCollisionPolygon(IEnumerable<CanonicalTileId> tiles, List<LatitudeLongitude> points)
        {
            foreach (var tileId in tiles)
            {
                if(!_colliderByTileId.ContainsKey(tileId))
                    _colliderByTileId.Add(tileId, new HashSet<List<LatitudeLongitude>>());
                if(!_colliderByTileId[tileId].Contains(points))
                    _colliderByTileId[tileId].Add(points);
            }
        }
    }
}