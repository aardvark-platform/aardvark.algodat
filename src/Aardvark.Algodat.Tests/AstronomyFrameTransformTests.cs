using Aardvark.Base;
using NUnit.Framework;
using System;

namespace Aardvark.Physics.Sky
{
    [TestFixture]
    public class AstronomyFrameTransformTests
    {
        // Offline eraGmst82(jd, 0) results, radians; UT1 input, not apparent sidereal time.
        // https://github.com/liberfa/erfa/blob/1a8044cde5b7763295d472a6443387239127c6c8/src/gmst82.c
        private static readonly (double jd, double gmst)[] MeanRotationFixtures =
        {
            (2305445, 4.8413096895730163), (2305445.25, 0.13322140676916661),
            (2305445.5, 1.7083184311444484), (2305445.875, 4.0709639677069163),
            (2415020, 4.8815280131055871), (2415020.25, 0.17343973057958806),
            (2415020.5, 1.7485367552330615), (2415020.875, 4.1111822922133285),
            (2451545, 4.8949612128230591), (2451545.25, 0.18687293038969582),
            (2451545.5, 1.76196995513592), (2451545.875, 4.1246154922552574),
            (2453736, 4.8871662395366968), (2453736.25, 0.17907795710889474),
            (2453736.5, 1.7541749818606718), (2453736.875, 4.1168205189883551),
            (2462502, 4.8903924174815714), (2462502.25, 0.18230413507599508),
            (2462502.5, 1.757401159850005), (2462502.875, 4.1200466970110341),
            (2488070, 4.9084079539684851), (2488070.25, 0.20031967162779551),
            (2488070.5, 1.7754166964666922), (2488070.875, 4.1380622337249235),
            (2597645, 4.9488293989192229), (2597645.25, 0.24074111685615662),
            (2597645.5, 1.8158381419735861), (2597645.875, 4.1784836796488207)
        };

        // At each boundary and at the representable single-JD values one second either side.
        private static readonly (double jd, double before, double at, double after)[] BoundaryFixtures =
        {
            (2415020, 4.8814550923425202, 4.8815280131055871, 4.8816009338686328),
            (2415020.5, 1.7484638344701295, 1.7485367552330615, 1.7486096759961072),
            (2451545, 4.8948882920600312, 4.8949612128230591, 4.8950341335860852),
            (2451545.5, 1.7618970343728932, 1.76196995513592, 1.7620428758989473),
            (2488070, 4.9083350332054181, 4.9084079539684851, 4.9084808747315307),
            (2488070.5, 1.7753437757036465, 1.7754166964666922, 1.7754896172296242)
        };

        // Reuse only the existing correction, without pinning or validating its astronomy model.
        private static double ExistingNutationAngle(double jd)
        {
            var n = Astronomy.BuildNutationTransform(jd);
            return Math.Atan(n.M01 / n.M00);
        }
        private static double MeanAngle(double jd)
        {
            var rotation = Astronomy.CEPtoITRF(jd, 0, 0);
            return Math.Atan2(rotation.M01, rotation.M00) - ExistingNutationAngle(jd);
        }
        private static double Wrap(double angle) => Math.Atan2(Math.Sin(angle), Math.Cos(angle));
        private static M33d PassiveZ(double angle)
        {
            var c = Math.Cos(angle);
            var s = Math.Sin(angle);
            return new M33d(c, s, 0, -s, c, 0, 0, 0, 1);
        }
        private static void AssertMatrix(M33d actual, M33d expected, string context)
        {
            for (var row = 0; row < 3; row++)
            for (var column = 0; column < 3; column++)
                Assert.That(Math.Abs(actual[row, column] - expected[row, column]), Is.LessThanOrEqualTo(2e-12),
                    $"{context}, matrix[{row},{column}]");
        }

        [Test]
        public void MeanRotationMatchesPinnedGmst82Fixtures()
        {
            Assert.Multiple(() =>
            {
                foreach (var (jd, gmst) in MeanRotationFixtures)
                {
                    var context = $"JD(UT1)={jd:R}";
                    // Allow expanded-polynomial versus ERFA Horner rounding over +/-400 years.
                    Assert.That(Math.Abs(Wrap(MeanAngle(jd) - gmst)), Is.LessThanOrEqualTo(2e-12), context + ", mean angle");
                    var actualMean = Astronomy.CEPtoITRF(jd, 0, 0) * PassiveZ(-ExistingNutationAngle(jd));
                    AssertMatrix(actualMean, PassiveZ(gmst), context + ", nutation removed");
                }
            });
        }

        [Test]
        public void MeanRotationRemainsContinuousAcrossJulianNoonAndCivilMidnight()
        {
            Assert.Multiple(() =>
            {
                foreach (var (jd, before, at, after) in BoundaryFixtures)
                {
                    var context = $"boundary JD(UT1)={jd:R}";
                    var actualBefore = MeanAngle(jd - 1.0 / 86400);
                    var actualAt = MeanAngle(jd);
                    var actualAfter = MeanAngle(jd + 1.0 / 86400);
                    Assert.That(Math.Abs(Wrap(actualAfter - actualBefore)), Is.LessThan(0.0002), context + ", no phase jump");
                    Assert.That(Math.Abs(Wrap((actualAt - actualBefore) - (at - before))), Is.LessThanOrEqualTo(1e-12), context + ", step to boundary");
                    Assert.That(Math.Abs(Wrap((actualAfter - actualAt) - (after - at))), Is.LessThanOrEqualTo(1e-12), context + ", step from boundary");
                }
            });
        }

        [Test]
        public void PolarMotionPreservesSignedCompositionAndTheExistingNutationCorrection()
        {
            var poles = new[] { (0.0, 0.0), (2e-6, 0.0), (0.0, -3e-6), (1.2e-6, -0.8e-6), (0.27, -0.41), (-0.37, 0.19) };
            var vectors = new[] { V3d.IOO, V3d.OIO, V3d.OOI, new V3d(0.3, -0.7, 1.1) };
            Assert.Multiple(() =>
            {
                foreach (var (jd, gmst) in MeanRotationFixtures)
                foreach (var (xp, yp) in poles)
                {
                    var actual = Astronomy.CEPtoITRF(jd, xp, yp);
                    var angle = gmst + ExistingNutationAngle(jd);
                    foreach (var v in vectors)
                    {
                        // Independently apply passive Z(angle), then Y(-yp), then X(-xp).
                        var z = new V3d(Math.Cos(angle) * v.X + Math.Sin(angle) * v.Y,
                            -Math.Sin(angle) * v.X + Math.Cos(angle) * v.Y, v.Z);
                        var y = new V3d(Math.Cos(yp) * z.X + Math.Sin(yp) * z.Z, z.Y,
                            -Math.Sin(yp) * z.X + Math.Cos(yp) * z.Z);
                        var expected = new V3d(y.X, Math.Cos(xp) * y.Y - Math.Sin(xp) * y.Z,
                            Math.Sin(xp) * y.Y + Math.Cos(xp) * y.Z);
                        var transformed = actual * v;
                        var context = $"JD(UT1)={jd:R}, xp={xp:R}, yp={yp:R}, vector={v}";
                        for (var axis = 0; axis < 3; axis++)
                            Assert.That(Math.Abs(transformed[axis] - expected[axis]), Is.LessThanOrEqualTo(2e-12), context + $", axis={axis}");
                        Assert.That(Math.Abs(transformed.LengthSquared - v.LengthSquared), Is.LessThanOrEqualTo(3e-15), context + ", length");
                    }
                }
            });
        }
    }
}
