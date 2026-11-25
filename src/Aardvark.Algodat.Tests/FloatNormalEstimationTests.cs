/*
    Copyright (C) 2006-2026. Aardvark Platform Team. http://github.com/aardvark-platform.
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.
    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License
    along with this program. If not, see <http://www.gnu.org/licenses/>.
*/
using Aardvark.Base;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Aardvark.Geometry.Tests;

[TestFixture]
public class FloatNormalEstimationTests
{
    [Test]
    public async Task PlaneNormalsStayAxisAlignedAfterTranslation()
    {
        foreach (var family in Families)
        foreach (var axis in new[] { 0, 1, 2 })
        foreach (var offset in new[] { 0f, family.Offset, -family.Offset })
        {
            var points = Plane(family.Side, axis, offset);
            var snapshot = (V3f[])points.Clone();
            var doubles = points.Select(p => (V3d)p).ToArray();
            var reference = doubles.EstimateNormals(points.Length, doubles.BuildKdTree());
            CheckNormals(reference, axis, $"double reference side={family.Side}, axis={axis}, offset={offset:R}");
            var tree = points.BuildKdTree();
            foreach (var temporary in new[] { false, true })
            foreach (var asynchronous in new[] { false, true })
            {
                var context = $"side={family.Side}, axis={axis}, offset={offset:R}, temporary={temporary}, async={asynchronous}";
                // An oversized k must still clamp to the full input neighborhood.
                var actual = asynchronous
                    ? temporary ? await points.EstimateNormalsAsync(points.Length + 7) : await points.EstimateNormalsAsync(points.Length + 7, tree)
                    : temporary ? points.EstimateNormals(points.Length + 7) : points.EstimateNormals(points.Length + 7, tree);
                Assert.That(actual.Length, Is.EqualTo(points.Length), context);
                CheckNormals(actual, axis, context);
                CompareNormals(actual, reference, context);
                Unchanged(points, snapshot, context);
            }
        }
    }

    [Test]
    public async Task PlaneDensitiesUseCentroidRelativeDoubleDistances()
    {
        foreach (var family in Families)
        foreach (var axis in new[] { 0, 1, 2 })
        foreach (var offset in new[] { 0f, family.Offset, -family.Offset })
        {
            var points = Plane(family.Side, axis, offset);
            var snapshot = (V3f[])points.Clone();
            var doubles = points.Select(p => (V3d)p).ToArray();
            var reference = doubles.EstimateNormalsAndLocalDensity(points.Length, doubles.BuildKdTree());
            var expected = (family.Side * family.Side - 1) / 6.0;
            Assert.That(double.IsFinite(expected) && expected > 0, Is.True, "analytic population spread");
            CheckDensities(reference.densities, expected, "double reference");
            CheckNormals(reference.normals, axis, "double reference");
            var tree = points.BuildKdTree();
            foreach (var temporary in new[] { false, true })
            foreach (var asynchronous in new[] { false, true })
            {
                var context = $"side={family.Side}, axis={axis}, offset={offset:R}, temporary={temporary}, async={asynchronous}";
                var actual = asynchronous
                    ? temporary ? await points.EstimateNormalsAndLocalDensityAsync(points.Length) : await points.EstimateNormalsAndLocalDensityAsync(points.Length, tree)
                    : temporary ? points.EstimateNormalsAndLocalDensity(points.Length) : points.EstimateNormalsAndLocalDensity(points.Length, tree);
                Assert.That(actual.normals.Length, Is.EqualTo(points.Length), context);
                Assert.That(actual.densities.Length, Is.EqualTo(points.Length), context);
                CheckDensities(actual.densities, expected, context);
                CheckNormals(actual.normals, axis, context);
                CompareNormals(actual.normals, reference.normals, context);
                for (var i = 0; i < points.Length; i++)
                    Assert.That(actual.densities[i], Is.EqualTo(reference.densities[i]).Within(2e-6 * expected), context + $", double density point={i}");
                Unchanged(points, snapshot, context);
            }
        }
    }

