using System;
using System.Collections.Generic;
using System.Linq;
using Mapbox.VectorModule.Filters;
using UnityEngine;

namespace Mapbox.VectorModule.ComponentSystem.RoadComponentVisualizer
{
    [Serializable]
    public class ComponentFilterStack
    {
        [SerializeReference]
        public List<RoadFilter> Filters = new List<RoadFilter>();
        public LayerFilterCombinerOperationType Type;

        public ComponentFilterStack()
        {
            
        }

        public void Initialize()
        {
            foreach (var filter in Filters)
            {
                filter.Initialize();
            }
        }

        public bool Try(RoadFeatureUnity feature)
        {
            if (Filters == null || Filters.Count == 0)
                return true;

            switch (Type)
            {
                case LayerFilterCombinerOperationType.Any:
                    return Filters.Any(m => m.Try(feature));
                case LayerFilterCombinerOperationType.All:
                    return Filters.All(m => m.Try(feature));
                case LayerFilterCombinerOperationType.None:
                    return !Filters.Any(m => m.Try(feature));
                default:
                    return false;
            }
        }
    }
}