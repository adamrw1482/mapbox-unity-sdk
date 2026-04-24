using System.Collections.Generic;

namespace Mapbox.BaseModule.Data.DataFetchers
{
	/// <summary>
	/// Size-keyed pool of <c>float[]</c> buffers used by terrain-RGB elevation extraction.
	/// Cuts the ~256 KB allocation per data tile decode down to a one-time cost per
	/// resolution: tiles that drop out of the memory cache return their backing array
	/// here, and the next decode rents it back instead of allocating fresh.
	/// </summary>
	/// <remarks>
	/// Thread-safe — async GPU readback callbacks fire on the main thread but the contract
	/// is conservative in case Unity changes that. Pool grows unbounded; in practice the
	/// only sizes seen are <c>256×256</c> (default texture) and <c>512×512</c> (retina).
	/// </remarks>
	public static class ElevationArrayPool
	{
		private static readonly Dictionary<int, Stack<float[]>> _pools = new Dictionary<int, Stack<float[]>>();

		/// <summary>
		/// Returns a <c>float[]</c> of exactly <paramref name="size"/> entries, either from
		/// the pool or freshly allocated. Contents are undefined; the caller is expected to
		/// overwrite every index.
		/// </summary>
		public static float[] Rent(int size)
		{
			lock (_pools)
			{
				if (_pools.TryGetValue(size, out var stack) && stack.Count > 0)
				{
					return stack.Pop();
				}
			}
			return new float[size];
		}

		/// <summary>
		/// Returns <paramref name="array"/> to the pool. No-op if null. The caller must not
		/// hold any references to <paramref name="array"/> after calling.
		/// </summary>
		public static void Return(float[] array)
		{
			if (array == null)
			{
				return;
			}
			lock (_pools)
			{
				if (!_pools.TryGetValue(array.Length, out var stack))
				{
					stack = new Stack<float[]>();
					_pools[array.Length] = stack;
				}
				stack.Push(array);
			}
		}
	}
}
