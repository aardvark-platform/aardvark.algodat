using Aardvark.Base;
using Aardvark.Rendering;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class BoxAttributesTests
    {
        private static readonly Symbol P = PolyMesh.Property.Positions, C = PolyMesh.Property.Colors;
        private static readonly Symbol UV = PolyMesh.Property.DiffuseColorCoordinates, N = PolyMesh.Property.Normals;
        private static readonly Symbol U = PolyMesh.Property.DiffuseColorUTangents, V = PolyMesh.Property.DiffuseColorVTangents;
        private static readonly int[] Corners = { 1, 2, 6, 5, 2, 3, 7, 6, 4, 5, 6, 7, 3, 0, 4, 7, 0, 1, 5, 4, 0, 3, 2, 1 };
        private static readonly V2d[] Uvs = { V2d.OI, V2d.II, V2d.IO, V2d.OO };
        private static readonly V3d[] Normals = { V3d.IOO, V3d.OIO, V3d.OOI, -V3d.IOO, -V3d.OIO, -V3d.OOI };
        private static readonly V3d[] Us = { V3d.OIO, -V3d.IOO, V3d.IOO, -V3d.OIO, V3d.IOO, V3d.OIO };
        private static readonly V3d[] Vs = { -V3d.OOI, -V3d.OOI, -V3d.OIO, -V3d.OOI, -V3d.OOI, -V3d.IOO };
        private static readonly Box3d[] Bounds = {
            new Box3d(V3d.Zero, V3d.III),
            new Box3d(new V3d(-3, -2, -1), new V3d(2, 4, 8)),
            new Box3d(new V3d(10.1, -20.2, 3.3), new V3d(10.6, -18.2, 7.3)),
            new Box3d(new V3d(1000, -2000, 3000), new V3d(1008, -1996, 3002))
        };

        [Test]
        public void DisabledShadingAttributesAreAbsentAcrossAllFlagBearingOverloads()
        {
            for (var mode = 0; mode < 4; mode++)
            foreach (var bounds in Bounds)
            for (var color = 0; color < 2; color++) CheckBox(Create(mode, bounds, color, false));
        }

        [Test]
        public void EnabledFramesAndForwardedDefaultsRetainGeometryUvsAndColors()
        {
            for (var mode = 0; mode < 4; mode++)
            foreach (var bounds in Bounds)
            for (var color = 0; color < 2; color++)
            {
                CheckBox(Create(mode, bounds, color, true));
                CheckBox(Create(mode, bounds, color, null));
            }
            for (var color = 0; color < 2; color++)
            foreach (var mode in new[] { 0, 2 })
            {
                var f = Create(mode, Bounds[0], color, true);
                var unit = mode == 0 ? PolyMeshPrimitives.Box(f.ByteColor) : PolyMeshPrimitives.Box(f.FloatColor);
                CheckBox(f with { Mesh = unit, Context = "unit-color default " + f.Context });
            }
        }

        [Test]
        public void ExportsAlignFlagDependentVectorsAndUnrelatedAttributesWithoutChangingTheirSources()
        {
            for (var mode = 0; mode < 4; mode++)
            foreach (var bounds in Bounds)
            foreach (var shading in new[] { false, true })
            {
                var f = Create(mode, bounds, 1, shading); var unchanged = Snapshot(f.Mesh, f.Context);
                for (var flags = 0; flags < 16; flags++)
                {
                    var options = (PolyMesh.GetGeometryOptions)flags;
                    CheckExport(f, f.Mesh.GetIndexedGeometry(options), Enumerable.Range(0, 6).ToArray(), options);
                    var selection = new[] { 5, 1, 5 };
                    CheckExport(f, f.Mesh.GetIndexedGeometry(selection, options), selection, options);
                }
                CheckExport(f, f.Mesh.GetIndexedGeometry(), Enumerable.Range(0, 6).ToArray(), PolyMesh.GetGeometryOptions.Default);
                unchanged();
            }
        }

        [Test]
        public void SeparateCallsOwnTheirArraysWhileEnabledTangentsShareOnlyTheirIdentityMap()
        {
            for (var mode = 0; mode < 4; mode++)
            foreach (var shading in new[] { false, true })
            {
                var a = Create(mode, Bounds[1], 0, shading); var b = Create(mode, Bounds[1], 0, shading); var context = a.Context;
                var unchanged = Snapshot(b.Mesh, context);
                Assert.That(a.Mesh.VertexIndexArray, Is.Not.SameAs(b.Mesh.VertexIndexArray), context);
                Assert.That(a.Mesh.FirstIndexArray, Is.Not.SameAs(b.Mesh.FirstIndexArray), context);
                var ad = new[] { a.Mesh.VertexAttributes, a.Mesh.FaceAttributes, a.Mesh.FaceVertexAttributes };
                var bd = new[] { b.Mesh.VertexAttributes, b.Mesh.FaceAttributes, b.Mesh.FaceVertexAttributes };
                for (var domain = 0; domain < ad.Length; domain++)
                {
                    Assert.That(ad[domain], Is.Not.SameAs(bd[domain]), context);
                    foreach (var pair in ad[domain])
                    {
                        Assert.That(pair.Value, Is.Not.SameAs(bd[domain][pair.Key]), context + ", " + pair.Key);
                        var array = pair.Value;
                        if (array is int[] ids) ids[0] = 123;
                        else if (array is V3d[] vectors) vectors[0] = new V3d(8, 9, 10);
                        else if (array is V2d[] uv) uv[0] = new V2d(8, 9);
                        else if (array is C4b[] cb) cb[0] = C4b.Black;
                        else if (array is C4f[] cf) cf[0] = new C4f(0, 0, 0, 0);
                    }
                }
                a.Mesh.VertexIndexArray[0] = 7; a.Mesh.FirstIndexArray[0] = 1; unchanged();
                var fresh = Create(mode, Bounds[1], 0, shading);
                var fd = new[] { fresh.Mesh.VertexAttributes, fresh.Mesh.FaceAttributes, fresh.Mesh.FaceVertexAttributes };
                for (var domain = 0; domain < fd.Length; domain++)
                    foreach (var pair in fd[domain]) Assert.That(pair.Value, Is.EqualTo(bd[domain][pair.Key]), context + ", later construction");
                if (shading)
                {
                    Assert.That(b.Mesh.FaceVertexAttributes[U], Is.SameAs(b.Mesh.FaceVertexAttributes[V]), context);
                    Assert.That(b.Mesh.FaceVertexAttributes[U], Is.Not.SameAs(b.Mesh.FaceVertexAttributes[UV]), context);
                    Assert.That(b.Mesh.FaceVertexAttributes[-U], Is.Not.SameAs(b.Mesh.FaceVertexAttributes[-V]), context);
                }
            }
        }

        private sealed record Fixture(PolyMesh Mesh, Box3d Bounds, bool FloatColors, C4b ByteColor, C4f FloatColor, bool Shading, string Context);
        private static Fixture Create(int mode, Box3d bounds, int color, bool? shading)
        {
            var cb = color == 0 ? new C4b(12, 34, 56, 78) : new C4b(203, 101, 17, 254);
            var cf = color == 0 ? new C4f(0.1f, 0.3f, 0.7f, 0.8f) : new C4f(0.75f, 0.5f, 0.25f, 0.125f);
            var bf = new Box3f((V3f)bounds.Min, (V3f)bounds.Max); var actualBounds = (mode & 1) != 0 ? (Box3d)bf : bounds;
            var mesh = mode switch {
                0 => shading.HasValue ? PolyMeshPrimitives.Box(bounds, cb, shading.Value) : PolyMeshPrimitives.Box(bounds, cb),
                1 => shading.HasValue ? PolyMeshPrimitives.Box(bf, cb, shading.Value) : PolyMeshPrimitives.Box(bf, cb),
                2 => shading.HasValue ? PolyMeshPrimitives.Box(bounds, cf, shading.Value) : PolyMeshPrimitives.Box(bounds, cf),
                _ => shading.HasValue ? PolyMeshPrimitives.Box(bf, cf, shading.Value) : PolyMeshPrimitives.Box(bf, cf)
            };
            return new Fixture(mesh, actualBounds, mode >= 2, cb, cf, shading ?? true, $"mode={mode}, bounds={actualBounds}, color={color}, shading={shading?.ToString() ?? "default"}");
        }
        private static V3d[] Positions(Box3d b) => new[] {
            new V3d(b.Min.X,b.Min.Y,b.Min.Z), new V3d(b.Max.X,b.Min.Y,b.Min.Z), new V3d(b.Max.X,b.Max.Y,b.Min.Z), new V3d(b.Min.X,b.Max.Y,b.Min.Z),
            new V3d(b.Min.X,b.Min.Y,b.Max.Z), new V3d(b.Max.X,b.Min.Y,b.Max.Z), new V3d(b.Max.X,b.Max.Y,b.Max.Z), new V3d(b.Min.X,b.Max.Y,b.Max.Z)
        };
        private static void CheckBox(Fixture f)
        {
            var m = f.Mesh; var context = f.Context;
            Assert.That(m.VertexCount, Is.EqualTo(8), context); Assert.That(m.FaceCount, Is.EqualTo(6), context); Assert.That(m.VertexIndexCount, Is.EqualTo(24), context);
            Assert.That(m.PositionArray, Is.EqualTo(Positions(f.Bounds)), context); Assert.That(m.VertexIndexArray, Is.EqualTo(Corners), context);
            Assert.That(m.FirstIndexArray, Is.EqualTo(new[] { 0, 4, 8, 12, 16, 20, 24 }), context); Assert.That(m.FaceVertexCountRange, Is.EqualTo(new Range1i(4, 4)), context);
            Assert.That(m.BoundingBox3d, Is.EqualTo(f.Bounds), context); Assert.That(m.HasTopology, Is.False, context); Assert.That(m.NormalArray, Is.Null, context);
            Assert.That(m.VertexAttributes.Keys, Is.EquivalentTo(new[] { P, C }), context);
            Assert.That(m.FaceAttributes.Keys, Is.EquivalentTo(f.Shading ? new[] { N } : Array.Empty<Symbol>()), context);
            Assert.That(m.FaceVertexAttributes.Keys, Is.EquivalentTo(f.Shading ? new[] { UV, -UV, U, -U, V, -V } : new[] { UV, -UV }), context);
            Assert.That(m.EdgeAttributes.Count, Is.Zero, context); Assert.That(m.InstanceAttributes.Count, Is.Zero, context);
            Assert.That(m.FaceVertexAttributes[UV], Is.TypeOf<int[]>(), context); Assert.That(m.FaceVertexAttributes[UV], Is.EqualTo(Enumerable.Range(0, 24).Select(i => i % 4)), context);
            Assert.That(m.FaceVertexAttributes[-UV], Is.TypeOf<V2d[]>(), context); Assert.That(m.FaceVertexAttributes[-UV], Is.EqualTo(Uvs), context);
            Assert.That(m.VertexAttributes[C], f.FloatColors ? Is.TypeOf<C4f[]>() : Is.TypeOf<C4b[]>(), context);
            Assert.That(m.VertexAttributes[C], f.FloatColors ? Is.EqualTo(Enumerable.Repeat(f.FloatColor, 8)) : Is.EqualTo(Enumerable.Repeat(f.ByteColor, 8)), context);
            if (!f.Shading) return;
            Assert.That(m.FaceAttributes[N], Is.TypeOf<V3d[]>(), context); Assert.That(m.FaceAttributes[N], Is.EqualTo(Normals), context);
            foreach (var name in new[] { U, V })
            {
                Assert.That(m.FaceVertexAttributes[name], Is.TypeOf<int[]>(), context); Assert.That(m.FaceVertexAttributes[name], Is.EqualTo(Enumerable.Range(0, 24)), context);
                Assert.That(m.FaceVertexAttributes[-name], Is.TypeOf<V3d[]>(), context); var values = (V3d[])m.FaceVertexAttributes[-name];
                Assert.That(values.Length, Is.EqualTo(24), context);
                for (var i = 0; i < values.Length; i++) Equal(values[i], (name == U ? Us : Vs)[i / 4], context + $", tangent={name}, corner={i}");
            }
            Assert.That(m.FaceVertexAttributes[U], Is.SameAs(m.FaceVertexAttributes[V]), context);
            for (var face = 0; face < 6; face++)
            {
                var a = m.PositionArray[Corners[4 * face]]; var b = m.PositionArray[Corners[4 * face + 1]]; var c = m.PositionArray[Corners[4 * face + 2]];
                var cross = Vec.Cross(b - a, c - a); Assert.That(cross.IsNaN || cross.IsInfinity, Is.False, context);
                Assert.That(Vec.Dot(cross, Normals[face]), Is.GreaterThan(0), context + ", outward winding");
            }
        }
        private static void CheckExport(Fixture f, IndexedGeometry geometry, int[] faces, PolyMesh.GetGeometryOptions options)
        {
            var context = f.Context + $", export flags={options}, faces={string.Join(",", faces)}";
            var arrays = geometry.IndexedAttributes; var indices = geometry.IndexArray;
            Assert.That(arrays.Keys, Is.EquivalentTo(f.Shading ? new[] { P, C, UV, N, U, V } : new[] { P, C, UV }), context);
            Assert.That(indices, (options & PolyMesh.GetGeometryOptions.IntIndices) != 0 ? Is.TypeOf<int[]>() : Is.TypeOf<short[]>(), context);
            var floats = (options & PolyMesh.GetGeometryOptions.FloatVectors) != 0; var bytes = (options & PolyMesh.GetGeometryOptions.ByteColors) != 0;
            Assert.That(arrays[P], floats ? Is.TypeOf<V3f[]>() : Is.TypeOf<V3d[]>(), context); Assert.That(arrays[UV], floats ? Is.TypeOf<V2f[]>() : Is.TypeOf<V2d[]>(), context);
            Assert.That(arrays[C], !f.FloatColors || bytes ? Is.TypeOf<C4b[]>() : Is.TypeOf<C4f[]>(), context);
            var keys = new HashSet<(int Vertex, int Uv, int Normal)>();
            foreach (var face in faces) for (var c = 0; c < 4; c++) keys.Add((Corners[4 * face + c], c, f.Shading ? face : -1));
            var expectedVertices = (options & PolyMesh.GetGeometryOptions.Compact) != 0 ? keys.Count : faces.Length * 4;
            foreach (var pair in arrays) Assert.That(pair.Value.Length, Is.EqualTo(expectedVertices), context + ", attribute=" + pair.Key);
            Assert.That(indices.Length, Is.EqualTo(6 * faces.Length), context);
            var fan = new[] { 0, 1, 2, 0, 2, 3 }; var positions = Positions(f.Bounds);
            for (var i = 0; i < indices.Length; i++)
            {
                var face = faces[i / 6]; var corner = fan[i % 6]; var index = Convert.ToInt32(indices.GetValue(i));
                Equal(Vector(arrays[P], index), positions[Corners[4 * face + corner]], context + $", corner={i}", floats ? 0.0002 : 1e-12);
                var uv = arrays[UV] is V2d[] ds ? ds[index] : (V2d)((V2f[])arrays[UV])[index];
                Assert.That(uv.IsNaN || uv.IsInfinity, Is.False, context); Assert.That(uv, Is.EqualTo(Uvs[corner]), context);
                if (arrays[C] is C4b[] cb) Assert.That(cb[index], Is.EqualTo(f.FloatColors ? new C4b(f.FloatColor) : f.ByteColor), context);
                else Assert.That(((C4f[])arrays[C])[index], Is.EqualTo(f.FloatColor), context);
                if (f.Shading) foreach (var name in new[] { N, U, V })
                {
                    Assert.That(arrays[name], floats ? Is.TypeOf<V3f[]>() : Is.TypeOf<V3d[]>(), context);
                    Equal(Vector(arrays[name], index), (name == N ? Normals : name == U ? Us : Vs)[face], context + ", vector=" + name);
                }
            }
        }
        private static V3d Vector(Array array, int index) => array is V3d[] ds ? ds[index] : (V3d)((V3f[])array)[index];
        private static void Equal(V3d actual, V3d expected, string context, double tolerance = 1e-12)
        {
            Assert.That(actual.IsNaN || actual.IsInfinity, Is.False, context);
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(tolerance), context); Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(tolerance), context); Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(tolerance), context);
        }
        private static Action Snapshot(PolyMesh m, string context)
        {
            var p = m.PositionArray; var pc = p.ToArray(); var indices = m.VertexIndexArray; var ic = indices.ToArray(); var first = m.FirstIndexArray; var fc = first.ToArray();
            var counts = new[] { m.VertexCount, m.VertexIndexCount, m.FaceCount, m.VertexCapacity, m.VertexIndexCapacity, m.FaceCapacity, m.EulerCharacteristic }; var bounds = m.BoundingBox3d; var range = m.FaceVertexCountRange;
            var dictionaries = new[] { m.VertexAttributes, m.FaceAttributes, m.FaceVertexAttributes }; var entries = dictionaries.Select(d => d.ToArray()).ToArray(); var copies = entries.Select(es => es.Select(kv => kv.Value.Clone()).ToArray()).ToArray();
            return () => {
                Assert.That(m.PositionArray, Is.SameAs(p), context); Assert.That(p, Is.EqualTo(pc), context); Assert.That(m.VertexIndexArray, Is.SameAs(indices), context); Assert.That(indices, Is.EqualTo(ic), context);
                Assert.That(m.FirstIndexArray, Is.SameAs(first), context); Assert.That(first, Is.EqualTo(fc), context);
                Assert.That(new[] { m.VertexCount, m.VertexIndexCount, m.FaceCount, m.VertexCapacity, m.VertexIndexCapacity, m.FaceCapacity, m.EulerCharacteristic }, Is.EqualTo(counts), context);
                Assert.That(m.BoundingBox3d, Is.EqualTo(bounds), context); Assert.That(m.FaceVertexCountRange, Is.EqualTo(range), context); Assert.That(m.HasTopology, Is.False, context);
                var current = new[] { m.VertexAttributes, m.FaceAttributes, m.FaceVertexAttributes };
                for (var d = 0; d < current.Length; d++)
                {
                    Assert.That(current[d], Is.SameAs(dictionaries[d]), context); Assert.That(current[d].Keys, Is.EquivalentTo(entries[d].Select(kv => kv.Key)), context);
                    for (var i = 0; i < entries[d].Length; i++) { Assert.That(current[d][entries[d][i].Key], Is.SameAs(entries[d][i].Value), context); Assert.That(current[d][entries[d][i].Key], Is.EqualTo(copies[d][i]), context); }
                }
                Assert.That(m.EdgeAttributes.Count, Is.Zero, context); Assert.That(m.InstanceAttributes.Count, Is.Zero, context);
            };
        }
    }
}
