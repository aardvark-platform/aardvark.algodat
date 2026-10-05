using Aardvark.Base;
using Aardvark.Data;
using Aardvark.Data.Points;
using Aardvark.Geometry.Points;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Aardvark.Geometry.Tests;

[TestFixture]
public class FilteredNodeConcurrencyTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private static readonly Box3d FilterBox = new(V3d.Zero, new V3d(0.5, 1, 1));

    // Pause the first reader inside initialization, then let a second reader attempt
    // the same operation. A correct implementation waits rather than exposing partial state.
    private static (T First, T Second) ReadDuringInitialization<T>(
        Func<T> read, ManualResetEventSlim entered, ManualResetEventSlim release)
    {
        using var secondStarted = new ManualResetEventSlim();
        var first = Task.Factory.StartNew(read, CancellationToken.None,
            TaskCreationOptions.LongRunning, TaskScheduler.Default);
        Task<T> second = null;
        try
        {
            Assert.That(entered.Wait(Timeout), Is.True, "First reader did not reach initialization.");
            second = Task.Factory.StartNew(() =>
            {
                secondStarted.Set();
                return read();
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            Assert.That(secondStarted.Wait(Timeout), Is.True, "Second reader did not start.");
            // Allow the dedicated second thread to read while the first is paused.
            second.Wait(TimeSpan.FromMilliseconds(200));
        }
        finally
        {
            release.Set();
            var tasks = second == null ? new Task[] { first } : new Task[] { first, second };
            Assert.That(Task.WaitAll(tasks, Timeout), Is.True, "Readers did not finish.");
        }
        return (first.Result, second.Result);
    }

    private sealed class PausingFilter : ISpatialFilter
    {
        private readonly ISpatialFilter inner;
        private readonly Action beforeFirstCell;
        private int called;

        public PausingFilter(ISpatialFilter inner, Action beforeFirstCell)
        {
            this.inner = inner;
            this.beforeFirstCell = beforeFirstCell;
        }

        public bool IsFullyInside(Box3d box)
        {
            if (Interlocked.Exchange(ref called, 1) == 0) beforeFirstCell();
            return inner.IsFullyInside(box);
        }

        public bool IsFullyOutside(Box3d box) => inner.IsFullyOutside(box);
        public bool IsFullyInside(IPointCloudNode node) => inner.IsFullyInside(node);
        public bool IsFullyOutside(IPointCloudNode node) => inner.IsFullyOutside(node);
        public HashSet<int> FilterPoints(IPointCloudNode node, HashSet<int> selected = null)
            => inner.FilterPoints(node, selected);
        public bool Contains(V3d point) => inner.Contains(point);
        public Box3d Clip(Box3d box) => inner.Clip(box);
        public JsonNode Serialize() => inner.Serialize();
        public bool Equals(IFilter other) => ReferenceEquals(this, other);
    }

    private static IPointCloudNode CreateOctree(Storage storage)
    {
        var cell = new Cell(0, 0, 0, 0);
        var children = Enumerable.Range(0, 8).Select(i =>
        {
            var child = new PointSetNode(storage, false,
                (Durable.Octree.NodeId, Guid.NewGuid()),
                (Durable.Octree.Cell, cell.GetOctant(i)),
                (Durable.Octree.PositionsLocal3f, new[] { V3f.Zero }));
            storage.Cache.Add(child.Id.ToString(), child, 1024);
            return child;
        }).ToArray();
        return new PointSetNode(storage, false,
            (Durable.Octree.NodeId, Guid.NewGuid()),
            (Durable.Octree.Cell, cell),
            (Durable.Octree.SubnodesGuids, children.Select(n => n.Id).ToArray()),
            (Durable.Octree.PositionsLocal3f, new[] { V3f.Zero }),
            (Durable.Octree.BoundingBoxExactGlobal, cell.BoundingBox));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ConcurrentQueriesDoNotSeeIncompleteSubnodes(bool nearRay)
    {
        using var storage = PointCloud.CreateInMemoryStore();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var root = CreateOctree(storage);
        var filter = new PausingFilter(new FilterInsideBox3d(FilterBox), () =>
        {
            entered.Set();
            if (!release.Wait(Timeout)) throw new TimeoutException("Subnode initialization was not released.");
        });
        var filtered = FilteredNode.CreateTransient(root, filter);
        var expected = root.QueryAllPoints().SelectMany(c => c.Positions).Where(FilterBox.Contains).ToArray();
        Assert.That(expected.Length, Is.EqualTo(4));

        V3d[] Read() => (nearRay
                ? filtered.QueryPointsNearRay(new Ray3d(new V3d(-1, 0.5, 0.5), V3d.IOO), 1, 0, 2)
                : filtered.EnumerateCells(-1).SelectMany(c => c.GetPoints(0)))
            .SelectMany(c => c.Positions).ToArray();

        var (first, second) = ReadDuringInitialization(Read, entered, release);
        Assert.That(first, Is.EquivalentTo(expected));
        Assert.That(second, Is.EquivalentTo(expected));
        Assert.That(Read(), Is.EquivalentTo(expected));
    }

    [Test]
    public void FailedSubnodeInitializationRecoversWithNewView()
    {
        using var storage = PointCloud.CreateInMemoryStore();
        var root = CreateOctree(storage);
        var filter = new PausingFilter(new FilterInsideBox3d(FilterBox), () =>
            throw new InvalidOperationException("Simulated transient failure."));
        var filtered = FilteredNode.CreateTransient(root, filter);

        Assert.Throws<InvalidOperationException>(() => filtered.EnumerateCells(-1).ToArray());
        Assert.Throws<InvalidOperationException>(() => filtered.EnumerateCells(-1).ToArray());
        var recreated = FilteredNode.CreateTransient(root, filter);
        Assert.That(recreated.EnumerateCells(-1).Count(), Is.EqualTo(4));
    }

    [Test]
    public void RecursiveSubnodeInitializationRecoversWithNewView()
    {
        using var storage = PointCloud.CreateInMemoryStore();
        IPointCloudNode filtered = null;
        var filter = new PausingFilter(new FilterInsideBox3d(FilterBox), () => _ = filtered.Subnodes);
        var root = CreateOctree(storage);
        filtered = FilteredNode.CreateTransient(root, filter);

        Assert.Throws<InvalidOperationException>(() => _ = filtered.Subnodes);
        Assert.Throws<InvalidOperationException>(() => _ = filtered.Subnodes);
        var recreated = FilteredNode.CreateTransient(root, filter);
        Assert.That(recreated.EnumerateCells(-1).Count(), Is.EqualTo(4));
    }

    [Test]
    public void ConcurrentFailedReadersRecoverWithNewView()
    {
        using var storage = PointCloud.CreateInMemoryStore();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var failure = new InvalidOperationException("Simulated transient failure.");
        var filter = new PausingFilter(new FilterInsideBox3d(FilterBox), () =>
        {
            entered.Set();
            if (!release.Wait(Timeout)) throw new TimeoutException("Subnode initialization was not released.");
            throw failure;
        });
        var root = CreateOctree(storage);
        var filtered = FilteredNode.CreateTransient(root, filter);
        int Read()
        {
            try { return filtered.EnumerateCells(-1).Count(); }
            catch (InvalidOperationException e)
            {
                Assert.That(e, Is.SameAs(failure));
                return -1;
            }
        }

        var (first, second) = ReadDuringInitialization(Read, entered, release);
        Assert.That(first, Is.EqualTo(-1), "The failed call must propagate the initialization error.");
        Assert.That(second, Is.EqualTo(-1), "Readers of the same failed initialization see its exception.");
        Assert.That(Read(), Is.EqualTo(-1));
        var recreated = FilteredNode.CreateTransient(root, filter);
        Assert.That(recreated.EnumerateCells(-1).Count(), Is.EqualTo(4));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AttributeInitializationRecoversWithNewView(bool positions)
    {
        using var backing = PointCloud.CreateInMemoryStore();
        var positionsId = Guid.NewGuid();
        var normalsId = Guid.NewGuid();
        var targetKey = (positions ? positionsId : normalsId).ToString();
        var enabled = false;
        var reads = 0;
        byte[] Get(string key)
        {
            if (enabled && key == targetKey && Interlocked.Increment(ref reads) == 1)
                throw new IOException("Simulated transient read error.");
            return backing.f_get(key);
        }
        using var storage = new Storage(backing.f_add, Get, backing.f_getSlice, backing.f_remove,
            () => { }, backing.f_flush, backing.Cache);
        var root = CreateAttributedLeaf(storage, positionsId, normalsId);
        var filter = new FilterInsideBox3d(FilterBox);
        var filtered = FilteredNode.CreateTransient(root, filter);
        storage.Cache.Clear();
        enabled = true;
        PersistentRef<V3f[]> Read() => positions ? filtered.Positions : filtered.Normals;

        Assert.Throws<IOException>(() => _ = Read());
        Assert.Throws<IOException>(() => _ = Read());
        Assert.That(reads, Is.EqualTo(1), "A failed lazy does not silently repeat the storage operation.");
        filtered = FilteredNode.CreateTransient(root, filter);
        var recovered = Read();
        Assert.That(recovered.Value.Length, Is.EqualTo(2));
        Assert.That(Read(), Is.SameAs(recovered));
        Assert.That(reads, Is.EqualTo(2));
    }

    private static IPointCloudNode CreateAttributedLeaf(Storage storage, Guid positionsId, Guid normalsId)
    {
        storage.Add(positionsId, new[]
        {
            new V3f(-0.25, -0.25, -0.25), new V3f(0.25, -0.25, -0.25),
            new V3f(-0.25, 0.25, 0.25), new V3f(0.25, 0.25, 0.25)
        });
        storage.Add(normalsId, new[] { V3f.IOO, V3f.OIO, V3f.OOI, -V3f.IOO });
        return new PointSetNode(storage, false,
            (Durable.Octree.NodeId, Guid.NewGuid()),
            (Durable.Octree.Cell, new Cell(0, 0, 0, 0)),
            (Durable.Octree.PositionsLocal3fReference, positionsId),
            (Durable.Octree.Normals3fReference, normalsId),
            (Durable.Octree.Colors4b, new[] { C4b.Red, C4b.Green, C4b.Blue, C4b.White }),
            (Durable.Octree.Intensities1i, new[] { 10, 20, 30, 40 }),
            (Durable.Octree.Classifications1b, new byte[] { 1, 2, 3, 4 }),
            (Durable.Octree.PerPointPartIndex1i, new[] { 7, 3, 11, 5 }),
            (Durable.Octree.PartIndexRange, new Range1i(3, 11)));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ConcurrentAttributeInitializationReusesCachedValue(bool positions)
    {
        using var backing = PointCloud.CreateInMemoryStore();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var positionsId = Guid.NewGuid();
        var normalsId = Guid.NewGuid();
        var targetKey = (positions ? positionsId : normalsId).ToString();
        var enabled = false;
        var reads = 0;
        byte[] Get(string key)
        {
            if (enabled && key == targetKey && Interlocked.Increment(ref reads) == 1)
            {
                entered.Set();
                if (!release.Wait(Timeout)) throw new TimeoutException("Attribute initialization was not released.");
            }
            return backing.f_get(key);
        }
        using var storage = new Storage(backing.f_add, Get, backing.f_getSlice, backing.f_remove,
            () => { }, backing.f_flush, backing.Cache);
        var root = CreateAttributedLeaf(storage, positionsId, normalsId);
        var filtered = FilteredNode.CreateTransient(root, new FilterInsideBox3d(FilterBox));
        storage.Cache.Clear();
        enabled = true;

        var (first, second) = ReadDuringInitialization(
            () => positions ? filtered.Positions : filtered.Normals, entered, release);
        Assert.That(second, Is.SameAs(first));
        Assert.That(reads, Is.EqualTo(1));
        Assert.That(first.Value.Length, Is.EqualTo(2));
    }

    [Test]
    public void ConcurrentAttributeReadsRemainAligned()
    {
        using var storage = PointCloud.CreateInMemoryStore();
        var root = CreateAttributedLeaf(storage, Guid.NewGuid(), Guid.NewGuid());
        for (var round = 0; round < 16; round++)
        {
            var filtered = FilteredNode.CreateTransient(root, new FilterInsideBox3d(FilterBox));
            Parallel.For(0, 8, i =>
            {
                // Vary the first cache touched by each reader.
                if (i % 3 == 0) _ = filtered.PositionsAbsolute;
                else if (i % 3 == 1) _ = filtered.Normals.Value;
                else _ = filtered.PartIndexRange;

                Assert.That(filtered.PositionsAbsolute, Is.EqualTo(new[] { new V3d(0.25), new V3d(0.25, 0.75, 0.75) }));
                Assert.That(filtered.Colors.Value, Is.EqualTo(new[] { C4b.Red, C4b.Blue }));
                Assert.That(filtered.Normals.Value, Is.EqualTo(new[] { V3f.IOO, V3f.OOI }));
                Assert.That(filtered.Intensities.Value, Is.EqualTo(new[] { 10, 30 }));
                Assert.That(filtered.Classifications.Value, Is.EqualTo(new byte[] { 1, 3 }));
                Assert.That(filtered.TryGetPartIndices(out var parts), Is.True);
                Assert.That(parts, Is.EqualTo(new[] { 7, 11 }));
                Assert.That(filtered.PartIndexRange, Is.EqualTo(new Range1i(7, 11)));
                Assert.That(filtered.BoundingBoxExactLocal, Is.EqualTo(new Box3f(filtered.Positions.Value)));
                var positions = filtered.Positions.Value;
                var nearest = filtered.KdTree.Value.GetClosest(positions[0], float.MaxValue, 2);
                Assert.That(nearest.Select(hit => positions[hit.Index]), Is.EquivalentTo(positions));
            });
        }
    }
}
