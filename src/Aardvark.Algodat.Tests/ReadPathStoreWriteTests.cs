/*
    Copyright (C) 2006-2025. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System.IO;
using System.Linq;
using System.Threading;

namespace Aardvark.Geometry.Tests
{
    /// <summary>
    /// Reading (decoding, querying) nodes must never write to the store.
    /// </summary>
    [TestFixture]
    public class ReadPathStoreWriteTests
    {
        static ReadPathStoreWriteTests() => Aardvark.Base.Coder.TypeInfo.Add(typeof(PointRkdTreeFData));

        /// <summary>
        /// Wraps an in-memory store and counts every add.
        /// </summary>
        private static (Storage storage, Func<int> addCount) CreateCountingStorage()
        {
            var inner = PointCloud.CreateInMemoryStore(cache: default);
            var count = 0;
            var storage = new Storage(
                add: (key, value, create) => { Interlocked.Increment(ref count); inner.f_add(key, value, create); },
                get: inner.f_get,
                getSlice: inner.f_getSlice,
                remove: inner.f_remove,
                dispose: inner.Dispose,
                flush: inner.f_flush,
                cache: new LruDictionary<string, object>(1L << 24)
                );
            return (storage, () => count);
        }

        /// <summary>
        /// Stores a leaf node WITHOUT a kd-tree (as older stores or foreign writers may produce).
        /// </summary>
        private static (Guid id, V3d[] psGlobal) StoreLeafWithoutKdTree(Storage storage, int n, int seed)
        {
            var r = new Random(seed);
            var psGlobalF = new V3f[n].SetByIndex(_ => new V3f(r.NextDouble(), r.NextDouble(), r.NextDouble()));
            var id = Guid.NewGuid();
            var cell = new Cell(psGlobalF);
            var center = (V3f)cell.GetCenter();
            var bbGlobal = (Box3d)new Box3f(psGlobalF);
            var bbLocal = (Box3f)bbGlobal - center;
            var psLocal = psGlobalF.Map(p => p - center);

            var psLocalId = Guid.NewGuid();
            storage.Add(psLocalId, psLocal);

            var data = ImmutableDictionary<Durable.Def, object>.Empty
                .Add(Durable.Octree.NodeId, id)
                .Add(Durable.Octree.Cell, cell)
                .Add(Durable.Octree.BoundingBoxExactGlobal, bbGlobal)
                .Add(Durable.Octree.BoundingBoxExactLocal, bbLocal)
                .Add(Durable.Octree.PointCountCell, n)
                .Add(Durable.Octree.PointCountTreeLeafs, (long)n)
                .Add(Durable.Octree.PositionsLocal3fReference, psLocalId)
                .Add(Durable.Octree.PerCellPartIndex1i, 42)
                .Add(Durable.Octree.PartIndexRange, new Range1i(42, 42))
                ;
            var buffer = Aardvark.Data.Codec.Serialize(Durable.Octree.Node, data);
            storage.Add(id.ToString(), buffer);

            // sanity: buffer must decode as a regular node (not via obsolete parser fallback)
            var check = PointSetNode.Decode(storage, buffer);
            ClassicAssert.AreEqual(id, check.Id);
            storage.Cache!.Clear();

            return (id, psGlobalF.Map(p => (V3d)p));
        }

        [Test]
        public void DecodingNodeWithoutKdTree_DoesNotWriteToStore()
        {
            var (storage, addCount) = CreateCountingStorage();
            using var ownedStorage = storage;
            var (id, psGlobal) = StoreLeafWithoutKdTree(storage, 2000, seed: 1);
            var addsAfterSetup = addCount();

            var query = new V3d(0.5, 0.5, 0.5);
            const double maxDist = 0.15;
            var expectedCount = psGlobal.Count(p => Vec.Distance(p, query) <= maxDist);
            ClassicAssert.IsTrue(expectedCount > 0);

            for (var round = 0; round < 3; round++)
            {
                storage.Cache!.Clear();

                var node = storage.GetPointCloudNode(id);
                ClassicAssert.IsTrue(node.HasPositions);
                ClassicAssert.IsTrue(node.HasKdTree, "decoded node must still offer a kd-tree (built in memory)");

                // kd-tree based query must work and be correct
                var near = node.QueryPointsNearPoint(query, maxDist, 100_000);
                ClassicAssert.AreEqual(expectedCount, near.Count);

                // line segment query (kd-tree path) must work; derived attribute check must not throw
                var line = new Line3d(new V3d(-1, 0.5, 0.5), new V3d(2, 0.5, 0.5));
                var nearLine = node.QueryPointsNearLineSegment(line, 0.1).Sum(c => c.Count);
                var expectedNearLine = psGlobal.Count(p => line.GetMinimalDistanceTo(p) <= 0.1);
                ClassicAssert.AreEqual(expectedNearLine, nearLine);
                node.CheckDerivedAttributes();

                ClassicAssert.AreEqual(addsAfterSetup, addCount(), $"round {round}: reading a node must not write to the store");
            }
        }

        [Test]
        public void DecodingNodeWithoutKdTree_Concurrently_BuildsOneTree()
        {
            var (storage, addCount) = CreateCountingStorage();
            using var ownedStorage = storage;
            var (id, psGlobal) = StoreLeafWithoutKdTree(storage, 2000, seed: 2);
            var addsAfterSetup = addCount();

            var node = storage.GetPointCloudNode(id);
            var query = new V3d(0.5, 0.5, 0.5);
            const double maxDist = 0.15;
            var expectedCount = psGlobal.Count(p => Vec.Distance(p, query) <= maxDist);

            var results = new int[32];
            System.Threading.Tasks.Parallel.For(0, results.Length, i =>
            {
                results[i] = node.QueryPointsNearPoint(query, maxDist, 100_000).Count;
            });

            ClassicAssert.IsTrue(results.All(x => x == expectedCount));
            ClassicAssert.AreEqual(addsAfterSetup, addCount());
            ClassicAssert.IsTrue(node.KdTree!.TryGetFromCache(out var a) && a != null);
            ClassicAssert.AreSame(a, node.KdTree.Value, "lazy kd-tree must be built exactly once");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void InMemoryKdTreeRecoversWithNewNode(bool recursive)
        {
            var (backing, addCount) = CreateCountingStorage();
            using var ownedBacking = backing;
            string targetKey = null;
            PointSetNode node = null;
            var reads = 0;
            byte[] Get(string key)
            {
                if (key == targetKey && Interlocked.Increment(ref reads) == 1)
                {
                    if (recursive) _ = node.KdTree.Value;
                    else throw new IOException("Simulated transient read error.");
                }
                return backing.f_get(key);
            }
            using var storage = new Storage(backing.f_add, Get, backing.f_getSlice, backing.f_remove,
                () => { }, backing.f_flush, backing.Cache);
            var (id, _) = StoreLeafWithoutKdTree(storage, 500, seed: 4);
            node = (PointSetNode)storage.GetPointCloudNode(id);
            var addsBefore = addCount();
            targetKey = node.PositionsId.Value.ToString();
            storage.Cache.Remove(targetKey);

            var errorType = recursive ? typeof(InvalidOperationException) : typeof(IOException);
            Assert.Throws(errorType, () => _ = node.KdTree.Value);
            Assert.Throws(errorType, () => _ = node.KdTree.Value);
            Assert.That(reads, Is.EqualTo(1));
            Assert.That(storage.Cache.Remove(id.ToString()), Is.True);
            var recreated = (PointSetNode)storage.GetPointCloudNode(id);
            Assert.That(recreated, Is.Not.SameAs(node));
            var recovered = recreated.KdTree.Value;
            Assert.That(recovered.GetClosest(V3f.Zero, float.MaxValue, 2).Count, Is.EqualTo(2));
            Assert.That(recreated.KdTree.Value, Is.SameAs(recovered));
            Assert.That(reads, Is.EqualTo(2));
            Assert.That(addCount(), Is.EqualTo(addsBefore), "Recreating a node must not persist the tree.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExplicitWritePersistsMissingKdTree(bool updateNode)
        {
            var (storage, addCount) = CreateCountingStorage();
            using var ownedStorage = storage;
            var (id, _) = StoreLeafWithoutKdTree(storage, 500, seed: 5);
            var node = (PointSetNode)storage.GetPointCloudNode(id);
            var addsBefore = addCount();
            if (updateNode) node = node.Without(Durable.Octree.PointRkdTreeFDataReference);
            var inMemoryTree = node.KdTree.Value;
            Assert.That(node.KdTreeId, Is.Null);
            Assert.That(addCount(), Is.EqualTo(addsBefore), "Construction and reads must not persist the tree.");

            var written = node.WriteToStore();
            Assert.That(written.Id, Is.EqualTo(node.Id));
            Assert.That(written.KdTreeId.HasValue, Is.True);
            var persistedData = written.KdTree.Value.Data;
            Assert.That(persistedData.PermArray, Is.EqualTo(inMemoryTree.Data.PermArray));
            Assert.That(persistedData.AxisArray, Is.EqualTo(inMemoryTree.Data.AxisArray));
            Assert.That(persistedData.RadiusArray, Is.EqualTo(inMemoryTree.Data.RadiusArray));
            Assert.That(node.KdTree.Value, Is.SameAs(inMemoryTree));
            Assert.That(addCount(), Is.EqualTo(addsBefore + 2), "Persist the tree and the node, once each.");
            Assert.That(node.KdTreeId, Is.Null, "Writing must not mutate the original immutable node.");

            storage.Cache.Clear();
            var reloaded = (PointSetNode)storage.GetPointCloudNode(written.Id);
            Assert.That(reloaded.KdTreeId, Is.EqualTo(written.KdTreeId));
            Assert.That(reloaded.KdTree.Value.GetClosest(V3f.Zero, float.MaxValue, 2).Count, Is.EqualTo(2));
            Assert.That(addCount(), Is.EqualTo(addsBefore + 2), "Reloading must remain read-only.");

            // An existing persisted tree is not regenerated by another explicit write.
            reloaded.WriteToStore();
            Assert.That(addCount(), Is.EqualTo(addsBefore + 3));
        }

        [Test]
        public void WritingNodeWithoutKdTree_StoresKdTree()
        {
            // the write path (import, merge) must still compute and persist the kd-tree
            var (storage, addCount) = CreateCountingStorage();
            using var ownedStorage = storage;
            var (id, _) = StoreLeafWithoutKdTree(storage, 500, seed: 3);
            var decoded = (PointSetNode)storage.GetPointCloudNode(id);
            var addsBefore = addCount();

            var written = new PointSetNode(decoded.Properties.ToImmutableDictionary(), storage, writeToStore: true);
            ClassicAssert.IsTrue(written.HasKdTree);
            ClassicAssert.IsTrue(written.KdTreeId.HasValue);
            ClassicAssert.IsTrue(addCount() > addsBefore);

            storage.Cache!.Clear();
            var reloaded = storage.GetPointCloudNode(id);
            ClassicAssert.IsTrue(reloaded.HasKdTree);
            ClassicAssert.AreEqual(written.KdTreeId, ((PointSetNode)reloaded).KdTreeId);
        }
    }
}
