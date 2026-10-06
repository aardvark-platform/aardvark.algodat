using Aardvark.Base;
using NUnit.Framework;
using System;

namespace Aardvark.Physics.Sky
{
    [TestFixture]
    public class HorizonTransitTests
    {
        private static double Noon(int month, int day)
            => new DateTime(2025, month, day, 12, 0, 0, DateTimeKind.Utc).ComputeJulianDay();

        private static readonly (double jd, double latitude)[] TimingCases =
        {
            (Noon(1, 22), -52), (Noon(5, 10), 52), (Noon(7, 31), 52)
        };

        private static readonly (double elevation, Func<double, double, double, (double, double, double)> call)[] Wrappers =
        {
            (-0.83, SunPosition.SunRiseAndSet), (-6, SunPosition.CivilDuskAndDawn),
            (-12, SunPosition.NauticalDuskAndDawn), (-18, SunPosition.AstronomicalDuskAndDawn)
        };

        // Independent altitude-equation roots within the UTC day, not ephemeris references.
        // No event approximation, transit offset or event-time inverse cosine is reused.
        private static (double rise, double set) SolveAltitude(double jd, double latitude, double elevation)
        {
            var phi = latitude * Math.PI / 180;
            var target = Math.Sin(elevation * Math.PI / 180);
            double Residual(double time)
            {
                var delta = SunPosition.GetDeclination(time);
                var hourAngle = SunPosition.HourAngleDeg(time, 0) * Math.PI / 180;
                return Math.Sin(phi) * Math.Sin(delta) + Math.Cos(phi) * Math.Cos(delta) * Math.Cos(hourAngle) - target;
            }
            double Bisect(double low, double high, double fLow)
            {
                if (fLow == 0) return low;
                for (var i = 0; i < 60; i++)
                {
                    var middle = (low + high) / 2;
                    if (middle == low || middle == high) break;
                    var fMiddle = Residual(middle);
                    if (fMiddle == 0) return middle;
                    if ((fMiddle > 0) == (fLow > 0)) { low = middle; fLow = fMiddle; }
                    else high = middle;
                }
                return (low + high) / 2;
            }
            var rise = double.NaN;
            var set = double.NaN;
            var left = jd - 0.5;
            var fLeft = Residual(left);
            for (var i = 1; i <= 96; i++)
            {
                var right = jd - 0.5 + i / 96.0;
                var fRight = Residual(right);
                if (fLeft <= 0 && fRight > 0) rise = Bisect(left, right, fLeft);
                if (fLeft >= 0 && fRight < 0) set = Bisect(left, right, fLeft);
                left = right;
                fLeft = fRight;
            }
            return (rise, set);
        }

        private static void AssertEvents(double jd, double latitude, double elevation, double rise, double set)
        {
            var expected = SolveAltitude(jd, latitude, elevation);
            var context = $"JD={jd:R}, latitude={latitude}, elevation={elevation}";
            Assert.That(double.IsFinite(expected.rise) && double.IsFinite(expected.set), Is.True, context + ", bracketed roots");
            Assert.Multiple(() =>
            {
                Assert.That(Math.Abs(rise - expected.rise) * 86400, Is.LessThanOrEqualTo(1), context + ", rise error (seconds)");
                Assert.That(Math.Abs(set - expected.set) * 86400, Is.LessThanOrEqualTo(1), context + ", set error (seconds)");
            });
        }

        private static (double elevation, double rise, double set)[] Levels(SunPosition.TwilightTimesJd t) => new[]
        {
            (-18.0, t.AstronomicalDawn, t.AstronomicalDusk), (-12.0, t.NauticalDawn, t.NauticalDusk),
            (-6.0, t.CivilDawn, t.CivilDusk), (-0.83, t.SunRise, t.SunSet),
            (-0.3, t.SunRiseEnd, t.SunSetStart), (6.0, t.GoldenHourEnd, t.GoldenHourStart)
        };

        [Test]
        public void AstronomicalEventsMatchBracketedAltitudeRoots()
        {
            Assert.Multiple(() =>
            {
                foreach (var (jd, latitude) in TimingCases)
                {
                    var (rise, transit, set) = SunPosition.HorizonTransit(jd, 0, latitude, -18);
                    AssertEvents(jd, latitude, -18, rise, set);
                    Assert.That(transit, Is.EqualTo(SunPosition.SolarTransit(jd, 0)), $"JD={jd:R}, unchanged transit");
                }
            });
        }

        [Test]
        public void WrappersAndTwilightLevelsUseTheirSpecifiedElevations()
        {
            foreach (var (jd, latitude) in TimingCases)
            {
                var times = SunPosition.GetTwilightTimes(jd, 0, latitude);
                Assert.That(times.Noon, Is.EqualTo(SunPosition.SolarTransit(jd, 0)), $"JD={jd:R}, noon");
                foreach (var (elevation, rise, set) in Levels(times))
                {
                    var context = $"JD={jd:R}, latitude={latitude}, elevation={elevation}";
                    AssertEvents(jd, latitude, elevation, rise, set);
                    Assert.That(SunPosition.HorizonTransit(jd, 0, latitude, elevation),
                        Is.EqualTo((rise, times.Noon, set)), context + ", direct parity");
                    Assert.That(rise < times.Noon && times.Noon < set, Is.True, context + ", event order");
                }
                foreach (var (elevation, call) in Wrappers)
                    Assert.That(call(jd, 0, latitude), Is.EqualTo(SunPosition.HorizonTransit(jd, 0, latitude, elevation)),
                        $"JD={jd:R}, latitude={latitude}, wrapper elevation={elevation}");
            }
        }

