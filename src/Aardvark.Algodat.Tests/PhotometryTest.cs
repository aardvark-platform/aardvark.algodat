using Aardvark.Base;
using Aardvark.Data.Photometry;
using NUnit.Framework;
using System.IO;
using System.Linq;

namespace Aardvark.Data.Photometry
{
    [TestFixture]
    public class PhotometryTest
    {
        static readonly string PhotometryDataPath = @"C:\Users\luksch\Desktop\Photometry Test Files";

        [Test]
        public void GetCPlaneTest()
        {
            var ldtC1 = new LDTData()
            {
                LampSets = new LDTLampData[0],
                HorizontalAngles = new double[] { 0, 30, 60, 90, 120, 150, 180, 210, 240, 270, 300, 330 },
                VerticleAngles = new double[] {0, 45, 90, 135, 180 },
                VertAngleStep = 45,
                Symmetry = LDTSymmetry.C1,
                Data = new Matrix<double>(5, 7).SetByCoord((x, y) => y),
            };

            var rows = new double[] { 3, 4, 5, 6, 5, 4, 3, 2, 1, 0, 1, 2 };

            EvaluatePlanes(ldtC1, rows);

            var ldtQuarter = new LDTData()
            {
                LampSets = new LDTLampData[0],
                HorizontalAngles = new double[] { 0, 30, 60, 90, 120, 150, 180, 210, 240, 270, 300, 330 },
                VerticleAngles = new double[] { 0, 45, 90, 135, 180 },
                VertAngleStep = 45,
                Symmetry = LDTSymmetry.Quarter,
                Data = new Matrix<double>(5, 4).SetByCoord((x, y) => y),
            };

            var rowsQuarter = new double[] { 0, 1, 2, 3, 2, 1, 0, 1, 2, 3, 2, 1 };

            EvaluatePlanes(ldtQuarter, rowsQuarter);
        }

        static void EvaluatePlanes(LDTData ldt, double[] rows)
        {
            var data = new LightMeasurementData(ldt);

            for (int i = 0; i < rows.Length; i++)
            {
                var angle = ldt.HorizontalAngles[i];
                var p = data.GetCPlane(angle);
                var row = p.First().Item2;
                var rowRef = rows[i];
                var correct = row == rowRef;
                Assert.IsTrue(correct);
            }
        }

        [Test, Ignore("Requires Data Files")]
        public void ZonesTest()
        {
            //{
            //    var file = @"C:\Users\Lui\Downloads\60715074_(STD).ldt";
            //    var zones = new[] { 249.1, 351.3, 130.2, 13.4, 3.4, 1.8, 0.6, 0, 0,
            //                          0,     0,     0,    0,   0,     0, 0,   0, 0 };
            //    EvaluateZones(file, zones);
            //}

            //{
            //    var file = @"C:\Users\Lui\Downloads\60714706_(STD).ldt";
            //    var zones = new[] { 114.9, 297.0, 358.8, 303.4, 186.6, 94.5, 32.0, 5.2, 0.3,
            //                          0,     0,     0,    0,      0,    0,    0,   0,   0 };
            //    EvaluateZones(file, zones);
            //}

            foreach (var f in Directory.GetFiles(PhotometryDataPath, "*.ldt"))
            {
                EvaluateZonesSampling(f);
            }
        }

        static void EvaluateZones(string file, double[] refZones)
        {
            var data = LightMeasurementData.FromFile(file);
            var zones = data.CalculateZones();
            var sampler = new IntensityProfileSampler(data);
            var zonesSampled = sampler.SampleZones(16000);

            Assert.True(zones.Length == 18);

            var totFluxRef = data.LumFlux;
            var totFlux = zones.Sum();
            var totFluxSampled = zonesSampled.Sum();

            var fluxRatio = totFlux / totFluxRef;
            var fluxRatioSampled = totFluxSampled / totFluxRef;

            Assert.True(fluxRatio.ApproximateEquals(1, 1e-2));
            Assert.True(fluxRatioSampled.ApproximateEquals(1, 1e-2));

            var e = data.LumFlux * 1e-2;

            for (int i = 0; i < 18; i++)
            {
                Assert.True(zones[i].ApproximateEquals(refZones[i], e));
                Assert.True(zonesSampled[i].ApproximateEquals(refZones[i], e));

                var ratio = refZones[i] > 0 ? zones[i] / refZones[i] : zones[i] > 0 ? double.MaxValue : 1.0;
                if (!ratio.ApproximateEquals(1.0, 0.05))
                    Report.Warn("Zone {0} Difference", i);

                var ratio2 = refZones[i] > 0 ? zonesSampled[i] / refZones[i] : zonesSampled[i] > 0 ? double.MaxValue : 1.0;
                if (!ratio2.ApproximateEquals(1.0, 0.05))
                    Report.Warn("Zone {0} Difference Sampled", i);
            }
        }

