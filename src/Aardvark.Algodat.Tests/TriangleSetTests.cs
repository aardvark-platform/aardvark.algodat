using System;
using System.Collections.Generic;
using System.Linq;
using Aardvark.Base;
using NUnit.Framework;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class TriangleSetTests
    {
        private static TriangleSet CreateSet(int columns, V3d offset) => new TriangleSet(
            from column in Enumerable.Range(0, columns)
            from z in new[] { 1.0, 3.0, 5.0 }
            let p = offset + new V3d(4 * column, 0, z)
            select new Triangle3d(p, p + new V3d(2, 0, 0), p + new V3d(0, 2, 0)));

        private static ObjectClosestPoint Incoming(TriangleSet set, double bound) => new ObjectClosestPoint {
            Distance = bound, DistanceSquared = bound * bound, Point = new V3d(-7, -8, -9),
            Coord = new V2d(.125, .75), SetObject = new SetObject(set, 99),
            ObjectStack = new List<SetObject> { new SetObject(set, 42) }
        };

        private static (bool Found, ObjectClosestPoint Closest) Oracle(
            TriangleSet set, IEnumerable<int> indices, V3d query, Func<int, bool> include, ObjectClosestPoint closest)
        {
            var found = false;
            foreach (var index in indices)
            {
                if (include != null && !include(index)) continue;
                // Fixtures have horizontal right triangles with legs of length two.
                // Project onto their XY simplex independently of the production triangle helper.
                var a = set.Position3dList[3 * index];
                var x = Math.Max(0, query.X - a.X);
                var y = Math.Max(0, query.Y - a.Y);
                if (x + y > 2)
                {
                    x = Math.Clamp((query.X - a.X - (query.Y - a.Y) + 2) * .5, 0, 2);
                    y = 2 - x;
                }
                var point = new V3d(a.X + x, a.Y + y, a.Z);
                var dx = query.X - point.X; var dy = query.Y - point.Y; var dz = query.Z - point.Z;
                var squared = dx * dx + dy * dy + dz * dz;
                Assert.That(double.IsFinite(squared), Is.True, "finite oracle distance");
                if (squared >= closest.DistanceSquared) continue;
                closest.DistanceSquared = squared;
                closest.Distance = Math.Sqrt(squared);
                closest.Point = point;
                closest.SetObject = new SetObject(set, index);
                found = true;
            }
            return (found, closest);
        }

        private static void AssertState(ObjectClosestPoint actual, ObjectClosestPoint expected, string context)
        {
            foreach (var value in new[] { actual.Distance, actual.DistanceSquared, actual.Point.X, actual.Point.Y, actual.Point.Z,
                expected.Distance, expected.DistanceSquared, expected.Point.X, expected.Point.Y, expected.Point.Z })
                Assert.That(double.IsFinite(value), Is.True, context + ": finite result");
            Assert.That(actual.Distance, Is.EqualTo(expected.Distance).Within(1e-11), context + ": distance");
            Assert.That(actual.DistanceSquared, Is.EqualTo(expected.DistanceSquared).Within(1e-9), context + ": squared distance");
            Assert.That(actual.Point.ApproximateEquals(expected.Point, 1e-11), Is.True, context + ": point");
            Assert.That(actual.SetObject.Set, Is.SameAs(expected.SetObject.Set), context + ": owner");
            Assert.That(actual.SetObject.Index, Is.EqualTo(expected.SetObject.Index), context + ": original index");
            Assert.That(actual.Coord, Is.EqualTo(expected.Coord), context + ": coordinate preserved");
            Assert.That(actual.ObjectStack, Is.SameAs(expected.ObjectStack), context + ": stack identity");
            Assert.That(actual.ObjectStack.Count, Is.EqualTo(1), context + ": stack count");
            Assert.That(actual.ObjectStack[0].Set, Is.SameAs(expected.SetObject.Set), context + ": stack owner");
            Assert.That(actual.ObjectStack[0].Index, Is.EqualTo(42), context + ": stack value");
        }

        [Test]
        public void InclusionPredicatesSelectOriginalIndicesAndRejectAllWithoutChangingResults()
        {
            Assert.Multiple(() => {
                foreach (var offset in new[] { V3d.Zero, new V3d(8, -4, 2) })
                {
                    var set = CreateSet(1, offset);
                    var snapshot = set.Position3dList.ToArray();
                    var query = offset + new V3d(.25, .25, 0);
                    foreach (var slice in new[] { new[] { int.MaxValue, 2, 0, 1, -1 }, new[] { -1, 1, 0, 2, int.MaxValue } })
                    foreach (var mask in new[] { 4, 0, 2, 5, 7 })
                    foreach (var bound in new[] { 6.0, 0.0, 1.0, 3.0, 5.0, Math.BitIncrement(5.0), 1000.0 })
                    {
                        var context = $"offset={offset}, slice=[{string.Join(",", slice)}], mask={mask}, bound={bound:R}";
                        var closest = Incoming(set, bound);
                        var before = closest;
                        var trace = new List<int>();
                        Func<int, bool> include = index => (mask & (1 << index)) != 0;
                        var expected = Oracle(set, slice.Skip(1).Take(3), query, include, closest);
                        var found = set.ClosestPoint(slice, 1, 3, query, (objects, index) => {
                            Assert.That(objects, Is.SameAs(set), context + ": callback receiver");
                            trace.Add(index); return include(index);
                        }, (_, _, _, _) => throw new Exception("Point-result filter is unused"), ref closest);
                        Assert.That(found, Is.EqualTo(expected.Found), context + ": found");
                        AssertState(closest, expected.Closest, context);
                        Assert.That(trace, Is.EqualTo(slice.Skip(1).Take(3)), context + ": callback indices/order");
                        if (!found) Assert.That(closest, Is.EqualTo(before), context + ": unchanged miss");
                        if (mask == 4 && bound == 6)
                        {
                            Assert.That(closest.Distance, Is.EqualTo(5), context + ": select last of distances 1,3,5");
                            Assert.That(closest.Point, Is.EqualTo(query + new V3d(0, 0, 5)), context + ": last surface point");
                        }
                    }
                    Assert.That(set.Position3dList, Is.EqualTo(snapshot), "positions remain unchanged");
                }
            });
        }

        [Test]
        public void RejectedTrianglesAreSkippedBeforePositionsAndNearestUpdatesRemainIncremental()
        {
            var set = CreateSet(1, V3d.Zero);
            var query = new V3d(.25, .25, 0);
            var slice = new[] { int.MinValue, int.MaxValue, 0, -1, 2, int.MinValue };
            var closest = Incoming(set, 10);
            var expected = Oracle(set, new[] { 2 }, query, null, closest);
            var trace = new List<int>();
            Assert.That(set.ClosestPoint(slice, 1, 4, query, (_, index) => {
                trace.Add(index); return index == 2;
            }, null, ref closest), Is.True);
            Assert.That(trace, Is.EqualTo(new[] { int.MaxValue, 0, -1, 2 }));
            AssertState(closest, expected.Closest, "rejected poisoned indices");
            Assert.Throws<ArgumentOutOfRangeException>(() => set.ClosestPoint(new[] { -1 }, 0, 1,
                query, (_, _) => true, null, ref closest));

            closest = Incoming(set, 10);
            var distances = new List<double>();
            Assert.That(set.ClosestPoint(new[] { 2, 1, 0 }, 0, 3, query, (_, _) => {
                distances.Add(closest.Distance); return true;
            }, null, ref closest), Is.True);
            Assert.That(distances, Is.EqualTo(new[] { 10.0, 5.0, 3.0 }));
            Assert.That(closest.Distance, Is.EqualTo(1));
        }

        [Test]
        public void NullAndAcceptAllRetainSlicesStrictCutoffsAndUnusedPointFilter()
        {
            var set = CreateSet(1, V3d.Zero);
            foreach (var indices in new[] { Array.Empty<int>(), new[] { 0, 1, 2 }, new[] { 2, 1, 0 }, new[] { 2, 2 } })
            foreach (var query in new[] { new V3d(.25, .25, 0), new V3d(1.5, 1.5, 0), new V3d(-1, -1, 0) })
            foreach (var bound in new[] { 0.0, .5, 1.0, Math.BitDecrement(5.0), 5.0, Math.BitIncrement(5.0), 6.0, 1000.0 })
            foreach (var useFilter in new[] { false, true })
            {
                var context = $"indices=[{string.Join(",", indices)}], query={query}, bound={bound:R}, all={useFilter}";
                var closest = Incoming(set, bound);
                var before = closest;
                var expected = Oracle(set, indices, query, null, closest);
                Func<IIntersectableObjectSet, int, bool> filter = useFilter ? (_, _) => {
                    Assert.That(indices, Is.Not.Empty, context + ": empty slice must not invoke predicate"); return true;
                } : null;
                var slice = new[] { int.MaxValue }.Concat(indices).Concat(new[] { -1 }).ToArray();
                var found = set.ClosestPoint(slice, 1, indices.Length, query, filter,
                    (_, _, _, _) => throw new Exception("Point-result filter is unused"), ref closest);
                Assert.That(found, Is.EqualTo(expected.Found), context);
                AssertState(closest, expected.Closest, context);
                if (!found) Assert.That(closest, Is.EqualTo(before), context + ": unchanged miss");
            }
        }

        [Test]
        public void SubdividedTreesMatchIndependentFilteredTriangleSurfaces()
        {
            Assert.Multiple(() => {
                foreach (var offset in new[] { V3d.Zero, new V3d(8, -4, 2) })
                {
                    var set = CreateSet(64, offset);
                    var tree = new KdIntersectionTree(set, KdIntersectionTree.BuildFlags.Raytracing | KdIntersectionTree.BuildFlags.NoMultithreading);
                    Assert.That(tree.Tree.Leafs().Count(), Is.GreaterThan(1), "fixture must genuinely subdivide");
                    foreach (var column in new[] { 0, 7, 31, 63 })
                    foreach (var mode in Enumerable.Range(0, 6))
                    foreach (var bound in new[] { .5, 1.0, 3.0, 5.0, 6.0, 1000.0 })
                    {
                        var query = offset + new V3d(4 * column + .25, .25, 0);
                        Func<int, bool> include = mode switch {
                            2 => _ => false, 3 => i => i == 3 * column + 2,
                            4 => i => i % 3 == 2, 5 => i => i != 3 * column, _ => _ => true
                        };
                        var context = $"offset={offset}, column={column}, mode={mode}, bound={bound}";
                        var closest = Incoming(set, bound);
                        var before = closest;
                        var expected = Oracle(set, Enumerable.Range(0, set.ObjectCount), query, include, closest);
                        Func<IIntersectableObjectSet, int, bool> filter = mode == 0 ? null : (objects, index) => {
                            Assert.That(objects, Is.SameAs(set), context + ": callback receiver");
                            Assert.That(index, Is.InRange(0, set.ObjectCount - 1), context + ": original index");
                            return include(index);
                        };
                        var found = tree.ClosestPoint(query, filter, (_, _, _, _) => throw new Exception("Point filter is unused"), ref closest);
                        Assert.That(found, Is.EqualTo(expected.Found), context + ": found");
                        AssertState(closest, expected.Closest, context);
                        if (!found) Assert.That(closest, Is.EqualTo(before), context + ": unchanged miss");
                        if (mode == 3 && bound == 6)
                            Assert.That(closest.Distance, Is.EqualTo(5), context + ": select last of distances 1,3,5");
                    }
                }
            });
        }
    }
}
