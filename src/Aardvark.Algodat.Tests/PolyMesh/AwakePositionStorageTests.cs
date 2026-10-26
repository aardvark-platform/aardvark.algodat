using Aardvark.Base;
using Aardvark.Base.Coder;
using NUnit.Framework;
using System;
using System.Linq;
using System.Reflection;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class AwakePositionStorageTests
    {
        private static readonly Symbol Custom = (Symbol)"AwakeStorageCustom";
        private static readonly string[] Representations = { "V3d", "V3f", "V2d", "V2f" };
        private static readonly FieldInfo CountCache = typeof(PolyMesh).GetField("m_vertexCount", BindingFlags.Instance | BindingFlags.NonPublic);
        static AwakePositionStorageTests() => Aardvark.Base.Coder.TypeInfo.Add(typeof(PolyMesh));

        [Test]
        public void CopyOverloadsPreservePositionRepresentationsActiveCountsAndShallowSharing()
        {
            foreach (var representation in Representations)
            foreach (var layout in new[] { (Capacity: 3, Active: 3), (Capacity: 8, Active: 3), (Capacity: 0, Active: 0), (Capacity: 8, Active: 0) })
            foreach (var cacheKind in new[] { 0, 1, 2 })
            for (var operation = 0; operation < 6; operation++)
            {
                var context = $"copy representation={representation}, layout={layout}, caches={cacheKind}, operation={operation}";
                var source = Create(representation, layout.Capacity, layout.Active, cacheKind);
                var raw = source.VertexAttributes[PolyMesh.Property.Positions];
                var unchanged = Snapshot(source, context);
                var copy = MakeCopy(source, operation);
                CheckRestored(copy, raw, layout.Active, true, context);
                Assert.That(copy, Is.Not.SameAs(source), context);
                Assert.That(copy.VertexAttributes, operation == 5 ? Is.Not.SameAs(source.VertexAttributes) : Is.SameAs(source.VertexAttributes), context);
                Assert.That(copy.FaceAttributes, operation == 5 ? Is.Not.SameAs(source.FaceAttributes) : Is.SameAs(source.FaceAttributes), context);
                Assert.That(copy.FaceVertexAttributes, operation == 5 ? Is.Not.SameAs(source.FaceVertexAttributes) : Is.SameAs(source.FaceVertexAttributes), context);
                Assert.That(copy.EdgeAttributes, Is.SameAs(source.EdgeAttributes), context);
                Assert.That(copy.FirstIndexArray, Is.SameAs(source.FirstIndexArray), context);
                Assert.That(copy.VertexIndexArray, Is.SameAs(source.VertexIndexArray), context);
                Assert.That(copy.InstanceAttributes, operation == 3 || operation == 4 ? Is.Not.SameAs(source.InstanceAttributes) : Is.SameAs(source.InstanceAttributes), context);
                Assert.That(copy.InstanceAttributes[Custom], Is.EqualTo(operation == 3 || operation == 4 ? "override" : "instance"), context);
                foreach (var kv in source.VertexAttributes) Assert.That(copy.VertexAttributes[kv.Key], Is.SameAs(kv.Value), context);
                if (layout.Active >= 3) CheckExport(copy, raw, context);
                unchanged();
            }
        }

        [Test]
        public void PublicAwakeAndBinaryRoundTripsPreserveRawStorageIncludingEmptyAndMissingPositions()
        {
            foreach (var representation in Representations)
            foreach (var layout in new[] { (Capacity: 0, Active: 0), (Capacity: 8, Active: 0), (Capacity: 3, Active: 3), (Capacity: 8, Active: 3) })
            foreach (var cacheKind in new[] { 0, 1, 2 })
            {
                var context = $"restore representation={representation}, layout={layout}, caches={cacheKind}";
                var source = Create(representation, layout.Capacity, layout.Active, cacheKind);
                var raw = source.VertexAttributes[PolyMesh.Property.Positions];
                CheckAwakeAndRoundTrip(source, raw, layout.Active, true, context);
            }
            foreach (var includePositions in new[] { false, true })
            {
                var source = Create("V3d", 0, 0, 0);
                if (includePositions) source.VertexAttributes[PolyMesh.Property.Positions] = null;
                else source.VertexAttributes.Remove(PolyMesh.Property.Positions);
                source.VertexAttributes = source.VertexAttributes;
                for (var operation = 0; operation < 6; operation++)
                    CheckRestored(MakeCopy(source, operation), null, 0, includePositions, $"missing/null copy, present={includePositions}, operation={operation}");
                CheckAwakeAndRoundTrip(source, null, 0, includePositions, $"missing/null positions, present={includePositions}");
            }
        }

        [Test]
        public void FloatAndNativeTranslationsTransformCoordinatesWithoutChangingTheirSources()
        {
            foreach (var representation in new[] { "V3f", "V3d" })
            foreach (var layout in new[] { (Capacity: 0, Active: 0), (Capacity: 8, Active: 0), (Capacity: 3, Active: 3), (Capacity: 8, Active: 3) })
            {
                var context = $"translate representation={representation}, layout={layout}";
                var source = Create(representation, layout.Capacity, layout.Active, 0);
                var raw = source.VertexAttributes[PolyMesh.Property.Positions];
                var unchanged = Snapshot(source, context);
                var translated = source.Transformed(Trafo3d.Translation(new V3d(10, 20, 30)));
                var result = translated.VertexAttributes[PolyMesh.Property.Positions];
                Assert.That(result.GetType(), Is.EqualTo(raw.GetType()), context);
                Assert.That(result.Length, Is.EqualTo(raw.Length), context);
                Assert.That(translated.VertexCount, Is.EqualTo(layout.Active), context);
                Assert.That(translated.Get<int>(PolyMesh.Property.VertexCount), Is.EqualTo(layout.Active), context);
                for (var i = 0; i < raw.Length; i++) Equal(Vector(result, i), Vector(raw, i) + new V3d(10, 20, 30), context);
                if (raw.Length > 0) Equal(Vector(result, 0), new V3d(11, 22, 33), context);
                Assert.That(translated.PositionArray, Is.SameAs(result as V3d[]), context);
                unchanged();
            }
        }

        [Test]
        public void NativeTopologyAndNormalColorCachesRetainCopyAndRestorationContracts()
        {
            foreach (var cacheKind in new[] { 0, 1, 2 })
            {
                var source = Create("V3d", 8, 3, cacheKind);
                source.BuildTopology(1);
                var raw = source.PositionArray;
                var unchanged = Snapshot(source, $"native topology, caches={cacheKind}");
                for (var operation = 0; operation < 6; operation++)
                {
                    var context = $"native topology caches={cacheKind}, copy={operation}";
                    var copy = MakeCopy(source, operation);
                    CheckRestored(copy, raw, 3, true, context);
                    CheckTopology(copy, context);
                    Assert.That(TopologyArray(copy, "m_faceEdgeRefArray"), Is.SameAs(TopologyArray(source, "m_faceEdgeRefArray")), context);
                    Assert.That(TopologyArray(copy, "m_edgeFaceRefArray"), Is.SameAs(TopologyArray(source, "m_edgeFaceRefArray")), context);
                }
                var without = source.Copy(PolyMesh.OverridesNoTopology);
                Assert.That(without.HasTopology, Is.False);
                CheckRestored(without, raw, 3, true, "no-topology override");
                // Poison only caches, then restore them from the unchanged stored map.
                foreach (var field in new[] { "m_positionArray", "m_normalArray", "m_colorArray" })
                    typeof(PolyMesh).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(source, null);
                CountCache.SetValue(source, -1);
                source.Awake(0);
                CheckRestored(source, raw, 3, true, "public native Awake");
                CheckTopology(source, "public native Awake");
                var decoded = source.Encode().Decode<PolyMesh>();
                CheckRestored(decoded, decoded.VertexAttributes[PolyMesh.Property.Positions], 3, true, "native topology round trip");
                CheckTopology(decoded, "native topology round trip");
                Assert.That(decoded.PositionArray, Is.EqualTo(raw));
                unchanged();
            }
        }

        [Test]
        public void RepeatedCacheRestorationDoesNotAllocateOrReplacePositionArrays()
        {
            foreach (var representation in Representations)
            foreach (var capacity in new[] { 0, 8, 4096 })
            {
                var active = capacity == 0 ? 0 : 3;
                var mesh = Create(representation, capacity, active, 0);
                var raw = mesh.VertexAttributes[PolyMesh.Property.Positions];
                var dictionary = mesh.VertexAttributes;
                for (var i = 0; i < 16; i++) mesh.Awake(0);
                var start = GC.GetAllocatedBytesForCurrentThread();
                for (var i = 0; i < 128; i++) mesh.Awake(0);
                var bytes = GC.GetAllocatedBytesForCurrentThread() - start;
                var context = $"allocation representation={representation}, capacity={capacity}";
                Assert.That(bytes, Is.Zero, context);
                CheckRestored(mesh, raw, active, true, context);
                Assert.That(mesh.VertexAttributes, Is.SameAs(dictionary), context);
            }
        }

        private static PolyMesh MakeCopy(PolyMesh source, int operation)
        {
            var overrides = new SymbolDict<object> { { PolyMesh.Property.InstanceAttributes, new SymbolDict<object> { { Custom, "override" } } } };
            switch (operation)
            {
                case 0: return source.Copy();
                case 1: return new PolyMesh(source);
                case 2: return source.Copy((SymbolDict<object>)null);
                case 3: return source.Copy(overrides);
                case 4: return new PolyMesh(source, overrides);
                default: return source.Copy(new SymbolDict<Func<Array, Array>>(), a => a);
            }
        }

        private static PolyMesh Create(string representation, int capacity, int active, int cacheKind)
        {
            var data = Enumerable.Range(0, capacity).Select(i =>
                i == 0 ? new V3d(1, 2, 3) : i == 1 ? new V3d(4, 2, 3) :
                i == 2 ? new V3d(1, 6, 3) : new V3d(20 + i, 30 + i, 40 + i)).ToArray();
            Array positions = representation == "V3d" ? data :
                representation == "V3f" ? data.Select(p => (V3f)p).ToArray() :
                representation == "V2d" ? data.Select(p => new V2d(p.X, p.Y)).ToArray() :
                data.Select(p => new V2f((float)p.X, (float)p.Y)).ToArray();
            var attributes = new SymbolDict<Array> { { PolyMesh.Property.Positions, positions }, { Custom, Enumerable.Range(0, capacity).ToArray() } };
            if (cacheKind == 0)
            {
                attributes.Add(PolyMesh.Property.Normals, Enumerable.Repeat(V3d.OOI, capacity).ToArray());
                attributes.Add(PolyMesh.Property.Colors, Enumerable.Repeat(new C4f(0.25f, 0.5f, 0.75f, 1f), capacity).ToArray());
            }
            else if (cacheKind == 1)
            {
                attributes.Add(PolyMesh.Property.Normals, Enumerable.Repeat(V3f.OOI, capacity).ToArray());
                attributes.Add(PolyMesh.Property.Colors, Enumerable.Repeat(C4b.White, capacity).ToArray());
            }
            else
            {
                attributes.Add(PolyMesh.Property.Normals, new int[capacity]);
                attributes.Add(-PolyMesh.Property.Normals, new[] { V3d.OOI });
                attributes.Add(PolyMesh.Property.Colors, new int[capacity]);
                attributes.Add(-PolyMesh.Property.Colors, new[] { C4f.White });
            }
            var mesh = new PolyMesh
            {
                FirstIndexArray = active >= 3 ? new[] { 0, 3 } : new[] { 0 },
                VertexIndexArray = active >= 3 ? new[] { 0, 1, 2 } : Array.Empty<int>(),
                VertexAttributes = attributes
            };
            // Prepare active-count metadata independently of the cache-restoration code under test.
            mesh[PolyMesh.Property.VertexCount] = active;
            CountCache.SetValue(mesh, active);
            mesh.FaceVertexAttributes.Add(Custom, active >= 3 ? new[] { 101f, 102f, 103f } : Array.Empty<float>());
            mesh.FaceAttributes.Add(Custom, new[] { 42f });
            mesh.EdgeAttributes.Add(Custom, new[] { 17 });
            mesh.InstanceAttributes.Add(Custom, "instance");
            return mesh;
        }

        private static void CheckAwakeAndRoundTrip(PolyMesh source, Array raw, int active, bool present, string context)
        {
            var unchanged = Snapshot(source, context);
            source.Awake(0);
            source.Awake(0);
            CheckRestored(source, raw, active, present, context);
            var decoded = source.Encode().Decode<PolyMesh>();
            var expectedDomains = new[] { source.VertexAttributes, source.FaceAttributes, source.FaceVertexAttributes, source.EdgeAttributes };
            var decodedDomains = new[] { decoded.VertexAttributes, decoded.FaceAttributes, decoded.FaceVertexAttributes, decoded.EdgeAttributes };
            for (var domain = 0; domain < expectedDomains.Length; domain++)
            {
                Assert.That(decodedDomains[domain].Keys, Is.EquivalentTo(expectedDomains[domain].Keys), context);
                foreach (var kv in expectedDomains[domain])
                {
                    Assert.That(decodedDomains[domain][kv.Key]?.GetType(), Is.EqualTo(kv.Value?.GetType()), context);
                    Assert.That(decodedDomains[domain][kv.Key], Is.EqualTo(kv.Value), context);
                }
            }
            Assert.That(decoded.InstanceAttributes[Custom], Is.EqualTo(source.InstanceAttributes[Custom]), context);
            Assert.That(decoded.FirstIndexArray, Is.EqualTo(source.FirstIndexArray), context);
            Assert.That(decoded.VertexIndexArray, Is.EqualTo(source.VertexIndexArray), context);
            Assert.That(decoded.FaceCount, Is.EqualTo(source.FaceCount), context);
            Assert.That(decoded.VertexIndexCount, Is.EqualTo(source.VertexIndexCount), context);
            Assert.That(decoded.VertexAttributes.ContainsKey(PolyMesh.Property.Positions), Is.EqualTo(present), context);
            var decodedRaw = present ? decoded.VertexAttributes[PolyMesh.Property.Positions] : null;
            Assert.That(decodedRaw?.GetType(), Is.EqualTo(raw?.GetType()), context);
            Assert.That(decodedRaw, Is.EqualTo(raw), context);
            CheckRestored(decoded, decodedRaw, active, present, context);
            if (active >= 3) { CheckExport(source, raw, context); CheckExport(decoded, raw, context); }
            unchanged();
        }

        private static void CheckRestored(PolyMesh mesh, Array raw, int active, bool present, string context)
        {
            Assert.That(mesh.VertexCount, Is.EqualTo(active), context);
            Assert.That(mesh.Get<int>(PolyMesh.Property.VertexCount), Is.EqualTo(active), context);
            Assert.That(mesh.VertexAttributes.ContainsKey(PolyMesh.Property.Positions), Is.EqualTo(present), context);
            if (present) Assert.That(mesh.VertexAttributes[PolyMesh.Property.Positions], Is.SameAs(raw), context);
            Assert.That(mesh.PositionArray, Is.SameAs(raw as V3d[]), context);
            Assert.That(mesh.NormalArray, Is.SameAs(mesh.VertexAttributes[PolyMesh.Property.Normals] as V3d[]), context);
            Assert.That(mesh.ColorArray, Is.SameAs(mesh.VertexAttributes[PolyMesh.Property.Colors] as C4f[]), context);
        }

        private static void CheckExport(PolyMesh mesh, Array raw, string context)
        {
            foreach (var options in new[] { PolyMesh.GetGeometryOptions.Default, PolyMesh.GetGeometryOptions.None })
            {
                var geometry = mesh.GetIndexedGeometry(options);
                var output = geometry.IndexedAttributes[PolyMesh.Property.Positions];
                var expectedType = options == PolyMesh.GetGeometryOptions.None ? raw.GetType() : raw is V2d[] || raw is V2f[] ? typeof(V2f[]) : typeof(V3f[]);
                Assert.That(output.GetType(), Is.EqualTo(expectedType), context);
                Assert.That(geometry.IndexArray.Length, Is.EqualTo(3), context);
                for (var i = 0; i < 3; i++)
                {
                    var index = Convert.ToInt32(geometry.IndexArray.GetValue(i));
                    Assert.That(index, Is.InRange(0, output.Length - 1), context);
                    Equal(Vector(output, index), Vector(raw, i), context);
                }
            }
        }

        private static void CheckTopology(PolyMesh mesh, string context)
        {
            Assert.That(mesh.HasTopology, Is.True, context);
            Assert.That(mesh.EdgeCount, Is.EqualTo(3), context);
            Assert.That(mesh.BorderEdgeCount, Is.EqualTo(3), context);
            Assert.That(mesh.NonManifoldEdgeCount, Is.Zero, context);
            Assert.That(mesh.EulerCharacteristic, Is.EqualTo(1), context);
            Assert.That(mesh.BoundingBox3d, Is.EqualTo(new Box3d(new V3d(1, 2, 3), new V3d(4, 6, 3))), context);
            Assert.That(mesh.GetFace(0).Edges.Select(e => (e.FromVertex.Index, e.ToVertex.Index)), Is.EqualTo(new[] { (0, 1), (1, 2), (2, 0) }), context);
        }
        private static object TopologyArray(PolyMesh mesh, string name) => typeof(PolyMesh).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(mesh);

        private static Action Snapshot(PolyMesh mesh, string context)
        {
            var dictionaries = new[] { mesh.VertexAttributes, mesh.FaceAttributes, mesh.FaceVertexAttributes, mesh.EdgeAttributes };
            var entries = dictionaries.Select(d => d.ToArray()).ToArray();
            var copies = entries.Select(xs => xs.Select(kv => kv.Value?.Clone()).ToArray()).ToArray();
            var instance = mesh.InstanceAttributes;
            var first = mesh.FirstIndexArray; var indices = mesh.VertexIndexArray;
            var firstValues = first.ToArray(); var indexValues = indices.ToArray();
            var faceCount = mesh.FaceCount; var indexCount = mesh.VertexIndexCount;
            var count = mesh.VertexCount; var stored = mesh.Get<int>(PolyMesh.Property.VertexCount);
            var positions = mesh.PositionArray; var normals = mesh.NormalArray; var colors = mesh.ColorArray;
            return () =>
            {
                var current = new[] { mesh.VertexAttributes, mesh.FaceAttributes, mesh.FaceVertexAttributes, mesh.EdgeAttributes };
                for (var domain = 0; domain < current.Length; domain++)
                {
                    Assert.That(current[domain], Is.SameAs(dictionaries[domain]), context);
                    Assert.That(current[domain].Keys, Is.EquivalentTo(entries[domain].Select(kv => kv.Key)), context);
                    for (var i = 0; i < entries[domain].Length; i++)
                    {
                        var key = entries[domain][i].Key;
                        Assert.That(current[domain][key], Is.SameAs(entries[domain][i].Value), context);
                        Assert.That(current[domain][key], Is.EqualTo(copies[domain][i]), context);
                    }
                }
                Assert.That(mesh.InstanceAttributes, Is.SameAs(instance), context);
                Assert.That(mesh.InstanceAttributes[Custom], Is.EqualTo("instance"), context);
                Assert.That(mesh.FirstIndexArray, Is.SameAs(first), context);
                Assert.That(mesh.VertexIndexArray, Is.SameAs(indices), context);
                Assert.That(mesh.FirstIndexArray, Is.EqualTo(firstValues), context);
                Assert.That(mesh.VertexIndexArray, Is.EqualTo(indexValues), context);
                Assert.That(mesh.FaceCount, Is.EqualTo(faceCount), context);
                Assert.That(mesh.VertexIndexCount, Is.EqualTo(indexCount), context);
                Assert.That(mesh.VertexCount, Is.EqualTo(count), context);
                Assert.That(mesh.Get<int>(PolyMesh.Property.VertexCount), Is.EqualTo(stored), context);
                Assert.That(mesh.PositionArray, Is.SameAs(positions), context);
                Assert.That(mesh.NormalArray, Is.SameAs(normals), context);
                Assert.That(mesh.ColorArray, Is.SameAs(colors), context);
            };
        }

        private static V3d Vector(Array array, int index)
        {
            var p = array.GetValue(index);
            if (p is V3d a) return a;
            if (p is V3f b) return (V3d)b;
            if (p is V2d c) return new V3d(c.X, c.Y, 0);
            var d = (V2f)p; return new V3d(d.X, d.Y, 0);
        }
        private static void Equal(V3d actual, V3d expected, string context)
        {
            Assert.That(actual.IsNaN || actual.IsInfinity, Is.False, context);
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(1e-13), context);
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(1e-13), context);
            Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(1e-13), context);
        }
    }
}
