using System;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Utilities;
using UnityEngine;
using UnityEngine.Serialization;

namespace Mapbox.VectorModule.MeshGeneration.GameObjectModifiers
{
    [Serializable]
    public class LayerMaskModifier : GameObjectModifier
    {
        //I couldn't get LayerMask to work here
        private int _layerMask;

        public LayerMaskModifier(int layerMask)
        {
            _layerMask = layerMask;
        }

        public override void Run(VectorEntity ve, IMapInformation mapInformation)
        {
            ve.GameObject.layer = _layerMask;
        }
    }
}