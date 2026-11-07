using Aardvark.Base;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry.Tests.PolyMeshTests;

[TestFixture]
public class CylinderFacesTests
{
    private static readonly C4b Color = new(23, 71, 139, 201);
    private static readonly Symbol U = PolyMesh.Property.DiffuseColorUTangents;
    private static readonly Symbol V = PolyMesh.Property.DiffuseColorVTangents;

    private static IEnumerable<(int n, double h, double r, Symbol uv)> Settings()
    {
        foreach (var n in new[] { 3, 4, 8, 32 })
        foreach (var dimensions in new[] { (h: 1.0, r: 1.0), (h: 4.0, r: .5), (h: .5, r: 3.0) })
        foreach (var uv in new[] { PolyMesh.Property.DiffuseColorCoordinates, (Symbol)"CylinderTestUv" })
            yield return (n, dimensions.h, dimensions.r, uv);
    }

    // Six-parameter calls with no tangents / sharing IDs; seven-parameter calls
    // with neither vector, U only, or U and V. Binormal-only selection is separate.
    private static PolyMesh Create(int n, double h, double r, Symbol uv, int mode) => mode switch
    {
        0 => PolyMeshPrimitives.Cylinder(n, h, r, Color, uv, Symbol.Empty),
        1 => PolyMeshPrimitives.Cylinder(n, h, r, Color, uv, U),
        2 => PolyMeshPrimitives.Cylinder(n, h, r, Color, uv, Symbol.Empty, Symbol.Empty),
        3 => PolyMeshPrimitives.Cylinder(n, h, r, Color, uv, U, Symbol.Empty),
        _ => PolyMeshPrimitives.Cylinder(n, h, r, Color, uv, U, V)
    };

    private static int[] Corners(int n)
    {
        var bottom = Enumerable.Range(0, n).SelectMany(j => new[] { 0, 1 + (j + 1) % n, 1 + j });
        var side = Enumerable.Range(0, n).SelectMany(j => new[]
        {
            1 + j, 1 + (j + 1) % n, n + 1 + j,
            1 + (j + 1) % n, n + 1 + (j + 1) % n, n + 1 + j
        });
        var top = Enumerable.Range(0, n).SelectMany(j => new[] { 2 * n + 1, n + 1 + j, n + 1 + (j + 1) % n });
        return bottom.Concat(side).Concat(top).ToArray();
    }

    private static int[] Sharing(int n)
    {
        var bottom = Enumerable.Range(0, n).SelectMany(j => new[] { 3 * j, j + 1 < n ? 3 * j + 1 : 2, j == 0 ? 2 : 3 * j - 2 });
        var side = Enumerable.Range(0, n).SelectMany(j => new[] { 3 * n + j, 3 * n + (j + 1) % n, 4 * n + j, 3 * n + (j + 1) % n, 4 * n + (j + 1) % n, 4 * n + j });
        var top = Enumerable.Range(0, n).SelectMany(j => new[] { 9 * n + 3 * j, 9 * n + (j == 0 ? 2 : 3 * j - 2), 9 * n + (j + 1 < n ? 3 * j + 1 : 2) });
        return bottom.Concat(side).Concat(top).ToArray();
    }

    private static void Near(object actual, object expected, double tolerance, string context)
    {
        if (expected is V3d p)
        {
            var a = actual is V3f f ? (V3d)f : (V3d)actual;
            Assert.That(double.IsFinite(p.LengthSquared) && double.IsFinite(a.LengthSquared), Is.True, context);
            Assert.That((a - p).Length, Is.LessThanOrEqualTo(tolerance), context);
        }
        else if (expected is V2d uv)
        {
            var a = actual is V2f f ? (V2d)f : (V2d)actual;
            Assert.That(double.IsFinite(uv.LengthSquared) && double.IsFinite(a.LengthSquared), Is.True, context);
            Assert.That((a - uv).Length, Is.LessThanOrEqualTo(tolerance), context);
        }
        else Assert.That(actual, Is.EqualTo(expected), context);
    }

