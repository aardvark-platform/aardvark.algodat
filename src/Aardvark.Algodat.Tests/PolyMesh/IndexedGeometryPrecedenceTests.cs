using System;
using System.Collections.Generic;
using System.Linq;
using Aardvark.Base;
using Aardvark.Rendering;
using NUnit.Framework;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class IndexedGeometryPrecedenceTests
    {
        private static readonly Symbol Shared = "export-shared", FaceOnly = "export-face", VertexOnly = "export-vertex";
        private static readonly Symbol FaceNormal = "export-face-normal", FaceColor = "export-face-color", Scalar = "export-scalar";
        private static readonly Symbol Token = "export-token", Label = "export-label";
        private static readonly int[][] Selections = { new[] { 0, 1, 2 }, new[] { 2, 0, 1 }, new[] { 1 } };

        private static void Attribute(SymbolDict<Array> domain, Symbol name, Array values, bool indexed)
        {
            if (!indexed) { domain[name] = values; return; }
            var palette = Array.CreateInstance(values.GetType().GetElementType(), values.Length);
            var indices = new int[values.Length];
            for (var i = 0; i < values.Length; i++) { indices[i] = values.Length - 1 - i; palette.SetValue(values.GetValue(i), indices[i]); }
            domain[name] = indices; domain[-name] = palette;
        }

        private static PolyMesh Fixture(bool faceIndexed, bool vertexIndexed, bool corner, bool distinct)
        {
            var m = new PolyMesh {
                PositionArray = new[] { V3d.Zero, new V3d(2, 0, 0), new V3d(2, 2, 0), new V3d(0, 2, 0), new V3d(-2, 0, 0), new V3d(9, 9, 9) },
                FirstIndexArray = new[] { 0, 4, 7, 10 }, VertexIndexArray = new[] { 0, 1, 2, 3, 0, 3, 4, 0, 1, 2 }
            };
            Attribute(m.FaceAttributes, distinct ? FaceOnly : Shared, new[] { 10, 20, 10 }, faceIndexed);
            Attribute(m.VertexAttributes, distinct ? VertexOnly : Shared, new[] { 100, 101, 102, 103, 104, 105 }, vertexIndexed);
            Attribute(m.FaceAttributes, distinct ? FaceNormal : PolyMesh.Property.Normals, new[] { V3d.OOI, V3d.OIO, V3d.OOI }, faceIndexed);
            Attribute(m.VertexAttributes, PolyMesh.Property.Normals, new[] { -V3d.IOO, -V3d.OIO, -V3d.OOI, -V3d.IOO, -V3d.OIO, -V3d.OOI }, vertexIndexed);
            var red = new C4f(.25f, .5f, 1, 1); var green = new C4f(1, .25f, 0, .5f);
            Attribute(m.FaceAttributes, distinct ? FaceColor : PolyMesh.Property.Colors, new[] { red, green, red }, faceIndexed);
            Attribute(m.VertexAttributes, PolyMesh.Property.Colors, Enumerable.Repeat(new C4f(.75f, .5f, .25f, .25f), 6).ToArray(), vertexIndexed);
            m.VertexAttributes[Scalar] = new[] { .25, 1.25, 2.25, 3.25, 4.25, 5.25 };
            if (corner)
            {
                Attribute(m.FaceVertexAttributes, Shared, new[] { 1000, 1001, 1002, 1003, 2000, 2003, 2004, 1000, 1001, 1002 }, faceIndexed);
                Attribute(m.FaceVertexAttributes, PolyMesh.Property.Normals, Enumerable.Repeat(V3d.IOO, 10).ToArray(), faceIndexed);
                Attribute(m.FaceVertexAttributes, PolyMesh.Property.Colors, Enumerable.Repeat(new C4f(0, 1, .5f, .75f), 10).ToArray(), faceIndexed);
            }
            m.InstanceAttributes[Token] = new object(); m.InstanceAttributes[Label] = "unchanged";
            return m;
        }

        private static SymbolDict<Array>[] Domains(PolyMesh m) => new[] { m.VertexAttributes, m.FaceAttributes, m.FaceVertexAttributes };
        private static object Value(SymbolDict<Array> domain, Symbol name, int i) => domain.TryGetValue(-name, out var values)
            ? values.GetValue(((int[])domain[name])[i]) : domain[name].GetValue(i);
        private static object Converted(object value, int options) => value switch {
            V3d v when (options & 1) != 0 => (V3f)v,
            double v when (options & 1) != 0 => (float)v,
            C4f v when (options & 2) != 0 => new C4b(v), _ => value
        };

        private static Action SourceCheck(PolyMesh m, string context, int[] selection = null)
        {
            var selectionCopy = selection?.ToArray();
            var first = m.FirstIndexArray; var via = m.VertexIndexArray;
            var firstCopy = first.ToArray(); var viaCopy = via.ToArray();
            var domains = Domains(m); var entries = domains.Select(d => d.ToArray()).ToArray();
            var copies = entries.Select(es => es.Select(e => (Array)e.Value.Clone()).ToArray()).ToArray();
            var instances = m.InstanceAttributes; var instanceEntries = instances.ToArray(); var counts = (m.VertexCount, m.FaceCount, m.VertexIndexCount);
            return () => {
                if (selection != null) Assert.That(selection, Is.EqualTo(selectionCopy), context + ": face selection unchanged");
                Assert.That((m.VertexCount, m.FaceCount, m.VertexIndexCount), Is.EqualTo(counts), context + ": source counts");
                Assert.That(m.FirstIndexArray, Is.SameAs(first), context + ": first reference");
                Assert.That(m.VertexIndexArray, Is.SameAs(via), context + ": index reference");
                Assert.That(first, Is.EqualTo(firstCopy), context + ": first values"); Assert.That(via, Is.EqualTo(viaCopy), context + ": index values");
                for (var d = 0; d < domains.Length; d++)
                {
                    Assert.That(Domains(m)[d], Is.SameAs(domains[d]), context + $": domain{d}");
                    Assert.That(domains[d].Count, Is.EqualTo(entries[d].Length), context + $": domain{d} keys");
                    for (var a = 0; a < entries[d].Length; a++)
                    {
                        Assert.That(domains[d][entries[d][a].Key], Is.SameAs(entries[d][a].Value), context + $": domain{d} array{a} reference");
                        Assert.That(entries[d][a].Value, Is.EqualTo(copies[d][a]), context + $": domain{d} array{a} values");
                    }
                }
                Assert.That(m.InstanceAttributes, Is.SameAs(instances), context + ": instance dictionary");
                Assert.That(instances.Count, Is.EqualTo(instanceEntries.Length), context + ": instance keys");
                foreach (var p in instanceEntries) Assert.That(instances[p.Key], Is.SameAs(p.Value), context + $": instance {p.Key}");
            };
        }

        private static void Check(PolyMesh m, IndexedGeometry g, int[] faces, int options, string context)
        {
            // Independent convex fan: retain source face, source corner and selected-corner identity.
            var triangles = new List<(int Face, int Corner, int Vertex, int Selected)>(); var offset = 0;
            foreach (var face in faces)
            {
                var first = m.FirstIndexArray[face]; var count = m.FirstIndexArray[face + 1] - first;
                for (var s = 1; s < count - 1; s++)
                foreach (var local in new[] { 0, s, s + 1 }) triangles.Add((face, first + local, m.VertexIndexArray[first + local], offset + local));
                offset += count;
            }
            Assert.That(g.IndexArray.GetType(), Is.EqualTo((options & 4) != 0 ? typeof(int[]) : typeof(short[])), context + ": index format");
            var indices = g.IndexArray.Cast<object>().Select(Convert.ToInt32).ToArray();
            Assert.That(indices.Length, Is.EqualTo(triangles.Count), context + ": triangle index count");
            Assert.That(g.Mode.ToString(), Is.EqualTo("TriangleList"), context + ": topology");
            Assert.That(indices.Distinct().OrderBy(i => i), Is.EqualTo(Enumerable.Range(0, g.VertexCount)), context + ": no unused output vertices");
            var names = Domains(m).SelectMany(d => d.Keys.Where(n => n.IsPositive)).Distinct().ToArray();
            Assert.That(g.IndexedAttributes.Keys, Is.EquivalentTo(names), context + ": attribute names");
            foreach (var name in names)
            {
                var domain = Domains(m).Last(d => d.ContainsKey(name));
                var sourceValues = domain.TryGetValue(-name, out var palette) ? palette : domain[name];
                var type = sourceValues.GetType().GetElementType();
                if ((options & 1) != 0 && type == typeof(V3d)) type = typeof(V3f);
                if ((options & 1) != 0 && type == typeof(double)) type = typeof(float);
                if ((options & 2) != 0 && type == typeof(C4f)) type = typeof(C4b);
                var actual = g.IndexedAttributes[name];
                Assert.That(actual.GetType().GetElementType(), Is.EqualTo(type), context + $": type {name}");
                Assert.That(actual.Length, Is.EqualTo(g.VertexCount), context + $": vertex alignment {name}");
                for (var t = 0; t < triangles.Count; t++)
                {
                    var c = triangles[t]; var row = ReferenceEquals(domain, m.VertexAttributes) ? c.Vertex : ReferenceEquals(domain, m.FaceAttributes) ? c.Face : c.Corner;
                    var expected = Converted(Value(domain, name, row), options); var value = actual.GetValue(indices[t]);
                    var numeric = value is V3d vd ? new[] { vd.X, vd.Y, vd.Z } : value is V3f vf ? new[] { (double)vf.X, vf.Y, vf.Z }
                        : value is C4f cf ? new[] { (double)cf.R, cf.G, cf.B, cf.A } : value is double d ? new[] { d } : value is float f ? new[] { (double)f } : Array.Empty<double>();
                    Assert.That(numeric.All(double.IsFinite), Is.True, context + $": finite {name}/triangle-corner{t}");
                    Assert.That(value, Is.EqualTo(expected), context + $": {name}/triangle-corner{t}");
                }
            }
            for (var a = 0; a < triangles.Count; a++)
            for (var b = a + 1; b < triangles.Count; b++)
            {
                var ca = triangles[a]; var cb = triangles[b];
                var share = ca.Selected == cb.Selected || ((options & 8) != 0 && ca.Vertex == cb.Vertex
                    && m.FaceAttributes.Keys.Where(n => n.IsPositive).All(n => Equals(Value(m.FaceAttributes, n, ca.Face), Value(m.FaceAttributes, n, cb.Face)))
                    && m.FaceVertexAttributes.Keys.Where(n => n.IsPositive).All(n => Equals(Value(m.FaceVertexAttributes, n, ca.Corner), Value(m.FaceVertexAttributes, n, cb.Corner))));
                Assert.That(indices[a] == indices[b], Is.EqualTo(share), context + $": compact triangle-corners{a}/{b}");
            }
            Assert.That(g.SingleAttributes.Keys, Is.EquivalentTo(m.InstanceAttributes.Keys), context + ": instance keys");
            foreach (var p in m.InstanceAttributes) Assert.That(g.SingleAttributes[p.Key], Is.SameAs(p.Value), context + $": instance {p.Key}");
        }

        [Test]
        public void FaceOverridesVertexForDirectAndIndexedInputsAcrossAllOptions()
        {
            Assert.Multiple(() => {
                foreach (var faceIndexed in new[] { false, true })
                foreach (var vertexIndexed in new[] { false, true })
                foreach (var faces in Selections)
                for (var options = 0; options < 16; options++)
                {
                    var context = $"faceIndexed={faceIndexed}, vertexIndexed={vertexIndexed}, faces=[{string.Join(",", faces)}], options={options}";
                    var m = Fixture(faceIndexed, vertexIndexed, false, false); var unchanged = SourceCheck(m, context, faces);
                    var g = m.GetIndexedGeometry(faces, (PolyMesh.GetGeometryOptions)options);
                    Check(m, g, faces, options, context);
                    unchanged();
                }
            });
        }

        [Test]
        public void CornersAndDistinctNamesRetainValuesConversionsAndSourceArrays()
        {
            foreach (var indexed in new[] { false, true })
            foreach (var corner in new[] { false, true })
            foreach (var faces in Selections)
            for (var options = 0; options < 16; options++)
            {
                var context = $"indexed={indexed}, corner={corner}, faces=[{string.Join(",", faces)}], options={options}";
                var m = Fixture(indexed, indexed, corner, !corner); var unchanged = SourceCheck(m, context, faces);
                var g = m.GetIndexedGeometry(faces, (PolyMesh.GetGeometryOptions)options);
                Check(m, g, faces, options, context);
                foreach (var a in Enumerable.Concat(g.IndexedAttributes.Values, new[] { g.IndexArray })) Array.Clear(a, 0, a.Length);
                unchanged();
            }
        }

        private static void Match(IndexedGeometry a, IndexedGeometry b, string context)
        {
            Assert.That(a.Mode, Is.EqualTo(b.Mode), context + ": mode");
            Assert.That(a.IndexArray.GetType(), Is.EqualTo(b.IndexArray.GetType()), context + ": index type");
            Assert.That(a.IndexArray, Is.EqualTo(b.IndexArray), context + ": index values");
            Assert.That(a.IndexedAttributes.Keys, Is.EquivalentTo(b.IndexedAttributes.Keys), context + ": keys");
            foreach (var p in b.IndexedAttributes)
            {
                Assert.That(a.IndexedAttributes[p.Key].GetType(), Is.EqualTo(p.Value.GetType()), context + $": type {p.Key}");
                Assert.That(a.IndexedAttributes[p.Key], Is.EqualTo(p.Value), context + $": array {p.Key}");
            }
        }

        [Test]
        public void OverloadsKeepMappingsInstancesAndOptionDefaults()
        {
            Assert.That(new[] { (int)PolyMesh.GetGeometryOptions.None, (int)PolyMesh.GetGeometryOptions.FloatVectors,
                (int)PolyMesh.GetGeometryOptions.ByteColors, (int)PolyMesh.GetGeometryOptions.IntIndices,
                (int)PolyMesh.GetGeometryOptions.Compact, (int)PolyMesh.GetGeometryOptions.FloatVectorsAndByteColors,
                (int)PolyMesh.GetGeometryOptions.Default }, Is.EqualTo(new[] { 0, 1, 2, 4, 8, 7, 15 }));
            var m = Fixture(true, true, false, true); var unchanged = SourceCheck(m, "overloads");
            Match(m.GetIndexedGeometry(), m.GetIndexedGeometry((PolyMesh.GetGeometryOptions)15), "default options");
            Check(m, m.GetIndexedGeometry(), new[] { 0, 1, 2 }, 15, "all faces forwarding");
            foreach (var faces in Selections)
            for (var options = 0; options < 16; options++)
            {
                var o = (PolyMesh.GetGeometryOptions)options; var context = $"forward faces=[{string.Join(",", faces)}], options={options}";
                var g = m.GetIndexedGeometry(faces, o);
                var names = m.GetIndexedGeometry(o, 1e-5, faces, m.FaceAttributeNames, m.VertexAttributeNames, m.FaceVertexAttributeNames, m.InstanceAttributes.Keys);
                var attributes = m.GetIndexedGeometry(o, 1e-5, faces, m.FaceIAttributes.ToArray(), m.VertexIAttributes.ToArray(), m.FaceVertexIAttributes.ToArray(), m.InstanceAttributes);
                Match(g, names, context + ": names"); Match(g, attributes, context + ": IAttribute");
                Check(m, names, faces, options, context); Check(m, attributes, faces, options, context);
                Assert.That(attributes.SingleAttributes, Is.SameAs(m.InstanceAttributes), context + ": explicit instance dictionary forwarding");
            }
            unchanged();
        }
    }
}
