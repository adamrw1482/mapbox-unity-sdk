using Mapbox.BaseModule.Unity;
using Mapbox.VectorModule.MeshGeneration.Unity;
using UnityEngine;

namespace Mapbox.VectorModule.MeshGeneration.GameObjectModifiers
{
    [CreateAssetMenu(menuName = "Mapbox/Modifiers/LayerMask Modifier")]
    public class LayerMaskModifierObject : ScriptableGameObjectModifierObject
    {
        public int LayerMask;
        private LayerMaskModifier _prefabModifierImplementation;
        protected override GameObjectModifier _gameObjectModifierImplementation => _prefabModifierImplementation;

        public override void ConstructModifier(UnityContext unityContext)
        {
            _prefabModifierImplementation = new LayerMaskModifier(LayerMask);
        }
    }
}