    [Test]
    public void SixParameterFacesKeepPositionsCornerOrderUvsNormalsColorsAndSharing()
    {
        foreach (var (n, h, r, uv) in Settings())
        foreach (var mode in new[] { 0, 1 })
        Assert.Multiple(() =>
        {
            var context = $"n={n}, h={h}, r={r}, uv={uv}, mode={mode}";
            var mesh = Create(n, h, r, uv, mode);
            Assert.That(mesh.FaceCount, Is.EqualTo(4 * n), context);
            Assert.That(mesh.FirstIndexArray, Is.EqualTo(Enumerable.Range(0, 4 * n + 1).Select(i => 3 * i)), context);
            Assert.That(mesh.VertexIndexArray, Is.EqualTo(Corners(n)), context);
            Assert.That(mesh.VertexIndexCount, Is.EqualTo(12 * n), context);
            Assert.That(mesh.VertexCount, Is.EqualTo(2 * n + 2), context);
            Assert.That(mesh.FaceAttributes.Count, Is.Zero, context);
            Assert.That(mesh.FaceVertexAttributes.Keys, Is.EquivalentTo(mode == 0 ? new[] { uv, -uv, PolyMesh.Property.Normals } : new[] { uv, -uv, PolyMesh.Property.Normals, U }), context);
            var positions = new List<V3d> { new(0, 0, -.5 * h) };
            foreach (var z in new[] { -.5 * h, .5 * h })
                for (var j = 0; j < n; j++) positions.Add(new V3d(r * Math.Cos(2 * Math.PI * j / n), r * Math.Sin(2 * Math.PI * j / n), z));
            positions.Add(new V3d(0, 0, .5 * h));
            for (var i = 0; i < positions.Count; i++) Near(mesh.PositionArray[i], positions[i], 2e-14 * Math.Max(1, r), context + $", vertex={i}");
            Assert.That(mesh.VertexAttributes[PolyMesh.Property.Colors], Is.EqualTo(Enumerable.Repeat(Color, 2 * n + 2)), context);
            Assert.That(mesh.FaceVertexAttributes[uv], Is.EqualTo(Enumerable.Range(0, 12 * n)), context);
            var expectedUv = new List<V2d>();
            for (var j = 0; j < n; j++) expectedUv.AddRange(new[] { new V2d((j + .5) / n, 0), new V2d((j + 1.0) / n, 1), new V2d(j / (double)n, 1) });
            for (var j = 0; j < n; j++) expectedUv.AddRange(new[] { new V2d(j / (double)n, 1), new V2d((j + 1.0) / n, 1), new V2d(j / (double)n, 0), new V2d((j + 1.0) / n, 1), new V2d((j + 1.0) / n, 0), new V2d(j / (double)n, 0) });
            for (var j = 0; j < n; j++) expectedUv.AddRange(new[] { new V2d((j + .5) / n, 1), new V2d(j / (double)n, 0), new V2d((j + 1.0) / n, 0) });
            var coordinates = (V2d[])mesh.FaceVertexAttributes[-uv];
            var normals = (V3d[])mesh.FaceVertexAttributes[PolyMesh.Property.Normals];
            Assert.That(coordinates.Length, Is.EqualTo(12 * n), context);
            Assert.That(normals.Length, Is.EqualTo(12 * n), context);
            for (var i = 0; i < 12 * n; i++)
            {
                Near(coordinates[i], expectedUv[i], 2e-14, context + $", uv corner={i}");
                var side = i - 3 * n;
                var j = side / 6;
                var k = side % 6;
                var angle = 2 * Math.PI * (j + (k == 1 || k == 3 || k == 4 ? 1 : 0)) / n;
                var normal = i < 3 * n ? -V3d.ZAxis : i >= 9 * n ? V3d.ZAxis : new V3d(Math.Cos(angle), Math.Sin(angle), 0);
                Near(normals[i], normal, 2e-14, context + $", normal corner={i}");
            }
            if (mode == 1) Assert.That(mesh.FaceVertexAttributes[U], Is.EqualTo(Sharing(n)), context);
        });
    }

    private static void Surface(V3d[] positions, int[] indices, int n, double h, double r, double tolerance, string context)
    {
        var expectedArea = 2 * n * r * h * Math.Sin(Math.PI / n) + n * r * r * Math.Sin(2 * Math.PI / n);
        Assert.That(double.IsFinite(expectedArea) && expectedArea > 0, Is.True, context);
        Assert.That(indices.Length, Is.EqualTo(12 * n), context);
        var area = 0.0;
        var inward = 0;
        for (var i = 0; i < indices.Length; i += 3)
        {
            var a = positions[indices[i]];
            var b = positions[indices[i + 1]];
            var c = positions[indices[i + 2]];
            var cross = Vec.Cross(b - a, c - a);
            if (!(Vec.Dot(cross, (a + b + c) / 3) > 0)) inward++;
            area += .5 * cross.Length;
        }
        Assert.That(inward, Is.Zero, context + ", non-outward triangles");
        Assert.That(double.IsFinite(area), Is.True, context);
        Assert.That(area, Is.EqualTo(expectedArea).Within(expectedArea * tolerance), context + ", polygonal surface area");
    }