        [Test]
        public void EquatorialHorizonControlsMatchAltitudeRoots()
        {
            foreach (var jd in new[] { Noon(1, 22), Noon(3, 20), Noon(5, 10), Noon(6, 21), Noon(7, 31), Noon(12, 21) })
            {
                var (rise, transit, set) = SunPosition.HorizonTransit(jd, 0, 0, 0);
                AssertEvents(jd, 0, 0, rise, set);
                Assert.That(transit, Is.EqualTo(SunPosition.SolarTransit(jd, 0)), $"JD={jd:R}, equatorial transit");
            }
        }

        [Test]
        public void PolarDayAndNightRetainMissingEventNaNs()
        {
            foreach (var jd in new[] { Noon(6, 21), Noon(12, 21) })
            foreach (var latitude in new[] { -89.0, 89.0 })
            {
                var times = SunPosition.GetTwilightTimes(jd, 0, latitude);
                var context = $"JD={jd:R}, latitude={latitude}";
                Assert.That(times.Noon, Is.EqualTo(SunPosition.SolarTransit(jd, 0)), context + ", finite noon");
                foreach (var (elevation, rise, set) in Levels(times))
                {
                    var expected = SolveAltitude(jd, latitude, elevation);
                    var direct = SunPosition.HorizonTransit(jd, 0, latitude, elevation);
                    Assert.That((double.IsNaN(expected.rise), double.IsNaN(expected.set)), Is.EqualTo((true, true)), context + $", no roots at {elevation}");
                    Assert.That((double.IsNaN(rise), double.IsNaN(set), double.IsNaN(direct.Item1), double.IsNaN(direct.Item3)),
                        Is.EqualTo((true, true, true, true)), context + $", NaNs at {elevation}");
                    Assert.That(direct.Item2, Is.EqualTo(times.Noon), context + ", retained transit");
                }
                foreach (var (elevation, call) in Wrappers)
                    Assert.That(call(jd, 0, latitude), Is.EqualTo(SunPosition.HorizonTransit(jd, 0, latitude, elevation)), context + $", wrapper={elevation}");
                var converted = times.ToDateTime();
                Assert.That(converted.Noon, Is.Not.EqualTo(DateTime.MinValue), context + ", converted noon");
                Assert.That(new[] { converted.AstronomicalDawn, converted.NauticalDawn, converted.CivilDawn,
                    converted.SunRise, converted.SunRiseEnd, converted.GoldenHourEnd, converted.GoldenHourStart,
                    converted.SunSetStart, converted.SunSet, converted.CivilDusk, converted.NauticalDusk, converted.AstronomicalDusk },
                    Is.All.EqualTo(DateTime.MinValue), context + ", absent transitions");
            }
        }

        private static double FiniteOrZero(double value) => double.IsNaN(value) ? 0 : value;
        private static double Consume(SunPosition.TwilightTimesJd t)
            => FiniteOrZero(t.AstronomicalDawn) + FiniteOrZero(t.NauticalDawn) + FiniteOrZero(t.CivilDawn)
            + FiniteOrZero(t.SunRise) + FiniteOrZero(t.SunRiseEnd) + FiniteOrZero(t.GoldenHourEnd) + t.Noon
            + FiniteOrZero(t.GoldenHourStart) + FiniteOrZero(t.SunSetStart) + FiniteOrZero(t.SunSet)
            + FiniteOrZero(t.CivilDusk) + FiniteOrZero(t.NauticalDusk) + FiniteOrZero(t.AstronomicalDusk);

        [Test]
        public void WarmedEventCalculationsRemainAllocationFree()
        {
            foreach (var (jd, latitude) in new[] { (Noon(1, 22), -52.0), (Noon(5, 10), 52.0), (Noon(7, 31), 52.0),
                (Noon(3, 20), 0.0), (Noon(6, 21), 89.0), (Noon(12, 21), 89.0) })
            foreach (var allLevels in new[] { false, true })
            {
                var sum = 0.0;
                double Run()
                {
                    if (allLevels) return Consume(SunPosition.GetTwilightTimes(jd, 0, latitude));
                    var (rise, transit, set) = SunPosition.HorizonTransit(jd, 0, latitude, -18);
                    return FiniteOrZero(rise) + transit + FiniteOrZero(set);
                }
                for (var i = 0; i < 1024; i++) sum += Run();
                var before = GC.GetAllocatedBytesForCurrentThread();
                for (var i = 0; i < 1024; i++) sum += Run();
                var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.That(bytes, Is.Zero, $"JD={jd:R}, latitude={latitude}, allLevels={allLevels}");
                Assert.That(double.IsFinite(sum) && sum > 0, Is.True, "consumed result");
            }
        }
    }
}
