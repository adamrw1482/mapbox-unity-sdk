using Mapbox.BaseModule.Data.DataFetchers;
using NUnit.Framework;

namespace Mapbox.BaseModuleTests
{
    /// <summary>
    /// Tests use unique sizes per test method to avoid cross-test contamination from
    /// the pool's static state.
    /// </summary>
    public class ElevationArrayPoolTests
    {
        [Test]
        public void Rent_AllocatesNewArrayOfRequestedSize_WhenPoolIsEmpty()
        {
            var array = ElevationArrayPool.Rent(1001);

            Assert.IsNotNull(array);
            Assert.AreEqual(1001, array.Length);
        }

        [Test]
        public void ReturnThenRent_ReusesSameBuffer()
        {
            var first = ElevationArrayPool.Rent(1002);
            ElevationArrayPool.Return(first);

            var second = ElevationArrayPool.Rent(1002);

            Assert.AreSame(first, second, "Pool should hand back the same buffer it just received.");
        }

        [Test]
        public void Rent_DifferentSizes_ReturnsDistinctBuffers()
        {
            var a = ElevationArrayPool.Rent(1003);
            var b = ElevationArrayPool.Rent(1004);

            Assert.AreNotSame(a, b);
            Assert.AreEqual(1003, a.Length);
            Assert.AreEqual(1004, b.Length);
        }

        [Test]
        public void Return_NullArray_IsNoOp()
        {
            // Should not throw.
            Assert.DoesNotThrow(() => ElevationArrayPool.Return(null));
        }

        [Test]
        public void Pool_CapsAtMaxPerSizeDepth_DroppingExcessToGC()
        {
            // MaxPerSizeDepth is 32 (private const). Push 40 distinct buffers of the
            // same size and confirm the pool gave us back at most 32 distinct buffers
            // by reference identity. The remaining 8 must have been dropped.
            const int size = 1005;
            const int pushCount = 40;

            var pushed = new float[pushCount][];
            for (int i = 0; i < pushCount; i++)
            {
                pushed[i] = new float[size];
                ElevationArrayPool.Return(pushed[i]);
            }

            int reusedCount = 0;
            for (int i = 0; i < pushCount; i++)
            {
                var rented = ElevationArrayPool.Rent(size);
                // Cleanup as we go so we don't leak into the next test.
                ElevationArrayPool.Return(rented);

                for (int j = 0; j < pushCount; j++)
                {
                    if (ReferenceEquals(rented, pushed[j]))
                    {
                        reusedCount++;
                        break;
                    }
                }
            }

            // Up to 32 of the pushed buffers should survive; the rest were dropped.
            Assert.LessOrEqual(reusedCount, 32, "Pool retained more buffers than its documented cap.");
            Assert.Greater(reusedCount, 0, "Pool should have retained at least one buffer for reuse.");
        }
    }
}
