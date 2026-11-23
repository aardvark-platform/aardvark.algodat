/*
    Aardvark Platform
    Copyright (C) 2006-2026 Aardvark Platform Team
    https://aardvark.graphics

    Licensed under the Apache License, Version 2.0 (the "License");
    you may not use this file except in compliance with the License.
    You may obtain a copy of the License at
        http://www.apache.org/licenses/LICENSE-2.0
    Unless required by applicable law or agreed to in writing, software
    distributed under the License is distributed on an "AS IS" BASIS,
    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
    See the License for the specific language governing permissions and
    limitations under the License.
*/
using Aardvark.Base;
using Aardvark.Physics.Sky;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Aardvark.Geometry.Tests;

[TestFixture]
public class SunLightScatteringTests
{
    [Test]
    public void CompleteRadianceMatchesOriginalWavelengthIntegration()
    {
        foreach (var turbidity in new[] { 1.9, 2.5, 6, 10 })
        foreach (var theta in new[] { 0, Math.PI / 6, Math.PI / 3, Math.PI / 2 - 1e-9, Math.PI / 2, Math.PI / 2 + 1e-9, 94 * Constant.RadiansPerDegree, Math.PI })
        foreach (var phi in new[] { 0, 1.2 })
        {
            var context = $"phi={phi:R}, theta={theta:R}, T={turbidity:R}";
            var expected = OriginalRadiance(theta, turbidity);
            if (theta <= Math.PI / 2) Assert.That(expected.G, Is.GreaterThan(0), context + ", positive visible luminance");
            Check(new SunLightScattering(phi, theta, turbidity).GetRadiance(), expected, true, context);
        }
    }

