using System;
using System.Collections.Generic;
using System.ComponentModel;
using Mapbox.BaseModule.Data.Tiles;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Utilities;
using UnityEngine;

namespace Mapbox.VectorModule.Filters
{
    [CreateAssetMenu(menuName = "Mapbox/Filters/Mesh Collider Filter")]
    [DisplayName("Polygon Collision Filter")]

    public class PolygonCollisionFilterObject : FilterBaseObject
    {
        private PolygonCollisionFilter _filter;
        public override ILayerFeatureFilterComparer Filter
        {
            get
            {
                if(_filter == null)
                    _filter = new PolygonCollisionFilter();
                return _filter;
            }
        }
    }
    
    //TODO this probably should work based on latlng values instead of tileID and vertex positions 
    [Serializable]
    public class PolygonCollisionFilter : FilterBase
    {
        [NonSerialized] public Dictionary<CanonicalTileId, List<List<Vector3>>> PolygonsPerTile = new Dictionary<CanonicalTileId, List<List<Vector3>>>();
        
        public override bool Try(VectorFeatureUnity feature)
        {
            if (PolygonsPerTile.TryGetValue(feature.TileId, out var colliders))
            {
                foreach (var collider in colliders)
                {
                    foreach (var submesh in feature.Points)
                    {
                        if (PolygonIntersection2D.ArePolygonsIntersecting(collider, submesh))
                            return false;
                    }
                }
            }
            return true;
        }
        
        public void AddMeshCollider(Transform tr, Mesh mesh, List<CanonicalTileId> tileList, IMapInformation mapInfo)
        {
            //PolygonsPerTile.Clear();
            foreach (var tileId in tileList)
            {
                var vertices = new List<Vector3>();
                foreach (var vertex in mesh.vertices)
                {
                    var pos = tr.TransformPoint(vertex);
                    var latlng = mapInfo.ConvertPositionToLatLng(pos);
                    var zeroOnePosition = Conversions.LatitudeLongitudeToInTile01(latlng, tileId);
                    var localPosition = ZeroOneToLocal(zeroOnePosition);
                    vertices.Add(new Vector3(localPosition.x, 0, localPosition.y));
                }

                if(!PolygonsPerTile.ContainsKey(tileId))
                    PolygonsPerTile.Add(tileId, new List<List<Vector3>>());
                PolygonsPerTile[tileId].Add(vertices);
            }
        }

        public void AddMeshCollider8192(Transform tr, Mesh mesh, List<CanonicalTileId> tileList, IMapInformation mapInfo)
        {
            var children = new Vector2[4]
            {
                new Vector2(0, 0),
                new Vector2(.5f, 0),
                new Vector2(0, .5f),
                new Vector2(.5f, .5f),
            };
            
            foreach (var tileId in tileList)
            {
                if (tileId.Z == 14)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        var child = tileId.Quadrant(i);
                        
                        var vertices = new List<Vector3>();
                        foreach (var vertex in mesh.vertices)
                        {
                            var localPosition = vertex / 8192;
                            
                            var newX = (localPosition.x - children[i].x) * 2;
                            var newY = (localPosition.z + children[i].y) * 2;
                            vertices.Add(new Vector3(newX, 0, newY));
                        }
                        
                        var tilePos = Conversions.TileBoundsInUnitySpace(child, mapInfo.CenterMercator, mapInfo.Scale);
                        for (int j = 1; j < vertices.Count; j++)
                        {
                            var p1 = tilePos.TopLeft.ToVector3xz() + ((float)tilePos.Size.x * vertices[j]);
                            var p2 = tilePos.TopLeft.ToVector3xz() + ((float)tilePos.Size.x * vertices[j - 1]);
                            Debug.DrawLine(p1, p2, Color.red, 1000);
                        }

                        if(!PolygonsPerTile.ContainsKey(child))
                            PolygonsPerTile.Add(child, new List<List<Vector3>>());
                        PolygonsPerTile[child].Add(vertices);
                    }
                }
            }
        }
        
        public float InverseLerpUnclamped(float from, float to, float value)
        {
            return (value - from) / (to - from);
        }

        private Vector2 ZeroOneToLocal(Vector2 pos)
        {
            return new Vector2(pos.x, -1 * (1 - pos.y));
        }
    }
}