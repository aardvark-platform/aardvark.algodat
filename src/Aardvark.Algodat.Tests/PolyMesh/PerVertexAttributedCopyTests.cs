using System;
using System.Collections.Generic;
using System.Linq;
using Aardvark.Base;
using NUnit.Framework;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class PerVertexAttributedCopyTests
    {
        private static readonly Symbol FaceLabel = "copy-face-label";
        private static readonly Symbol Shared = "copy-precedence";
        private static readonly Symbol Uv = PolyMesh.Property.DiffuseColorCoordinates;
        private static readonly Symbol Normal = PolyMesh.Property.Normals;
        private static readonly Symbol Color = PolyMesh.Property.Colors;

        private static void Attribute(SymbolDict<Array> domain, Symbol name, int[] indices, Array values, bool indexed)
        {
            if (indexed) { domain[name] = indices; domain[-name] = values; }
            else
            {
                var direct = Array.CreateInstance(values.GetType().GetElementType(), indices.Length);
                for (var i = 0; i < indices.Length; i++) direct.SetValue(values.GetValue(indices[i]), i);
                domain[name] = direct;
            }
        }

        private static PolyMesh Fixture(int domains, bool indexed)
        {
            var mesh = new PolyMesh {
                PositionArray = new[] { V3d.Zero, V3d.IOO, new V3d(1, 1, 0), V3d.OIO,
                    new V3d(-1, 1, 0), new V3d(7, 8, 9), new V3d(9, 8, 7) },
                FirstIndexArray = new[] { 0, 4, 7, 11 },
                VertexIndexArray = new[] { 0, 1, 2, 3, 0, 3, 4, 0, 1, 2, 3 }
            };
            if ((domains & 1) != 0)
            {
                Attribute(mesh.FaceAttributes, FaceLabel, new[] { 1, 2, 0 }, new[] { "other", "same", "same" }, indexed);
                Attribute(mesh.FaceAttributes, Normal, new[] { 1, 2, 0 }, new[] { -V3d.OOI, V3d.OOI, V3d.OOI }, indexed);
                mesh.FaceAttributes[Shared] = new[] { .5f, .5f, 1.5f };
            }
            if ((domains & 2) != 0)
            {
                var indices = new[] { 2, 0, 1, 2, 0, 1, 0 };
                Attribute(mesh.VertexAttributes, Color, indices, new[] { C4b.Red, C4b.Green, C4b.Blue }, indexed);
                Attribute(mesh.VertexAttributes, Normal, indices, new[] { V3f.IOO, V3f.OIO, V3f.OOI }, indexed);
                mesh.VertexAttributes[Shared] = Enumerable.Range(10, 7).ToArray();
            }
            if ((domains & 4) != 0)
            {
                Attribute(mesh.FaceVertexAttributes, Uv, new[] { 0, 1, 2, 3, 4, 3, 5, 6, 1, 2, 3 }, new[] {
                    new V2f(0, 0), new V2f(1, 0), new V2f(1, 1), new V2f(0, 1),
                    new V2f(.5f, 0), new V2f(-1, 1), new V2f(0, 0) }, indexed);
                Attribute(mesh.FaceVertexAttributes, Normal, new int[11], new[] { V3d.OIO }, indexed);
                mesh.FaceVertexAttributes[Shared] = Enumerable.Repeat(42.0, 11).ToArray();
            }
            return mesh;
        }

        private static SymbolDict<Array>[] Domains(PolyMesh mesh) =>
            new[] { mesh.FaceAttributes, mesh.VertexAttributes, mesh.FaceVertexAttributes };

        private static object Value(SymbolDict<Array> domain, Symbol name, int index) =>
            domain.TryGetValue(-name, out var values) ? values.GetValue(((int[])domain[name])[index]) : domain[name].GetValue(index);

        private static Action SourceCheck(PolyMesh source, string context)
        {
            var first = source.FirstIndexArray; var via = source.VertexIndexArray; var positions = source.PositionArray;
            var firstValues = first.ToArray(); var viaValues = via.ToArray();
            var domains = Domains(source); var entries = domains.Select(d => d.ToArray()).ToArray();
            var values = entries.Select(es => es.Select(e => (Array)e.Value.Clone()).ToArray()).ToArray();
            var counts = (source.VertexCount, source.FaceCount, source.VertexIndexCount);
            return () => {
                Assert.That((source.VertexCount, source.FaceCount, source.VertexIndexCount), Is.EqualTo(counts), context + ": source counts");
                Assert.That(source.PositionArray, Is.SameAs(positions), context + ": source positions reference");
                Assert.That(source.FirstIndexArray, Is.SameAs(first), context + ": source first reference");
                Assert.That(source.VertexIndexArray, Is.SameAs(via), context + ": source indices reference");
                Assert.That(first, Is.EqualTo(firstValues), context + ": source first values");
                Assert.That(via, Is.EqualTo(viaValues), context + ": source index values");
                var current = Domains(source);
                for (var d = 0; d < domains.Length; d++)
                {
                    Assert.That(current[d], Is.SameAs(domains[d]), context + $": source domain{d}");
                    Assert.That(domains[d].Count, Is.EqualTo(entries[d].Length), context + $": source key count{d}");
                    for (var a = 0; a < entries[d].Length; a++)
                    {
                        var entry = entries[d][a]; var label = context + $": source domain{d}/{entry.Key}";
                        Assert.That(domains[d][entry.Key], Is.SameAs(entry.Value), label + " reference");
                        Assert.That(entry.Value, Is.EqualTo(values[d][a]), label + " values");
                    }
                }
            };
        }

        private static void CheckOutput(PolyMesh source, PolyMesh output, int[] faces, bool compact, string context)
        {
            var corners = faces.SelectMany(f => Enumerable.Range(source.FirstIndexArray[f],
                source.FirstIndexArray[f + 1] - source.FirstIndexArray[f]).Select(c => (Face: f, Corner: c, Vertex: source.VertexIndexArray[c]))).ToArray();
            var first = new List<int> { 0 };
            foreach (var f in faces) first.Add(first.Last() + source.FirstIndexArray[f + 1] - source.FirstIndexArray[f]);
            Assert.That(output.FirstIndexArray, Is.EqualTo(first), context + ": face topology");
            Assert.That(output.FaceCount, Is.EqualTo(faces.Length), context + ": face count");
            Assert.That(output.VertexIndexArray.Length, Is.EqualTo(corners.Length), context + ": corner count");
            Assert.That(output.VertexIndexCount, Is.EqualTo(corners.Length), context + ": index count");
            Assert.That(output.VertexIndexArray.Distinct().OrderBy(i => i), Is.EqualTo(Enumerable.Range(0, output.VertexCount)), context + ": no unused output vertices");
            Assert.That(output.FaceAttributes.Count, Is.Zero, context + ": no remaining face attributes");
            Assert.That(output.FaceVertexAttributes.Count, Is.Zero, context + ": no remaining corner attributes");
            var expected = new Dictionary<Symbol, object[]>(); var types = new Dictionary<Symbol, Type>();
            foreach (var domain in new[] { source.VertexAttributes, source.FaceAttributes, source.FaceVertexAttributes })
            foreach (var name in domain.Keys.Where(n => n.IsPositive))
            {
                expected[name] = corners.Select(c => Value(domain, name, ReferenceEquals(domain, source.VertexAttributes)
                    ? c.Vertex : ReferenceEquals(domain, source.FaceAttributes) ? c.Face : c.Corner)).ToArray();
                types[name] = (domain.TryGetValue(-name, out var values) ? values : domain[name]).GetType();
            }
            Assert.That(output.VertexAttributes.Keys, Is.EquivalentTo(expected.Keys), context + ": positive-only output keys");
            foreach (var pair in expected)
            {
                var actual = output.VertexAttributes[pair.Key]; var label = context + $": attribute={pair.Key}";
                Assert.That(actual.GetType(), Is.EqualTo(types[pair.Key]), label + " runtime array type");
                Assert.That(actual.Length, Is.EqualTo(output.VertexCount), label + " direct vertex length");
                Assert.That(PolyMesh.GetIAttribute(pair.Key, output.VertexAttributes).IndexArray, Is.Null, label + " direct representation");
                for (var c = 0; c < corners.Length; c++)
                    Assert.That(actual.GetValue(output.VertexIndexArray[c]), Is.EqualTo(pair.Value[c]), label + $" corner{c}");
            }
            // Compaction compares all face/corner values, even attributes shadowed in the output.
            for (var a = 0; a < corners.Length; a++)
            for (var b = a + 1; b < corners.Length; b++)
            {
                var ca = corners[a]; var cb = corners[b];
                var share = compact && ca.Vertex == cb.Vertex
                    && source.FaceAttributes.Keys.Where(n => n.IsPositive).All(n => Equals(Value(source.FaceAttributes, n, ca.Face), Value(source.FaceAttributes, n, cb.Face)))
                    && source.FaceVertexAttributes.Keys.Where(n => n.IsPositive).All(n => Equals(Value(source.FaceVertexAttributes, n, ca.Corner), Value(source.FaceVertexAttributes, n, cb.Corner)));
                Assert.That(output.VertexIndexArray[a] == output.VertexIndexArray[b], Is.EqualTo(share), context + $": compact corners{a}/{b}");
            }
        }

        private static PolyMesh Explicit(PolyMesh mesh, bool compact, int[] faces) => mesh.PerVertexAttributedCopy(
            faces, compact, mesh.FaceAttributeNames, mesh.VertexAttributeNames, mesh.FaceVertexAttributeNames);

        private static void Match(PolyMesh actual, PolyMesh expected, string context)
        {
            Assert.That(actual.FirstIndexArray, Is.EqualTo(expected.FirstIndexArray), context + ": explicit first indices");
            Assert.That(actual.VertexIndexArray, Is.EqualTo(expected.VertexIndexArray), context + ": explicit vertex indices");
            Assert.That(actual.VertexAttributes.Keys, Is.EquivalentTo(expected.VertexAttributes.Keys), context + ": explicit keys");
            foreach (var pair in expected.VertexAttributes)
            {
                Assert.That(actual.VertexAttributes[pair.Key].GetType(), Is.EqualTo(pair.Value.GetType()), context + $": explicit type {pair.Key}");
                Assert.That(actual.VertexAttributes[pair.Key], Is.EqualTo(pair.Value), context + $": explicit values {pair.Key}");
            }
        }

        [Test]
        public void IndexedDomainsExpandSeamsWithExistingCompactionAndPrecedence()
        {
            Assert.Multiple(() => {
                foreach (var domains in new[] { 1, 2, 4, 7 })
                foreach (var compact in new[] { false, true })
                {
                    var context = $"indexed domains={domains}, compact={compact}";
                    var source = Fixture(domains, true); var unchanged = SourceCheck(source, context);
                    var faces = new[] { 0, 1, 2 }; var explicitCopy = Explicit(source, compact, faces);
                    CheckOutput(source, explicitCopy, faces, compact, context + ": positive-name baseline");
                    PolyMesh actual = null;
                    Assert.DoesNotThrow(() => actual = source.PerVertexAttributedCopy(compact), context);
                    if (actual == null) { unchanged(); continue; }
                    CheckOutput(source, actual, faces, compact, context);
                    Match(actual, explicitCopy, context);
                    var expectedCount = !compact ? 11 : domains == 1 ? 9 : domains == 2 ? 5 : domains == 4 ? 6 : 10;
                    Assert.That(actual.VertexCount, Is.EqualTo(expectedCount), context + ": independent vertex count");
                    foreach (var array in Enumerable.Concat(actual.VertexAttributes.Values, new Array[] { actual.FirstIndexArray, actual.VertexIndexArray }))
                        Array.Clear(array, 0, array.Length);
                    unchanged();
                }
            });
        }

        [Test]
        public void DirectOnlyAndEmptyMeshesRetainTypedPerVertexConversions()
        {
            foreach (var domains in new[] { 0, 1, 2, 4, 7, -1 })
            foreach (var compact in new[] { false, true })
            {
                var source = domains < 0 ? new PolyMesh { PositionArray = Array.Empty<V3d>(), FirstIndexArray = new[] { 0 }, VertexIndexArray = Array.Empty<int>() }
                    : Fixture(domains, false);
                var context = $"direct domains={domains}, compact={compact}"; var unchanged = SourceCheck(source, context);
                var faces = Enumerable.Range(0, source.FaceCount).ToArray();
                var actual = source.PerVertexAttributedCopy(compact);
                CheckOutput(source, actual, faces, compact, context);
                Match(actual, Explicit(source, compact, faces), context);
                unchanged();
            }
        }

        [Test]
        public void ExplicitSelectionsKeepBackMapsAndNegativeNameValidation()
        {
            foreach (var compact in new[] { false, true })
            {
                var source = Fixture(7, true); var unchanged = SourceCheck(source, $"explicit compact={compact}");
                foreach (var faces in new[] { new[] { 2, 0, 2, 1 }, new[] { 1 }, Array.Empty<int>() })
                {
                    var context = $"faces=[{string.Join(",", faces)}], compact={compact}"; var snapshot = faces.ToArray();
                    var copy = Explicit(source, compact, faces);
                    CheckOutput(source, copy, faces, compact, context);
                    var attributes = source.PerVertexAttributedCopy(faces, compact, source.FaceIAttributes.ToArray(),
                        source.VertexIAttributes.ToArray(), source.FaceVertexIAttributes.ToArray());
                    Match(copy, attributes, context + ": IAttribute overload");
                    Assert.That(faces, Is.EqualTo(snapshot), context + ": face selection unchanged");
                }
                foreach (var pair in new[] { (source.FaceAttributes, FaceLabel), (source.VertexAttributes, Color), (source.FaceVertexAttributes, Uv) })
                    Assert.Throws<InvalidOperationException>(() => PolyMesh.GetIAttribute(-pair.Item2, pair.Item1));
                Assert.Throws<InvalidOperationException>(() => source.PerVertexAttributedCopy(new[] { 0 }, compact,
                    new[] { -FaceLabel }, source.VertexAttributeNames, source.FaceVertexAttributeNames));
                Assert.Throws<InvalidOperationException>(() => source.PerVertexAttributedCopy(new[] { 0 }, compact,
                    source.FaceAttributeNames, new[] { -Color }, source.FaceVertexAttributeNames));
                Assert.Throws<InvalidOperationException>(() => source.PerVertexAttributedCopy(new[] { 0 }, compact,
                    source.FaceAttributeNames, source.VertexAttributeNames, new[] { -Uv }));
                unchanged();
            }
        }
    }
}
