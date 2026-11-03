using NUnit.Framework;
using System;

namespace Aardvark.Physics.Sky
{
    [TestFixture]
    public class TrueAnomalyTests
    {
        private static readonly double[] HyperbolicEccentricities = { 1.01, 1.2, 1.5, 2, 3 };
        private static readonly double[] PositiveMeanAnomalies = { 0.1, 1, 3 };
        private static readonly double[] NearlyParabolicEccentricities =
            { 0.9999999, 0.99999995, 0.99999999, 0.999999995, 0.999999999, 0.9999999999 };
        private static readonly double[] EllipticRegressionMagnitudes = { 0.01, 0.1, 1, 3 };

        // Monotone Kepler equations, solved without the production Newton iteration or seed.
        private static double Bisect(Func<double, double> residual, double low, double high, string context)
        {
            Assert.That(residual(low), Is.LessThanOrEqualTo(0), context + ", lower bracket");
            Assert.That(residual(high), Is.GreaterThanOrEqualTo(0), context + ", upper bracket");
            for (var i = 0; i < 80; i++)
            {
                var middle = (low + high) / 2;
                if (middle == low || middle == high) break;
                var value = residual(middle);
                if (value == 0) return middle;
                if (value < 0) low = middle;
                else high = middle;
            }
            return (low + high) / 2;
        }

        private static double EllipticReference(double mean, double e, string context)
        {
            var reduced = mean - 2 * Math.PI * Math.Round(mean / (2 * Math.PI));
            var eccentric = Bisect(x => x - e * Math.Sin(x) - reduced, -Math.PI, Math.PI, context);
            // Full-angle conversion; the product avoids cancellation in 1 - e*e near e=1.
            var expected = Math.Atan2(Math.Sqrt((1 - e) * (1 + e)) * Math.Sin(eccentric), Math.Cos(eccentric) - e);
            Assert.That(double.IsFinite(expected), Is.True, context + ", finite reference");
            return expected;
        }

        [Test]
        public void NearlyParabolicEllipticAnomaliesMatchStableRootsAndOddSymmetry()
        {
            Assert.Multiple(() =>
            {
                foreach (var e in NearlyParabolicEccentricities)
                foreach (var magnitude in EllipticRegressionMagnitudes)
                {
                    var positive = Astronomy.CalculateTrueAnomaly(magnitude, e, 1);
                    var negative = Astronomy.CalculateTrueAnomaly(-magnitude, e, 1);
                    var context = $"e={e:R}, M=+/-{magnitude:R}";
                    Assert.That(BitConverter.DoubleToInt64Bits(negative),
                        Is.EqualTo(BitConverter.DoubleToInt64Bits(-positive)), context + ", exact odd symmetry");
                    foreach (var (mean, actual) in new[] { (magnitude, positive), (-magnitude, negative) })
                    {
                        var where = $"e={e:R}, M={mean:R}";
                        var expected = EllipticReference(mean, e, where);
                        Assert.That(double.IsFinite(actual), Is.True, where + ", finite result");
                        if (!double.IsFinite(actual)) continue;
                        Assert.That(actual, Is.EqualTo(expected).Within(2e-12), where + ", stable full-angle reference");
                        Assert.That(actual * mean, Is.GreaterThan(0), where + ", sign");
                        Assert.That(Math.Abs(actual), Is.LessThan(Math.PI), where + ", elliptic range");
                    }
                }
            });
        }

        [Test]
        public void WrappedEllipticAnomaliesUseTheirReducedSignAndKeepZeroBehavior()
        {
            Assert.Multiple(() =>
            {
                foreach (var e in NearlyParabolicEccentricities)
                {
                    var zero = Astronomy.CalculateTrueAnomaly(0, e, 1);
                    Assert.That(double.IsFinite(zero), Is.True, $"e={e:R}, zero finite");
                    foreach (var turns in new[] { -8, -2, -1, 0, 1, 2, 8 })
                    {
                        foreach (var signedZero in new[] { -0.0, 0.0 })
                        {
                            var mean = turns == 0 ? signedZero : turns * 2 * Math.PI;
                            Assert.That(BitConverter.DoubleToInt64Bits(Astronomy.CalculateTrueAnomaly(mean, e, 1)),
                                Is.EqualTo(BitConverter.DoubleToInt64Bits(zero)), $"e={e:R}, turns={turns}, reduced zero");
                        }
                        foreach (var magnitude in EllipticRegressionMagnitudes)
                        foreach (var sign in new[] { -1, 1 })
                        {
                            var mean = sign * magnitude + turns * 2 * Math.PI;
                            var reduced = mean - 2 * Math.PI * Math.Round(mean / (2 * Math.PI));
                            var context = $"e={e:R}, magnitude={magnitude:R}, sign={sign}, turns={turns}, M={mean:R}";
                            var actual = Astronomy.CalculateTrueAnomaly(mean, e, 1);
                            var opposite = Astronomy.CalculateTrueAnomaly(-mean, e, 1);
                            var expected = EllipticReference(mean, e, context);
                            Assert.That(double.IsFinite(actual), Is.True, context + ", finite result");
                            Assert.That(double.IsFinite(opposite), Is.True, context + ", finite opposite");
                            if (!double.IsFinite(actual) || !double.IsFinite(opposite)) continue;
                            Assert.That(actual, Is.EqualTo(expected).Within(2e-12), context + ", wrapped full-angle reference");
                            Assert.That(actual * reduced, Is.GreaterThan(0), context + ", reduced sign");
                            Assert.That(BitConverter.DoubleToInt64Bits(opposite),
                                Is.EqualTo(BitConverter.DoubleToInt64Bits(-actual)), context + ", wrapped odd symmetry");
                        }
                    }
                }
            });
        }