    [Test]
    public void SimpleTriangulationAndAllExportsHaveExactlyTheOutwardPolygonalSurface()
    {
        foreach (var (n, h, r, uv) in Settings())
        for (var mode = 0; mode < 5; mode++)
        Assert.Multiple(() =>
        {
            var context = $"n={n}, h={h}, r={r}, uv={uv}, mode={mode}";
            var mesh = Create(n, h, r, uv, mode);
            var corners = mesh.VertexIndexArray.ToArray();
            var attributes = mesh.FaceVertexAttributes.ToDictionary(x => x.Key, x => (Array)x.Value.Clone());
            var triangles = mesh.CreateSimpleTriangleVertexIndexArray();
            Assert.That(triangles, Is.EqualTo(Corners(n)), context);
            Surface(mesh.PositionArray, triangles, n, h, r, 2e-12, context + ", simple");
            for (var flags = 0; flags < 16; flags++)
            {
                var geometry = mesh.GetIndexedGeometry((PolyMesh.GetGeometryOptions)flags);
                var indices = geometry.IndexArray is int[] ints ? ints : ((short[])geometry.IndexArray).Select(i => (int)i).ToArray();
                var array = geometry.IndexedAttributes[PolyMesh.Property.Positions];
                var ps = array is V3d[] doubles ? doubles : ((V3f[])array).Select(p => (V3d)p).ToArray();
                var message = context + $", flags={flags}";
                Surface(ps, indices, n, h, r, 2e-6, message);
                foreach (var data in geometry.IndexedAttributes.Values) Assert.That(data.Length, Is.EqualTo(ps.Length), message + ", aligned export");
                if (indices.Length != 12 * n) continue;
                for (var i = 0; i < indices.Length; i++)
                {
                    Near(ps[indices[i]], mesh.PositionArray[corners[i]], 2e-6, message + $", position={i}");
                    Assert.That(geometry.IndexedAttributes[PolyMesh.Property.Colors].GetValue(indices[i]), Is.EqualTo(Color), message + $", color={i}");
                    foreach (var key in mesh.FaceVertexAttributes.Keys.Where(k => k.IsPositive))
                    {
                        var values = mesh.FaceVertexAttributes[key];
                        var expected = mesh.FaceVertexAttributes.TryGetValue(-key, out var pool) ? pool.GetValue(((int[])values)[i]) : values.GetValue(i);
                        Near(geometry.IndexedAttributes[key].GetValue(indices[i]), expected, 2e-6, message + $", attribute={key}, corner={i}");
                    }
                }
            }
            Assert.That(mesh.VertexIndexArray, Is.EqualTo(corners), context + ", source corners");
            foreach (var data in attributes) Assert.That(mesh.FaceVertexAttributes[data.Key], Is.EqualTo(data.Value), context + $", source attribute={data.Key}");
        });
    }

    [Test]
    public void TopologyIsClosedManifoldWithEulerTwoAcrossWorkingOverloads()
    {
        foreach (var (n, h, r, uv) in Settings())
        for (var mode = 0; mode < 5; mode++)
        Assert.Multiple(() =>
        {
            var context = $"n={n}, h={h}, r={r}, uv={uv}, mode={mode}";
            var mesh = Create(n, h, r, uv, mode);
            Assert.DoesNotThrow(() => mesh.BuildTopology(), context);
            if (!mesh.HasTopology) return;
            Assert.That(mesh.IsManifold, Is.True, context);
            Assert.That(mesh.BorderEdgeCount, Is.Zero, context);
            Assert.That(mesh.NonManifoldEdgeCount, Is.Zero, context);
            Assert.That(mesh.EdgeCount, Is.EqualTo(6 * n), context);
            Assert.That(mesh.VertexCount - mesh.EdgeCount + mesh.FaceCount, Is.EqualTo(2), context);
            Assert.That(mesh.EulerCharacteristic, Is.EqualTo(2), context);
            Assert.That(mesh.Faces.SelectMany(f => f.Edges).All(e => !e.OppositeIsBorder), Is.True, context);
        });
    }

    [Test]
    public void ForwardingTangentSelectionAndConstructionOwnershipKeepTheirContracts()
    {
        foreach (var (n, h, r, uv) in Settings())
        for (var mode = 2; mode < 5; mode++)
        Assert.Multiple(() =>
        {
            var context = $"n={n}, h={h}, r={r}, uv={uv}, mode={mode}";
            var reference = Create(n, h, r, uv, mode == 2 ? 0 : 1);
            if (mode != 2) reference.AddPerFaceVertexTangents(uv, U, mode == 4 ? V : Symbol.Empty, (int[])reference.FaceVertexAttributes[U]);
            var mesh = Create(n, h, r, uv, mode);
            Assert.That(mesh.FirstIndexArray, Is.EqualTo(reference.FirstIndexArray), context);
            Assert.That(mesh.VertexIndexArray, Is.EqualTo(reference.VertexIndexArray), context);
            Assert.That(mesh.PositionArray, Is.EqualTo(reference.PositionArray), context);
            Assert.That(mesh.PositionArray, Is.Not.SameAs(reference.PositionArray), context);
            Assert.That(mesh.FaceVertexAttributes.Keys, Is.EquivalentTo(reference.FaceVertexAttributes.Keys), context);
            foreach (var data in reference.FaceVertexAttributes)
            {
                Assert.That(mesh.FaceVertexAttributes[data.Key], Is.EqualTo(data.Value), context + $", attribute={data.Key}");
                Assert.That(mesh.FaceVertexAttributes[data.Key], Is.Not.SameAs(data.Value), context + $", ownership={data.Key}");
            }
        });
        foreach (var n in new[] { int.MinValue, -1, 0, 1, 2 })
        for (var mode = 0; mode < 5; mode++)
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => Create(n, 1, 1, PolyMesh.Property.DiffuseColorCoordinates, mode))?.ParamName, Is.EqualTo("tessellation"), $"n={n}, mode={mode}");
    }
}
