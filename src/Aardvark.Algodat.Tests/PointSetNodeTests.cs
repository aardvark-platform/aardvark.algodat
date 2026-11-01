/*
    Copyright (C) 2006-2023. Aardvark Platform Team. http://github.com/aardvark-platform.
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.
    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/
using Aardvark.Base;
using Aardvark.Data;
using Aardvark.Data.Points;
using Aardvark.Geometry.Points;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Uncodium.SimpleStore;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class PointSetNodeTests
    {
        internal static Storage CreateStorage()
        {
            var x = new SimpleMemoryStore();
            void add(string name, object value, Func<byte[]> create) => x.Add(name, create());
            return new Storage(add, x.Get, x.GetSlice, x.Remove, x.Dispose, x.Flush, cache: default);
        }

        internal static Storage CreateDiskStorage(string dbDiskLocation)
        {
            var x = new SimpleDiskStore(dbDiskLocation);
            void add(string name, object value, Func<byte[]> create) => x.Add(name, create());
            return new Storage(add, x.Get, x.GetSlice, x.Remove, x.Dispose, x.Flush, cache: default);
        }

        internal static readonly ImmutableDictionary<Durable.Def, object> EmptyData = ImmutableDictionary<Durable.Def, object>.Empty;

        internal static PointSetNode CreateNode(Storage storage)
        {
            var id = Guid.NewGuid();
            var cell = new Cell(1, 2, 3, 0);

            var psId = Guid.NewGuid();
            var ps = new[] { new V3f(1.1f, 2.2f, 3.3f) };
            storage.Add(psId, ps);

            var kdId = Guid.NewGuid();
            var kd = ps.BuildKdTree();
            storage.Add(kdId, kd.Data);

            var csId = Guid.NewGuid();
            var cs = new[] { C4b.Yellow };
            storage.Add(csId, cs);

            var data = EmptyData
                .Add(Durable.Octree.NodeId, id)
                .Add(Durable.Octree.Cell, cell)
                .Add(Durable.Octree.PointCountTreeLeafs, ps.LongLength)
                .Add(Durable.Octree.PositionsLocal3fReference, psId)
                .Add(Durable.Octree.PointRkdTreeFDataReference, kdId)
                .Add(Durable.Octree.BoundingBoxExactLocal, new Box3f(ps))
                .Add(Durable.Octree.BoundingBoxExactGlobal, ((Box3d)new Box3f(ps)) + cell.GetCenter())
                .Add(Durable.Octree.Colors4bReference, csId)
                ;

            return new PointSetNode(data, storage, writeToStore: true);
        }

        [Test]
        public void IdenticalLocalPositionsHaveFiniteZeroSpreadInEitherGetterOrder()
        {
            foreach (var point in new[] { new V3f(1.1f, 0.1f, 2), new V3f(2.2f, -3.3f, 0.4f), new V3f(10000000, 0.1f, 2) })
            foreach (var count in new[] { 2, 8, 16, 1024, 8192 })
            foreach (var spreadFirst in new[] { false, true })
            {
                var ps = Enumerable.Repeat(point, count).ToArray();
                CheckComputedSpread(ps, point, 0, spreadFirst, $"identical point={point}, count={count}, spreadFirst={spreadFirst}");
            }
        }

        [Test]
        public void TranslatedUnitSpreadsUseSampleNormalizationAndTheUnroundedMean()
        {
            foreach (var offset in new[] { 0.0f, 1000000.0f, 10000000.0f })
            foreach (var count in new[] { 2, 8, 64, 8192 })
            foreach (var axis in new[] { 0, 1, 2 })
            foreach (var spreadFirst in new[] { false, true })
            {
                var ps = Enumerable.Range(0, count).Select(i => { var p = new V3f(offset); p[axis] += i % 2 == 0 ? -1 : 1; return p; }).ToArray();
                CheckComputedSpread(ps, new V3f(offset), (float)Math.Sqrt((double)count / (count - 1)), spreadFirst, $"symmetric offset={offset}, count={count}, axis={axis}, spreadFirst={spreadFirst}");
            }
            foreach (var offset in new[] { 0.0f, 1000000.0f, 10000000.0f })
            foreach (var count in new[] { 3, 6, 33, 8193 })
            foreach (var spreadFirst in new[] { false, true })
            {
                var ps = Enumerable.Range(0, count).Select(i => new V3f(offset + (i % 3 == 0 ? 0 : 1), 0, 0)).ToArray();
                var centroid = new V3f((float)((double)offset + 2.0 / 3), 0, 0);
                var spread = (float)Math.Sqrt(2.0 * count / (9.0 * (count - 1)));
                CheckComputedSpread(ps, centroid, spread, spreadFirst, $"unrounded mean offset={offset}, count={count}, spreadFirst={spreadFirst}");
            }
        }

        [Test]
        public void CentroidSpreadReloadRetainsComputedResultsAndDoesNotPersistGetterCaches()
        {
            foreach (var count in new[] { 8, 8192 })
            foreach (var identical in new[] { false, true })
            foreach (var spreadFirst in new[] { false, true })
            {
                using var storage = PointCloud.CreateInMemoryStore();
                var ps = Enumerable.Range(0, count).Select(i => identical ? new V3f(1.1f, 0.1f, 2) : new V3f(10000000 + (i % 2 == 0 ? -1 : 1), 0, 0)).ToArray();
                var centroid = identical ? ps[0] : new V3f(10000000, 0, 0);
                var spread = identical ? 0 : (float)Math.Sqrt((double)count / (count - 1));
                var node = CreateSpreadNode(storage, ps); var before = node.Encode(); var context = $"reload count={count}, identical={identical}, spreadFirst={spreadFirst}";
                var positions = node.Positions.Value; var snapshot = positions.ToArray();
                CheckSpreadValues(node, centroid, spread, spreadFirst, context);
                Assert.That(node.Positions.Value, Is.SameAs(positions), context + " retained position storage");
                Assert.That(positions, Is.EqualTo(snapshot), context + " input positions unchanged");
                Assert.That(node.Encode(), Is.EqualTo(before), context + " getter caches are not serialized");
                var reloaded = PointSetNode.Decode(storage, before);
                Assert.That(reloaded.Properties.ContainsKey(Durable.Octree.PositionsLocal3fCentroid), Is.False, context);
                Assert.That(reloaded.Properties.ContainsKey(Durable.Octree.PositionsLocal3fDistToCentroidStdDev), Is.False, context);
                CheckSpreadValues(reloaded, centroid, spread, !spreadFirst, context);
                Assert.That(reloaded.Encode(), Is.EqualTo(before), context + " roundtrip wire layout");
            }
        }

        [Test]
        public void StoredMetadataOrdinaryEmptyAndSingletonControlsKeepTheirContracts()
        {
            foreach (var spreadFirst in new[] { false, true })
            {
                CheckComputedSpread(new[] { new V3f(2, -3, 4) }, new V3f(2, -3, 4), 0, spreadFirst, "singleton");
                CheckComputedSpread(new[] { new V3f(-1, -2, -3), new V3f(1, 2, 3) }, V3f.Zero, (float)Math.Sqrt(28), spreadFirst, "ordinary multi-axis sample, not radial variance");
                foreach (var stored in new[] { 0.0f, 123.25f, float.NaN })
                {
                    using var storage = CreateStorage(); var ps = Enumerable.Repeat(new V3f(1.1f, 0.1f, 2), 8).ToArray(); var centroid = new V3f(9, -8, 7);
                    var node = CreateSpreadNode(storage, ps, centroid, stored); var before = node.Encode();
                    if (spreadFirst) { _ = node.CentroidLocalStdDev; _ = node.CentroidLocal; } else { _ = node.CentroidLocal; _ = node.CentroidLocalStdDev; }
                    for (var i = 0; i < 4; i++)
                    {
                        Assert.That(node.CentroidLocal, Is.EqualTo(centroid), "explicit centroid wins");
                        Assert.That(BitConverter.SingleToInt32Bits(node.CentroidLocalStdDev), Is.EqualTo(BitConverter.SingleToInt32Bits(stored)), "explicit spread wins, including NaN");
                    }
                    var copy = PointSetNode.Decode(storage, before);
                    Assert.That(copy.CentroidLocal, Is.EqualTo(centroid));
                    Assert.That(BitConverter.SingleToInt32Bits(copy.CentroidLocalStdDev), Is.EqualTo(BitConverter.SingleToInt32Bits(stored)));
                    Assert.That(node.Encode(), Is.EqualTo(before));
                }
                Assert.That(PointSetNode.Empty.CentroidLocal.IsNaN, Is.True, "stored empty centroid");
                Assert.That(float.IsNaN(PointSetNode.Empty.CentroidLocalStdDev), Is.True, "stored empty spread remains authoritative");
            }
            var empty = ((V3f, float))typeof(PointSetNode).GetMethod("ComputeStuffs", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { Array.Empty<V3f>() });
            Assert.That(empty.Item1.IsNaN, Is.True); Assert.That(empty.Item2, Is.Zero, "computed empty fallback is unchanged");
        }

        private static PointSetNode CreateSpreadNode(Storage storage, V3f[] ps, V3f? storedCentroid = null, float? storedSpread = null)
        {
            var psId = Guid.NewGuid(); storage.Add(psId, ps); var cell = new Cell(-1, -1, -1, 28); var local = ps.Length == 0 ? Box3f.Invalid : new Box3f(ps);
            var data = EmptyData.Add(Durable.Octree.NodeId, Guid.NewGuid()).Add(Durable.Octree.Cell, cell)
                .Add(PointSetNode.TemporaryImportNode, 0).Add(Durable.Octree.PositionsLocal3fReference, psId)
                .Add(Durable.Octree.PointCountCell, ps.Length).Add(Durable.Octree.PointCountTreeLeafs, (long)ps.Length)
                .Add(Durable.Octree.BoundingBoxExactLocal, local).Add(Durable.Octree.BoundingBoxExactGlobal, (Box3d)local + cell.GetCenter());
            if (storedCentroid.HasValue) data = data.Add(Durable.Octree.PositionsLocal3fCentroid, storedCentroid.Value);
            if (storedSpread.HasValue) data = data.Add(Durable.Octree.PositionsLocal3fDistToCentroidStdDev, storedSpread.Value);
            return new PointSetNode(data, storage, writeToStore: false);
        }

        private static void CheckComputedSpread(V3f[] ps, V3f centroid, float spread, bool spreadFirst, string context)
        {
            var source = ps.ToArray(); var reads = 0; var inner = new SimpleMemoryStore();
            using var storage = new Storage((key, value, encode) => inner.Add(key, encode()), key => { reads++; return inner.Get(key); }, inner.GetSlice, inner.Remove, inner.Dispose, inner.Flush, cache: null);
            var node = CreateSpreadNode(storage, ps); var properties = node.Properties; reads = 0;
            CheckSpreadValues(node, centroid, spread, spreadFirst, context);
            Assert.That(reads, Is.EqualTo(1), context + " either getter fills both caches");
            Assert.That(ps, Is.EqualTo(source), context + " input is untouched");
            Assert.That(node.Properties, Is.SameAs(properties), context + " metadata dictionary unchanged");
            Assert.That(node.Properties.ContainsKey(Durable.Octree.PositionsLocal3fCentroid), Is.False, context);
            Assert.That(node.Properties.ContainsKey(Durable.Octree.PositionsLocal3fDistToCentroidStdDev), Is.False, context);
        }

        private static void CheckSpreadValues(PointSetNode node, V3f centroid, float spread, bool spreadFirst, string context)
        {
            if (spreadFirst) { _ = node.CentroidLocalStdDev; _ = node.CentroidLocal; } else { _ = node.CentroidLocal; _ = node.CentroidLocalStdDev; }
            for (var i = 0; i < 4; i++)
            {
                var actual = node.CentroidLocalStdDev;
                Assert.That(node.CentroidLocal.IsNaN || node.CentroidLocal.IsInfinity, Is.False, context + " finite centroid");
                Assert.That(float.IsNaN(actual) || float.IsInfinity(actual), Is.False, context + " finite spread");
                Assert.That(node.CentroidLocal, Is.EqualTo(centroid), context + " centroid");
                Assert.That(actual, Is.EqualTo(spread).Within(spread == 0 ? 0 : 0.000002 * spread), context + " spread");
            }
        }

        [Test]
        public void CanCreatePointSetNode()
        {
            var storage = CreateStorage();

            var id = Guid.NewGuid();
            var cell = new Cell(1, 2, 3, 0);

            var psId = Guid.NewGuid();
            var ps = new[] { new V3f(1.1f, 2.2f, 3.3f) };
            storage.Add(psId, ps);

            var kdId = Guid.NewGuid();
            var kd = ps.BuildKdTree();
            storage.Add(kdId, kd.Data);

            var data = EmptyData
                .Add(Durable.Octree.NodeId, id)
                .Add(Durable.Octree.Cell, cell)
                .Add(Durable.Octree.PointCountTreeLeafs, ps.LongLength)
                .Add(Durable.Octree.PositionsLocal3fReference, psId)
                .Add(Durable.Octree.PointRkdTreeFDataReference, kdId)
                .Add(Durable.Octree.BoundingBoxExactLocal, new Box3f(ps))
                .Add(Durable.Octree.BoundingBoxExactGlobal, ((Box3d)new Box3f(ps)) + cell.GetCenter())
                ;

            var node = new PointSetNode(data, storage, writeToStore: true);

            ClassicAssert.IsTrue(node.Id == id);
            ClassicAssert.IsTrue(node.Cell == cell);
            ClassicAssert.IsTrue(node.PointCountCell == ps.LongLength);
            ClassicAssert.IsTrue(node.PointCountTree == ps.LongLength);
            ClassicAssert.IsTrue(node.Cell.BoundingBox == cell.BoundingBox);
            ClassicAssert.IsTrue(node.BoundingBoxExactLocal == new Box3f(ps));
        }

        [Test]
        public void CanUpdatePointSetNode()
        {
            var storage = CreateStorage();

            var id = Guid.NewGuid();
            var cell = new Cell(1, 2, 3, 0);

            var psId = Guid.NewGuid();
            var ps = new[] { new V3f(1.1f, 2.2f, 3.3f) };
            storage.Add(psId, ps);

            var data = EmptyData
                .Add(Durable.Octree.NodeId, id)
                .Add(Durable.Octree.Cell, cell)
                .Add(Durable.Octree.PointCountTreeLeafs, ps.LongLength)
                .Add(Durable.Octree.PositionsLocal3fReference, psId)
                .Add(Durable.Octree.BoundingBoxExactLocal, new Box3f(ps))
                ;

            var node = new PointSetNode(data, storage, writeToStore: true);

            var replacements = ImmutableDictionary<Durable.Def, object>.Empty
                .Add(Durable.Octree.BoundingBoxExactGlobal, ((Box3d)new Box3f(ps)) + cell.GetCenter())
                ;
            var node2 = node.With(replacements);

            ClassicAssert.AreNotEqual(node.Id, node2.Id);
        }

        [Test]
        public void CanEncodePointSetNode()
        {
            var storage = CreateStorage();
            var node = CreateNode(storage);
            node.Encode();
        }

        [Test]
        public void CanDecodePointSetNode()
        {
            // encode
            var storage = CreateStorage();
            var node = CreateNode(storage);
            var buffer = node.Encode();

            // decode
            var node2 = PointSetNode.Decode(storage, buffer);
            ClassicAssert.IsTrue(node.Id == node2.Id);
            ClassicAssert.IsTrue(node.Cell == node2.Cell);
            ClassicAssert.IsTrue(node.PointCountTree == node2.PointCountTree);
        }
    }
}
