using System;
using System.Collections.Generic;
using Mapbox.VectorModule.Filters;

namespace Mapbox.VectorModule.ComponentSystem.RoadComponentVisualizer
{
    [Serializable]
    public abstract class RoadFilter
    {
        public RoadFilter()
        {
            
        }
        
        public abstract void Initialize();
        public abstract bool Try(RoadFeatureUnity feature);
    }
    
    [Serializable]
    public class RoadClassFilter : RoadFilter
    {
        //public FeatureStringPropertyFilterSettings PropertyFilterSettings;
        public StringCheckOperation CheckOperation = StringCheckOperation.Equals;
        public string FilterString;
        public bool Invert;
        private HashSet<string> _types;
        
        public override void Initialize()
        {
            if (CheckOperation == StringCheckOperation.Contains)
            {
                _types = new HashSet<string>();
                foreach (var s in FilterString.Split(','))
                {
                    _types.Add(s.Trim().ToLowerInvariant());
                }
            }
        }

        public override bool Try(RoadFeatureUnity feature)
        {
            var result = false;
            if (CheckOperation == StringCheckOperation.Equals)
            {
                result = FilterString.ToLowerInvariant() == feature.Class.ToLowerInvariant();
            }
            else if (CheckOperation == StringCheckOperation.Contains)
            {
                result = _types.Contains(feature.Class.ToLowerInvariant());
            }

            return !Invert ? result : !result; 
        }
    }
}