// SPDX-FileCopyrightText: 2024 Mapbox
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GLTFast.Addons;
using GLTFast.FeatureId.Schema;
using Newtonsoft.Json.Linq;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using Mesh = UnityEngine.Mesh;

namespace GLTFast.FeatureId
{
    /// <summary>
    /// ImportAddonInstance that extracts _FEATURE_ID_RGBA4444 data during loading
    /// and applies it to meshes as vertex colors during instantiation.
    /// Uses the low-level mesh API to properly add Color as a new vertex stream,
    /// since glTFast creates meshes with SetVertexBufferParams/SetVertexBufferData.
    /// </summary>
    public class FeatureIdImportAddonInstance : ImportAddonInstance
    {
        const MeshUpdateFlags k_MeshUpdateFlags =
            MeshUpdateFlags.DontRecalculateBounds |
            MeshUpdateFlags.DontValidateIndices |
            MeshUpdateFlags.DontNotifyMeshUsers |
            MeshUpdateFlags.DontResetBoneBounds;

        GltfImportBase m_GltfImport;

        // Cached decoded feature ID colors: key = (meshIndex, primitiveIndex), value = Color[]
        Dictionary<(int meshIndex, int primitiveIndex), Color[]> m_CachedColors;

        /// <inheritdoc />
        public override bool SupportsGltfExtension(string extensionName)
        {
            return false;
        }

        /// <inheritdoc />
        public override void Inject(GltfImportBase gltfImport)
        {
            m_GltfImport = gltfImport;
            gltfImport.AddImportAddonInstance(this);
        }

        /// <inheritdoc />
        public override void Inject(IInstantiator instantiator)
        {
            if (m_CachedColors == null || m_CachedColors.Count == 0)
                return;

            if (instantiator is GameObjectInstantiator goInstantiator)
            {
                goInstantiator.MeshAdded += OnMeshAdded;
            }
        }

        /// <summary>
        /// Called after loading is complete but before volatile buffer data is disposed.
        /// Extracts and caches feature ID accessor data needed during instantiation.
        /// </summary>
        /// <param name="gltfReadable">Read-only access to the glTF data while buffers are still available.</param>
        public void OnLoadCompleted(IGltfReadable gltfReadable)
        {
            m_CachedColors = new Dictionary<(int, int), Color[]>();

            for (int meshIndex = 0; ; meshIndex++)
            {
                var mesh = gltfReadable.GetSourceMesh(meshIndex);
                if (mesh == null)
                    break;

                var primitives = mesh.Primitives;
                if (primitives == null)
                    continue;

                for (int primIndex = 0; primIndex < primitives.Count; primIndex++)
                {
                    if (primitives[primIndex] is not FeatureIdMeshPrimitive primitive)
                        continue;

                    var additionalAttrs = primitive.attributes?.AdditionalAttributes;
                    if (additionalAttrs == null
                        || !additionalAttrs.TryGetValue("_FEATURE_ID_RGBA4444", out var token))
                        continue;

                    var featureIdAccessor = token.Value<int>();
                    if (featureIdAccessor < 0)
                        continue;

                    var data = gltfReadable.GetAccessorData(featureIdAccessor);
                    if (data.Length == 0)
                        continue;

                    int vertexCount = data.Length / 4; // SCALAR float = 4 bytes per vertex
                    var colors = FeatureIdDecoder.Decode(data, vertexCount);
                    m_CachedColors[(meshIndex, primIndex)] = colors;
                }
            }
        }

