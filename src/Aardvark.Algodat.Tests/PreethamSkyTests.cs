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

namespace Aardvark.Geometry.Tests;

[TestFixture]
public class PreethamSkyTests
{
    [Test]
    public void SamplingMatchesOriginalFormulaAcrossTurbiditySunAndViewDirections()
    {
        foreach (var turbidity in new[] { 1.9, 2.5, 4, 6, 10 })
        foreach (var theta in new[] { 0, Math.PI / 6, Math.PI / 4, Math.PI / 2 - 1e-9, Math.PI / 2 })
        foreach (var phi in new[] { 0, 1.2, 2 * Math.PI - 0.1 })
        {
            var sky = new PreethamSky(phi, theta, turbidity);
            foreach (var view in Views(phi, theta))
                Check(sky, phi, theta, turbidity, view, true);
        }
    }

    [Test]
    public void InterleavedInstancesRetainTheirOwnNormalization()
    {
        (double Phi, double Theta, double T)[] parameters = { (0.2, 0.0, 1.9), (2.7, 1.2, 10.0), (-0.8, 0.6, 3.0), (0.0, 1.57, 6.0) };
        var skies = parameters.Select(p => new PreethamSky(p.Phi, p.Theta, p.T)).ToArray();
        for (var round = 0; round < 4; round++)
        {
            // Construction of a different (including nonfinite) sky must not change older skies.
            var unrelated = new PreethamSky(round, round * 0.2, round == 2 ? double.NaN : round + 2);
            _ = unrelated.GetRadiance(V3d.OOI);
            foreach (var i in Enumerable.Range(0, skies.Length).Reverse())
            foreach (var view in Views(parameters[i].Phi, parameters[i].Theta))
                Check(skies[i], parameters[i].Phi, parameters[i].Theta, parameters[i].T, view, true);
        }
    }

    [Test]
    public void ExtremeAndNonfiniteInputsKeepOriginalIeeeResults()
    {
        (double Phi, double Theta, double T)[] parameters =
        {
            (0.0, 0.5, double.NaN), (0.0, 0.5, double.PositiveInfinity),
            (0.0, 0.5, double.NegativeInfinity), (0.0, 0.5, double.MaxValue),
            (0.0, 0.5, -2.0), (0.0, Math.PI, 10.0), (0.0, 1e150, 2.5),
            (0.0, double.NaN, 2.5), (0.0, double.PositiveInfinity, 2.5),
            (0.0, double.NegativeInfinity, 2.5), (double.NaN, 0.5, 2.5),
            (double.PositiveInfinity, 0.5, 2.5), (double.NegativeInfinity, 0.5, 2.5)
        };
        foreach (var p in parameters)
        {
            var sky = new PreethamSky(p.Phi, p.Theta, p.T);
            foreach (var view in Views(0, 0.5)) Check(sky, p.Phi, p.Theta, p.T, view, false);
        }
        var ordinary = new PreethamSky(0.2, 0.5, 2.5);
        foreach (var view in new[]
        {
            V3d.Zero, new V3d(0, 0, 2), new V3d(0, 0, -2),
            new V3d(double.NaN, 0, 1), new V3d(double.PositiveInfinity, 0, 1),
            new V3d(0, double.NegativeInfinity, 1)
        }) Check(ordinary, 0.2, 0.5, 2.5, view, false);
    }

    private static V3d[] Views(double phi, double theta)
        => new[]
        {
            V3d.OOI, -V3d.OOI, V3d.IOO, -V3d.IOO, V3d.OIO, -V3d.OIO,
            new V3d(1, 2, 3).Normalized, new V3d(2, -1, -0.01).Normalized,
            Sky.V3dFromPhiTheta(0.7, Math.PI / 2 - 1e-12),
            Sky.V3dFromPhiTheta(0.7, Math.PI / 2 + 1e-12),
            Sky.V3dFromPhiTheta(phi, theta), -Sky.V3dFromPhiTheta(phi, theta)
        };

