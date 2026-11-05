using Aardvark.Base;
using Aardvark.Physics.Sky;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;

namespace Aardvark.Physics.Sky
{
    [TestFixture]
    public class SkyTests
    {
        [Test]
        public void SignedCelestialPolesAndNearbyDeclinationsMatchCartesianRotation()
        {
            Assert.Multiple(() =>
            {
                // Reported cancellation/roundoff case, with a finite south-pole reference.
                CheckAngle(Astronomy.Declination(-1.5707963267948966, -1.1692606430933252, 0.40153568370157144),
                    -Math.PI / 2, 3e-8, "reported south celestial pole");
                foreach (var epsilon in new[] { 0.1, 0.2, 0.40153568370157144, 0.4090928042223289, 0.7, 1.1 })
                foreach (var sign in new[] { -1, 1 })
                foreach (var offset in new[] { -0.01, -0.00001, 0.0, 0.00001, 0.01 })
                {
                    var lambda = sign * Math.PI / 2;
                    var beta = sign * (Math.PI / 2 - epsilon) + offset;
                    var q = EquatorialVector(lambda, beta, epsilon);
                    var expected = Math.Atan2(q.Z, Math.Sqrt(q.X * q.X + q.Y * q.Y));
                    CheckAngle(Astronomy.Declination(lambda, beta, epsilon), expected, 3e-8,
                        $"epsilon={epsilon:R}, sign={sign}, beta offset={offset:R}");
                }
            });
        }

        [Test]
        public void ObserverZenithNadirAndNearbyHeightsMatchCartesianRotations()
        {
            Assert.Multiple(() =>
            {
                foreach (var jd in new[] { 2451545.0, 2453097.0, 2460000.5 })
                foreach (var direction in new[] { (0.3, 0.0), (1.5707963267948966, 0.0), (-0.7, 0.4), (2.0, -0.6) })
                foreach (var nadir in new[] { false, true })
                {
                    var q = EquatorialVector(direction.Item1, direction.Item2, Astronomy.GetEarthMeanObliquityAA2010(jd));
                    var alpha = Math.Atan2(q.Y, q.X);
                    var delta = Math.Atan2(q.Z, Math.Sqrt(q.X * q.X + q.Y * q.Y));
                    var longitude = Math.IEEERemainder((alpha + (nadir ? Math.PI : 0) - Astronomy.SideralTime(jd)) * Constant.DegreesPerRadian, 360);
                    var latitude = (nadir ? -delta : delta) * Constant.DegreesPerRadian;
                    foreach (var offset in new[] { (0.0, 0.0), (-0.001, 0.0), (0.001, 0.0), (0.0, -0.001), (0.0, 0.001) })
                    {
                        var lon = longitude + offset.Item1;
                        var lat = latitude + offset.Item2;
                        var expected = ObserverReference(direction.Item1, direction.Item2, jd, lon, lat);
                        var actual = Astronomy.GeocentricToObserver(direction.Item1, direction.Item2, jd, lon, lat);
                        var context = $"jd={jd:R}, lambda={direction.Item1:R}, beta={direction.Item2:R}, lon={lon:R}, lat={lat:R}, nadir={nadir}";
                        CheckAngle(actual.Item1, expected.Height, 3e-8, context);
                        // Compatibility, not a geometrically defined azimuth at a vertical direction.
                        SameBits(actual.Item2, LegacyObserver(direction.Item1, direction.Item2, jd, lon, lat).Azimuth, context + ", retained azimuth calculation");
                    }
                }
            });
        }

