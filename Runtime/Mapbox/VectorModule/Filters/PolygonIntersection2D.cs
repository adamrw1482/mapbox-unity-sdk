using System.Collections.Generic;
using Mapbox.BaseModule.Data.Vector2d;
using Mapbox.BaseModule.Map;
using UnityEngine;

namespace Mapbox.VectorModule.Filters
{
    public class PolygonIntersection2D
    {
        //LATLNG
        public static bool ArePolygonsIntersecting(IMapInformation mapInformation, List<LatitudeLongitude> polygon1,
            List<LatitudeLongitude> polygon2)
        {
            return IsSeparatingAxisFound(polygon1, polygon2) == false && IsSeparatingAxisFound(polygon2, polygon1) == false;
        }

        private static bool IsSeparatingAxisFound(List<LatitudeLongitude> polygonA, List<LatitudeLongitude> polygonB)
        {
            // Iterate through each edge of polygonA
            for (int i = 0; i < polygonA.Count; i++)
            {
                // Get the current edge in the XZ plane
                var edge = new LatitudeLongitude(
                    polygonA[(i + 1) % polygonA.Count].Longitude - polygonA[i].Longitude,
                    polygonA[(i + 1) % polygonA.Count].Latitude - polygonA[i].Latitude
                );

                // Find the axis perpendicular to the edge
                var axis = new LatitudeLongitude(-edge.Latitude, edge.Longitude);

                // Project both polygons onto this axis
                (double minA, double maxA) = ProjectPolygonOnAxis(axis, polygonA);
                (double minB, double maxB) = ProjectPolygonOnAxis(axis, polygonB);

                // Check for gap
                if (maxA < minB || maxB < minA)
                {
                    // If there's a gap, then there's a separating axis
                    return true;
                }
            }
            return false;
        }

        private static (double min, double max) ProjectPolygonOnAxis(LatitudeLongitude axis, List<LatitudeLongitude> polygon)
        {
            // Project the first point of the polygon onto the axis
            double min = LatitudeLongitude.Dot(axis, new LatitudeLongitude(polygon[0].Longitude, polygon[0].Latitude));
            double max = min;

            // Project the rest of the points
            for (int i = 1; i < polygon.Count; i++)
            {
                double projection = LatitudeLongitude.Dot(axis, new LatitudeLongitude(polygon[i].Longitude, polygon[i].Latitude));
                if (projection < min) min = projection;
                if (projection > max) max = projection;
            }

            return (min, max);
        }
        
        
        
        //VECTOR3
        public static bool ArePolygonsIntersecting(List<Vector3> polygon1, List<Vector3> polygon2)
        {
            return IsSeparatingAxisFound(polygon1, polygon2) == false && IsSeparatingAxisFound(polygon2, polygon1) == false;
        }

        private static bool IsSeparatingAxisFound(List<Vector3> polygonA, List<Vector3> polygonB)
        {
            // Iterate through each edge of polygonA
            for (int i = 0; i < polygonA.Count; i++)
            {
                // Get the current edge in the XZ plane
                Vector2 edge = new Vector2(
                    polygonA[(i + 1) % polygonA.Count].x - polygonA[i].x,
                    polygonA[(i + 1) % polygonA.Count].z - polygonA[i].z
                );

                // Find the axis perpendicular to the edge
                Vector2 axis = new Vector2(-edge.y, edge.x);

                // Project both polygons onto this axis
                (float minA, float maxA) = ProjectPolygonOnAxis(axis, polygonA);
                (float minB, float maxB) = ProjectPolygonOnAxis(axis, polygonB);

                // Check for gap
                if (maxA < minB || maxB < minA)
                {
                    // If there's a gap, then there's a separating axis
                    return true;
                }
            }
            return false;
        }

        private static (float min, float max) ProjectPolygonOnAxis(Vector2 axis, List<Vector3> polygon)
        {
            // Project the first point of the polygon onto the axis
            float min = Vector2.Dot(axis, new Vector2(polygon[0].x, polygon[0].z));
            float max = min;

            // Project the rest of the points
            for (int i = 1; i < polygon.Count; i++)
            {
                float projection = Vector2.Dot(axis, new Vector2(polygon[i].x, polygon[i].z));
                if (projection < min) min = projection;
                if (projection > max) max = projection;
            }

            return (min, max);
        }
    }
}