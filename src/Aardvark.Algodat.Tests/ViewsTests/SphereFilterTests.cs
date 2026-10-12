using Aardvark.Base;
using Aardvark.Data;
using Aardvark.Data.Points;
using Aardvark.Geometry.Points;
using NUnit.Framework;
using System;
using System.Linq;

namespace Aardvark.Geometry.Tests;

[TestFixture]
public class SphereFilterTests
{
    private static bool Contains(Sphere3d sphere, V3d p)
    {
        var dx = p.X - sphere.Center.X;
        var dy = p.Y - sphere.Center.Y;
        var dz = p.Z - sphere.Center.Z;
        return dx * dx + dy * dy + dz * dz <= sphere.Radius * sphere.Radius;
    }

    private static bool CornersInside(Sphere3d sphere, Box3d box)
    {
        for (var i = 0; i < 8; i++)
        {
            var p = new V3d((i & 1) == 0 ? box.Min.X : box.Max.X,
                (i & 2) == 0 ? box.Min.Y : box.Max.Y, (i & 4) == 0 ? box.Min.Z : box.Max.Z);
            if (!Contains(sphere, p)) return false;
        }
        return true;
    }

    private static IPointCloudNode BoundsNode(Box3d box) => new PointSetNode(Storage.None, false,
        (Durable.Octree.NodeId, Guid.Empty), (Durable.Octree.Cell, Cell.Unit),
        (Durable.Octree.BoundingBoxExactGlobal, box), (Durable.Octree.PointCountTreeLeafs, 0L));

