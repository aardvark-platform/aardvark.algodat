/*
    Aardvark Platform
    Copyright (C) 2006-2025 Aardvark Platform Team
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
using System.Collections.Generic;

namespace Aardvark.Geometry.Tests;

[TestFixture]
public class AstronomyLocalFrameTests
{
    private static IEnumerable<(double Longitude, double Latitude)> Locations()
    {
        foreach (var longitude in new[] { 0.0, 90.0, 180.0, -90.0, -180.0 })
            yield return (longitude, 0);
        foreach (var latitude in new[] { -90.0, 90.0 })
        foreach (var longitude in new[] { 0.0, 90.0, -90.0, 180.0, -180.0, 16.37, -122.4194 })
            yield return (longitude, latitude);
        foreach (var location in new[]
        {
            (16.37, 48.21), (-122.4194, 37.7749), (151.2093, -33.8688), (18.4241, -33.9249),
            (139.6917, 35.6895), (-70.6693, -33.4489), (45.0, 0.125), (-45.0, -0.125),
            (179.999, 89.999), (-179.999, -89.999), (180.0, 45.0), (-180.0, -45.0),
            (73.5, 12.25), (-73.5, -12.25)
        }) yield return location;
    }

    private static readonly V3d[] Vectors =
    {
        V3d.Zero, V3d.IOO, V3d.OIO, V3d.OOI,
        new(1.25, -2.5, 4.75), new(-7, 11, -13),
        new(1e-9, -2e-9, 3e-9), new(6378137, -2345678, 1234567)
    };

    [Test]
    public void GeographicBasisMapsToPositiveEastNorthAndUp()
    {
        Assert.Multiple(() =>
        {
            foreach (var (longitude, latitude) in Locations())
            {
                var (east, north, up) = EarthFixedBasis(longitude, latitude);
                var frame = Astronomy.ITRFtoLocal(longitude, latitude);
                var context = $"longitude={longitude}, latitude={latitude}";
                Near(frame * east, V3d.IOO, 2e-14, context + ", east -> +X");
                Near(frame * north, V3d.OIO, 2e-14, context + ", north -> +Y");
                Near(frame * up, V3d.OOI, 2e-14, context + ", up -> +Z");
            }
        });
    }

    [Test]
    public void ArbitraryEarthFixedVectorsAgreeWithAnalyticEnuMatrix()
    {
        Assert.Multiple(() =>
        {
            foreach (var (longitude, latitude) in Locations())
            {
                var frame = Astronomy.ITRFtoLocal(longitude, latitude);
                var lon = longitude * Math.PI / 180;
                var lat = latitude * Math.PI / 180;
                var sl = Math.Sin(lon); var cl = Math.Cos(lon);
                var sp = Math.Sin(lat); var cp = Math.Cos(lat);
                foreach (var vector in Vectors)
                {
                    // Direct ECEF-to-ENU rows, independent of the rotation helpers/composition.
                    var expected = new V3d(
                        -sl * vector.X + cl * vector.Y,
                        -sp * cl * vector.X - sp * sl * vector.Y + cp * vector.Z,
                        cp * cl * vector.X + cp * sl * vector.Y + sp * vector.Z);
                    Near(frame * vector, expected, 5e-14 * Math.Max(1, vector.Length),
                        $"longitude={longitude}, latitude={latitude}, vector={vector}");
                }
            }
        });
    }

    [Test]
    public void FramesRemainOrthonormalRightHandedAndInvertWithTranspose()
    {
        foreach (var (longitude, latitude) in Locations())
        {
            var frame = Astronomy.ITRFtoLocal(longitude, latitude);
            var x = frame * V3d.IOO; var y = frame * V3d.OIO; var z = frame * V3d.OOI;
            var context = $"longitude={longitude}, latitude={latitude}";
            Finite(x, context); Finite(y, context); Finite(z, context);
            foreach (var axis in new[] { x, y, z })
                Assert.That(Vec.Dot(axis, axis), Is.EqualTo(1).Within(2e-14), context + ", unit column");
            Assert.That(Vec.Dot(x, y), Is.EqualTo(0).Within(2e-14), context + ", orthogonal XY");
            Assert.That(Vec.Dot(y, z), Is.EqualTo(0).Within(2e-14), context + ", orthogonal YZ");
            Assert.That(Vec.Dot(z, x), Is.EqualTo(0).Within(2e-14), context + ", orthogonal ZX");
            Near(Vec.Cross(x, y), z, 2e-14, context + ", right handed");
            Assert.That(Vec.Dot(x, Vec.Cross(y, z)), Is.EqualTo(1).Within(2e-14), context + ", determinant +1");
            foreach (var vector in Vectors)
            {
                var local = frame * vector;
                Finite(local, context);
                Assert.That(local.Length, Is.EqualTo(vector.Length).Within(5e-14 * Math.Max(1, vector.Length)), context + ", length");
                Near(frame.Transposed * local, vector, 5e-14 * Math.Max(1, vector.Length), context + $", ITRF round trip {vector}");
                Near(frame * (frame.Transposed * vector), vector, 5e-14 * Math.Max(1, vector.Length), context + $", local round trip {vector}");
            }
        }
    }

    private static (V3d East, V3d North, V3d Up) EarthFixedBasis(double longitude, double latitude)
    {
        var lon = longitude * Math.PI / 180;
        var lat = latitude * Math.PI / 180;
        var east = new V3d(-Math.Sin(lon), Math.Cos(lon), 0);
        var north = new V3d(-Math.Sin(lat) * Math.Cos(lon), -Math.Sin(lat) * Math.Sin(lon), Math.Cos(lat));
        var up = new V3d(Math.Cos(lat) * Math.Cos(lon), Math.Cos(lat) * Math.Sin(lon), Math.Sin(lat));
        Finite(east, "east reference"); Finite(north, "north reference"); Finite(up, "up reference");
        return (east, north, up);
    }

    private static void Near(V3d actual, V3d expected, double tolerance, string context)
    {
        Finite(expected, context + ", expected");
        Finite(actual, context + ", actual");
        Assert.That((actual - expected).Length, Is.LessThanOrEqualTo(tolerance), context);
    }

    private static void Finite(V3d vector, string context)
        => Assert.That(double.IsFinite(vector.X) && double.IsFinite(vector.Y) && double.IsFinite(vector.Z), Is.True, context);
}