    private static void Check(PreethamSky sky, double phi, double theta, double turbidity, V3d view, bool finite)
    {
        var context = $"T={turbidity:R}, sun=({phi:R},{theta:R}), view={view}";
        var expected = OriginalFormula(phi, theta, turbidity, view);
        var actual = sky.Preetham(view);
        var radiance = sky.GetRadiance(view);
        var expectedRadiance = expected.ToC3f();
        var color = sky.GetColor(view);
        // The public conversion helper is unchanged; keep the legacy float conversion boundary.
        var expectedColor = PreethamSky.XYZTosRGBScaledToFit((expected * 0.0006 / 318.0).ToC3f());
        var doubles = new[] { (actual.R, expected.R), (actual.G, expected.G), (actual.B, expected.B) };
        var floats = new[]
        {
            (radiance.R, expectedRadiance.R), (radiance.G, expectedRadiance.G), (radiance.B, expectedRadiance.B),
            (color.R, expectedColor.R), (color.G, expectedColor.G), (color.B, expectedColor.B)
        };
        foreach (var (value, reference) in doubles)
        {
            if (finite) Assert.That(double.IsFinite(reference) && double.IsFinite(value), Is.True, context + ", finite XYZ");
            if (double.IsNaN(reference)) Assert.That(double.IsNaN(value), Is.True, context + ", XYZ NaN");
            else Assert.That(BitConverter.DoubleToInt64Bits(value), Is.EqualTo(BitConverter.DoubleToInt64Bits(reference)), context + ", XYZ bits");
        }
        foreach (var (value, reference) in floats)
        {
            if (finite) Assert.That(float.IsFinite(reference) && float.IsFinite(value), Is.True, context + ", finite radiance/color");
            if (float.IsNaN(reference)) Assert.That(float.IsNaN(value), Is.True, context + ", radiance/color NaN");
            else Assert.That(BitConverter.SingleToInt32Bits(value), Is.EqualTo(BitConverter.SingleToInt32Bits(reference)), context + ", radiance/color bits");
        }
    }

    // The uncached original formula is the compatibility oracle, not a new sky model.
    private static C3d OriginalFormula(double phi, double theta, double t, V3d view)
    {
        var chi = (4.0 / 9.0 - t / 120.0) * (Constant.Pi - 2 * theta);
        var yz = (4.0453 * t - 4.9710) * Math.Tan(chi) - 0.2155 * t + 2.4192;
        if (yz < 0) yz = 0;
        var t2 = t * t; var s2 = theta * theta; var s3 = s2 * theta;
        var xz = t2 * (s3 * 0.0017 + s2 * -0.0037 + theta * 0.0021 + 0.0000) +
                 t * (s3 * -0.0290 + s2 * 0.0638 + theta * -0.0320 + 0.0039) +
                     (s3 * 0.1169 + s2 * -0.2120 + theta * 0.0605 + 0.2589);
        var cz = t2 * (s3 * 0.0028 + s2 * -0.0061 + theta * 0.0032 + 0.0000) +
                 t * (s3 * -0.0421 + s2 * 0.0897 + theta * -0.0415 + 0.0052) +
                     (s3 * 0.1535 + s2 * -0.2676 + theta * 0.0667 + 0.2669);
        var v = Fun.Acos(view.Z);
        var gamma = Fun.Acos(Fun.Clamp(Vec.Dot(view, Sky.V3dFromPhiTheta(phi, theta)), -1, 1));
        return new C3f(yz * Distribution(Coefficients[0], t, v, theta, gamma),
                       xz * Distribution(Coefficients[1], t, v, theta, gamma),
                       cz * Distribution(Coefficients[2], t, v, theta, gamma)).FromYxyToXYZ().ToC3d() * 1000;
    }

    private static readonly double[][] Coefficients =
    {
        new[] { -1.4630, 0.4275, 5.3251, -2.5771, 0.3703, 0.1787, -0.3554, -0.0227, 0.1206, -0.0670 },
        new[] { -0.2592, 0.0008, 0.2125, -0.8989, 0.0452, -0.0193, -0.0665, -0.0004, -0.0641, -0.0033 },
        new[] { -0.2608, 0.0092, 0.2102, -1.6537, 0.0529, -0.0167, -0.0950, -0.0079, -0.0441, -0.0109 }
    };

    private static double Distribution(double[] xs, double t, double theta, double sunTheta, double gamma)
    {
        var a = xs[0] + xs[5] * t; var b = xs[1] + xs[6] * t; var c = xs[2] + xs[7] * t;
        var d = xs[3] + xs[8] * t; var e = xs[4] + xs[9] * t;
        return Perez(a, b, c, d, e, theta, gamma) / Perez(a, b, c, d, e, 0, sunTheta);
    }

    private static double Perez(double a, double b, double c, double d, double e, double theta, double gamma)
    {
        var cosG = Math.Cos(gamma); var cosT = Fun.Max(Math.Cos(theta), 0);
        return (1.0 + a * Math.Exp(b / (cosT + 0.001))) * (1.0 + c * Math.Exp(d * gamma) + e * cosG * cosG);
    }
}
