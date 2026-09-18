using Aardvark.Base;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class DegenerateEdgeCleanupTests
    {
        private static readonly Symbol Direct = "cleanup-direct";
        private static readonly Symbol Indexed = "cleanup-indexed";
        private static readonly Symbol Opaque = "cleanup-opaque";
        private readonly record struct Token(int Number, string Text);

        public static IEnumerable<TestCaseData> FaceCases()
        {
            var cases = new[]
            {
                Array.Empty<int>(), new[] { 0 }, new[] { 0, 0 }, new[] { 0, 0, 0 },
                new[] { 0, 1 }, new[] { 0, 0, 1 }, new[] { 0, 1, 1 }, new[] { 0, 1, 0 },
                new[] { 0, 0, 1, 2 }, new[] { 0, 1, 2, 0 }, new[] { 0, 0, 1, 1, 2, 2, 0, 0 },
                new[] { 0, 1, 0, 1 }, new[] { 0, 1, 2 }, new[] { 0, 1, 2, 3 },
                new[] { 3, 3, 3, 2, 2, 1, 1, 3, 3 }
            };
            for (var i = 0; i < cases.Length; i++)
            foreach (var attributes in new[] { false, true })
            foreach (var spare in new[] { 0, 7 })
                yield return new TestCaseData(cases[i], attributes, spare).SetName($"CleanupFace_{i}_attributes{attributes}_spare{spare}");
        }

        [TestCaseSource(nameof(FaceCases))]
        public void FaceCompactionMatchesRunReference(int[] face, bool attributes, int spare)
        {
            var mesh = Mesh(new[] { face }, spare);
            if (attributes) AddAttributes(mesh);
            Check(mesh);
        }

        [Test]
        public void RepeatedIndexRetainsLastCornerOfTheRun()
        {
            var source = Mesh(new[] { new[] { 0, 0, 1, 2 } });
            source.FaceVertexAttributes[Direct] = new[] { 10, 20, 30, 40 };
            source.FaceVertexAttributes[Indexed] = new[] { 3, 1, 0, 2 };
            var pool = new[] { "red", "green", "blue", "unused" };
            source.FaceVertexAttributes[-Indexed] = pool;
            var result = source.WithoutDegeneratedEdges();
            Assert.That(result.VertexIndicesOfFace(0), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(result.FaceVertexAttributes[Direct], Is.EqualTo(new[] { 20, 30, 40 }));
            Assert.That(result.FaceVertexAttributes[Indexed], Is.EqualTo(new[] { 1, 0, 2 }));
            Assert.That(result.FaceVertexAttributes[-Indexed], Is.SameAs(pool));
            Assert.That(source.FaceVertexAttributes[Direct], Is.EqualTo(new[] { 10, 20, 30, 40 }));
            Assert.That(source.FaceVertexAttributes[Indexed], Is.EqualTo(new[] { 3, 1, 0, 2 }));
        }

        [Test]
        public void RepeatedCleanupDoesNotConsumeTheFollowingTriangle()
        {
            var source = Mesh(new[] { new[] { 0, 0, 0 }, new[] { 0, 1, 2 } });
            AddAttributes(source);
            var once = source.WithoutDegeneratedEdges();
            var twice = once.WithoutDegeneratedEdges();
            Assert.That(twice.FirstIndexArray.Take(3), Is.EqualTo(new[] { 0, 0, 3 }));
            Assert.That(twice.VertexIndicesOfFace(0), Is.Empty);
            Assert.That(twice.VertexIndicesOfFace(1), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(twice.FaceVertexAttributes[Direct], Is.EqualTo(new[] { 3, 4, 5 }));
            Check(source);
        }

        [Test]
        public void EmptyFacesAtEveryPositionAreIndependent([Range(0, 6)] int position, [Values] bool spare)
        {
            var faces = Enumerable.Range(0, 6).Select(i => i % 2 == 0 ? new[] { 0, 0, 1, 2 } : new[] { 3, 4, 5 }).ToList();
            faces.Insert(position, Array.Empty<int>());
            var source = Mesh(faces.ToArray(), spare ? 9 : 0);
            AddAttributes(source);
            Check(source);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(17)]
        public void AllEmptyFacesAndEmptyCornerArraysAreSupported(int count)
        {
            var source = Mesh(Enumerable.Range(0, count).Select(_ => Array.Empty<int>()).ToArray());
            AddAttributes(source);
            Check(source);
        }

        [Test]
        public void DefaultEmptyMeshDoesNotChangeSourceDictionaries()
        {
            var source = new PolyMesh();
            Check(source);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EmptyMeshesWithNoVerticesPreserveAttributes(bool spare)
        {
            var source = Mesh(new[] { Array.Empty<int>(), Array.Empty<int>() }, spare ? 4 : 0);
            source.PositionArray = Array.Empty<V3d>();
            source.VertexAttributes["empty-vertex-data"] = Array.Empty<double>();
            AddAttributes(source);
            Check(source);
        }

        [Test]
        public void DistinctIndicesAtEqualPositionsAreNotWelded()
        {
            var source = Mesh(new[] { new[] { 0, 1, 2 } });
            source.PositionArray = new V3d[8];
            AddAttributes(source);
            var result = Check(source);
            Assert.That(result.VertexIndicesOfFace(0), Is.EqualTo(new[] { 0, 1, 2 }));
        }

        [Test]
        public void NonzeroCornerOffsetIsMappedAndCompacted([Values(1, 5)] int prefix, [Values] bool attributes)
        {
            var source = Mesh(new[] { Array.Empty<int>(), new[] { 0, 0, 1, 2 }, Array.Empty<int>(), new[] { 3, 4, 3 } }, 4, prefix);
            if (attributes) AddAttributes(source);
            Check(source);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NonDoublePositionArraysRemainUntouched(bool changed)
        {
            var source = Mesh(new[] { changed ? new[] { 0, 0, 1, 2 } : new[] { 0, 1, 2 } });
            source.VertexAttributes = new SymbolDict<Array> { [PolyMesh.Property.Positions] = new V3f[8], ["vertex-id"] = Enumerable.Range(0, 8).ToArray() };
            AddAttributes(source);
            Check(source);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TopologyIsInvalidatedOnlyWhenConnectivityChanges(bool changed)
        {
            var source = Mesh(new[] { changed ? new[] { 0, 0, 1, 2 } : new[] { 0, 1, 2 } });
            source.BuildTopology();
            var edgeCount = source.EdgeCount;
            var euler = source.EulerCharacteristic;
            var result = Check(source);
            Assert.That(source.HasTopology, Is.True);
            Assert.That(source.EdgeCount, Is.EqualTo(edgeCount));
            Assert.That(source.EulerCharacteristic, Is.EqualTo(euler));
            Assert.That(result.HasTopology, Is.EqualTo(!changed));
            if (changed)
            {
                Assert.That(result.EulerCharacteristic, Is.EqualTo(int.MinValue));
                result.BuildTopology();
                Assert.That(result.HasTopology, Is.True);
                var fresh = Mesh(new[] { new[] { 0, 1, 2 } });
                fresh.BuildTopology();
                Assert.That(result.EdgeCount, Is.EqualTo(fresh.EdgeCount));
                Assert.That(result.EulerCharacteristic, Is.EqualTo(fresh.EulerCharacteristic));
            }
            else Assert.That(result.EdgeCount, Is.EqualTo(edgeCount));
        }

        [Test]
        public void SeededMixedFacesMatchIndependentCompaction([Range(0, 63)] int seed)
        {
            var random = new Random(seed);
            var faces = Enumerable.Range(0, random.Next(1, 30)).Select(_ =>
                Enumerable.Range(0, random.Next(0, 14)).Select(_ => random.Next(0, 8)).ToArray()).ToArray();
            var source = Mesh(faces, seed % 6, seed % 3);
            AddAttributes(source);
            Check(source);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LateRemovalReplaysOnlyTheIdentityPrefix(bool closing)
        {
            var faces = Enumerable.Repeat(new[] { 0, 1, 2, 3 }, 256)
                .Concat(new[] { Array.Empty<int>(), closing ? new[] { 3, 4, 5, 3 } : new[] { 3, 4, 4, 5 }, new[] { 6, 7 } }).ToArray();
            var source = Mesh(faces, 9);
            AddAttributes(source);
            Check(source);
        }

        [TestCase(1024)]
        [TestCase(16384)]
        public void UnneededCornerMappingDoesNotAllocateByMeshSize(int count)
        {
            var faces = Enumerable.Repeat(new[] { 0, 1, 2, 3 }, count).ToArray();
            var plain = Mesh(faces); var attributed = Mesh(faces); var poolsOnly = Mesh(faces);
            attributed.FaceVertexAttributes[Direct] = new int[count * 4];
            poolsOnly.FaceVertexAttributes[-Opaque] = new[] { new Token(7, "pool") };
            var changed = Mesh(Enumerable.Repeat(new[] { 0, 0, 1, 2 }, count).ToArray());
            var baseline = Allocated(plain);
            // Allow fixed runtime bookkeeping around collections. A separate corner map
            // would cost at least 16 KiB per call here, increasing with the mesh size.
            Assert.That(Allocated(attributed), Is.LessThanOrEqualTo(baseline + 4096));
            Assert.That(Allocated(poolsOnly), Is.LessThanOrEqualTo(baseline + 4096));
            Assert.That(Allocated(changed), Is.LessThanOrEqualTo(baseline + 4096));
        }

        private static long Allocated(PolyMesh mesh)
        {
            for (var i = 0; i < 8; i++) mesh.WithoutDegeneratedEdges();
            var start = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 4; i++) GC.KeepAlive(mesh.WithoutDegeneratedEdges());
            return (GC.GetAllocatedBytesForCurrentThread() - start) / 4;
        }

        private static PolyMesh Mesh(int[][] faces, int spare = 0, int prefix = 0)
        {
            var count = faces.Sum(f => f.Length);
            var indices = Enumerable.Repeat(-123, prefix + count + spare).ToArray();
            var first = new int[faces.Length + spare + 1];
            var offset = prefix;
            for (var f = 0; f < faces.Length; f++)
            {
                first[f] = offset; Array.Copy(faces[f], 0, indices, offset, faces[f].Length); offset += faces[f].Length;
            }
            for (var f = faces.Length; f < first.Length; f++) first[f] = offset;
            var result = new PolyMesh { PositionArray = Enumerable.Range(0, 12).Select(i => new V3d(i % 3, i / 3, 0)).ToArray(), FirstIndexArray = first, VertexIndexArray = indices };
            result.FaceCount = faces.Length; result.VertexIndexCount = prefix + count; result.VertexCount = 8;
            result.FaceAttributes["face-id"] = Enumerable.Range(0, faces.Length + spare).ToArray();
            result.VertexAttributes["vertex-id"] = Enumerable.Range(0, 12).ToArray();
            result.InstanceAttributes["instance"] = new object();
            return result;
        }

        private static void AddAttributes(PolyMesh mesh)
        {
            var count = mesh.VertexIndexArray?.Length ?? 0;
            var indices = Enumerable.Range(0, count).ToArray();
            mesh.FaceVertexAttributes[Direct] = indices;
            mesh.FaceVertexAttributes[Indexed] = indices.Select(i => (i * 7) % 11).ToArray();
            mesh.FaceVertexAttributes[-Indexed] = Enumerable.Range(0, 11).Select(i => new V2d(i, -i)).ToArray();
            mesh.FaceVertexAttributes["vectors"] = indices.Select(i => new V3f(i, -i, 0.5f)).ToArray();
            mesh.FaceVertexAttributes["colors"] = indices.Select(i => new C4b((byte)i, (byte)(i >> 8), (byte)3, (byte)255)).ToArray();
            mesh.FaceVertexAttributes["names"] = indices.Select(i => "corner" + i).ToArray();
            mesh.FaceVertexAttributes["tokens"] = indices.Select(i => new Token(i, "t" + i)).ToArray();
            mesh.FaceVertexAttributes["nullable"] = indices.Select(i => i % 2 == 0 ? (int?)i : null).ToArray();
            mesh.FaceVertexAttributes["objects"] = indices.Select(_ => new object()).ToArray();
            mesh.FaceVertexAttributes[-Opaque] = new[] { Guid.NewGuid() };
            mesh.FaceVertexAttributes[Symbol.Empty] = new[] { "opaque zero key" };
        }

        // Independent run-length model: retain each run's final source corner, then drop
        // a closing run matching the first. Single surviving runs become empty face slots.
        private static int[] RetainedCorners(PolyMesh mesh, int face)
        {
            var corners = new List<int>();
            for (var i = mesh.FirstIndexArray[face]; i < mesh.FirstIndexArray[face + 1]; i++)
            {
                if (corners.Count != 0 && mesh.VertexIndexArray[corners[^1]] == mesh.VertexIndexArray[i]) corners[^1] = i;
                else corners.Add(i);
            }
            if (corners.Count > 1 && mesh.VertexIndexArray[corners[0]] == mesh.VertexIndexArray[corners[^1]]) corners.RemoveAt(corners.Count - 1);
            if (corners.Count < 2) corners.Clear();
            return corners.ToArray();
        }

        private static PolyMesh Check(PolyMesh source)
        {
            var faces = Enumerable.Range(0, source.FaceCount).Select(f => RetainedCorners(source, f)).ToArray();
            var map = faces.SelectMany(f => f).ToArray();
            var expected = map.Select(i => source.VertexIndexArray[i]).ToArray();
            var starts = new int[source.FaceCount + 1];
            for (var i = 0; i < faces.Length; i++) starts[i + 1] = starts[i] + faces[i].Length;
            var vertexCount = source.VertexCount; var indexCount = source.VertexIndexCount; var range = source.FaceVertexCountRange;
            var positions = source.PositionArray; var first = source.FirstIndexArray; var indices = source.VertexIndexArray;
            var arrays = new[] { positions, (Array)first, indices }.Where(a => a != null).Select(a => (Original: a, Snapshot: (Array)a.Clone())).ToArray();
            var dictionaries = new[] { source.VertexAttributes, source.FaceAttributes, source.FaceVertexAttributes, source.EdgeAttributes };
            var entries = dictionaries.Select(d => d.ToArray()).ToArray();
            var attributeSnapshots = entries.SelectMany(e => e).Where(p => p.Value != null).Select(p => (Original: p.Value, Snapshot: (Array)p.Value.Clone())).ToArray();
            var instances = source.InstanceAttributes.ToArray();
            var result = source.WithoutDegeneratedEdges();
            Assert.That(result, Is.Not.SameAs(source));
            Assert.That(result.FaceCount, Is.EqualTo(faces.Length));
            Assert.That(result.VertexCount, Is.EqualTo(vertexCount));
            Assert.That(result.VertexIndexCount, Is.EqualTo(map.Length));
            Assert.That(result.FirstIndexArray.Take(faces.Length + 1), Is.EqualTo(starts));
            Assert.That(result.VertexIndexArray.Take(map.Length), Is.EqualTo(expected));
            var expectedRange = faces.Length == 0 ? Range1i.Invalid : new Range1i(faces.Min(f => f.Length), faces.Max(f => f.Length));
            Assert.That(result.FaceVertexCountRange, Is.EqualTo(expectedRange));
            Assert.That(result[PolyMesh.Property.FaceCount], Is.EqualTo(faces.Length));
            Assert.That(result[PolyMesh.Property.VertexIndexCount], Is.EqualTo(map.Length));
            Assert.That(result[PolyMesh.Property.FaceVertexCountRange], Is.EqualTo(expectedRange));
            foreach (var entry in source.FaceVertexAttributes)
            {
                var actual = result.FaceVertexAttributes[entry.Key];
                Assert.That(actual.GetType(), Is.EqualTo(entry.Value.GetType()));
                if (entry.Key.IsPositive)
                    Assert.That(Enumerable.Range(0, map.Length).Select(i => actual.GetValue(i)), Is.EqualTo(map.Select(i => entry.Value.GetValue(i))));
                else Assert.That(actual, Is.SameAs(entry.Value));
            }
            foreach (var entry in entries[0]) Assert.That(result.VertexAttributes[entry.Key], Is.SameAs(entry.Value));
            foreach (var entry in entries[1]) Assert.That(result.FaceAttributes[entry.Key], Is.SameAs(entry.Value));
            foreach (var entry in instances) Assert.That(result.InstanceAttributes[entry.Key], Is.SameAs(entry.Value));
            foreach (var array in arrays.Concat(attributeSnapshots)) Assert.That(array.Original, Is.EqualTo(array.Snapshot));
            for (var i = 0; i < dictionaries.Length; i++)
            {
                Assert.That(dictionaries[i].Count, Is.EqualTo(entries[i].Length));
                foreach (var entry in entries[i]) Assert.That(dictionaries[i][entry.Key], Is.SameAs(entry.Value));
            }
            Assert.That(source.PositionArray, Is.SameAs(positions));
            Assert.That(source.FirstIndexArray, Is.SameAs(first));
            Assert.That(source.VertexIndexArray, Is.SameAs(indices));
            Assert.That(source.VertexCount, Is.EqualTo(vertexCount));
            Assert.That(source.VertexIndexCount, Is.EqualTo(indexCount));
            Assert.That(source.FaceVertexCountRange, Is.EqualTo(range));
            var again = result.WithoutDegeneratedEdges();
            Assert.That(again.VertexIndexCount, Is.EqualTo(map.Length));
            Assert.That(again.FirstIndexArray.Take(faces.Length + 1), Is.EqualTo(starts));
            Assert.That(again.VertexIndexArray.Take(map.Length), Is.EqualTo(expected));
            foreach (var entry in result.FaceVertexAttributes) Assert.That(again.FaceVertexAttributes[entry.Key], Is.EqualTo(entry.Value));
            return result;
        }
    }
}
