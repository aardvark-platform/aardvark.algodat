using Aardvark.Base;
using Aardvark.Data;
using Aardvark.Data.Points;
using Aardvark.Geometry.Points;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class NearRayClippingTests
    {
        private static readonly Durable.Def Ids = new Durable.Def(new Guid("ce2cb29b-794a-499e-b751-53eb5ad28493"), "NearRayTest.Ids", "Stored point tags", Durable.Primitives.Int32Array.Id, true);
        private static readonly Durable.Def Vectors = new Durable.Def(new Guid("8c28c42c-1005-41ee-abcf-2bc8199ec0c0"), "NearRayTest.Vectors", "Stored vector tags", Durable.Aardvark.V2dArray.Id, true);
        private static readonly Durable.Def Missing = new Durable.Def(new Guid("426d449c-bb90-46c7-a117-45ba59367aab"), "NearRayTest.Missing", "Absent tags", Durable.Primitives.Int32Array.Id, true);
        private static readonly Durable.Def[] Requested = { Ids, Vectors, Missing };
        private static readonly V3d[] BasePoints = (from x in new[] { 0.5, 1, 1.5, 2 } from z in new[] { -0.125, 0.125 } select new V3d(x, 0.5, z)).ToArray();
        private static readonly V3d[] Offsets = { V3d.Zero, new V3d(4, -3, 2) };

        [Test]
        public void OffsetSignedAxisRaysRetainAllNearbyPointsThroughBothAdapters()
        {
            for (var axis = 0; axis < 6; axis++)
            foreach (var offset in Offsets)
            foreach (var split in new[] { 2, 32 })
            {
                using var f = Build(BasePoints.Select(p => Map(p, axis) + offset).ToArray(), split, true);
                Assert.That(f.Root.IsLeaf, Is.EqualTo(split > BasePoints.Length)); var unchanged = Snapshot(f.Root);
                foreach (var speed in new[] { 0.25, 1, 4 })
                {
                    var ray = new Ray3d(Map(new V3d(-1, 0, 0), axis) + offset, Map(V3d.IOO, axis) * speed);
                    Check(f, ray, 0.75, int.MinValue, $"axis={axis}, offset={offset}, split={split}, speed={speed}");
                    Assert.That(f.Cloud.QueryPointsNearRay(ray, 0.75).Sum(c => c.Count), Is.EqualTo(8));
                }
                unchanged();
            }
        }

        [Test]
        public void ObliqueClippingRetainsEndpointCandidatesInsteadOfTruncatingTheSelection()
        {
            foreach (var axis in new[] { 0, 2, 4 })
            foreach (var offset in Offsets)
            foreach (var split in new[] { 2, 32 })
            {
                using var f = Build(BasePoints.Select(p => Map(p, axis) + offset).ToArray(), split, true); var unchanged = Snapshot(f.Root);
                foreach (var sign in new[] { -1, 1 })
                foreach (var speed in new[] { 0.5, 3 })
                foreach (var radius in new[] { 0.2, 0.75 })
                {
                    var origin = new V3d(-1, sign > 0 ? 0 : 1, 0); var direction = new V3d(1, sign * 0.25, 0);
                    var ray = new Ray3d(Map(origin, axis) + offset, Map(direction, axis) * speed);
                    Check(f, ray, radius, int.MinValue, $"oblique axis={axis}, offset={offset}, split={split}, sign={sign}, speed={speed}, radius={radius}");
                    if (radius == 0.75) Assert.That(f.Cloud.QueryPointsNearRay(ray, radius).Sum(c => c.Count), Is.EqualTo(8));
                }
                unchanged();
            }
        }

        [Test]
        public void IntersectingDisjointAndBoundedRayControlsRetainTheirSelectionsAndAttributes()
        {
            for (var axis = 0; axis < 6; axis++)
            foreach (var offset in Offsets)
            foreach (var split in new[] { 2, 32 })
            {
                using var f = Build(BasePoints.Select(p => Map(p, axis) + offset).ToArray(), split, true); var unchanged = Snapshot(f.Root);
                foreach (var y in new[] { 0.5, 3.0 })
                foreach (var speed in new[] { 0.25, 1, 4 })
                {
                    var ray = new Ray3d(Map(new V3d(-1, y, 0), axis) + offset, Map(V3d.IOO, axis) * speed);
                    Check(f, ray, 0.25, int.MinValue, $"control axis={axis}, offset={offset}, split={split}, y={y}, speed={speed}");
                }
                var bounded = new Ray3d(Map(new V3d(-1, 0, 0), axis) + offset, Map(V3d.IOO, axis));
                foreach (var interval in new[] { (Min: 0.0, Max: 10.0), (Min: 1.4, Max: 2.1), (Min: 1.7, Max: 2.7) })
                {
                    var expected = Front(f.Root, int.MinValue).SelectMany(NodeRows).Where(r => Within(r.Position, bounded, 0.75) && Parameter(r.Position, bounded) >= interval.Min && Parameter(r.Position, bounded) <= interval.Max).Select(Standard).ToArray();
                    Assert.That(StandardRows(f.Cloud.QueryPointsNearRay(bounded, 0.75, interval.Min, interval.Max)), Is.EquivalentTo(expected), "bounded PointSet control");
                    Assert.That(StandardRows(f.Root.QueryPointsNearRay(bounded, 0.75, interval.Min, interval.Max)), Is.EquivalentTo(expected), "bounded node control");
                }
                unchanged();
            }
        }

        [Test]
        public void LodCutoffsCustomSelectionAndSourceOwnershipKeepStoredAttributeAlignment()
        {
            foreach (var attributes in new[] { false, true })
            {
                using var f = Build(BasePoints, 2, attributes); var unchanged = Snapshot(f.Root);
                Assert.That(f.Root.IsLeaf, Is.False);
                Assert.That(f.Root.PointCountCell, Is.LessThan(f.Root.PointCountTree));
                var cutoffs = Nodes(f.Root).Select(n => n.Cell.Exponent).Append(f.Root.Cell.Exponent + 1).Append(int.MinValue).Distinct().ToArray();
                foreach (var cutoff in cutoffs)
                foreach (var direction in new[] { V3d.IOO, new V3d(2, 0.5, 0) })
                    Check(f, new Ray3d(new V3d(-1, 0, 0), direction), 0.75, cutoff, $"LoD cutoff={cutoff}, attrs={attributes}");
                unchanged();
            }
        }

        private sealed class Fixture : IDisposable
        {
            public Storage Storage { get; }
            public PointSet Cloud { get; }
            public IPointCloudNode Root => Cloud.Root.Value;
            public Fixture(Storage storage, PointSet cloud) { Storage = storage; Cloud = cloud; }
            public void Dispose() => Storage.Dispose();
        }
        private static Fixture Build(V3d[] points, int split, bool attributes)
        {
            var storage = PointCloud.CreateInMemoryStore();
            try
            {
                var indices = Enumerable.Range(0, points.Length).ToArray();
                var chunk = new Chunk(points, attributes ? indices.Select(i => new C4b((byte)(40 + i), (byte)(100 + i), (byte)(200 - i), (byte)231)).ToArray() : null,
                    attributes ? indices.Select(i => new V3f(1, i + 1, 2)).ToArray() : null,
                    attributes ? indices.Select(i => 100 + i).ToArray() : null, attributes ? indices.Select(i => (byte)(20 + i)).ToArray() : null,
                    attributes ? indices.Select(i => 1000 + i).ToArray() : null, null, null);
                var root = InMemoryPointSet.Build(chunk, new Cell(chunk.BoundingBox), split).ToPointSetNode(storage, isTemporaryImportNode: true);
                var cloud = new PointSet(storage, "ray", root, split).GenerateLod(ImportConfig.Default.WithStorage(storage).WithKey("processed"));
                var tagged = Attach((PointSetNode)cloud.Root.Value);
                cloud = new PointSet(storage, "tagged", tagged, split);
                foreach (var node in Nodes(cloud.Root.Value)) { Assert.That(node.HasKdTree, Is.True); Assert.That(((PointSetNode)node).IsTemporaryImportNode, Is.False); }
                return new Fixture(storage, cloud);
            }
            catch { storage.Dispose(); throw; }
        }
        private static PointSetNode Attach(PointSetNode node)
        {
            var ps = node.PositionsAbsolute; var replacements = new Dictionary<Durable.Def, object> {
                [Ids] = Enumerable.Range(0, ps.Length).Select(i => 17 * i + 101 * node.Cell.Exponent).ToArray(),
                [Vectors] = ps.Select(p => new V2d(p.X + p.Y, p.Z - p.X)).ToArray()
            };
            if (!node.IsLeaf) replacements[Durable.Octree.SubnodesGuids] = node.Subnodes.Select(n => n == null ? Guid.Empty : Attach((PointSetNode)n.Value).Id).ToArray();
            return node.With(replacements).WriteToStore();
        }
        private static V3d Map(V3d p, int axis) => axis switch {
            0 => p, 1 => new V3d(-p.X, p.Y, p.Z), 2 => new V3d(p.Z, p.X, p.Y),
            3 => new V3d(p.Z, -p.X, p.Y), 4 => new V3d(p.Y, p.Z, p.X), _ => new V3d(p.Y, p.Z, -p.X)
        };
        private static IEnumerable<IPointCloudNode> Nodes(IPointCloudNode node)
        {
            yield return node;
            if (!node.IsLeaf) foreach (var child in node.Subnodes) if (child != null) foreach (var n in Nodes(child.Value)) yield return n;
        }
        private static IEnumerable<IPointCloudNode> Front(IPointCloudNode node, int cutoff)
        {
            if (node.IsLeaf || node.Cell.Exponent == cutoff) { yield return node; yield break; }
            foreach (var child in node.Subnodes) if (child != null) foreach (var n in Front(child.Value, cutoff)) yield return n;
        }
        private sealed record Row(V3d Position, C4b? Color, V3f? Normal, int? Intensity, byte? Classification, int? Part, int Id, V2d Vector);
        private sealed record StandardRow(V3d Position, C4b? Color, V3f? Normal, int? Intensity, byte? Classification, int? Part);
        private static StandardRow Standard(Row r) => new StandardRow(r.Position, r.Color, r.Normal, r.Intensity, r.Classification, r.Part);
        private static IEnumerable<Row> NodeRows(IPointCloudNode node)
        {
            var ps = node.PositionsAbsolute; node.TryGetPartIndices(out var parts); var ids = (int[])node.Properties[Ids]; var vectors = (V2d[])node.Properties[Vectors];
            for (var i = 0; i < ps.Length; i++) yield return new Row(ps[i], node.HasColors ? node.Colors.Value[i] : null, node.HasNormals ? node.Normals.Value[i] : null,
                node.HasIntensities ? node.Intensities.Value[i] : null, node.HasClassifications ? node.Classifications.Value[i] : null, parts == null ? null : parts[i], ids[i], vectors[i]);
        }
        private static StandardRow[] StandardRows(IEnumerable<Chunk> chunks)
        {
            var rows = new List<StandardRow>();
            foreach (var chunk in chunks)
            {
                var parts = chunk.TryGetPartIndices(); var count = chunk.Count;
                foreach (var n in new int?[] { chunk.Colors?.Count, chunk.Normals?.Count, chunk.Intensities?.Count, chunk.Classifications?.Count, parts?.Count }) if (n.HasValue) Assert.That(n.Value, Is.EqualTo(count));
                for (var i = 0; i < count; i++) { Assert.That(chunk.Positions[i].IsNaN || chunk.Positions[i].IsInfinity, Is.False); rows.Add(new StandardRow(chunk.Positions[i], chunk.Colors?[i], chunk.Normals?[i], chunk.Intensities?[i], chunk.Classifications?[i], parts?[i])); }
            }
            return rows.ToArray();
        }
        private static void Check(Fixture f, Ray3d ray, double radius, int cutoff, string context)
        {
            var originalRay = ray; var front = Front(f.Root, cutoff).SelectMany(NodeRows).ToArray();
            foreach (var row in front) Assert.That(row.Position.IsNaN || row.Position.IsInfinity, Is.False, context + " stored oracle position");
            var expected = front.Where(r => Within(r.Position, ray, radius)).ToArray();
            Assert.That(StandardRows(f.Cloud.QueryPointsNearRay(ray, radius, cutoff)), Is.EquivalentTo(expected.Select(Standard).ToArray()), context + " standard");
            void CheckCustom(IEnumerable<GenericChunk> chunks, bool tags)
            {
                var rows = new List<(V3d, int, V2d)>(); var positions = new List<V3d>();
                foreach (var chunk in chunks)
                {
                    Assert.That(chunk.Data.Keys, Is.EquivalentTo(tags ? new[] { GenericChunk.Defs.Positions3d, Ids, Vectors } : new[] { GenericChunk.Defs.Positions3d }), context + " requested attributes");
                    var ps = chunk.PositionsAsV3d; positions.AddRange(ps);
                    foreach (var p in ps) Assert.That(p.IsNaN || p.IsInfinity, Is.False, context);
                    if (tags) { var ids = (int[])chunk.Data[Ids]; var vs = (V2d[])chunk.Data[Vectors]; Assert.That(ids.Length, Is.EqualTo(ps.Length)); Assert.That(vs.Length, Is.EqualTo(ps.Length)); for (var i = 0; i < ps.Length; i++) rows.Add((ps[i], ids[i], vs[i])); }
                }
                Assert.That(positions, Is.EquivalentTo(expected.Select(r => r.Position)), context + " custom positions");
                if (tags) Assert.That(rows, Is.EquivalentTo(expected.Select(r => (r.Position, r.Id, r.Vector))), context + " custom alignment");
            }
            CheckCustom(f.Cloud.QueryPointsNearRayCustom(ray, radius, cutoff, Requested), true);
            CheckCustom(f.Cloud.QueryPointsNearRayCustom(ray, radius, cutoff), false);
            if (cutoff == int.MinValue) CheckCustom(f.Cloud.QueryPointsNearRayCustom(ray, radius, Requested), true);
            Assert.That(ray.Origin, Is.EqualTo(originalRay.Origin)); Assert.That(ray.Direction, Is.EqualTo(originalRay.Direction));
        }
        private static double Parameter(V3d p, Ray3d ray)
        {
            var d = ray.Direction; var v = p - ray.Origin;
            return (v.X * d.X + v.Y * d.Y + v.Z * d.Z) / (d.X * d.X + d.Y * d.Y + d.Z * d.Z);
        }
        private static bool Within(V3d p, Ray3d ray, double radius)
        {
            var t = Math.Max(0, Parameter(p, ray)); var x = p.X - ray.Origin.X - t * ray.Direction.X; var y = p.Y - ray.Origin.Y - t * ray.Direction.Y; var z = p.Z - ray.Origin.Z - t * ray.Direction.Z;
            return x * x + y * y + z * z <= radius * radius;
        }
        private static Action Snapshot(IPointCloudNode root)
        {
            var nodes = Nodes(root).ToArray(); var positions = nodes.Select(n => n.Positions.Value).ToArray(); var values = positions.Select(p => p.ToArray()).ToArray(); var rows = nodes.Select(n => NodeRows(n).ToArray()).ToArray();
            var bounds = nodes.Select(n => n.BoundingBoxExactGlobal).ToArray(); var cells = nodes.Select(n => n.Cell).ToArray(); var counts = nodes.Select(n => (n.PointCountCell, n.PointCountTree)).ToArray();
            return () => { for (var i = 0; i < nodes.Length; i++) { Assert.That(nodes[i].Positions.Value, Is.SameAs(positions[i])); Assert.That(positions[i], Is.EqualTo(values[i])); Assert.That(NodeRows(nodes[i]).ToArray(), Is.EqualTo(rows[i])); Assert.That(nodes[i].BoundingBoxExactGlobal, Is.EqualTo(bounds[i])); Assert.That(nodes[i].Cell, Is.EqualTo(cells[i])); Assert.That((nodes[i].PointCountCell, nodes[i].PointCountTree), Is.EqualTo(counts[i])); } };
        }
    }
}
