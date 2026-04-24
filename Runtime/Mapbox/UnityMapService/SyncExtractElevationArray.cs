using System;
using Mapbox.BaseModule.Data.DataFetchers;
using Unity.Collections;
using UnityEngine;
using TerrainData = Mapbox.BaseModule.Data.DataFetchers.TerrainData;

namespace Mapbox.UnityMapService
{
	/// <summary>
	/// Synchronous terrain-RGB -> float[] elevation decoder. Used on platforms that don't
	/// support <c>AsyncGPUReadback</c>. Iterates the raw texture bytes in-place (no byte[]
	/// copy) and decodes each pixel via Mapbox's terrain-RGB formula.
	/// </summary>
	public class SyncExtractElevationArray : IElevationDataExtractionStrategy
	{
		public void ExtractHeightData(Texture2D texture, Action<float[]> callback)
		{
			var rgbData = texture.GetRawTextureData<byte>();
			var width = texture.width;
			var heightData = ElevationArrayPool.Rent(width * width);

			int idx = 0;
			for (int y = 0; y < width; y++)
			{
				for (int x = 0; x < width; x++, idx++)
				{
					int rgbIdx = idx * 4;
					float r = rgbData[rgbIdx + 1];
					float g = rgbData[rgbIdx + 2];
					float b = rgbData[rgbIdx + 3];
					// Mapbox terrain-RGB decode inlined for the hot loop.
					heightData[idx] = -10000f + (r * 65536f + g * 256f + b) * 0.1f;
				}
			}
			callback?.Invoke(heightData);
		}

		public void ExtractHeightData(TerrainData terrainData)
		{
			ExtractHeightData(terrainData.Texture, terrainData);
		}

		public void ExtractHeightData(Texture2D texture, TerrainData terrainData)
		{
			var rgbData = texture.GetRawTextureData<byte>();
			var width = texture.width;
			var heightData = ElevationArrayPool.Rent(width * width);
			var min = float.MaxValue;
			var max = float.MinValue;

			int idx = 0;
			for (int y = 0; y < width; y++)
			{
				for (int x = 0; x < width; x++, idx++)
				{
					int rgbIdx = idx * 4;
					float r = rgbData[rgbIdx + 1];
					float g = rgbData[rgbIdx + 2];
					float b = rgbData[rgbIdx + 3];
					var value = -10000f + (r * 65536f + g * 256f + b) * 0.1f;
					heightData[idx] = value;
					if (value < min) min = value;
					if (value > max) max = value;
				}
			}
			terrainData.SetElevationValues(heightData, min, max);
		}
	}
}
