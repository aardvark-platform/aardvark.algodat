using Aardvark.Base;
using Aardvark.Data;
using Aardvark.Data.Points;
using Aardvark.Geometry.Points;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class PrismFilterSerializationTests
    {
        public enum RegionKind { Empty, Single, Disconnected, Concave, Holed, NestedIsland }
        public enum Domain { All, Empty, FullSelection, SparseSelection }
        private static readonly Range1d Heights = new(-2, 3);

        [Test]
        public void EmptyShapeDeserializesAsEmptyRegion()
        {
            var source = new FilterInsidePrismXY(PolyRegion.Empty, Heights);
            var restored = RoundTrip(source);
            Assert.That(restored.Shape, Is.SameAs(PolyRegion.Empty));
            AssertGeometry(restored, RegionKind.Empty);
        }

        [Test]
        public void DisconnectedContoursAllSurvive()
        {
            var restored = RoundTrip(CreateFilter(RegionKind.Disconnected));
            Assert.That(restored.Shape.Polygons.Count(), Is.EqualTo(2));
            AssertGeometry(restored, RegionKind.Disconnected);
            Assert.That(restored.Contains(new V3d(0.5, 0.5, 0)), Is.True);
            Assert.That(restored.Contains(new V3d(3.5, 0.5, 0)), Is.True);
            Assert.That(restored.Contains(new V3d(2, 0.5, 0)), Is.False);
        }

        [Test]
        public void RegionGeometrySurvivesRepeatedRoundTrips([Values] RegionKind kind, [Values(1, 6)] int repetitions)
        {
            var source = CreateFilter(kind);
            var original = source.Serialize().ToJsonString();
            var filter = source;
            for (int i = 0; i < repetitions; i++)
            {
                filter = RoundTrip(filter);
                AssertGeometry(filter, kind);
                Assert.That(PolyRegion.Xor(source.Shape, filter.Shape).IsEmpty, Is.True);
            }
            Assert.That(source.Serialize().ToJsonString(), Is.EqualTo(original));
        }

        [Test]
        public void SpatialClassificationAndClippingSurvive([Values] RegionKind kind)
        {
            var source = CreateFilter(kind);
            var restored = RoundTrip(source);
            foreach (var box in new[] {
                new Box3d(new V3d(0.25, 0.25, -1), new V3d(0.75, 0.75, 2)),
                new Box3d(new V3d(3.25, 0.25, -1), new V3d(3.75, 0.75, 2)),
                new Box3d(new V3d(3, 3, -1), new V3d(3.5, 3.5, 2)),
                new Box3d(new V3d(-1, -1, -3), new V3d(11, 11, 4)),
                new Box3d(new V3d(0.25, 0.25, -4), new V3d(0.75, 0.75, -3)),
                new Box3d(new V3d(0.25, 0.25, 4), new V3d(0.75, 0.75, 5)) })
            {
                Assert.That(restored.IsFullyInside(box), Is.EqualTo(source.IsFullyInside(box)));
                Assert.That(restored.IsFullyOutside(box), Is.EqualTo(source.IsFullyOutside(box)));
                Assert.That(restored.Clip(box), Is.EqualTo(source.Clip(box)));
            }
        }

        [Test]
        public void ExistingSingleContourJsonRemainsCompatible()
        {
            const string json = "{\"Type\":\"FilterInsidePrismXY\",\"Shape\":[[[0,0],[1,0],[1,1],[0,1]]],\"Range\":[-2,3]}";
            var filter = (FilterInsidePrismXY)Filter.Deserialize(json);
            var polygonConstructor = new FilterInsidePrismXY(new Polygon2d(Rectangle(0, 0, 1, 1)), Heights);
            Assert.That(PolyRegion.Xor(filter.Shape, polygonConstructor.Shape).IsEmpty, Is.True);
            AssertGeometry(filter, RegionKind.Single);
            for (int i = 0; i < 5; i++)
            {
                var encoded = filter.Serialize();
                Assert.That(encoded.AsObject().Select(p => p.Key), Is.EquivalentTo(new[] { "Type", "Shape", "Range" }));
                Assert.That((string)encoded["Type"], Is.EqualTo(FilterInsidePrismXY.Type));
                Assert.That(encoded["Shape"].AsArray().Count, Is.EqualTo(1));
                Assert.That(encoded["Shape"][0].AsArray().All(p => p.AsArray().Count == 2), Is.True);
                Assert.That(encoded["Range"].AsArray().Select(v => (double)v), Is.EqualTo(new[] { -2.0, 3.0 }));
                filter = FilterInsidePrismXY.Deserialize(encoded);
                AssertGeometry(filter, RegionKind.Single);
            }
        }

        [Test]
        public void DeserializeDoesNotModifyJson([Values] RegionKind kind)
        {
            var json = CreateFilter(kind).Serialize();
            var original = json.ToJsonString();
            FilterInsidePrismXY.Deserialize(json);
            FilterInsidePrismXY.Deserialize(json);
            Assert.That(json.ToJsonString(), Is.EqualTo(original));
        }

        [Test]
        public void SelectionSurvivesPersistence(
            [Values(RegionKind.Empty, RegionKind.Single, RegionKind.Disconnected, RegionKind.Concave)] RegionKind kind,
            [Values] Domain domain, [Values] bool stringRoute)
        {
            using var storage = PointCloud.CreateInMemoryStore(cache: default);
            var points = new[] {
                new V3f(0.25, 0.25, 0), new V3f(0.75, 0.75, -2), new V3f(3.25, 0.25, 3),
                new V3f(3.75, 0.75, 0), new V3f(2, 0.5, 0), new V3f(0.5, 2, 0),
                new V3f(2, 2, 0), new V3f(0.5, 0.5, -2.25), new V3f(0.5, 0.5, 3.25) };
            var node = CreateNode(storage, points);
            var source = CreateFilter(kind);
            var filter = stringRoute ? (FilterInsidePrismXY)Filter.Deserialize(source.Serialize().ToJsonString()) : RoundTrip(source);
            HashSet<int> selection = domain switch {
                Domain.All => null,
                Domain.Empty => new HashSet<int>(),
                Domain.FullSelection => Enumerable.Range(0, points.Length).ToHashSet(),
                _ => new HashSet<int> { 0, 2, 4, 6, 8 }
            };
            var snapshot = selection?.ToArray();
            var expected = Enumerable.Range(0, points.Length).Where(i =>
                (selection == null || selection.Contains(i)) && points[i].Z >= -2 && points[i].Z <= 3 &&
                ExpectedContains(kind, (V2d)points[i].XY)).ToArray();
            for (int i = 0; i < 3; i++)
            {
                Assert.That(filter.FilterPoints(node, selection), Is.EquivalentTo(expected));
                if (selection != null) Assert.That(selection, Is.EquivalentTo(snapshot));
            }
            for (int i = 0; i < points.Length; i++)
                Assert.That(filter.Contains((V3d)points[i]), Is.EqualTo(points[i].Z >= -2 && points[i].Z <= 3 && ExpectedContains(kind, (V2d)points[i].XY)));
        }

        [Test]
        public void PersistedNodeKeepsFourIslandPointsAndTheirAttributes([Values] bool cached)
        {
            using var storage = PointCloud.CreateInMemoryStore(cached ? new LruDictionary<string, object>(1 << 20) : null);
            var points = new[] { new V3f(0.25, 0.25, 0), new V3f(0.75, 0.75, 1),
                new V3f(3.25, 0.25, 0), new V3f(3.75, 0.75, 1), new V3f(2, 0.5, 0) };
            var source = CreateNode(storage, points);
            var sourceEncoding = source.Encode();
            var view = (FilteredNode)FilteredNode.Create(source, CreateFilter(RegionKind.Disconnected));
            var id = view.Id;
            AssertSelectedAttributes(view, points.Take(4).ToArray());
            for (int round = 0; round < 5; round++)
            {
                var encoded = view.Encode();
                view = FilteredNode.Decode(storage, encoded);
                Assert.That(view.Id, Is.EqualTo(id));
                Assert.That(view.Node.Id, Is.EqualTo(source.Id));
                AssertSelectedAttributes(view, points.Take(4).ToArray());
                AssertGeometry((FilterInsidePrismXY)view.Filter, RegionKind.Disconnected);
            }
            Assert.That(source.PointCountCell, Is.EqualTo(5));
            Assert.That(source.PositionsAbsolute, Is.EqualTo(points.Select(p => (V3d)p)));
            Assert.That(source.Encode(), Is.EqualTo(sourceEncoding));
        }

        [Test]
        public void ContourCountIsPartOfEquality()
        {
            var multiple = CreateFilter(RegionKind.Disconnected);
            var prefix = new FilterInsidePrismXY(new PolyRegion(multiple.Shape.Polygons.Head), Heights);
            Assert.That(prefix.Shape.Polygons.Head == multiple.Shape.Polygons.Head, Is.True, "The shared prefix is intentional.");
            Assert.That(prefix.Equals(multiple), Is.False);
            Assert.That(multiple.Equals(prefix), Is.False);
            var empty = CreateFilter(RegionKind.Empty);
            Assert.That(empty.Equals(prefix), Is.False);
            Assert.That(prefix.Equals(empty), Is.False);
        }

        [Test]
        public void EqualityMatchesCompleteExactSequence(
            [Values] RegionKind left, [Values] RegionKind right, [Values] bool differentRange)
        {
            var a = CreateFilter(left);
            var b = CreateFilter(right);
            if (differentRange) b = new FilterInsidePrismXY(b.Shape, new Range1d(-2, 4));
            var ap = a.Shape.Polygons.ToArray();
            var bp = b.Shape.Polygons.ToArray();
            var expected = ap.Length == bp.Length && a.ZRange == b.ZRange;
            for (int i = 0; expected && i < ap.Length; i++) expected &= ap[i] == bp[i];
            Assert.That(a.Equals(b), Is.EqualTo(expected));
            Assert.That(b.Equals(a), Is.EqualTo(expected));
            Assert.That(a.Equals(a), Is.True);
        }

        [Test]
        public void EqualityRejectsNullAndOtherFilterTypes()
        {
            var filter = CreateFilter(RegionKind.Single);
            Assert.That(filter.Equals((IFilter)null), Is.False);
            Assert.That(filter.Equals(new FilterInsideBox3d(Box3d.Unit)), Is.False);
        }

        [Test]
        public void EqualityIsStructuralRatherThanGeometric([Values] RegionKind kind)
        {
            var filter = CreateFilter(kind);
            var reversed = new FilterInsidePrismXY(filter.Shape.Reversed, Heights);
            Assert.That(PolyRegion.Xor(filter.Shape, reversed.Shape).IsEmpty, Is.True);
            Assert.That(filter.Equals(reversed), Is.EqualTo(kind == RegionKind.Empty));
        }

        [Test]
        public void EqualityKeepsExactPolygonOperatorSemantics([Values] bool nan)
        {
            var a = CreateFilter(RegionKind.Single);
            var b = CreateFilter(RegionKind.Single);
            var pa = a.Shape.Polygons.Head;
            var pb = b.Shape.Polygons.Head;
            if (nan)
            {
                // Polygon2d.Equals may differ from its exact == operator for NaN.
                // Build valid regions first; only equality is exercised afterwards.
                pa[0] = new V2d(double.NaN, 0);
                pb[0] = new V2d(double.NaN, 0);
            }
            else pb[0] += new V2d(1e-12, 0);
            Assert.That(pa == pb, Is.False);
            Assert.That(a.Equals(b), Is.False);
        }

        [Test]
        public void ZRangeRoundTripsExactly([Values(-5.0, 0.0, 2.0)] double min, [Values(-1.0, 3.0, 5.0)] double max)
        {
            var range = new Range1d(min, max);
            var source = new FilterInsidePrismXY(CreateFilter(RegionKind.Disconnected).Shape, range);
            var result = RoundTrip(source);
            Assert.That(result.ZRange.Min, Is.EqualTo(min));
            Assert.That(result.ZRange.Max, Is.EqualTo(max));
            Assert.That(PolyRegion.Xor(source.Shape, result.Shape).IsEmpty, Is.True);
            var other = new FilterInsidePrismXY(source.Shape, new Range1d(min + 1e-12, max));
            Assert.That(source.Equals(other), Is.False);
        }

        [Test]
        public void EqualityRetainsNaNRangeSemantics()
        {
            var region = CreateFilter(RegionKind.Single).Shape;
            var a = new FilterInsidePrismXY(region, new Range1d(double.NaN, 3));
            var b = new FilterInsidePrismXY(region, new Range1d(double.NaN, 3));
            Assert.That(a.ZRange == b.ZRange, Is.False);
            Assert.That(a.Equals(b), Is.False);
            Assert.That(a.Equals(a), Is.False);
        }

        [Test]
        public void SerializedContoursUseEvenOddParity([Values(0, 1, 2, 3)] int variant, [Values] bool reverse)
        {
            V2d[][] contours = variant switch {
                0 => new[] { Rectangle(0, 0, 2, 1), Rectangle(1, 0, 3, 1) },
                1 => new[] { Rectangle(0, 0, 1, 1), Rectangle(0, 0, 1, 1) },
                2 => new[] { Rectangle(0, 0, 10, 10), Rectangle(2, 2, 8, 8), Rectangle(4, 4, 6, 6) },
                _ => new[] { Array.Empty<V2d>(), Rectangle(0, 0, 1, 1) }
            };
            if (reverse) contours = contours.Reverse().Select(c => c.Reverse().ToArray()).ToArray();
            var json = JsonSerializer.SerializeToNode(new { Type = FilterInsidePrismXY.Type, Shape = contours, Range = new[] { -2.0, 3.0 } });
            var filter = FilterInsidePrismXY.Deserialize(json);
            var triangles = filter.Shape.Triangulate();
            Assert.That(TriangulatedArea(triangles), Is.EqualTo(new[] { 2.0, 0.0, 68.0, 1.0 }[variant]).Within(1e-9));
            for (double x = -0.25; x <= 10.25; x += 0.5)
            for (double y = -0.25; y <= 10.25; y += 0.5)
            {
                var point = new V2d(x, y);
                var parity = contours.Count(c => PolygonContains(c, point)) % 2 == 1;
                Assert.That(TrianglesContain(triangles, point), Is.EqualTo(parity));
            }
        }

        [Test]
        public void ManyContoursSurviveBalancedReconstruction([Values(2, 3, 7, 16, 33, 64)] int count)
        {
            var contours = Enumerable.Range(0, count).Select(i => Rectangle(i * 3, 0, i * 3 + 1, 1)).ToArray();
            var json = JsonSerializer.SerializeToNode(new { Type = FilterInsidePrismXY.Type, Shape = contours, Range = new[] { -2.0, 3.0 } });
            var filter = FilterInsidePrismXY.Deserialize(json);
            Assert.That(filter.Shape.Polygons.Count(), Is.EqualTo(count));
            Assert.That(TriangulatedArea(filter.Shape.Triangulate()), Is.EqualTo(count).Within(1e-9));
            for (int i = 0; i < count; i++)
            {
                Assert.That(filter.Contains(new V3d(i * 3 + 0.5, 0.5, 0)), Is.True);
                Assert.That(filter.Contains(new V3d(i * 3 + 1.5, 0.5, 0)), Is.False);
            }
        }

        private static FilterInsidePrismXY RoundTrip(FilterInsidePrismXY filter)
            => FilterInsidePrismXY.Deserialize(filter.Serialize());

        private static V2d[] Rectangle(double x0, double y0, double x1, double y1)
            => new[] { new V2d(x0, y0), new V2d(x1, y0), new V2d(x1, y1), new V2d(x0, y1) };

        private static V2d[][] Contours(RegionKind kind) => kind switch {
            RegionKind.Empty => Array.Empty<V2d[]>(),
            RegionKind.Single => new[] { Rectangle(0, 0, 1, 1) },
            RegionKind.Disconnected => new[] { Rectangle(0, 0, 1, 1), Rectangle(3, 0, 4, 1) },
            RegionKind.Concave => new[] { new[] { new V2d(0, 0), new V2d(3, 0), new V2d(3, 1), new V2d(1, 1), new V2d(1, 3), new V2d(0, 3) } },
            RegionKind.Holed => new[] { Rectangle(0, 0, 8, 8), Rectangle(2, 2, 6, 6) },
            _ => new[] { Rectangle(0, 0, 10, 10), Rectangle(2, 2, 8, 8), Rectangle(4, 4, 6, 6) }
        };

        private static FilterInsidePrismXY CreateFilter(RegionKind kind)
        {
            var contours = Contours(kind);
            if (contours.Length == 0) return new FilterInsidePrismXY(PolyRegion.Empty, Heights);
            var region = new PolyRegion(new Polygon2d(contours[0]));
            if (kind == RegionKind.Disconnected) region = PolyRegion.Union(region, new PolyRegion(new Polygon2d(contours[1])));
            if (kind == RegionKind.Holed || kind == RegionKind.NestedIsland)
                region = PolyRegion.Difference(region, new PolyRegion(new Polygon2d(contours[1])));
            if (kind == RegionKind.NestedIsland) region = PolyRegion.Union(region, new PolyRegion(new Polygon2d(contours[2])));
            return new FilterInsidePrismXY(region, Heights);
        }

        private static bool ExpectedContains(RegionKind kind, V2d point)
            => Contours(kind).Count(c => PolygonContains(c, point)) % 2 == 1;

        private static bool PolygonContains(V2d[] polygon, V2d point)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                var a = polygon[i]; var b = polygon[j];
                if ((a.Y > point.Y) != (b.Y > point.Y) && point.X < a.X + (point.Y - a.Y) * (b.X - a.X) / (b.Y - a.Y))
                    inside = !inside;
            }
            return inside;
        }

        private static double Cross(V2d a, V2d b) => a.X * b.Y - a.Y * b.X;
        private static double TriangulatedArea(Triangle2d[] triangles)
            => triangles.Sum(t => Math.Abs(Cross(t.P1 - t.P0, t.P2 - t.P0)) * 0.5);

        private static bool TrianglesContain(Triangle2d[] triangles, V2d point) => triangles.Any(t => {
            var a = Cross(t.P1 - t.P0, point - t.P0);
            var b = Cross(t.P2 - t.P1, point - t.P1);
            var c = Cross(t.P0 - t.P2, point - t.P2);
            return (a >= -1e-10 && b >= -1e-10 && c >= -1e-10) || (a <= 1e-10 && b <= 1e-10 && c <= 1e-10);
        });

        private static void AssertGeometry(FilterInsidePrismXY filter, RegionKind kind)
        {
            var contours = Contours(kind);
            Assert.That(filter.ZRange, Is.EqualTo(Heights));
            Assert.That(filter.Shape.Polygons.Count(), Is.EqualTo(contours.Length));
            var triangles = filter.Shape.Triangulate();
            var area = kind switch { RegionKind.Empty => 0, RegionKind.Single => 1, RegionKind.Disconnected => 2,
                RegionKind.Concave => 5, RegionKind.Holed => 48, _ => 68 };
            Assert.That(TriangulatedArea(triangles), Is.EqualTo(area).Within(1e-9));
            if (kind == RegionKind.Empty) Assert.That(filter.Shape.IsEmpty, Is.True);
            else Assert.That(filter.Shape.BoundingBox, Is.EqualTo(new Box2d(contours.SelectMany(c => c))));
            // The pinned PolyRegion.Contains(point) has a separate hole limitation.
            // Test topology through tessellation, not that point-containment path.
            for (double x = -0.25; x <= 10.25; x += 0.5)
            for (double y = -0.25; y <= 10.25; y += 0.5)
            {
                var point = new V2d(x, y);
                Assert.That(TrianglesContain(triangles, point), Is.EqualTo(ExpectedContains(kind, point)));
            }
        }

        private static IPointCloudNode CreateNode(Storage storage, V3f[] global)
        {
            var cell = new Cell(global);
            var center = (V3f)cell.GetCenter();
            var local = global.Select(p => p - center).ToArray();
            Guid Store<T>(T[] values) { var id = Guid.NewGuid(); storage.Add(id, values); return id; }
            var kdId = Guid.NewGuid(); storage.Add(kdId, local.BuildKdTree().Data);
            var data = ImmutableDictionary<Durable.Def, object>.Empty
                .Add(Durable.Octree.NodeId, Guid.NewGuid())
                .Add(Durable.Octree.Cell, cell)
                .Add(Durable.Octree.PositionsLocal3fReference, Store(local))
                .Add(Durable.Octree.PointRkdTreeFDataReference, kdId)
                .Add(Durable.Octree.BoundingBoxExactGlobal, (Box3d)new Box3f(global))
                .Add(Durable.Octree.BoundingBoxExactLocal, new Box3f(local))
                .Add(Durable.Octree.PointCountTreeLeafs, global.LongLength)
                .Add(Durable.Octree.Colors4bReference, Store(Enumerable.Range(0, global.Length).Select(i => new C4b(10 + i, 20 + i, 30 + i, 255)).ToArray()))
                .Add(Durable.Octree.Normals3fReference, Store(Enumerable.Range(0, global.Length).Select(i => new V3f(i, i + 1, i + 2)).ToArray()))
                .Add(Durable.Octree.Intensities1iReference, Store(Enumerable.Range(0, global.Length).Select(i => 100 + i).ToArray()))
                .Add(Durable.Octree.Classifications1bReference, Store(Enumerable.Range(0, global.Length).Select(i => (byte)(50 + i)).ToArray()))
                .Add(Durable.Octree.PerCellPartIndex1i, 42)
                .Add(Durable.Octree.PartIndexRange, new Range1i(42, 42));
            return new PointSetNode(data, storage, writeToStore: true);
        }

        private static void AssertSelectedAttributes(IPointCloudNode node, V3f[] points)
        {
            Assert.That(node.PointCountCell, Is.EqualTo(4));
            Assert.That(node.PointCountTree, Is.EqualTo(4));
            Assert.That(node.PositionsAbsolute, Is.EqualTo(points.Select(p => (V3d)p)));
            Assert.That(node.Colors.Value, Is.EqualTo(Enumerable.Range(0, 4).Select(i => new C4b(10 + i, 20 + i, 30 + i, 255))));
            Assert.That(node.Normals.Value, Is.EqualTo(Enumerable.Range(0, 4).Select(i => new V3f(i, i + 1, i + 2))));
            Assert.That(node.Intensities.Value, Is.EqualTo(new[] { 100, 101, 102, 103 }));
            Assert.That(node.Classifications.Value, Is.EqualTo(new byte[] { 50, 51, 52, 53 }));
            Assert.That(node.TryGetPartIndices(out var parts), Is.True);
            Assert.That(parts, Is.EqualTo(new[] { 42, 42, 42, 42 }));
        }
    }
}