    [Test]
    public void ContainmentOverloadsMatchIndependentCornersForFiniteAndCompatibilityInputs()
    {
        void Check(Box3d box, V3d center, params double[] radii)
        {
            var node = BoundsNode(box);
            foreach (var radius in radii)
            {
                var sphere = new Sphere3d(center, radius);
                var filter = new FilterInsideSphere3d(sphere);
                var expected = CornersInside(sphere, box);
                var context = $"box={box}, center={center}, radius={radius:R}";
                Assert.That(filter.IsFullyInside(box), Is.EqualTo(expected), $"box overload: {context}");
                Assert.That(filter.IsFullyInside(node), Is.EqualTo(expected), $"node overload: {context}");
            }
        }

        var random = new Random(17341);
        for (var i = 0; i < 1024; i++)
        {
            var scale = Math.Pow(2, random.Next(-450, 451));
            V3d Vector() => scale * new V3d(random.NextDouble() * 8 - 4,
                random.NextDouble() * 8 - 4, random.NextDouble() * 8 - 4);
            var a = Vector();
            var b = Vector();
            var box = new Box3d(new V3d(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z)),
                new V3d(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z)));
            Check(box, Vector(), 0, scale * random.NextDouble() * 12, -scale * random.NextDouble() * 12);
        }

        var boundary = new Box3d(new V3d(-1, -2, -2), new V3d(1, 2, 2));
        Check(boundary, V3d.Zero, 3, -3, Math.BitDecrement(3), Math.BitIncrement(3));
        Check(new Box3d(new V3d(-double.MaxValue), new V3d(double.MaxValue)), new V3d(double.MaxValue),
            1, double.MaxValue, double.PositiveInfinity);
        foreach (var infinity in new[] { double.NegativeInfinity, double.PositiveInfinity })
        {
            var point = new V3d(infinity, 0, 0);
            Check(new Box3d(point, point), point, 1, double.PositiveInfinity);
            Check(new Box3d(new V3d(-1), point), point, 1, double.PositiveInfinity);
        }
        Assert.That(CornersInside(new Sphere3d(V3d.Zero, 3), boundary), Is.True, "Exactly on the surface.");
        Assert.That(CornersInside(new Sphere3d(V3d.Zero, Math.BitDecrement(3)), boundary), Is.False);
        foreach (var box in new[] { new Box3d(V3d.Zero, V3d.Zero),
            new Box3d(new V3d(-1, 0, 0), V3d.IOO), new Box3d(new V3d(-1, -1, 0), new V3d(1, 1, 0)),
            new Box3d(V3d.III, -V3d.III), Box3d.Invalid,
            new Box3d(new V3d(-double.MaxValue), new V3d(double.MaxValue)),
            new Box3d(new V3d(double.Epsilon), new V3d(double.Epsilon)) })
            Check(box, V3d.Zero, 0, -0.0, 1, -1, double.Epsilon, double.MaxValue,
                double.PositiveInfinity, double.NegativeInfinity, double.NaN);

        // Place each special value in either endpoint or the center on every axis.
        // This catches asymmetric max implementations that accidentally discard NaN.
        foreach (var value in new[] { double.NegativeInfinity, -double.MaxValue, -0.0,
            0.0, double.MaxValue, double.PositiveInfinity, double.NaN })
        for (var axis = 0; axis < 3; axis++)
        {
            var min = -V3d.III;
            var max = V3d.III;
            var center = V3d.Zero;
            min[axis] = value;
            Check(new Box3d(min, max), center, 0, 2, -2, double.MaxValue, double.PositiveInfinity, double.NaN);
            min = -V3d.III;
            max[axis] = value;
            Check(new Box3d(min, max), center, 0, 2, -2, double.MaxValue, double.PositiveInfinity, double.NaN);
            max = V3d.III;
            center[axis] = value;
            Check(new Box3d(min, max), center, 0, 2, -2, double.MaxValue, double.PositiveInfinity, double.NaN);
        }
    }

    [Test]
    public void AttributedLeafAndSubdividedViewsAndReloadsMatchStoredMembership()
    {
        var leaf = new[] { V3d.Zero, V3d.IOO, -V3d.IOO, V3d.OIO, V3d.OOI,
            new V3d(.5, .5, .5), new V3d(1.25, 0, 0), new V3d(0, -1.25, 0) };
        var grid = Enumerable.Range(0, 729).Select(i =>
            new V3d((i / 81 - 4) * .25, (i / 9 % 9 - 4) * .25, (i % 9 - 4) * .25)).ToArray();
        foreach (var positions in new[] { leaf, grid })
        foreach (var offset in new[] { V3d.Zero, new V3d(4, -2, 8) })
        {
            using var storage = PointCloud.CreateInMemoryStore(cache: new LruDictionary<string, object>(1L << 24));
            var ids = Enumerable.Range(0, positions.Length).ToArray();
            var chunk = new Chunk(positions.Select(p => p + offset).ToArray(),
                ids.Select(i => new C4b((byte)i, (byte)(i >> 8), (byte)(255 - i % 256), (byte)255)).ToArray(),
                ids.Select(i => i % 3 == 0 ? V3f.IOO : i % 3 == 1 ? V3f.OIO : V3f.OOI).ToArray(),
                ids.Select(i => 1000 + i).ToArray(), ids.Select(i => (byte)(i % 31)).ToArray(),
                ids.Select(i => 20000 + i).ToArray(), null, null);
            var set = PointCloud.Chunks(chunk, ImportConfig.Default.WithStorage(storage).WithKey("sphere")
                .WithOctreeSplitLimit(positions.Length == leaf.Length ? 64 : 8));
            var source = set.Root.Value;
            Assert.That(source.IsLeaf(), Is.EqualTo(positions.Length == leaf.Length));
            (V3d Position, C4b Color, V3f Normal, int Intensity, byte Classification, int Part)[] Rows(IPointCloudNode node)
                => node.QueryAllPoints().SelectMany(c =>
                {
                    var parts = c.TryGetPartIndices();
                    return Enumerable.Range(0, c.Count).Select(i =>
                        (c.Positions[i], c.Colors[i], c.Normals[i], c.Intensities[i], c.Classifications[i], parts[i]));
                }).ToArray();
            var rows = Rows(source);
            Assert.That(rows.Length, Is.EqualTo(positions.Length));
            foreach (var sphere in new[] { new Sphere3d(offset, 2), new Sphere3d(offset, 1),
                new Sphere3d(offset, .6), new Sphere3d(offset, 0), new Sphere3d(offset + new V3d(3, 0, 0), .25) })
            {
                var expected = rows.Where(row => Contains(sphere, row.Position)).ToArray();
                var filter = new FilterInsideSphere3d(sphere);
                var view = FilteredNode.Create(source, filter);
                var context = $"points={positions.Length}, offset={offset}, sphere={sphere}";
                Assert.That(Rows(view), Is.EqualTo(expected), context);
                Assert.That(view.PositionsAbsolute,
                    Is.EqualTo(source.PositionsAbsolute.Where(p => Contains(sphere, p))), context);
                var json = filter.Serialize().ToJsonString();
                for (var round = 0; round < 2; round++)
                {
                    storage.Cache.Clear();
                    var reloaded = (FilteredNode)storage.GetPointCloudNode(view.Id);
                    Assert.That(Rows(reloaded), Is.EqualTo(expected), $"reload={round}, {context}");
                    Assert.That(reloaded.Filter.Serialize().ToJsonString(), Is.EqualTo(json), context);
                    Assert.That(reloaded.Filter.Equals(filter), Is.True, context);
                }
                Assert.That(Rows(source), Is.EqualTo(rows), $"Backing data unchanged: {context}");
            }
        }
    }

    [Test]
    [NonParallelizable]
    public void WarmedContainmentOverloadsDoNotAllocate()
    {
        var boxes = new[] { Box3d.Unit, new Box3d(new V3d(-2), new V3d(2)),
            new Box3d(new V3d(-.5, 0, 0), new V3d(2, 0, 0)),
            new Box3d(V3d.IOO, V3d.IOO), Box3d.Invalid };
        var filters = boxes.Select(_ => new FilterInsideSphere3d(new Sphere3d(V3d.Zero, 1))).ToArray();
        var nodes = boxes.Select(BoundsNode).ToArray();
        var expected = boxes.Select(b => CornersInside(new Sphere3d(V3d.Zero, 1), b)).ToArray();
        var allocations = new long[boxes.Length * 2];
        var counts = new int[allocations.Length];
        const int iterations = 4096;
        for (var entry = 0; entry < allocations.Length; entry++)
        {
            var i = entry % boxes.Length;
            var byNode = entry >= boxes.Length;
            for (var warm = 0; warm < iterations; warm++)
                if (byNode ? filters[i].IsFullyInside(nodes[i]) : filters[i].IsFullyInside(boxes[i])) counts[entry]++;
            counts[entry] = 0;
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var j = 0; j < iterations; j++)
                if (byNode ? filters[i].IsFullyInside(nodes[i]) : filters[i].IsFullyInside(boxes[i])) counts[entry]++;
            allocations[entry] = GC.GetAllocatedBytesForCurrentThread() - before;
        }
        Assert.Multiple(() =>
        {
            for (var entry = 0; entry < allocations.Length; entry++)
            {
                var i = entry % boxes.Length;
                var context = $"overload={(entry >= boxes.Length ? "node" : "box")}, bounds={boxes[i]}";
                Assert.That(counts[entry], Is.EqualTo(expected[i] ? iterations : 0), context);
                Assert.That(allocations[entry], Is.Zero, context);
            }
        });
    }
}
