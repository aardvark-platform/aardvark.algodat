using Aardvark.Base;
using Aardvark.Data.Points;
using Aardvark.Geometry.Points;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class BoundedRayTraversalTests
    {
        [Test]
        public void ResidentUncachedQueriesReadEachOfSeventyTwoChildDescriptorsOnce()
        {
            using var f = Build(8, false, true, "uncached");
            Assert.That(f.Model.All().Count(), Is.EqualTo(73));
            Assert.That(f.Model.Rows.Length, Is.LessThan(512), "root stores a genuinely coarser front");
            var keys = f.Model.Children.Select(c => Vec.Distance(new V3d(c.Bounds.Center), DefaultRay.Origin)).ToArray();
            Assert.That(keys.Distinct().Count(), Is.LessThan(keys.Length), "ordered comparisons include stable distance ties");
            foreach (var radius in new[] { 2.0, 0.14 })
            {
                f.Reset(); var rows = Check(f, DefaultRay, radius, 0, 3, int.MinValue, $"resident uncached radius={radius}");
                Assert.That(rows.Length, Is.EqualTo(radius == 2 ? 512 : 32));
                Assert.That(f.Reads.Values.Sum(), Is.EqualTo(72), "one descriptor load for each child");
                Assert.That(f.Reads.Count, Is.EqualTo(72));
                Assert.That(f.Reads.Values, Is.All.EqualTo(1));
                Assert.That(f.Reads.ContainsKey(f.Root.Id.ToString()), Is.False, "root is already resident");
            }
        }

        [Test]
        public void PruningDeferredAndRepeatedEnumerationAndSparseChildrenRetainLoadContracts()
        {
            foreach (var sparse in new[] { false, true })
            {
                using var f = Build(8, sparse, true, "uncached");
                var childCount = f.Model.All().Count() - 1;
                if (sparse) Assert.That(f.Root.Subnodes.Count(n => n == null), Is.GreaterThan(0));
                f.Reset();
                Check(f, new Ray3d(new V3d(-1, 4, 4), V3d.IOO), 0.1, 0, 3, int.MinValue, "spatial prune");
                Check(f, DefaultRay, 0.14, 10, 11, int.MinValue, "parameter prune");
                Assert.That(f.Reads, Is.Empty);
                var query = f.Root.QueryPointsNearRay(DefaultRay, 2, 0, 3);
                using (var iterator = query.GetEnumerator())
                {
                    Assert.That(f.Reads, Is.Empty, "creating query/enumerator is deferred");
                    Assert.That(iterator.MoveNext(), Is.True);
                    Assert.That(f.Reads.Values.Sum(), Is.LessThan(childCount), "do not recurse through the whole tree before the first chunk");
                }
                f.Reset();
                var expected = Ordered(f.Model, DefaultRay, 2, 0, 3, int.MinValue).ToArray();
                for (var repeat = 1; repeat <= 2; repeat++)
                {
                    Assert.That(Rows(query), Is.EqualTo(expected), $"sparse={sparse}, repeat={repeat}");
                    Assert.That(f.Reads.Values.Sum(), Is.EqualTo(childCount * repeat));
                    Assert.That(f.Reads.Values, Is.All.EqualTo(repeat), "resolve once per enumeration, not forever");
                }
            }
        }

        [Test]
        public void CachedAndInMemoryQueriesKeepStableOrderCutoffsAndStandardAttributeAlignment()
        {
            foreach (var mode in new[] { "cached", "memory", "uncached" })
            foreach (var grid in new[] { 2, 8 })
            foreach (var attributes in new[] { false, true })
            {
                using var f = Build(grid, false, attributes, mode);
                foreach (var cutoff in new[] { int.MinValue, f.Root.Cell.Exponent, f.Root.Cell.Exponent - 1, f.Root.Cell.Exponent + 1 })
                foreach (var ray in new[] { DefaultRay, new Ray3d(new V3d(2, 0.5, 0.5), -V3d.IOO), new Ray3d(new V3d(-1, -1, -1), new V3d(1).Normalized) })
                foreach (var radius in new[] { 0.14, 2.0 })
                {
                    f.Reset(); var expected = Check(f, ray, radius, 0, 4, cutoff, $"mode={mode}, grid={grid}, attrs={attributes}, cutoff={cutoff}, direction={ray.Direction}, radius={radius}");
                    if (mode != "uncached") Assert.That(f.Reads, Is.Empty, "resident descriptor controls");
                    if (cutoff == f.Root.Cell.Exponent) Assert.That(f.Reads, Is.Empty, "cutoff stays at the resident root");
                    var pointSet = new PointSet(f.Storage, "forward", f.Root, grid == 2 ? 1 : 8) { Root = new PersistentRef<IPointCloudNode>(f.Root.Id, f.Root) };
                    Assert.That(Rows(pointSet.QueryPointsNearRay(ray, radius, 0, 4, cutoff)), Is.EqualTo(expected), "PointSet forwards bounds and cutoff");
                }
            }
        }

        private static readonly Ray3d DefaultRay = new Ray3d(new V3d(-1, 0.5, 0.5), V3d.IOO);
        private sealed record Row(V3d Position, C4b? Color, V3f? Normal, int? Intensity, byte? Classification, int? Part);
        private sealed class Model
        {
            public Guid Id; public Cell Cell; public Box3d Bounds; public Row[] Rows; public Model[] Children;
            public IEnumerable<Model> All() { yield return this; if (Children != null) foreach (var c in Children) if (c != null) foreach (var n in c.All()) yield return n; }
        }
        private sealed class Fixture : IDisposable
        {
            public Storage Storage; public IPointCloudNode Root; public Model Model; public Dictionary<string, int> Reads;
            public void Reset() => Reads.Clear();
            public void Dispose() => Storage.Dispose();
        }
        private static Fixture Build(int grid, bool sparse, bool attributes, string mode)
        {
            var blobs = new ConcurrentDictionary<string, byte[]>();
            using var setup = new Storage((key, value, encode) => blobs[key] = encode(), key => blobs.TryGetValue(key, out var b) ? b : null,
                (key, offset, size) => blobs[key].Skip((int)offset).Take(size).ToArray(), key => blobs.TryRemove(key, out _), () => { }, () => { }, new LruDictionary<string, object>(1L << 28));
            var points = (from x in Enumerable.Range(0, grid) from y in Enumerable.Range(0, grid) from z in Enumerable.Range(0, grid)
                          select new V3d((x + 0.5) / grid, (y + 0.5) / grid, (z + 0.5) / grid)).Where(p => !sparse || p.Y < 0.5 && p.Z < 0.5).ToArray();
            var indices = Enumerable.Range(0, points.Length).ToArray();
            var chunk = new Chunk(points, attributes ? indices.Select(i => new C4b((byte)i, (byte)(i * 3), (byte)(255 - i), (byte)255)).ToArray() : null,
                attributes ? indices.Select(i => new V3f(1, i % 5, 2)).ToArray() : null, attributes ? indices.Select(i => 1000 + i).ToArray() : null,
                attributes ? indices.Select(i => (byte)(i % 255)).ToArray() : null, attributes ? indices.Select(i => i / 8 + 31).ToArray() : null, null, null);
            var split = grid == 2 ? 1 : 8;
            var root = InMemoryPointSet.Build(chunk, new Cell(0, 0, 0, 0), split).ToPointSetNode(setup, isTemporaryImportNode: true);
            var cloud = new PointSet(setup, "source", root, split).GenerateLod(ImportConfig.Default.WithStorage(setup).WithKey("processed"));
            Model Snapshot(IPointCloudNode n)
            {
                var ps = n.PositionsAbsolute; n.TryGetPartIndices(out var parts);
                var rows = Enumerable.Range(0, ps.Length).Select(i => new Row(ps[i], n.HasColors ? n.Colors.Value[i] : null, n.HasNormals ? n.Normals.Value[i] : null,
                    n.HasIntensities ? n.Intensities.Value[i] : null, n.HasClassifications ? n.Classifications.Value[i] : null, parts == null ? null : parts[i])).ToArray();
                foreach (var row in rows) Assert.That(row.Position.IsNaN || row.Position.IsInfinity, Is.False, "stored oracle position");
                return new Model { Id = n.Id, Cell = n.Cell, Bounds = n.BoundingBoxExactGlobal, Rows = rows, Children = n.IsLeaf ? null : n.Subnodes.Select(c => c == null ? null : Snapshot(c.Value)).ToArray() };
            }
            var model = Snapshot(cloud.Root.Value); var ids = model.All().Select(n => n.Id.ToString()).ToHashSet(); var reads = new Dictionary<string, int>();
            var storage = new Storage((key, value, encode) => throw new InvalidOperationException("Query must not write"), key => {
                if (ids.Contains(key)) reads[key] = reads.TryGetValue(key, out var count) ? count + 1 : 1;
                return blobs.TryGetValue(key, out var b) ? b : null;
            }, (key, offset, size) => blobs[key].Skip((int)offset).Take(size).ToArray(), key => throw new InvalidOperationException(), () => { }, () => { }, mode == "cached" ? new LruDictionary<string, object>(1L << 28) : null);
            try
            {
                var resident = storage.GetPointCloudNode(model.Id);
                if (mode == "cached") foreach (var node in resident.EnumerateNodes()) { _ = node.Positions.Value; _ = node.Colors?.Value; _ = node.Normals?.Value; _ = node.Intensities?.Value; _ = node.Classifications?.Value; _ = node.PartIndices; }
                if (mode == "memory")
                {
                    void Pin(IPointCloudNode node)
                    {
                        if (node.IsLeaf) return;
                        for (var i = 0; i < 8; i++) if (node.Subnodes[i] != null) { var child = node.Subnodes[i].Value; Pin(child); node.Subnodes[i] = new PersistentRef<IPointCloudNode>(child.Id, child); }
                    }
                    Pin(resident);
                }
                reads.Clear(); return new Fixture { Storage = storage, Root = resident, Model = model, Reads = reads };
            }
            catch { storage.Dispose(); throw; }
        }
        private static Row[] Rows(IEnumerable<Chunk> chunks)
        {
            var result = new List<Row>();
            foreach (var chunk in chunks)
            {
                var parts = chunk.TryGetPartIndices();
                foreach (var count in new int?[] { chunk.Colors?.Count, chunk.Normals?.Count, chunk.Intensities?.Count, chunk.Classifications?.Count, parts?.Count }) if (count.HasValue) Assert.That(count.Value, Is.EqualTo(chunk.Count));
                for (var i = 0; i < chunk.Count; i++) result.Add(new Row(chunk.Positions[i], chunk.Colors?[i], chunk.Normals?[i], chunk.Intensities?[i], chunk.Classifications?[i], parts?[i]));
            }
            return result.ToArray();
        }
        private static Row[] Check(Fixture f, Ray3d ray, double radius, double min, double max, int cutoff, string context)
        {
            var expected = Ordered(f.Model, ray, radius, min, max, cutoff).ToArray();
            var selection = Front(f.Model, cutoff).SelectMany(n => n.Rows).Where(r => Selected(r.Position, ray, radius, min, max)).ToArray();
            Assert.That(expected, Is.EquivalentTo(selection), context + " independent stored-front selection");
            var actual = Rows(f.Root.QueryPointsNearRay(ray, radius, min, max, cutoff));
            Assert.That(actual, Is.EqualTo(expected), context + " ordered geometry and attributes");
            return actual;
        }
        private static IEnumerable<Model> Front(Model n, int cutoff)
        {
            if (n.Children == null || n.Cell.Exponent == cutoff) { yield return n; yield break; }
            foreach (var c in n.Children) if (c != null) foreach (var x in Front(c, cutoff)) yield return x;
        }
        private static bool Selected(V3d p, Ray3d ray, double radius, double min, double max)
        {
            var v = p - ray.Origin; var d = ray.Direction; var t = v.X * d.X + v.Y * d.Y + v.Z * d.Z;
            var x = v.X - t * d.X; var y = v.Y - t * d.Y; var z = v.Z - t * d.Z;
            return t >= min && t <= max && x * x + y * y + z * z <= radius * radius;
        }
        private static IEnumerable<Row> Ordered(Model n, Ray3d ray, double radius, double min, double max, int cutoff)
        {
            double entry = double.NegativeInfinity, exit = double.PositiveInfinity;
            for (var axis = 0; axis < 3; axis++)
            {
                var lo = n.Bounds.Min[axis] - radius; var hi = n.Bounds.Max[axis] + radius; var origin = ray.Origin[axis]; var direction = ray.Direction[axis];
                if (direction == 0) { if (origin < lo || origin > hi) yield break; }
                else { var a = (lo - origin) / direction; var b = (hi - origin) / direction; entry = Math.Max(entry, Math.Min(a, b)); exit = Math.Min(exit, Math.Max(a, b)); }
            }
            if (entry > exit || exit < min || entry > max) yield break;
            if (n.Children == null || n.Cell.Exponent == cutoff)
            {
                foreach (var row in n.Rows) if (Selected(row.Position, ray, radius, min, max)) yield return row;
            }
            else foreach (var child in n.Children.Where(c => c != null).OrderBy(c => Vec.Distance(new V3d(c.Bounds.Center), ray.Origin)))
                foreach (var row in Ordered(child, ray, radius, Math.Max(min, entry), Math.Min(max, exit), cutoff)) yield return row;
        }
    }
}