        [Test]
        public void OrdinaryDirectionsKeepLegacyBitsAndNonfiniteInputsPropagateNaN()
        {
            foreach (var lambda in new[] { -3.0, -1, 0, 0.7, 2.4 })
            foreach (var beta in new[] { -1.0, -0.4, 0, 0.5, 1 })
            foreach (var epsilon in new[] { 0.0, 0.40153568370157144, 0.4090928042223289, 0.7 })
            {
                var context = $"lambda={lambda:R}, beta={beta:R}, epsilon={epsilon:R}";
                var q = EquatorialVector(lambda, beta, epsilon);
                var actual = Astronomy.Declination(lambda, beta, epsilon);
                CheckAngle(actual, Math.Atan2(q.Z, Math.Sqrt(q.X * q.X + q.Y * q.Y)), 2e-12, context);
                SameBits(actual, LegacyDeclination(lambda, beta, epsilon), context + ", legacy bits");
            }
            foreach (var lambda in new[] { -3.0, -0.7, 0.3, 1.9 })
            foreach (var beta in new[] { -0.6, 0.0, 0.4 })
            foreach (var jd in new[] { 2451545.0, 2453097.0, 2460000.5 })
            foreach (var longitude in new[] { -120.0, 0, 16.37 })
            foreach (var latitude in new[] { -60.0, 0, 48.21 })
            {
                var context = $"lambda={lambda:R}, beta={beta:R}, jd={jd:R}, lon={longitude:R}, lat={latitude:R}";
                var actual = Astronomy.GeocentricToObserver(lambda, beta, jd, longitude, latitude);
                var legacy = LegacyObserver(lambda, beta, jd, longitude, latitude);
                var expected = ObserverReference(lambda, beta, jd, longitude, latitude);
                SameBits(actual.Item1, legacy.Height, context + ", height bits");
                SameBits(actual.Item2, legacy.Azimuth, context + ", azimuth bits");
                CheckAngle(actual.Item1, expected.Height, 2e-10, context + ", height reference");
                Assert.That(double.IsFinite(expected.Azimuth) && double.IsFinite(actual.Item2), Is.True, context);
                Assert.That(Math.Abs(Math.IEEERemainder(actual.Item2 - expected.Azimuth, 2 * Math.PI)), Is.LessThanOrEqualTo(2e-10), context + ", azimuth reference");
            }
            foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                foreach (var angles in new[] { (invalid, 0.2, 0.4), (0.3, invalid, 0.4), (0.3, 0.2, invalid) })
                    Assert.That(Astronomy.Declination(angles.Item1, angles.Item2, angles.Item3), Is.NaN, $"declination nonfinite={invalid}");
                for (var parameter = 0; parameter < 5; parameter++)
                {
                    var input = new[] { 0.3, 0.2, 2451545, 16.37, 48.21 };
                    input[parameter] = invalid;
                    var actual = Astronomy.GeocentricToObserver(input[0], input[1], input[2], input[3], input[4]);
                    Assert.That(actual.Item1, Is.NaN, $"observer parameter={parameter}, nonfinite={invalid}, height");
                    Assert.That(actual.Item2, Is.NaN, $"observer parameter={parameter}, nonfinite={invalid}, azimuth");
                }
            }
        }

        [Test]
        public void SolarZenithAndNadirKeepFiniteZenithAnglesAndDistances()
        {
            Assert.Multiple(() =>
            {
                // Solar zenith reproduction: the original altitude sine rounds above one.
                const double reproductionJd = 2453102.0;
                const double reproductionLongitude = 0.5819315366679803;
                const double reproductionLatitude = 6.666810133493239;
                foreach (var nadir in new[] { false, true })
                {
                    var longitude = reproductionLongitude - (nadir ? 180 : 0);
                    var latitude = nadir ? -reproductionLatitude : reproductionLatitude;
                    var (solar, distance) = SunPosition.Compute(reproductionJd, longitude, latitude);
                    CheckAngle(solar.Theta, nadir ? Math.PI : 0, 3e-8, $"solar reproduction, nadir={nadir}");
                    Assert.That(double.IsFinite(distance) && distance > 0, Is.True, "solar reproduction distance");
                }
                foreach (var jd in new[] { 2451545.0, 2453097.0, 2455197.5, 2459215.5, 2460000.5, 2462502.5 })
                {
                    var mean = Astronomy.GetMeanAnomaly(Planet.Earth, jd);
                    var anomaly = Astronomy.ApproximateTrueAnomaly(Planet.Earth, mean);
                    var lambda = anomaly + (Astronomy.GetPerihelion(Planet.Earth) + 180) * Constant.RadiansPerDegree;
                    var q = EquatorialVector(lambda, 0, Astronomy.GetEarthMeanObliquityAA2010(jd));
                    var alpha = Math.Atan2(q.Y, q.X);
                    var delta = Math.Atan2(q.Z, Math.Sqrt(q.X * q.X + q.Y * q.Y));
                    foreach (var nadir in new[] { false, true })
                    {
                        var longitude = Math.IEEERemainder((alpha + (nadir ? Math.PI : 0) - Astronomy.SideralTime(jd)) * Constant.DegreesPerRadian, 360);
                        var latitude = (nadir ? -delta : delta) * Constant.DegreesPerRadian;
                        var context = $"solar jd={jd:R}, lon={longitude:R}, lat={latitude:R}, nadir={nadir}";
                        var (actual, distance) = SunPosition.Compute(jd, longitude, latitude);
                        var expected = ObserverReference(lambda, 0, jd, longitude, latitude);
                        CheckAngle(actual.Theta, Math.PI / 2 - expected.Height, 3e-8, context);
                        Assert.That(actual.Theta, Is.InRange(0, Math.PI), context);
                        Assert.That(double.IsFinite(distance) && distance > 0, Is.True, context + ", distance finite");
                        SameBits(distance, Astronomy.GetDistanceToTheSun(Planet.Earth, anomaly) * Astronomy.AU, context + ", distance unchanged");
                        SameBits(actual.Phi, Astronomy.GeocentricToObserver(lambda, 0, jd, longitude, latitude).Item2, context + ", azimuth forwarding only");
                        if (jd == 2453097.0)
                        {
                            var (utc, _) = SunPosition.Compute(new DateTime(2004, 4, 1, 12, 0, 0), 0, longitude, latitude);
                            var (local, _) = SunPosition.Compute(new DateTime(2004, 4, 1, 14, 0, 0), 2, longitude, latitude);
                            SameBits(utc.Theta, actual.Theta, context + ", UTC overload");
                            SameBits(local.Theta, actual.Theta, context + ", fixed timezone overload");
                        }
                    }
                }
            });
        }

