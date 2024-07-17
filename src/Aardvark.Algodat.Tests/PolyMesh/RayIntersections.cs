using Aardvark.Base;
using NUnit.Framework;
using NUnit.Framework.Legacy;

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

            ClassicAssert.AreEqual(hits.Count, 2);
            ClassicAssert.True(Fun.ApproximateEquals(hits[0].T, 1.0));
            ClassicAssert.True(Fun.ApproximateEquals(hits[1].T, 2.0));

            foreach (var hit in hits)
            {
                var p = mesh.GetFace(hit.Part).Polygon3d;
                ClassicAssert.True(Vec.AllNaN(hit.Coord));
                ClassicAssert.True(p.Contains(Constant<double>.PositiveTinyValue, hit.Point, out double _));
            }
        }

        [Test]
        public void GetRayIntersectionsWithFilter()
        {
            var mesh = PolyMeshPrimitives.Box(C4f.Black);
            var ray = new Ray3d(new V3d(0.5, 0.5, -1.0), V3d.ZAxis);

            var hits = mesh.GetRayIntersections(ray, filter: (in RayHit3d hit) => !hit.BackSide);

            ClassicAssert.AreEqual(hits.Count, 1);
            ClassicAssert.True(Fun.ApproximateEquals(hits[0].T, 1.0));

            foreach (var hit in hits)
            {
                var p = mesh.GetFace(hit.Part).Polygon3d;
                ClassicAssert.True(Vec.AllNaN(hit.Coord));
                ClassicAssert.True(p.Contains(Constant<double>.PositiveTinyValue, hit.Point, out double _));
            }
        }

        [Test]
        public void GetRayIntersectionsTriangulated()
        {
            var mesh = PolyMeshPrimitives.Box(C4f.Black).TriangulatedCopy();
            var ray = new Ray3d(new V3d(0.15, 0.45, -1.0), V3d.ZAxis);

            var hits = mesh.GetRayIntersections(ray);
            hits.Sort((x, y) => x.T.CompareTo(y.T));

            ClassicAssert.AreEqual(hits.Count, 2);
            ClassicAssert.True(Fun.ApproximateEquals(hits[0].T, 1.0));
            ClassicAssert.True(Fun.ApproximateEquals(hits[1].T, 2.0));

            foreach (var hit in hits)
            {
                var p = mesh.GetFace(hit.Part).Polygon3d;
                ClassicAssert.True(Fun.IsFinite(hit.Coord));
                ClassicAssert.True(Vec.AllSmallerOrEqual(hit.Coord, 1.0));
                ClassicAssert.True(p.Contains(Constant<double>.PositiveTinyValue, hit.Point, out double _));
            }
        }

        [Test]
        public void Intersects()
        {
            var mesh = PolyMeshPrimitives.Box(C4f.Black);

            var rays = new Ray3d[]
            {
                new Ray3d(new V3d(0.5, 0.5, -1.0), V3d.ZAxis),
                new Ray3d(new V3d(0.5, 0.5, 2.0), -V3d.ZAxis)
            };

            foreach (Ray3d ray in rays)
            {
                var foundHit = mesh.Intersects(ray, out var hit);

                ClassicAssert.True(foundHit);
                ClassicAssert.True(Fun.ApproximateEquals(hit.T, 1.0));
                ClassicAssert.True(Vec.AllNaN(hit.Coord));

                var p = mesh.GetFace(hit.Part).Polygon3d;
                ClassicAssert.True(p.Contains(Constant<double>.PositiveTinyValue, hit.Point, out double _));
            }
        }

        [Test]
        public void IntersectsWithFilter()
        {
            var mesh = PolyMeshPrimitives.Box(C4f.Black);

            var rays = new Ray3d[]
            {
                new Ray3d(new V3d(0.5, 0.5, -1.0), V3d.ZAxis),
                new Ray3d(new V3d(0.5, 0.5, 2.0), -V3d.ZAxis)
            };

            foreach (Ray3d ray in rays)
            {
                var foundHit = mesh.Intersects(ray, out var hit, filter: (in RayHit3d h) => h.BackSide);

                ClassicAssert.True(foundHit);
                ClassicAssert.True(Fun.ApproximateEquals(hit.T, 2.0));
                ClassicAssert.True(Vec.AllNaN(hit.Coord));

                var p = mesh.GetFace(hit.Part).Polygon3d;
                ClassicAssert.True(p.Contains(Constant<double>.PositiveTinyValue, hit.Point, out double _));
            }
        }

        [Test]
        public void IntersectsWithinInterval()
        {
            var box = PolyMeshPrimitives.Box(C4f.Black);

            var meshes = new PolyMesh[]
            {
                box,
                box, box.TriangulatedCopy()
            };

            var rays = new Ray3d[]
            {
                new Ray3d(new V3d(0.5, 0.5, -1.0), -V3d.ZAxis),
                new Ray3d(new V3d(0.5, 0.5, 2.0), V3d.ZAxis)
            };

            foreach (var mesh in meshes)
            {
                foreach (Ray3d ray in rays)
                {
                    var foundHit = mesh.Intersects(ray, out var hit, -2.5);

                    ClassicAssert.True(foundHit);
                    ClassicAssert.True(Fun.ApproximateEquals(hit.T, -1.0));

                    var p = mesh.GetFace(hit.Part).Polygon3d;
                    ClassicAssert.True(p.Contains(Constant<double>.PositiveTinyValue, hit.Point, out double _));
                }
            }
        }
    }
}
