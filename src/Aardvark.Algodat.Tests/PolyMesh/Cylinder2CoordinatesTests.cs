using Aardvark.Base;
using NUnit.Framework;
using System;
using System.Linq;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class Cylinder2CoordinatesTests
    {
        private static readonly int[] Tessellations = { 3, 4, 8, 31 };
        private static readonly Symbol[] Names = { PolyMesh.Property.DiffuseColorCoordinates, (Symbol)"Cylinder2CustomCoordinates" };
        private static readonly (double Height, double Radius)[] Shapes = { (2.5, 0.75), (-3, -1.25), (0, 0) };
        private static readonly C4b[] Colors = { C4b.White, new C4b(17, 23, 42, 93) };
        private static readonly PolyMesh.GetGeometryOptions[] Options =
        {
            PolyMesh.GetGeometryOptions.Default,
            PolyMesh.GetGeometryOptions.Compact | PolyMesh.GetGeometryOptions.IntIndices,
            PolyMesh.GetGeometryOptions.None, PolyMesh.GetGeometryOptions.FloatVectors
        };

        [Test]
        public void NamedCoordinatesRetainIndexedMantleSeamsAndExistingCapValues()
        {
            foreach (var n in Tessellations)
            foreach (var name in Names)
            foreach (var shape in Shapes)
            foreach (var color in Colors)
            {
                var context = $"n={n}, name={name}, shape={shape}, color={color}";
                var mesh = PolyMeshPrimitives.Cylinder2(n, shape.Height, shape.Radius, color, name);
                CheckGeometryAndNormals(mesh, n, shape.Height, shape.Radius, context);
                Assert.That(mesh.FaceVertexAttributes.Keys, Is.EquivalentTo(new[] { PolyMesh.Property.Normals, -PolyMesh.Property.Normals, name, -name }), context);
                Assert.That(mesh.FaceVertexAttributes[name], Is.TypeOf<int[]>(), context);
                Assert.That(mesh.FaceVertexAttributes[-name], Is.TypeOf<V2d[]>(), context);
                var indices = (int[])mesh.FaceVertexAttributes[name];
                var coordinates = (V2d[])mesh.FaceVertexAttributes[-name];
                Assert.That(indices.Length, Is.EqualTo(10 * n), context);
                Assert.That(coordinates.Length, Is.EqualTo(3 * n + 4), context);
                for (var i = 0; i <= n; i++)
                {
                    Equal(coordinates[i], new V2d(i / (double)n, 0), context);
                    Equal(coordinates[n + 1 + i], new V2d(i / (double)n, 1), context);
                }
                for (var i = 0; i < n; i++) Equal(coordinates[2 * n + 2 + i], Cap(i, n), context);
                Equal(coordinates[3 * n + 2], new V2d(0.5, 0), context);
                Equal(coordinates[3 * n + 3], new V2d(0.5, 1), context);
                for (var face = 0; face < 3 * n; face++)
                for (var corner = 0; corner < (face < n ? 4 : 3); corner++)
                {
                    var index = indices[mesh.FirstIndexArray[face] + corner];
                    Assert.That(index, Is.InRange(0, coordinates.Length - 1), context);
                    Equal(coordinates[index], ExpectedUv(n, face, corner), $"{context}, face={face}, corner={corner}");
                }
                Assert.That(indices[0], Is.Not.EqualTo(indices[4 * (n - 1) + 3]), context);
                Equal(coordinates[indices[0]], V2d.Zero, context);
                Equal(coordinates[indices[4 * (n - 1) + 3]], V2d.IO, context);
                var disabled = PolyMeshPrimitives.Cylinder2(n, shape.Height, shape.Radius, color, Symbol.Empty);
                Assert.That(mesh.PositionArray, Is.EqualTo(disabled.PositionArray), context);
                Assert.That(mesh.VertexIndexArray, Is.EqualTo(disabled.VertexIndexArray), context);
                Assert.That(mesh.FirstIndexArray, Is.EqualTo(disabled.FirstIndexArray), context);
                foreach (var key in disabled.FaceVertexAttributes.Keys)
                    Assert.That(mesh.FaceVertexAttributes[key], Is.EqualTo(disabled.FaceVertexAttributes[key]), context);
            }
        }

        [Test]
        public void IndexedGeometryPropagatesNamedCoordinatesAndPreservesTrianglePayloads()
        {
            foreach (var n in Tessellations)
            foreach (var name in Names)
            foreach (var options in Options)
            {
                var mesh = PolyMeshPrimitives.Cylinder2(n, 2.5, 0.75, C4b.White, name);
                CheckExport(mesh, n, name, options, $"n={n}, name={name}, options={options}");
            }
        }

        [Test]
        public void DisabledCoordinatesUnusedColorAndValidationRetainTheirContracts()
        {
            foreach (var n in Tessellations)
            {
                foreach (var shape in Shapes)
                {
                    var context = $"disabled n={n}, shape={shape}";
                    var meshes = Colors.Select(color => PolyMeshPrimitives.Cylinder2(n, shape.Height, shape.Radius, color, Symbol.Empty)).ToArray();
                    foreach (var mesh in meshes)
                    {
                        CheckGeometryAndNormals(mesh, n, shape.Height, shape.Radius, context);
                        Assert.That(mesh.FaceVertexAttributes.Keys, Is.EquivalentTo(new[] { PolyMesh.Property.Normals, -PolyMesh.Property.Normals }), context);
                    }
                    Assert.That(meshes[0].PositionArray, Is.EqualTo(meshes[1].PositionArray), context);
                    foreach (var key in meshes[0].FaceVertexAttributes.Keys)
                        Assert.That(meshes[0].FaceVertexAttributes[key], Is.EqualTo(meshes[1].FaceVertexAttributes[key]), context);
                }
                foreach (var options in Options)
                    CheckExport(PolyMeshPrimitives.Cylinder2(n, 2.5, 0.75, C4b.White, Symbol.Empty), n, Symbol.Empty, options, $"disabled n={n}, options={options}");
            }
            foreach (var n in new[] { int.MinValue, -1, 0, 1, 2 })
            foreach (var name in new[] { Symbol.Empty, Names[0], Names[1] })
            {
                var error = Assert.Throws<ArgumentOutOfRangeException>(() => PolyMeshPrimitives.Cylinder2(n, 1, 1, C4b.White, name));
                Assert.That(error.ParamName, Is.EqualTo("tessellation"), $"n={n}, name={name}");
            }
            foreach (var name in new[] { PolyMesh.Property.Normals, -PolyMesh.Property.Normals })
                Assert.Throws<ArgumentException>(() => PolyMeshPrimitives.Cylinder2(3, 1, 1, C4b.White, name), $"normal-key collision: {name}");
        }

        private static void CheckGeometryAndNormals(PolyMesh mesh, int n, double height, double radius, string context)
        {
            Assert.That(mesh.VertexCount, Is.EqualTo(2 * n + 2), context);
            Assert.That(mesh.FaceCount, Is.EqualTo(3 * n), context);
            Assert.That(mesh.VertexIndexCount, Is.EqualTo(10 * n), context);
            Assert.That(mesh.PositionArray.Length, Is.EqualTo(2 * n + 2), context);
            Assert.That(mesh.FirstIndexArray.Length, Is.EqualTo(3 * n + 1), context);
            Assert.That(mesh.HasTopology, Is.False, context);
            Assert.That(mesh.VertexAttributes.Keys, Is.EquivalentTo(new[] { PolyMesh.Property.Positions }), context);
            Assert.That(mesh.FaceAttributes.Count, Is.Zero, context);
            Assert.That(mesh.InstanceAttributes.Count, Is.Zero, context);
            Assert.That(mesh.FaceVertexAttributes[PolyMesh.Property.Normals], Is.TypeOf<int[]>(), context);
            Assert.That(mesh.FaceVertexAttributes[-PolyMesh.Property.Normals], Is.TypeOf<V3d[]>(), context);
            var normalIndices = (int[])mesh.FaceVertexAttributes[PolyMesh.Property.Normals];
            var normals = (V3d[])mesh.FaceVertexAttributes[-PolyMesh.Property.Normals];
            Assert.That(normalIndices.Length, Is.EqualTo(10 * n), context);
            Assert.That(normals.Length, Is.EqualTo(n + 2), context);
            for (var i = 0; i < 2 * n + 2; i++) Equal(mesh.PositionArray[i], Position(i, n, height, radius), context);
            for (var i = 0; i < n; i++) Equal(normals[i], Radial(i, n), context);
            Equal(normals[n], V3d.OOI, context);
            Equal(normals[n + 1], -V3d.OOI, context);
            for (var face = 0; face < 3 * n; face++)
            {
                var count = face < n ? 4 : 3;
                Assert.That(mesh.FirstIndexArray[face], Is.EqualTo(face < n ? face * 4 : 4 * n + (face - n) * 3), context);
                for (var k = 0; k < count; k++)
                {
                    var corner = mesh.FirstIndexArray[face] + k;
                    Assert.That(mesh.VertexIndexArray[corner], Is.EqualTo(Vertex(n, face, k)), context);
                    var expectedNormal = face < n ? ((k < 2 ? face : face + 1) % n) : (face < 2 * n ? n : n + 1);
                    Assert.That(normalIndices[corner], Is.EqualTo(expectedNormal), context);
                }
            }
            Assert.That(mesh.FirstIndexArray[3 * n], Is.EqualTo(10 * n), context);
        }

        private static void CheckExport(PolyMesh mesh, int n, Symbol name, PolyMesh.GetGeometryOptions options, string context)
        {
            var dictionary = mesh.FaceVertexAttributes;
            var arrays = dictionary.ToDictionary(kv => kv.Key, kv => kv.Value);
            var copies = arrays.ToDictionary(kv => kv.Key, kv => kv.Value.Clone());
            var geometry = mesh.GetIndexedGeometry(options);
            var floats = (options & PolyMesh.GetGeometryOptions.FloatVectors) != 0;
            Assert.That(geometry.IndexArray, (options & PolyMesh.GetGeometryOptions.IntIndices) != 0 ? Is.TypeOf<int[]>() : Is.TypeOf<short[]>(), context);
            var positions = geometry.IndexedAttributes[PolyMesh.Property.Positions];
            var normals = geometry.IndexedAttributes[PolyMesh.Property.Normals];
            Assert.That(positions.GetType(), Is.EqualTo(floats ? typeof(V3f[]) : typeof(V3d[])), context);
            Assert.That(normals.GetType(), Is.EqualTo(positions.GetType()), context);
            Assert.That(normals.Length, Is.EqualTo(positions.Length), context);
            Assert.That(geometry.IndexedAttributes.ContainsKey(name), Is.EqualTo(name.IsNotEmpty), context);
            var coordinates = name.IsNotEmpty ? geometry.IndexedAttributes[name] : null;
            if (coordinates != null)
            {
                Assert.That(coordinates.GetType(), Is.EqualTo(floats ? typeof(V2f[]) : typeof(V2d[])), context);
                Assert.That(coordinates.Length, Is.EqualTo(positions.Length), context);
            }
            Assert.That(geometry.IndexArray.Length, Is.EqualTo(12 * n), context);
            var draw = 0;
            var seamFirst = -1;
            var seamLast = -1;
            for (var face = 0; face < 3 * n; face++)
            foreach (var k in face < n ? new[] { 0, 1, 2, 0, 2, 3 } : new[] { 0, 1, 2 })
            {
                var index = Convert.ToInt32(geometry.IndexArray.GetValue(draw++));
                Assert.That(index, Is.InRange(0, positions.Length - 1), context);
                var expectedPosition = Position(Vertex(n, face, k), n, 2.5, 0.75);
                var expectedNormal = face < n ? Radial((k < 2 ? face : face + 1) % n, n) : (face < 2 * n ? V3d.OOI : -V3d.OOI);
                Equal(V3(positions, index), floats ? (V3d)(V3f)expectedPosition : expectedPosition, context);
                Equal(V3(normals, index), floats ? (V3d)(V3f)expectedNormal : expectedNormal, context);
                if (coordinates != null)
                {
                    var expected = ExpectedUv(n, face, k);
                    Equal(coordinates is V2f[] fs ? (V2d)fs[index] : ((V2d[])coordinates)[index], floats ? (V2d)(V2f)expected : expected, context);
                    if (face == 0 && k == 0) seamFirst = index;
                    if (face == n - 1 && k == 3) seamLast = index;
                }
            }
            if (coordinates != null) Assert.That(seamLast, Is.Not.EqualTo(seamFirst), context);
            Assert.That(mesh.FaceVertexAttributes, Is.SameAs(dictionary), context);
            foreach (var kv in arrays)
            {
                Assert.That(mesh.FaceVertexAttributes[kv.Key], Is.SameAs(kv.Value), context);
                Assert.That(mesh.FaceVertexAttributes[kv.Key], Is.EqualTo(copies[kv.Key]), context);
            }
            CheckGeometryAndNormals(mesh, n, 2.5, 0.75, context);
        }

        private static V3d V3(Array values, int i) => values is V3f[] fs ? (V3d)fs[i] : ((V3d[])values)[i];
        private static V3d Radial(int i, int n) => new V3d(Math.Cos(2 * Math.PI * (i / (double)n)), Math.Sin(2 * Math.PI * (i / (double)n)), 0);
        private static V3d Position(int i, int n, double height, double radius) => i >= 2 * n
            ? new V3d(0, 0, (i == 2 * n ? 0.5 : -0.5) * height)
            : Radial(i % n, n) * radius + new V3d(0, 0, (i < n ? 0.5 : -0.5) * height);
        private static int Vertex(int n, int face, int k)
        {
            var j = face % n;
            var next = (j + 1) % n;
            if (face < n) return new[] { j, j + n, next + n, next }[k];
            return face < 2 * n ? new[] { j, next, 2 * n }[k] : new[] { next + n, j + n, 2 * n + 1 }[k];
        }
        private static V2d Cap(int j, int n)
        {
            var normal = Radial(j, n);
            return new V2d(0.5 + 0.5 * normal.X, 0.5 + 0.5 * normal.Y);
        }
        private static V2d ExpectedUv(int n, int face, int k)
        {
            var j = face % n;
            if (face < n) return new V2d((j + (k >= 2 ? 1 : 0)) / (double)n, k == 0 || k == 3 ? 0 : 1);
            // Retain the existing cap corner order and center values, independently of geometry winding.
            return k < 2 ? Cap((j + k) % n, n) : new V2d(0.5, face < 2 * n ? 0 : 1);
        }
        private static void Equal(V3d actual, V3d expected, string context)
        {
            Assert.That(actual.IsNaN || actual.IsInfinity, Is.False, context);
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(2e-14), context);
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(2e-14), context);
            Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(2e-14), context);
        }
        private static void Equal(V2d actual, V2d expected, string context)
        {
            Assert.That(actual.IsNaN || actual.IsInfinity, Is.False, context);
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(2e-14), context);
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(2e-14), context);
        }
    }
}
