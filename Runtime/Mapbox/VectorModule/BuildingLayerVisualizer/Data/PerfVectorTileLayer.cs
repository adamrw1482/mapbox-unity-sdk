using System;
using System.Collections.Generic;
using Mapbox.VectorTile.Contants;
using Mapbox.VectorTile.Geometry;
using UnityEngine;

namespace Mapbox.VectorModule.BuildingLayerVisualizer
{
    public ref struct PerfVectorTileLayer
    {
        public ReadOnlySpan<byte> Data;

        /// <summary>
        /// Class to access a vector tile layer
        /// </summary>
        public PerfVectorTileLayer(ref ReadOnlySpan<byte> data)
        {
            Data = data;
            _FeaturesData = new List<Vector2Int>();
            Keys = new List<string>();
            Values = new List<object>();
            Name = null;
            Version = 0;
            Extent = 0;
            HeightTag = 0;
            MinHeightTag = 0;
            ExtrudeTag = 0;
        }
		
        /// <summary>
        /// Get number of features.
        /// </summary>
        /// <returns>Number of features in this layer</returns>
        public int FeatureCount()
        {
            return _FeaturesData.Count;
        }

        public Vector2Int GetViewFor(int i)
        {
            return _FeaturesData[i];
        }
        
//         /// <summary>
//         /// Get a feature of this layer
//         /// </summary>
//         /// <param name="feature">Index of the feature to request</param>
//         /// <param name="clipBuffer">
//         /// <para>'null': returns the geometries unaltered as they are in the vector tile. </para>
//         /// <para>Any value >=0 clips a border with the size around the tile. </para>
//         /// <para>These are not pixels but the same units as the 'extent' of the layer. </para>
//         /// </param>
//         /// <returns></returns>
//         public PerfVectorTileFeature GetFeature(int feature, uint? clipBuffer = null, float scale = 1.0f)
//         {
//             return GetFeature(this, _FeaturesData[feature], false, clipBuffer, scale);
//         }
//
//         public PerfVectorTileFeature GetFeature(
//             PerfVectorTileLayer layer
//             , Vector2Int view
//             , bool validate = true
//             , uint? clipBuffer = null
//             , float scale = 1.0f
//         )
//         {
//
//             var layerData = Data.Slice(view.x, view.y);
//             var featureReader = new PerfPbfReader(layerData);
//             var feat = new PerfVectorTileFeature();
//             bool geomTypeSet = false;
//             while (featureReader.NextByte())
//             {
//                 int featureType = featureReader.Tag;
//                 switch ((FeatureType)featureType)
//                 {
//                     case FeatureType.Id:
//                         feat.Id = (ulong)featureReader.Varint();
//                         break;
//                     case FeatureType.Tags:
// #if NET20
// 						List<int> tags = featureReader.GetPackedInt();
// #else
//                         var tags = featureReader.GetPackedInt();
// #endif
//                         feat.Tags = tags;
//                         break;
//                     case FeatureType.Type:
//                         int geomType = (int)featureReader.Varint();
//                         if (validate)
//                         {
//                             if (!ConstantsAsDictionary.GeomType.ContainsKey(geomType))
//                             {
//                                 throw new System.Exception(string.Format("Layer [{0}] has unknown geometry type tag: {1}", layer.Name, geomType));
//                             }
//                         }
//                         feat.GeometryType = (GeomType)geomType;
//                         geomTypeSet = true;
//                         break;
//                     case FeatureType.Geometry:
//                         if (null != feat.GeometryCommands)
//                         {
//                             throw new System.Exception(string.Format("Layer [{0}], feature already has a geometry", layer.Name));
//                         }
//                         //get raw array of commands and coordinates
//                         feat.GeometryCommands = featureReader.GetPackedUnit32();
//                         break;
//                     default:
//                         featureReader.Skip();
//                         break;
//                 }
//             }
//
//             return feat;
//         }

        public void AddFeatureData(Vector2Int data)
        {
            _FeaturesData.Add(data);
        }


        /// <summary>Name of this layer https://github.com/mapbox/vector-tile-spec/blob/master/2.1/vector_tile.proto#L57</summary>
        public string Name { get; set; }


        /// <summary>Version of this layer https://github.com/mapbox/vector-tile-spec/blob/master/2.1/vector_tile.proto#L55</summary>
        public long Version { get; set; }


        /// <summary>Extent of this layer https://github.com/mapbox/vector-tile-spec/blob/master/2.1/vector_tile.proto#L70</summary>
        public ulong Extent { get; set; }


        /// <summary>Raw data of the features contained in this layer</summary>
        private List<Vector2Int> _FeaturesData { get; set; }


        /// <summary>
        /// TODO: switch to 'dynamic' when Unity supports .Net 4.5
        /// Values contained in this layer
        /// </summary>
        public List<object> Values { get; set; }


        /// <summary>
        /// Keys contained in this layer
        /// </summary>
        public List<string> Keys { get; set; }

        public int HeightTag;
        public int MinHeightTag;
        public int ExtrudeTag;
    }
}