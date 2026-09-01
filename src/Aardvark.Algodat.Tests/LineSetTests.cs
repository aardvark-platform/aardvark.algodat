/*
    Copyright (C) 2006-2025. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System.Collections.Generic;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class LineSetTests
    {
        private static readonly FastRay3d s_ray = new(V3d.Zero, V3d.XAxis);

        private static Cylinder3d CylinderAt(double x, double radius = 0.5)
            => new(new V3d(x, -1.0, 0.0), new V3d(x, 1.0, 0.0), radius);

        private static void AssertHit(ObjectRayHit hit, LineSet set, int index, double t)
        {
            ClassicAssert.AreSame(set, hit.SetObject.Set);
            ClassicAssert.AreEqual(index, hit.SetObject.Index);
            ClassicAssert.AreEqual(t, hit.RayHit.T, 1e-12);
            ClassicAssert.IsTrue(hit.RayHit.Point.ApproximateEquals(s_ray.Ray.GetPointOnRay(t), 1e-12));
        }

        private static ObjectRayHit CreateSentinelHit(LineSet set, double cutoff, out object tag, out List<SetObject> stack)
        {
            tag = new object();
            stack = new List<SetObject> { new(set, 17) };
            var hit = new ObjectRayHit(cutoff)
            {
                SetObject = new SetObject(set, 23),
                ObjectStack = stack,
                Tag = tag
            };
            hit.RayHit.Point = new V3d(91, 92, 93);
            hit.RayHit.Coord = new V2d(41, 42);
            hit.RayHit.BackSide = true;
            hit.RayHit.Part = 37;
            return hit;
        }

        private static void AssertUnchanged(ObjectRayHit expected, ObjectRayHit actual, object tag, List<SetObject> stack)
        {
            ClassicAssert.AreEqual(expected.RayHit.T, actual.RayHit.T);
            ClassicAssert.AreEqual(expected.RayHit.Point, actual.RayHit.Point);
            ClassicAssert.AreEqual(expected.RayHit.Coord, actual.RayHit.Coord);
            ClassicAssert.AreEqual(expected.RayHit.BackSide, actual.RayHit.BackSide);
            ClassicAssert.AreEqual(expected.RayHit.Part, actual.RayHit.Part);
            ClassicAssert.AreSame(expected.SetObject.Set, actual.SetObject.Set);
            ClassicAssert.AreEqual(expected.SetObject.Index, actual.SetObject.Index);
            ClassicAssert.AreSame(stack, actual.ObjectStack);
            ClassicAssert.AreSame(tag, actual.Tag);
        }

        [Test]
        public void ObjectBoundingBoxReturnsAggregateAndSelectedCylinderBounds()
        {
            var set = new LineSet(new[] { CylinderAt(3.0), CylinderAt(8.0) });

            ClassicAssert.AreEqual(
                new Box3d(new V3d(2.5, -1.0, -0.5), new V3d(8.5, 1.0, 0.5)),
                set.ObjectBoundingBox());
            ClassicAssert.AreEqual(
                new Box3d(new V3d(2.5, -1.0, -0.5), new V3d(3.5, 1.0, 0.5)),
                set.ObjectBoundingBox(0));
            ClassicAssert.AreEqual(
                new Box3d(new V3d(7.5, -1.0, -0.5), new V3d(8.5, 1.0, 0.5)),
                set.ObjectBoundingBox(1));
        }

        [Test]
        public void RayIntersectionReturnsNearestHitRegardlessOfIndexOrder()
        {
            var set = new LineSet(new[] { CylinderAt(8.0), CylinderAt(3.0), CylinderAt(5.0) });

            foreach (var indices in new[] { new[] { 0, 2, 1 }, new[] { 1, 2, 0 } })
            {
                var hit = new ObjectRayHit(100.0);
                var found = set.ObjectsIntersectRay(
                    indices, 0, indices.Length, s_ray,
                    objectFilter: null, hitFilter: null,
                    tmin: 0.0, tmax: 100.0, ref hit);

                ClassicAssert.IsTrue(found);
                AssertHit(hit, set, index: 1, t: 2.5);
            }
        }

        [Test]
        public void NullObjectFilterInspectsOnlyRequestedIndexSlice()
        {
            var set = new LineSet(new[]
            {
                CylinderAt(2.0), CylinderAt(8.0), CylinderAt(5.0), CylinderAt(1.0)
            });
            var hit = new ObjectRayHit(100.0);

            var found = set.ObjectsIntersectRay(
                new[] { 0, 1, 2, 3 }, firstIndex: 1, indexCount: 2, s_ray,
                objectFilter: null, hitFilter: null,
                tmin: 0.0, tmax: 100.0, ref hit);

            ClassicAssert.IsTrue(found);
            AssertHit(hit, set, index: 2, t: 4.5);
        }

        [Test]
        public void ObjectFilterInspectsOnlyRequestedSliceAndExcludesCandidates()
        {
            var set = new LineSet(new[]
            {
                CylinderAt(2.0), CylinderAt(8.0), CylinderAt(5.0), CylinderAt(1.0)
            });
            var inspected = new List<int>();
            var hit = new ObjectRayHit(100.0);

            var found = set.ObjectsIntersectRay(
                new[] { 0, 1, 2, 3 }, firstIndex: 1, indexCount: 2, s_ray,
                objectFilter: (_, index) => { inspected.Add(index); return index != 2; },
                hitFilter: null,
                tmin: 0.0, tmax: 100.0, ref hit);

            ClassicAssert.IsTrue(found);
            CollectionAssert.AreEqual(new[] { 1, 2 }, inspected);
            AssertHit(hit, set, index: 1, t: 7.5);
        }

        [Test]
        public void RejectedNearHitDoesNotHideAcceptedFarHit()
        {
            var set = new LineSet(new[] { CylinderAt(3.0), CylinderAt(8.0) });
            var filtered = new List<int>();
            var hit = new ObjectRayHit(100.0);

            var found = set.ObjectsIntersectRay(
                new[] { 0, 1 }, 0, 2, s_ray,
                objectFilter: null,
                hitFilter: (_, index, _, _) => { filtered.Add(index); return index == 0; },
                tmin: 0.0, tmax: 100.0, ref hit);

            ClassicAssert.IsTrue(found);
            CollectionAssert.AreEqual(new[] { 0, 1 }, filtered);
            AssertHit(hit, set, index: 1, t: 7.5);
        }

        [Test]
        public void IncomingCutoffIsStrictAndPreservesPriorHit()
        {
            var set = new LineSet(new[] { CylinderAt(3.0) });
            var hit = CreateSentinelHit(set, cutoff: 2.5, out var tag, out var stack);
            var original = hit;

            var found = set.ObjectsIntersectRay(
                new[] { 0 }, 0, 1, s_ray,
                objectFilter: null, hitFilter: null,
                tmin: 0.0, tmax: 100.0, ref hit);

            ClassicAssert.IsFalse(found);
            AssertUnchanged(original, hit, tag, stack);
        }

        [Test]
        public void RejectedHitsPreservePriorHitState()
        {
            var set = new LineSet(new[] { CylinderAt(3.0), CylinderAt(8.0) });
            var hit = CreateSentinelHit(set, cutoff: 100.0, out var tag, out var stack);
            var original = hit;

            var found = set.ObjectsIntersectRay(
                new[] { 0, 1 }, 0, 2, s_ray,
                objectFilter: null,
                hitFilter: (_, _, _, _) => true,
                tmin: 0.0, tmax: 100.0, ref hit);

            ClassicAssert.IsFalse(found);
            AssertUnchanged(original, hit, tag, stack);
        }

        [Test]
        public void AbsentHitsPreservePriorHitState()
        {
            var set = new LineSet(new[] { CylinderAt(3.0), CylinderAt(8.0) });
            var missRay = new FastRay3d(new V3d(0.0, 0.0, 10.0), V3d.XAxis);
            var hit = CreateSentinelHit(set, cutoff: 100.0, out var tag, out var stack);
            var original = hit;

            var found = set.ObjectsIntersectRay(
                new[] { 0, 1 }, 0, 2, missRay,
                objectFilter: null, hitFilter: null,
                tmin: 0.0, tmax: 100.0, ref hit);

            ClassicAssert.IsFalse(found);
            AssertUnchanged(original, hit, tag, stack);
        }
    }
}
