using Aardvark.Base;
using Aardvark.Data;
using Aardvark.Data.Points;
using Aardvark.Geometry.Points;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry.Tests;

[TestFixture]
public class CellOwnershipTests
{
    private sealed class Fixture : IDisposable
    {
        public readonly Storage Store = PointCloud.CreateInMemoryStore();
        public readonly V3d[] Positions;
        public readonly bool Attributes;
        public readonly PointSetNode Root;
        public readonly PointSet PointSet;
        public readonly Dictionary<V3d, int> Ids;

        public Fixture(V3d[] positions, V3i offset, int depth, bool attributes)
        {
            Positions = positions.Select(p => p + (V3d)offset).ToArray();
            Ids = Positions.Select((p, i) => (p, i)).ToDictionary(x => x.p, x => x.i);
            Attributes = attributes;
            Root = Build(new Cell(offset.X, offset.Y, offset.Z, 0), Enumerable.Range(0, Positions.Length).ToArray(), depth);
            PointSet = new PointSet(Store, "cell-ownership", Root, 1);
        }

        private PointSetNode Build(Cell cell, int[] ids, int depth)
        {
            var children = new Guid[8];
            if (depth > 0)
            {
                var center = cell.GetCenter();
                for (var octant = 0; octant < 8; octant++)
                {
                    var group = ids.Where(i => ((Positions[i].X >= center.X ? 1 : 0)
                        | (Positions[i].Y >= center.Y ? 2 : 0) | (Positions[i].Z >= center.Z ? 4 : 0)) == octant).ToArray();
                    if (group.Length > 0) children[octant] = Build(cell.GetOctant(octant), group, depth - 1).Id;
                }
            }
            var localIds = depth == 0 ? ids : new[] { ids[0] };
            var local = localIds.Select(i => (V3f)(Positions[i] - cell.GetCenter())).ToArray();
            var data = new List<(Durable.Def, object)>
            {
                (Durable.Octree.NodeId, Guid.NewGuid()), (Durable.Octree.Cell, cell),
                (Durable.Octree.PositionsLocal3f, local), (Durable.Octree.PointCountCell, local.Length),
                (Durable.Octree.PointCountTreeLeafs, (long)ids.Length),
                (Durable.Octree.BoundingBoxExactLocal, new Box3f(local)),
                (Durable.Octree.BoundingBoxExactGlobal, new Box3d(ids.Select(i => Positions[i])))
            };
            if (depth > 0) data.Add((Durable.Octree.SubnodesGuids, children));
            if (Attributes)
            {
                data.Add((Durable.Octree.Colors4b, localIds.Select(Color).ToArray()));
                data.Add((Durable.Octree.Normals3f, localIds.Select(Normal).ToArray()));
                data.Add((Durable.Octree.Intensities1i, localIds.Select(i => 2000 + i).ToArray()));
                data.Add((Durable.Octree.Classifications1b, localIds.Select(i => (byte)(i % 251)).ToArray()));
                var partId = Guid.NewGuid();
                Store.Add(partId, localIds.Select(i => 1000 + i).ToArray());
                data.Add((Durable.Octree.PerPointPartIndex1iReference, partId));
                data.Add((Durable.Octree.PartIndexRange, new Range1i(1000 + ids.Min(), 1000 + ids.Max())));
            }
            return new PointSetNode(Store, true, data.ToArray());
        }

        public void Dispose() => Store.Dispose();
    }

    private static C4b Color(int id) => new((byte)(id % 200), (byte)(3 * id % 255), (byte)17, (byte)201);
    private static V3f Normal(int id) => new(id + 1, 2, -3);
    private static V3d[] Diagonal(params double[] values) => values.Select(v => new V3d(v, v, v)).ToArray();
    private static V3d[] Grid() => (from x in new[] { 0.0, .25, .5, .75, 1.0 }
                                   from y in new[] { 0.0, .25, .5, .75, 1.0 }
                                   from z in new[] { 0.0, .25, .5, .75, 1.0 }
                                   select new V3d(x, y, z)).ToArray();
    private static readonly V3i[] Offsets = { V3i.Zero, new(-2, -3, -1), new(8, -4, 2) };

    // Independent grid-index reference: floor selects the upper side of an internal
    // boundary, while a point at the real root maximum stays in its last cell.
    private static Cell Owner(V3d p, Cell root, int exponent)
    {
        var max = root.BoundingBox.Max;
        var size = Math.Pow(2, exponent);
        long Axis(double v, double outerMax)
        {
            var q = v / size;
            var index = (long)Math.Floor(q);
            return v == outerMax && q == index ? index - 1 : index;
        }
        return new Cell(Axis(p.X, max.X), Axis(p.Y, max.Y), Axis(p.Z, max.Z), exponent);
    }

    private static IEnumerable<IPointCloudNode> Front(IPointCloudNode n, int depth)
    {
        if (depth == 0 || n.IsLeaf) { yield return n; yield break; }
        foreach (var child in n.Subnodes)
            if (child != null)
                foreach (var leaf in Front(child.Value, depth - 1)) yield return leaf;
    }

