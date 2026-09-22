using Aardvark.Base;
using Aardvark.Physics.Sky;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aardvark.Physics.Sky.Tests
{
    [TestFixture]
    public class NutationTests
    {
        // Offline outputs from unmodified ERFA revision 1a8044cde5b7763295d472a6443387239127c6c8:
        // https://github.com/liberfa/erfa/blob/1a8044cde5b7763295d472a6443387239127c6c8/src/nut80.c
        // eraNut80(jd, 0, &psi, &epsilon), with eraObl06(jd, 0) for the existing AA2010 obliquity.
        // These fixtures neither read nor reproduce the production coefficient table.
        // Whole-century offsets span -4 to +4; the fractional dates also exercise both sides of J2000.
        private static readonly (double Jd, double Psi, double Epsilon, double Obliquity)[] References =
        {
            (2305445.0, 7.1049783095331585e-05, 2.0359419571645012e-05, 0.41000024853719369),
            (2378495.0, -4.11884259535093e-05, 3.4656749384294475e-05, 0.40954666143760515),
            (2415020.0, 8.4063802103853712e-05, -1.1127148444141159e-05, 0.40931966106145129),
            (2433282.5, -1.6025705416766619e-05, 4.0317701745728347e-05, 0.40920613469634765),
            (2446895.5, -1.8363890684754518e-05, 4.5778364005102591e-05, 0.40912150588886237),
            (2450123.7, 3.5448798339711806e-05, -4.1393382239689196e-05, 0.40910143663160609),
            (2451179.75, -4.7224352955476797e-05, -3.9491699260567403e-05, 0.40909487131112343),
            (2451544.5, -6.7501478376485387e-05, -2.7944643634682161e-05, 0.40909260370901634),
            (2451545.0, -6.7502476175324775e-05, -2.7992212383770132e-05, 0.40909260060058289),
            (2451545.5, -6.7464266927358922e-05, -2.8042824512127437e-05, 0.40909259749214943),
            (2451910.25, -7.8120320250867564e-05, -1.4019780146145167e-05, 0.40909032988986482),
            (2453736.5, -9.6436583532266852e-06, 4.0600510068797106e-05, 0.40907897633565105),
            (2462502.5, 8.4569836716380368e-05, -6.576198196394667e-06, 0.40902447946374126),
            (2469807.5, 7.3530450850939355e-05, -2.5849577175475714e-05, 0.40897906606062212),
            (2488070.0, 1.5841380151871319e-05, 4.1589583799188894e-05, 0.40886553835874173),
            (2524595.0, 5.4316257901885869e-05, -3.8691910160248668e-05, 0.40863853257264893),
            (2597645.0, 6.6694401230189923e-05, 2.5881903410037339e-05, 0.40818492282799518),
        };

        private static IEnumerable<TestCaseData> AngleCases()
        {
            foreach (var r in References) yield return new TestCaseData(r.Jd, r.Psi, r.Epsilon);
        }

        private static IEnumerable<TestCaseData> TransformCases()
        {
            foreach (var r in References) yield return new TestCaseData(r.Jd, r.Psi, r.Epsilon, r.Obliquity);
        }

        [TestCaseSource(nameof(AngleCases))]
        public void AnglesMatchIndependentErfaValues(double jd, double psi, double epsilon)
        {
            var actual = Astronomy.CalcNutation(jd);
            Assert.Multiple(() =>
            {
                Assert.That(actual.Item1, Is.EqualTo(psi).Within(1e-13), "longitude in radians");
                Assert.That(actual.Item2, Is.EqualTo(epsilon).Within(1e-13), "obliquity in radians");
            });
        }

        [Test]
        public void PublishedErfaValidationExample()
        {
            // ERFA's own t_nut80 example (MJD 53736), not computed by this fixture.
            AnglesMatchIndependentErfaValues(2453736.5, -0.9643658353226563966e-5, 0.4060051006879713322e-4);
        }

        [TestCaseSource(nameof(TransformCases))]
        public void NutationTransformUsesReferenceAnglesAndPreservesRotations(double jd, double psi, double epsilon, double obliquity)
        {
            var actual = Astronomy.BuildNutationTransform(jd);
            AssertMatrix(actual, ReferenceNutationMatrix(psi, epsilon, obliquity), 1e-13);
            AssertProperRotation(actual);
        }

        [TestCaseSource(nameof(TransformCases))]
        public void IcrfToCepAppliesNutationAfterPrecession(double jd, double psi, double epsilon, double obliquity)
        {
            var precession = Astronomy.BuildPrecessionTransform(jd);
            var expected = ReferenceNutationMatrix(psi, epsilon, obliquity) * precession;
            var actual = Astronomy.ICRFtoCEP(jd);
            AssertMatrix(actual, expected, 1e-13);
            AssertProperRotation(actual);
        }

        // Independent full-matrix goldens: eraNumat(eraObl06(jd, 0), psi, epsilon) * eraPmat76(jd, 0).
        // Deliberately not eraPnm80: production keeps its AA2010 mean obliquity, not IAU1980 obliquity.
        private static IEnumerable<TestCaseData> CombinedMatrixCases()
        {
            yield return new TestCaseData(2415020.0, new M33d(
                0.99970495641151413, 0.022275639993066323, 0.0096848329439478773,
                -0.022275747945018187, 0.99975186006093664, -9.6737725161599313e-05,
                -0.0096845846448307751, -0.00011902771423291303, 0.99995309622634809));
            yield return new TestCaseData(2451545.0, new M33d(
                0.99999999772170789, 6.1932316456514099e-05, 2.6850930360225168e-05,
                -6.193306804919538e-05, 0.99999999769038883, 2.7991380899927698e-05,
                -2.6849196727150012e-05, -2.7993043796581407e-05, 0.99999999924775507));
            yield return new TestCaseData(2453736.5, new M33d(
                0.99999894398716005, -0.0013328792725020413, -0.00057918685226686936,
                0.0013328557561229448, 0.99999911090742521, -4.0986494654659373e-05,
                0.00057924096736531715, 4.0214478842539525e-05, 0.99999983143133442));
            yield return new TestCaseData(2462502.5, new M33d(
                0.99997262322178448, -0.0067866575458421863, -0.0029485736040422042,
                0.0067866769338411738, 0.99997697023702958, -3.4302130340917983e-06,
                0.0029485289787721746, -1.6580897340316003e-05, 0.99999565294151904));
            yield return new TestCaseData(2488070.0, new M33d(
                0.99970226197697776, -0.022380806254848472, -0.0097204377214853647,
                0.022380401783334357, 0.99974951612710239, -0.00015039827507104585,
                0.0097213689432540593, -6.7193805930699325e-05, 0.99995274411887169));
        }

        [TestCaseSource(nameof(CombinedMatrixCases))]
        public void IcrfToCepMatchesIndependentFullMatrices(double jd, M33d expected)
            => AssertMatrix(Astronomy.ICRFtoCEP(jd), expected, 1e-13);

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void NonFiniteDatesPreserveNaNAngles(double jd)
        {
            var (psi, epsilon) = Astronomy.CalcNutation(jd);
            Assert.That(double.IsNaN(psi) && double.IsNaN(epsilon), Is.True);
        }

        [Test]
        public void RepeatedAndConcurrentCallsHaveNoSharedEvaluationState()
        {
            var previous = new (double, double)[References.Length];
            for (var i = 0; i < previous.Length; i++) previous[i] = Astronomy.CalcNutation(References[i].Jd);
            Parallel.For(0, 64, new ParallelOptions { MaxDegreeOfParallelism = 4 }, pass =>
            {
                for (var i = 0; i < References.Length; i++)
                {
                    var index = (i + pass) % References.Length;
                    var r = References[index];
                    var actual = Astronomy.CalcNutation(r.Jd);
                    Assert.That(actual, Is.EqualTo(previous[index]));
                    Assert.That(actual.Item1, Is.EqualTo(r.Psi).Within(1e-13));
                    Assert.That(actual.Item2, Is.EqualTo(r.Epsilon).Within(1e-13));
                }
            });
        }

        [Test]
        public void WarmedEvaluationAllocatesNothing(
            [Values(0, 1, 2)] int operation, [Values(1, 37, 4096)] int count)
        {
            Evaluate(operation, 4096); // initialize the type and warm the code outside measurement
            var before = GC.GetAllocatedBytesForCurrentThread();
            var checksum = Evaluate(operation, count);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(double.IsFinite(checksum), Is.True);
            Assert.That(allocated, Is.Zero, "steady-state managed allocation, not a timing assertion");
        }

        private static double Evaluate(int operation, int count)
        {
            var sum = 0.0;
            for (var i = 0; i < count; i++)
            {
                var jd = References[i % References.Length].Jd;
                if (operation == 0)
                {
                    var (psi, epsilon) = Astronomy.CalcNutation(jd);
                    sum += psi + 3 * epsilon;
                }
                else
                {
                    var m = operation == 1 ? Astronomy.BuildNutationTransform(jd) : Astronomy.ICRFtoCEP(jd);
                    sum += m.M00 + 2 * m.M01 + 3 * m.M02 + 4 * m.M10 + 5 * m.M11
                         + 6 * m.M12 + 7 * m.M20 + 8 * m.M21 + 9 * m.M22;
                }
            }
            return sum;
        }

        private static M33d ReferenceNutationMatrix(double psi, double epsilon, double obliquity)
        {
            // Expanded passive-rotation components, independent of production's rotation helpers.
            var cp = Math.Cos(psi); var sp = Math.Sin(psi);
            var ce = Math.Cos(obliquity); var se = Math.Sin(obliquity);
            var ct = Math.Cos(obliquity + epsilon); var st = Math.Sin(obliquity + epsilon);
            return new M33d(cp, -sp * ce, -sp * se,
                           ct * sp, ct * cp * ce + st * se, ct * cp * se - st * ce,
                           st * sp, st * cp * ce - ct * se, st * cp * se + ct * ce);
        }

        private static void AssertProperRotation(M33d m)
        {
            AssertMatrix(m * m.Transposed, M33d.Identity, 5e-15);
            Assert.That(m.Determinant, Is.EqualTo(1.0).Within(5e-15));
            var v = new V3d(2, -3, 7);
            Assert.That((m * v).LengthSquared, Is.EqualTo(v.LengthSquared).Within(1e-13));
            Assert.That(Vec.Cross(m.C0, m.C1), Is.EqualTo(m.C2).Using<V3d>((a, b) => (a - b).Length < 5e-15));
        }

        private static void AssertMatrix(M33d actual, M33d expected, double tolerance)
        {
            for (var row = 0; row < 3; row++)
                for (var column = 0; column < 3; column++)
                    Assert.That(actual[row, column], Is.EqualTo(expected[row, column]).Within(tolerance), $"[{row},{column}]");
        }
    }
}
