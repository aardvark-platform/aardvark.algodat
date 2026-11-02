using System;
using System.Linq;
using Aardvark.Base;
using NUnit.Framework;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class ConeTangentsTests
    {
        private static readonly C4b Color = new C4b(29, 80, 140, 201);
        private static readonly (Symbol Uv, Symbol Tangent)[] Names =
        {
            (PolyMesh.Property.DiffuseColorCoordinates, PolyMesh.Property.DiffuseColorUTangents),
            ((Symbol)"ConeTest.Uv", (Symbol)"ConeTest.Tangent")
        };

        private static void Cases(Action<int, double, double, Symbol, Symbol, string> check)
        {
            foreach (var n in new[] { 3, 4, 8, 16 })
            foreach (var shape in new[] { (1.0, 1.0), (4.0, 0.5), (0.5, 3.0) })
            foreach (var names in Names)
                check(n, shape.Item1, shape.Item2, names.Uv, names.Tangent,
                    $"n={n}, height={shape.Item1}, radius={shape.Item2}, uv={names.Uv}, tangent={names.Tangent}");
        }

        [Test]
        public void RequestedTangentsKeepSharingAndFiniteIndexedVectorValues()
        {
            Cases((n, height, radius, uv, tangent, context) =>
            {
                var mesh = PolyMeshPrimitives.Cone(n, height, radius, Color, uv, tangent);
                CheckGeometry(mesh, n, height, radius, uv, context);
                Assert.That(mesh.FaceVertexAttributes.Keys, Is.EquivalentTo(new[]
                    { uv, -uv, PolyMesh.Property.Normals, tangent, -tangent }), context);
                Assert.That(mesh.FaceVertexAttributes[tangent], Is.TypeOf<int[]>(), context);
                Assert.That(mesh.FaceVertexAttributes.ContainsKey(-tangent), Is.True, context);
                Assert.That(mesh.FaceVertexAttributes[-tangent], Is.TypeOf<V3d[]>(), context);
                var indices = (int[])mesh.FaceVertexAttributes[tangent];
                var values = (V3d[])mesh.FaceVertexAttributes[-tangent];
                var sharing = Sharing(n);
                Assert.That(indices, Is.EqualTo(sharing), context);
                Assert.That(values.Length, Is.EqualTo(5 * n), context);
                var expected = TangentOracle(mesh, uv, sharing);
                for (var i = 0; i < values.Length; i++)
                    CheckVector(values[i], expected[i], 1e-10, $"{context}, pool={i}");
                for (var i = 0; i < indices.Length; i++)
                {
                    Assert.That(indices[i], Is.InRange(0, values.Length - 1), $"{context}, corner={i}");
                    Assert.That(values[indices[i]].Length, Is.EqualTo(1).Within(1e-10), $"{context}, corner={i}");
                }
                Assert.That(mesh.FaceVertexAttributes.ContainsKey(PolyMesh.Property.DiffuseColorVTangents), Is.False, context);
            });
        }

        [Test]
        public void ExportsKeepVectorTangentsAndAlignedAttributesAcrossAllOptions()
        {
            Cases((n, height, radius, uv, tangent, context) =>
            {
                var mesh = PolyMeshPrimitives.Cone(n, height, radius, Color, uv, tangent);
                var source = Snapshot(mesh);
                foreach (var faces in new[] { Enumerable.Range(0, 2 * n).ToArray(), new[] { n + 1, 0, n + 1, 2 * n - 1 } })
                for (var flags = 0; flags < 16; flags++)
                {
                    var options = (PolyMesh.GetGeometryOptions)flags;
                    var geometry = mesh.GetIndexedGeometry(faces, options);
                    var where = $"{context}, options={options}, faces={string.Join(",", faces)}";
                    Assert.That(geometry.IndexArray.GetType(), Is.EqualTo((flags & 4) != 0 ? typeof(int[]) : typeof(short[])), where);
                    Assert.That(geometry.IndexArray.Length, Is.EqualTo(3 * faces.Length), where);
                    Assert.That(geometry.IndexedAttributes.Keys, Is.EquivalentTo(new[]
                        { PolyMesh.Property.Positions, PolyMesh.Property.Colors, PolyMesh.Property.Normals, uv, tangent }), where);
                    foreach (var name in new[] { PolyMesh.Property.Positions, PolyMesh.Property.Normals, tangent })
                        Assert.That(geometry.IndexedAttributes[name].GetType(), Is.EqualTo((flags & 1) != 0 ? typeof(V3f[]) : typeof(V3d[])), $"{where}, attribute={name}");
                    Assert.That(geometry.IndexedAttributes[uv].GetType(), Is.EqualTo((flags & 1) != 0 ? typeof(V2f[]) : typeof(V2d[])), where);
                    Assert.That(geometry.IndexedAttributes[PolyMesh.Property.Colors], Is.TypeOf<C4b[]>(), where);
                    var count = geometry.IndexedAttributes[PolyMesh.Property.Positions].Length;
                    foreach (var attribute in geometry.IndexedAttributes)
                        Assert.That(attribute.Value.Length, Is.EqualTo(count), $"{where}, attribute={attribute.Key}");
                    var normals = (V3d[])mesh.FaceVertexAttributes[PolyMesh.Property.Normals];
                    var uvIndices = (int[])mesh.FaceVertexAttributes[uv];
                    var coordinates = (V2d[])mesh.FaceVertexAttributes[-uv];
                    var expectedTangents = TangentOracle(mesh, uv, Sharing(n));
                    var sharing = Sharing(n);
                    for (var k = 0; k < geometry.IndexArray.Length; k++)
                    {
                        var output = Convert.ToInt32(geometry.IndexArray.GetValue(k));
                        var corner = 3 * faces[k / 3] + k % 3;
                        var position = mesh.VertexIndexArray[corner];
                        Assert.That(output, Is.InRange(0, count - 1), $"{where}, index={k}");
                        CheckVector(Vector(geometry.IndexedAttributes[PolyMesh.Property.Positions], output), mesh.PositionArray[position], 1e-6, $"{where}, position={k}");
                        CheckVector(Vector(geometry.IndexedAttributes[PolyMesh.Property.Normals], output), normals[corner], 1e-6, $"{where}, normal={k}");
                        CheckVector(Vector(geometry.IndexedAttributes[tangent], output), expectedTangents[sharing[corner]], 1e-6, $"{where}, tangent={k}");
                        var actualUv = geometry.IndexedAttributes[uv].GetValue(output);
                        var coordinate = actualUv is V2f f ? (V2d)f : (V2d)actualUv;
                        Assert.That(double.IsFinite(coordinate.X) && double.IsFinite(coordinate.Y), Is.True, where);
                        Assert.That((coordinate - coordinates[uvIndices[corner]]).Length, Is.LessThan(1e-6), $"{where}, uv={k}");
                        var actualColor = geometry.IndexedAttributes[PolyMesh.Property.Colors].GetValue(output);
                        Assert.That(actualColor, Is.EqualTo(Color), $"{where}, color={k}");
                    }
                }
                CheckSnapshot(mesh, source, context);
            });
        }

        [Test]
        public void NoTangentAndDefaultConesKeepGeometryNormalsUvsColorsAndExports()
        {
            Cases((n, height, radius, uv, tangent, context) =>
            {
                var mesh = PolyMeshPrimitives.Cone(n, height, radius, Color, uv, Symbol.Empty);
                CheckGeometry(mesh, n, height, radius, uv, context);
                Assert.That(mesh.FaceVertexAttributes.Keys, Is.EquivalentTo(new[] { uv, -uv, PolyMesh.Property.Normals }), context);
                var source = Snapshot(mesh);
                for (var flags = 0; flags < 16; flags++)
                {
                    var geometry = mesh.GetIndexedGeometry((PolyMesh.GetGeometryOptions)flags);
                    Assert.That(geometry.IndexedAttributes.Keys, Is.EquivalentTo(new[]
                        { PolyMesh.Property.Positions, PolyMesh.Property.Colors, PolyMesh.Property.Normals, uv }), $"{context}, flags={flags}");
                    Assert.That(geometry.IndexArray.Length, Is.EqualTo(6 * n), context);
                }
                CheckSnapshot(mesh, source, context);
                var defaults = PolyMeshPrimitives.Cone(n, height, radius, C4b.White);
                CheckGeometry(defaults, n, height, radius, PolyMesh.Property.DiffuseColorCoordinates, context, C4b.White);
                Assert.That(defaults.FaceVertexAttributes.Count, Is.EqualTo(3), context);
            });
            foreach (var n in new[] { -1, 0, 1, 2 })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => PolyMeshPrimitives.Cone(n, 1, 1, Color), $"n={n}, default");
                Assert.Throws<ArgumentOutOfRangeException>(() => PolyMeshPrimitives.Cone(n, 1, 1, Color, Names[0].Uv, Names[0].Tangent), $"n={n}, tangent");
            }
        }

        [Test]
        public void ManualGeneratorReferenceAndRepeatedConstructionKeepAttributeOwnership()
        {
            Cases((n, height, radius, uv, tangent, context) =>
            {
                var reference = PolyMeshPrimitives.Cone(n, height, radius, Color, uv, Symbol.Empty);
                var source = Snapshot(reference);
                reference.AddPerFaceVertexTangents(uv, tangent, Symbol.Empty, Sharing(n));
                CheckSnapshot(reference, source, context);
                var mesh = PolyMeshPrimitives.Cone(n, height, radius, Color, uv, tangent);
                Assert.That(mesh.FaceVertexAttributes.ContainsKey(-tangent), Is.True, context);
                foreach (var entry in reference.FaceVertexAttributes)
                {
                    Assert.That(mesh.FaceVertexAttributes[entry.Key], Is.EqualTo(entry.Value), $"{context}, attribute={entry.Key}");
                    Assert.That(mesh.FaceVertexAttributes[entry.Key], Is.Not.SameAs(entry.Value), $"{context}, ownership={entry.Key}");
                }
                var values = (V3d[])mesh.FaceVertexAttributes[-tangent];
                var saved = ((V3d[])reference.FaceVertexAttributes[-tangent])[0];
                values[0] = new V3d(9, 8, 7);
                Assert.That(((V3d[])reference.FaceVertexAttributes[-tangent])[0], Is.EqualTo(saved), context);
                CheckGeometry(mesh, n, height, radius, uv, context);
            });
        }

        private static int[] Sharing(int n)
        {
            var result = new int[6 * n];
            for (var i = 0; i < n; i++)
            {
                result[3 * i] = 3 * i;
                result[3 * i + 1] = i + 1 < n ? 3 * i + 1 : 2;
                result[3 * i + 2] = i == 0 ? 2 : 3 * i - 2;
                result[3 * n + 3 * i] = 3 * n + i;
                result[3 * n + 3 * i + 1] = 3 * n + (i + 1) % n;
                result[3 * n + 3 * i + 2] = 4 * n + i;
            }
            return result;
        }

        private static void CheckGeometry(PolyMesh mesh, int n, double height, double radius, Symbol uv, string context, C4b? color = null)
        {
            Assert.That(mesh.VertexCount, Is.EqualTo(n + 2), context);
            Assert.That(mesh.FaceCount, Is.EqualTo(2 * n), context);
            Assert.That(mesh.VertexIndexCount, Is.EqualTo(6 * n), context);
            Assert.That(mesh.FirstIndexArray, Is.EqualTo(Enumerable.Range(0, 2 * n + 1).Select(i => 3 * i)), context);
            Assert.That(mesh.VertexAttributes.Keys, Is.EquivalentTo(new[] { PolyMesh.Property.Positions, PolyMesh.Property.Colors }), context);
            Assert.That(mesh.FaceAttributes.Count, Is.Zero, context);
            CheckVector(mesh.PositionArray[0], new V3d(0, 0, -height / 2), 1e-12, context);
            CheckVector(mesh.PositionArray[n + 1], new V3d(0, 0, height / 2), 1e-12, context);
            var normals = (V3d[])mesh.FaceVertexAttributes[PolyMesh.Property.Normals];
            var coordinates = (V2d[])mesh.FaceVertexAttributes[-uv];
            Assert.That(normals.Length, Is.EqualTo(6 * n), context);
            Assert.That(coordinates.Length, Is.EqualTo(6 * n), context);
            Assert.That(mesh.FaceVertexAttributes[uv], Is.EqualTo(Enumerable.Range(0, 6 * n)), context);
            Assert.That(mesh.VertexAttributes[PolyMesh.Property.Colors], Is.EqualTo(Enumerable.Repeat(color ?? Color, n + 2)), context);
            for (var i = 0; i < n; i++)
            {
                var angle = 2 * Math.PI * i / n;
                CheckVector(mesh.PositionArray[i + 1], new V3d(radius * Math.Cos(angle), radius * Math.Sin(angle), -height / 2), 1e-12, $"{context}, vertex={i}");
                var bottom = new[] { 0, 1 + (i + 1) % n, 1 + i };
                var side = new[] { 1 + i, 1 + (i + 1) % n, n + 1 };
                var bottomUvs = new[] { new V2d((i + 0.5) / n, 0), new V2d((i + 1.0) / n, 1), new V2d((double)i / n, 1) };
                var sideUvs = new[] { new V2d((double)i / n, 1), new V2d((i + 1.0) / n, 1), new V2d((i + 0.5) / n, 0) };
                var normalAngles = new[] { angle, 2 * Math.PI * ((i + 1) % n) / n, 2 * Math.PI * (i + 0.5) / n };
                for (var k = 0; k < 3; k++)
                {
                    var b = 3 * i + k;
                    var s = 3 * n + b;
                    Assert.That(mesh.VertexIndexArray[b], Is.EqualTo(bottom[k]), context);
                    Assert.That(mesh.VertexIndexArray[s], Is.EqualTo(side[k]), context);
                    CheckVector(normals[b], new V3d(0, 0, -1), 1e-12, context);
                    CheckVector(normals[s], new V3d(Math.Cos(normalAngles[k]), Math.Sin(normalAngles[k]), radius / height).Normalized, 1e-12, context);
                    foreach (var pair in new[] { (b, bottomUvs[k]), (s, sideUvs[k]) })
                    {
                        var actual = coordinates[pair.Item1];
                        Assert.That(double.IsFinite(actual.X) && double.IsFinite(actual.Y), Is.True, context);
                        Assert.That((actual - pair.Item2).Length, Is.LessThan(1e-12), $"{context}, uv={pair.Item1}");
                    }
                }
            }
        }

        // Triangle UV derivatives with atan2 corner angles, independent of the generator's acos weighting.
        private static V3d[] TangentOracle(PolyMesh mesh, Symbol uv, int[] sharing)
        {
            var result = new V3d[sharing.Max() + 1];
            var indices = (int[])mesh.FaceVertexAttributes[uv];
            var values = (V2d[])mesh.FaceVertexAttributes[-uv];
            for (var corner = 0; corner < mesh.VertexIndexCount; corner += 3)
            {
                var p = Enumerable.Range(0, 3).Select(i => mesh.PositionArray[mesh.VertexIndexArray[corner + i]]).ToArray();
                var c = Enumerable.Range(0, 3).Select(i => values[indices[corner + i]]).ToArray();
                var duv1 = c[1] - c[0];
                var duv2 = c[2] - c[0];
                var determinant = duv1.X * duv2.Y - duv2.X * duv1.Y;
                Assert.That(double.IsFinite(determinant) && determinant != 0, Is.True, $"corner={corner}");
                var tangent = ((p[1] - p[0]) * duv2.Y - (p[2] - p[0]) * duv1.Y) / determinant;
                tangent /= tangent.Length;
                for (var i = 0; i < 3; i++)
                {
                    var a = p[(i + 1) % 3] - p[i];
                    var b = p[(i + 2) % 3] - p[i];
                    var angle = Math.Atan2(Vec.Cross(a, b).Length, Vec.Dot(a, b));
                    result[sharing[corner + i]] += tangent * angle;
                }
            }
            for (var i = 0; i < result.Length; i++)
                if (result[i].Length > 0) result[i] /= result[i].Length;
            return result;
        }

        private static V3d Vector(Array values, int index)
            => values is V3f[] floats ? (V3d)floats[index] : ((V3d[])values)[index];

        private static void CheckVector(V3d actual, V3d expected, double tolerance, string context)
        {
            Assert.That(double.IsFinite(expected.X) && double.IsFinite(expected.Y) && double.IsFinite(expected.Z), Is.True, $"{context}, expected finite");
            Assert.That(double.IsFinite(actual.X) && double.IsFinite(actual.Y) && double.IsFinite(actual.Z), Is.True, $"{context}, actual finite");
            Assert.That((actual - expected).Length, Is.LessThanOrEqualTo(tolerance), context);
        }

        private static (SymbolDict<Array> Dictionary, Symbol Key, Array Array, Array Copy)[] Snapshot(PolyMesh mesh)
            => new[] { mesh.VertexAttributes, mesh.FaceVertexAttributes }.SelectMany(d => d.Select(p => (d, p.Key, p.Value, (Array)p.Value.Clone())))
                .Concat(new[] { (mesh.VertexAttributes, PolyMesh.Property.VertexIndexArray, (Array)mesh.VertexIndexArray, (Array)mesh.VertexIndexArray.Clone()),
                    (mesh.VertexAttributes, PolyMesh.Property.FirstIndexArray, (Array)mesh.FirstIndexArray, (Array)mesh.FirstIndexArray.Clone()) }).ToArray();

        private static void CheckSnapshot(PolyMesh mesh, (SymbolDict<Array> Dictionary, Symbol Key, Array Array, Array Copy)[] source, string context)
        {
            foreach (var entry in source)
            {
                var current = entry.Key == PolyMesh.Property.VertexIndexArray ? mesh.VertexIndexArray :
                    entry.Key == PolyMesh.Property.FirstIndexArray ? mesh.FirstIndexArray : entry.Dictionary[entry.Key];
                Assert.That(current, Is.SameAs(entry.Array), $"{context}, identity={entry.Key}");
                Assert.That(current, Is.EqualTo(entry.Copy), $"{context}, contents={entry.Key}");
            }
        }
    }
}