    [Test]
    public void ExtremeAndNonfiniteInputsKeepOriginalRadianceBehavior()
    {
        foreach (var turbidity in new[] { 0, -2, double.MaxValue, double.MinValue, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var theta in new[] { 0, 0.7, Math.PI / 2, Math.PI })
            Check(new SunLightScattering(0.3, theta, turbidity).GetRadiance(), OriginalRadiance(theta, turbidity), false,
                $"theta={theta:R}, T={turbidity:R}");
        foreach (var theta in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, double.MaxValue })
            Check(new SunLightScattering(0.3, theta, 2.5).GetRadiance(), OriginalRadiance(theta, 2.5), false, $"theta={theta:R}");
        // Azimuth and the base class's sun vector are not inputs to this integration.
        foreach (var phi in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            Check(new SunLightScattering(phi, 0.7, 2.5).GetRadiance(), OriginalRadiance(0.7, 2.5), true, $"phi={phi:R}");
    }

    [Test]
    public void RepeatedAndConcurrentCallsRemainIndependent()
    {
        var shared = new SunLightScattering(0.2, 0, 1.9);
        var skies = new[] { shared, new SunLightScattering(1, Math.PI / 2, 6), new SunLightScattering(-1, 1.1, 10), shared, new SunLightScattering(0, double.NaN, 2.5) };
        var expected = new[] { OriginalRadiance(0, 1.9), OriginalRadiance(Math.PI / 2, 6), OriginalRadiance(1.1, 10), OriginalRadiance(0, 1.9), OriginalRadiance(double.NaN, 2.5) };
        for (var round = 0; round < 8; round++)
        for (var i = 0; i < skies.Length; i++)
            Check(skies[i].GetRadiance(), expected[i], i != 4, $"serial round={round}, sky={i}");

        using var start = new Barrier(4);
        var workers = Enumerable.Range(0, 4).Select(worker => Task.Factory.StartNew(() =>
        {
            if (!start.SignalAndWait(TimeSpan.FromSeconds(20))) throw new TimeoutException("Scattering workers did not start together.");
            return Enumerable.Range(0, 16).Select(round => skies[(worker + round) % skies.Length].GetRadiance()).ToArray();
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
        Task.WaitAll(workers);
        for (var worker = 0; worker < workers.Length; worker++)
        for (var round = 0; round < workers[worker].Result.Length; round++)
        {
            var i = (worker + round) % skies.Length;
            Check(workers[worker].Result[round], expected[i], i != 4, $"worker={worker}, round={round}, sky={i}");
        }
    }

    private static void Check(C3d actual, C3d expected, bool finite, string context)
    {
        foreach (var (value, reference) in new[] { (actual.R, expected.R), (actual.G, expected.G), (actual.B, expected.B) })
        {
            if (finite) Assert.That(double.IsFinite(reference) && double.IsFinite(value), Is.True, context + ", finite XYZ");
            if (double.IsNaN(reference)) Assert.That(double.IsNaN(value), Is.True, context + ", NaN");
            else Assert.That(BitConverter.DoubleToInt64Bits(value), Is.EqualTo(BitConverter.DoubleToInt64Bits(reference)), context + ", XYZ bits");
        }
    }

    // Independent uncached integration: recompute both densities inside every wavelength's step loop.
    private static C3d OriginalRadiance(double theta, double turbidity)
    {
        var mass = 1.0 / (theta.Cos() + 0.15 * Fun.Pow(Fun.Max(0, 93.885 - theta.DegreesFromRadians()), -1.253));
        var dust = (0.6544 * turbidity - 0.651) * 1e-16;
        var spectrum = new double[41];
        for (var wavelength = 0; wavelength < spectrum.Length; wavelength++)
        {
            var lambda = (380 + 10 * wavelength) * 1e-9;
            var ozone = -Ozone[wavelength] * 0.35f * mass;
            var gas = (-1.41 * Gases[wavelength] * mass) / Fun.Pow(1 + 118.93 * Gases[wavelength] * mass, 0.45);
            var vapor = (-0.2385 * Water[wavelength] * 2.0f * mass) / Fun.Pow(1 + 20.07 * Water[wavelength] * 2.0f * mass, 0.45);
            var total = 1.0;
            total *= Fun.Exp(ozone + gas + vapor);
            var rayleigh = 8 * Constant.Pi.Pow(3) * (1.0003.Square() - 1).Square() / (3 * 2.545e25 * lambda.Pow(4)) * (6 + 3 * 0.035) / (6 - 7 * 0.035);
            var mie = 0.434 * dust * (Constant.PiTimesTwo / lambda).Square() * Scatter[wavelength];
            var airTransmission = 1.0; var dustTransmission = 1.0;
            for (var step = 0; step < 500; step++)
            {
                var height = theta.Cos() * (500 - step - 1) * 200;
                var airDensity = Fun.Exp(-height / 8000);
                airTransmission *= Fun.Exp(-rayleigh * airDensity * 200);
                var dustDensity = Fun.Exp(-height / 2000);
                dustTransmission *= Fun.Exp(-mie * dustDensity * 200 / 100);
            }
            total *= airTransmission * dustTransmission;
            spectrum[wavelength] = Solar[wavelength] * total;
        }
        var values = new Vector<double>(spectrum);
        var xyz = new C3d(values.DotProduct(new Vector<double>(SpectralData.Ciexyz31X_380_780_10nm)),
                          values.DotProduct(new Vector<double>(SpectralData.Ciexyz31Y_380_780_10nm)),
                          values.DotProduct(new Vector<double>(SpectralData.Ciexyz31Z_380_780_10nm)));
        xyz *= 10000;
        xyz /= 100;
        xyz *= 683;
        return xyz;
    }

    // Original spectral coefficients, sampled at 380..780 nm in 10 nm increments.
    private static readonly double[] Solar = { 1655.9, 1623.37, 2112.75, 2588.82, 2582.91, 2423.23, 2676.05, 2965.83, 3054.54, 3005.75, 3066.37, 2883.04, 2871.21, 2782.5, 2710.06, 2723.36, 2636.13, 2550.38, 2506.02, 2531.16, 2535.59, 2513.42, 2463.15, 2417.32, 2368.53, 2321.21, 2282.77, 2233.98, 2197.02, 2152.67, 2109.79, 2072.83, 2024.04, 1987.08, 1942.72, 1907.24, 1862.89, 1825.92, 0.0, 0.0, 0.0 };
    private static readonly double[] Scatter = { 0.650393, 0.653435, 0.656387, 0.657828, 0.660644, 0.662016, 0.663365, 0.665996, 0.667276, 0.668532, 0.669765, 0.670974, 0.67216, 0.673323, 0.674462, 0.675578, 0.67667, 0.677739, 0.678784, 0.678781, 0.679802, 0.6808, 0.681775, 0.681771, 0.682722, 0.683649, 0.683646, 0.68455, 0.684546, 0.685426, 0.686282, 0.686279, 0.687112, 0.687108, 0.687917, 0.687913, 0.688699, 0.688695, 0.688691, 0.689453, 0.689449 };
    private static readonly double[] Ozone = { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.003, 0.006, 0.009, 0.014, 0.021, 0.03, 0.04, 0.048, 0.063, 0.075, 0.085, 0.103, 0.12, 0.12, 0.115, 0.125, 0.12, 0.105, 0.09, 0.079, 0.067, 0.057, 0.048, 0.036, 0.028, 0.023, 0.018, 0.014, 0.011, 0.01, 0.009, 0.007, 0.004, 0.0 };
    private static readonly double[] Water = { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.016, 0.024, 0.0125, 1, 0.87, 0.061, 0.001, 1e-05, 1e-05, 0.0006 };
    private static readonly double[] Gases = { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 3.0, 0.21, 0.0 };
}