        private static V3d EquatorialVector(double lambda, double beta, double epsilon)
        {
            var x = Math.Cos(beta) * Math.Cos(lambda);
            var y = Math.Cos(beta) * Math.Sin(lambda);
            var z = Math.Sin(beta);
            return new V3d(x, y * Math.Cos(epsilon) - z * Math.Sin(epsilon), y * Math.Sin(epsilon) + z * Math.Cos(epsilon));
        }

        private static (double Height, double Azimuth) ObserverReference(double lambda, double beta, double jd, double longitude, double latitude)
        {
            var q = EquatorialVector(lambda, beta, Astronomy.GetEarthMeanObliquityAA2010(jd));
            var theta = Astronomy.SideralTime(jd) + longitude * Constant.RadiansPerDegree;
            var phi = latitude * Constant.RadiansPerDegree;
            var meridian = q.X * Math.Cos(theta) + q.Y * Math.Sin(theta);
            var west = q.X * Math.Sin(theta) - q.Y * Math.Cos(theta);
            var south = meridian * Math.Sin(phi) - q.Z * Math.Cos(phi);
            var up = meridian * Math.Cos(phi) + q.Z * Math.Sin(phi);
            return (Math.Atan2(up, Math.Sqrt(south * south + west * west)), Math.Atan2(west, south));
        }

        private static double LegacyDeclination(double lambda, double beta, double epsilon)
            => Math.Asin(Math.Sin(beta) * Math.Cos(epsilon) + Math.Cos(beta) * Math.Sin(epsilon) * Math.Sin(lambda));

        private static (double Height, double Azimuth) LegacyObserver(double lambda, double beta, double jd, double longitude, double latitude)
        {
            var epsilon = Astronomy.GetEarthMeanObliquityAA2010(jd);
            var delta = LegacyDeclination(lambda, beta, epsilon);
            var alpha = Math.Atan2(Math.Sin(lambda) * Math.Cos(epsilon) - Math.Tan(beta) * Math.Sin(epsilon), Math.Cos(lambda));
            var theta = Astronomy.SideralTime(jd) - (-longitude * Constant.RadiansPerDegree);
            var h = theta - alpha;
            var phi = latitude * Constant.RadiansPerDegree;
            return (Math.Asin(Math.Sin(phi) * Math.Sin(delta) + Math.Cos(phi) * Math.Cos(delta) * Math.Cos(h)),
                Math.Atan2(Math.Sin(h), Math.Cos(h) * Math.Sin(phi) - Math.Tan(delta) * Math.Cos(phi)));
        }