    [Test]
    public async Task ValidationSmallInputsAndListForwardingKeepExistingContracts()
    {
        var plane = Plane(3, 2, 0);
        var tree = plane.BuildKdTree();
        var missing = (V3f[])null;
        Assert.Throws<ArgumentNullException>(() => missing.EstimateNormals(3, tree));
        Assert.Throws<ArgumentNullException>(() => missing.EstimateNormalsAndLocalDensity(3, tree));
        foreach (var k in new[] { -1, 0, 2 })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => plane.EstimateNormals(k, tree), $"k={k}");
            Assert.Throws<ArgumentOutOfRangeException>(() => plane.EstimateNormalsAndLocalDensity(k, tree), $"k={k}");
            Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await plane.EstimateNormalsAsync(k, tree), $"async k={k}");
            Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await plane.EstimateNormalsAndLocalDensityAsync(k, tree), $"async density k={k}");
        }
        Assert.Throws<ArgumentNullException>(() => plane.EstimateNormals(3, null));
        Assert.Throws<ArgumentNullException>(() => plane.EstimateNormalsAndLocalDensity(3, null));
        foreach (var count in new[] { 0, 1, 2, 3 })
        {
            var points = new[] { V3f.Zero, V3f.IOO, V3f.OIO }.Take(count).ToArray();
            var snapshot = (V3f[])points.Clone();
            var kd = points.BuildKdTree();
            var doubles = points.Select(p => (V3d)p).ToArray();
            var reference = doubles.EstimateNormalsAndLocalDensity(16, doubles.BuildKdTree());
            var actual = points.EstimateNormalsAndLocalDensity(16, kd);
            var asynchronous = await points.EstimateNormalsAndLocalDensityAsync(16);
            Assert.That(actual.normals, Has.Length.EqualTo(count), $"count={count}");
            Assert.That(actual.densities, Has.Length.EqualTo(count), $"count={count}");
            CompareNormals(actual.normals, reference.normals, $"count={count}");
            CompareNormals(points.EstimateNormals(16, kd), reference.normals, $"normals count={count}");
            CompareNormals(await points.EstimateNormalsAsync(16), reference.normals, $"async count={count}");
            CompareNormals(asynchronous.normals, reference.normals, $"async density count={count}");
            for (var i = 0; i < count; i++)
            {
                Assert.That(actual.densities[i], Is.EqualTo(reference.densities[i]).Within(1e-6), $"count={count}, point={i}");
                Assert.That(asynchronous.densities[i], Is.EqualTo(actual.densities[i]), $"async count={count}, point={i}");
            }
            Unchanged(points, snapshot, $"count={count}");
        }
        foreach (IList<V3f> list in new IList<V3f>[] { plane, plane.ToList(), Array.AsReadOnly(plane) })
        {
            var context = list.GetType().Name;
            CheckNormals(list.EstimateNormals(plane.Length), 2, context);
            CheckNormals(list.EstimateNormals(plane.Length, tree), 2, context + ", supplied");
            CheckNormals(await list.EstimateNormalsAsync(plane.Length), 2, context + ", async");
        }
    }

    private static readonly (int Side, float Offset)[] Families = { (3, 1e9f), (7, 1e8f), (16, 10000001f) };

    private static V3f[] Plane(int side, int axis, float offset)
        => Enumerable.Range(0, side * side).Select(i =>
        {
            var u = i % side - (side - 1) / 2f;
            var v = i / side - (side - 1) / 2f;
            // Translate only the normal coordinate so unit in-plane spacing remains representable.
            return axis == 0 ? new V3f(offset, u, v) : axis == 1 ? new V3f(u, offset, v) : new V3f(u, v, offset);
        }).ToArray();

    private static void CheckNormals(V3f[] normals, int axis, string context)
    {
        var expected = axis == 0 ? V3d.IOO : axis == 1 ? V3d.OIO : V3d.OOI;
        for (var i = 0; i < normals.Length; i++)
        {
            var n = (V3d)normals[i];
            Assert.That(double.IsFinite(n.X) && double.IsFinite(n.Y) && double.IsFinite(n.Z), Is.True, context + $", finite normal point={i}");
            Assert.That(n.LengthSquared, Is.EqualTo(1).Within(2e-6), context + $", unit normal point={i}");
            Assert.That(Math.Abs(Vec.Dot(n, expected)), Is.EqualTo(1).Within(2e-6), context + $", axis normal point={i}");
        }
    }

    private static void CompareNormals(V3f[] actual, V3f[] reference, string context)
    {
        Assert.That(actual.Length, Is.EqualTo(reference.Length), context);
        for (var i = 0; i < actual.Length; i++)
        {
            var a = (V3d)actual[i]; var b = (V3d)reference[i];
            Assert.That(double.IsFinite(a.LengthSquared) && double.IsFinite(b.LengthSquared), Is.True, context + $", finite reference point={i}");
            Assert.That(a.LengthSquared, Is.EqualTo(b.LengthSquared).Within(2e-6), context + $", normal length point={i}");
            Assert.That(Math.Abs(Vec.Dot(a, b)), Is.EqualTo(b.LengthSquared).Within(2e-6), context + $", double axis point={i}");
        }
    }

    private static void CheckDensities(float[] densities, double expected, string context)
    {
        for (var i = 0; i < densities.Length; i++)
        {
            Assert.That(float.IsFinite(densities[i]) && densities[i] > 0, Is.True, context + $", finite positive density point={i}");
            Assert.That((double)densities[i], Is.EqualTo(expected).Within(2e-6 * expected), context + $", density point={i}");
        }
    }

    private static void Unchanged(V3f[] actual, V3f[] snapshot, string context)
    {
        for (var i = 0; i < actual.Length; i++)
            Assert.That(new[] { BitConverter.SingleToInt32Bits(actual[i].X), BitConverter.SingleToInt32Bits(actual[i].Y), BitConverter.SingleToInt32Bits(actual[i].Z) },
                Is.EqualTo(new[] { BitConverter.SingleToInt32Bits(snapshot[i].X), BitConverter.SingleToInt32Bits(snapshot[i].Y), BitConverter.SingleToInt32Bits(snapshot[i].Z) }), context + $", source bits point={i}");
    }
}
