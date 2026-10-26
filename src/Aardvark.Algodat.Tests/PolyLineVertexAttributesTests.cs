using Aardvark.Base;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class PolyLineVertexAttributesTests
    {
        private static readonly Symbol Custom = (Symbol)"PolyLineCustomVertexAttribute";

        [Test]
        public void FreshAndReplacementDictionariesSynchronizeCountsAndPreserveSharedAttributes()
        {
            foreach (var initialCount in new[] { -1, 0, 1, 2, 4, 7 })
            foreach (var count in new[] { 1, 2, 3, 4, 7 })
            foreach (var hidden in new[] { false, true })
            {
                var line = new PolyLine { HideLastVertex = hidden };
                if (initialCount >= 0) line.PositionArray = Positions(initialCount);
                var points = Positions(count);
                var attributes = Attributes(points);
                // Stale serialized metadata must be replaced, not treated as the new count.
                line[PolyLine.Property.VertexCount] = 101;
                var context = $"initial={initialCount}, replacement={count}, hidden={hidden}";
                AssignAndCheck(line, attributes, points, hidden, context);
                var reference = new PolyLine { PositionArray = points, HideLastVertex = hidden };
                Assert.That(line.VertexCount, Is.EqualTo(reference.VertexCount), context);
                Assert.That(line.LineCount, Is.EqualTo(reference.LineCount), context);
                Assert.That(line.VisibleVertexCount, Is.EqualTo(reference.VisibleVertexCount), context);
                Assert.That(line.ObjectCount, Is.EqualTo(reference.ObjectCount), context);
                Assert.That(line.ObjectBoundingBox(), Is.EqualTo(reference.ObjectBoundingBox()), context);
                Assert.That(line.Vertices.Select(v => v.Index), Is.EqualTo(reference.Vertices.Select(v => v.Index)), context);
                Assert.That(line.VisibleVertices.Select(v => v.Index), Is.EqualTo(reference.VisibleVertices.Select(v => v.Index)), context);
                Assert.That(line.Lines.Select(l => l.Index), Is.EqualTo(reference.Lines.Select(l => l.Index)), context);
            }
        }

        [Test]
        public void GrowthMakesTheNewNearestSegmentVisibleToDirectAndKdTreeQueries()
        {
            foreach (var hidden in new[] { false, true })
            {
                var points = new[] { new V3d(0, 10, 0), new V3d(1, 10, 0), new V3d(1, 0, 0) };
                var line = new PolyLine { PositionArray = points.Take(2).ToArray(), HideLastVertex = hidden };
                CheckNearest(line, new V3d(0, 10, 0), 10, 0, $"before growth, hidden={hidden}");
                AssignAndCheck(line, Attributes(points), points, hidden, $"growth, hidden={hidden}");
                CheckQueries(line, new V3d(1, 0, 0), 1, 1, $"growth, hidden={hidden}");
                var reference = new PolyLine { PositionArray = points, HideLastVertex = hidden };
                CheckQueries(reference, new V3d(1, 0, 0), 1, 1, $"PositionArray growth reference, hidden={hidden}");
            }
        }

        [Test]
        public void ShrinkingReplacementBoundsAndQueriesDoNotReadDiscardedIndices()
        {
            foreach (var hidden in new[] { false, true })
            {
                var line = new PolyLine { PositionArray = Positions(4), HideLastVertex = hidden };
                var points = new[] { new V3d(0, 2, 0), new V3d(4, 2, 0) };
                AssignAndCheck(line, Attributes(points), points, hidden, $"shrink four to two, hidden={hidden}");
                Assert.That(line.ObjectBoundingBox(), Is.EqualTo(new Box3d(new V3d(0, 2, 0), new V3d(4, 2, 0))));
                CheckQueries(line, new V3d(0, 2, 0), 2, 0, $"shrink, hidden={hidden}");
                var reference = new PolyLine { PositionArray = points, HideLastVertex = hidden };
                CheckQueries(reference, new V3d(0, 2, 0), 2, 0, $"PositionArray shrink reference, hidden={hidden}");
            }
        }

        [Test]
        public void EmptyMissingAndInvalidPositionsPreserveRetrievalAndFailureContracts()
        {
            // GetArray's cast and assignment order remain unchanged on failure.
            foreach (var array in new Array[] { new V3f[2], new int[2] })
            {
                var previous = Positions(3);
                var line = new PolyLine { PositionArray = previous };
                var wrong = new SymbolDict<Array> { { PolyLine.Property.Positions, array } };
                Assert.Throws<InvalidCastException>(() => line.VertexAttributes = wrong, $"position type={array.GetType()}");
                Assert.That(line.VertexAttributes, Is.SameAs(wrong));
                Assert.That(line.PositionArray, Is.SameAs(previous));
                Assert.That(line.VertexCount, Is.EqualTo(3));
                Assert.That(line[PolyLine.Property.VertexCount], Is.EqualTo(3));
            }
            var nullLine = new PolyLine { PositionArray = Positions(3) };
            var oldPositions = nullLine.PositionArray;
            Assert.Throws<NullReferenceException>(() => nullLine.VertexAttributes = null);
            Assert.That(nullLine.VertexAttributes, Is.Null);
            Assert.That(nullLine.PositionArray, Is.SameAs(oldPositions));
            Assert.That(nullLine.VertexCount, Is.EqualTo(3));
            Assert.That(nullLine[PolyLine.Property.VertexCount], Is.EqualTo(3));

            foreach (var initialized in new[] { false, true })
            foreach (var kind in new[] { "empty", "missing", "null" })
            foreach (var hidden in new[] { false, true })
            {
                var line = new PolyLine { HideLastVertex = hidden };
                if (initialized) line.PositionArray = Positions(3);
                var points = kind == "empty" ? Array.Empty<V3d>() : null;
                var attributes = Attributes(points, includePositions: kind != "missing");
                var context = $"initialized={initialized}, kind={kind}, hidden={hidden}";
                AssignAndCheck(line, attributes, points, hidden, context);
                var closest = ObjectClosestPoint.MaxRange;
                Assert.That(line.ClosestPoint(Array.Empty<int>(), 0, 0, V3d.Zero, null, null, ref closest), Is.False, context);
                Assert.That(closest.SetObject.Index, Is.EqualTo(-1), context);
                Assert.That(closest.DistanceSquared, Is.EqualTo(double.MaxValue), context);
            }
        }

        private static V3d[] Positions(int count) => Enumerable.Range(0, count).Select(i => new V3d(i * 2, 10 + i, i * 3)).ToArray();

        private static SymbolDict<Array> Attributes(V3d[] points, bool includePositions = true)
        {
            var attributes = new SymbolDict<Array>
            {
                { Custom, new[] { 17, 42, 93 } },
                { PolyLine.Property.VertexCount, new[] { 777 } }
            };
            if (includePositions) attributes.Add(PolyLine.Property.Positions, points);
            return attributes;
        }

        private static void AssignAndCheck(PolyLine line, SymbolDict<Array> attributes, V3d[] points, bool hidden, string context)
        {
            var arrays = attributes.ToDictionary(kv => kv.Key, kv => kv.Value);
            var copies = arrays.ToDictionary(kv => kv.Key, kv => kv.Value?.Clone());
            var lineAttributes = new SymbolDict<Array> { { Custom, new[] { 23 } } };
            var instanceAttributes = new SymbolDict<object> { { Custom, new object() } };
            line.LineAttributes = lineAttributes;
            line.InstanceAttributes = instanceAttributes;
            line.VertexAttributes = attributes;
            var count = points?.Length ?? 0;
            Assert.That(line.VertexAttributes, Is.SameAs(attributes), context);
            Assert.That(line[PolyLine.Property.VertexAttributes], Is.SameAs(attributes), context);
            Assert.That(line.PositionArray, Is.SameAs(points), context);
            Assert.That(line.VertexCount, Is.EqualTo(count), context);
            Assert.That(line[PolyLine.Property.VertexCount], Is.TypeOf<int>(), context);
            Assert.That(line[PolyLine.Property.VertexCount], Is.EqualTo(count), context);
            if (count > 0)
            {
                Assert.That(line.LineCount, Is.EqualTo(count - 1), context);
                Assert.That(line.ObjectCount, Is.EqualTo(count - 1), context);
                Assert.That(line.VisibleVertexCount, Is.EqualTo(hidden ? count - 1 : count), context);
            }
            Assert.That(line.HideLastVertex, Is.EqualTo(hidden), context);
            Assert.That(line.LineAttributes, Is.SameAs(lineAttributes), context);
            Assert.That(line.InstanceAttributes, Is.SameAs(instanceAttributes), context);
            Assert.That(attributes.Keys, Is.EquivalentTo(arrays.Keys), context);
            foreach (var pair in arrays)
            {
                Assert.That(attributes[pair.Key], Is.SameAs(pair.Value), context);
                Assert.That(attributes[pair.Key], Is.EqualTo(copies[pair.Key]), context);
            }
            Assert.That(line.Vertices.Select(v => v.Index), Is.EqualTo(Enumerable.Range(0, count)), context);
            Assert.That(line.VisibleVertices.Select(v => v.Index), Is.EqualTo(Enumerable.Range(0, Math.Max(0, hidden ? count - 1 : count))), context);
            Assert.That(line.Lines.Select(l => l.Index), Is.EqualTo(Enumerable.Range(0, Math.Max(0, count - 1))), context);
            foreach (var vertex in line.Vertices) Assert.That(vertex.PolyLine, Is.SameAs(line), context);
            foreach (var segment in line.Lines) Assert.That(segment.PolyLine, Is.SameAs(line), context);
        }

        private static void CheckQueries(PolyLine line, V3d expectedPoint, double distance, int index, string context)
        {
            Assert.That(line.ObjectBoundingBox(), Is.EqualTo(new Box3d(line.PositionArray)), context);
            CheckNearest(line, expectedPoint, distance, index, context);
            foreach (var flags in new[] { KdIntersectionTree.BuildFlags.Default, KdIntersectionTree.BuildFlags.Raytracing })
            foreach (var flat in new[] { false, true })
            {
                var tree = new KdIntersectionTree(line, flags | KdIntersectionTree.BuildFlags.NoMultithreading);
                if (flat) tree.Flatten();
                var hit = ObjectClosestPoint.MaxRange;
                Assert.That(tree.ClosestPoint(V3d.Zero, ref hit), Is.True, $"{context}, flags={flags}, flat={flat}");
                CheckClosest(hit, line, expectedPoint, distance, index, context);
            }
        }

        private static void CheckNearest(PolyLine line, V3d point, double distance, int index, string context)
        {
            var closest = ObjectClosestPoint.MaxRange;
            var coord = new V2d(0.125, 0.75);
            var stack = new List<SetObject> { new SetObject(line, 19) };
            closest.Coord = coord;
            closest.ObjectStack = stack;
            var candidates = Enumerable.Range(0, line.ObjectCount).ToArray();
            Assert.That(line.ClosestPoint(candidates, 0, candidates.Length, V3d.Zero, null, null, ref closest), Is.True, context);
            CheckClosest(closest, line, point, distance, index, context);
            Assert.That(closest.Coord, Is.EqualTo(coord), context);
            Assert.That(closest.ObjectStack, Is.SameAs(stack), context);
        }

        private static void CheckClosest(ObjectClosestPoint hit, PolyLine line, V3d point, double distance, int index, string context)
        {
            Assert.That(hit.SetObject.Set, Is.SameAs(line), context);
            Assert.That(hit.SetObject.Index, Is.EqualTo(index), context);
            Assert.That(hit.Point.IsNaN || hit.Point.IsInfinity, Is.False, context);
            Assert.That(hit.Point, Is.EqualTo(point), context);
            Assert.That(hit.Distance, Is.EqualTo(distance), context);
            Assert.That(hit.DistanceSquared, Is.EqualTo(distance * distance), context);
        }
    }
}
