/*
    Copyright (C) 2006-2023. Aardvark Platform Team. http://github.com/aardvark-platform.
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.
    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using Aardvark.Base;
using Aardvark.Geometry.Points;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class KdTreeTests
    {
        [Test]
        public void CreatingKdTreeDoesNotChangeOrderOfPoints()
        {
            var r = new Random();
            var ps = new V3f[1000].SetByIndex(_ => new V3f(r.NextDouble(), r.NextDouble(), r.NextDouble()));
            var copy = ps.Copy();

            var kd = ps.BuildKdTree();
            ClassicAssert.IsTrue(kd != null);
            ClassicAssert.IsTrue(ps.Length == copy.Length);
            for (var i = 0; i < ps.Length; i++)
            {
                ClassicAssert.IsTrue(ps[i] == copy[i]);
            }
        }

        [Test]
        public void PointKdTreeF_FilterMatchesBruteForce()
        {
            var points = CreateFilterPointsF();
            var tree = points.CreateKdTreeDist2(1e-6f);

            AssertFilteredQueries(
                points,
                new V3f(0.35f, -0.8f, 1.1f),
                new V3f(-7.2f, 2.4f, -1.3f),
                12.0,
                float.MaxValue,
                float.MinValue,
                1e-5,
                (radius, count) => CreateQuery(tree, (float)radius, count),
                (point, radius, count) => ToHits(tree.GetClosest(point, (float)radius, count)),
                (a, b) => (a - b).Length
                );
        }

        [Test]
        public void PointKdTreeD_FilterMatchesBruteForce()
        {
            var points = CreateFilterPointsD();
            var tree = points.CreateKdTreeDist2(1e-12);

            AssertFilteredQueries(
                points,
                new V3d(0.35, -0.8, 1.1),
                new V3d(-7.2, 2.4, -1.3),
                12.0,
                double.MaxValue,
                double.MinValue,
                1e-12,
                (radius, count) => CreateQuery(tree, radius, count),
                (point, radius, count) => ToHits(tree.GetClosest(point, radius, count)),
                (a, b) => (a - b).Length
                );
        }

        [Test]
        public void PointRkdTreeF_FilterMatchesBruteForce()
        {
            var points = CreateFilterPointsF();
            var tree = points.CreateRkdTreeDist2(1e-6f);

            AssertFilteredQueries(
                points,
                new V3f(0.35f, -0.8f, 1.1f),
                new V3f(-7.2f, 2.4f, -1.3f),
                12.0,
                float.MaxValue,
                float.MinValue,
                1e-5,
                (radius, count) => CreateQuery(tree, (float)radius, count),
                (point, radius, count) => ToHits(tree.GetClosest(point, (float)radius, count)),
                (a, b) => (a - b).Length
                );
        }

        [Test]
        public void PointRkdTreeD_FilterMatchesBruteForce()
        {
            var points = CreateFilterPointsD();
            var tree = points.CreateRkdTreeDist2(1e-12);

            AssertFilteredQueries(
                points,
                new V3d(0.35, -0.8, 1.1),
                new V3d(-7.2, 2.4, -1.3),
                12.0,
                double.MaxValue,
                double.MinValue,
                1e-12,
                (radius, count) => CreateQuery(tree, radius, count),
                (point, radius, count) => ToHits(tree.GetClosest(point, radius, count)),
                (a, b) => (a - b).Length
                );
        }

        [Test]
        public void PointRkdTreeSelector_FilterAppliesDuringBulkCollection()
        {
            var points = CreateFilterPointsF();
            var tree = points.CreateRkdTreeSelectorDist2(1e-6f);
            var query = tree.CreateClosestToPointQuery(float.MaxValue, 0);
            Func<long, bool> filter = index => index % 4 == 1;
            query.Filter = filter;

            var actual = ToHits(tree.GetClosest(query, new V3f(0.35f, -0.8f, 1.1f)));
            var expected = Enumerable.Range(0, points.Length)
                .Where(i => filter(i))
                .Select(i => (long)i)
                .ToArray();

            Assert.That(actual.Select(x => x.Index), Is.EquivalentTo(expected));
            Assert.That(actual.Select(x => x.Index).Distinct().Count(), Is.EqualTo(actual.Length));
            Assert.That(actual.Any(x => x.Distance == float.MinValue), Is.True,
                "The test must exercise the radius-tree whole-subtree collector.");
        }

        private static void AssertFilteredQueries<TPoint>(
            IReadOnlyList<TPoint> points,
            TPoint firstCenter,
            TPoint secondCenter,
            double finiteRadius,
            double unboundedRadius,
            double bulkDistanceSentinel,
            double distanceTolerance,
            Func<double, int, ReusablePointQuery<TPoint>> createQuery,
            Func<TPoint, double, int, QueryHit[]> directQuery,
            Func<TPoint, TPoint, double> distance
            )
        {
            var nearest = Enumerable.Range(0, points.Count)
                .OrderBy(i => distance(firstCenter, points[i]))
                .Take(4)
                .ToHashSet();
            Func<long, bool> filter = index => !nearest.Contains((int)index) && index % 5 != 2;

            var reusable = createQuery(finiteRadius, 0);
            reusable.Filter = filter;
            var first = reusable.GetClosest(firstCenter);
            var expectedFirst = BruteForce(points, firstCenter, finiteRadius, 0, filter, distance);
            AssertMatchesBruteForce(points, firstCenter, first, expectedFirst,
                bulkDistanceSentinel, distanceTolerance, distance);
            Assert.That(first.Any(x => nearest.Contains((int)x.Index)), Is.False,
                "The filter must exclude the geometrically nearest points.");

            var accumulated = reusable.GetClosest(secondCenter);
            var expectedSecond = BruteForce(points, secondCenter, finiteRadius, 0, filter, distance);
            Assert.That(accumulated.Select(x => x.Index),
                Is.EquivalentTo(expectedFirst.Concat(expectedSecond).Select(x => x.Index)));

            reusable.Clear();
            Assert.That(reusable.Count, Is.Zero);
            Assert.That(reusable.Filter, Is.SameAs(filter));
            var second = reusable.GetClosest(secondCenter);
            AssertMatchesBruteForce(points, secondCenter, second, expectedSecond,
                bulkDistanceSentinel, distanceTolerance, distance);

            var fixedCount = createQuery(unboundedRadius, 7);
            fixedCount.Filter = filter;
            var fixedResult = fixedCount.GetClosest(firstCenter);
            var expectedFixed = BruteForce(points, firstCenter, unboundedRadius, 7, filter, distance);
            AssertMatchesBruteForce(points, firstCenter, fixedResult, expectedFixed,
                bulkDistanceSentinel, distanceTolerance, distance);

            var allAccepted = createQuery(unboundedRadius, 0);
            allAccepted.Filter = filter;
            var allAcceptedResult = allAccepted.GetClosest(firstCenter);
            var expectedAllAccepted = BruteForce(points, firstCenter, unboundedRadius, 0, filter, distance);
            AssertMatchesBruteForce(points, firstCenter, allAcceptedResult, expectedAllAccepted,
                bulkDistanceSentinel, distanceTolerance, distance);

            var rejectAll = createQuery(unboundedRadius, 0);
            rejectAll.Filter = _ => false;
            Assert.That(rejectAll.GetClosest(firstCenter), Is.Empty);

            AssertNullFilterParity(firstCenter, finiteRadius, 0, createQuery, directQuery);
            AssertNullFilterParity(secondCenter, unboundedRadius, 7, createQuery, directQuery);
        }

        private static QueryHit[] BruteForce<TPoint>(
            IReadOnlyList<TPoint> points,
            TPoint center,
            double maxDistance,
            int maxCount,
            Func<long, bool> filter,
            Func<TPoint, TPoint, double> distance
            )
        {
            var hits = Enumerable.Range(0, points.Count)
                .Where(i => filter(i))
                .Select(i => new QueryHit(i, distance(center, points[i])))
                .Where(x => x.Distance <= maxDistance)
                .OrderBy(x => x.Distance)
                .ThenBy(x => x.Index);

            return (maxCount > 0 ? hits.Take(maxCount) : hits).ToArray();
        }

        private static void AssertMatchesBruteForce<TPoint>(
            IReadOnlyList<TPoint> points,
            TPoint center,
            IReadOnlyCollection<QueryHit> actual,
            IReadOnlyCollection<QueryHit> expected,
            double bulkDistanceSentinel,
            double distanceTolerance,
            Func<TPoint, TPoint, double> distance
            )
        {
            Assert.That(actual.Select(x => x.Index), Is.EquivalentTo(expected.Select(x => x.Index)));
            Assert.That(actual.Select(x => x.Index).Distinct().Count(), Is.EqualTo(actual.Count));

            foreach (var hit in actual)
            {
                Assert.That(hit.Index, Is.InRange(0L, points.Count - 1L));
                if (hit.Distance != bulkDistanceSentinel)
                {
                    Assert.That(hit.Distance,
                        Is.EqualTo(distance(center, points[(int)hit.Index])).Within(distanceTolerance));
                }
            }
        }

        private static void AssertNullFilterParity<TPoint>(
            TPoint center,
            double maxDistance,
            int maxCount,
            Func<double, int, ReusablePointQuery<TPoint>> createQuery,
            Func<TPoint, double, int, QueryHit[]> directQuery
            )
        {
            var query = createQuery(maxDistance, maxCount);
            query.Filter = null;
            var reusable = query.GetClosest(center);
            var direct = directQuery(center, maxDistance, maxCount);

            Assert.That(reusable.Length, Is.EqualTo(direct.Length));
            for (var i = 0; i < reusable.Length; i++)
            {
                Assert.That(reusable[i].Index, Is.EqualTo(direct[i].Index));
                Assert.That(reusable[i].Distance, Is.EqualTo(direct[i].Distance));
            }
        }

        private static V3f[] CreateFilterPointsF()
        {
            return new V3f[67].SetByIndex(i => new V3f(
                (i * 37 % 71) - 35 + i * 0.001f,
                (i * 19 % 29) - 14 + i * 0.002f,
                (i * 11 % 23) - 11 + i * 0.003f
                ));
        }

        private static V3d[] CreateFilterPointsD()
        {
            return new V3d[67].SetByIndex(i => new V3d(
                (i * 37 % 71) - 35 + i * 0.001,
                (i * 19 % 29) - 14 + i * 0.002,
                (i * 11 % 23) - 11 + i * 0.003
                ));
        }

        private static ReusablePointQuery<V3f> CreateQuery(
            PointKdTreeF<V3f[], V3f> tree, float maxDistance, int maxCount)
        {
            var query = tree.CreateClosestToPointQuery(maxDistance, maxCount);
            return new ReusablePointQuery<V3f>(
                () => query.Filter,
                filter => query.Filter = filter,
                query.Clear,
                () => query.List.Count,
                point => ToHits(tree.GetClosest(query, point))
                );
        }

        private static ReusablePointQuery<V3d> CreateQuery(
            PointKdTreeD<V3d[], V3d> tree, double maxDistance, int maxCount)
        {
            var query = tree.CreateClosestToPointQuery(maxDistance, maxCount);
            return new ReusablePointQuery<V3d>(
                () => query.Filter,
                filter => query.Filter = filter,
                query.Clear,
                () => query.List.Count,
                point => ToHits(tree.GetClosest(query, point))
                );
        }

        private static ReusablePointQuery<V3f> CreateQuery(
            PointRkdTreeF<V3f[], V3f> tree, float maxDistance, int maxCount)
        {
            var query = tree.CreateClosestToPointQuery(maxDistance, maxCount);
            return new ReusablePointQuery<V3f>(
                () => query.Filter,
                filter => query.Filter = filter,
                query.Clear,
                () => query.List.Count,
                point => ToHits(tree.GetClosest(query, point))
                );
        }

        private static ReusablePointQuery<V3d> CreateQuery(
            PointRkdTreeD<V3d[], V3d> tree, double maxDistance, int maxCount)
        {
            var query = tree.CreateClosestToPointQuery(maxDistance, maxCount);
            return new ReusablePointQuery<V3d>(
                () => query.Filter,
                filter => query.Filter = filter,
                query.Clear,
                () => query.List.Count,
                point => ToHits(tree.GetClosest(query, point))
                );
        }

        private static QueryHit[] ToHits(IEnumerable<IndexDist<float>> hits)
            => hits.Select(x => new QueryHit(x.Index, x.Dist)).ToArray();

        private static QueryHit[] ToHits(IEnumerable<IndexDist<double>> hits)
            => hits.Select(x => new QueryHit(x.Index, x.Dist)).ToArray();

        private readonly struct QueryHit
        {
            public long Index { get; }
            public double Distance { get; }

            public QueryHit(long index, double distance)
            {
                Index = index;
                Distance = distance;
            }
        }

        private sealed class ReusablePointQuery<TPoint>
        {
            private readonly Func<Func<long, bool>> m_getFilter;
            private readonly Action<Func<long, bool>> m_setFilter;
            private readonly Action m_clear;
            private readonly Func<int> m_getCount;
            private readonly Func<TPoint, QueryHit[]> m_getClosest;

            public Func<long, bool> Filter
            {
                get => m_getFilter();
                set => m_setFilter(value);
            }

            public int Count => m_getCount();

            public ReusablePointQuery(
                Func<Func<long, bool>> getFilter,
                Action<Func<long, bool>> setFilter,
                Action clear,
                Func<int> getCount,
                Func<TPoint, QueryHit[]> getClosest
                )
            {
                m_getFilter = getFilter;
                m_setFilter = setFilter;
                m_clear = clear;
                m_getCount = getCount;
                m_getClosest = getClosest;
            }

            public void Clear() => m_clear();
            public QueryHit[] GetClosest(TPoint point) => m_getClosest(point);
        }
    }
}
