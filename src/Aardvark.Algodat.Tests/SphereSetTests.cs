using Aardvark.Base;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;

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

        #endregion
    }
}
