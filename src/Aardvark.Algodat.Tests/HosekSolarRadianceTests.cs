using Aardvark.Base;
using Aardvark.Physics.Sky;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace Aardvark.Physics.Sky.Tests
{
    [TestFixture]
    public class HosekSolarRadianceTests
    {
        private const double Elevation = Math.PI / 4;
        private const double Theta = Math.PI / 4;

        private static ArHosekSkyModelState Model(int kind, double turbidity = 3, double albedo = 0.3)
            => kind == 0 ? new ArHosekSkyModelState(Elevation, turbidity, albedo, Col.Format.None)
             : kind == 1 ? new ArHosekSkyModelState(Elevation, 0.25, 3500, turbidity, albedo)
             : new ArHosekSkyModelState(Elevation, 4, 10000, turbidity, albedo);

        // Radius and direct-only evaluation are internal implementation details. Binding once
        // outside measurements avoids changing public API or measuring reflection allocations.
        private static FieldInfo Field(string name) => typeof(ArHosekSkyModelState).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
        private static double Radius(ArHosekSkyModelState state) => (double)Field("solar_radius").GetValue(state)!;
        private static Func<double, double, double, double> Direct(ArHosekSkyModelState state)
            => typeof(ArHosekSkyModelState).GetMethod("arhosekskymodel_solar_radiance_internal2", BindingFlags.Instance | BindingFlags.NonPublic)!
                .CreateDelegate<Func<double, double, double, double>>(state);

        [Test]
        public void RightAngleFromSunContainsOnlyDiffuseRadiance()
        {
            var state = Model(0);
            Assert.That(state.arhosekskymodel_solar_radiance(Theta, Math.PI / 2, 560),
                Is.EqualTo(state.arhosekskymodel_radiance(Theta, Math.PI / 2, 560)));
        }

        private static IEnumerable<TestCaseData> DiscCases()
        {
            foreach (var kind in new[] { 0, 1, 2 })
                foreach (var turbidity in new[] { 1.0, 3.0, 3.75, 10.0 })
                    foreach (var albedo in new[] { 0.0, 0.3, 1.0 })
                        foreach (var wavelength in new[] { 320.0, 340.0, 560.0, 567.25, 719.9, 720.0 })
                            yield return new TestCaseData(kind, turbidity, albedo, wavelength);
        }

        [TestCaseSource(nameof(DiscCases))]
        public void DirectLightIsRestrictedToTheInstanceDisc(int kind, double turbidity, double albedo, double wavelength)
        {
            var state = Model(kind, turbidity, albedo);
            var direct = Direct(state);
            var radius = Radius(state);
            foreach (var fraction in new[] { 0.0, 0.5, 0.999 })
            {
                var gamma = fraction * radius;
                var sun = direct(wavelength, Elevation, gamma);
                Assert.That(sun, Is.GreaterThan(0), $"inside at {fraction} radii");
                Assert.That(state.arhosekskymodel_solar_radiance(Theta, gamma, wavelength),
                    Is.EqualTo(sun + state.arhosekskymodel_radiance(Theta, gamma, wavelength)));
            }
            foreach (var gamma in new[] { radius, Math.BitIncrement(radius), radius * 1.001,
                                           Math.PI / 2, Math.PI - radius * 0.5, Math.PI, 2 * Math.PI })
            {
                Assert.That(direct(wavelength, Elevation, gamma), Is.Zero, $"outside at {gamma:R}");
                Assert.That(state.arhosekskymodel_solar_radiance(Theta, gamma, wavelength),
                    Is.EqualTo(state.arhosekskymodel_radiance(Theta, gamma, wavelength)), $"diffuse at {gamma:R}");
            }
        }

        [Test]
        public void AlienWorldsUseTheirOwnLargerAndSmallerRadii()
        {
            var earth = Model(0); var large = Model(1); var small = Model(2);
            var e = Radius(earth); var l = Radius(large); var s = Radius(small);
            Assert.That(s, Is.LessThan(e)); Assert.That(l, Is.GreaterThan(e));
            var betweenSmallAndEarth = (s + e) / 2;
            var betweenEarthAndLarge = (e + l) / 2;
            Assert.That(Direct(small)(560, Elevation, betweenSmallAndEarth), Is.Zero);
            Assert.That(Direct(earth)(560, Elevation, betweenSmallAndEarth), Is.GreaterThan(0));
            Assert.That(Direct(earth)(560, Elevation, betweenEarthAndLarge), Is.Zero);
            Assert.That(Direct(large)(560, Elevation, betweenEarthAndLarge), Is.GreaterThan(0));
        }

        [Test]
        public void SignedAnglesHaveTheSameSupportAndInDiscValues([Values(0, 1, 2)] int kind)
        {
            var state = Model(kind); var radius = Radius(state);
            foreach (var gamma in new[] { 0.0, radius / 2, radius * 0.999, radius, radius * 1.01, Math.PI })
                Assert.That(state.arhosekskymodel_solar_radiance(Theta, -gamma, 567.25),
                    Is.EqualTo(state.arhosekskymodel_solar_radiance(Theta, gamma, 567.25)));
        }

        [TestCase(280)]
        [TestCase(319.999)]
        [TestCase(720.001)]
        [TestCase(740)]
        [TestCase(759.99)]
        [TestCase(760)]
        [TestCase(780)]
        public void UnsupportedDirectWavelengthsKeepExistingDiffuseHandling(double wavelength)
        {
            var state = Model(0); var direct = Direct(state);
            foreach (var gamma in new[] { 0.0, Radius(state) / 2, Math.PI / 2 })
            {
                Assert.That(direct(wavelength, Elevation, gamma), Is.Zero);
                Assert.That(state.arhosekskymodel_solar_radiance(Theta, gamma, wavelength),
                    Is.EqualTo(state.arhosekskymodel_radiance(Theta, gamma, wavelength)));
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void RejectedDirectionsDoNotReadSolarInterpolationData(int sample)
        {
            var state = Model(0);
            var gamma = sample == 0 ? Math.PI / 2 : sample == 1 ? Math.PI : Radius(state);
            if (sample == 3)
            {
                // A representable angle inside the angular boundary whose sine rounds to one:
                // this exercises the numerical grazing guard independently of the angular guard.
                Field("solar_radius").SetValue(state, Math.PI / 2);
                gamma = Math.BitDecrement(Math.PI / 2);
                Assert.That(Math.Sin(gamma), Is.EqualTo(1.0));
            }
            Field("emission_correction_factor_sun").SetValue(state, Array.Empty<double>());
            Assert.That(Direct(state)(560, Elevation, gamma), Is.Zero);
        }

        [TestCase(0.0)]
        [TestCase(Math.PI / 2)]
        public void ZeroRadiusHasNoDirectContribution(double gamma)
        {
            var state = new ArHosekSkyModelState(Elevation, 0, 5800, 3, 0.3);
            Assert.That(Radius(state), Is.Zero);
            Assert.That(state.arhosekskymodel_solar_radiance(Theta, gamma, 560), Is.Zero);
        }

        [TestCase(0.0, 1.0)]
        [TestCase(0.5, 1.0)]
        [TestCase(11.0, 10.0)]
        public void ExistingInternalTurbidityClampsRemainUnchanged(double turbidity, double clamped)
        {
            var state = Model(0);
            Field("turbidity").SetValue(state, turbidity); // constructor validation/data access is out of scope
            Assert.That(Direct(state)(567.25, Elevation, Radius(state) / 2),
                Is.EqualTo(Direct(Model(0, clamped))(567.25, Elevation, Radius(state) / 2)));
        }

        [Test]
        public void AlienWorldOffDiscIntegrationMatchesDiffuseOnlySpectrum(
            [Values(0.25, 1.0, 4.0)] double intensity, [Values(3500.0, 5800.0, 10000.0)] double temperature,
            [Values(1.0, 3.75, 10.0)] double turbidity)
        {
            var sky = new AlienWorld(0, Theta, intensity, temperature, turbidity, 0.3);
            var state = new ArHosekSkyModelState(Elevation, intensity, temperature, turbidity, 0.3);
            foreach (var view in new[] { Sky.V3dFromPhiTheta(Math.PI, Theta), V3d.OOI, V3d.IOO, -sky.SunVec })
            {
                var theta = Math.Acos(view.Z);
                var gamma = Math.Acos(Fun.Clamp(view.Dot(sky.SunVec), -1, 1));
                Assert.That(gamma, Is.GreaterThan(Radius(state)));
                var x = 0.0; var y = 0.0; var z = 0.0;
                for (var i = 0; i < 41; i++)
                {
                    var diffuse = state.arhosekskymodel_radiance(theta, gamma, 380 + 10 * i);
                    x += diffuse * SpectralData.Ciexyz31X_380_780_10nm[i];
                    y += diffuse * SpectralData.Ciexyz31Y_380_780_10nm[i];
                    z += diffuse * SpectralData.Ciexyz31Z_380_780_10nm[i];
                }
                var expected = new C3f(x, y, z) * 0.01f;
                var actual = sky.GetRadiance(view);
                for (var c = 0; c < 3; c++)
                    Assert.That(actual[c], Is.EqualTo(expected[c]).Within(Math.Max(1e-8, Math.Abs(expected[c]) * 2e-6)));
            }
        }

        [Test]
        public void WarmedSpectralCallsAllocateNothing([Values(0, 1, 2)] int kind,
            [Values(0.0, 0.5, 0.999, 1.0, 2.0)] double fraction, [Values(560.0, 567.25, 740.0)] double wavelength)
        {
            var state = Model(kind); var gamma = Radius(state) * fraction;
            Sum(state, gamma, wavelength, 4096);
            var before = GC.GetAllocatedBytesForCurrentThread();
            var sum = Sum(state, gamma, wavelength, 1024);
            var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(double.IsFinite(sum), Is.True);
            Assert.That(bytes, Is.Zero);
        }

        private static double Sum(ArHosekSkyModelState state, double gamma, double wavelength, int count)
        {
            var sum = 0.0;
            for (var i = 0; i < count; i++) sum += state.arhosekskymodel_solar_radiance(Theta, gamma, wavelength);
            return sum;
        }

        [Test]
        public void RepeatedAndConcurrentSamplingDoesNotChangeState()
        {
            var state = Model(1, 3.75, 0.3); var radius = Radius(state);
            var gammas = new[] { 0.0, radius / 2, radius * 0.999, radius, Math.PI / 2, Math.PI };
            var expected = new double[gammas.Length];
            for (var i = 0; i < gammas.Length; i++) expected[i] = state.arhosekskymodel_solar_radiance(Theta, gammas[i], 567.25);
            Parallel.For(0, 64, new ParallelOptions { MaxDegreeOfParallelism = 4 }, pass =>
            {
                for (var i = 0; i < gammas.Length; i++)
                {
                    var index = (i + pass) % gammas.Length;
                    Assert.That(state.arhosekskymodel_solar_radiance(Theta, gammas[index], 567.25), Is.EqualTo(expected[index]));
                }
            });
            Assert.That(Radius(state), Is.EqualTo(radius));
        }

        // Fixed offline outputs from unmodified pbrt-v3 ArHosekSkyModel.c at
        // https://github.com/mmp/pbrt-v3/blob/13d871faae88233b327d04cda24022b8bb0093ee/src/ext/ArHosekSkyModel.c
        // In-disc cases are also controls for the old implementation. No production data
        // arrays or evaluator are used to calculate these expected values during tests.
        private static IEnumerable<TestCaseData> InDiscCases()
        {
            yield return new TestCaseData(0, 0d, 320d, 1463.2507032269802d, 0.063737627308041292d, 1463.3144408542883d);
            yield return new TestCaseData(0, 0d, 340d, 2957.0053631662654d, 0.091160459719823034d, 2957.0965236259854d);
            yield return new TestCaseData(0, 0d, 560d, 20679.617280346512d, 0.19174961847826569d, 20679.809029964992d);
            yield return new TestCaseData(0, 0d, 567.25d, 20789.380180770229d, 0.18992555140335032d, 20789.570106321633d);
            yield return new TestCaseData(0, 0d, 720d, 18098.799148338428d, 0.11293021120168668d, 18098.912078549631d);
            yield return new TestCaseData(0, 0.5d, 320d, 1289.9906760912006d, 0.063731301344465113d, 1290.054407392545d);
            yield return new TestCaseData(0, 0.5d, 340d, 2639.1234234698518d, 0.091156974120794099d, 2639.2145804439724d);
            yield return new TestCaseData(0, 0.5d, 560d, 19214.057196185222d, 0.19173412506484705d, 19214.248930310288d);
            yield return new TestCaseData(0, 0.5d, 567.25d, 19337.130912521916d, 0.18987446217786119d, 19337.320786984095d);
            yield return new TestCaseData(0, 0.5d, 720d, 17116.62981634356d, 0.11291092275928316d, 17116.742727266319d);
            yield return new TestCaseData(0, 0.999d, 320d, 180.25118130505581d, 0.063724330558433476d, 180.31490563561425d);
            yield return new TestCaseData(0, 0.999d, 340d, 431.80451993833657d, 0.091152150532024911d, 431.89567208886859d);
            yield return new TestCaseData(0, 0.999d, 560d, 6740.9947530404215d, 0.1917035089359333d, 6741.1864565493579d);
            yield return new TestCaseData(0, 0.999d, 567.25d, 6895.6114977826683d, 0.18980884484657015d, 6895.8013066275153d);
            yield return new TestCaseData(0, 0.999d, 720d, 8002.3550377765969d, 0.11287991615168957d, 8002.4679176927484d);
            yield return new TestCaseData(1, 0.5d, 400d, 311.76105106319693d, 0.041230040625073856d, 311.80228110382279d);
            yield return new TestCaseData(2, 0.999d, 720d, 5821.4626093686284d, 0.71013429086873636d, 5822.1727436594974d);
            yield return new TestCaseData(3, 0d, 599.75d, 19113.274112500148d, 0.35613020498769438d, 19113.630242705141d);
            yield return new TestCaseData(4, 0d, 560d, 20650.491583530562d, 0.29468970071922396d, 20650.786273231282d);
            yield return new TestCaseData(5, 0d, 320d, 16.156438718545974d, 0.0028108889738980521d, 16.159249607519872d);
            yield return new TestCaseData(5, 0d, 560d, 1169.2756296930677d, 0.013205404789654681d, 1169.2888350978574d);
            yield return new TestCaseData(5, 0d, 720d, 1921.1528586191744d, 0.0073281323612879737d, 1921.1601867515355d);
            yield return new TestCaseData(6, 0d, 400d, 118502.59314263542d, 6.6592436533874642d, 118509.2523862888d);
            yield return new TestCaseData(6, 0d, 720d, 59500.023757495859d, 2.6319418091717908d, 59502.655699305033d);
            yield return new TestCaseData(3, 0.5d, 599.75d, 17865.026988149519d, 0.35604353292073465d, 17865.383031682442d);
            yield return new TestCaseData(4, 0.5d, 560d, 19186.995657238414d, 0.29454890821996738d, 19187.290206146634d);
            yield return new TestCaseData(5, 0.5d, 320d, 14.243357849415716d, 0.0028107542215738291d, 14.24616860363729d);
            yield return new TestCaseData(5, 0.5d, 560d, 1086.4077634598227d, 0.013194305944525628d, 1086.4209577657673d);
            yield return new TestCaseData(5, 0.5d, 720d, 1816.8955027731881d, 0.0073278420553272161d, 1816.9028306152434d);
            yield return new TestCaseData(6, 0.5d, 400d, 106278.30344488181d, 6.6496722838664386d, 106284.95311716567d);
            yield return new TestCaseData(6, 0.5d, 720d, 56271.138755668137d, 2.6301000735137698d, 56273.768855741648d);
            yield return new TestCaseData(3, 0.999d, 599.75d, 6829.4296899953315d, 0.35592794580734005d, 6829.785617941141d);
            yield return new TestCaseData(4, 0.999d, 560d, 6731.5005772892391d, 0.29438443382181201d, 6731.7949617230606d);
            yield return new TestCaseData(5, 0.999d, 320d, 1.9902312901159225d, 0.0028105641503708067d, 1.9930418542662933d);
            yield return new TestCaseData(5, 0.999d, 560d, 381.1514455404884d, 0.013183205586732241d, 381.16462874607515d);
            yield return new TestCaseData(5, 0.999d, 720d, 849.4335809039676d, 0.0073273500788663629d, 849.44090825404646d);
            yield return new TestCaseData(6, 0.999d, 400d, 20291.930042214211d, 6.6399954929550651d, 20298.570037707166d);
            yield return new TestCaseData(6, 0.999d, 720d, 26307.84492040077d, 2.6281502694949315d, 26310.473070670265d);
        }

        [TestCaseSource(nameof(InDiscCases))]
        public void InDiscValuesMatchFixedReferences(int config, double fraction, double wavelength,
            double expectedDirect, double expectedDiffuse, double expectedTotal)
        {
            var elevation = config == 1 ? 0.1 : config == 2 ? Math.PI / 2 : config == 3 ? Math.PI / 6
                          : config == 5 ? 0.7 : config == 6 ? 1.0 : Elevation;
            var state = config switch
            {
                0 => Model(0),
                1 => new ArHosekSkyModelState(elevation, 1, 0, Col.Format.None),
                2 => new ArHosekSkyModelState(elevation, 10, 1, Col.Format.None),
                3 => new ArHosekSkyModelState(elevation, 3.75, 1, Col.Format.None),
                4 => new ArHosekSkyModelState(elevation, 1, 5800, 3.75, 0.25),
                5 => new ArHosekSkyModelState(elevation, 0.25, 3500, 1, 0),
                _ => new ArHosekSkyModelState(elevation, 4, 10000, 10, 1)
            };
            var gamma = fraction * Radius(state); var theta = Math.PI / 2 - elevation;
            Near(Direct(state)(wavelength, elevation, gamma), expectedDirect);
            Near(state.arhosekskymodel_radiance(theta, gamma, wavelength), expectedDiffuse);
            Near(state.arhosekskymodel_solar_radiance(theta, gamma, wavelength), expectedTotal);
        }

        private static void Near(double actual, double expected)
            => Assert.That(actual, Is.EqualTo(expected).Within(2e-12 * Math.Max(1, Math.Abs(expected))));
    }
}
