using System;
using System.Collections.Generic;
using Mapbox.VectorTile.Geometry;
using UnityEngine;

namespace Mapbox.VectorModule.BuildingLayerVisualizer
{
    public ref struct PerfVectorTileFeature
    {
        public ulong Id;
        public GeomType GeometryType;
        public uint[] GeometryCommands;

        public float Height;
        public float MinHeight;
        public bool DoExtrude;
        
        /// <summary>Tags to resolve properties https://github.com/mapbox/vector-tile-spec/tree/master/2.1#44-feature-attributes</summary>
        public int[] Tags;
        
        public MeshVertexData Geometry(Vector3 scale)
        {
            return PerformanceDecodeGeometry.GetGeometry(GeometryCommands, scale);
        }
        
        public MeshVertexData Geometry(Vector3 scale, Vector4 offsetTo14)
        {
            return PerformanceDecodeGeometry.GetGeometry(GeometryCommands, scale, offsetTo14);
        }

        /// <summary>
        /// Get properties of this feature. Throws exception if there is an uneven number of feature tag ids
        /// </summary>
        /// <returns>Dictionary of this feature's properties</returns>
        public Dictionary<string, object> GetProperties(ref PerfVectorTileLayer layer)
        {

            if (0 != Tags.Length % 2)
            {
                throw new Exception(string.Format("Layer [{0}]: uneven number of feature tag ids", layer.Name));
            }
            int tagCount = Tags.Length;
            Dictionary<string, object> properties = new Dictionary<string, object>(tagCount/2);
            for (int i = 0; i < tagCount; i += 2)
            {
                properties.Add(layer.Keys[Tags[i]], layer.Values[Tags[i + 1]]);
            }
            return properties;
        }

        public void SetTags(ref PerfVectorTileLayer layer)
        {
            for (int i = 0; i < Tags.Length; i+=2)
            {
                if (Tags[i] == layer.HeightTag)
                {
                    Height = Convert.ToSingle(layer.Values[Tags[i + 1]]);
                }
                else if (Tags[i] == layer.MinHeightTag)
                {
                    MinHeight = Convert.ToSingle(layer.Values[Tags[i + 1]]);
                }
                else if (Tags[i] == layer.ExtrudeTag)
                {
                    DoExtrude = Convert.ToBoolean(layer.Values[Tags[i + 1]]);
                }
            }
        }
    }
    
}