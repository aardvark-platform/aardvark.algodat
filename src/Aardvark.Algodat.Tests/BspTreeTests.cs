using Aardvark.Base;
using Aardvark.Base.Coder;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TypeInfo = Aardvark.Base.Coder.TypeInfo;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class BspTreeTests
    {
        private const int Sentinel = int.MinValue;
        private static readonly TimeSpan CompletionTimeout = TimeSpan.FromSeconds(10);

        // SplitTriangles serialized before this repair (7c9ea62). Keep both historical
        // node/index units covered, not merely round trips written by the new code.
        private const string VertexLayout =
            "CEFhcmR2YXJrBAAAAB4BAAAAAAAAB0JzcFRyZWUAAAAACgEAAAAAAAABYgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAADwPwEAAAAAAAAABgAAAAFiAAAAAAAA4D8AAAAAAAAAAAAAAAAAAAAAVVVVVVVV5b9VVVVVVVXlv1VVVVVVVdU/AgAAAAYAAAAJAAAAAAAAAAFuAAAAAAFuAwAAAAFiAAAAAAAAAAAAAAAAAAAAAAAAAAAAAPC/VVVVVVVV5b9VVVVVVVXlv1VVVVVVVdU/AQAAAAMAAAAAAAAAAW4AAAAAAW4MAAAAAAAAAAEAAAACAAAAAwAAAAYAAAAHAAAABgAAAAQAAAAFAAAABgAAAAUAAAAHAAAA/////w==";
        private const string AttributeLayout =
            "CEFhcmR2YXJrBAAAAC4BAAAAAAAAB0JzcFRyZWUAAAAAGgEAAAAAAAABYgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAADwPwEAAAAAAAAAAgAAAAFiAAAAAAAA4D8AAAAAAAAAAAAAAAAAAAAAVVVVVVVV5b9VVVVVVVXlv1VVVVVVVdU/AgAAAAIAAAADAAAAAAAAAAFuAAAAAAFuAQAAAAFiAAAAAAAAAAAAAAAAAAAAAAAAAAAAAPC/VVVVVVVV5b9VVVVVVVXlv1VVVVVVVdU/AQAAAAEAAAAAAAAAAW4AAAAAAW4MAAAAAAAAAAEAAAACAAAAAwAAAAYAAAAHAAAABgAAAAQAAAAFAAAABgAAAAUAAAAHAAAABAAAAAoAAAAUAAAAFAAAABQAAAA=";

        // Pin the root alias locally: assembly discovery/global coder registration
        // differs between isolated tests, full-suite runs and coverage instrumentation.
        private static readonly TypeInfo[] TreeTypeInfo = { new TypeInfo("BspTree", typeof(BspTree)) };

        private static byte[] EncodeTree(BspTree tree)
        {
            using var stream = new MemoryStream();
            using (var coder = new BinaryWritingCoder(stream))
            {
                coder.Add(TreeTypeInfo);
                coder.CodeT(ref tree);
            }
            return stream.ToArray();
        }

        private static BspTree DecodeTree(byte[] encoded)
        {
            using var stream = new MemoryStream(encoded);
            using var coder = new BinaryReadingCoder(stream);
            coder.Add(TreeTypeInfo);
            BspTree tree = null;
            coder.CodeT(ref tree);
            return tree;
        }

        private static BspTreeBuilder ParallelTriangles(bool attributes, params double[] heights)
        {
            var positions = heights.SelectMany(z => new[]
            {
                new V3d(0, 0, z), new V3d(1, 0, z), new V3d(0, 1, z)
            }).ToArray();
            var indices = Enumerable.Range(0, positions.Length).ToArray();
            return attributes
                ? new BspTreeBuilder(indices, positions, 1e-9,
                    Enumerable.Range(1, heights.Length).Select(i => i * 10).ToArray())
                : new BspTreeBuilder(indices, positions, 1e-9);
        }

        private static BspTreeBuilder SplitTriangles(bool attributes)
        {
            return new BspTreeBuilder(new[] { 0, 1, 2, 3, 4, 5 }, new[]
            {
                new V3d(0, 0, 0), new V3d(1, 0, 0), new V3d(0, 1, 0),
                new V3d(0, 0, -1), new V3d(1, 0, 1), new V3d(0, 1, 1)
            }, 1e-9, attributes ? new[] { 10, 20 } : null);
        }

        private static bool Ascending(BspTree.Order order, double eyeZ)
            => (order == BspTree.Order.BackToFront) == (eyeZ >= 0);

        private static void WaitForCompletion(CountdownEvent finished, bool parallel)
        {
            // Never hang the test runner when a worker fails to signal completion.
            if (!parallel) Assert.That(finished.IsSet, Is.True, "Serial sorts must finish before returning.");
            Assert.That(finished.Wait(CompletionTimeout), Is.True, "BSP sort did not complete.");
            Assert.That(finished.CurrentCount, Is.Zero);
        }

        private static void AssertSort(BspTree tree, int vertexCount, bool paired,
            BspTree.Order order, double eyeZ, bool parallel, int[] expectedVertices, int[] expectedAttributes)
        {
            // Oversized, poisoned buffers also detect unwritten slots and writes past the final count.
            var vertices = Enumerable.Repeat(Sentinel, vertexCount + 3).ToArray();
            var attributes = Enumerable.Repeat(Sentinel, vertexCount / 3 + 2).ToArray();
            var eye = new V3d(0.25, 0.25, eyeZ);
            using var finished = paired
                ? tree.SortVertexAndAttributeIndexArrays(order, eye, vertices, attributes, parallel)
                : tree.SortVertexIndexArray(order, eye, vertices, parallel);
            WaitForCompletion(finished, parallel);
            Assert.That(vertices.Take(vertexCount), Is.EqualTo(expectedVertices));
            Assert.That(vertices.Skip(vertexCount), Is.All.EqualTo(Sentinel));
            Assert.That(attributes.Take(vertexCount / 3), paired
                ? Is.EqualTo(expectedAttributes)
                : Is.All.EqualTo(Sentinel));
            Assert.That(attributes.Skip(vertexCount / 3), Is.All.EqualTo(Sentinel));
        }

        [Test]
        public void BuilderReportsAttributePresence([Values] bool attributes)
        {
            var builder = ParallelTriangles(attributes, 0, 1);
            Assert.That(builder.HasAttributeArray, Is.EqualTo(attributes));
            Assert.That(builder.TriangleCountMul3, Is.EqualTo(6));
            _ = builder.BspTree;
            Assert.That(builder.HasAttributeArray, Is.EqualTo(attributes));
        }

        [Test, Combinatorial]
        public void TwoParallelTrianglesSortAcrossLayouts(
            [Values] bool attributes, [Values] bool paired, [Values] BspTree.Order order,
            [Values] bool parallel, [Values(-2.0, 2.0, 0.0)] double eyeZ)
        {
            // Regression: indices [0,1,2,3,4,5], optional IDs [10,20], at z=0 and z=1.
            var builder = ParallelTriangles(attributes, 0, 1);
            var tree = builder.BspTree;
            var ascending = Ascending(order, eyeZ);
            AssertSort(tree, builder.TriangleCountMul3, paired, order, eyeZ, parallel,
                ascending ? new[] { 0, 1, 2, 3, 4, 5 } : new[] { 3, 4, 5, 0, 1, 2 },
                !attributes ? new[] { 0, 0 } : ascending ? new[] { 10, 20 } : new[] { 20, 10 });
        }

        [Test, Combinatorial]
        public void CoplanarTrianglesAndBothChildSubtreesSortAcrossLayouts(
            [Values] bool attributes, [Values] bool paired, [Values] BspTree.Order order,
            [Values] bool parallel, [Values(-3.0, 3.0)] double eyeZ)
        {
            // The root contains triangles 0 and 1; both children have descendants.
            var builder = ParallelTriangles(attributes, 0, 0, -1, 1, -2, 2);
            var tree = builder.BspTree;
            var triangleOrder = Ascending(order, eyeZ)
                ? new[] { 4, 2, 0, 1, 3, 5 }
                : new[] { 5, 3, 0, 1, 2, 4 };
            AssertSort(tree, builder.TriangleCountMul3, paired, order, eyeZ, parallel,
                triangleOrder.SelectMany(i => new[] { 3 * i, 3 * i + 1, 3 * i + 2 }).ToArray(),
                triangleOrder.Select(i => attributes ? (i + 1) * 10 : 0).ToArray());
        }

        [Test, Combinatorial]
        public void SplitFragmentsKeepTheirAttributesAcrossLayouts(
            [Values] bool attributes, [Values] bool paired, [Values] BspTree.Order order,
            [Values] bool parallel, [Values(-2.0, 2.0)] double eyeZ)
        {
            var builder = SplitTriangles(attributes);
            Assert.That(builder.TriangleCountMul3, Is.EqualTo(12));
            Assert.That(builder.VertexCount, Is.EqualTo(8));
            Assert.That(builder.PositionArray[6], Is.EqualTo(new V3d(0.5, 0, 0)));
            Assert.That(builder.PositionArray[7], Is.EqualTo(new V3d(0, 0.5, 0)));
            var tree = builder.BspTree;
            var ascending = Ascending(order, eyeZ);
            AssertSort(tree, builder.TriangleCountMul3, paired, order, eyeZ, parallel,
                ascending
                    ? new[] { 3, 6, 7, 0, 1, 2, 6, 4, 5, 6, 5, 7 }
                    : new[] { 6, 4, 5, 6, 5, 7, 0, 1, 2, 3, 6, 7 },
                !attributes ? new[] { 0, 0, 0, 0 }
                    : ascending ? new[] { 20, 10, 20, 20 } : new[] { 20, 20, 10, 20 });
        }

        [Test, Combinatorial]
        public void SerializationPreservesBothPackedLayouts(
            [Values] bool attributes, [Values] bool paired, [Values] BspTree.Order order,
            [Values] bool parallel, [Values(-2.0, 2.0)] double eyeZ)
        {
            var builder = SplitTriangles(attributes);
            var tree = builder.BspTree;
            var encoded = Convert.FromBase64String(attributes ? AttributeLayout : VertexLayout);
            Assert.That(EncodeTree(tree), Is.EqualTo(encoded), "The serialized representation must remain unchanged.");
            var decoded = DecodeTree(encoded);
            Assert.That(EncodeTree(decoded), Is.EqualTo(encoded));
            var ascending = Ascending(order, eyeZ);
            AssertSort(decoded, builder.TriangleCountMul3, paired, order, eyeZ, parallel,
                ascending
                    ? new[] { 3, 6, 7, 0, 1, 2, 6, 4, 5, 6, 5, 7 }
                    : new[] { 6, 4, 5, 6, 5, 7, 0, 1, 2, 3, 6, 7 },
                !attributes ? new[] { 0, 0, 0, 0 }
                    : ascending ? new[] { 20, 10, 20, 20 } : new[] { 20, 20, 10, 20 });
            Assert.That(EncodeTree(decoded), Is.EqualTo(encoded), "Sorting must not mutate the packed tree.");
        }

        [Test]
        public void RepeatedAndConcurrentSortsAgreeBetweenApis([Values] bool attributes)
        {
            var builder = SplitTriangles(attributes);
            var tree = builder.BspTree;
            var encoded = tree.Encode();
            for (var repeat = 0; repeat < 3; repeat++)
            {
                var jobs = Enumerable.Range(0, 32).Select(i => Task.Run(() =>
                {
                    var order = (i & 1) == 0 ? BspTree.Order.BackToFront : BspTree.Order.FrontToBack;
                    var eye = new V3d(0.25, 0.25, (i & 2) == 0 ? 2 : -2);
                    var parallel = (i & 4) == 0;
                    // Exactly-sized buffers, independent for every outstanding call.
                    var vertices = Enumerable.Repeat(Sentinel, builder.TriangleCountMul3).ToArray();
                    var pairedVertices = Enumerable.Repeat(Sentinel, vertices.Length).ToArray();
                    var ids = Enumerable.Repeat(Sentinel, vertices.Length / 3).ToArray();
                    using var vertexFinished = tree.SortVertexIndexArray(order, eye, vertices, parallel);
                    using var pairedFinished = tree.SortVertexAndAttributeIndexArrays(order, eye, pairedVertices, ids, parallel);
                    WaitForCompletion(vertexFinished, parallel);
                    WaitForCompletion(pairedFinished, parallel);
                    Assert.That(vertices, Is.EqualTo(pairedVertices));
                    Assert.That(vertices, Does.Not.Contain(Sentinel));
                    for (var j = 0; j < ids.Length; j++)
                        Assert.That(ids[j], Is.EqualTo(!attributes ? 0 : vertices[j * 3] == 0 ? 10 : 20));
                })).ToArray();
                Assert.That(Task.WaitAll(jobs, TimeSpan.FromSeconds(30)), Is.True, "Concurrent sorts did not complete.");
            }
            Assert.That(tree.Encode(), Is.EqualTo(encoded));
        }

        [Test, Combinatorial]
        public void NullAttributeOutputAgreesWithVertexOnlyApi(
            [Values] bool attributes, [Values] BspTree.Order order, [Values] bool parallel)
        {
            var builder = SplitTriangles(attributes);
            var tree = builder.BspTree;
            var vertices = new int[builder.TriangleCountMul3];
            var pairedVertices = new int[vertices.Length];
            using var vertexFinished = tree.SortVertexIndexArray(order, new V3d(0, 0, 2), vertices, parallel);
            using var pairedFinished = tree.SortVertexAndAttributeIndexArrays(order, new V3d(0, 0, 2), pairedVertices, null, parallel);
            WaitForCompletion(vertexFinished, parallel);
            WaitForCompletion(pairedFinished, parallel);
            Assert.That(pairedVertices, Is.EqualTo(vertices));
        }

        [Test, Combinatorial]
        public void ParallelSortDoesNotDependOnCallingTaskScheduler(
            [Values] bool attributes, [Values] bool paired, [Values] BspTree.Order order)
        {
            var builder = ParallelTriangles(attributes, 0, 1);
            var tree = builder.BspTree;
            var scheduler = new ConcurrentExclusiveSchedulerPair(TaskScheduler.Default, maxConcurrencyLevel: 1);
            try
            {
                // Waiting inside an exclusive task must not prevent root/child workers
                // from running. Parallel sorts use the default scheduler instead.
                var job = Task.Factory.StartNew(() =>
                {
                    var ascending = order == BspTree.Order.BackToFront;
                    AssertSort(tree, builder.TriangleCountMul3, paired, order, 2, true,
                        ascending ? new[] { 0, 1, 2, 3, 4, 5 } : new[] { 3, 4, 5, 0, 1, 2 },
                        !attributes ? new[] { 0, 0 } : ascending ? new[] { 10, 20 } : new[] { 20, 10 });
                }, CancellationToken.None, TaskCreationOptions.None, scheduler.ExclusiveScheduler);
                Assert.That(job.Wait(TimeSpan.FromSeconds(30)), Is.True);
            }
            finally
            {
                scheduler.Complete();
                Assert.That(scheduler.Completion.Wait(TimeSpan.FromSeconds(30)), Is.True);
            }
        }

        [Test, Combinatorial]
        public void LargeSubtreesCompleteAcrossTaskChunkBoundary(
            [Values] bool attributes, [Values] bool paired, [Values] BspTree.Order order,
            [Values] bool parallel)
        {
            // Both layouts exceed the 32768-unit task threshold and have smaller
            // descendants that must be scheduled. Coplanar groups keep construction shallow.
            const int count = 70000;
            var levels = new[] { -4.0, 4.0, -2.0, 2.0, -3.0, 3.0, -1.0, 1.0 };
            var heights = Enumerable.Range(0, count).Select(i => i == 0 ? 0.0 : levels[i % levels.Length]).ToArray();
            var builder = ParallelTriangles(attributes, heights);
            var tree = builder.BspTree;
            var vertices = Enumerable.Repeat(Sentinel, builder.TriangleCountMul3).ToArray();
            var ids = Enumerable.Repeat(Sentinel, count).ToArray();
            using var finished = paired
                ? tree.SortVertexAndAttributeIndexArrays(order, new V3d(0, 0, 5), vertices, ids, parallel)
                : tree.SortVertexIndexArray(order, new V3d(0, 0, 5), vertices, parallel);
            WaitForCompletion(finished, parallel);
            Assert.That(vertices.OrderBy(i => i), Is.EqualTo(Enumerable.Range(0, 3 * count)));
            double previous = order == BspTree.Order.BackToFront ? double.NegativeInfinity : double.PositiveInfinity;
            for (var i = 0; i < count; i++)
            {
                var triangle = vertices[3 * i] / 3;
                Assert.That(vertices[3 * i + 1], Is.EqualTo(3 * triangle + 1));
                Assert.That(vertices[3 * i + 2], Is.EqualTo(3 * triangle + 2));
                if (paired) Assert.That(ids[i], Is.EqualTo(attributes ? (triangle + 1) * 10 : 0));
                var z = heights[triangle];
                Assert.That(order == BspTree.Order.BackToFront ? z >= previous : z <= previous, Is.True);
                previous = z;
            }
        }
    }
}
