using System;
using System.Threading;
using Aardvark.Base;
using NUnit.Framework;

namespace Aardvark.Geometry.Tests
{
    [TestFixture, NonParallelizable]
    public class KeepAliveCacheTests
    {
        // Keep one registration alive between tests; do not depend on worker restart timing.
        private KeepAliveCache anchor;

        [OneTimeSetUp]
        public void Start() => anchor = new KeepAliveCache("cache tests anchor", 0);

        [OneTimeTearDown]
        public void Stop() => anchor?.Dispose();

        [Test]
        public void NullCommandsAreRejectedBeforeQueuingAndBothLiveCachesKeepProcessing()
        {
            using var receiver = new KeepAliveCache("null receiver", 16);
            using var other = new KeepAliveCache("null peer", 16);
            Assert.Multiple(() =>
            {
                foreach (var size in new long[] { 0, 1, 16 })
                {
                    var error = Assert.Throws<ArgumentNullException>(() => receiver.Add(null, size), $"Add(null, {size}) must throw synchronously");
                    if (error != null) Assert.That(error.ParamName, Is.EqualTo("value"), $"Add size={size}");
                }
                var removal = Assert.Throws<ArgumentNullException>(() => receiver.Remove(null), "Remove(null) must throw synchronously");
                if (removal != null) Assert.That(removal.ParamName, Is.EqualTo("value"));
            });
            Assert.That(receiver.ProcessedCount, Is.Zero, "rejected adds were not processed");
            Assert.That(receiver.CurrentSizeInBytes, Is.Zero);
            var receiverValue = new object();
            var otherValue = new object();
            for (var step = 1; step <= 3; step++)
            {
                receiver.Add(receiverValue, step);
                other.Add(otherValue, 2 * step);
                Await(receiver, step, step, $"receiver step={step}");
                Await(other, step, 2 * step, $"other live cache step={step}");
            }
        }

        [Test]
        public void SizeValidationPrecedesNullValidationWithoutPublishingCommands()
        {
            using var cache = new KeepAliveCache("size validation", 8);
            foreach (var value in new[] { null, new object() })
            foreach (var size in new[] { long.MinValue, -1L, 9L, long.MaxValue })
            {
                var context = $"value={(value == null ? "null" : "object")}, size={size}";
                var error = Assert.Throws<ArgumentOutOfRangeException>(() => cache.Add(value, size), context);
                Assert.That(error.ParamName, Is.EqualTo("sizeInBytes"), context);
            }
            foreach (var size in new[] { long.MinValue, -1L })
            {
                var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
                {
                    using var invalid = new KeepAliveCache("invalid capacity", size);
                });
                Assert.That(error.ParamName, Is.EqualTo("maxMemoryInBytes"), $"capacity={size}");
            }
            cache.Add(new object(), 8);
            Await(cache, 1, 8, "valid boundary after rejected sizes");
            using var zero = new KeepAliveCache("zero capacity", 0);
            zero.Add(string.Empty, 0);
            Await(zero, 1, 0, "zero-size value at zero capacity");
            zero.Flush();
            zero.Add(0, 0);
            Await(zero, 2, 0, "flush still accepts its non-null sentinel");
        }

        [Test]
        public void ValidAddRefreshRemoveAndFlushRetainTheirStateTransitions()
        {
            foreach (var value in new object[] { new object(), string.Empty, 0 })
            {
                using var cache = new KeepAliveCache("valid commands", 8);
                var other = new object();
                var fence = new object();
                var context = $"value type={value.GetType().Name}";
                cache.Add(value, 3);
                Await(cache, 1, 3, context + ", add");
                cache.Add(value, 5);
                Await(cache, 2, 5, context + ", refresh size");
                cache.Add(other, 2);
                Await(cache, 3, 7, context + ", second value");
                cache.Remove(value);
                Await(cache, 3, 2, context + ", remove");
                cache.Remove(value);
                cache.Remove(new object());
                cache.Add(other, 2);
                Await(cache, 4, 2, context + ", missing removes then refresh fence");
                cache.Flush();
                cache.Add(fence, 0);
                Await(cache, 5, 0, context + ", flush then zero-size fence");
                cache.Remove(other);
                cache.Add(fence, 0);
                Await(cache, 6, 0, context + ", flushed value is absent");
                cache.Add(value, 4);
                Await(cache, 7, 4, context + ", usable after flush");
                cache.Flush();
                cache.Flush();
                cache.Add(fence, 0);
                Await(cache, 8, 0, context + ", repeated flush");
            }
        }

        [Test]
        public void EvictionRetainsRefreshedValuesAndRemovingEvictedValuesIsHarmless()
        {
            using var cache = new KeepAliveCache("eviction", 6);
            var first = new object();
            var second = new object();
            var third = new object();
            cache.Add(first, 3);
            Await(cache, 1, 3, "first");
            cache.Add(second, 3);
            Await(cache, 2, 6, "second");
            cache.Add(first, 3);
            Await(cache, 3, 6, "refresh first makes second oldest");
            cache.Add(third, 3);
            Await(cache, 4, 6, "evict second");
            cache.Remove(second);
            cache.Add(third, 3);
            Await(cache, 5, 6, "evicted second was already absent");
            cache.Remove(first);
            Await(cache, 5, 3, "refreshed first was retained");
            cache.Remove(third);
            Await(cache, 5, 0, "last value removed");
        }

        private static void Await(KeepAliveCache cache, long adds, long size, string context)
            => Assert.That(SpinWait.SpinUntil(() => cache.ProcessedCount == adds && cache.CurrentSizeInBytes == size,
                    TimeSpan.FromSeconds(10)), Is.True,
                $"{context}: expected adds={adds}, size={size}; actual adds={cache.ProcessedCount}, size={cache.CurrentSizeInBytes}");
    }
}
