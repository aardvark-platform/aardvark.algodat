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
using Aardvark.Base;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class NormalsEstimationTests
    {
        private static V3d[] CreatePlanarQualityPoints()
        {
            return (
                from x in Enumerable.Range(-1, 3)
                from y in Enumerable.Range(-1, 3)
                select new V3d(x, y, 0.02 * x * y)
                ).ToArray();
        }

        private static V3d[] CreateExactPlanePoints()
        {
            return (
                from x in Enumerable.Range(-1, 3)
                from y in Enumerable.Range(-1, 3)
                select new V3d(x, y, 0.0)
                ).ToArray();
        }

        private static void AssertNormalAndQualityResultsEqual(
            (V3f[] normals, float[] qualities) expected,
            (V3f[] normals, float[] qualities) actual,
            float qualityTolerance = 0.0f
            )
        {
            Assert.That(actual.normals, Has.Length.EqualTo(expected.normals.Length));
            Assert.That(actual.qualities, Has.Length.EqualTo(expected.qualities.Length));
            for (var i = 0; i < expected.normals.Length; i++)
            {
                Assert.That(
                    Math.Abs(expected.normals[i].Dot(actual.normals[i])),
                    Is.EqualTo(1.0f).Within(1e-5f)
                    );
                Assert.That(actual.qualities[i], Is.EqualTo(expected.qualities[i]).Within(qualityTolerance));
            }
        }

        [Test]
        public void EstimateNormalsAndQualityRecognizesPlanesAndDegenerateNeighborhoods()
        {
            var plane = CreateExactPlanePoints();
            var (planeNormals, planeQualities) = plane.EstimateNormalsAndQuality(plane.Length);
            Assert.That(planeNormals, Has.Length.EqualTo(plane.Length));
            Assert.That(planeQualities, Has.All.EqualTo(1.0f).Within(1e-6f));
            Assert.That(planeNormals, Has.All.Matches<V3f>(n => Math.Abs(n.Dot(V3f.ZAxis)) > 1.0f - 1e-6f));

            var collinear = Enumerable.Range(-3, 7).Select(x => new V3d(x, 2.0 * x, -x)).ToArray();
            var (_, lineQualities) = collinear.EstimateNormalsAndQuality(collinear.Length);
            Assert.That(lineQualities, Has.All.EqualTo(0.0f));

            var coincident = Enumerable.Repeat(new V3d(4.0, -2.0, 7.0), 5).ToArray();
            var (_, coincidentQualities) = coincident.EstimateNormalsAndQuality(coincident.Length);
            Assert.That(coincidentQualities, Has.All.EqualTo(0.0f));

            var twoPoints = new[] { V3d.Zero, V3d.XAxis };
            var (_, twoPointQualities) = twoPoints.EstimateNormalsAndQuality(3);
            Assert.That(twoPointQualities, Has.All.EqualTo(0.0f));
        }

        [Test]
        public void EstimateNormalsAndQualitySeparatesNearLinearAndNoisyPlanarNeighborhoods()
        {
            var nearLinear = Enumerable.Range(-4, 9)
                .Select(x => new V3d(x, (x & 1) == 0 ? 0.01 : -0.01, 0.0))
                .ToArray();
            var (_, lineQualities) = nearLinear.EstimateNormalsAndQuality(nearLinear.Length);
            Assert.That(lineQualities, Has.All.GreaterThan(0.0f));
            Assert.That(lineQualities, Has.All.LessThan(0.001f));

            var noisyPlane = CreatePlanarQualityPoints();
            var (_, planeQualities) = noisyPlane.EstimateNormalsAndQuality(noisyPlane.Length);
            Assert.That(planeQualities, Has.All.GreaterThan(0.99f));
            Assert.That(planeQualities, Has.All.LessThanOrEqualTo(1.0f));
        }

        [Test]
        public void EstimateNormalsAndQualityIsScaleAndTranslationInvariant()
        {
            var points = CreatePlanarQualityPoints();
            var transformed = points
                .Select(p => p * 37.0 + new V3d(1_000_000.0, -2_000_000.0, 3_000_000.0))
                .ToArray();

            var original = points.EstimateNormalsAndQuality(points.Length);
            var changed = transformed.EstimateNormalsAndQuality(transformed.Length);
            AssertNormalAndQualityResultsEqual(original, changed, qualityTolerance: 1e-5f);
        }

        [Test]
        public void EstimateNormalsAndQualityHasFloatDoubleParity()
        {
            var pointsD = CreatePlanarQualityPoints();
            var pointsF = pointsD.Map(p => (V3f)p);
            var resultD = pointsD.EstimateNormalsAndQuality(pointsD.Length, pointsD.BuildKdTree());
            var resultF = pointsF.EstimateNormalsAndQuality(pointsF.Length, pointsF.BuildKdTree());
            AssertNormalAndQualityResultsEqual(resultD, resultF, qualityTolerance: 1e-5f);
        }

        [Test]
        public async Task EstimateNormalsAndQualityVariantsAgree()
        {
            var pointsD = CreatePlanarQualityPoints();
            var pointsF = pointsD.Map(p => (V3f)p);
            var kdTreeD = pointsD.BuildKdTree();
            var kdTreeF = pointsF.BuildKdTree();

            var expectedD = pointsD.EstimateNormalsAndQuality(pointsD.Length, kdTreeD);
            AssertNormalAndQualityResultsEqual(expectedD, pointsD.EstimateNormalsAndQuality(pointsD.Length));
            AssertNormalAndQualityResultsEqual(expectedD, await pointsD.EstimateNormalsAndQualityAsync(pointsD.Length, kdTreeD));
            AssertNormalAndQualityResultsEqual(expectedD, await pointsD.EstimateNormalsAndQualityAsync(pointsD.Length));

            var expectedF = pointsF.EstimateNormalsAndQuality(pointsF.Length, kdTreeF);
            AssertNormalAndQualityResultsEqual(expectedF, pointsF.EstimateNormalsAndQuality(pointsF.Length));
            AssertNormalAndQualityResultsEqual(expectedF, await pointsF.EstimateNormalsAndQualityAsync(pointsF.Length, kdTreeF));
            AssertNormalAndQualityResultsEqual(expectedF, await pointsF.EstimateNormalsAndQualityAsync(pointsF.Length));
        }

        [Test]
        public async Task EstimateNormalsAndQualityValidatesInputsAndHandlesEmptyArrays()
        {
            Assert.Throws<ArgumentNullException>(() => Normals.EstimateNormalsAndQuality((V3f[])null!, 3));
            Assert.Throws<ArgumentNullException>(() => Normals.EstimateNormalsAndQuality((V3d[])null!, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => Array.Empty<V3f>().EstimateNormalsAndQuality(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => Array.Empty<V3d>().EstimateNormalsAndQuality(2));

            var pointsF = CreateExactPlanePoints().Map(p => (V3f)p);
            var pointsD = CreateExactPlanePoints();
            Assert.Throws<ArgumentNullException>(() => pointsF.EstimateNormalsAndQuality(3, null!));
            Assert.Throws<ArgumentNullException>(() => pointsD.EstimateNormalsAndQuality(3, null!));
            Assert.ThrowsAsync<ArgumentNullException>(async () => await Normals.EstimateNormalsAndQualityAsync((V3f[])null!, 3));
            Assert.ThrowsAsync<ArgumentNullException>(async () => await Normals.EstimateNormalsAndQualityAsync((V3d[])null!, 3));
            Assert.ThrowsAsync<ArgumentNullException>(async () => await pointsF.EstimateNormalsAndQualityAsync(3, null!));
            Assert.ThrowsAsync<ArgumentNullException>(async () => await pointsD.EstimateNormalsAndQualityAsync(3, null!));
            Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await pointsF.EstimateNormalsAndQualityAsync(2));
            Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await pointsD.EstimateNormalsAndQualityAsync(2));

            var emptyF = Array.Empty<V3f>().EstimateNormalsAndQuality(3);
            var emptyD = Array.Empty<V3d>().EstimateNormalsAndQuality(3);
            var emptyAsyncF = await Array.Empty<V3f>().EstimateNormalsAndQualityAsync(3);
            var emptyAsyncD = await Array.Empty<V3d>().EstimateNormalsAndQualityAsync(3);
            Assert.That(emptyF.normals, Is.Empty);
            Assert.That(emptyF.qualities, Is.Empty);
            Assert.That(emptyD.normals, Is.Empty);
            Assert.That(emptyD.qualities, Is.Empty);
            Assert.That(emptyAsyncF.normals, Is.Empty);
            Assert.That(emptyAsyncF.qualities, Is.Empty);
            Assert.That(emptyAsyncD.normals, Is.Empty);
            Assert.That(emptyAsyncD.qualities, Is.Empty);
        }

        [Test]
        public void EstimateNormalsAndQualityPreservesExistingNormalResults()
        {
            var pointsD = CreatePlanarQualityPoints();
            var pointsF = pointsD.Map(p => (V3f)p);
            var kdTreeD = pointsD.BuildKdTree();
            var kdTreeF = pointsF.BuildKdTree();

            var existingD = pointsD.EstimateNormals(pointsD.Length, kdTreeD);
            var existingF = pointsF.EstimateNormals(pointsF.Length, kdTreeF);
            var qualityD = pointsD.EstimateNormalsAndQuality(pointsD.Length, kdTreeD);
            var qualityF = pointsF.EstimateNormalsAndQuality(pointsF.Length, kdTreeF);

            Assert.That(qualityD.normals, Is.EqualTo(existingD));
            Assert.That(qualityF.normals, Is.EqualTo(existingF));
        }

        [Test]
        public void CanEstimateNormals_FromZeroToThreePoints()
        {
            var r = new Random();
            for (var n = 0; n < 4; n++)
            {
                var ps = new V3f[n].SetByIndex(_ => new V3f(r.NextDouble(), r.NextDouble(), r.NextDouble()));

                var kd = ps.BuildKdTree();

                var ns = Normals.EstimateNormals(ps, 16, kd);
                ClassicAssert.IsTrue(ns.Length == n);
            }
        }

        [Test]
        public void CanEstimateNormals_FromZeroToThreePoints_List()
        {
            var r = new Random();
            for (var n = 0; n < 4; n++)
            {
                var ps = new V3f[n].SetByIndex(_ => new V3f(r.NextDouble(), r.NextDouble(), r.NextDouble())).ToList();

                var kd = ps.BuildKdTree();

                var ns = Normals.EstimateNormals(ps, 16, kd);
                ClassicAssert.IsTrue(ns.Length == n);
            }
        }

        [Test]
        public void CanEstimateNormalsD_FromZeroToThreePoints()
        {
            var r = new Random();
            for (var n = 0; n < 4; n++)
            {
                var ps = new V3d[n].SetByIndex(_ => new V3d(r.NextDouble(), r.NextDouble(), r.NextDouble()));

                var kd = ps.BuildKdTree();

                var ns = Normals.EstimateNormals(ps, 16, kd);
                ClassicAssert.IsTrue(ns.Length == n);
            }
        }



        [Test]
        public void CanEstimateNormalsAsync_FromZeroToThreePoints()
        {
            var r = new Random();
            for (var n = 0; n < 4; n++)
            {
                var ps = new V3f[n].SetByIndex(_ => new V3f(r.NextDouble(), r.NextDouble(), r.NextDouble()));

                var kd = ps.BuildKdTreeAsync().Result;

                var ns = Normals.EstimateNormalsAsync(ps, 16, kd).Result;
                ClassicAssert.IsTrue(ns.Length == n);
            }
        }

        [Test]
        public void CanEstimateNormalsAsyncD_FromZeroToThreePoints()
        {
            var r = new Random();
            for (var n = 0; n < 4; n++)
            {
                var ps = new V3d[n].SetByIndex(_ => new V3d(r.NextDouble(), r.NextDouble(), r.NextDouble()));

                var kd = ps.BuildKdTreeAsync().Result;

                var ns = Normals.EstimateNormalsAsync(ps, 16, kd).Result;
                ClassicAssert.IsTrue(ns.Length == n);
            }
        }



        [Test]
        public void CanEstimateNormals()
        {
            var ps = new[]
            {
                new V3f(0, 0, 0),
                new V3f(1, 0, 0),
                new V3f(1, 1, 0),
                new V3f(0, 1, 0),
            };

            var kd = ps.BuildKdTree();

            var ns = Normals.EstimateNormals(ps, 16, kd);
            ClassicAssert.IsTrue(ns.Length == 4);
            ClassicAssert.IsTrue(ns.All(n => n == V3f.ZAxis));
        }

        [Test]
        public void CanEstimateNormalsD()
        {
            var ps = new[]
            {
                new V3d(0, 0, 0),
                new V3d(1, 0, 0),
                new V3d(1, 1, 0),
                new V3d(0, 1, 0),
            };

            var kd = ps.BuildKdTree();

            var ns = Normals.EstimateNormals(ps, 16, kd);
            ClassicAssert.IsTrue(ns.Length == 4);
            ClassicAssert.IsTrue(ns.All(n => n == V3f.ZAxis));
        }



        [Test]
        public void CanEstimateNormalsAsync()
        {
            var ps = new[]
            {
                new V3f(0, 0, 0),
                new V3f(1, 0, 0),
                new V3f(1, 1, 0),
                new V3f(0, 1, 0),
            };

            var kd = ps.BuildKdTreeAsync().Result;

            var ns = Normals.EstimateNormalsAsync(ps, 16, kd).Result;
            ClassicAssert.IsTrue(ns.Length == 4);
            ClassicAssert.IsTrue(ns.All(n => n == V3f.ZAxis));
        }

        [Test]
        public void CanEstimateNormalsAsyncD()
        {
            var ps = new[]
            {
                new V3d(0, 0, 0),
                new V3d(1, 0, 0),
                new V3d(1, 1, 0),
                new V3d(0, 1, 0),
            };

            var kd = ps.BuildKdTreeAsync().Result;

            var ns = Normals.EstimateNormalsAsync(ps, 16, kd).Result;
            ClassicAssert.IsTrue(ns.Length == 4);
            ClassicAssert.IsTrue(ns.All(n => n == V3f.ZAxis));
        }



        [Test]
        public void CanEstimateNormalsWithoutKdTree()
        {
            var ps = new[]
            {
                new V3f(0, 0, 0),
                new V3f(1, 0, 0),
                new V3f(1, 1, 0),
                new V3f(0, 1, 0),
            };

            var ns = Normals.EstimateNormals(ps, 16);
            ClassicAssert.IsTrue(ns.Length == 4);
            ClassicAssert.IsTrue(ns.All(n => n == V3f.ZAxis));
        }

        [Test]
        public void CanEstimateNormalsWithoutKdTreeD()
        {
            var ps = new[]
            {
                new V3d(0, 0, 0),
                new V3d(1, 0, 0),
                new V3d(1, 1, 0),
                new V3d(0, 1, 0),
            };

            var ns = Normals.EstimateNormals(ps, 16);
            ClassicAssert.IsTrue(ns.Length == 4);
            ClassicAssert.IsTrue(ns.All(n => n == V3f.ZAxis));
        }



        [Test]
        public void CanEstimateNormalsWithoutKdTreeAsync()
        {
            var ps = new[]
            {
                new V3f(0, 0, 0),
                new V3f(1, 0, 0),
                new V3f(1, 1, 0),
                new V3f(0, 1, 0),
            };

            var ns = Normals.EstimateNormalsAsync(ps, 16).Result;
            ClassicAssert.IsTrue(ns.Length == 4);
            ClassicAssert.IsTrue(ns.All(n => n == V3f.ZAxis));
        }

        [Test]
        public void CanEstimateNormalsWithoutKdTreeAsyncD()
        {
            var ps = new[]
            {
                new V3d(0, 0, 0),
                new V3d(1, 0, 0),
                new V3d(1, 1, 0),
                new V3d(0, 1, 0),
            };

            var ns = Normals.EstimateNormalsAsync(ps, 16).Result;
            ClassicAssert.IsTrue(ns.Length == 4);
            ClassicAssert.IsTrue(ns.All(n => n == V3f.ZAxis));
        }
    }
}