    private static IPointCloudNode Resolve(IPointCloudNode root, Cell query)
    {
        if (!root.Cell.Contains(query)) return root;
        var n = root;
        while (n.Cell != query && !n.IsLeaf)
        {
            var center = n.Cell.GetCenter();
            var q = query.GetCenter();
            var i = (q.X >= center.X ? 1 : 0) | (q.Y >= center.Y ? 2 : 0) | (q.Z >= center.Z ? 4 : 0);
            var child = n.Subnodes[i];
            if (child == null) break;
            n = child.Value;
        }
        return n;
    }

    private static int[] Expected(Fixture fixture, IPointCloudNode root, Cell query, int depth)
    {
        var node = Resolve(root, query);
        return Front(node, depth).SelectMany(n => n.PositionsAbsolute)
            .Where(p => Owner(p, root.Cell, query.Exponent) == query)
            .Select(p => fixture.Ids[p]).OrderBy(i => i).ToArray();
    }

    private static void Check(Fixture fixture, IEnumerable<Chunk> sequence, int[] expected, string context)
    {
        var ids = new List<int>();
        foreach (var chunk in sequence)
        {
            if (chunk.Count == 0) continue;
            var parts = chunk.TryGetPartIndices();
            var chunkIds = new List<int>();
            for (var j = 0; j < chunk.Count; j++)
            {
                var p = chunk.Positions[j];
                Assert.That(double.IsFinite(p.LengthSquared), Is.True, context);
                var id = fixture.Ids[p];
                ids.Add(id); chunkIds.Add(id);
                if (fixture.Attributes)
                {
                    Assert.That(chunk.Colors[j], Is.EqualTo(Color(id)), context + $", color id={id}");
                    Assert.That(chunk.Normals[j], Is.EqualTo(Normal(id)), context + $", normal id={id}");
                    Assert.That(chunk.Intensities[j], Is.EqualTo(2000 + id), context + $", intensity id={id}");
                    Assert.That(chunk.Classifications[j], Is.EqualTo((byte)(id % 251)), context + $", class id={id}");
                    Assert.That(parts[j], Is.EqualTo(1000 + id), context + $", part id={id}");
                }
            }
            if (fixture.Attributes)
                Assert.That(chunk.PartIndexRange, Is.EqualTo(new Range1i(1000 + chunkIds.Min(), 1000 + chunkIds.Max())), context);
            else
            {
                Assert.That(chunk.Colors, Is.Null, context); Assert.That(chunk.Normals, Is.Null, context);
                Assert.That(chunk.Intensities, Is.Null, context); Assert.That(chunk.Classifications, Is.Null, context);
                Assert.That(chunk.PartIndices, Is.Null, context);
            }
        }
        var result = ids.OrderBy(i => i).ToArray();
        Assert.That(result, Is.EqualTo(expected), context + ", ordered identities including multiplicity");
    }

    [Test]
    public void DiagonalAndFaceEdgeCornerBoundariesHaveOneOwnerInLeafAndSubdividedTrees()
    {
        foreach (var points in new[] { Diagonal(.25, .5, .75), Grid() })
        foreach (var offset in Offsets)
        foreach (var depth in new[] { 0, 1, 2 })
        foreach (var attributes in new[] { false, true })
        using (var fixture = new Fixture(points, offset, depth, attributes))
        foreach (var exponent in new[] { -1, -2 })
        Assert.Multiple(() =>
        {
            var context = $"points={points.Length}, offset={offset}, storedDepth={depth}, attributes={attributes}, exponent={exponent}";
            var cells = fixture.Root.EnumerateCells(exponent).ToArray();
            var owners = fixture.Positions.Select(p => Owner(p, fixture.Root.Cell, exponent)).Distinct().ToArray();
            Assert.That(cells.Select(c => c.Cell), Is.EquivalentTo(owners), context);
            foreach (var cell in cells)
                Check(fixture, cell.GetPoints(8), Enumerable.Range(0, points.Length).Where(i => Owner(fixture.Positions[i], fixture.Root.Cell, exponent) == cell.Cell).ToArray(), context + $", cell={cell.Cell}");
            Check(fixture, cells.SelectMany(c => c.GetPoints(8)), Enumerable.Range(0, points.Length).ToArray(), context + ", full-resolution union");
        });
    }

