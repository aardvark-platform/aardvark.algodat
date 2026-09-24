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
    public class CIESkyTests
    {
        private static readonly (double SunPhi, double SunTheta, double ViewPhi, double ViewTheta)[] Directions =
        {
            (0, 45, 0, 40), (0, 45, 0, 45), (0, 45, 180, 45), (0, 45, 0, 0),
            (0, 45, 90, 90), (35, 25, 120, 65), (210, 75, 210, 74.99), (300, 80, 120, 90)
        };
        private static double Radians(double degrees) => degrees * (Math.PI / 180);
        private static V3d Direction(double phi, double theta)
        {
            var p = Radians(phi); var z = Radians(theta);
            return new V3d(-Math.Sin(z) * Math.Sin(p), -Math.Sin(z) * Math.Cos(p), Math.Cos(z));
        }
        private static (double Diffuse, double Global) Illuminance(int mode)
            => mode == 0 ? (-1, -1) : mode == 1 ? (15000, 80000) : (15000, 15100);
        private static CIESky Model(int type, int mode, double phi = 0, double theta = 45)
        {
            var (diffuse, global) = Illuminance(mode);
            if (mode != 0)
            {
                // Scale measurements with solar elevation: a fixed 80 klx at a low
                // sun can imply negative turbidity and clamp calibrated luminance to zero.
                var scale = Math.Cos(Radians(theta)) / Math.Cos(Radians(45));
                diffuse *= scale; global *= scale;
            }
            return new CIESky(Radians(phi), Radians(theta), (CIESkyType)type, diffuse, global);
        }
        private static double Field(CIESky model, string name)
            => (double)typeof(CIESky).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(model)!;
        private static void Near(double actual, double expected, double relative = 1e-6)
            => Assert.That(actual, Is.EqualTo(expected).Within(relative * Math.Max(1, Math.Abs(expected))));
        private static void Near(C3f actual, C3f expected)
        {
            for (var c = 0; c < 3; c++) Near(actual[c], expected[c]);
        }

        private static IEnumerable<TestCaseData> DistributionCases()
        {
            for (var type = 0; type < 15; type++)
                for (var mode = 0; mode < 3; mode++)
                    for (var direction = 0; direction < Directions.Length; direction++)
                        yield return new TestCaseData(type, mode, direction);
        }

        [TestCaseSource(nameof(DistributionCases))]
        public void RelativeDistributionMatchesOfflineReferences(int type, int mode, int direction)
        {
            var d = Directions[direction];
            var model = Model(type, mode, d.SunPhi, d.SunTheta);
            var expected = References[direction]; var offset = 3 * type;
            var zenith = model.GetRadiance(V3d.OOI);
            var sample = model.GetRadiance(Direction(d.ViewPhi, d.ViewTheta));
            var ratio = (double)sample.G / zenith.G;
            Near(ratio, expected[offset + 1]);
            // Standard gradation has no epsilon. Account explicitly for the preserved
            // regularization, separately from the float XYZ rounding tolerance.
            Assert.That(ratio, Is.EqualTo(expected[offset]).Within(
                Math.Abs(expected[offset + 1] - expected[offset]) + 1e-6 * Math.Max(1, expected[offset])));
            Near(Field(model, "gradationIndicatrixZ"), expected[offset + 2], 2e-13);
            Near((double)sample.R / sample.G, (double)zenith.R / zenith.G);
            Near((double)sample.B / sample.G, (double)zenith.B / zenith.G);
            Assert.That(model.Type, Is.EqualTo(type));
        }

        [Test]
        public void ClearSky2NearSunReproducesRadiansScattering()
        {
            var model = Model(11, 0);
            var ratio = (double)model.GetRadiance(Direction(0, 40)).G / model.GetRadiance(V3d.OOI).G;
            Assert.That(ratio, Is.EqualTo(5.419443772284771).Within(6e-6));
        }

        private static IEnumerable<TestCaseData> CalibrationCases()
        {
            foreach (var r in LegacyCalibration)
                yield return new TestCaseData((int)r[0], (int)r[1], r[2], r[3], new C3f(r[4], r[5], r[6]));
        }
        [TestCaseSource(nameof(CalibrationCases))]
        public void ZenithCalibrationAndZeroCControlsRemainUnchanged(int type, int mode, double phi, double theta, C3f expected)
            => Near(Model(type, mode).GetRadiance(Direction(phi, theta)), expected);

        private static IEnumerable<TestCaseData> TypeModes()
        {
            for (var type = 0; type < 15; type++)
                for (var mode = 0; mode < 3; mode++) yield return new TestCaseData(type, mode);
        }
        [TestCaseSource(nameof(TypeModes))]
        public void AzimuthRotationAndConstructorOverloadsPreserveResults(int type, int mode)
        {
            var model = Model(type, mode);
            var (diffuse, global) = Illuminance(mode);
            foreach (var rotation in new[] { -37.0, 90.0, 180.0, 360.0 })
            {
                var rotated = new CIESky(Radians(rotation), Radians(45), type, diffuse, global);
                foreach (var view in new[] { (0.0, 40.0), (180.0, 45.0), (90.0, 90.0), (120.0, 70.0) })
                    Near(rotated.GetRadiance(Direction(view.Item1 + rotation, view.Item2)),
                         model.GetRadiance(Direction(view.Item1, view.Item2)));
            }
        }

        [Test]
        public void BothMeasuredIlluminancesAreRequired([Range(0, 14)] int type)
        {
            var expected = Model(type, 0).GetRadiance(Direction(0, 40));
            Near(new CIESky(0, Radians(45), type, 15000, -1).GetRadiance(Direction(0, 40)), expected);
            Near(new CIESky(0, Radians(45), type, -1, 80000).GetRadiance(Direction(0, 40)), expected);
            Near(new CIESky(0, Radians(45), type, -2, -3).GetRadiance(Direction(0, 40)), expected);
        }

        [Test]
        public void SunClampAndCachedNormalizationKeepTheirExistingDomains([Range(0, 14)] int type)
        {
            foreach (var theta in new[] { 0.0, 5.0, 10.0, 90.0, 170.0, 175.0, 180.0 })
            {
                var model = Model(type, 0, 0, theta);
                Near(model.SunTheta, Radians(theta), 1e-15); // original direction is not changed
                Near(Field(model, "Zs"), Radians(Math.Clamp(theta, 10, 170)), 1e-15);
                var column = theta < 90 ? 0 : theta == 90 ? 1 : 2;
                Near(Field(model, "gradationIndicatrixZ"), ClampNormalizations[type][column], 2e-13);
                Assert.That(model.GetRadiance(V3d.OOI).G, Is.GreaterThanOrEqualTo(0));
            }
        }

        [Test]
        public void ExistingZeroIlluminanceAndNegativeLuminanceClampsRemainBlack()
        {
            for (var type = 0; type < 15; type++)
            {
                var model = new CIESky(0, Radians(45), type, 0, 0);
                Assert.That(model.GetRadiance(Direction(0, 40)), Is.EqualTo(C3f.Black));
                Assert.That(model.GetColor(Direction(0, 40)), Is.EqualTo(C3f.Black));
            }
            var night = Model(1, 0, 0, 180);
            Assert.That(Field(night, "Lz"), Is.Zero);
            Assert.That(night.GetRadiance(V3d.OOI), Is.EqualTo(C3f.Black));
        }

        [Test]
        public void NinetyDegreeIndicatrixIsOneAndZeroCIsAngleIndependent()
        {
            var model = Model(11, 0);
            var f = typeof(CIESky).GetMethod("f", BindingFlags.Instance | BindingFlags.NonPublic)!
                .CreateDelegate<Func<double, double, double, double, double>>(model);
            foreach (var parameters in new[] { (2.0, -1.5, 0.15), (5.0, -2.5, 0.3), (10.0, -3.0, 0.45), (16.0, -3.0, 0.3), (24.0, -2.8, 0.15) })
                Assert.That(f(parameters.Item1, parameters.Item2, parameters.Item3, Math.PI / 2), Is.EqualTo(1));
            foreach (var chi in new[] { 0.0, 0.01, Math.PI / 4, Math.PI / 2, Math.PI })
                Assert.That(f(0, -1, 0, chi), Is.EqualTo(1));
        }

        [TestCase(0)]
        [TestCase(6)]
        [TestCase(11)]
        public void ColorConversionPipelineIsUnchanged(int type)
        {
            var model = Model(type, 0);
            foreach (var d in Directions)
            {
                var view = Direction(d.ViewPhi, d.ViewTheta);
                var expected = CIESky.XYZTosRGBScaledToFit(model.GetRadiance(view) * 0.0006f / 318.0f);
                Assert.That(model.GetColor(view), Is.EqualTo(expected));
            }
            foreach (var xyz in new[] { C3f.Black, new C3f(0.2f, 0.1f, 0.05f), new C3f(1000, 800, 500) })
            {
                var color = CIESky.XYZTosRGBScaledToFit(xyz);
                for (var c = 0; c < 3; c++) Assert.That(color[c], Is.InRange(0.0f, 1.0f));
            }
        }

        private static readonly V3d[] AllocationViews = { Direction(0, 40), Direction(0, 45), Direction(180, 45), V3d.OOI, Direction(90, 90) };
        [TestCaseSource(nameof(TypeModes))]
        public void WarmedRadianceAllocatesNothing(int type, int mode)
        {
            var model = Model(type, mode);
            Sum(model, 4096);
            foreach (var count in new[] { 1, 37, 4096 })
            {
                var before = GC.GetAllocatedBytesForCurrentThread();
                var sum = Sum(model, count);
                var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.That(double.IsFinite(sum), Is.True);
                Assert.That(bytes, Is.Zero);
            }
        }
        private static double Sum(CIESky model, int count)
        {
            var sum = 0.0;
            for (var i = 0; i < count; i++)
            {
                var color = model.GetRadiance(AllocationViews[i % AllocationViews.Length]);
                sum += color.R + 3.0 * color.G + 7.0 * color.B;
            }
            return sum;
        }

        [Test]
        public void ConcurrentInterleavedSamplingDoesNotChangeModels()
        {
            var models = new[] { Model(0, 0), Model(6, 1), Model(11, 0), Model(14, 2) };
            var expected = new C3f[models.Length, AllocationViews.Length];
            for (var m = 0; m < models.Length; m++)
                for (var v = 0; v < AllocationViews.Length; v++) expected[m, v] = models[m].GetRadiance(AllocationViews[v]);
            Parallel.For(0, 64, new ParallelOptions { MaxDegreeOfParallelism = 4 }, pass =>
            {
                for (var v = 0; v < AllocationViews.Length; v++)
                {
                    var m = pass % models.Length;
                    Assert.That(models[m].GetRadiance(AllocationViews[v]), Is.EqualTo(expected[m, v]));
                }
            });
        }

        // CIE S 011/E:2003, equations (1), (3)-(7), table 1 (CIE numbering is 1-15).
        // Offline outputs from the unmodified scalar functions and coefficients at:
        // https://github.com/colour-science/colour/blob/5259f87c012e42b570778007f3d2560c15549518/colour/phenomena/sky/cie2003.py
        // Each type has: standard ratio, independently epsilon-regularized ratio,
        // and epsilon-regularized zenith normalization. No production tables are read.
        private static readonly double[][] References =
        {
            new double[] {
                0.87197471623894363d, 0.87197495095376498d, 2.9863426056035847d,
                1.5764146035759217d, 1.5764150279091793d, 4.4830004424114591d,
                0.9282990722078569d, 0.92829917974822618d, 1.4942622559381948d,
                1.6782415667123991d, 1.6782417611311198d, 2.2431379245904535d,
                1d, 1d, 1d,
                1.8078673317219707d, 1.8078673317219707d, 1.5011674929726884d,
                2.9767328660355661d, 2.9767328660355661d, 1.7533202485375154d,
                4.3463928119689772d, 4.3463928119689772d, 2.0829693382102543d,
                2.1891000323071679d, 2.189099720794903d, 0.63506871619902538d,
                3.6044492307969911d, 3.6044487178783826d, 0.74174190724014988d,
                5.2629352155162348d, 5.2629344665925473d, 0.88119991252913898d,
                5.4194448733091747d, 5.4194437722847706d, 0.57042267500048405d,
                6.656946083142496d, 6.6569447307050655d, 0.69085718824813647d,
                6.816069596378183d, 6.8160678590001531d, 0.3513987578640026d,
                7.2899577569564356d, 7.289955898786963d, 0.47936642949497926d,
            },
            new double[] {
                0.83258464279643529d, 0.83258495196250304d, 2.9863426056035847d,
                1.6419330401560144d, 1.6419336498597676d, 4.4830004424114591d,
                0.90670172241055924d, 0.90670186244025341d, 1.4942622559381948d,
                1.7880986977996158d, 1.7880989739509758d, 2.2431379245904535d,
                1d, 1d, 1d,
                1.9720914316185179d, 1.9720914316185179d, 1.5011674929726884d,
                3.5369953893133417d, 3.5369953893133417d, 1.7533202485375154d,
                5.4538330840480249d, 5.4538330840480249d, 2.0829693382102543d,
                2.5200264145068174d, 2.5200259490012837d, 0.63506871619902538d,
                4.5197305085105413d, 4.5197296736147043d, 0.74174190724014988d,
                6.9691512329286409d, 6.969149945569729d, 0.88119991252913898d,
                7.2490750773671975d, 7.2490731219407749d, 0.57042267500048405d,
                9.0391831044300339d, 9.0391806661248317d, 0.69085718824813647d,
                9.3320820496291521d, 9.3320788521017022d, 0.3513987578640026d,
                9.9105784043309839d, 9.9105750085886051d, 0.47936642949497926d,
            },
            new double[] {
                0.83258464279643529d, 0.83258495196250304d, 2.9863426056035847d,
                0.55462474820028829d, 0.55462495415070301d, 4.4830004424114591d,
                0.90670172241055924d, 0.90670186244025341d, 1.4942622559381948d,
                0.60399770622201676d, 0.60399779950254329d, 2.2431379245904535d,
                1d, 1d, 1d,
                0.66614818445058988d, 0.66614818445058988d, 1.5011674929726884d,
                0.57034646171121495d, 0.57034646171121495d, 1.7533202485375154d,
                0.48008387913152289d, 0.48008387913152289d, 2.0829693382102543d,
                0.85123386972657178d, 0.85123371248453883d, 0.63506871619902538d,
                0.72881415429768592d, 0.72881401966932657d, 0.74174190724014988d,
                0.61347259928887754d, 0.61347248596669657d, 0.88119991252913898d,
                0.63811342034600549d, 0.63811324821592963d, 0.57042267500048405d,
                0.5268735281028013d, 0.52687338597950872d, 0.69085718824813647d,
                0.54394594480813585d, 0.54394575843149651d, 0.3513987578640026d,
                0.39873866334801861d, 0.39873852672493548d, 0.47936642949497926d,
            },
            new double[] {
                1d, 1d, 2.9863426056035847d,
                1d, 1d, 4.4830004424114591d,
                1d, 1d, 1.4942622559381948d,
                1d, 1d, 2.2431379245904535d,
                1d, 1d, 1d,
                1d, 1d, 1.5011674929726884d,
                1d, 1d, 1.7533202485375154d,
                1d, 1d, 2.0829693382102543d,
                1d, 1d, 0.63506871619902538d,
                1d, 1d, 0.74174190724014988d,
                1d, 1d, 0.88119991252913898d,
                1d, 1d, 0.57042267500048405d,
                1d, 1d, 0.69085718824813647d,
                1d, 1d, 0.3513987578640026d,
                1d, 1d, 0.47936642949497926d,
            },
            new double[] {
                0.33485791741468324d, 0.33485776150519242d, 2.9863426056035847d,
                0.22306499373469679d, 0.22306488987587256d, 4.4830004424114591d,
                0.66922674426423268d, 0.66922656717453866d, 1.4942622559381948d,
                0.44580418067739785d, 0.44580406270941964d, 2.2431379245904535d,
                1d, 1d, 1d,
                0.66614818445058988d, 0.66614818445058988d, 1.5011674929726884d,
                0.57034646171121495d, 0.57034646171121495d, 1.7533202485375154d,
                0.48008387913152289d, 0.48008387913152289d, 2.0829693382102543d,
                1.5746315704283602d, 1.5746327515314233d, 0.63506871619902538d,
                1.3481768256011852d, 1.3481778368445823d, 0.74174190724014988d,
                1.1348154212229642d, 1.1348162724277764d, 0.88119991252913898d,
                1.7530845026121358d, 1.7530859901373159d, 0.57042267500048405d,
                1.4474759306155414d, 1.4474771588260991d, 0.69085718824813647d,
                2.8457672394891533d, 2.8457698771576685d, 0.3513987578640026d,
                2.0860849062377529d, 2.0860868397762378d, 0.47936642949497926d,
            },
            new double[] {
                0.59046963570290179d, 0.59047036258288432d, 2.9863426056035847d,
                0.35836324527554719d, 0.35836368642790073d, 5.8922149515152187d,
                0.78010932898321894d, 0.78010961920904509d, 1.4942622559381948d,
                0.47345789503868635d, 0.47345807118029065d, 2.9482599851078932d,
                1d, 1d, 1d,
                0.60691223325810395d, 0.60691223325810395d, 1.9730539089718118d,
                0.439165563151587d, 0.439165563151587d, 2.8275902111441682d,
                0.33010157913508836d, 0.33010157913508836d, 3.9807026834083756d,
                1.0441847385727554d, 1.0441843195200085d, 0.83470020422630764d,
                0.75557873712289947d, 0.75557843389365886d, 1.1962116777337708d,
                0.56793554689321868d, 0.56793531896903804d, 1.6840357618696227d,
                0.64008864575428248d, 0.64008817603076207d, 1.0901183379888084d,
                0.5110769171633901d, 0.51107654211401166d, 1.485404399163025d,
                0.56534764691132466d, 0.56534705657796247d, 0.75553858260520179d,
                0.465130867298364d, 0.46513038161089298d, 1.1005876172376234d,
            },
            new double[] {
                0.42462055741721233d, 0.42462129649088315d, 2.9863426056035847d,
                1.1413155294783304d, 1.1413175159960418d, 3.2886246550478182d,
                0.7027581491212439d, 0.7027583630890013d, 1.4942622559381948d,
                1.8889071078851434d, 1.8889076829979587d, 1.6455137085627567d,
                1d, 1d, 1d,
                2.6878480317120563d, 2.6878480317120563d, 1.1012214904200979d,
                5.5792242992825321d, 5.5792242992825321d, 1.1111409234900074d,
                9.9837616038519794d, 9.9837616038519794d, 1.1373401038033781d,
                5.5936574519365134d, 5.5936554170822257d, 0.4658716108933173d,
                11.610875767343339d, 11.61087154355228d, 0.47006802578680462d,
                20.777120519783502d, 20.777112961506447d, 0.4811515856726713d,
                25.86012832865163d, 25.860099716247642d, 0.31146141832039059d,
                37.274622557957848d, 37.274581316222871d, 0.32632313235684729d,
                45.42020214401709d, 45.420114778825372d, 0.1659815448446931d,
                58.988936489137053d, 58.988823024654245d, 0.18515407645669105d,
            },
            new double[] {
                0.33485791741468324d, 0.33485776150519242d, 2.9863426056035847d,
                0.30898684813624d, 0.30898670427229158d, 3.1692621402193701d,
                0.66922674426423268d, 0.66922656717453866d, 1.4942622559381948d,
                0.61752239276638221d, 0.61752222935860968d, 1.5857888463358509d,
                1d, 1d, 1d,
                0.9227401595333794d, 0.9227401595333794d, 1.0612520258970135d,
                1.1246609853366003d, 1.1246609853366003d, 1.06293571113299d,
                1.2534705331650615d, 1.2534705331650615d, 1.0753824487573422d,
                2.18116001877527d, 2.1811616548245074d, 0.44896253403103309d,
                2.6584575847799714d, 2.6584595788416019d, 0.44967481685510191d,
                2.9629357554299149d, 2.9629379778751921d, 0.45494040761760268d,
                4.57719965550152d, 4.5772035393415367d, 0.2944942692223228d,
                3.7882455840310145d, 3.7882487984286595d, 0.30341219625069704d,
                7.4477681805669178d, 7.4477750837118304d, 0.15432808791299643d,
                5.1631391862903895d, 5.1631439718714045d, 0.16583247775868315d,
            },
        };
        // The same independent indicatrix at the preserved 10/90/170 degree clamp cases.
        private static readonly double[][] ClampNormalizations =
        {
            new double[] { 2.9863426056035847d, 2.9863426056035847d, 2.9863426056035847d },
            new double[] { 7.4516633887920838d, 2.9863426056035847d, 2.924405452869121d },
            new double[] { 1.4942622559381948d, 1.4942622559381948d, 1.4942622559381948d },
            new double[] { 3.728553892287926d, 1.4942622559381948d, 1.4632710530541968d },
            new double[] { 1d, 1d, 1d },
            new double[] { 2.4952473218611133d, 1d, 0.97925986368133233d },
            new double[] { 4.424453938983123d, 1d, 1.1954423242322885d },
            new double[] { 7.2704464013494254d, 1d, 1.3479602114002152d },
            new double[] { 1.0556140608636428d, 0.4230498722973477d, 0.41427576027630575d },
            new double[] { 1.8717646738723073d, 0.4230498722973477d, 0.50573172260531418d },
            new double[] { 3.0757614216355855d, 0.4230498722973477d, 0.57025439529476685d },
            new double[] { 1.9910170584982285d, 0.27385073055881237d, 0.3691398886561601d },
            new double[] { 2.909767744596909d, 0.27385073055881237d, 0.31476427284959951d },
            new double[] { 1.4800291413582931d, 0.13929189446886514d, 0.1601022271763467d },
            new double[] { 2.1691332655736817d, 0.13929189446886514d, 0.11926408213950765d },
        };
        // Compatibility snapshots from baseline 18e8f11, not standard-model oracles:
        // type, illuminance mode, view phi/theta (degrees), XYZ. Zenith snapshots
        // protect absolute calibration/color; off-zenith snapshots cover zero-C types.
        private static readonly double[][] LegacyCalibration =
        {
            new double[] { 0, 0, 0, 0, 3747.6245d, 3862.9204d, 5448.3237d },
            new double[] { 0, 0, 0, 40, 3267.8347d, 3368.3699d, 4750.802d },
            new double[] { 0, 0, 90, 90, 1254.9211d, 1293.5289d, 1824.4136d },
            new double[] { 0, 0, 120, 70, 1903.2936d, 1961.8486d, 2767.0222d },
            new double[] { 0, 1, 0, 0, 6149.2896d, 6124.433d, 12059.531d },
            new double[] { 0, 1, 0, 40, 5362.027d, 5340.3525d, 10515.61d },
            new double[] { 0, 1, 90, 90, 2059.1375d, 2050.814d, 4038.2278d },
            new double[] { 0, 1, 120, 70, 3123.0193d, 3110.3955d, 6124.6343d },
            new double[] { 0, 2, 0, 0, 5941.638d, 6124.433d, 8637.997d },
            new double[] { 0, 2, 0, 40, 5180.96d, 5340.3525d, 7532.117d },
            new double[] { 0, 2, 90, 90, 1989.6036d, 2050.814d, 2892.5002d },
            new double[] { 0, 2, 120, 70, 3017.56d, 3110.3955d, 4386.9507d },
            new double[] { 1, 0, 0, 0, 7137.4956d, 7185.5938d, 13256.263d },
            new double[] { 1, 1, 0, 0, 6354.763d, 6329.076d, 12462.491d },
            new double[] { 1, 2, 0, 0, 6140.1733d, 6329.076d, 8926.629d },
            new double[] { 2, 0, 0, 0, 4970.0786d, 5122.9834d, 7225.5366d },
            new double[] { 2, 0, 0, 40, 4613.72d, 4755.6616d, 6707.46d },
            new double[] { 2, 0, 90, 90, 3326.1086d, 3428.4368d, 4835.5215d },
            new double[] { 2, 0, 120, 70, 3678.8843d, 3792.0654d, 5348.3887d },
            new double[] { 2, 1, 0, 0, 5436.7695d, 5414.793d, 10662.189d },
            new double[] { 2, 1, 0, 40, 5046.9487d, 5026.548d, 9897.702d },
            new double[] { 2, 1, 90, 90, 3638.4304d, 3623.7231d, 7135.42d },
            new double[] { 2, 1, 120, 70, 4024.3315d, 4008.0645d, 7892.2207d },
            new double[] { 2, 2, 0, 0, 5253.1787d, 5414.793d, 7637.1094d },
            new double[] { 2, 2, 0, 40, 4876.5215d, 5026.548d, 7089.5225d },
            new double[] { 2, 2, 90, 90, 3515.5664d, 3623.7231d, 5110.956d },
            new double[] { 2, 2, 120, 70, 3888.4363d, 4008.0645d, 5653.037d },
            new double[] { 3, 0, 0, 0, 7785.6d, 7838.0654d, 14459.968d },
            new double[] { 3, 1, 0, 0, 5671.4673d, 5648.542d, 11122.461d },
            new double[] { 3, 2, 0, 0, 5479.9507d, 5648.542d, 7966.7925d },
            new double[] { 4, 0, 0, 0, 5843.358d, 6023.1294d, 8495.116d },
            new double[] { 4, 0, 0, 40, 5843.358d, 6023.1294d, 8495.116d },
            new double[] { 4, 0, 90, 90, 5843.358d, 6023.1294d, 8495.116d },
            new double[] { 4, 0, 120, 70, 5843.358d, 6023.1294d, 8495.116d },
            new double[] { 4, 1, 0, 0, 4794.037d, 4774.6587d, 9401.71d },
            new double[] { 4, 1, 0, 40, 4794.037d, 4774.6587d, 9401.71d },
            new double[] { 4, 1, 90, 90, 4794.037d, 4774.6587d, 9401.71d },
            new double[] { 4, 1, 120, 70, 4794.037d, 4774.6587d, 9401.71d },
            new double[] { 4, 2, 0, 0, 4632.1504d, 4774.6587d, 6734.254d },
            new double[] { 4, 2, 0, 40, 4632.1504d, 4774.6587d, 6734.254d },
            new double[] { 4, 2, 90, 90, 4632.1504d, 4774.6587d, 6734.254d },
            new double[] { 4, 2, 120, 70, 4632.1504d, 4774.6587d, 6734.254d },
            new double[] { 5, 0, 0, 0, 11941.508d, 12021.979d, 22178.613d },
            new double[] { 5, 1, 0, 0, 5036.1836d, 5015.826d, 9876.59d },
            new double[] { 5, 2, 0, 0, 4866.1196d, 5015.826d, 7074.4004d },
            new double[] { 6, 0, 0, 0, 12212.245d, 12226.709d, 23366.76d },
            new double[] { 6, 1, 0, 0, 3841.3667d, 3825.839d, 7533.404d },
            new double[] { 6, 2, 0, 0, 4626.4536d, 4768.7866d, 6725.9717d },
            new double[] { 7, 0, 0, 0, 9476.113d, 9475.772d, 18240.912d },
            new double[] { 7, 1, 0, 0, 3697.0442d, 3682.1d, 7250.369d },
            new double[] { 7, 2, 0, 0, 4345.958d, 4479.6616d, 6318.1855d },
            new double[] { 8, 0, 0, 0, 9893.682d, 9905.399d, 18930.45d },
            new double[] { 8, 1, 0, 0, 3220.937d, 3207.9175d, 6316.663d },
            new double[] { 8, 2, 0, 0, 3559.8054d, 3669.3232d, 5175.2715d },
            new double[] { 9, 0, 0, 0, 8435.946d, 8435.643d, 16238.656d },
            new double[] { 9, 1, 0, 0, 3750.8743d, 3735.7124d, 7355.9365d },
            new double[] { 9, 2, 0, 0, 3416.6184d, 3521.731d, 4967.105d },
            new double[] { 10, 0, 0, 0, 4630.306d, 4614.761d, 9052.7705d },
            new double[] { 10, 1, 0, 0, 2948.037d, 2936.1206d, 5781.472d },
            new double[] { 10, 2, 0, 0, 3236.3872d, 3335.9548d, 4705.0835d },
            new double[] { 11, 0, 0, 0, 3128.2961d, 3115.422d, 6136.98d },
            new double[] { 11, 1, 0, 0, 3270.837d, 3257.6155d, 6414.523d },
            new double[] { 11, 2, 0, 0, 2998.9946d, 3091.259d, 4359.9604d },
            new double[] { 12, 0, 0, 0, 4573.858d, 4559.687d, 8931.938d },
            new double[] { 12, 1, 0, 0, 2522.779d, 2512.5815d, 4947.4873d },
            new double[] { 12, 2, 0, 0, 2916.213d, 3005.9304d, 4239.612d },
            new double[] { 13, 0, 0, 0, 4852.2617d, 4838.4976d, 9464.318d },
            new double[] { 13, 1, 0, 0, 2817.1409d, 2805.7534d, 5524.768d },
            new double[] { 13, 2, 0, 0, 2707.789d, 2791.0945d, 3936.6038d },
            new double[] { 14, 0, 0, 0, 3832.4783d, 3819.6118d, 7492.927d },
            new double[] { 14, 1, 0, 0, 3081.8179d, 3069.3606d, 6043.833d },
            new double[] { 14, 2, 0, 0, 2771.1724d, 2856.4277d, 4028.7507d },
        };
    }
}
