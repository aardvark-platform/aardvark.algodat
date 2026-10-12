using NUnit.Framework;
using System;

namespace Aardvark.Physics.Sky
{
    [TestFixture]
    public class TrueAnomalyTests
    {
        private static readonly double[] HyperbolicEccentricities = { 1.01, 1.2, 1.5, 2, 3 };
        private static readonly double[] PositiveMeanAnomalies = { 0.1, 1, 3 };

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
                    var reducedMean = mean - 2 * Math.PI * Math.Round(mean / (2 * Math.PI));
                    var eccentric = Bisect(x => x - e * Math.Sin(x) - reducedMean, -Math.PI, Math.PI, context);
                    var expected = Math.Atan2(Math.Sqrt(1 - e * e) * Math.Sin(eccentric), Math.Cos(eccentric) - e);
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
