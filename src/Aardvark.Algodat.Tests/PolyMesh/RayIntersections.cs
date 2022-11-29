using Aardvark.Base;
using NUnit.Framework;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class PolyMeshRayIntersections
    {
        [Test]
        public void GetRayIntersections()
        {
            var mesh = PolyMeshPrimitives.Box(C4f.Black);
            var ray = new Ray3d(new V3d(0.5, 0.5, -1.0), V3d.ZAxis);

            var hits = mesh.GetRayIntersections(ray);
            hits.Sort((x, y) => x.T.CompareTo(y.T));

            Assert.AreEqual(hits.Count, 2);
            Assert.True(Fun.ApproximateEquals(hits[0].T, 1.0));
            Assert.True(Fun.ApproximateEquals(hits[1].T, 2.0));

            foreach (var hit in hits)
            {
                var p = mesh.GetFace(hit.Part).Polygon3d;
                Assert.True(Vec.AllNaN(hit.Coord));
                Assert.True(p.Contains(Constant<double>.PositiveTinyValue, hit.Point, out double _));
            }
        }

        [Test]
        public void GetRayIntersectionsWithFilter()
        {
            var mesh = PolyMeshPrimitives.Box(C4f.Black);
            var ray = new Ray3d(new V3d(0.5, 0.5, -1.0), V3d.ZAxis);

            var hits = mesh.GetRayIntersections(ray, (in RayHit3d hit) => !hit.BackSide);

            Assert.AreEqual(hits.Count, 1);
            Assert.True(Fun.ApproximateEquals(hits[0].T, 1.0));

            foreach (var hit in hits)
            {
                var p = mesh.GetFace(hit.Part).Polygon3d;
                Assert.True(Vec.AllNaN(hit.Coord));
                Assert.True(p.Contains(Constant<double>.PositiveTinyValue, hit.Point, out double _));
            }
        }

        [Test]
        public void GetRayIntersectionsTriangulated()
        {
            var mesh = PolyMeshPrimitives.Box(C4f.Black).TriangulatedCopy();
            var ray = new Ray3d(new V3d(0.15, 0.45, -1.0), V3d.ZAxis);

            var hits = mesh.GetRayIntersections(ray);
            hits.Sort((x, y) => x.T.CompareTo(y.T));

            Assert.AreEqual(hits.Count, 2);
            Assert.True(Fun.ApproximateEquals(hits[0].T, 1.0));
            Assert.True(Fun.ApproximateEquals(hits[1].T, 2.0));

            foreach (var hit in hits)
            {
                var p = mesh.GetFace(hit.Part).Polygon3d;
                Assert.True(Fun.IsFinite(hit.Coord));
                Assert.True(Vec.AllSmallerOrEqual(hit.Coord, 1.0));
                Assert.True(p.Contains(Constant<double>.PositiveTinyValue, hit.Point, out double _));
            }
        }

        [Test]
        public void TryGetRayIntersection()
        {
            var mesh = PolyMeshPrimitives.Box(C4f.Black);

            var rays = new Ray3d[]
            {
                new Ray3d(new V3d(0.5, 0.5, -1.0), V3d.ZAxis),
                new Ray3d(new V3d(0.5, 0.5, 2.0), -V3d.ZAxis)
            };

            foreach (Ray3d ray in rays)
            {
                var foundHit = mesh.TryGetRayIntersection(ray, out var hit);

                Assert.True(foundHit);
                Assert.True(Fun.ApproximateEquals(hit.T, 1.0));
                Assert.True(Vec.AllNaN(hit.Coord));

                var p = mesh.GetFace(hit.Part).Polygon3d;
                Assert.True(p.Contains(Constant<double>.PositiveTinyValue, hit.Point, out double _));
            }
        }

        [Test]
        public void TryGetRayIntersectionWithFilter()
        {
            var mesh = PolyMeshPrimitives.Box(C4f.Black);

            var rays = new Ray3d[]
            {
                new Ray3d(new V3d(0.5, 0.5, -1.0), V3d.ZAxis),
                new Ray3d(new V3d(0.5, 0.5, 2.0), -V3d.ZAxis)
            };

            foreach (Ray3d ray in rays)
            {
                var foundHit = mesh.TryGetRayIntersection(ray, out var hit, (in RayHit3d h) => h.BackSide);

                Assert.True(foundHit);
                Assert.True(Fun.ApproximateEquals(hit.T, 2.0));
                Assert.True(Vec.AllNaN(hit.Coord));

                var p = mesh.GetFace(hit.Part).Polygon3d;
                Assert.True(p.Contains(Constant<double>.PositiveTinyValue, hit.Point, out double _));
            }
        }
    }
}