        [Test]
        public void HyperbolicAnomaliesMatchBracketedRootsAndKeplerResiduals()
        {
            Assert.Multiple(() =>
            {
                foreach (var e in HyperbolicEccentricities)
                foreach (var magnitude in PositiveMeanAnomalies)
                {
                    var positive = Astronomy.CalculateTrueAnomaly(magnitude, e, 1);
                    var negative = Astronomy.CalculateTrueAnomaly(-magnitude, e, 1);
                    Assert.That(Math.Abs(positive + negative) <= 2e-12, Is.True,
                        $"e={e:R}, M=+/-{magnitude:R}, odd symmetry");
                    foreach (var (mean, actual) in new[] { (magnitude, positive), (-magnitude, negative) })
                    {
                        var context = $"e={e:R}, M={mean:R}";
                        Assert.That(double.IsFinite(actual), Is.True, context + ", finite result");
                        if (!double.IsFinite(actual)) continue;
                        var h = Bisect(x => e * Math.Sinh(x) - x - mean, -4, 4, context);
                        // Independent full-angle conversion, rather than the production half-angle formula.
                        var expected = Math.Atan2(Math.Sqrt(e * e - 1) * Math.Sinh(h), e - Math.Cosh(h));
                        Assert.That(Math.Abs(actual - expected), Is.LessThanOrEqualTo(2e-12), context + ", true anomaly");
                        Assert.That(actual * mean, Is.GreaterThan(0), context + ", sign");
                        Assert.That(Math.Abs(actual), Is.LessThan(Math.Acos(-1 / e)), context + ", hyperbolic asymptote");

                        var inverseArgument = Math.Sqrt((e - 1) / (e + 1)) * Math.Tan(actual / 2);
                        Assert.That(Math.Abs(inverseArgument) < 1, Is.True, context + ", finite inverse anomaly");
                        if (!(Math.Abs(inverseArgument) < 1)) continue;
                        var recoveredH = 2 * Math.Atanh(inverseArgument);
                        Assert.That(Math.Abs(e * Math.Sinh(recoveredH) - recoveredH - mean),
                            Is.LessThanOrEqualTo(2e-12 * (1 + Math.Abs(mean))), context + ", Kepler residual");
                    }
                }
                Assert.That(Astronomy.CalculateTrueAnomaly(1, 1.5, 1),
                    Is.EqualTo(1.727196007387909).Within(2e-12), "e=1.5, M=1, reference example");
            });
        }

        [Test]
        public void ZeroAndNonHyperbolicControlsKeepTheirExistingBehavior()
        {
            Assert.Multiple(() =>
            {
                foreach (var e in new[] { 0.0, 0.01671, 0.25, 0.75, 0.95, 1.01, 1.2, 1.5, 2, 3 })
                    Assert.That(Astronomy.CalculateTrueAnomaly(0, e, 1), Is.EqualTo(0).Within(1e-15), $"e={e:R}, zero mean anomaly");

                foreach (var mean in new[] { -12.5, -3.0, -0.1, -0.0, 0.0, 0.1, 3, 12.5 })
                    Assert.That(BitConverter.DoubleToInt64Bits(Astronomy.CalculateTrueAnomaly(mean, 0, 1)),
                        Is.EqualTo(BitConverter.DoubleToInt64Bits(mean)), $"circular e=0, M={mean:R}, exact passthrough");

                foreach (var e in new[] { 0.01671, 0.25, 0.75, 0.95 })
                foreach (var mean in new[] { -3.0, -1, -0.1, 0.1, 1, 3, 4 * Math.PI + 1, -4 * Math.PI - 1 })
                {
                    var context = $"elliptic e={e:R}, M={mean:R}";
                    var expected = EllipticReference(mean, e, context);
                    var actual = Astronomy.CalculateTrueAnomaly(mean, e, 1);
                    Assert.That(Math.Abs(actual - expected) <= 2e-12, Is.True, context + ", true anomaly");
                }

                // Compatibility only: this fix does not redesign the existing e=1 branch.
                Assert.That(Astronomy.CalculateTrueAnomaly(1, 1, 1), Is.EqualTo(Math.PI), "parabolic positive-anomaly control");
                foreach (var mean in new[] { -1.0, 0.0 })
                    Assert.That(Astronomy.CalculateTrueAnomaly(mean, 1, 1), Is.NaN, $"parabolic M={mean:R}, existing NaN");
            });
        }
    }
}
