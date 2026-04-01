using UnityEngine;                                                                                                              
using UnityEngine.Scripting;                                                                                                    
using GLTFast.Addons;                                                                                                           
using GLTFast.FeatureId;                                                                                                        

namespace Mapbox.LandmarkModule
{
    /// <summary>
    /// ============================================================================                                                
    /// IMPORTANT: DO NOT REMOVE THIS CLASS                                                                                       
    /// ============================================================================
    ///                                                                                                                             
    /// This class registers glTFast import add-ons required for Mapbox functionality.
    /// It runs automatically before any scene loads via [RuntimeInitializeOnLoadMethod].                                           
    ///                                                                                                                             
    /// - FeatureIdImportAddon: Enables EXT_mesh_features / feature ID support in glTF
    ///   models. Without this, feature IDs will not be decoded and Mapbox features                                                 
    ///   that rely on per-vertex/per-primitive feature identification will not work.                                               
    ///                                                                                                                             
    /// The [Preserve] attribute prevents Unity's managed code stripping (IL2CPP)                                                   
    /// from removing this class, since it is only invoked via the runtime callback                                                 
    /// and never explicitly referenced in code.                                                                                    
    /// ============================================================================                                                
    /// </summary>
    [Preserve]
    public static class GltfAddonBootstrap
    {
      [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
      [Preserve]
      static void RegisterAddons()
      {
          ImportAddonRegistry.RegisterImportAddon(new FeatureIdImportAddon());
      }
    }
  }    