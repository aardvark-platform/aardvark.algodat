/*
    Copyright (C) 2006-2026. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using Aardvark.Base;
using Aardvark.Geometry;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class IntersectableClosestPointTests
    {
        private static readonly V2d s_preservedCoord = new(7.0, 11.0);
        private static readonly V3d s_preservedPoint = new(101.0, 102.0, 103.0);

        [Test]
        public void BoxSetDirectClosestPointHonorsSliceFilterAndCutoff()
        {
            var boxes = CreateDirectBoxes();
            var set = new IntersectableBoxSet(boxes);
            var objectIndices = new[] { 2, 0, 1, 2 };
            var stack = new List<SetObject> { new(set, 17) };
            var closest = InitialResult(set, 100.0, stack);
            var pointFilterCalls = 0;

            var found = set.ClosestPoint(
                objectIndices, 1, 2, V3d.Zero,
                ios_index_objectFilter: null,
                (_, _, _, _) => { pointFilterCalls++; return true; },
                ref closest
                );

            ClassicAssert.IsTrue(found);
            ClassicAssert.AreEqual(0, pointFilterCalls);
            AssertClosest(closest, set, 0, new V3d(1.0, 0.0, 0.0), 1.0, stack);

            closest = InitialResult(set, 100.0, stack);
            found = set.ClosestPoint(
                objectIndices, 1, 2, V3d.Zero,
                (_, index) => index == 1,
                ios_index_part_ocp_pointFilter: null,
                ref closest
                );

            ClassicAssert.IsTrue(found);
            AssertClosest(closest, set, 1, new V3d(8.0, 0.0, 0.0), 64.0, stack);

            closest = InitialResult(set, 0.01, stack);
            var unchanged = closest;
            found = set.ClosestPoint(
                objectIndices, 0, objectIndices.Length, V3d.Zero,
                ios_index_objectFilter: null,
                ios_index_part_ocp_pointFilter: null,
                ref closest
                );

            ClassicAssert.IsFalse(found);
            AssertUnchanged(closest, unchanged);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TriangleSetDirectClosestPointHonorsSliceFilterAndCutoff(bool indexed)
        {
            var set = CreateDirectTriangleSet(indexed);
            var objectIndices = new[] { 2, 0, 1, 2 };
            var stack = new List<SetObject> { new(set, 19) };
            var closest = InitialResult(set, 100.0, stack);
            var pointFilterCalls = 0;

            var found = set.ClosestPoint(
                objectIndices, 1, 2, V3d.Zero,
                ios_index_objectFilter: null,
                (_, _, _, _) => { pointFilterCalls++; return true; },
                ref closest
                );

            ClassicAssert.IsTrue(found);
            ClassicAssert.AreEqual(0, pointFilterCalls);
            AssertClosest(closest, set, 0, new V3d(1.0, 0.0, 0.0), 1.0, stack);

            closest = InitialResult(set, 100.0, stack);
            found = set.ClosestPoint(
                objectIndices, 1, 2, V3d.Zero,
                (_, index) => index == 1,
                ios_index_part_ocp_pointFilter: null,
                ref closest
                );

            ClassicAssert.IsTrue(found);
            AssertClosest(closest, set, 1, new V3d(8.0, 0.0, 0.0), 64.0, stack);

            closest = InitialResult(set, 0.01, stack);
            var unchanged = closest;
            found = set.ClosestPoint(
                objectIndices, 0, objectIndices.Length, V3d.Zero,
                ios_index_objectFilter: null,
                ios_index_part_ocp_pointFilter: null,
                ref closest
                );

            ClassicAssert.IsFalse(found);
            AssertUnchanged(closest, unchanged);
        }

        [Test]
        public void BoxSetKdTreeClosestPointHonorsNullAndSelectiveFilters()
        {
            var set = new IntersectableBoxSet(CreateDirectBoxes());
            var tree = CreateTree(set);
            var stack = new List<SetObject> { new(set, 23) };
            var closest = InitialResult(set, 100.0, stack);

            var found = tree.ClosestPoint(V3d.Zero, ref closest);

            ClassicAssert.IsTrue(found);
            AssertClosest(closest, set, 2, new V3d(0.25, 0.0, 0.0), 0.0625, stack);

            closest = InitialResult(set, 100.0, stack);
            var pointFilterCalls = 0;
            found = tree.ClosestPoint(
                V3d.Zero,
                (_, index) => index == 1,
                (_, _, _, _) => { pointFilterCalls++; return true; },
                ref closest
                );

            ClassicAssert.IsTrue(found);
            ClassicAssert.AreEqual(0, pointFilterCalls);
            AssertClosest(closest, set, 1, new V3d(8.0, 0.0, 0.0), 64.0, stack);

            closest = InitialResult(set, 0.01, stack);
            var unchanged = closest;
            found = tree.ClosestPoint(V3d.Zero, ref closest);
            ClassicAssert.IsFalse(found);
            AssertUnchanged(closest, unchanged);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TriangleSetKdTreeClosestPointHonorsNullAndSelectiveFilters(bool indexed)
        {
            var set = CreateDirectTriangleSet(indexed);
            var tree = CreateTree(set);
            var stack = new List<SetObject> { new(set, 29) };
            var closest = InitialResult(set, 100.0, stack);

            var found = tree.ClosestPoint(V3d.Zero, ref closest);

            ClassicAssert.IsTrue(found);
            AssertClosest(closest, set, 2, new V3d(0.25, 0.0, 0.0), 0.0625, stack);

            closest = InitialResult(set, 100.0, stack);
            var pointFilterCalls = 0;
            found = tree.ClosestPoint(
                V3d.Zero,
                (_, index) => index == 1,
                (_, _, _, _) => { pointFilterCalls++; return true; },
                ref closest
                );

            ClassicAssert.IsTrue(found);
            ClassicAssert.AreEqual(0, pointFilterCalls);
            AssertClosest(closest, set, 1, new V3d(8.0, 0.0, 0.0), 64.0, stack);

            closest = InitialResult(set, 0.01, stack);
            var unchanged = closest;
            found = tree.ClosestPoint(V3d.Zero, ref closest);
            ClassicAssert.IsFalse(found);
            AssertUnchanged(closest, unchanged);
        }

        [Test]
        public void BoxSetKdTreeClosestPointMatchesBruteForceAcrossLeaves()
        {
            var boxes = Enumerable.Range(0, 96)
                .Select(i =>
                {
                    var min = new V3d((i % 12) * 2.0, ((i / 12) % 4) * 3.0, (i / 48) * 4.0);
                    return new Box3d(min, min + new V3d(0.4, 0.6, 0.8));
                })
                .ToArray();
            var set = new IntersectableBoxSet(boxes);
            var tree = CreateTree(set);
            ClassicAssert.Greater(tree.Tree.Leafs().Count(), 1);

            for (var i = 0; i < 48; i++)
            {
                var query = new V3d((i * 17 % 25) - 1.25, (i * 11 % 15) - 1.5, (i * 7 % 9) - 0.75);
                const double cutoffSquared = 400.0;
                var expected = BruteForceBox(boxes, query, cutoffSquared, index => (index % 4) != 1);
                var stack = new List<SetObject> { new(set, i) };
                var closest = InitialResult(set, cutoffSquared, stack);

                var found = tree.ClosestPoint(query, (_, index) => (index % 4) != 1, null, ref closest);

                ClassicAssert.AreEqual(expected.Index >= 0, found);
                if (found)
                    AssertClosest(closest, set, expected.Index, expected.Point, expected.DistanceSquared, stack);
            }
        }

        [Test]
        public void TriangleSetKdTreeClosestPointMatchesBruteForceAcrossLeaves()
        {
            var triangles = Enumerable.Range(0, 96)
                .Select(i =>
                {
                    var center = new V3d((i % 12) * 2.0, ((i / 12) % 4) * 3.0, (i / 48) * 4.0);
                    return new[]
                    {
                        center + new V3d(-0.5, -0.5, 0.0),
                        center + new V3d( 0.5, -0.5, 0.0),
                        center + new V3d( 0.0,  0.5, 0.0)
                    };
                })
                .ToArray();
            var positions = triangles.SelectMany(t => t).Select(p => (V3f)p).ToArray();
            var set = new IntersectableTriangleSet(positions);
            var tree = CreateTree(set);
            ClassicAssert.Greater(tree.Tree.Leafs().Count(), 1);

            for (var i = 0; i < 48; i++)
            {
                var query = new V3d((i * 13 % 25) - 1.1, (i * 7 % 15) - 1.3, (i * 5 % 9) - 0.6);
                const double cutoffSquared = 400.0;
                var expected = BruteForceTriangle(triangles, query, cutoffSquared, index => (index % 3) != 0);
                var stack = new List<SetObject> { new(set, i) };
                var closest = InitialResult(set, cutoffSquared, stack);

                var found = tree.ClosestPoint(query, (_, index) => (index % 3) != 0, null, ref closest);

                ClassicAssert.AreEqual(expected.Index >= 0, found);
                if (found)
                    AssertClosest(closest, set, expected.Index, expected.Point, expected.DistanceSquared, stack);
            }
        }

        private static Box3d[] CreateDirectBoxes()
            => new[]
            {
                new Box3d(new V3d(1.0, -1.0, -1.0), new V3d(2.0, 1.0, 1.0)),
                new Box3d(new V3d(8.0, -1.0, -1.0), new V3d(9.0, 1.0, 1.0)),
                new Box3d(new V3d(0.25, -1.0, -1.0), new V3d(0.5, 1.0, 1.0))
            };

        private static IntersectableTriangleSet CreateDirectTriangleSet(bool indexed)
        {
            static V3d[] Triangle(double x) =>
                new[]
                {
                    new V3d(x, -1.0, -1.0),
                    new V3d(x,  1.0, -1.0),
                    new V3d(x,  0.0,  1.0)
                };

            var triangles = new[] { Triangle(1.0), Triangle(8.0), Triangle(0.25) };
            if (!indexed)
                return new IntersectableTriangleSet(triangles.SelectMany(t => t).Select(p => (V3f)p).ToArray());

            var positions = triangles.Reverse().SelectMany(t => t).Select(p => (V3f)p).ToArray();
            var indices = new[] { 6, 7, 8, 3, 4, 5, 0, 1, 2 };
            return new IntersectableTriangleSet(indices, positions);
        }

        private static KdIntersectionTree CreateTree(IIntersectableObjectSet set)
            => new(
                set,
                KdIntersectionTree.BuildFlags.Raytracing |
                KdIntersectionTree.BuildFlags.NoMultithreading
                );

        private static ObjectClosestPoint InitialResult(
            IIntersectableObjectSet set,
            double cutoffSquared,
            List<SetObject> stack
            )
            => new()
            {
                DistanceSquared = cutoffSquared,
                Distance = Math.Sqrt(cutoffSquared),
                Point = s_preservedPoint,
                Coord = s_preservedCoord,
                SetObject = new SetObject(set, 71),
                ObjectStack = stack
            };

        private static void AssertClosest(
            ObjectClosestPoint actual,
            IIntersectableObjectSet expectedSet,
            int expectedIndex,
            V3d expectedPoint,
            double expectedDistanceSquared,
            List<SetObject> expectedStack
            )
        {
            ClassicAssert.AreEqual(expectedDistanceSquared, actual.DistanceSquared, 1e-12);
            ClassicAssert.AreEqual(Math.Sqrt(expectedDistanceSquared), actual.Distance, 1e-12);
            ClassicAssert.AreEqual(expectedPoint, actual.Point);
            ClassicAssert.AreSame(expectedSet, actual.SetObject.Set);
            ClassicAssert.AreEqual(expectedIndex, actual.SetObject.Index);
            ClassicAssert.AreEqual(s_preservedCoord, actual.Coord);
            ClassicAssert.AreSame(expectedStack, actual.ObjectStack);
        }

        private static void AssertUnchanged(ObjectClosestPoint actual, ObjectClosestPoint expected)
        {
            ClassicAssert.AreEqual(expected.DistanceSquared, actual.DistanceSquared);
            ClassicAssert.AreEqual(expected.Distance, actual.Distance);
            ClassicAssert.AreEqual(expected.Point, actual.Point);
            ClassicAssert.AreEqual(expected.Coord, actual.Coord);
            ClassicAssert.AreSame(expected.SetObject.Set, actual.SetObject.Set);
            ClassicAssert.AreEqual(expected.SetObject.Index, actual.SetObject.Index);
            ClassicAssert.AreSame(expected.ObjectStack, actual.ObjectStack);
        }

        private static (int Index, V3d Point, double DistanceSquared) BruteForceBox(
            Box3d[] boxes,
            V3d query,
            double cutoffSquared,
            Func<int, bool> filter
            )
        {
            var index = -1;
            var point = V3d.NaN;
            var distanceSquared = cutoffSquared;
            for (var i = 0; i < boxes.Length; i++)
            {
                if (!filter(i)) continue;
                var candidate = boxes[i].GetClosestPointOn(query);
                var d2 = Vec.DistanceSquared(query, candidate);
                if (d2 >= distanceSquared) continue;
                index = i;
                point = candidate;
                distanceSquared = d2;
            }
            return (index, point, distanceSquared);
        }

        private static (int Index, V3d Point, double DistanceSquared) BruteForceTriangle(
            V3d[][] triangles,
            V3d query,
            double cutoffSquared,
            Func<int, bool> filter
            )
        {
            var index = -1;
            var point = V3d.NaN;
            var distanceSquared = cutoffSquared;
            for (var i = 0; i < triangles.Length; i++)
            {
                if (!filter(i)) continue;
                var triangle = triangles[i];
                var candidate = query.GetClosestPointOnTriangle(triangle[0], triangle[1], triangle[2]);
                var d2 = Vec.DistanceSquared(query, candidate);
                if (d2 >= distanceSquared) continue;
                index = i;
                point = candidate;
                distanceSquared = d2;
            }
            return (index, point, distanceSquared);
        }
    }
}
