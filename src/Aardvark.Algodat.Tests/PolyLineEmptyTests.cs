using System;
using System.Collections.Generic;
using System.Linq;
using Aardvark.Base;
using NUnit.Framework;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class PolyLineEmptyTests
    {
        private static PolyLine Empty(bool explicitPositions) => explicitPositions ? new PolyLine { PositionArray = Array.Empty<V3d>() } : new PolyLine();
        private static ObjectClosestPoint Closest(PolyLine line, double bound) => new ObjectClosestPoint {
            Distance = bound, DistanceSquared = bound * bound, Point = new V3d(-7,-8,-9), Coord = new V2d(.125,.75),
            SetObject = new SetObject(line,99), ObjectStack = new List<SetObject> { new SetObject(line,42) }
        };
        private static ObjectRayHit RayHit(PolyLine line) => new ObjectRayHit(10) {
            RayHit = new RayHit3d(10) { Point = new V3d(-7,-8,-9), Coord = new V2d(.125,.75), Part = 17, BackSide = true },
            SetObject = new SetObject(line,99), ObjectStack = new List<SetObject> { new SetObject(line,42) }, Tag = new object()
        };
        private static void Stack(List<SetObject> actual, List<SetObject> before, PolyLine line, string context)
        {
            Assert.That(actual, Is.SameAs(before), context + ": stack reference");
            Assert.That(actual.Count, Is.EqualTo(1), context + ": stack count");
            Assert.That(actual[0].Index, Is.EqualTo(42), context + ": stack index");
            Assert.That(actual[0].Set, Is.SameAs(line), context + ": stack owner");
        }
        private static void Miss(ObjectClosestPoint actual, ObjectClosestPoint before, PolyLine line, string context)
        {
            Assert.That(actual, Is.EqualTo(before), context + ": complete closest result unchanged");
            Stack(actual.ObjectStack,before.ObjectStack,line,context);
        }
        private static void Counts(PolyLine line, int vertices, bool hide, string context)
        {
            var lines = vertices == 0 ? 0 : vertices - 1;
            var visible = hide ? lines : vertices;
            Assert.That(line.VertexCount, Is.EqualTo(vertices), context + ": vertices");
            Assert.That(line.LineCount, Is.EqualTo(lines), context + ": lines");
            Assert.That(line.ObjectCount, Is.EqualTo(lines), context + ": objects");
            Assert.That(((IIntersectableObjectSet)line).ObjectCount, Is.EqualTo(lines), context + ": interface objects");
            Assert.That(line.VisibleVertexCount, Is.EqualTo(visible), context + ": visible vertices");
            Assert.That(line.Vertices.Select(v => v.Index), Is.EqualTo(Enumerable.Range(0,vertices)), context + ": vertex enumeration");
            Assert.That(line.VisibleVertices.Select(v => v.Index), Is.EqualTo(Enumerable.Range(0,visible)), context + ": visible enumeration");
            Assert.That(line.Lines.Select(l => l.Index), Is.EqualTo(Enumerable.Range(0,lines)), context + ": segment enumeration");
            foreach (var vertex in line.Vertices) Assert.That(vertex.PolyLine, Is.SameAs(line), context);
            foreach (var segment in line.Lines) Assert.That(segment.PolyLine, Is.SameAs(line), context);
        }

        [Test]
        public void EmptyCountsAndEnumerationsAgreeForDefaultAndExplicitStorage()
        {
            Assert.Multiple(() => {
                foreach (var explicitPositions in new[] { false, true })
                {
                    var line = Empty(explicitPositions); var positions = line.PositionArray; var attributes = line.VertexAttributes;
                    foreach (var hide in new[] { false, true, false })
                    {
                        line.HideLastVertex = hide; var context = $"explicit={explicitPositions}, hide={hide}";
                        Counts(line,0,hide,context);
                        Assert.That(line.ObjectBoundingBox(), Is.EqualTo(Box3d.Invalid), context + ": existing empty bounds");
                        Assert.That(line.PositionArray, Is.SameAs(positions), context + ": position storage unchanged");
                        Assert.That(line.VertexAttributes, Is.SameAs(attributes), context + ": attributes unchanged");
                    }
                }
            });
        }

        private static void EmptyQueries(PolyLine line, KdIntersectionTree tree, string context)
        {
            foreach (var query in new[] { V3d.Zero, new V3d(5,-2,1) })
            foreach (var bound in new[] { 0.0, 10.0 })
            {
                var label = context + $", query={query}, bound={bound}"; var closest = Closest(line,bound); var before = closest;
                var found = tree == null
                    ? line.ClosestPoint(new[] { int.MinValue, int.MaxValue },1,0,query,(_,_) => throw new Exception("Empty object callback"),(_,_,_,_) => throw new Exception("Empty point callback"),ref closest)
                    : tree.ClosestPoint(query,(_,_) => throw new Exception("Empty object callback"),(_,_,_,_) => throw new Exception("Empty point callback"),ref closest);
                Assert.That(found, Is.False, label); Miss(closest,before,line,label);
                var hit = RayHit(line); var previous = hit; var ray = new FastRay3d(query,V3d.ZAxis);
                var intersects = tree == null
                    ? line.ObjectsIntersectRay(Array.Empty<int>(),0,0,ray,(_,_) => throw new Exception("Empty ray callback"),(_,_,_,_) => throw new Exception("Empty hit callback"),0,10,ref hit)
                    : tree.Intersect(ray,(_,_) => throw new Exception("Empty ray callback"),(_,_,_,_) => throw new Exception("Empty hit callback"),0,10,ref hit);
                Assert.That(intersects, Is.False, label); Assert.That(hit.RayHit, Is.EqualTo(previous.RayHit), label + ": ray payload");
                Assert.That(hit.SetObject.Set, Is.SameAs(line), label); Assert.That(hit.SetObject.Index, Is.EqualTo(99), label);
                Assert.That(hit.Tag, Is.SameAs(previous.Tag), label); Stack(hit.ObjectStack,previous.ObjectStack,line,label);
            }
        }

        [Test]
        public void EmptyDirectAndKdTreeQueriesMissWithoutChangingSeededResults()
        {
            Assert.Multiple(() => {
                foreach (var explicitPositions in new[] { false, true })
                foreach (var hide in new[] { false, true })
                {
                    var line = Empty(explicitPositions); line.HideLastVertex = hide; var positions = line.PositionArray;
                    var context = $"explicit={explicitPositions}, hide={hide}";
                    EmptyQueries(line,null,context + ": direct");
                    foreach (var optimize in new[] { false, true })
                    {
                        var flags = KdIntersectionTree.BuildFlags.NoMultithreading | (optimize ? KdIntersectionTree.BuildFlags.EmptySpaceOptimization : 0);
                        KdIntersectionTree tree = null;
                        Assert.DoesNotThrow(() => tree = new KdIntersectionTree(line,flags), context + $", optimize={optimize}: construction");
                        if (tree == null) continue; // Original implementation fails construction; retain the other matrix rows.
                        Assert.That(tree.Count, Is.Zero, context); Assert.That(tree.Tree, Is.Not.Null, context);
                        EmptyQueries(line,tree,context + $", optimize={optimize}");
                    }
                    Assert.That(line.PositionArray, Is.SameAs(positions), context + ": source storage");
                }
            });
        }

        [Test]
        public void SingletonAndPopulatedLinesKeepHiddenVertexBoundsAndNearestSegments()
        {
            var fixtures = new[] {
                new[] { new V3d(3,4,5) },
                new[] { new V3d(1,-1,0), new V3d(1,1,0) },
                new[] { new V3d(1,-1,0), new V3d(1,1,0), new V3d(5,1,0), new V3d(5,-1,0) }
            };
            foreach (var positions in fixtures)
            foreach (var hide in new[] { false, true })
            {
                var line = new PolyLine { PositionArray = positions, HideLastVertex = hide }; var values = (V3d[])positions.Clone();
                var context = $"vertices={positions.Length}, hide={hide}"; Counts(line,positions.Length,hide,context);
                var bounds = positions.Length == 1 ? new Box3d(positions[0],positions[0]) : new Box3d(new V3d(1,-1,0),new V3d(positions.Length == 2 ? 1 : 5,1,0));
                Assert.That(line.ObjectBoundingBox(), Is.EqualTo(bounds), context + ": all vertices contribute to bounds");
                var query = positions.Length == 4 ? new V3d(6,0,0) : new V3d(2,0,0);
                var point = positions.Length == 4 ? new V3d(5,0,0) : new V3d(1,0,0);
                var indices = Enumerable.Range(0,positions.Length-1).ToArray();
                foreach (var optimize in new[] { false, true })
                {
                    var flags = KdIntersectionTree.BuildFlags.NoMultithreading | (optimize ? KdIntersectionTree.BuildFlags.EmptySpaceOptimization : 0);
                    var tree = new KdIntersectionTree(line,flags); Assert.That(tree.Count, Is.EqualTo(indices.Length), context);
                    foreach (var accelerated in new[] { false, true })
                    foreach (var bound in new[] { 0.0, 1.0, Math.BitIncrement(1.0), 10.0 })
                    {
                        var closest = Closest(line,bound); var before = closest; var label = context + $", optimize={optimize}, accelerated={accelerated}, bound={bound:R}";
                        var found = accelerated ? tree.ClosestPoint(query,ref closest) : line.ClosestPoint(indices,0,indices.Length,query,null,null,ref closest);
                        var expected = positions.Length > 1 && bound > 1;
                        Assert.That(found, Is.EqualTo(expected), label);
                        if (!expected) Miss(closest,before,line,label);
                        else
                        {
                            Assert.That(double.IsFinite(closest.Distance) && double.IsFinite(closest.DistanceSquared) && double.IsFinite(closest.Point.X) && double.IsFinite(closest.Point.Y) && double.IsFinite(closest.Point.Z), Is.True, label);
                            Assert.That(closest.Distance, Is.EqualTo(1), label); Assert.That(closest.DistanceSquared, Is.EqualTo(1), label);
                            Assert.That(closest.Point, Is.EqualTo(point), label); Assert.That(closest.SetObject.Set, Is.SameAs(line), label);
                            Assert.That(closest.SetObject.Index, Is.EqualTo(positions.Length-2), label + ": last segment remains queryable when hidden");
                            Assert.That(closest.Coord, Is.EqualTo(before.Coord), label); Stack(closest.ObjectStack,before.ObjectStack,line,label);
                        }
                        var hit = RayHit(line); var previous = hit;
                        var ray = new FastRay3d(query,V3d.ZAxis);
                        Assert.That(accelerated ? tree.Intersect(ray,0,10,ref hit) : line.ObjectsIntersectRay(indices,0,indices.Length,ray,null,null,0,10,ref hit), Is.False, label + ": existing ray behavior");
                        Assert.That(hit, Is.EqualTo(previous), label + ": ray result unchanged"); Stack(hit.ObjectStack,previous.ObjectStack,line,label);
                    }
                }
                Assert.That(line.PositionArray, Is.SameAs(positions), context); Assert.That(positions, Is.EqualTo(values), context + ": source values");
            }
        }
    }
}
