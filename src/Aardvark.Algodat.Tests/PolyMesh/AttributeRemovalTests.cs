using System;
using System.Collections.Generic;
using System.Linq;
using Aardvark.Base;
using NUnit.Framework;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class AttributeRemovalTests
    {
        private static readonly Symbol Target = "remove-target", Keep = "remove-keep", Missing = "remove-missing";
        private static readonly Symbol EdgeOnly = "remove-edge-only", Orphan = "remove-orphan", Scratch = "remove-scratch";

        private static SymbolDict<Array> Arrays(PolyMesh m, int domain) => domain == 1 ? m.VertexAttributes
            : domain == 2 ? m.FaceAttributes : domain == 3 ? m.FaceVertexAttributes : m.EdgeAttributes;
        private static object Dictionary(PolyMesh m, int domain) => domain == 0 ? (object)m.InstanceAttributes : Arrays(m, domain);
        private static IEnumerable<KeyValuePair<Symbol, object>> Entries(PolyMesh m, int domain) => domain == 0
            ? m.InstanceAttributes : Arrays(m, domain).Select(p => new KeyValuePair<Symbol, object>(p.Key, p.Value));

        private static PolyMesh Fixture(bool empty, bool topology, int domains = 0, bool indexed = false)
        {
            var m = new PolyMesh {
                PositionArray = empty ? Array.Empty<V3d>() : new[] { V3d.Zero, V3d.IOO, new V3d(1, 1, 0), V3d.OIO },
                FirstIndexArray = empty ? new[] { 0 } : new[] { 0, 3, 6 },
                VertexIndexArray = empty ? Array.Empty<int>() : new[] { 0, 1, 2, 0, 2, 3 }
            };
            if (topology) m.BuildTopology();
            m.InstanceAttributes[Keep] = new object();
            m.VertexAttributes[Keep] = Enumerable.Range(0, m.VertexCount).Select(i => i + .25).ToArray();
            m.FaceAttributes[Keep] = Enumerable.Range(0, m.FaceCount).ToArray();
            m.FaceVertexAttributes[Keep] = Enumerable.Range(0, m.VertexIndexCount).Select(i => new V2f(i, -i)).ToArray();
            m.EdgeAttributes[Keep] = new int[m.EdgeCount];
            m.EdgeAttributes[EdgeOnly] = new int[m.EdgeCount]; m.EdgeAttributes[-EdgeOnly] = new[] { 7, 9 };
            m.EdgeAttributes[Target] = new int[m.EdgeCount]; m.EdgeAttributes[-Target] = new[] { 11, 13 };
            m.VertexAttributes[-Orphan] = new[] { 101, 103 };
            if ((domains & 1) != 0)
            {
                m.InstanceAttributes[Target] = indexed ? (object)new[] { 1, 0 } : new object();
                if (indexed) m.InstanceAttributes[-Target] = new[] { "low", "high" };
            }
            foreach (var d in new[] { 1, 2, 3 })
            {
                if ((domains & (1 << d)) == 0) continue;
                var count = d == 1 ? m.VertexCount : d == 2 ? m.FaceCount : m.VertexIndexCount;
                var dictionary = Arrays(m, d);
                dictionary[Target] = indexed ? Enumerable.Range(0, count).Select(i => i % 2).ToArray() : Enumerable.Range(100 * d, count).ToArray();
                if (indexed) dictionary[-Target] = new[] { new V2f(d, .25f), new V2f(-d, .75f) };
            }
            return m;
        }

        private static int[] Topology(PolyMesh m)
        {
            var result = new List<int> { m.HasTopology ? 1 : 0, m.EulerCharacteristic, m.EdgeCount, m.BorderEdgeCount, m.NonManifoldEdgeCount, m.HalfEdgeCount };
            if (m.HasTopology)
            {
                foreach (var e in m.Edges)
                    result.AddRange(new[] { e.Ref, e.FromVertexIndex, e.ToVertexIndex, e.Face.Index, e.Face.Side,
                        e.OppositeFace.Index, e.OppositeFace.Side, e.IsBorder ? 1 : 0, e.OppositeIsBorder ? 1 : 0 });
                foreach (var face in m.Faces) result.AddRange(face.Edges.Select(e => e.Ref));
            }
            return result.ToArray();
        }

        private static Action SourceCheck(PolyMesh m, string context)
        {
            var first = m.FirstIndexArray; var via = m.VertexIndexArray; var positions = m.PositionArray;
            var fc = first.ToArray(); var vc = via.ToArray(); var topology = Topology(m);
            var counts = (m.VertexCount, m.FaceCount, m.VertexIndexCount);
            var dictionaries = Enumerable.Range(0, 5).Select(d => Dictionary(m, d)).ToArray();
            var entries = Enumerable.Range(0, 5).Select(d => Entries(m, d).ToArray()).ToArray();
            var values = entries.Select(es => es.Select(p => p.Value is Array a ? a.Clone() : p.Value).ToArray()).ToArray();
            return () => {
                Assert.That((m.VertexCount, m.FaceCount, m.VertexIndexCount), Is.EqualTo(counts), context + ": source counts");
                Assert.That(m.PositionArray, Is.SameAs(positions), context + ": source positions reference");
                Assert.That(m.FirstIndexArray, Is.SameAs(first), context + ": source first reference");
                Assert.That(m.VertexIndexArray, Is.SameAs(via), context + ": source indices reference");
                Assert.That(first, Is.EqualTo(fc), context + ": source first values"); Assert.That(via, Is.EqualTo(vc), context + ": source indices values");
                Assert.That(Topology(m), Is.EqualTo(topology), context + ": source topology");
                for (var d = 0; d < 5; d++)
                {
                    Assert.That(Dictionary(m, d), Is.SameAs(dictionaries[d]), context + $": source dictionary{d}");
                    var current = Entries(m, d).ToDictionary(p => p.Key, p => p.Value);
                    Assert.That(current.Keys, Is.EquivalentTo(entries[d].Select(p => p.Key)), context + $": source keys{d}");
                    for (var a = 0; a < entries[d].Length; a++)
                    {
                        var p = entries[d][a]; Assert.That(current[p.Key], Is.SameAs(p.Value), context + $": source reference{d}/{p.Key}");
                        Assert.That(p.Value, Is.EqualTo(values[d][a]), context + $": source values{d}/{p.Key}");
                    }
                }
            };
        }

        private static void Removed(PolyMesh source, PolyMesh copy, int domains, string context)
        {
            Assert.That(copy, Is.Not.SameAs(source), context + ": distinct copy");
            Assert.That(copy.PositionArray, Is.SameAs(source.PositionArray), context + ": shallow positions");
            Assert.That(copy.FirstIndexArray, Is.SameAs(source.FirstIndexArray), context + ": shallow first indices");
            Assert.That(copy.VertexIndexArray, Is.SameAs(source.VertexIndexArray), context + ": shallow vertex indices");
            Assert.That((copy.VertexCount, copy.FaceCount, copy.VertexIndexCount), Is.EqualTo((source.VertexCount, source.FaceCount, source.VertexIndexCount)), context + ": geometry counts");
            Assert.That(Topology(copy), Is.EqualTo(Topology(source)), context + ": preserved topology");
            for (var d = 0; d < 5; d++)
            {
                var changed = d < 4 && (domains & (1 << d)) != 0;
                if (changed) Assert.That(Dictionary(copy, d), Is.Not.SameAs(Dictionary(source, d)), context + $": copied domain{d}");
                else Assert.That(Dictionary(copy, d), Is.SameAs(Dictionary(source, d)), context + $": shared domain{d}");
                var expected = Entries(source, d).Where(p => !changed || (p.Key != Target && p.Key != -Target)).ToDictionary(p => p.Key, p => p.Value);
                var actual = Entries(copy, d).ToDictionary(p => p.Key, p => p.Value);
                Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys), context + $": keys domain{d}");
                foreach (var p in expected) Assert.That(actual[p.Key], Is.SameAs(p.Value), context + $": retained value{d}/{p.Key}");
            }
            if (copy.HasTopology)
                foreach (var e in copy.Edges) Assert.That(e.Mesh, Is.SameAs(copy), context + ": topology facades use copy");
        }

        [Test]
        public void MissingRepeatedAndFluentRemovalsKeepTheOriginalObject()
        {
            Assert.Multiple(() => {
                foreach (var empty in new[] { false, true })
                foreach (var topology in new[] { false, true })
                {
                    var context = $"empty={empty}, topology={topology}"; var m = Fixture(empty, topology, 15, true);
                    var unchanged = SourceCheck(m, context);
                    Assert.That(m.HasTopology, Is.EqualTo(topology), context + ": fixture topology");
                    foreach (var name in new[] { Missing, EdgeOnly, Orphan, Symbol.Empty })
                        Assert.That(m.WithoutAttribute(name), Is.SameAs(m), context + $": missing {name}");
                    PolyMesh noOps = null;
                    Assert.DoesNotThrow(() => noOps = m.WithoutAttribute(Missing).WithoutAttribute(Missing).WithoutAttribute(EdgeOnly).WithoutAttribute(Orphan), context + ": no-op chain");
                    Assert.That(noOps, Is.SameAs(m), context + ": fluent no-op identity");
                    if (empty) { unchanged(); continue; }
                    var removed = m.WithoutAttribute(Target);
                    Removed(m, removed, 15, context + ": initial removal");
                    Assert.That(removed.WithoutAttribute(Target), Is.SameAs(removed), context + ": repeated removal identity");
                    PolyMesh repeated = null;
                    Assert.DoesNotThrow(() => repeated = removed.WithoutAttribute(Target).WithoutAttribute(Missing), context + ": repeat chain");
                    Assert.That(repeated, Is.SameAs(removed), context + ": repeated chain identity");
                    PolyMesh mixed = null;
                    Assert.DoesNotThrow(() => mixed = m.WithoutAttribute(Missing).WithoutAttribute(Target).WithoutAttribute(Missing), context + ": mixed chain");
                    Assert.That(mixed, Is.Not.Null, context + ": mixed result");
                    if (mixed != null) Removed(m, mixed, 15, context + ": mixed removal");
                    unchanged();
                }
            });
        }

        [Test]
        public void ExistingAttributesRetainDomainPoolAndShallowCopySemantics()
        {
            foreach (var domains in new[] { 1, 2, 4, 8, 15 })
            foreach (var indexed in new[] { false, true })
            foreach (var topology in new[] { false, true })
            foreach (var name in indexed ? new[] { Target, -Target } : new[] { Target })
            {
                var context = $"domains={domains}, indexed={indexed}, topology={topology}, name={name}";
                var m = Fixture(false, topology, domains, indexed); var unchanged = SourceCheck(m, context);
                var copy = m.WithoutAttribute(name);
                Assert.That(copy, Is.Not.Null, context);
                Removed(m, copy, domains, context);
                // Only changed dictionaries are owned; unrelated arrays/domains intentionally remain shared.
                for (var d = 0; d < 4; d++)
                {
                    if ((domains & (1 << d)) == 0) continue;
                    if (d == 0) copy.InstanceAttributes[Scratch] = new object();
                    else Arrays(copy, d)[Scratch] = Array.Empty<int>();
                }
                unchanged();
            }
            Assert.Throws<NullReferenceException>(() => PolyMeshExtensions.WithoutAttribute(null, Target), "null receiver behavior unchanged");
        }

        [Test]
        public void NoOpRemovalAllocatesNoBytes()
        {
            foreach (var empty in new[] { false, true })
            foreach (var topology in new[] { false, true })
            {
                var m = Fixture(empty, topology); var names = new[] { Missing, EdgeOnly, Orphan, Symbol.Empty };
                foreach (var name in names)
                {
                    for (var i = 0; i < 64; i++) GC.KeepAlive(m.WithoutAttribute(name));
                    var before = GC.GetAllocatedBytesForCurrentThread();
                    for (var i = 0; i < 4096; i++) GC.KeepAlive(m.WithoutAttribute(name));
                    var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                    Assert.That(allocated, Is.Zero, $"empty={empty}, topology={topology}, name={name}");
                }
            }
        }
    }
}
