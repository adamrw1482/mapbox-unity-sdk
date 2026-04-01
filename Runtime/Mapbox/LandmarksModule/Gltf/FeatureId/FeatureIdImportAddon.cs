// SPDX-FileCopyrightText: 2024 Mapbox
// SPDX-License-Identifier: Apache-2.0

using GLTFast.Addons;

namespace GLTFast.FeatureId
{
    /// <summary>
    /// Import add-on that enables feature ID extraction from glTF meshes
    /// with _FEATURE_ID_RGBA4444 custom vertex attributes.
    /// Register via ImportAddonRegistry.RegisterImportAddon(new FeatureIdImportAddon()).
    /// </summary>
    public class FeatureIdImportAddon : ImportAddon<FeatureIdImportAddonInstance> { }
}
