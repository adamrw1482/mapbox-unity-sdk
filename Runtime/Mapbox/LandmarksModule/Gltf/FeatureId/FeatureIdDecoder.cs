// SPDX-FileCopyrightText: 2024 Mapbox
// SPDX-License-Identifier: Apache-2.0

using Unity.Collections;
using UnityEngine;

namespace GLTFast.FeatureId
{
    /// <summary>
    /// Decodes packed _FEATURE_ID_RGBA4444 accessor data into Color arrays.
    /// The packed format: each vertex has a 4-byte (float-sized) value where
    /// lower 16 bits encode RGBA4444 color and upper 16 bits encode part ID.
    /// </summary>
    public static class FeatureIdDecoder
    {
        /// <summary>
        /// Decodes raw accessor bytes into Color array.
        /// </summary>
        /// <param name="data">Raw accessor byte data (SCALAR float, 4 bytes per element).</param>
        /// <param name="vertexCount">Number of vertices to decode.</param>
        /// <returns>Color array with decoded feature ID data (rgb = color, a = partId).</returns>
        public static Color[] Decode(NativeSlice<byte> data, int vertexCount)
        {
            var colors = new Color[vertexCount];

            for (int i = 0; i < vertexCount; i++)
            {
                int offset = i * 4;
                var intValue = (data[offset]) |
                               (data[offset + 1] << 8) |
                               (data[offset + 2] << 16) |
                               (data[offset + 3] << 24);

                var color = intValue & 0xffff;

                float r = ((color & 0xF000) | (color & 0xF000) >> 4) >> 8;
                float g = ((color & 0x0F00) | (color & 0x0F00) >> 4) >> 4;
                float b = (color & 0x00F0) | (color & 0x00F0) >> 4;

                var id = (intValue >> 16) & 0xffff;
                var pid = id & 0xf;

                colors[i] = new Color(r / 255f, g / 255f, b / 255f, pid);
            }

            return colors;
        }
    }
}
