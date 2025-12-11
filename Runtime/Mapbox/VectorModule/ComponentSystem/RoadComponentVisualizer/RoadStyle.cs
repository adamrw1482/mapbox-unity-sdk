using System;
using UnityEngine;

namespace Mapbox.VectorModule.ComponentSystem.RoadComponentVisualizer
{
    [Serializable]
    public class RoadStyle
    {
        [Tooltip("The name of the road style (used only for the UI)")]
        public string Name;
        public ComponentFilterStack Filters;
        public float Width;
        public Material Material;

        public void Initialize()
        {
            Filters.Initialize();
        }
    }
}