        static void EvaluateZonesSampling(string file)
        {
            Report.Line("Evaluating Zones: {0}", Path.GetFileName(file));

            var data = LightMeasurementData.FromFile(file);

            Report.Line("Info: Symmetry={0} VerticalRange={1}", data.HorizontalSymmetry, data.VerticalRange);

            var zones = data.CalculateZones();
            var sampler = new IntensityProfileSampler(data);
            var zonesSampled = sampler.SampleZones(16000);

            Assert.True(zones.Length == 18);

            var totFlux = zones.Sum();
            var totFluxSampled = zonesSampled.Sum();

            var fluxRatio = totFlux / totFluxSampled;

            Assert.True(fluxRatio.ApproximateEquals(1, 1e-2));

            var e = data.LumFlux * 1e-2;

            var rmsError = 0.0;

            for (int i = 0; i < 18; i++)
            {
                var error = zonesSampled[i] > 0 ? 1 - zones[i] / zonesSampled[i] : zones[i] > 0 ? 1 : 0;
                rmsError += error.Square();

                Report.Line("Zone {0} Error={1:0.00}%", i, (error).Abs() * 100);

                Assert.True(zones[i].ApproximateEquals(zonesSampled[i], e));
                Assert.True(zonesSampled[i].ApproximateEquals(zonesSampled[i], e));

                var ratio = zonesSampled[i] > 0 ? zones[i] / zonesSampled[i] : zones[i] > 0 ? double.MaxValue : 1.0;
                if (!ratio.ApproximateEquals(1.0, 0.05))
                    Report.Warn("Zone {0} Difference", i);
            }

            Report.Line("RMS Error={0:0.00000}", (rmsError / 18).Sqrt());
        }

        [Test, Ignore("Requires Data Files")]
        public void LumFluxTest()
        {
            foreach (var f in Directory.GetFiles(PhotometryDataPath, "*.ldt"))
            {
                EvaluateLumFlux(f);
            }
        }

        static void EvaluateLumFlux(string file)
        {
            Report.Line("Evaluating Lum Flux: {0}", Path.GetFileName(file));

            var data = LightMeasurementData.FromFile(file);

            Report.Line("Info: Symmetry={0} VerticalRange={1}", data.HorizontalSymmetry, data.VerticalRange);

            var eqMtx = data.BuildEquidistantMatrix();
            var lumFluxCalc = data.CalculateLumFlux();
            var lumFluxCalcEq = LightMeasurementData.CalculateLumFlux(eqMtx, data.VerticalRange);
            var sampler = new IntensityProfileSampler(data);
            var lumFluxSample = sampler.SampleLumFlux(16000);

            var ratioCalc = lumFluxCalc / data.LumFlux;
            var ratioCalcEq = lumFluxCalcEq / data.LumFlux;
            var ratioSample = lumFluxSample / data.LumFlux;

            var errorCalc = (1 - ratioCalc).Abs();
            var errorCalcEq = (1 - ratioCalcEq).Abs();
            var errorSample = (1 - ratioSample).Abs();

            var better = errorCalc < errorCalcEq ? "Calc" : "CalcEq";
            Report.Line("{0} Calculation More Accurate [Calc Error={1:0.00}%, CalcEq Error={2:0.00}% Sample={3:0.00}%", better, errorCalc * 100, errorCalcEq * 100, errorSample * 100);

            Assert.True(ratioCalc.ApproximateEquals(1, 0.05), "Luminous flux calculation broken or photometry file contains invalid data");
            Assert.True(ratioCalcEq.ApproximateEquals(1, 0.12), "Luminous flux calculation broken or photometry file contains invalid data");
            Assert.True(ratioSample.ApproximateEquals(1, 0.05), "Luminous flux calculation broken or photometry file contains invalid data");
        }

        public static void TestDirection(double gamma, double c, V3d dir)
        {
            var (c2, g2) = IntensityProfileSampler.CartesianToSpherical(dir);
            if (!c2.ApproximateEquals(c, 1e-7) || !g2.ApproximateEquals(gamma, 1e-7))
                Report.Line("FAIL");
            var v = IntensityProfileSampler.SphericalToCartesian(c, gamma);
            if (!v.ApproximateEquals(dir, 1e-7))
                Report.Line("FAIL");
        }

        [Test]
        public static void CoordinateSystem()
        {
            // gamma 0° / -Z-Axis
            TestDirection(0, Constant.Pi, -V3d.ZAxis); // c angle is undefined, but outcome will be 180°
                                                        // gamma 180° / Z-Axis
            TestDirection(Constant.Pi, 0, V3d.ZAxis); // c angle is undefined, but outcome will be 0°

            // gamma 90° + C=0° / X-Axis
            TestDirection(Constant.PiHalf, 0, V3d.XAxis);
            // gamma 90° + C=90° / Y-Axis
            TestDirection(Constant.PiHalf, Constant.PiHalf, V3d.YAxis);
            // gamma 90° + C=180° / -X-Axis
            TestDirection(Constant.PiHalf, Constant.Pi, -V3d.XAxis);
            // gamma 90° + C=270° / -Y-Axis
            TestDirection(Constant.PiHalf, Constant.Pi * 3 / 2, -V3d.YAxis);

            var rnd = new RandomSystem(2143);
            for (int i = 0; i < 1000; i++)
            {
                var v = RandomSample.Spherical(rnd.UniformDouble(), rnd.UniformDouble());

                var (c, gamma) = IntensityProfileSampler.CartesianToSpherical(v);
                var v2 = IntensityProfileSampler.SphericalToCartesian(c, gamma);

                if (!v.ApproximateEquals(v2, 1e-3))
                    Report.Line("FAIL");
                else
                    Report.Line("OK");
            }
        }
    }
}