        void OnMeshAdded(
            GameObject gameObject,
            uint nodeIndex,
            string meshName,
            MeshResult meshResult,
            uint[] joints = null,
            uint? rootJoint = null,
            float[] morphTargetWeights = null,
            int meshNumeration = 0)
        {
            if (m_CachedColors == null)
                return;

            var mesh = meshResult.mesh;
            if (mesh == null)
                return;

            var primitiveIndices = meshResult.primitiveIndices;
            if (primitiveIndices == null || primitiveIndices.Length == 0)
                return;

            // Check if any primitive in this mesh has feature ID data
            bool hasAnyFeatureId = false;
            for (int i = 0; i < primitiveIndices.Length; i++)
            {
                if (m_CachedColors.ContainsKey((meshResult.meshIndex, primitiveIndices[i])))
                {
                    hasAnyFeatureId = true;
                    break;
                }
            }
            if (!hasAnyFeatureId)
                return;

            // Build combined color array from all primitives
            int totalVertices = mesh.vertexCount;
            var combinedColors = new Color[totalVertices];
            int vertexOffset = 0;

            for (int i = 0; i < primitiveIndices.Length; i++)
            {
                var key = (meshResult.meshIndex, primitiveIndices[i]);
                if (m_CachedColors.TryGetValue(key, out var primColors))
                {
                    int count = Mathf.Min(primColors.Length, totalVertices - vertexOffset);
                    Array.Copy(primColors, 0, combinedColors, vertexOffset, count);
                    vertexOffset += count;
                }
                else
                {
                    // No feature ID for this primitive — skip its vertices
                    if (i < mesh.subMeshCount)
                    {
                        var subMesh = mesh.GetSubMesh(i);
                        vertexOffset += subMesh.vertexCount;
                    }
                }
            }

            ApplyColorsToMesh(mesh, combinedColors);
        }

        /// <summary>
        /// Applies Color data to a mesh that was created with the low-level mesh API
        /// (SetVertexBufferParams/SetVertexBufferData). Reads back existing vertex data,
        /// adds Color as a new vertex stream, and re-uploads all data.
        /// </summary>
        static void ApplyColorsToMesh(Mesh mesh, Color[] colors)
        {
            // Read existing vertex layout
            var existingAttrs = mesh.GetVertexAttributes();

            // Find max existing stream index
            int maxStream = 0;
            bool hasExistingColor = false;
            int existingColorStream = -1;

            for (int i = 0; i < existingAttrs.Length; i++)
            {
                if (existingAttrs[i].stream > maxStream)
                    maxStream = existingAttrs[i].stream;
                if (existingAttrs[i].attribute == VertexAttribute.Color)
                {
                    hasExistingColor = true;
                    existingColorStream = existingAttrs[i].stream;
                }
            }

            if (hasExistingColor)
            {
                // Color attribute already exists — just overwrite its stream data
                var nativeColors = new NativeArray<Color>(colors, Allocator.Temp);
                mesh.SetVertexBufferData(nativeColors, 0, 0, nativeColors.Length,
                    existingColorStream, k_MeshUpdateFlags);
                nativeColors.Dispose();
                return;
            }

            // Color doesn't exist — need to add it as a new stream.
            // Read back all existing vertex data before re-setting layout.
            var readMeshData = Mesh.AcquireReadOnlyMeshData(mesh);
            var rd = readMeshData[0];

            // Cache existing stream data
            var streamDataCache = new NativeArray<byte>[maxStream + 1];
            for (int s = 0; s <= maxStream; s++)
            {
                var streamData = rd.GetVertexData<byte>(s);
                streamDataCache[s] = new NativeArray<byte>(streamData.Length, Allocator.Temp);
                NativeArray<byte>.Copy(streamData, streamDataCache[s]);
            }

            readMeshData.Dispose();

            // Build new attribute array with Color added on a new stream
            int colorStream = maxStream + 1;
            var newAttrs = new VertexAttributeDescriptor[existingAttrs.Length + 1];
            Array.Copy(existingAttrs, newAttrs, existingAttrs.Length);
            newAttrs[existingAttrs.Length] = new VertexAttributeDescriptor(
                VertexAttribute.Color, VertexAttributeFormat.Float32, 4, colorStream);

            // Re-set vertex layout (clears all vertex data)
            mesh.SetVertexBufferParams(mesh.vertexCount, newAttrs);

            // Re-upload existing stream data
            for (int s = 0; s <= maxStream; s++)
            {
                mesh.SetVertexBufferData(streamDataCache[s], 0, 0,
                    streamDataCache[s].Length, s, k_MeshUpdateFlags);
                streamDataCache[s].Dispose();
            }

            // Upload color data to the new stream
            var nativeColorData = new NativeArray<Color>(colors, Allocator.Temp);
            mesh.SetVertexBufferData(nativeColorData, 0, 0, nativeColorData.Length,
                colorStream, k_MeshUpdateFlags);
            nativeColorData.Dispose();
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            m_CachedColors?.Clear();
            m_CachedColors = null;
        }
    }
}
