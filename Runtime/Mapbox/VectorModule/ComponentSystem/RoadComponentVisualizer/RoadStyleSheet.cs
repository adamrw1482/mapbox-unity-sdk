using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;

namespace Mapbox.VectorModule.ComponentSystem.RoadComponentVisualizer
{
    [DisplayName("Road Style Sheet")]
    [CreateAssetMenu(menuName = "Mapbox/Road Style Sheet")]
    public class RoadStyleSheet : ScriptableObject
    {
        public List<RoadStyle> Styles;

        public void Initialize()
        {
            foreach (var style in Styles)
            {
                style.Initialize();
            }
        }
        
        public bool TryGetStyle(RoadFeatureUnity feature, out RoadStyle style)
        {
            style = Styles.FirstOrDefault(x => x.Filters.Try(feature));
            return style != null;
        }

        public bool Contains(RoadFeatureUnity feature)
        {
            return Styles.Any(x => x.Filters.Try(feature));
        }
    }
}