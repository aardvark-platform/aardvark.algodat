using Aardvark.Base;
using Aardvark.Rendering;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class SimpleTriangleIndicesTests
    {
        private static readonly Symbol Custom = (Symbol)"SimpleTriangleSource";

        [Test]
        public void StaticTriangleBuffersReturnOnlyTheActivePrefixWithoutSharingSpareCapacity()
        {
            foreach (var faceCount in new[] { 0, 1, 2, 5, 32, 257 })
            foreach (var extra in new[] { 1, 2, 3, 13, 257 })
            foreach (var sentinels in new[] { false, true })
            foreach (var extraFirst in new[] { 0, 5 })
            {
                var expected = Enumerable.Range(0, faceCount).SelectMany(Triangle).ToArray();
                var buffer = new int[expected.Length + extra]; expected.CopyTo(buffer, 0); FillSuffix(buffer, expected.Length, sentinels);
                var first = new int[faceCount + 1 + extraFirst];
                for (var i = 0; i <= faceCount; i++) first[i] = 3 * i;
                for (var i = faceCount + 1; i < first.Length; i++) first[i] = int.MinValue;
                var before = buffer.ToArray(); var beforeFirst = first.ToArray();
                var context = $"static faces={faceCount}, extra={extra}, sentinels={sentinels}, extraFirst={extraFirst}";
                var result = PolyMesh.CreateSimpleTriangleVertexIndexArray(first, buffer, faceCount, expected.Length, new Range1i(3, 3));
                Assert.That(result, Is.EqualTo(expected), context); Assert.That(result.Length, Is.EqualTo(expected.Length), context);
                Assert.That(result, Is.Not.SameAs(buffer), context);
                if (result.Length > 0) { result[0] = int.MaxValue; Assert.That(buffer, Is.EqualTo(before), context + ", returned array ownership"); }
                Assert.That(buffer, Is.EqualTo(before), context); Assert.That(first, Is.EqualTo(beforeFirst), context);
            }
        }

        [Test]
        public void IncrementalFacesExposeOnlyActiveTrianglesThroughBothApisAndIndependentCopies()
        {
            foreach (var faceCount in new[] { 1, 2, 5, 6, 10, 11, 21, 22, 64, 257 })
            foreach (var sentinels in new[] { false, true })
            {
                var faces = Enumerable.Range(0, faceCount).Select(Triangle).ToArray();
                var mesh = new PolyMesh { PositionArray = TrianglePositions() };
                for (var i = 0; i < faceCount; i++) Assert.That(mesh.AddFace(faces[i]), Is.EqualTo(i), $"AddFace {i}");
                var expected = faces.SelectMany(f => f).ToArray();
                Assert.That(mesh.VertexIndexCount, Is.EqualTo(expected.Length));
                Assert.That(mesh.VertexIndexCapacity, Is.GreaterThan(mesh.VertexIndexCount));
                FillSuffix(mesh.VertexIndexArray, mesh.VertexIndexCount, sentinels);
                for (var i = faceCount + 1; i < mesh.FirstIndexArray.Length; i++) mesh.FirstIndexArray[i] = int.MinValue;
                SeedAttributes(mesh);
                var context = $"incremental faces={faceCount}, capacity={mesh.VertexIndexCapacity}, sentinels={sentinels}";
                var unchanged = Snapshot(mesh, context);
                var instance = mesh.CreateSimpleTriangleVertexIndexArray(); var direct = Static(mesh);
                Assert.That(instance, Is.EqualTo(expected), context); Assert.That(direct, Is.EqualTo(expected), context);
                Assert.That(instance, Is.Not.SameAs(mesh.VertexIndexArray), context); Assert.That(direct, Is.Not.SameAs(mesh.VertexIndexArray), context);
                Assert.That(instance, Is.Not.SameAs(direct), context);
                instance[0] = int.MaxValue;
                Assert.That(direct, Is.EqualTo(expected), context + ", independent calls"); unchanged();
                direct[0] = int.MinValue; unchanged();
                Assert.That(mesh.CreateSimpleTriangleVertexIndexArray(), Is.EqualTo(expected), context + ", later call");
            }
        }

        [Test]
        public void ExactSizeTriangleBuffersRetainBackingArrayIdentityIncludingTheStaticEmptyPrefix()
        {
            var empty = Array.Empty<int>();
            Assert.That(PolyMesh.CreateSimpleTriangleVertexIndexArray(new[] { 0 }, empty, 0, 0, new Range1i(3, 3)), Is.SameAs(empty));
            foreach (var faceCount in new[] { 1, 2, 5, 32, 257 })
            {
                var faces = Enumerable.Range(0, faceCount).Select(Triangle).ToArray();
                var mesh = Create(TrianglePositions(), faces, 0, false); var context = $"exact faces={faceCount}";
                var unchanged = Snapshot(mesh, context);
                var instance = mesh.CreateSimpleTriangleVertexIndexArray(); var direct = Static(mesh);
                Assert.That(instance, Is.SameAs(mesh.VertexIndexArray), context); Assert.That(direct, Is.SameAs(instance), context);
                var old = instance[0];
                try { instance[0] = 7; Assert.That(mesh.VertexIndexArray[0], Is.EqualTo(7), context); Assert.That(direct[0], Is.EqualTo(7), context); }
                finally { instance[0] = old; }
                unchanged();
            }
        }

        [Test]
        public void ConvexFansAndIndexedExportsKeepTheirExistingGeometryAndSourceArrays()
        {
            foreach (var valences in new[] { new[] { 4 }, new[] { 5 }, new[] { 8 }, new[] { 3, 4, 5 }, new[] { 4, 3, 6, 3 } })
            foreach (var shift in new[] { 0, 1, 3 })
            foreach (var reverse in new[] { false, true })
            foreach (var layout in new[] { (Extra: 0, Sentinels: false), (Extra: 1, Sentinels: true), (Extra: 3, Sentinels: false), (Extra: 13, Sentinels: true) })
            {
                var fixture = Convex(valences, shift, reverse); var mesh = Create(fixture.Positions, fixture.Faces, layout.Extra, layout.Sentinels);
                var context = $"fan valences={string.Join(",", valences)}, shift={shift}, reverse={reverse}, extra={layout.Extra}, sentinels={layout.Sentinels}";
                var unchanged = Snapshot(mesh, context); var expected = Fan(fixture.Faces);
                var instance = mesh.CreateSimpleTriangleVertexIndexArray(); var direct = Static(mesh);
                Assert.That(instance, Is.EqualTo(expected), context); Assert.That(direct, Is.EqualTo(expected), context);
                Assert.That(instance, Is.Not.SameAs(mesh.VertexIndexArray), context); Assert.That(direct, Is.Not.SameAs(instance), context);
                CheckAreas(fixture.Positions, fixture.Faces, instance, context); unchanged();
            }
            var polygon = Convex(new[] { 4, 3, 5 }, 1, true);
            var triangles = Enumerable.Range(0, 6).Select(Triangle).ToArray();
            var incremental = new PolyMesh { PositionArray = TrianglePositions() };
            foreach (var face in triangles) incremental.AddFace(face);
            FillSuffix(incremental.VertexIndexArray, incremental.VertexIndexCount, true); SeedAttributes(incremental);
            foreach (var fixture in new[] { (Mesh: incremental, Faces: triangles), (Mesh: Create(polygon.Positions, polygon.Faces, 13, true), Faces: polygon.Faces) })
            {
                var mesh = fixture.Mesh; var unchanged = Snapshot(mesh, "export source");
                for (var flags = 0; flags < 16; flags++)
                {
                    var options = (PolyMesh.GetGeometryOptions)flags;
                    CheckExport(mesh.GetIndexedGeometry(options), mesh.PositionArray, Fan(fixture.Faces), options, $"export all flags={flags}");
                    foreach (var selection in new[] { Enumerable.Range(0, mesh.FaceCount).ToArray(), new[] { mesh.FaceCount - 1, 0, mesh.FaceCount - 1 } })
                        CheckExport(mesh.GetIndexedGeometry(selection, options), mesh.PositionArray, Fan(selection.Select(i => fixture.Faces[i]).ToArray()), options, $"export selection={string.Join(",", selection)}, flags={flags}");
                }
                CheckExport(mesh.GetIndexedGeometry(), mesh.PositionArray, Fan(fixture.Faces), PolyMesh.GetGeometryOptions.Default, "export default"); unchanged();
            }
        }

        private static int[] Triangle(int face)
        {
            var start = 3 * (face % 3);
            return (face & 1) == 0 ? new[] { start, start + 1, start + 2 } : new[] { start + 2, start + 1, start };
        }
        private static V3d[] TrianglePositions() => Enumerable.Range(0, 3).SelectMany(i => new[] { new V3d(3 * i, 0, 0), new V3d(3 * i + 1, 0, 0), new V3d(3 * i, 1, 0) }).ToArray();
        private static void FillSuffix(int[] buffer, int active, bool sentinels)
        {
            var suffix = sentinels ? new[] { -1, int.MinValue, int.MaxValue } : new[] { 6, 7, 8 };
            for (var i = active; i < buffer.Length; i++) buffer[i] = suffix[(i - active) % suffix.Length];
        }
        private static int[] Static(PolyMesh mesh) => PolyMesh.CreateSimpleTriangleVertexIndexArray(mesh.FirstIndexArray, mesh.VertexIndexArray, mesh.FaceCount, mesh.VertexIndexCount, mesh.FaceVertexCountRange);
        private static PolyMesh Create(V3d[] positions, int[][] faces, int extra, bool sentinels)
        {
            var active = faces.SelectMany(f => f).ToArray(); var buffer = new int[active.Length + extra]; active.CopyTo(buffer, 0); FillSuffix(buffer, active.Length, sentinels);
            var first = new int[faces.Length + 1]; for (var i = 0; i < faces.Length; i++) first[i + 1] = first[i] + faces[i].Length;
            var mesh = new PolyMesh { PositionArray = positions.ToArray(), FirstIndexArray = first, VertexIndexArray = buffer };
            mesh.VertexIndexCount = active.Length; SeedAttributes(mesh); return mesh;
        }
        private static void SeedAttributes(PolyMesh mesh)
        {
            mesh.VertexAttributes[Custom] = Enumerable.Range(0, mesh.VertexCount).ToArray();
            mesh.FaceAttributes[Custom] = Enumerable.Range(0, mesh.FaceCount).ToArray();
            mesh.FaceVertexAttributes[Custom] = Enumerable.Range(0, mesh.VertexIndexCount).ToArray();
            mesh.InstanceAttributes[Custom] = "unchanged";
        }
        private static (V3d[] Positions, int[][] Faces) Convex(int[] valences, int shift, bool reverse)
        {
            var positions = new List<V3d>(); var faces = new List<int[]>();
            foreach (var n in valences)
            {
                var start = positions.Count;
                for (var i = 0; i < n; i++) { var a = 2 * Math.PI * i / n; positions.Add(new V3d(4 * faces.Count + Math.Cos(a), Math.Sin(a), 0)); }
                faces.Add(Enumerable.Range(0, n).Select(i => start + (shift + (reverse ? n - i : i)) % n).ToArray());
            }
            return (positions.ToArray(), faces.ToArray());
        }
        private static int[] Fan(int[][] faces) => faces.SelectMany(f => Enumerable.Range(1, f.Length - 2).SelectMany(i => new[] { f[0], f[i], f[i + 1] })).ToArray();
        private static void CheckAreas(V3d[] positions, int[][] faces, int[] triangles, string context)
        {
            var offset = 0;
            foreach (var face in faces)
            {
                double polygon = 0, sum = 0;
                for (var i = 0; i < face.Length; i++) { var p = positions[face[i]]; var q = positions[face[(i + 1) % face.Length]]; polygon += p.X * q.Y - p.Y * q.X; }
                for (var i = 0; i < face.Length - 2; i++)
                {
                    var a = positions[triangles[offset++]]; var b = positions[triangles[offset++]]; var c = positions[triangles[offset++]];
                    var area = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
                    Assert.That(double.IsFinite(area), Is.True, context); Assert.That(Math.Sign(area), Is.EqualTo(Math.Sign(polygon)), context); sum += area;
                }
                Assert.That(sum, Is.EqualTo(polygon).Within(1e-11), context);
            }
        }
        private static void CheckExport(IndexedGeometry geometry, V3d[] source, int[] expected, PolyMesh.GetGeometryOptions options, string context)
        {
            var indices = geometry.IndexArray; var positions = geometry.IndexedAttributes[DefaultSemantic.Positions];
            Assert.That(indices, (options & PolyMesh.GetGeometryOptions.IntIndices) != 0 ? Is.TypeOf<int[]>() : Is.TypeOf<short[]>(), context);
            Assert.That(positions, (options & PolyMesh.GetGeometryOptions.FloatVectors) != 0 ? Is.TypeOf<V3f[]>() : Is.TypeOf<V3d[]>(), context);
            Assert.That(indices.Length, Is.EqualTo(expected.Length), context);
            for (var i = 0; i < expected.Length; i++)
            {
                var index = Convert.ToInt32(indices.GetValue(i));
                var actual = positions is V3d[] ds ? ds[index] : (V3d)((V3f[])positions)[index]; var wanted = source[expected[i]];
                Assert.That(actual.IsNaN || actual.IsInfinity, Is.False, context);
                Assert.That(actual.X, Is.EqualTo(wanted.X).Within(1e-6), context + $", corner={i}");
                Assert.That(actual.Y, Is.EqualTo(wanted.Y).Within(1e-6), context + $", corner={i}");
                Assert.That(actual.Z, Is.EqualTo(wanted.Z).Within(1e-6), context + $", corner={i}");
            }
        }
        private static Action Snapshot(PolyMesh mesh, string context)
        {
            var positions = mesh.PositionArray; var p = positions.ToArray(); var indices = mesh.VertexIndexArray; var ix = indices.ToArray(); var first = mesh.FirstIndexArray; var fi = first.ToArray();
            var counts = new[] { mesh.VertexCount, mesh.VertexIndexCount, mesh.FaceCount, mesh.VertexIndexCapacity, mesh.FaceCapacity, mesh.EulerCharacteristic }; var range = mesh.FaceVertexCountRange; var topology = mesh.HasTopology;
            var dictionaries = new[] { mesh.VertexAttributes, mesh.FaceAttributes, mesh.FaceVertexAttributes }; var entries = dictionaries.Select(d => d.ToArray()).ToArray(); var copies = entries.Select(es => es.Select(kv => kv.Value.Clone()).ToArray()).ToArray();
            var instance = mesh.InstanceAttributes; var instanceEntries = instance.ToArray();
            return () => {
                Assert.That(mesh.PositionArray, Is.SameAs(positions), context); Assert.That(positions, Is.EqualTo(p), context);
                Assert.That(mesh.VertexIndexArray, Is.SameAs(indices), context); Assert.That(indices, Is.EqualTo(ix), context);
                Assert.That(mesh.FirstIndexArray, Is.SameAs(first), context); Assert.That(first, Is.EqualTo(fi), context);
                Assert.That(new[] { mesh.VertexCount, mesh.VertexIndexCount, mesh.FaceCount, mesh.VertexIndexCapacity, mesh.FaceCapacity, mesh.EulerCharacteristic }, Is.EqualTo(counts), context);
                Assert.That(mesh.FaceVertexCountRange, Is.EqualTo(range), context); Assert.That(mesh.HasTopology, Is.EqualTo(topology), context);
                var current = new[] { mesh.VertexAttributes, mesh.FaceAttributes, mesh.FaceVertexAttributes };
                for (var d = 0; d < current.Length; d++)
                {
                    Assert.That(current[d], Is.SameAs(dictionaries[d]), context); Assert.That(current[d].Keys, Is.EquivalentTo(entries[d].Select(kv => kv.Key)), context);
                    for (var i = 0; i < entries[d].Length; i++) { Assert.That(current[d][entries[d][i].Key], Is.SameAs(entries[d][i].Value), context); Assert.That(current[d][entries[d][i].Key], Is.EqualTo(copies[d][i]), context); }
                }
                Assert.That(mesh.InstanceAttributes, Is.SameAs(instance), context); Assert.That(instance.ToArray(), Is.EquivalentTo(instanceEntries), context);
            };
        }
    }
}
