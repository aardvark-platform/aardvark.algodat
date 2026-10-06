using NUnit.Framework;
using System;

namespace Aardvark.Physics.Sky
{
    [TestFixture]
    public class AstronomyObliquityTests
    {
        // Offline radians from PyMeeus Coordinates.mean_obliquity(Epoch(jd)), TT input:
        // https://github.com/architest/pymeeus/blob/c8c5d719ace57d00fa7f4ae93ffa82ef1a79cf92/pymeeus/Coordinates.py#L235-L299
        // J2000, Meeus's 1987 example, signed day/year/century offsets, and |U| = 1.
        private static readonly (double jd, double radians)[] Reference =
        {
            (2451545.0, 0.4090928042223289),
            (2446895.5, 0.40912169256035796),
            (2451544.0, 0.40909281043554974),
            (2451546.0, 0.40909279800910814),
            (2451179.75, 0.4090950736011484),
            (2451910.25, 0.40909053484335917),
            (2415020.0, 0.4093197316662917),
            (2488070.0, 0.4088658752704614),
            (-1200955.0, 0.4229428645015299),
            (6104045.0, 0.39464487171158363)
        };

        [Test]
        public void LaskarMatchesPinnedLongTermReference()
        {
            Assert.Multiple(() =>
            {
                foreach (var (jd, expected) in Reference)
                    Assert.That(Astronomy.GetEarthMeanObliquityLaskar(jd), Is.EqualTo(expected).Within(2e-15),
                        $"JD={jd:R}, U={(jd - Astronomy.J2000) / 3652500:R}");
            });
        }

        [Test]
        public void ShortTermObliquityModelsKeepTheirExistingResults()
        {
            // Original DE200/AA2010 outputs: compatibility controls, not Laskar references.
            var controls = new (double jd, double de200, double aa2010)[]
            {
                (2451545.0, 0.40909280422232897, 0.4090926006005829),
                (2446895.5, 0.40912169604580345, 0.4091215058888625),
                (2451544.0, 0.4090928104363064, 0.4090926068174498),
                (2451546.0, 0.4090927980083515, 0.409092594383716),
                (2451179.75, 0.4090950738772822, 0.40909487131112343),
                (2451910.25, 0.40909053456680367, 0.4090903298898648),
                (2415020.0, 0.40931975809706767, 0.4093196610614513),
                (2488070.0, 0.4088658446267888, 0.40886553835874184)
            };
            foreach (var (jd, de200, aa2010) in controls)
            {
                Assert.That(Astronomy.GetEarthMeanObliquityDE200(jd), Is.EqualTo(de200), $"DE200, JD={jd:R}");
                Assert.That(Astronomy.GetEarthMeanObliquityAA2010(jd), Is.EqualTo(aa2010), $"AA2010, JD={jd:R}");
            }
            Assert.That(Astronomy.GetEarthMeanObliquityLaskar(Astronomy.J2000),
                Is.EqualTo(Astronomy.GetEarthMeanObliquityDE200(Astronomy.J2000)), "unchanged J2000 constant");
        }

        [Test]
        public void WarmedLaskarCallsDoNotAllocate()
        {
            var sum = 0.0;
            for (var i = 0; i < 4096; i++)
                sum += Astronomy.GetEarthMeanObliquityLaskar(Reference[i % Reference.Length].jd);
            Assert.That(double.IsFinite(sum), Is.True, "warmup");

            foreach (var count in new[] { 1, 128, 4096 })
            {
                sum = 0.0;
                var before = GC.GetAllocatedBytesForCurrentThread();
                for (var i = 0; i < count; i++)
                    sum += Astronomy.GetEarthMeanObliquityLaskar(Reference[i % Reference.Length].jd);
                var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.That(bytes, Is.Zero, $"calls={count}");
                Assert.That(double.IsFinite(sum) && sum > 0, Is.True, $"consumed result, calls={count}");
            }
        }
    }
}
