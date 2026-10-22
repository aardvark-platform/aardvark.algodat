#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Aardvark.Base;
using Aardvark.Geometry;
using NUnit.Framework;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class LineSetClosestPointTests
    {
        private static readonly LineSet SeedOwner = new LineSet(Array.Empty<Cylinder3d>());
        private static readonly double[] Bounds = { 1, Math.BitIncrement(1), 5, Math.BitIncrement(5), 6, 9, Math.BitIncrement(9), 20 };
        private static readonly int[][] Orders = { new[] { 0, 1, 2 }, new[] { 2, 1, 0 }, new[] { 1, 2, 0 }, new[] { 2, 0, 2, 1 } };
        private sealed record Fixture(LineSet Set, V3d Query, V3d[] Points, string Name);

        private static IEnumerable<Fixture> Fixtures(int count = 3)
        {
            for (var orientation = 0; orientation < 3; ++orientation)
                foreach (var offset in new[] { V3d.Zero, new V3d(16, -8, 4) })
                {
                    V3d Map(V3d p) => offset + (orientation == 0 ? p : orientation == 1 ? new V3d(p.Z, p.X, p.Y) : new V3d(p.Y, p.Z, p.X));
                    var lines = Enumerable.Range(0, count).Select(i => new Cylinder3d(Map(new V3d(4 * i, 0, -2)), Map(new V3d(4 * i, 0, 2)), 1)).ToArray();
                    yield return new Fixture(new LineSet(lines), Map(new V3d(-2, 0, 0.5)), Enumerable.Range(0, count).Select(i => Map(new V3d(4 * i - 1, 0, 0.5))).ToArray(), $"count={count}, orientation={orientation}, offset={offset}");
                }
        }

        private static ObjectClosestPoint Seed(double bound) => new ObjectClosestPoint
        {
            Distance = bound,
            DistanceSquared = bound * bound,
            Point = new V3d(-7, -8, -9),
            Coord = new V2d(0.125, 0.75),
            SetObject = new SetObject(SeedOwner, 99),
            ObjectStack = new List<SetObject> { new SetObject(SeedOwner, 37) }
        };

        private static bool Includes(int policy, int index, int count) => policy switch
        {
            0 => index == 1,
            1 => index != 0,
            2 => index == 2,
            3 => false,
            6 => index == count - 1,
            _ => true
        };

        private static void AssertResult(ObjectClosestPoint actual, ObjectClosestPoint before, Fixture f, int expectedIndex, string context)
        {
            Assert.That(actual.Coord, Is.EqualTo(before.Coord), context);
            Assert.That(actual.ObjectStack, Is.SameAs(before.ObjectStack), context);
            Assert.That(actual.ObjectStack.Count, Is.EqualTo(1), context);
            Assert.That(actual.ObjectStack[0].Set, Is.SameAs(SeedOwner), context);
            Assert.That(actual.ObjectStack[0].Index, Is.EqualTo(37), context);
            if (expectedIndex < 0)
            {
                Assert.That(actual.Distance, Is.EqualTo(before.Distance), context);
                Assert.That(actual.DistanceSquared, Is.EqualTo(before.DistanceSquared), context);
                Assert.That(actual.Point, Is.EqualTo(before.Point), context);
                Assert.That(actual.SetObject.Set, Is.SameAs(before.SetObject.Set), context);
                Assert.That(actual.SetObject.Index, Is.EqualTo(before.SetObject.Index), context);
            }
            else
            {
                var distance = 1 + 4 * expectedIndex;
                Assert.That(double.IsFinite(actual.Distance) && double.IsFinite(actual.DistanceSquared), Is.True, context);
                for (var axis = 0; axis < 3; ++axis) Assert.That(double.IsFinite(actual.Point[axis]), Is.True, $"{context}, axis={axis}");
                Assert.That(actual.Distance, Is.EqualTo(distance), context);
                Assert.That(actual.DistanceSquared, Is.EqualTo(distance * distance), context);
                Assert.That(actual.Point, Is.EqualTo(f.Points[expectedIndex]), context);
                Assert.That(actual.SetObject.Set, Is.SameAs(f.Set), context);
                Assert.That(actual.SetObject.Index, Is.EqualTo(expectedIndex), context);
            }
        }

        private static void Check(Fixture f, int[] indices, int first, int count, double bound, int policy, KdIntersectionTree? tree = null)
        {
            var context = $"{f.Name}, indices=[{string.Join(",", indices)}], slice={first}/{count}, bound={bound:R}, policy={policy}, tree={tree != null}";
            var before = Seed(bound);
            var actual = before;
            var calls = new List<int>();
            Func<IIntersectableObjectSet, int, bool>? filter = policy == 5 ? null : (set, index) =>
            {
                Assert.That(set, Is.SameAs(f.Set), context);
                calls.Add(index);
                return Includes(policy, index, f.Set.ObjectCount);
            };
            bool Unused(IIntersectableObjectSet set, int index, int part, ObjectClosestPoint point) => throw new InvalidOperationException("point-result predicate must remain unused");
            var source = f.Set.Lines.ToArray();
            var savedIndices = indices.ToArray();
            var expected = -1;
            var distanceSquared = before.DistanceSquared;
            foreach (var index in indices.Skip(first).Take(count))
                if (Includes(policy, index, f.Set.ObjectCount) && (1 + 4 * index) * (1 + 4 * index) < distanceSquared)
                {
                    expected = index;
                    distanceSquared = (1 + 4 * index) * (1 + 4 * index);
                }
            var found = tree == null
                ? f.Set.ClosestPoint(indices, first, count, f.Query, filter, Unused, ref actual)
                : tree.ClosestPoint(f.Query, filter, Unused, ref actual);
            Assert.That(found, Is.EqualTo(expected >= 0), context);
            AssertResult(actual, before, f, expected, context);
            Assert.That(f.Set.Lines, Is.EqualTo(source), context);
            Assert.That(indices, Is.EqualTo(savedIndices), context);
            if (policy < 4 && tree == null) Assert.That(calls, Is.EqualTo(indices.Skip(first).Take(count)), context);
            else foreach (var index in calls) Assert.That(index, Is.InRange(0, f.Set.ObjectCount - 1), context);
        }

        [Test]
        public void DirectFiltersSelectIncludedCylindersWithOriginalIndicesSlicesAndStrictCutoffs()
        {
            foreach (var f in Fixtures())
                foreach (var order in Orders)
                    foreach (var prefix in new[] { 0, 2 })
                        foreach (var bound in Bounds)
                            foreach (var policy in new[] { 0, 1, 2, 3 })
                            {
                                var indices = Enumerable.Repeat(int.MaxValue, prefix).Concat(order).Concat(new[] { -1 }).ToArray();
                                Check(f, indices, prefix, order.Length, bound, policy);
                            }
        }

        private static KdIntersectionTree Tree(LineSet set, bool optimized, bool flat)
        {
            var flags = KdIntersectionTree.BuildFlags.Raytracing | KdIntersectionTree.BuildFlags.NoMultithreading;
            if (optimized) flags |= KdIntersectionTree.BuildFlags.EmptySpaceOptimization;
            var tree = new KdIntersectionTree(set, flags);
            // Existing box-intersection construction can split these fixtures without changing global object bounds.
            if (set.ObjectCount >= 128) Assert.That(tree.Tree.Leafs().Count(), Is.GreaterThan(1));
            if (flat) tree.Flatten();
            return tree;
        }

        [Test]
        public void TreeFiltersExcludeNearestAndAllWithinCutoffWhilePreservingCallerState()
        {
            foreach (var count in new[] { 3, 128 })
                foreach (var f in Fixtures(count))
                    foreach (var optimized in new[] { false, true })
                        foreach (var flat in new[] { false, true })
                        {
                            var tree = Tree(f.Set, optimized, flat);
                            var indices = Enumerable.Range(0, count).ToArray();
                            foreach (var bound in Bounds)
                                foreach (var policy in new[] { 0, 1, 2, 3 }) Check(f, indices, 0, count, bound, policy, tree);
                            if (count > 3) Check(f, indices, 0, count, 1000, 6, tree);
                        }
        }

        [Test]
        public void NullAndAcceptAllFiltersRetainDirectAndTreeNearestPointContracts()
        {
            foreach (var f in Fixtures())
                foreach (var policy in new[] { 4, 5 })
                    foreach (var bound in Bounds)
                    {
                        foreach (var order in Orders) Check(f, order, 0, order.Length, bound, policy);
                        foreach (var optimized in new[] { false, true })
                            foreach (var flat in new[] { false, true }) Check(f, new[] { 0, 1, 2 }, 0, 3, bound, policy, Tree(f.Set, optimized, flat));
                    }
        }

        [Test]
        public void RejectedInvalidIndicesEmptySlicesAndThrowingPredicatesKeepAccessAndUpdateOrder()
        {
            foreach (var f in Fixtures())
            {
                var source = f.Set.Lines.ToArray();
                var before = Seed(20);
                var actual = before;
                var visited = new List<int>();
                var indices = new[] { -123, int.MaxValue, -1, 2, -456 };
                Assert.DoesNotThrow(() =>
                {
                    var found = f.Set.ClosestPoint(indices, 1, 3, f.Query, (set, index) =>
                    {
                        Assert.That(set, Is.SameAs(f.Set), f.Name);
                        visited.Add(index);
                        return index == 2;
                    }, null, ref actual);
                    Assert.That(found, Is.True, f.Name);
                }, f.Name);
                Assert.That(visited, Is.EqualTo(new[] { int.MaxValue, -1, 2 }), f.Name);
                AssertResult(actual, before, f, 2, f.Name + ": rejected invalid storage");

                foreach (var first in new[] { 0, 2, indices.Length })
                {
                    actual = before;
                    Assert.That(f.Set.ClosestPoint(indices, first, 0, f.Query, (_, _) => throw new Exception("empty object callback"), (_, _, _, _) => throw new Exception("empty point callback"), ref actual), Is.False, f.Name);
                    AssertResult(actual, before, f, -1, f.Name + ": empty slice");
                }
                actual = before;
                Assert.That(f.Set.ClosestPoint(null!, 0, 0, f.Query, null, null, ref actual), Is.False, f.Name);
                AssertResult(actual, before, f, -1, f.Name + ": null empty storage");

                var failure = new InvalidOperationException("object predicate failed");
                actual = before;
                var thrown = Assert.Throws<InvalidOperationException>(() => f.Set.ClosestPoint(new[] { 2, 1, 0 }, 0, 3, f.Query,
                    (_, index) => index == 1 ? throw failure : true, null, ref actual));
                Assert.That(thrown, Is.SameAs(failure), f.Name);
                AssertResult(actual, before, f, 2, f.Name + ": earlier accepted update survives later throw");

                actual = before;
                Assert.Throws<ArgumentOutOfRangeException>(() => f.Set.ClosestPoint(new[] { -1 }, 0, 1, f.Query, (_, _) => true, null, ref actual));
                AssertResult(actual, before, f, -1, f.Name + ": included invalid index still fails");
                Assert.That(f.Set.Lines, Is.EqualTo(source), f.Name);
            }
        }
    }
}
