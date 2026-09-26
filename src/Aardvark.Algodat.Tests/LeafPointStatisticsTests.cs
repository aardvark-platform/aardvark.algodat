/*
    Copyright (C) 2006-2025. Aardvark Platform Team. http://github.com/aardvark-platform.
    This program is free software under the GNU Affero General Public License, version 3.
*/
using Aardvark.Base;
using Aardvark.Data;
using Aardvark.Data.Points;
using Aardvark.Geometry.Points;
using NUnit.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class LeafPointStatisticsTests
    {
        private static double Statistic(IPointCloudNode node, bool outOfCore, int operation) => operation switch
        {
            0 => node.GetMinimumLeafPointCount(outOfCore),
            1 => node.GetMaximumLeafPointCount(outOfCore),
            _ => node.GetAverageLeafPointCount(outOfCore)
        };

        private static void AssertStatistics(IPointCloudNode node, bool outOfCore, IEnumerable<int> counts)
        {
            var xs = counts.ToArray();
            Assert.That(node.GetMinimumLeafPointCount(outOfCore), Is.EqualTo(xs.Length == 0 ? long.MaxValue : xs.Min(x => (long)x)));
            Assert.That(node.GetMaximumLeafPointCount(outOfCore), Is.EqualTo(xs.Length == 0 ? long.MinValue : xs.Max(x => (long)x)));
            var expected = xs.Length == 0 ? double.NaN : xs.Sum(x => (long)x) / (double)xs.Length;
            Assert.That(node.GetAverageLeafPointCount(outOfCore), Is.EqualTo(expected));
        }

        [Test]
        public void SingleLeafUsesItsLocalCount([Values(0, 1, 512, int.MaxValue)] int count, [Values(false, true)] bool outOfCore, [Range(0, 2)] int operation)
        {
            var node = Probe.Create(count);
            Assert.That(Statistic(node.Node, outOfCore, operation), Is.EqualTo(count));
            Assert.That(node.SubnodeReads, Is.EqualTo(1));
            Assert.That(node.CountReads, Is.EqualTo(1));
        }

        [Test]
        public void EmptyPhysicalLeafContributesZero([Values(false, true)] bool outOfCore)
        {
            AssertStatistics(PointSetNode.Empty, outOfCore, new[] { 0 });
        }

        [Test]
        public void UnevenTreeExcludesLodAndWeightsIndividualLeaves([Values(false, true)] bool outOfCore)
        {
            var root = Probe.Create(987654,
                Ref(Probe.Create(3)), null,
                Ref(Probe.Create(100000, Ref(Probe.Create(11)), Ref(Probe.Create(200000, Ref(Probe.Create(2)), null, Ref(Probe.Create(7)))))),
                null, Ref(Probe.Create(0)));
            AssertStatistics(root.Node, outOfCore, new[] { 3, 11, 2, 7, 0 });
        }

        [Test]
        public void AccumulatesLeafCountsInInt64([Values(false, true)] bool outOfCore)
        {
            var root = Probe.Create(1, Ref(Probe.Create(int.MaxValue)), Ref(Probe.Create(int.MaxValue - 1)), Ref(Probe.Create(7)));
            AssertStatistics(root.Node, outOfCore, new[] { int.MaxValue, int.MaxValue - 1, 7 });
        }

        [Test]
        public void NoReachedLeavesHaveSentinelsAndUndefinedAverage([Values(false, true)] bool outOfCore, [Values(false, true)] bool nested)
        {
            var inner = Probe.Create(999, new Reference[8]);
            var root = nested ? Probe.Create(888, Ref(inner)) : inner;
            AssertStatistics(root.Node, outOfCore, Array.Empty<int>());
        }

        [Test]
        public void ResolutionUsesTheRequestedReferenceCallback([Values(false, true)] bool outOfCore, [Range(0, 2)] int operation)
        {
            var present = Ref(Probe.Create(2));
            var unavailable = Ref(Probe.Create(10), cached: false);
            var deep = Ref(Probe.Create(900, Ref(Probe.Create(8)), Ref(Probe.Create(12), cached: false)));
            var root = Probe.Create(1000, present, null, unavailable, deep);
            var expected = outOfCore ? new[] { 2, 10, 8, 12 } : new[] { 2, 8 };
            var value = operation == 0 ? expected.Min() : operation == 1 ? expected.Max() : expected.Average();
            Assert.That(Statistic(root.Node, outOfCore, operation), Is.EqualTo(value));
            foreach (var r in new[] { present, unavailable, deep })
            {
                Assert.That(r.ValueCalls, Is.EqualTo(outOfCore ? 1 : 0));
                Assert.That(r.TryCalls, Is.EqualTo(outOfCore ? 0 : 1));
            }
        }

        [Test]
        public void UnavailableSubtreesAreNotLeaves([Range(0, 2)] int operation)
        {
            var missing = Ref(Probe.Create(200, Ref(Probe.Create(5))), cached: false);
            var root = Probe.Create(600, missing);
            var actual = Statistic(root.Node, false, operation);
            Assert.That(actual, Is.EqualTo(operation == 0 ? (double)long.MaxValue : operation == 1 ? (double)long.MinValue : double.NaN));
            Assert.That(missing.ValueCalls, Is.Zero);
            Assert.That(missing.TryCalls, Is.EqualTo(1));
        }

        [Test]
        public void FailedValueResolutionIsNotSilentlySkipped([Range(0, 2)] int operation)
        {
            var failing = Ref(Probe.Create(3), cached: false);
            failing.Failure = new IOException("unavailable child");
            var root = Probe.Create(500, failing);
            Assert.That(Assert.Throws<IOException>(() => Statistic(root.Node, true, operation)), Is.SameAs(failing.Failure));
        }

        [Test]
        public void SeededTreesMatchIndependentLeafEnumeration([Range(0, 31)] int seed, [Values(false, true)] bool outOfCore)
        {
            var random = new Random(seed);
            Probe Build(int depth)
            {
                if (depth == 0 || random.Next(4) == 0) return Probe.Create(random.Next(100));
                var children = new Reference[8];
                for (var i = 0; i < 8; i++)
                    if (random.Next(3) == 0) children[i] = Ref(Build(depth - 1), random.Next(4) != 0);
                return Probe.Create(10000 + random.Next(5000), children);
            }
            var root = Build(4);
            AssertStatistics(root.Node, outOfCore, Oracle(root, outOfCore));
        }

        [Test]
        public void Reproduction512Points([Values(false, true)] bool outOfCore, [Range(0, 2)] int operation)
        {
            using var bytes = new ByteStore();
            using var storage = bytes.Open(false);
            var positions = (from x in Enumerable.Range(0, 8) from y in Enumerable.Range(0, 8) from z in Enumerable.Range(0, 8)
                             select new V3d((x + 0.125) / 8, (y + 0.125) / 8, (z + 0.125) / 8)).ToArray();
            var root = PointSet.Create(storage, Guid.NewGuid().ToString(), positions, null, null, null, null, null, 8, true, false).Root.Value;
            Assert.That(root.PointCountTree, Is.EqualTo(512));
            var leaves = EnumerateLeafCounts(root).ToArray();
            Assert.That(leaves.Length, Is.EqualTo(64));
            Assert.That(leaves, Is.All.EqualTo(8));
            Assert.That(Statistic(root, outOfCore, operation), Is.EqualTo(8));
        }

        [Test]
        public void PersistedUnevenTreesAndDetachedRoots([Values(false, true)] bool warmCache, [Values(false, true)] bool outOfCore, [Values(false, true)] bool detached)
        {
            using var bytes = new ByteStore();
            using var storage = bytes.Open(warmCache);
            var root = MakeUnevenTree(bytes, storage);
            if (warmCache) bytes.Warm(storage);
            if (detached)
            {
                root = root.With(ImmutableDictionary<Durable.Def, object>.Empty);
                Assert.That(bytes.Records.ContainsKey(root.Id.ToString()), Is.False);
            }
            AssertStatistics(root, outOfCore, new[] { 3, 11, 2, 7, 0 });
        }

        [Test]
        public void PersistedAverageReadsOnlyNodeMetadata([Values(false, true)] bool warmCache, [Values(false, true)] bool compressed)
        {
            using var bytes = new ByteStore();
            using var storage = bytes.Open(warmCache);
            var root = MakeUnevenTree(bytes, storage);
            if (compressed) bytes.CompressNodes(storage);
            if (warmCache) bytes.Warm(storage);
            bytes.Reads.Clear(); bytes.ForbidPayloadReads = true;
            Assert.That(root.GetAverageLeafPointCount(true), Is.EqualTo(23.0 / 5));
            Assert.That(bytes.Reads.Keys, Is.SubsetOf(bytes.Nodes.Keys));
            Assert.That(bytes.Reads.ContainsKey(root.Id.ToString()), Is.False, "the supplied root need not be reloaded");
            Assert.That(bytes.Reads.Values, Is.All.EqualTo(1));
            Assert.That(bytes.Reads.Count, Is.EqualTo(warmCache ? 0 : bytes.Nodes.Count - 1));
        }

        [Test]
        public void WarmReferenceModePerformsNoStorageReads()
        {
            using var bytes = new ByteStore();
            using var storage = bytes.Open(true);
            var root = MakeUnevenTree(bytes, storage);
            bytes.Warm(storage); bytes.ForbidAllReads = true;
            AssertStatistics(root, false, new[] { 3, 11, 2, 7, 0 });
        }

        [Test]
        public void DefaultTryResolverCanLoadOnCacheMiss()
        {
            using var bytes = new ByteStore();
            using var storage = bytes.Open(false);
            var root = MakeUnevenTree(bytes, storage);
            bytes.Reads.Clear();
            AssertStatistics(root, false, new[] { 3, 11, 2, 7, 0 });
            Assert.That(bytes.Reads.Count, Is.GreaterThan(0), "preserve the existing TryGetFromCache callback, not a new cache-only policy");
        }

        [Test]
        public void MissingPersistedChildFollowsResolutionPolicy([Values(false, true)] bool outOfCore, [Range(0, 2)] int operation)
        {
            using var bytes = new ByteStore();
            using var storage = bytes.Open(false);
            var root = MakeUnevenTree(bytes, storage);
            bytes.Records.TryRemove(root.Subnodes[0].Id, out _);
            if (outOfCore) Assert.Throws<Exception>(() => Statistic(root, true, operation));
            else
            {
                var expected = operation == 0 ? 0 : operation == 1 ? 11 : 20.0 / 4;
                Assert.That(Statistic(root, false, operation), Is.EqualTo(expected));
            }
        }

        [Test]
        public void ViewStatisticsUseVisibleLeavesNotParentEstimates([Values(false, true)] bool outOfCore, [Values(0, 1, 2)] int selection)
        {
            using var bytes = new ByteStore();
            using var storage = bytes.Open(false);
            var root = MakeUnevenTree(bytes, storage);
            var filter = new OddPositionFilter(selection);
            var view = FilteredNode.CreateTransient(root, filter);
            var counts = EnumerateLeafCounts(view).ToArray();
            Assert.That(counts.Length, Is.EqualTo(5));
            Assert.That(counts, Is.EqualTo(new[] { 3, 11, 2, 7, 0 }.Select(n => selection == 0 ? 0 : selection == 1 ? n : (n + 1) / 2)));
            AssertStatistics(view, outOfCore, counts);
        }

        [Test]
        public void BuiltInFilteredViewsReloadWithTransientChildren([Values(false, true)] bool outOfCore)
        {
            using var bytes = new ByteStore();
            using var storage = bytes.Open(false);
            var root = MakeUnevenTree(bytes, storage);
            var filter = new FilterInsideBox3d(new Box3d(new V3d(-100, -100, -100), new V3d(7.9, 100, 100)));
            var view = FilteredNode.Create(root, filter);
            var decoded = storage.GetPointCloudNode(view.Id);
            var expected = EnumerateLeafCounts(decoded).ToArray();
            Assert.That(expected.Length, Is.GreaterThan(0));
            AssertStatistics(decoded, outOfCore, expected);
        }

        [Test]
        public void CompletelyPrunedViewHasNoLeaves([Values(false, true)] bool outOfCore)
        {
            using var bytes = new ByteStore();
            using var storage = bytes.Open(false);
            var root = MakeUnevenTree(bytes, storage);
            var view = FilteredNode.CreateTransient(root, new FilterInsideBox3d(new Box3d(new V3d(100), new V3d(101))));
            Assert.That(view.IsLeaf, Is.False);
            AssertStatistics(view, outOfCore, Array.Empty<int>());
        }

        [Test]
        public void DurableLeafWithoutCountUsesCompatibleFallback([Values(false, true)] bool compressed)
        {
            using var bytes = new ByteStore();
            using var storage = bytes.Open(false);
            var leaf = MakeNode(bytes, storage, Cell.Unit, 9);
            var data = leaf.Properties.ToImmutableDictionary().Remove(Durable.Octree.PointCountCell);
            storage.Add(leaf.Id, Durable.Octree.Node, data, compressed);
            var root = MakeNode(bytes, storage, new Cell(0, 0, 0, 1), 1, leaf);
            Assert.That(root.GetAverageLeafPointCount(true), Is.EqualTo(9));
        }

        [Test]
        public void LegacyBinaryChildrenUseInterfaceFallback([Values(0, 7)] int count, [Values(false, true)] bool outOfCore)
        {
            using var bytes = new ByteStore();
            using var storage = bytes.Open(false);
            var leaf = MakeNode(bytes, storage, Cell.Unit, count);
            var emptyKdId = Guid.NewGuid();
            if (count == 0) storage.Add(emptyKdId, Aardvark.Geometry.Points.Codec.PointRkdTreeDDataToBuffer(new PointRkdTreeDData { PermArray = Array.Empty<long>(), AxisArray = Array.Empty<int>(), RadiusArray = Array.Empty<double>() }));
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
            {
                writer.Write(count == 0 ? 17u : 1u); writer.Write(leaf.Id.ToByteArray());
                writer.Write(0L); writer.Write(0L); writer.Write(0L); writer.Write(0);
                writer.Write((long)count);
                writer.Write(leaf.PositionsId.Value.ToByteArray());
                if (count == 0) writer.Write(emptyKdId.ToByteArray());
            }
            storage.Add(leaf.Id, stream.ToArray());
            var root = MakeNode(bytes, storage, new Cell(0, 0, 0, 1), 1, leaf);
            AssertStatistics(root, outOfCore, new[] { count });
        }

        [Test]
        public void PartiallyWarmAverageStillAvoidsAttributeReads()
        {
            using var bytes = new ByteStore();
            using var storage = bytes.Open(true);
            var root = MakeUnevenTree(bytes, storage);
            var first = root.Subnodes[0].Id;
            storage.Cache.Add(first, bytes.Nodes[first], bytes.Records[first].Length, default);
            bytes.Reads.Clear(); bytes.ForbidPayloadReads = true;
            Assert.That(root.GetAverageLeafPointCount(true), Is.EqualTo(23.0 / 5));
            Assert.That(bytes.Reads.Count, Is.EqualTo(bytes.Nodes.Count - 2));
            Assert.That(bytes.Reads.Values, Is.All.EqualTo(1));
        }

        [Test]
        public void StoredWrapperChildUsesVisibleLeafCount([Values(false, true)] bool cached)
        {
            using var bytes = new ByteStore();
            using var storage = bytes.Open(cached);
            var leaf = MakeNode(bytes, storage, Cell.Unit, 8);
            var view = FilteredNode.Create(leaf, new FilterInsideBox3d(new Box3d(V3d.Zero, new V3d(0.5, 1, 1))));
            Assert.That(view.PointCountCell, Is.EqualTo(4));
            var root = MakeNode(bytes, storage, new Cell(0, 0, 0, 1), 1, view);
            if (cached) storage.Cache.Add(view.Id.ToString(), view, 1, default);
            Assert.That(root.GetAverageLeafPointCount(true), Is.EqualTo(4));
        }

        [Test]
        public void EmptyDurableChildSlotsDoNotInventALeaf()
        {
            using var bytes = new ByteStore();
            using var storage = bytes.Open(false);
            var child = MakeNode(bytes, storage, Cell.Unit, 8);
            var root = MakeNode(bytes, storage, new Cell(0, 0, 0, 1), 1, child);
            var data = child.Properties.ToImmutableDictionary().Add(Durable.Octree.SubnodesGuids, new Guid[8]);
            storage.Add(child.Id, Durable.Octree.Node, data, false);
            bytes.ForbidPayloadReads = true;
            Assert.That(root.GetAverageLeafPointCount(true), Is.NaN);
        }

        [Test]
        public void InvalidStoredChildIsNotSilentlySkipped([Values(0, 8, 16)] int length)
        {
            using var bytes = new ByteStore();
            using var storage = bytes.Open(false);
            var child = MakeNode(bytes, storage, Cell.Unit, 1);
            var root = MakeNode(bytes, storage, new Cell(0, 0, 0, 1), 1, child);
            storage.Add(child.Id, new byte[length]);
            Assert.Catch(() => root.GetAverageLeafPointCount(true));
        }

        private static Reference Ref(Probe child, bool cached = true) => new(child, cached);
        private static IEnumerable<int> Oracle(Probe node, bool outOfCore)
        {
            if (node.Children == null) yield return node.Count;
            else foreach (var reference in node.Children)
                if (reference != null && (outOfCore || reference.Cached))
                    foreach (var count in Oracle(reference.Child, outOfCore)) yield return count;
        }
        private static IEnumerable<int> EnumerateLeafCounts(IPointCloudNode node)
        {
            var children = node.Subnodes;
            if (children == null) yield return node.PointCountCell;
            else foreach (var child in children)
                if (child != null) foreach (var count in EnumerateLeafCounts(child.Value)) yield return count;
        }

        private static PointSetNode MakeUnevenTree(ByteStore bytes, Storage storage)
        {
            var rootCell = new Cell(0, 0, 0, 4);
            var a = MakeNode(bytes, storage, rootCell.GetOctant(0), 3);
            var innerCell = rootCell.GetOctant(2);
            var b = MakeNode(bytes, storage, innerCell.GetOctant(0), 11);
            var deepCell = innerCell.GetOctant(1);
            var c = MakeNode(bytes, storage, deepCell.GetOctant(0), 2);
            var d = MakeNode(bytes, storage, deepCell.GetOctant(2), 7);
            var deep = MakeNode(bytes, storage, deepCell, 1, c, null, d);
            var inner = MakeNode(bytes, storage, innerCell, 2, b, deep);
            var empty = MakeNode(bytes, storage, rootCell.GetOctant(4), 0);
            return MakeNode(bytes, storage, rootCell, 1, a, null, inner, null, empty);
        }

        private static PointSetNode MakeNode(ByteStore bytes, Storage storage, Cell cell, int count, params IPointCloudNode[] children)
        {
            var ps = Enumerable.Range(0, count).Select(i => new V3f((float)(((i + 0.5) / Math.Max(1, count) - 0.5) * cell.BoundingBox.Size.X), 0, 0)).ToArray();
            var id = Guid.NewGuid(); var pId = Guid.NewGuid(); var cId = Guid.NewGuid(); var nId = Guid.NewGuid(); var jId = Guid.NewGuid(); var kId = Guid.NewGuid();
            storage.Add(pId, ps); storage.Add(cId, Enumerable.Repeat(C4b.White, count).ToArray());
            storage.Add(nId, Enumerable.Repeat(V3f.ZAxis, count).ToArray()); storage.Add(jId, Enumerable.Repeat(42, count).ToArray()); storage.Add(kId, Enumerable.Repeat((byte)1, count).ToArray());
            var data = ImmutableDictionary<Durable.Def, object>.Empty.Add(Durable.Octree.NodeId, id).Add(Durable.Octree.Cell, cell)
                .Add(Durable.Octree.PointCountCell, count).Add(Durable.Octree.PointCountTreeLeafs, children.Length == 0 ? (long)count : children.Where(n => n != null).Sum(n => n.PointCountTree))
                .Add(Durable.Octree.PositionsLocal3fReference, pId).Add(Durable.Octree.Colors4bReference, cId).Add(Durable.Octree.Normals3fReference, nId)
                .Add(Durable.Octree.Intensities1iReference, jId).Add(Durable.Octree.Classifications1bReference, kId)
                .Add(Durable.Octree.BoundingBoxExactLocal, new Box3f(ps)).Add(Durable.Octree.BoundingBoxExactGlobal, cell.BoundingBox)
                .Add(Durable.Octree.MinTreeDepth, children.Length == 0 ? 0 : 1).Add(Durable.Octree.MaxTreeDepth, children.Length == 0 ? 0 : 4);
            if (count == 0)
            {
                // Supply the empty kd-tree data directly; the separate balancing constructor
                // requires a nonempty input. This is a normal, non-temporary empty leaf.
                var kdId = Guid.NewGuid();
                storage.Add(kdId, new PointRkdTreeFData { PermArray = Array.Empty<long>(), AxisArray = Array.Empty<int>(), RadiusArray = Array.Empty<float>() });
                data = data.Add(Durable.Octree.PointRkdTreeFDataReference, kdId);
            }
            if (children.Length != 0)
            {
                var ids = new Guid[8]; for (var i = 0; i < children.Length; i++) ids[i] = children[i]?.Id ?? Guid.Empty;
                data = data.Add(Durable.Octree.SubnodesGuids, ids);
            }
            var node = new PointSetNode(data, storage, true);
            bytes.Nodes[id.ToString()] = node;
            return node;
        }

        public class Probe : DispatchProxy
        {
            public IPointCloudNode Node;
            public Reference[] Children;
            public int Count, CountReads, SubnodeReads;
            private PersistentRef<IPointCloudNode>[] references;
            public static Probe Create(int count, params Reference[] children)
            {
                var node = Create<IPointCloudNode, Probe>(); var probe = (Probe)node;
                probe.Node = node; probe.Count = count;
                if (children.Length > 0)
                {
                    probe.Children = new Reference[8]; probe.references = new PersistentRef<IPointCloudNode>[8];
                    for (var i = 0; i < children.Length; i++) { probe.Children[i] = children[i]; probe.references[i] = children[i]?.ReferenceValue; }
                }
                return probe;
            }
            protected override object Invoke(MethodInfo method, object[] args)
            {
                if (method.Name == "get_Subnodes") { SubnodeReads++; return references; }
                if (method.Name == "get_PointCountCell") { CountReads++; return Count; }
                throw new InvalidOperationException("Unexpected access: " + method.Name);
            }
        }
        public sealed class Reference
        {
            public readonly Probe Child;
            public readonly bool Cached;
            public readonly PersistentRef<IPointCloudNode> ReferenceValue;
            public int ValueCalls, TryCalls;
            public IOException Failure;
            public Reference(Probe child, bool cached)
            {
                Child = child; Cached = cached;
                ReferenceValue = new PersistentRef<IPointCloudNode>(Guid.NewGuid(), _ => { ValueCalls++; if (Failure != null) throw Failure; return child.Node; },
                    (string _, out IPointCloudNode result) => { TryCalls++; result = Cached ? child.Node : null; return Cached; });
            }
        }
        private sealed class OddPositionFilter : IFilter
        {
            private readonly int selection;
            public OddPositionFilter(int selection) => this.selection = selection;
            public bool IsFullyInside(IPointCloudNode node) => false;
            public bool IsFullyOutside(IPointCloudNode node) => false;
            public HashSet<int> FilterPoints(IPointCloudNode node, HashSet<int> selected = null) => Enumerable.Range(0, node.PointCountCell)
                .Where(i => (selection == 1 || (selection == 2 && i % 2 == 0)) && (selected == null || selected.Contains(i))).ToHashSet();
            public System.Text.Json.Nodes.JsonNode Serialize() => throw new NotSupportedException();
            public bool Equals(IFilter other) => ReferenceEquals(this, other);
        }
        private sealed class ByteStore : IDisposable
        {
            public readonly ConcurrentDictionary<string, byte[]> Records = new();
            public readonly Dictionary<string, PointSetNode> Nodes = new();
            public readonly Dictionary<string, int> Reads = new();
            public bool ForbidAllReads, ForbidPayloadReads;
            public Storage Open(bool cache) => new((key, _, encode) => Records[key] = (byte[])encode().Clone(), key =>
            {
                if (ForbidAllReads || (ForbidPayloadReads && !Nodes.ContainsKey(key))) throw new InvalidOperationException("Unexpected read: " + key);
                lock (Reads) { Reads.TryGetValue(key, out var n); Reads[key] = n + 1; }
                return Records.TryGetValue(key, out var buffer) ? (byte[])buffer.Clone() : null;
            }, (_, _, _) => throw new NotSupportedException(), key => Records.TryRemove(key, out _), () => { }, () => { }, cache ? new LruDictionary<string, object>(1L << 30) : null);
            public void Warm(Storage storage)
            {
                foreach (var (id, node) in Nodes) storage.Cache.Add(id, node, Records[id].Length, default);
            }
            public void CompressNodes(Storage storage)
            {
                foreach (var (id, node) in Nodes) storage.Add(id, Durable.Octree.Node, node.Properties, true);
            }
            public void Dispose() => Records.Clear();
        }
    }
}
