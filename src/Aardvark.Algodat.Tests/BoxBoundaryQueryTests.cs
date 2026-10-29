using Aardvark.Base;
using Aardvark.Data.Points;
using Aardvark.Geometry.Points;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class BoxBoundaryQueryTests
    {
        [Test]
        public void FaceEdgeAndCornerContactsReachClosedContainmentInLeafAndSubdividedClouds()
        {
            foreach (var split in new[] { 1, 32 })
            {
                using var repro = Build(new[] { new V3d(1, 0.25, 0.25), new V3d(2, 0.75, 0.75) }, new Cell(0, 0, 0, 2), split, true);
                Check(repro, Box3d.Unit, int.MinValue, "two-point reproduction");
                Assert.That(repro.Cloud.CountPointsInsideBox(Box3d.Unit), Is.EqualTo(1));
                Assert.That(repro.Cloud.CountPointsApproximatelyInsideBox(Box3d.Unit), Is.GreaterThanOrEqualTo(1));
            }
            for (var x = -1; x <= 1; x++)
            for (var y = -1; y <= 1; y++)
            for (var z = -1; z <= 1; z++)
            {
                if (x == 0 && y == 0 && z == 0) continue;
                foreach (var split in new[] { 1, 32 })
                foreach (var attributes in new[] { false, true })
                {
                    double contact(int d) => d < 0 ? 0 : d > 0 ? 1 : 0.25;
                    double other(int d) => d < 0 ? -0.75 : d > 0 ? 1.75 : 0.75;
                    var points = new[] { new V3d(contact(x), contact(y), contact(z)), new V3d(other(x), other(y), other(z)) };
                    using var f = Build(points, new Cell(x, y, z, 0), split, attributes);
                    Assert.That(f.Root.IsLeaf, Is.EqualTo(split > 1));
                    var unchanged = Snapshot(f.Root);
                    foreach (var cutoff in Cutoffs(f.Root)) Check(f, Box3d.Unit, cutoff, $"contact=({x},{y},{z}), split={split}, attrs={attributes}");
                    unchanged();
                }
            }
        }

        [Test]
        public void DegenerateQueriesAndExactNodeBoundsRetainBoundaryPoints()
        {
            var layouts = new[] {
                new[] { new V3d(1, 1, 0.5) },
                new[] { new V3d(1, 0.25, 0.25), new V3d(1, 0.75, 0.75) },
                new[] { new V3d(1, 1, 0.25), new V3d(1, 1, 0.75) },
                new[] { new V3d(1, 0.25, 0.5), new V3d(1.75, 0.75, 0.5) }
            };
            for (var layout = 0; layout < layouts.Length; layout++)
            foreach (var split in new[] { 1, 32 })
            {
                using var f = Build(layouts[layout], new Cell(0, 0, 0, 1), split, true);
                var p = f.Root.PositionsAbsolute.First();
                var queries = new[] { Box3d.Unit, new Box3d(p, p), new Box3d(new V3d(p.X, 0, 0), new V3d(p.X, 1, 1)), new Box3d(new V3d(p.X, p.Y, 0), new V3d(p.X, p.Y, 1)), new Box3d(new V3d(0, 0, p.Z), new V3d(2, 2, p.Z)),
                    new Box3d(p - V3d.III, new V3d(Math.BitDecrement(p.X), p.Y, p.Z)), new Box3d(new V3d(Math.BitIncrement(p.X), p.Y, p.Z), p + V3d.III) };
                var unchanged = Snapshot(f.Root);
                foreach (var query in queries)
                foreach (var cutoff in Cutoffs(f.Root)) Check(f, query, cutoff, $"degenerate layout={layout}, split={split}");
                unchanged();
            }
        }

        [Test]
        public void LodFrontsAndForwardingKeepOptionalAttributesAlignedWithStoredCoordinates()
        {
            var positions = (from x in new[] { 0.0, 0.5, 1.0, 1.5, 2.0 }
                             from y in new[] { 0.0, 0.5, 1.0, 1.5, 2.0 }
                             from z in new[] { 0.0, 0.5, 1.0, 1.5, 2.0 } select new V3d(x, y, z)).ToArray();
            foreach (var attributes in new[] { false, true })
            {
                using var f = Build(positions, new Cell(0, 0, 0, 2), 4, attributes);
                Assert.That(f.Root.IsLeaf, Is.False); Assert.That(f.Root.PointCountCell, Is.LessThan(f.Root.PointCountTree));
                var queries = new[] { Box3d.Unit, new Box3d(V3d.III, V3d.III), new Box3d(new V3d(1, 0, 0), new V3d(1, 2, 2)), new Box3d(new V3d(0.1), new V3d(0.9)), new Box3d(new V3d(5), new V3d(6)), new Box3d(V3d.Zero, new V3d(4)) };
                var cutoffs = Nodes(f.Root).Select(n => n.Cell.Exponent).Append(f.Root.Cell.Exponent + 1).Append(int.MinValue).Distinct().ToArray();
                var unchanged = Snapshot(f.Root);
                foreach (var query in queries)
                foreach (var cutoff in cutoffs) Check(f, query, cutoff, $"LoD attrs={attributes}");
                unchanged();
            }
        }

        [Test]
        public void InteriorDisjointAndEmptyEnumerationAndCountsRetainTheirContracts()
        {
            var positions = new[] { new V3d(0.25), new V3d(0.75), new V3d(1.25), new V3d(1.75) };
            foreach (var split in new[] { 1, 32 })
            {
                using var f = Build(positions, new Cell(0, 0, 0, 1), split, false);
                foreach (var query in new[] { new Box3d(V3d.Zero, new V3d(2)), new Box3d(new V3d(0.1), new V3d(0.9)), new Box3d(new V3d(3), new V3d(4)), Box3d.Invalid })
                    foreach (var cutoff in Cutoffs(f.Root)) Check(f, query, cutoff, $"ordinary split={split}");
                foreach (var query in new[] { new Box3d(V3d.Zero, new V3d(2)), new Box3d(new V3d(0.1), new V3d(0.9)), new Box3d(new V3d(3), new V3d(4)) })
                {
                    var stored = Front(f.Root, int.MinValue).SelectMany(Rows).ToArray();
                    Assert.That(Rows(f.Cloud.QueryPointsOutsideBox(query)), Is.EqualTo(stored.Where(r => !query.Contains(r.Position)).ToArray()));
                    Assert.That(f.Cloud.CountPointsOutsideBox(query), Is.EqualTo(stored.LongCount(r => !query.Contains(r.Position))));
                    Assert.That(Rows(f.Cloud.QueryPointsOutsideBoxXY(query.XY)), Is.EqualTo(stored.Where(r => !query.XY.Contains(r.Position.XY)).ToArray()));
                    Assert.That(f.Cloud.CountPointsOutsideBoxXY(query.XY), Is.EqualTo(stored.LongCount(r => !query.XY.Contains(r.Position.XY))));
                }
            }
            using var empty = new Fixture(PointCloud.CreateInMemoryStore(), null);
            foreach (var query in new[] { Box3d.Unit, new Box3d(V3d.Zero, V3d.Zero), Box3d.Invalid }) Check(empty, query, int.MinValue, "empty counts/enumeration only");
        }

        private sealed class Fixture : IDisposable
        {
            public Storage Storage { get; }
            public PointSet Cloud { get; }
            public IPointCloudNode Root => Cloud.Root.Value;
            public Fixture(Storage storage, PointSet cloud) { Storage = storage; Cloud = cloud ?? new PointSet(storage, "empty"); }
            public void Dispose() => Storage.Dispose();
        }
        private static Fixture Build(V3d[] positions, Cell cell, int split, bool attributes)
        {
            var storage = PointCloud.CreateInMemoryStore();
            try
            {
                var ids = Enumerable.Range(0, positions.Length).ToArray();
                var chunk = new Chunk(positions,
                    attributes ? ids.Select(i => new C4b((byte)i, (byte)(255 - i), (byte)(i ^ 63), (byte)231)).ToArray() : null,
                    attributes ? ids.Select(i => new V3f(i + 1, 2, 3)).ToArray() : null,
                    attributes ? ids.Select(i => i + 100).ToArray() : null,
                    attributes ? ids.Select(i => (byte)(i % 251)).ToArray() : null,
                    attributes ? ids.Select(i => i + 1000).ToArray() : null, null, null);
                var root = InMemoryPointSet.Build(chunk, cell, split).ToPointSetNode(storage, isTemporaryImportNode: true);
                var cloud = new PointSet(storage, "boundary", root, split);
                if (!root.IsLeaf) cloud = cloud.GenerateLod(ImportConfig.Default.WithStorage(storage).WithKey("lod"));
                Assert.That(cloud.Root.Value.Cell, Is.EqualTo(cell));
                return new Fixture(storage, cloud);
            }
            catch { storage.Dispose(); throw; }
        }
        private static int[] Cutoffs(IPointCloudNode node) => new[] { int.MinValue, node.Cell.Exponent - 1, node.Cell.Exponent, node.Cell.Exponent + 1 }.Distinct().ToArray();
        private static IEnumerable<IPointCloudNode> Nodes(IPointCloudNode node)
        {
            yield return node;
            if (!node.IsLeaf) foreach (var child in node.Subnodes) if (child != null) foreach (var n in Nodes(child.Value)) yield return n;
        }
        private static IEnumerable<IPointCloudNode> Front(IPointCloudNode node, int cutoff)
        {
            if (node.Cell.Exponent < cutoff) yield break;
            if (node.IsLeaf || node.Cell.Exponent == cutoff) { yield return node; yield break; }
            foreach (var child in node.Subnodes) if (child != null) foreach (var n in Front(child.Value, cutoff)) yield return n;
        }
        private sealed record Row(V3d Position, C4b? Color, V3f? Normal, int? Intensity, byte? Classification, int? Part);
        private static IEnumerable<Row> Rows(IPointCloudNode node)
        {
            var ps = node.PositionsAbsolute; node.TryGetPartIndices(out var parts);
            for (var i = 0; i < ps.Length; i++) yield return new Row(ps[i], node.HasColors ? node.Colors.Value[i] : null, node.HasNormals ? node.Normals.Value[i] : null,
                node.HasIntensities ? node.Intensities.Value[i] : null, node.HasClassifications ? node.Classifications.Value[i] : null, parts != null ? parts[i] : null);
        }
        private static Row[] Rows(IEnumerable<Chunk> chunks)
        {
            var result = new List<Row>();
            foreach (var chunk in chunks)
            {
                var n = chunk.Positions.Count; var parts = chunk.TryGetPartIndices();
                foreach (var count in new int?[] { chunk.Colors?.Count, chunk.Normals?.Count, chunk.Intensities?.Count, chunk.Classifications?.Count, parts?.Count })
                    if (count.HasValue) Assert.That(count.Value, Is.EqualTo(n), "attribute alignment");
                for (var i = 0; i < n; i++)
                {
                    Assert.That(chunk.Positions[i].IsNaN || chunk.Positions[i].IsInfinity, Is.False);
                    result.Add(new Row(chunk.Positions[i], chunk.Colors?[i], chunk.Normals?[i], chunk.Intensities?[i], chunk.Classifications?[i], parts?[i]));
                }
            }
            return result.ToArray();
        }
        private static void Check(Fixture f, Box3d query, int cutoff, string label)
        {
            var context = $"{label}, query={query}, cutoff={cutoff}"; var front = Front(f.Root, cutoff).ToArray(); var stored = front.SelectMany(Rows).ToArray();
            var inside3 = stored.Where(r => query.Contains(r.Position)).ToArray(); var inside2 = stored.Where(r => query.XY.Contains(r.Position.XY)).ToArray();
            Assert.That(Rows(f.Root.QueryPointsInsideBox(query, cutoff)), Is.EqualTo(inside3), context + " node 3D");
            Assert.That(Rows(f.Cloud.QueryPointsInsideBox(query, cutoff)), Is.EqualTo(inside3), context + " PointSet 3D");
            Assert.That(f.Root.CountPointsInsideBox(query, cutoff), Is.EqualTo(inside3.LongLength), context + " count 3D");
            Assert.That(f.Cloud.CountPointsInsideBox(query, cutoff), Is.EqualTo(inside3.LongLength), context + " forwarded count 3D");
            Assert.That(Rows(f.Root.QueryPointsInsideBoxXY(query.XY, cutoff)), Is.EqualTo(inside2), context + " node XY");
            Assert.That(Rows(f.Cloud.QueryPointsInsideBoxXY(query.XY, cutoff)), Is.EqualTo(inside2), context + " PointSet XY");
            Assert.That(f.Root.CountPointsInsideBoxXY(query.XY, cutoff), Is.EqualTo(inside2.LongLength), context + " count XY");
            Assert.That(f.Cloud.CountPointsInsideBoxXY(query.XY, cutoff), Is.EqualTo(inside2.LongLength), context + " forwarded count XY");
            // Empty existence is a separate contract; do not widen this fix to cover it.
            if (stored.Length > 0) Assert.That(f.Root.QueryContainsPointsInsideBoxXY(query.XY, cutoff), Is.EqualTo(inside2.Length > 0), context + " existence XY");
            var approx3 = front.Where(n => Overlap(query, n.BoundingBoxExactGlobal)).Sum(n => (long)n.PointCountCell);
            var approx2 = front.Where(n => Overlap(query.XY, n.BoundingBoxExactGlobal.XY)).Sum(n => (long)n.PointCountCell);
            Assert.That(approx3, Is.GreaterThanOrEqualTo(inside3.LongLength), context); Assert.That(approx2, Is.GreaterThanOrEqualTo(inside2.LongLength), context);
            Assert.That(f.Root.CountPointsApproximatelyInsideBox(query, cutoff), Is.EqualTo(approx3), context + " approximate 3D");
            Assert.That(f.Cloud.CountPointsApproximatelyInsideBox(query, cutoff), Is.EqualTo(approx3), context + " forwarded approximate 3D");
            Assert.That(f.Root.CountPointsApproximatelyInsideBoxXY(query.XY, cutoff), Is.EqualTo(approx2), context + " approximate XY");
            Assert.That(f.Cloud.CountPointsApproximatelyInsideBoxXY(query.XY, cutoff), Is.EqualTo(approx2), context + " forwarded approximate XY");
        }
        private static bool Overlap(Box2d a, Box2d b) => Math.Max(a.Min.X, b.Min.X) <= Math.Min(a.Max.X, b.Max.X) && Math.Max(a.Min.Y, b.Min.Y) <= Math.Min(a.Max.Y, b.Max.Y);
        private static bool Overlap(Box3d a, Box3d b) => Overlap(a.XY, b.XY) && Math.Max(a.Min.Z, b.Min.Z) <= Math.Min(a.Max.Z, b.Max.Z);
        private static Action Snapshot(IPointCloudNode root)
        {
            var nodes = Nodes(root).ToArray(); var positions = nodes.Select(n => n.Positions.Value).ToArray(); var copies = positions.Select(p => p.ToArray()).ToArray(); var rows = nodes.Select(n => Rows(n).ToArray()).ToArray();
            var bounds = nodes.Select(n => n.BoundingBoxExactGlobal).ToArray(); var cells = nodes.Select(n => n.Cell).ToArray(); var counts = nodes.Select(n => (n.PointCountCell, n.PointCountTree)).ToArray();
            return () => { for (var i = 0; i < nodes.Length; i++) {
                Assert.That(nodes[i].Positions.Value, Is.SameAs(positions[i])); Assert.That(positions[i], Is.EqualTo(copies[i])); Assert.That(Rows(nodes[i]).ToArray(), Is.EqualTo(rows[i]));
                Assert.That(nodes[i].BoundingBoxExactGlobal, Is.EqualTo(bounds[i])); Assert.That(nodes[i].Cell, Is.EqualTo(cells[i])); Assert.That((nodes[i].PointCountCell, nodes[i].PointCountTree), Is.EqualTo(counts[i]));
            } };
        }
    }
}