        private static void CheckAngle(double actual, double expected, double tolerance, string context)
        {
            Assert.That(double.IsFinite(expected), Is.True, context + ", finite reference");
            Assert.That(double.IsFinite(actual), Is.True, context + ", finite result");
            if (double.IsFinite(actual)) Assert.That(actual, Is.EqualTo(expected).Within(tolerance), context);
        }

        private static void SameBits(double actual, double expected, string context)
            => Assert.That(BitConverter.DoubleToInt64Bits(actual), Is.EqualTo(BitConverter.DoubleToInt64Bits(expected)), context);

        [Test]
        public void AnomalyTest()
        {
            // example from https://www.aa.quae.nl/en/reken/zonpositie.html "3. The Equation of Center"
            TestAnomalyCalc(Planet.Earth, 87.1807 * Constant.RadiansPerDegree);
            TestAnomalyCalc(Planet.Mars, 112.6531 * Constant.RadiansPerDegree);

            var rnd = new Random();
            for(int i = 0; i < 10000; i++)
            {
                var p = rnd.Next(9);

                var M = rnd.NextDouble() * Constant.PiTimesTwo;

                TestAnomalyCalc((Planet)p, M);
            }
        }

        static void TestAnomalyCalc(Planet p, double M)
        {
            var trueAnomalyApprox = Astronomy.ApproximateTrueAnomaly(p, M) * Constant.DegreesPerRadian;

            var a = Astronomy.OrbitParameters[(int)p].Item1;
            var e = Astronomy.OrbitParameters[(int)p].Item2;

            var trueAnomalyCalc = Astronomy.CalculateTrueAnomaly(M, e, a) * Constant.DegreesPerRadian;

            var diff = ((trueAnomalyCalc - trueAnomalyApprox) % 360).Abs();

            if (diff > 0.1 && diff < 359.9)
                Assert.Fail("FAIL");
        }

        [Test]
        public void SolarTransmitTest()
        {
            var jd = 2453097.0;
            var longitude = 5.0; // Netherlands: 5° East

            var jtrans = SunPosition.SolarTransit(jd, longitude);

            var h = SunPosition.HourAngleDeg(jtrans, longitude);

            var diff = Fun.AngleDifference(h.RadiansFromDegrees(), 0) * Constant.DegreesPerRadian;

            ClassicAssert.AreEqual(0.0, diff, 0.01);

            ClassicAssert.AreEqual(2453096.9895, jtrans, 0.001);
        }

        [Test]
        public void SunRiseSunSetTest()
        {
            // 1. April 2004
            var jd = 2453097.0;

            // Veldhuizen - Netherlands: 5° East, 52° North
            var longitude = 5.0;
            var latitude = 52.0;

            var (jdRise, _, jdSet) = SunPosition.SunRiseAndSet(jd, longitude, latitude);

            var date = DateTimeExtensions.ComputeDateFromJulianDay(jd);
            var timeRise = DateTimeExtensions.ComputeDateFromJulianDay(jdRise);
            var timeSet = DateTimeExtensions.ComputeDateFromJulianDay(jdSet);

            Report.Line("Date: " + date.ToString("dd.MM.yyyy"));
            Report.Line("Sun rise at: " + timeRise.ToString("HH:mm:ss"));
            Report.Line("Sun set at: " + timeSet.ToString("HH:mm:ss"));

            ClassicAssert.AreEqual(2453096.7191, jdRise, 0.001);
        }

        [Test]
        public void DuskDawnTest()
        {
            // 1. April 2004
            var jd = 2453097.0;

            // Veldhuizen - Netherlands: 5° East, 52° North
            var longitude = 5.0; 
            var latitude = 52.0;

            var (jdDawn, _, jdDusk) = SunPosition.CivilDuskAndDawn(jd, longitude, latitude);

            var date = DateTimeExtensions.ComputeDateFromJulianDay(jd);
            var timeStart = DateTimeExtensions.ComputeDateFromJulianDay(jdDawn);
            var timeEnd = DateTimeExtensions.ComputeDateFromJulianDay(jdDusk);

            Report.Line("Date: " + date.ToString("dd.MM.yyyy"));
            Report.Line("Civil dusk start: " + timeStart.ToString("HH:mm:ss"));
            Report.Line("Civil dawn end: " + timeEnd.ToString("HH:mm:ss"));

            var diffStart = new DateTime(2004, 4, 1, 4, 40, 0) - timeStart;
            var diffEnd = new DateTime(2004, 4, 1, 18, 49, 0) - timeEnd;
            ClassicAssert.IsTrue(diffStart.TotalMinutes < 1);
            ClassicAssert.IsTrue(diffEnd.TotalMinutes < 1);
        }

