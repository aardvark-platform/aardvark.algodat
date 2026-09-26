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
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class ClassificationFilterTests
    {
        private static readonly byte[] AllClasses = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
        private static readonly byte[][] Sets = { Array.Empty<byte>(), new byte[] { 1 }, new byte[] { 2 }, new byte[] { 0, 255 }, AllClasses };
        private static readonly byte[][] Values = { null, Array.Empty<byte>(), new byte[] { 1, 1, 1, 1 }, new byte[] { 2, 2, 2, 2 }, new byte[] { 1, 2, 1, 2 }, new byte[] { 0, 255, 1, 2 } };

        [Test]
        public void LeafPredicatesAndSelectionAgree([Range(0, 5)] int layout, [Range(0, 4)] int set, [Range(0, 3)] int domain)
        {
            var xs = Values[layout];
            var probe = NodeProbe.Create(true, xs);
            var filter = new FilterClassification(Sets[set]);
            var count = xs?.Length ?? 4;
            var selected = domain == 0 ? null : new HashSet<int>(Enumerable.Range(0, count).Where(i => domain == 1 || (domain == 2 && i % 2 == 0)));
            var saved = selected?.ToArray();
            var expected = Enumerable.Range(0, count).Where(i => xs != null && Sets[set].Contains(xs[i]) && (selected == null || selected.Contains(i))).ToArray();

            Assert.That(filter.IsFullyInside(probe.Node), Is.EqualTo(xs != null && xs.All(Sets[set].Contains)));
            Assert.That(filter.IsFullyOutside(probe.Node), Is.EqualTo(xs == null || xs.All(x => !Sets[set].Contains(x))));
            var actual = filter.FilterPoints(probe.Node, selected);
            Assert.That(actual, Is.EquivalentTo(expected));
            if (selected != null)
            {
                Assert.That(selected, Is.EquivalentTo(saved));
                Assert.That(actual, Is.Not.SameAs(selected));
            }
            actual.Add(999); // Results are independently owned and not reused by the filter.
            Assert.That(filter.FilterPoints(probe.Node, selected), Is.EquivalentTo(expected));
            if (selected != null) Assert.That(selected, Is.EquivalentTo(saved));
        }

        [Test]
        public void LocalLodSelectionDoesNotDescribeDescendants([Range(0, 4)] int set, [Range(0, 3)] int domain)
        {
            var values = new byte[] { 0, 1, 255, 2 };
            var probe = NodeProbe.Create(false, values);
            var filter = new FilterClassification(Sets[set]);
            var selection = domain == 0 ? null : new HashSet<int>(Enumerable.Range(0, 4).Where(i => domain == 1 || (domain == 2 && i % 2 == 0)));
            var saved = selection?.ToArray();
            var result = filter.FilterPoints(probe.Node, selection);
            Assert.That(result, Is.EquivalentTo(Enumerable.Range(0, 4).Where(i => Sets[set].Contains(values[i]) && (selection == null || selection.Contains(i)))));
            Assert.That(probe.PayloadReads, Is.EqualTo(1));
            Assert.That(probe.LeafReads, Is.Zero);
            if (selection != null) Assert.That(selection, Is.EquivalentTo(saved));
        }

        [Test]
        public void InternalPredicatesAreMetadataOnly([Range(0, 4)] int set, [Values(false, true)] bool advertisedPayload)
        {
            var probe = NodeProbe.Create(false, advertisedPayload ? new byte[] { 1 } : null);
            probe.ForbidClassificationAccess = true;
            var filter = new FilterClassification(Sets[set]);
            Assert.That(filter.IsFullyInside(probe.Node), Is.False);
            Assert.That(filter.IsFullyOutside(probe.Node), Is.EqualTo(Sets[set].Length == 0));
            Assert.That(probe.PayloadReads, Is.Zero);
            Assert.That(probe.AvailabilityReads, Is.Zero);
            Assert.That(probe.LeafReads, Is.EqualTo(Sets[set].Length == 0 ? 1 : 2));
        }

        [Test]
        public void LeafScansStopAtFirstDecisiveClassification([Values(false, true)] bool inside, [Values(0, 32, 64, 65)] int decisive)
        {
            var comparer = new CountingComparer();
            var filter = new FilterClassification(new HashSet<byte>(new byte[] { 1 }, comparer));
            var xs = Enumerable.Repeat(inside ? (byte)1 : (byte)2, 65).ToArray();
            if (decisive < xs.Length) xs[decisive] = inside ? (byte)2 : (byte)1;
            var probe = NodeProbe.Create(true, xs);
            comparer.Calls = 0;
            Assert.That(inside ? filter.IsFullyInside(probe.Node) : filter.IsFullyOutside(probe.Node), Is.EqualTo(decisive == xs.Length));
            Assert.That(comparer.Calls, Is.EqualTo(Math.Min(decisive + 1, xs.Length)));
            Assert.That(probe.PayloadReads, Is.EqualTo(1));
        }

        [Test]
        public void EmptyFilterRejectsWithoutLoadingPayloads([Values(false, true)] bool leaf)
        {
            var probe = NodeProbe.Create(leaf, new byte[] { 1 });
            probe.ForbidClassificationAccess = true;
            Assert.That(new FilterClassification(Array.Empty<byte>()).IsFullyOutside(probe.Node), Is.True);
            Assert.That(probe.LeafReads + probe.AvailabilityReads + probe.PayloadReads, Is.Zero);
        }

        [Test]
        public void FilterSetRemainsLiveAndMutable()
        {
            var allowed = new HashSet<byte> { 1 };
            var filter = new FilterClassification(allowed);
            var leaf = NodeProbe.Create(true, new byte[] { 1, 2 });
            var inner = NodeProbe.Create(false, new byte[] { 1 });
            Assert.That(filter.Filter, Is.SameAs(allowed));
            for (var i = 0; i < 3; i++)
            {
                allowed.Clear();
                Assert.That(filter.IsFullyOutside(inner.Node), Is.True);
                Assert.That(filter.IsFullyOutside(leaf.Node), Is.True);
                allowed.Add(1);
                Assert.That(filter.IsFullyOutside(inner.Node), Is.False);
                Assert.That(filter.IsFullyInside(inner.Node), Is.False);
                Assert.That(filter.FilterPoints(leaf.Node), Is.EquivalentTo(new[] { 0 }));
                allowed.Add(2);
                Assert.That(filter.IsFullyInside(leaf.Node), Is.True);
                Assert.That(filter.FilterPoints(leaf.Node), Is.EquivalentTo(new[] { 0, 1 }));
            }
            var input = new byte[] { 1 };
            var copied = new FilterClassification(input);
            input[0] = 2;
            Assert.That(copied.Filter, Is.EquivalentTo(new byte[] { 1 }));
        }

        public static IEnumerable<TestCaseData> Exclusions()
        {
            yield return new TestCaseData(Array.Empty<Range1b>()).SetName("ExclusionRanges_Empty");
            foreach (var pair in new[] { (0, 0), (0, 1), (1, 254), (254, 254), (254, 255), (255, 255), (0, 255), (200, 100) })
                yield return new TestCaseData(new[] { new Range1b((byte)pair.Item1, (byte)pair.Item2) }).SetName($"ExclusionRanges_{pair.Item1}_{pair.Item2}");
            yield return new TestCaseData(new[] { new Range1b(0, 10), new Range1b(5, 20), new Range1b(250, 255), new Range1b(255, 255) }).SetName("ExclusionRanges_Overlapping");
            yield return new TestCaseData(new[] { new Range1b(0, 127), new Range1b(128, 255), new Range1b(0, 127) }).SetName("ExclusionRanges_FullWithDuplicates");
        }

        [TestCaseSource(nameof(Exclusions))]
        public void InclusiveExclusionRanges(Range1b[] ranges)
        {
            var before = ranges.ToArray();
            var expected = AllClasses.Where(b => !ranges.Any(r => b >= r.Min && b <= r.Max)).ToArray();
            var filter = FilterClassification.AllExcept(ranges);
            Assert.That(filter.Filter, Is.EquivalentTo(expected));
            Assert.That(ranges, Is.EqualTo(before));
            Assert.That(filter.FilterPoints(NodeProbe.Create(true, AllClasses).Node), Is.EquivalentTo(expected.Select(b => (int)b)));
        }

        [Test]
        public void ByteExclusionsAreInclusiveAndIgnoreDuplicates([Range(0, 3)] int caseIndex)
        {
            var xs = new[] { Array.Empty<byte>(), new byte[] { 0 }, new byte[] { 0, 255, 255, 1 }, AllClasses }[caseIndex];
            var before = xs.ToArray();
            Assert.That(FilterClassification.AllExcept(xs).Filter, Is.EquivalentTo(AllClasses.Except(xs)));
            Assert.That(xs, Is.EqualTo(before));
        }

        [Test]
        public void SerializationKeepsItsSchemaAndCurrentSet([Range(0, 4)] int set)
        {
            var filter = new FilterClassification(Sets[set]);
            var json = filter.Serialize();
            Assert.That(json.AsObject().Select(p => p.Key), Is.EquivalentTo(new[] { "Type", "Filter" }));
            Assert.That((string)json["Type"], Is.EqualTo("FilterClassification"));
            Assert.That(json["Filter"].AsArray().Select(x => (int)x), Is.EquivalentTo(Sets[set].Select(x => (int)x)));
            for (var cycle = 0; cycle < 3; cycle++)
            {
                filter = (FilterClassification)Filter.Deserialize(filter.Serialize().ToJsonString());
                Assert.That(filter.Filter, Is.EquivalentTo(Sets[set]));
                Assert.That(filter.Equals(new FilterClassification(Sets[set].Reverse().ToArray())), Is.True);
            }
            filter.Filter.Clear();
            filter.Filter.Add(255);
            Assert.That(((FilterClassification)Filter.Deserialize(filter.Serialize().ToJsonString())).Filter, Is.EquivalentTo(new byte[] { 255 }));
        }

        [Test]
        public void LegacyJsonAndSetEqualityRemainCompatible()
        {
            var filter = (FilterClassification)Filter.Deserialize("{\"Type\":\"FilterClassification\",\"Filter\":[255,1,1,0]}");
            Assert.That(filter.Equals(new FilterClassification(0, 1, 255)), Is.True);
            Assert.That(filter.Equals(new FilterClassification(0, 1)), Is.False);
            Assert.That(filter.Equals(new FilterInsideBox3d(Box3d.Unit)), Is.False);
            Assert.That(filter.Equals(null), Is.False);
            var direct = FilterClassification.Deserialize(JsonNode.Parse("{\"Filter\":[2,1]}"));
            Assert.That(direct.Filter, Is.EquivalentTo(new byte[] { 1, 2 }));
        }

        [Test]
        public void AlternatingGridEnumeratesAndCountsExactly([Values(1, 2)] int selectedClass, [Values(0, 4096)] int cacheSize, [Values(false, true)] bool reload)
        {
            using var bytes = new ByteStore();
            using var storage = bytes.Open(cacheSize);
            var root = CreateGrid(storage).Root.Value;
            Assert.That(root.IsLeaf, Is.False);
            Assert.That(root.PointCountTree, Is.EqualTo(64));
            Assert.That(root.PointCountCell, Is.LessThan(64));
            var filter = new FilterClassification((byte)selectedClass);
            var view = FilteredNode.Create(root, filter);
            var id = view.Id;
            if (!reload) AssertGrid(view, selectedClass, _ => true);
            else
            {
                using var fresh = bytes.Open(cacheSize);
                for (var cycle = 0; cycle < 3; cycle++)
                {
                    var decoded = fresh.GetPointCloudNode(id);
                    Assert.That(decoded, Is.TypeOf<FilteredNode>());
                    Assert.That(decoded.Id, Is.EqualTo(id));
                    Assert.That(((FilteredNode)decoded).Node.Id, Is.EqualTo(root.Id));
                    Assert.That(((FilteredNode)decoded).Filter.Equals(filter), Is.True);
                    AssertGrid(decoded, selectedClass, _ => true);
                    fresh.Add(id, decoded.Encode());
                }
            }
        }

        [Test]
        public void InternalSamplesCannotAdmitOrDiscardDescendants([Values(-1, 1, 2)] int sample, [Values(1, 2, 256)] int selectedClass, [Values(false, true)] bool missingLeaf)
        {
            using var bytes = new ByteStore();
            using var storage = bytes.Open(0);
            var root = CreateGrid(storage).Root.Value;
            Rewrite(root, storage, n => !n.IsLeaf ? sample : (missingLeaf && n.PositionsAbsolute.Any(p => p.X < 1) ? -1 : -2));
            root = storage.GetPointCloudNode(root.Id);
            Assert.That(root.HasClassifications, Is.EqualTo(sample != -1));
            var filter = new FilterClassification(selectedClass == 256 ? AllClasses : new[] { (byte)selectedClass });
            var view = FilteredNode.Create(root, filter);
            using var reopened = bytes.Open(0);
            AssertGrid(reopened.GetPointCloudNode(view.Id), selectedClass, p => !missingLeaf || p.X >= 1);
        }

        [Test]
        public void MissingClassificationLeafSelectsNothing([Range(0, 4)] int set)
        {
            using var bytes = new ByteStore();
            using var storage = bytes.Open(0);
            var root = PointSet.Create(storage, Guid.NewGuid().ToString(), new[] { new V3d(0.125), new V3d(0.25) }, null, null, null, null, null, 8, false, false).Root.Value;
            var view = FilteredNode.Create(root, new FilterClassification(Sets[set]));
            Assert.That(view.PointCountCell, Is.Zero);
            Assert.That(view.QueryAllPoints().Sum(c => c.Count), Is.Zero);
            Assert.That(view.CountPointsInsideBox(Box3d.Unit), Is.Zero);
        }

        [Test]
        public void RealInternalPredicatesPerformNoStorageReads([Range(0, 4)] int set, [Values(-1, 1, 2)] int sample)
        {
            using var bytes = new ByteStore();
            using var storage = bytes.Open(0);
            var root = CreateGrid(storage).Root.Value;
            Rewrite(root, storage, n => n.IsLeaf ? -2 : sample);
            root = storage.GetPointCloudNode(root.Id);
            bytes.Reads = 0;
            bytes.ForbidReads = true;
            var filter = new FilterClassification(Sets[set]);
            Assert.That(filter.IsFullyInside(root), Is.False);
            Assert.That(filter.IsFullyOutside(root), Is.EqualTo(set == 0));
            Assert.That(bytes.Reads, Is.Zero);
        }

        private static PointSet CreateGrid(Storage storage)
        {
            var positions = Enumerable.Range(0, 64).Select(i => new V3d(i % 8 + 0.125, i / 8 + 0.125, 0.125)).ToArray();
            return PointSet.Create(storage, Guid.NewGuid().ToString(), positions,
                Enumerable.Range(0, 64).Select(i => new C4b(i, 2, 3, 255)).ToArray(),
                Enumerable.Repeat(V3f.ZAxis, 64).ToArray(), Enumerable.Range(0, 64).Select(i => i * 7).ToArray(),
                Enumerable.Range(0, 64).Select(i => (byte)(1 + i % 2)).ToArray(),
                null, 1, true, false);
        }

        private static void AssertGrid(IPointCloudNode view, int selectedClass, Func<V3d, bool> hasClassification)
        {
            var chunks = view.QueryAllPoints().ToArray();
            var positions = chunks.SelectMany(c => c.Positions).ToArray();
            var expected = Enumerable.Range(0, 64).Where(i => selectedClass == 256 || i % 2 + 1 == selectedClass)
                .Select(i => new V3d(i % 8 + 0.125, i / 8 + 0.125, 0.125)).Where(hasClassification).ToArray();
            Assert.That(positions, Is.EquivalentTo(expected));
            Assert.That(positions.Distinct().Count(), Is.EqualTo(expected.Length));
            Assert.That(view.CountPointsInsideBox(new Box3d(V3d.Zero, new V3d(8, 8, 1))), Is.EqualTo(expected.Length));
            Assert.That(view.CountPoints(_ => true, _ => false, _ => true), Is.EqualTo(expected.Length));
            // PointCountTree is an LoD-derived estimate on views, not the exact count.
            foreach (var c in chunks)
            {
                for (var j = 0; j < c.Count; j++)
                {
                    var i = (int)c.Positions[j].X + 8 * (int)c.Positions[j].Y;
                    Assert.That(c.Classifications[j], Is.EqualTo(1 + i % 2));
                    Assert.That(c.Colors[j], Is.EqualTo(new C4b(i, 2, 3, 255)));
                    Assert.That(c.Normals[j], Is.EqualTo(V3f.ZAxis));
                    Assert.That(c.Intensities[j], Is.EqualTo(i * 7));
                }
            }
        }

        // -2 leaves the payload unchanged; -1 removes it; other values replace local samples.
        private static void Rewrite(IPointCloudNode node, Storage storage, Func<IPointCloudNode, int> replacement)
        {
            if (!node.IsLeaf)
                foreach (var child in node.Subnodes.Where(x => x != null)) Rewrite(child.Value, storage, replacement);
            var value = replacement(node);
            if (value == -2) return;
            var data = node.Properties.ToImmutableDictionary().Remove(Durable.Octree.Classifications1b).Remove(Durable.Octree.Classifications1bReference);
            if (value >= 0)
            {
                var id = Guid.NewGuid();
                storage.Add(id, Enumerable.Repeat((byte)value, node.PointCountCell).ToArray());
                data = data.Add(Durable.Octree.Classifications1bReference, id);
            }
            storage.Add(node.Id, new PointSetNode(data, storage, false).Encode());
        }

        public class NodeProbe : DispatchProxy
        {
            public IPointCloudNode Node;
            public bool ForbidClassificationAccess;
            public int LeafReads, AvailabilityReads, PayloadReads;
            private bool leaf;
            private byte[] values;
            private PersistentRef<byte[]> reference;

            public static NodeProbe Create(bool leaf, byte[] values)
            {
                var node = Create<IPointCloudNode, NodeProbe>();
                var probe = (NodeProbe)node;
                probe.Node = node; probe.leaf = leaf; probe.values = values;
                probe.reference = new PersistentRef<byte[]>(Guid.NewGuid(), _ => { probe.PayloadReads++; return values; }, (string _, out byte[] result) => { result = null; return false; });
                return probe;
            }

            protected override object Invoke(MethodInfo method, object[] args)
            {
                if (method.Name == "get_IsLeaf") { LeafReads++; return leaf; }
                if (ForbidClassificationAccess) throw new InvalidOperationException("Unexpected access: " + method.Name);
                if (method.Name == "get_HasClassifications") { AvailabilityReads++; return values != null; }
                if (method.Name == "get_Classifications") return reference;
                throw new InvalidOperationException("Unexpected access: " + method.Name);
            }
        }

        private sealed class CountingComparer : IEqualityComparer<byte>
        {
            public int Calls;
            public bool Equals(byte x, byte y) => x == y;
            public int GetHashCode(byte x) { Calls++; return x; }
        }

        private sealed class ByteStore : IDisposable
        {
            private readonly ConcurrentDictionary<string, byte[]> data = new();
            public int Reads;
            public bool ForbidReads;
            public Storage Open(int cacheSize) => new(
                (key, _, encode) => data[key] = (byte[])encode().Clone(),
                key => { Interlocked.Increment(ref Reads); if (ForbidReads) throw new InvalidOperationException("Unexpected storage read"); return data.TryGetValue(key, out var bytes) ? (byte[])bytes.Clone() : null; },
                (_, _, _) => throw new NotSupportedException(), key => data.TryRemove(key, out _), () => { }, () => { },
                cacheSize == 0 ? null : new LruDictionary<string, object>(cacheSize));
            public void Dispose() => data.Clear();
        }
    }
}
