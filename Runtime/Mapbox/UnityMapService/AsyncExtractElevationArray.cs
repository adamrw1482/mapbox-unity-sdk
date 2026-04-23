using System;
using UnityEngine;
using UnityEngine.Rendering;
using TerrainData = Mapbox.BaseModule.Data.DataFetchers.TerrainData;

namespace Mapbox.UnityMapService
{
	/// <summary>
	/// Asynchronous terrain-RGB -> float[] elevation decoder using <c>AsyncGPUReadback</c>.
	/// Iterates the readback's NativeArray directly (no managed copy) and decodes each
	/// pixel via Mapbox's terrain-RGB formula.
	/// </summary>
	public class AsyncExtractElevationArray : IElevationDataExtractionStrategy
	{
		public void ExtractHeightData(Texture2D texture, Action<float[]> callback = null)
		{
			AsyncGPUReadback.Request(texture, 0, (t) =>
			{
				var width = t.width;
				var data = t.GetData<Color32>();
				var heightData = new float[width * width];

				int idx = 0;
				for (int y = 0; y < width; y++)
				{
					for (int x = 0; x < width; x++, idx++)
					{
						var c = data[idx];
						float r = c.g;
						float g = c.b;
						float b = c.a;
						heightData[idx] = -10000f + (r * 65536f + g * 256f + b) * 0.1f;
					}
				}

				callback?.Invoke(heightData);
			});
		}

		public void ExtractHeightData(TerrainData terrainData)
		{
			ExtractHeightData(terrainData.Texture, terrainData);
		}

		public void ExtractHeightData(Texture2D texture, TerrainData terrainData)
		{
			AsyncGPUReadback.Request(texture, 0, (t) =>
			{
				var width = t.width;
				var data = t.GetData<Color32>();
				var heightData = new float[width * width];
				var min = float.MaxValue;
				var max = float.MinValue;

				int idx = 0;
				for (int y = 0; y < width; y++)
				{
					for (int x = 0; x < width; x++, idx++)
					{
						var c = data[idx];
						float r = c.g;
						float g = c.b;
						float b = c.a;
						var value = -10000f + (r * 65536f + g * 256f + b) * 0.1f;
						heightData[idx] = value;
						if (value < min) min = value;
						if (value > max) max = value;
					}
				}

				terrainData.SetElevationValues(heightData, min, max);
			});
		}
	}
}