        [Test]
        public void DuskDawnTest2()
        {
            // 1. April 2004
            var jd = 2453097.0;

            // North Cape - Norway: 25° East, 71° North
            var longitude = 25.0;
            var latitude = 71.0;

            var (jdDawn, _, jdDusk) = SunPosition.AstronomicalDuskAndDawn(jd, longitude, latitude);

            ClassicAssert.IsTrue(jdDawn.IsNaN());
            ClassicAssert.IsTrue(jdDusk.IsNaN());
        }

        [Test]
        public void HorizonTest()
        {
            // 1. April 2004
            var jd = 2453097.0;

            // Veldhuizen - Netherlands: 5° East, 52° North
            var longitude = 5.0;
            var latitude = 52.0;

            var (jdRise, _, jdSet) = SunPosition.HorizonTransit(jd, longitude, latitude);

            var date = DateTimeExtensions.ComputeDateFromJulianDay(jd);
            var timeRise = DateTimeExtensions.ComputeDateFromJulianDay(jdRise);
            var timeSet = DateTimeExtensions.ComputeDateFromJulianDay(jdSet);

            Report.Line("Date: " + date.ToString("dd.MM.yyyy"));
            Report.Line("Sun rise at: " + timeRise.ToString("HH:mm:ss"));
            Report.Line("Sun set at: " + timeSet.ToString("HH:mm:ss"));

            var (jdRise2, _, jdSet2) = SunPosition.HorizonTransit(jd, longitude, latitude, 0.0);

            ClassicAssert.AreEqual(jdRise2, jdRise, 0.001);
            ClassicAssert.AreEqual(jdSet2, jdSet, 0.001);
        }

        [Test]
        public void TwilightTimesTest()
        {
            // 1. April 2004
            var jd = new DateTime(2021, 10, 06, 12, 0, 0).ComputeJulianDay();

            // Vienna
            var longitude = 16; // east
            var latitude = 48; // north

            var timesJd = SunPosition.GetTwilightTimes(jd, longitude, latitude);

            var timeZone = 2;
            var twilightTimes = timesJd.ToDateTime(timeZone);

            var date = DateTimeExtensions.ComputeDateFromJulianDay(jd);

            Report.Line("Date: " + date.ToString("dd.MM.yyyy") + "\n");

            Report.Line("Astronomical dawn start:   " + twilightTimes.AstronomicalDawn.ToString("HH:mm:ss"));
            Report.Line("Nautical dawn start:       " + twilightTimes.NauticalDawn.ToString("HH:mm:ss"));
            Report.Line("Civil dawn start:          " + twilightTimes.CivilDawn.ToString("HH:mm:ss"));
            Report.Line("Sun rise start:            " + twilightTimes.SunRise.ToString("HH:mm:ss"));
            Report.Line("Sun rise end:              " + twilightTimes.SunRiseEnd.ToString("HH:mm:ss"));
            Report.Line("Golden morning hour end:   " + twilightTimes.GoldenHourEnd.ToString("HH:mm:ss"));
            Report.Line("Noon:                      " + twilightTimes.Noon.ToString("HH:mm:ss"));
            Report.Line("Golden evening hour start: " + twilightTimes.GoldenHourStart.ToString("HH:mm:ss"));
            Report.Line("Sun set start:             " + twilightTimes.SunSetStart.ToString("HH:mm:ss"));
            Report.Line("Sun set end:               " + twilightTimes.SunSet.ToString("HH:mm:ss"));
            Report.Line("Civil dusk end:            " + twilightTimes.CivilDusk.ToString("HH:mm:ss"));
            Report.Line("Nautical dusk end:         " + twilightTimes.NauticalDusk.ToString("HH:mm:ss"));
            Report.Line("Astronomical dusk end:     " + twilightTimes.AstronomicalDusk.ToString("HH:mm:ss"));
        }
    }
}
