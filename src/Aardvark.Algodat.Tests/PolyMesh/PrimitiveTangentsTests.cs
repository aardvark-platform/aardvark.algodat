using System;
using System.Collections.Generic;
using Aardvark.Base;
using NUnit.Framework;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class PrimitiveTangentsTests
    {
        private static IEnumerable<(string Kind, int Major, int Minor)> Fixtures()
        {
            foreach (var n in new[] { 3, 4, 8 })
            foreach (var kind in new[] { "Sphere", "Cylinder", "Torus" })
                yield return (kind, n, n);
            yield return ("Torus", 3, 8);
            yield return ("Torus", 8, 3);
        }

        private static IEnumerable<(Symbol Uv, Symbol Tangent, Symbol Binormal)> Names()
        {
            yield return (PolyMesh.Property.DiffuseColorCoordinates,
                PolyMesh.Property.DiffuseColorUTangents, PolyMesh.Property.DiffuseColorVTangents);
            yield return (Symbol.Create("primitive-uv"), Symbol.Create("primitive-tangent"), Symbol.Create("primitive-binormal"));
        }

        private static PolyMesh Build((string Kind, int Major, int Minor) fixture,
            Symbol uv, Symbol tangent, Symbol binormal, bool lower = false)
        {
            switch (fixture.Kind)
            {
                case "Sphere": return lower
                    ? PolyMeshPrimitives.Sphere(fixture.Major, 2.5, C4b.Red, uv, tangent)
                    : PolyMeshPrimitives.Sphere(fixture.Major, 2.5, C4b.Red, uv, tangent, binormal);
                case "Cylinder": return lower
                    ? PolyMeshPrimitives.Cylinder(fixture.Major, 3.5, 1.25, C4b.Red, uv, tangent)
                    : PolyMeshPrimitives.Cylinder(fixture.Major, 3.5, 1.25, C4b.Red, uv, tangent, binormal);
                case "Torus": return lower
                    ? PolyMeshPrimitives.Torus(3.5, .75, fixture.Major, fixture.Minor, C4b.Red, uv, tangent)
                    : PolyMeshPrimitives.Torus(3.5, .75, fixture.Major, fixture.Minor, C4b.Red, uv, tangent, binormal);
                default: throw new ArgumentException(fixture.Kind);
            }
        }

        private static void AssertAttributes(SymbolDict<Array> actual, SymbolDict<Array> expected, string context)
        {
            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys), context + ": keys");
            foreach (var item in expected)
                Assert.That(actual[item.Key], Is.EqualTo(item.Value), context + $": attribute={item.Key}");
        }

        private static void AssertGeometryAndUvs(PolyMesh actual, PolyMesh bare, Symbol uv, string context)
        {
            Assert.That(actual.PositionArray, Is.EqualTo(bare.PositionArray), context + ": positions");
            Assert.That(actual.VertexIndexArray, Is.EqualTo(bare.VertexIndexArray), context + ": vertex indices");
            Assert.That(actual.FirstIndexArray, Is.EqualTo(bare.FirstIndexArray), context + ": face slices");
            AssertAttributes(actual.VertexAttributes, bare.VertexAttributes, context + ": vertex attributes");
            AssertAttributes(actual.FaceAttributes, bare.FaceAttributes, context + ": face attributes");
            foreach (var item in bare.FaceVertexAttributes)
                Assert.That(actual.FaceVertexAttributes[item.Key], Is.EqualTo(item.Value), context + $": base corner attribute={item.Key}");
            foreach (var p in actual.PositionArray)
                Assert.That(double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z), Is.True, context + $": finite position={p}");
            foreach (var p in (V2d[])actual.FaceVertexAttributes[-uv])
                Assert.That(double.IsFinite(p.X) && double.IsFinite(p.Y), Is.True, context + $": finite UV={p}");
        }

        private static void AssertIndexedOutput(PolyMesh mesh, PolyMesh control, Symbol name, string context)
        {
            var indices = (int[])mesh.FaceVertexAttributes[name];
            var values = (V3d[])mesh.FaceVertexAttributes[-name];
            Assert.That(indices.Length, Is.EqualTo(mesh.VertexIndexArray.Length), context + ": corner index count");
            Assert.That(indices, Is.EqualTo(control.FaceVertexAttributes[name]), context + ": sharing indices");
            Assert.That(values, Is.EqualTo(control.FaceVertexAttributes[-name]), context + ": values");
            for (var i = 0; i < indices.Length; i++)
            {
                Assert.That(indices[i], Is.InRange(0, values.Length - 1), context + $": corner={i}");
                var v = values[indices[i]];
                Assert.That(double.IsFinite(v.X) && double.IsFinite(v.Y) && double.IsFinite(v.Z), Is.True,
                    context + $": finite indexed output corner={i}, index={indices[i]}, value={v}");
            }
        }

        private static void AssertOutputKeys(PolyMesh mesh, PolyMesh bare, Symbol tangent, Symbol binormal, string context)
        {
            var keys = new HashSet<Symbol>(bare.FaceVertexAttributes.Keys);
            foreach (var name in new[] { tangent, binormal })
                if (name.IsNotEmpty) { keys.Add(name); keys.Add(-name); }
            Assert.That(mesh.FaceVertexAttributes.Keys, Is.EquivalentTo(keys), context + ": only requested outputs");
            foreach (var attributes in new[] { mesh.VertexAttributes, mesh.FaceAttributes, mesh.FaceVertexAttributes })
                Assert.That(attributes.ContainsKey(Symbol.Empty), Is.False, context + ": no empty-symbol attribute");
        }

        [Test]
        public void BinormalOnlyPrimitivesMatchIndexedBothOutputControls()
        {
            Assert.Multiple(() =>
            {
                foreach (var fixture in Fixtures())
                foreach (var names in Names())
                {
                    var context = $"{fixture}, UV={names.Uv}, binormal={names.Binormal}";
                    var bare = Build(fixture, names.Uv, Symbol.Empty, Symbol.Empty, lower: true);
                    var both = Build(fixture, names.Uv, names.Tangent, names.Binormal);
                    PolyMesh only = null;
                    Assert.DoesNotThrow(() => only = Build(fixture, names.Uv, Symbol.Empty, names.Binormal), context + ": construction");
                    Assert.That(only, Is.Not.Null, context + ": constructed mesh");
                    if (only == null) continue;
                    AssertGeometryAndUvs(only, bare, names.Uv, context);
                    AssertIndexedOutput(only, both, names.Binormal, context);
                    AssertOutputKeys(only, bare, Symbol.Empty, names.Binormal, context);
                    Assert.That(only.FaceVertexAttributes.ContainsKey(names.Tangent), Is.False, context + ": no tangent indices");
                    Assert.That(only.FaceVertexAttributes.ContainsKey(-names.Tangent), Is.False, context + ": no tangent values");
                }
            });
        }

        [Test]
        public void ExistingPrimitiveOutputModesRetainGeometryUvsAndGeneratorResults()
        {
            Assert.Multiple(() =>
            {
                foreach (var fixture in Fixtures())
                foreach (var names in Names())
                foreach (var mode in new[] { "tangent-only", "both", "neither" })
                {
                    var tangent = mode == "neither" ? Symbol.Empty : names.Tangent;
                    var binormal = mode == "both" ? names.Binormal : Symbol.Empty;
                    var context = $"{fixture}, mode={mode}, UV={names.Uv}";
                    var bare = Build(fixture, names.Uv, Symbol.Empty, Symbol.Empty, lower: true);
                    var direct = Build(fixture, names.Uv, tangent, Symbol.Empty, lower: true);
                    if (tangent.IsNotEmpty)
                        direct.AddPerFaceVertexTangents(names.Uv, tangent, binormal, (int[])direct.FaceVertexAttributes[tangent]);
                    var mesh = Build(fixture, names.Uv, tangent, binormal);
                    AssertGeometryAndUvs(mesh, bare, names.Uv, context);
                    AssertAttributes(mesh.FaceVertexAttributes, direct.FaceVertexAttributes, context);
                    AssertOutputKeys(mesh, bare, tangent, binormal, context);
                    foreach (var name in new[] { tangent, binormal })
                        if (name.IsNotEmpty) AssertIndexedOutput(mesh, direct, name, context + $", output={name}");
                }
            });
        }
    }
}
