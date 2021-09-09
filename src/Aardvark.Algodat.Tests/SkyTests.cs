using Aardvark.Base;
using Aardvark.Physics.Sky;
using NUnit.Framework;
using System;

namespace Aardvark.Physics.Sky
{
    [TestFixture]
    public class SkyTests
    {
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

            Assert.AreEqual(0.0, diff, 0.01);

            Assert.AreEqual(2453096.9895, jtrans, 0.001);
        }

        [Test]
        public void SunRiseSunSetTest()
        {
            // 1. April 2004
            var jd = 2453097.0;

            // Veldhuizen - Netherlands: 5° East, 52° North
            var longitude = 5.0;
            var latitude = 52.0;

            var (jdRise, jdSet) = SunPosition.SunRiseAndSet(jd, longitude, latitude);

            var date = DateTimeExtensions.ComputeDateFromJulianDay(jd);
            var timeRise = DateTimeExtensions.ComputeDateFromJulianDay(jdRise);
            var timeSet = DateTimeExtensions.ComputeDateFromJulianDay(jdSet);

            Report.Line("Date: " + date.ToString("dd.MM.yyyy"));
            Report.Line("Sun rise at: " + timeRise.ToString("HH:mm:ss"));
            Report.Line("Sun set at: " + timeSet.ToString("HH:mm:ss"));

            Assert.AreEqual(2453096.7191, jdRise, 0.001);
        }

        [Test]
        public void DuskDawnTest()
        {
            // 1. April 2004
            var jd = 2453097.0;

            // Veldhuizen - Netherlands: 5° East, 52° North
            var longitude = 5.0; 
            var latitude = 52.0;

            var (jdDawn, jdDusk) = SunPosition.CivilDuskAndDawn(jd, longitude, latitude);

            var date = DateTimeExtensions.ComputeDateFromJulianDay(jd);
            var timeStart = DateTimeExtensions.ComputeDateFromJulianDay(jdDawn);
            var timeEnd = DateTimeExtensions.ComputeDateFromJulianDay(jdDusk);

            Report.Line("Date: " + date.ToString("dd.MM.yyyy"));
            Report.Line("Civil dusk start: " + timeStart.ToString("HH:mm:ss"));
            Report.Line("Civil dawn end: " + timeEnd.ToString("HH:mm:ss"));

            var diffStart = new DateTime(2004, 4, 1, 4, 40, 0) - timeStart;
            var diffEnd = new DateTime(2004, 4, 1, 18, 49, 0) - timeEnd;
            Assert.IsTrue(diffStart.TotalMinutes < 1);
            Assert.IsTrue(diffEnd.TotalMinutes < 1);
        }

        [Test]
        public void DuskDawnTest2()
        {
            // 1. April 2004
            var jd = 2453097.0;

            // North Cape - Norway: 25° East, 71° North
            var longitude = 25.0;
            var latitude = 71.0;

            var (jdDawn, jdDusk) = SunPosition.AstronomicalDuskAndDawn(jd, longitude, latitude);

            Assert.IsTrue(jdDawn.IsNaN());
            Assert.IsTrue(jdDusk.IsNaN());
        }

        [Test]
        public void HorizonTest()
        {
            // 1. April 2004
            var jd = 2453097.0;

            // Veldhuizen - Netherlands: 5° East, 52° North
            var longitude = 5.0;
            var latitude = 52.0;

            var (jdRise, jdSet) = SunPosition.HorizonTransit(jd, longitude, latitude);

            var date = DateTimeExtensions.ComputeDateFromJulianDay(jd);
            var timeRise = DateTimeExtensions.ComputeDateFromJulianDay(jdRise);
            var timeSet = DateTimeExtensions.ComputeDateFromJulianDay(jdSet);

            Report.Line("Date: " + date.ToString("dd.MM.yyyy"));
            Report.Line("Sun rise at: " + timeRise.ToString("HH:mm:ss"));
            Report.Line("Sun set at: " + timeSet.ToString("HH:mm:ss"));

            var (jdRise2, jdSet2) = SunPosition.HorizonTransit(jd, longitude, latitude, 0.0);

            Assert.AreEqual(jdRise2, jdRise, 0.001);
            Assert.AreEqual(jdSet2, jdSet, 0.001);
        }
    }
}