    [Test]
    public void MissingChildAndVirtualFallbacksUseUpperOwnershipAndKeepOuterRootMaxima()
    {
        foreach (var offset in Offsets)
        foreach (var attributes in new[] { false, true })
        foreach (var reload in new[] { false, true })
        using (var fixture = new Fixture(Diagonal(.5, .75, 1), offset, 1, attributes))
        Assert.Multiple(() =>
        {
            if (reload) fixture.Store.Cache.Clear();
            var root = reload ? fixture.Store.GetPointCloudNode(fixture.Root.Id) : fixture.Root;
            foreach (var exponent in new[] { -1, -2 })
            {
                var side = 1 << -exponent;
                for (var x = 0; x < side; x++)
                for (var y = 0; y < side; y++)
                for (var z = 0; z < side; z++)
                foreach (var depth in new[] { 0, 1, 8 })
                {
                    var cell = new Cell(offset.X * side + x, offset.Y * side + y, offset.Z * side + z, exponent);
                    Check(fixture, root.QueryCell(cell).GetPoints(depth), Expected(fixture, root, cell, depth),
                        $"offset={offset}, attributes={attributes}, reload={reload}, cell={cell}, depth={depth}");
                }
            }
            var parent = root.Cell.Parent;
            Check(fixture, root.QueryCell(parent).GetPoints(8), Expected(fixture, root, parent, 8), $"offset={offset}, containing parent");
        });
    }

    [Test]
    public void StridesLodRepeatedEnumerationAndChunkOwnershipKeepTheirContracts()
    {
        foreach (var offset in Offsets)
        foreach (var storedDepth in new[] { 0, 2 })
        using (var fixture = new Fixture(Grid(), offset, storedDepth, true))
        foreach (var exponent in new[] { -1, -2 })
        foreach (var stride in new[] { V3i.III, new V3i(2, 1, 2), new V3i(2, 2, 2), new V3i(3, 2, 1) })
        Assert.Multiple(() =>
        {
            var context = $"offset={offset}, storedDepth={storedDepth}, exponent={exponent}, stride={stride}";
            var sequence = fixture.PointSet.EnumerateCells(exponent, stride);
            var first = sequence.ToArray();
            Assert.That(sequence.Select(c => c.Cell), Is.EqualTo(first.Select(c => c.Cell)), context + ", repeated cell sequence");
            var expectedCells = fixture.Positions.Select(p => Owner(p, fixture.Root.Cell, exponent)).Distinct()
                .Where(c => c.X % stride.X == 0 && c.Y % stride.Y == 0 && c.Z % stride.Z == 0).ToArray();
            Assert.That(first.Select(c => c.Cell), Is.EquivalentTo(expectedCells), context);
            foreach (var result in first)
            foreach (var depth in new[] { 0, 1, 8, int.MaxValue })
            {
                var expected = Expected(fixture, fixture.Root, result.Cell, depth);
                var chunks = result.GetPoints(depth).ToArray();
                Check(fixture, chunks, expected, context + $", cell={result.Cell}, depth={depth}");
                var nonempty = chunks.FirstOrDefault(c => c.Count > 0);
                if (nonempty != null)
                {
                    nonempty.Positions[0] = new V3d(12345);
                    nonempty.Colors[0] = C4b.White; nonempty.Normals[0] = V3f.Zero;
                    nonempty.Intensities[0] = -999; nonempty.Classifications[0] = 255;
                    ((IList<int>)nonempty.PartIndices)[0] = -999;
                }
                Check(fixture, result.GetPoints(depth), expected, context + ", fresh points after result mutation");
            }
            foreach (var node in Front(fixture.Root, 8))
                Check(fixture, new[] { node.ToChunk() }, node.PositionsAbsolute.Select(p => fixture.Ids[p]).OrderBy(i => i).ToArray(), context + ", source attributes unchanged");
        });
    }

    [Test]
    public void ClosedCellBoxColumnsAndArgumentControlsRemainSeparate()
    {
        using var fixture = new Fixture(Diagonal(.25, .5, .75), V3i.Zero, 0, true);
        var lower = new Cell(0, 0, 0, -1);
        var chunk = fixture.Root.ToChunk();
        Check(fixture, new[] { chunk.ImmutableFilterByCell(lower) }, new[] { 0, 1 }, "global cell filter stays closed");
        Check(fixture, new[] { chunk.ImmutableFilterByBox3d(lower.BoundingBox) }, new[] { 0, 1 }, "global box filter stays closed");
        Check(fixture, fixture.Root.QueryPointsInsideBox(lower.BoundingBox), new[] { 0, 1 }, "closed box query unchanged");
        var columns = fixture.Root.EnumerateCellColumns(-1).ToArray();
        var low = columns.Single(c => c.Cell == new Cell2d(0, 0, -1));
        Check(fixture, new[] { low.CollectPoints(8).Points }, new[] { 0, 1 }, "XY split convention unchanged");
        Assert.That(Assert.Throws<ArgumentException>(() => fixture.Root.QueryCell(lower).GetPoints(-1).ToArray())?.ParamName, Is.EqualTo("fromRelativeDepth"));
        Assert.Throws<ArgumentNullException>(() => Queries.QueryCell((IPointCloudNode)null, lower));
        Assert.Throws<ArgumentNullException>(() => Queries.EnumerateCells((IPointCloudNode)null, -1));
        foreach (var stride in new[] { new V3i(0, 1, 1), new V3i(1, -1, 1), new V3i(1, 1, 0) })
            Assert.Throws<InvalidOperationException>(() => fixture.Root.EnumerateCells(-1, stride), $"stride={stride}");
    }
}
