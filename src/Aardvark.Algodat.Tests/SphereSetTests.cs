using Aardvark.Base;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class SphereSetTests
    {
        #region SphereSet

        private static readonly Sphere3d[] SPHERES = new[]
            {
                new Sphere3d(new V3d(0, 0, 0), 0.5),
                new Sphere3d(new V3d(1, 0, 0), 0.5),
                new Sphere3d(new V3d(1, 1, 0), 0.5),
                new Sphere3d(new V3d(0, 1, 0), 0.5),
            };
        
        [Test]
        public void CanCreateSphereSet()
        {
            var x = new SphereSet(SPHERES);
            ClassicAssert.IsTrue(x.ObjectCount == SPHERES.Length);
        }

        [Test]
        public void CanCreateSphereSetFromEmptyEnumerable()
        {
            var x = new SphereSet(new Sphere3d[0]);
            ClassicAssert.IsTrue(x.ObjectCount == 0);
        }

        [Test]
        public void CannotCreateSphereSetFromNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                var x = new SphereSet(null);
            });
        }

        [Test]
        public void CanGetBoundingBoxOfSingleObject()
        {
            var x = new SphereSet(SPHERES);
            ClassicAssert.IsTrue(
                x.ObjectBoundingBox(0) == new Box3d(new V3d(-0.5, -0.5, -0.5), new V3d(0.5, 0.5, 0.5))
                );
        }

        [Test]
        public void CanGetBoundingBoxOfCompleteSet()
        {
            var x = new SphereSet(SPHERES);
            ClassicAssert.IsTrue(
                x.ObjectBoundingBox() == new Box3d(new V3d(-0.5, -0.5, -0.5), new V3d(1.5, 1.5, 0.5))
                );
        }

        [Test]
        public void CanComputeClosestPoint()
        {
            var x = new SphereSet(SPHERES);
            var cp = ObjectClosestPoint.MaxRange;

            var isCloser = x.ClosestPoint(new[] { 0, 1, 2, 3 }, 0, 4, new V3d(0, -1, 0), null, null, ref cp);
            ClassicAssert.IsTrue(isCloser == true);
            ClassicAssert.IsTrue(cp.DistanceSquared.ApproximateEquals(0.25, 1e-8));
            ClassicAssert.IsTrue(cp.Distance.ApproximateEquals(0.5, 1e-8));
            ClassicAssert.IsTrue(cp.Point.ApproximateEquals(new V3d(0, -0.5, 0), 1e-8));

            isCloser = x.ClosestPoint(new[] { 0, 1, 2, 3 }, 0, 4, new V3d(0, -0.7, 0), null, null, ref cp);
            ClassicAssert.IsTrue(isCloser == true);
            ClassicAssert.IsTrue(cp.DistanceSquared.ApproximateEquals(0.04, 1e-8));
            ClassicAssert.IsTrue(cp.Distance.ApproximateEquals(0.2, 1e-8));
            ClassicAssert.IsTrue(cp.Point.ApproximateEquals(new V3d(0, -0.5, 0), 1e-8));

            isCloser = x.ClosestPoint(new[] { 0, 1, 2, 3 }, 0, 4, new V3d(0, -0.8, 0), null, null, ref cp);
            ClassicAssert.IsTrue(isCloser == false);
        }

        private static SphereSet FilterSet(V3d offset) => new SphereSet(new[] {
            new Sphere3d(offset + new V3d(2, 0, 0), 1),
            new Sphere3d(offset + new V3d(6, 0, 0), 1),
            new Sphere3d(offset + new V3d(9, 0, 0), 1),
            new Sphere3d(offset + new V3d(6, 0, 0), 1)
        });

        private static ObjectClosestPoint Incoming(SphereSet set, double bound) => new ObjectClosestPoint {
            Distance = bound, DistanceSquared = bound * bound, Point = new V3d(-7, -8, -9),
            Coord = new V2d(.125, .75), SetObject = new SetObject(set, 99),
            ObjectStack = new List<SetObject> { new SetObject(set, 42) }
        };

        private static (bool Found, ObjectClosestPoint Closest) FilteredOracle(
            SphereSet set, IEnumerable<int> indices, V3d query, Func<int, bool> include, ObjectClosestPoint closest)
        {
            var found = false;
            foreach (var index in indices)
            {
                if (include != null && !include(index)) continue;
                var sphere = set.Sphere3ds[index];
                var dx = query.X - sphere.Center.X;
                var dy = query.Y - sphere.Center.Y;
                var dz = query.Z - sphere.Center.Z;
                var length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                Assert.That(double.IsFinite(length) && length > 0, Is.True, "oracle fixture must have a finite nonzero radius direction");
                var point = new V3d(sphere.Center.X + sphere.Radius * dx / length,
                    sphere.Center.Y + sphere.Radius * dy / length, sphere.Center.Z + sphere.Radius * dz / length);
                dx = query.X - point.X; dy = query.Y - point.Y; dz = query.Z - point.Z;
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
                Assert.That(!double.IsNaN(value) && !double.IsInfinity(value), Is.True, context + ": finite result");
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
        public void ClosestPointFiltersUseOriginalIndicesAndKeepRejectedResults()
        {
            foreach (var offset in new[] { V3d.Zero, new V3d(8, -4, 2) })
            {
                var set = FilterSet(offset);
                foreach (var slice in new[] { new[] { int.MaxValue, 2, 3, 1, 0, -1 }, new[] { -1, 0, 1, 3, 2, int.MaxValue } })
                foreach (var mask in new[] { 0, 2, 4, 10, 15 })
                foreach (var bound in new[] { 0.0, 1.0, 5.0, 6.0, 1000.0 })
                {
                    var context = $"offset={offset}, slice=[{string.Join(",", slice)}], mask={mask}, bound={bound}";
                    var closest = Incoming(set, bound);
                    var before = closest;
                    var trace = new List<int>();
                    Func<int, bool> include = index => (mask & (1 << index)) != 0;
                    var expected = FilteredOracle(set, slice.Skip(1).Take(4), offset, include, closest);
                    var found = set.ClosestPoint(slice, 1, 4, offset, (objects, index) => {
                        Assert.That(objects, Is.SameAs(set), context + ": callback receiver");
                        trace.Add(index);
                        return include(index);
                    }, (_, _, _, _) => throw new Exception("Point-result filter is unused"), ref closest);
                    Assert.That(found, Is.EqualTo(expected.Found), context);
                    Assert.That(trace, Is.EqualTo(slice.Skip(1).Take(4)), context + ": callback indices/order");
                    AssertState(closest, expected.Closest, context);
                    if (!found) Assert.That(closest, Is.EqualTo(before), context + ": unchanged rejection/miss");
                    if (mask == 2 && bound == 6)
                    {
                        Assert.That(closest.Distance, Is.EqualTo(5), context + ": excluded nearest sphere must not win");
                        Assert.That(closest.Point, Is.EqualTo(offset + new V3d(5, 0, 0)), context + ": distance-five reproduction");
                    }
                }
            }
        }

        [Test]
        public void RejectedSpheresAreSkippedBeforeGeometryIsRead()
        {
            var set = FilterSet(V3d.Zero);
            var slice = new[] { int.MinValue, int.MaxValue, 2, -1, 1, int.MinValue };
            var trace = new List<int>();
            var closest = Incoming(set, 10);
            var expected = FilteredOracle(set, new[] { 1 }, V3d.Zero, null, closest);
            Assert.That(set.ClosestPoint(slice, 1, 4, V3d.Zero, (_, index) => {
                trace.Add(index); return index == 1;
            }, null, ref closest), Is.True);
            Assert.That(trace, Is.EqualTo(new[] { int.MaxValue, 2, -1, 1 }));
            AssertState(closest, expected.Closest, "poisoned rejected indices");
            Assert.Throws<ArgumentOutOfRangeException>(() => set.ClosestPoint(new[] { -1 }, 0, 1,
                V3d.Zero, (_, _) => true, null, ref closest));

            // Existing nearest updates remain visible to the next predicate invocation.
            closest = Incoming(set, 10);
            var distances = new List<double>();
            Assert.That(set.ClosestPoint(new[] { 2, 1, 0 }, 0, 3, V3d.Zero, (_, _) => {
                distances.Add(closest.Distance); return true;
            }, null, ref closest), Is.True);
            Assert.That(distances, Is.EqualTo(new[] { 10.0, 8.0, 5.0 }));
            Assert.That(closest.Distance, Is.EqualTo(1));
        }

        [Test]
        public void NullAndAcceptAllKeepStrictCutoffsSlicesAndUnusedPointFilter()
        {
            var set = FilterSet(V3d.Zero);
            foreach (var indices in new[] { Array.Empty<int>(), new[] { 3, 1 }, new[] { 1, 3 }, new[] { 2, 1, 0 } })
            foreach (var bound in new[] { 0.0, .5, 1.0, Math.BitDecrement(5.0), 5.0, Math.BitIncrement(5.0), 6.0, 1000.0 })
            foreach (var useFilter in new[] { false, true })
            {
                var context = $"indices=[{string.Join(",", indices)}], bound={bound:R}, acceptAll={useFilter}";
                var closest = Incoming(set, bound);
                var before = closest;
                var expected = FilteredOracle(set, indices, V3d.Zero, null, closest);
                Func<IIntersectableObjectSet, int, bool> filter = useFilter ? (_, _) => true : null;
                var slice = new[] { int.MaxValue }.Concat(indices).Concat(new[] { -1 }).ToArray();
                var found = set.ClosestPoint(slice, 1, indices.Length, V3d.Zero, filter,
                    (_, _, _, _) => throw new Exception("Point-result filter is unused"), ref closest);
                Assert.That(found, Is.EqualTo(expected.Found), context);
                AssertState(closest, expected.Closest, context);
                if (!found) Assert.That(closest, Is.EqualTo(before), context + ": unchanged miss");
            }
        }

        [Test]
        public void SubdividedTreesMatchIndependentFilteredSphereSurfaces()
        {
            foreach (var offset in new[] { V3d.Zero, new V3d(8, -4, 2) })
            {
                var set = new SphereSet(Enumerable.Range(0, 128).Select(i => new Sphere3d(
                    offset + new V3d((i % 8) * 4, ((i / 8) % 8) * 4, (i / 64) * 4), .5 + (i % 3) * .125)));
                var tree = new KdIntersectionTree(set, KdIntersectionTree.BuildFlags.Raytracing | KdIntersectionTree.BuildFlags.NoMultithreading);
                Assert.That(tree.Tree.Leafs().Count(), Is.GreaterThan(1), "fixture must genuinely subdivide");
                foreach (var index in new[] { 0, 7, 63, 127 })
                foreach (var mode in Enumerable.Range(0, 7))
                foreach (var bound in new[] { .5, 2.0, 5.0, 1000.0 })
                {
                    var query = set.Sphere3ds[index].Center + new V3d(.3125, -.6875, 1.4375);
                    Func<int, bool> include = mode switch {
                        2 => _ => false, 3 => i => i != index, 4 => i => i == (index + 17) % 128,
                        5 => i => i % 3 == 0, 6 => i => i >= 120, _ => _ => true
                    };
                    var context = $"offset={offset}, querySphere={index}, mode={mode}, bound={bound}";
                    var closest = Incoming(set, bound);
                    var before = closest;
                    var expected = FilteredOracle(set, Enumerable.Range(0, set.ObjectCount), query, include, closest);
                    Func<IIntersectableObjectSet, int, bool> filter = mode == 0 ? null : (objects, i) => {
                        Assert.That(objects, Is.SameAs(set), context + ": callback owner");
                        Assert.That(i, Is.InRange(0, set.ObjectCount - 1), context + ": original index");
                        return include(i);
                    };
                    var found = tree.ClosestPoint(query, filter, (_, _, _, _) => throw new Exception("Point filter is unused"), ref closest);
                    Assert.That(found, Is.EqualTo(expected.Found), context);
                    AssertState(closest, expected.Closest, context);
                    if (!found) Assert.That(closest, Is.EqualTo(before), context + ": unchanged miss");
                }
            }
        }

        #endregion
    }
}
