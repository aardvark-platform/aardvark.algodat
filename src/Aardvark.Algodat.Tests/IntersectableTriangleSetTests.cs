using System;
using System.Linq;
using Aardvark.Base;
using NUnit.Framework;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class IntersectableTriangleSetTests
    {
        private static readonly V3d Query = new V3d(0.25, 0.25, 0);

        private static IntersectableTriangleSet CreateSet(bool indexed, params float[] heights)
        {
            var positions = heights.SelectMany(z => new[]
            {
                new V3f(0, 0, z), new V3f(2, 0, z), new V3f(0, 2, z)
            }).ToArray();
            if (!indexed) return new IntersectableTriangleSet(positions);

            // Non-identity indices represent the same triangles as the unindexed set.
            Array.Reverse(positions);
            var indices = Enumerable.Range(0, positions.Length).Reverse().ToArray();
            return new IntersectableTriangleSet(indices, positions);
        }

        private static void AssertClosest(
            ObjectClosestPoint closest, IntersectableTriangleSet set,
            int index, double height, double distance)
        {
            Assert.Multiple(() =>
            {
                Assert.That(closest.SetObject.Set, Is.SameAs(set));
                Assert.That(closest.SetObject.Index, Is.EqualTo(index));
                Assert.That(closest.Point.ApproximateEquals(new V3d(0.25, 0.25, height), 1e-12), Is.True);
                Assert.That(closest.Distance, Is.EqualTo(distance).Within(1e-12));
                Assert.That(closest.DistanceSquared, Is.EqualTo(distance * distance).Within(1e-12));
            });
        }

        [Test]
        public void KdTreeClosestPointAcceptsNullFilters([Values] bool indexed)
        {
            var set = CreateSet(indexed, 2);
            var tree = new KdIntersectionTree(set);
            var closest = ObjectClosestPoint.MaxRange;

            Assert.That(tree.ClosestPoint(Query, ref closest), Is.True);
            AssertClosest(closest, set, 0, 2, 2);
        }

        [Test]
        public void ClosestPointIsIndependentOfCandidateOrder(
            [Values] bool indexed, [Values] bool nearestLast)
        {
            var set = CreateSet(indexed, 2, 5);
            var candidates = nearestLast ? new[] { 1, 0 } : new[] { 0, 1 };
            var closest = ObjectClosestPoint.MaxRange;

            // A non-null filter isolates distance tracking from null-filter handling.
            Assert.That(set.ClosestPoint(candidates, 0, candidates.Length, Query,
                (_, _) => true, null, ref closest), Is.True);
            AssertClosest(closest, set, 0, 2, 2);
        }

        [TestCase(1)]
        [TestCase(-1)]
        public void ClosestPointRespectsObjectFilter(int acceptedIndex)
        {
            var set = CreateSet(true, 2, 5);
            var closest = ObjectClosestPoint.MaxRange;
            var before = closest;

            var found = set.ClosestPoint(new[] { 0, 1 }, 0, 2, Query,
                (objects, index) => ReferenceEquals(objects, set) && index == acceptedIndex,
                null, ref closest);

            Assert.That(found, Is.EqualTo(acceptedIndex >= 0));
            if (found) AssertClosest(closest, set, 1, 5, 5);
            else Assert.That(closest, Is.EqualTo(before));
        }

        [TestCase(1.0, false)]
        [TestCase(2.0, false)]
        [TestCase(3.0, true)]
        public void ClosestPointOnlyUpdatesForAStrictlyCloserCandidate(double bound, bool expected)
        {
            var set = CreateSet(true, 2, 5);
            var closest = ObjectClosestPoint.MaxRange;
            closest.Distance = bound;
            closest.DistanceSquared = bound * bound;
            closest.Point = new V3d(0.25, 0.25, bound);
            var before = closest;

            var found = set.ClosestPoint(new[] { 0, 1 }, 0, 2, Query,
                (_, _) => true, null, ref closest);

            Assert.That(found, Is.EqualTo(expected));
            if (found) AssertClosest(closest, set, 0, 2, 2);
            else Assert.That(closest, Is.EqualTo(before));
        }

        [Test]
        public void ClosestPointUsesOnlyTheRequestedCandidateRange()
        {
            var set = CreateSet(true, 2, 5);
            var closest = ObjectClosestPoint.MaxRange;

            Assert.That(set.ClosestPoint(new[] { -1, 1, -1 }, 1, 1, Query,
                null, null, ref closest), Is.True);
            AssertClosest(closest, set, 1, 5, 5);
        }

        [Test]
        public void ClosestPointLeavesTheResultUnchangedForAnEmptyRange()
        {
            var set = CreateSet(true, 2);
            var closest = ObjectClosestPoint.MaxRange;
            var before = closest;

            Assert.That(set.ClosestPoint(Array.Empty<int>(), 0, 0, Query,
                null, null, ref closest), Is.False);
            Assert.That(closest, Is.EqualTo(before));
        }

        [Test]
        public void KdTreeClosestPointTraversesSeparatedTriangles([Values] bool indexed)
        {
            var set = CreateSet(indexed, Enumerable.Range(0, 32).Select(i => 4.0f * i).ToArray());
            var tree = new KdIntersectionTree(set, KdIntersectionTree.BuildFlags.Raytracing);

            foreach (var index in new[] { 0, 7, 15, 31 })
            {
                var query = new V3d(0.25, 0.25, 4 * index + 1);
                var closest = ObjectClosestPoint.MaxRange;
                Assert.That(tree.ClosestPoint(query, ref closest), Is.True);
                AssertClosest(closest, set, index, 4 * index, 1);

                var before = closest;
                Assert.That(tree.ClosestPoint(query, ref closest), Is.False);
                Assert.That(closest, Is.EqualTo(before));
            }
        }
    }
}
