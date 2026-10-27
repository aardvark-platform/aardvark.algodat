using Aardvark.Base;
using Aardvark.Geometry.Clustering;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class FaceEqualClusteringTests
    {
        private static readonly Symbol Custom = (Symbol)"FaceEqualClusteringSource";
        private static readonly double[] Epsilons = { 1e-9, 1e-6, 1.0 };

        [Test]
        public void CyclicRotationsOfTrianglesThroughDecagonsShareExactCoordinateClusters()
        {
            for (var n = 3; n <= 10; n++)
            for (var shift = 0; shift < n; shift++)
            foreach (var separateIds in new[] { false, true })
            foreach (var reverseInput in new[] { false, true })
            foreach (var epsilon in Epsilons)
            {
                var face = Polygon(n);
                var faces = new[] { face, Rotate(face, shift) };
                if (reverseInput) Array.Reverse(faces);
                var context = $"pair n={n}, shift={shift}, separateIds={separateIds}, reverseInput={reverseInput}, epsilon={epsilon:R}";
                var mesh = Create(faces, separateIds);
                if (separateIds) Assert.That(mesh.VertexIndexArray.Distinct().Count(), Is.EqualTo(2 * n), context);
                var clustering = Check(mesh, faces, epsilon, context);
                Assert.That(clustering.Count, Is.EqualTo(1), context);
                Assert.That(clustering.CountArray, Is.EqualTo(new[] { 2 }), context);
            }
        }

        [Test]
        public void MultipleRotatedDuplicatesCompactAllMembersAndCountsRegardlessOfInputOrder()
        {
            for (var n = 3; n <= 10; n++)
            foreach (var order in new[] { 0, 1, 2 })
            foreach (var separateIds in new[] { false, true })
            {
                var face = Polygon(n);
                var faces = Enumerable.Range(0, 3 * n).Select(i => Rotate(face, i % n)).ToArray();
                Reorder(faces, order);
                var context = $"duplicates n={n}, copies={faces.Length}, order={order}, separateIds={separateIds}";
                var result = Check(Create(faces, separateIds), faces, 1e-6, context);
                Assert.That(result.Count, Is.EqualTo(1), context);
                Assert.That(result.IndexArray, Is.All.EqualTo(0), context);
                Assert.That(result.CountArray, Is.EqualTo(new[] { 3 * n }), context);
            }
        }

        [Test]
        public void ChangedCornersWindingConnectivityAndValenceRetainExactSeparation()
        {
            for (var n = 3; n <= 10; n++)
            foreach (var separateIds in new[] { false, true })
            foreach (var epsilon in Epsilons)
            {
                var face = Polygon(n);
                for (var corner = 0; corner < n; corner++)
                {
                    var changed = face.ToArray(); changed[corner].X += 1e-7;
                    var faces = new[] { face, changed };
                    Assert.That(Check(Create(faces, separateIds), faces, epsilon, $"changed n={n}, corner={corner}, separateIds={separateIds}, epsilon={epsilon:R}").Count, Is.EqualTo(2));
                }
                for (var shift = 0; shift < n; shift++)
                {
                    var faces = new[] { face, Rotate(face.Reverse().ToArray(), shift) };
                    Assert.That(Check(Create(faces, separateIds), faces, epsilon, $"reversed n={n}, shift={shift}, separateIds={separateIds}, epsilon={epsilon:R}").Count, Is.EqualTo(2));
                }
                var longer = face.Concat(new[] { face[0] }).ToArray();
                var differentValences = new[] { face, longer };
                Assert.That(Check(Create(differentValences, separateIds), differentValences, epsilon, $"valence n={n}/{n + 1}, separateIds={separateIds}, epsilon={epsilon:R}").Count, Is.EqualTo(2));
                if (n > 3)
                {
                    var reordered = face.ToArray(); (reordered[1], reordered[2]) = (reordered[2], reordered[1]);
                    var faces = new[] { face, reordered };
                    Assert.That(Check(Create(faces, separateIds), faces, epsilon, $"connectivity n={n}, separateIds={separateIds}, epsilon={epsilon:R}").Count, Is.EqualTo(2));
                }
            }
            // Coarse candidate hashes still require exact coordinate equality.
            var a = Polygon(4); var b = a.ToArray(); b[1].X += 1e-7;
            Assert.That(a[1].HashCode1(1.0), Is.EqualTo(b[1].HashCode1(1.0)));
            Check(Create(new[] { a, b }, true), new[] { a, b }, 1.0, "same raster cell, distinct coordinates");
            // Clustering must neither build nor replace already-built topology.
            for (var n = 3; n <= 10; n++)
            {
                var faces = new[] { Polygon(n) }; var mesh = Create(faces, false);
                mesh.BuildTopology(1);
                var edges = mesh.GetFace(0).Edges.ToArray();
                Check(mesh, faces, 1e-6, $"built topology n={n}");
                Assert.That(mesh.EdgeCount, Is.EqualTo(n));
                Assert.That(mesh.GetFace(0).Edges, Is.EqualTo(edges));
            }
        }

        [Test]
        public void MixedGroupsRetainPairwiseMembershipCompactCountsAndSourceArrays()
        {
            var all = new List<V3d[]>();
            for (var n = 3; n <= 10; n++)
            {
                var a = Polygon(n).Select(p => p + new V3d(n * 128, 0, 0)).ToArray();
                var b = a.Select(p => p + new V3d(0, 32, 0)).ToArray();
                var reverse = a.Reverse().ToArray();
                var changed = a.ToArray(); changed[n - 1].Y += 0.125;
                all.Add(a); all.Add(Rotate(a, 1)); all.Add(Rotate(a, n - 1));
                all.Add(b); all.Add(Rotate(b, 1)); all.Add(reverse); all.Add(Rotate(reverse, 1));
                all.Add(changed);
            }
            foreach (var order in new[] { 0, 1, 2 })
            foreach (var separateIds in new[] { false, true })
            foreach (var epsilon in Epsilons)
            {
                var faces = all.ToArray(); Reorder(faces, order);
                var result = Check(Create(faces, separateIds), faces, epsilon, $"mixed order={order}, separateIds={separateIds}, epsilon={epsilon:R}");
                Assert.That(result.Count, Is.EqualTo(32));
                Assert.That(result.CountArray.OrderBy(i => i), Is.EqualTo(Enumerable.Repeat(1, 8).Concat(Enumerable.Repeat(2, 16)).Concat(Enumerable.Repeat(3, 8))));
            }
            Check(Create(Array.Empty<V3d[]>(), false), Array.Empty<V3d[]>(), 1e-6, "empty mesh");
        }

        private static FaceEqualClustering Check(PolyMesh mesh, V3d[][] faces, double epsilon, string context)
        {
            var unchanged = Snapshot(mesh, context);
            var result = new FaceEqualClustering(mesh, epsilon);
            var expected = new List<int>(); var counts = new List<int>();
            for (var i = 0; i < faces.Length; i++)
            {
                var match = -1;
                for (var j = 0; j < i; j++) if (Same(faces[i], faces[j])) { match = expected[j]; break; }
                if (match < 0) { match = counts.Count; counts.Add(0); }
                expected.Add(match); counts[match]++;
            }
            Assert.That(result.Count, Is.EqualTo(counts.Count), context);
            Assert.That(result.IndexArray.Length, Is.EqualTo(faces.Length), context);
            Assert.That(result.CountArray.Length, Is.EqualTo(result.Count), context);
            Assert.That(result.CountArray.Sum(), Is.EqualTo(faces.Length), context);
            Assert.That(result.IndexArray.Distinct().OrderBy(i => i), Is.EqualTo(Enumerable.Range(0, result.Count)), context + ", compact IDs");
            for (var i = 0; i < faces.Length; i++)
            {
                Assert.That(result.CountArray[result.IndexArray[i]], Is.EqualTo(counts[expected[i]]), context + $", size at face {i}");
                for (var j = 0; j < faces.Length; j++)
                    Assert.That(result.IndexArray[i] == result.IndexArray[j], Is.EqualTo(expected[i] == expected[j]), context + $", pair {i}/{j}");
            }
            unchanged();
            return result;
        }

        // Compare all cyclic starts using scalar coordinates, independent of hashing or sorting.
        private static bool Same(V3d[] a, V3d[] b)
        {
            if (a.Length != b.Length) return false;
            for (var start = 0; start < b.Length; start++)
            {
                var equal = true;
                for (var i = 0; i < a.Length; i++)
                {
                    var q = b[(start + i) % b.Length];
                    if (a[i].X != q.X || a[i].Y != q.Y || a[i].Z != q.Z) { equal = false; break; }
                }
                if (equal) return true;
            }
            return false;
        }
        private static V3d[] Polygon(int n) => Enumerable.Range(0, n).Select(i => new V3d(i + 0.125, i * i * 0.25 - 0.375, 2)).ToArray();
        private static V3d[] Rotate(V3d[] face, int shift) => Enumerable.Range(0, face.Length).Select(i => face[(i + shift) % face.Length]).ToArray();
        private static void Reorder<T>(T[] values, int order)
        {
            if (order == 1) Array.Reverse(values);
            if (order == 2)
            {
                var random = new Random(6319);
                for (var i = values.Length - 1; i > 0; i--) { var j = random.Next(i + 1); (values[i], values[j]) = (values[j], values[i]); }
            }
        }
        private static PolyMesh Create(V3d[][] faces, bool separateIds)
        {
            var positions = new List<V3d>(); var first = new List<int> { 0 }; var indices = new List<int>();
            foreach (var face in faces)
            {
                foreach (var p in face)
                {
                    var index = separateIds ? -1 : positions.FindIndex(q => q.X == p.X && q.Y == p.Y && q.Z == p.Z);
                    if (index < 0) { index = positions.Count; positions.Add(p); }
                    indices.Add(index);
                }
                first.Add(indices.Count);
            }
            var mesh = new PolyMesh { PositionArray = positions.ToArray(), FirstIndexArray = first.ToArray(), VertexIndexArray = indices.ToArray() };
            mesh.VertexAttributes.Add(Custom, Enumerable.Range(0, positions.Count).ToArray());
            mesh.FaceAttributes.Add(Custom, Enumerable.Range(0, faces.Length).ToArray());
            mesh.FaceVertexAttributes.Add(Custom, Enumerable.Range(0, indices.Count).ToArray());
            return mesh;
        }
        private static Action Snapshot(PolyMesh mesh, string context)
        {
            var positions = mesh.PositionArray; var pValues = positions.ToArray();
            var first = mesh.FirstIndexArray; var fValues = first.ToArray();
            var indices = mesh.VertexIndexArray; var iValues = indices.ToArray();
            var topology = mesh.HasTopology; var euler = mesh.EulerCharacteristic;
            var vertexCount = mesh.VertexCount; var indexCount = mesh.VertexIndexCount; var faceCount = mesh.FaceCount;
            var dictionaries = new[] { mesh.VertexAttributes, mesh.FaceAttributes, mesh.FaceVertexAttributes };
            var attributes = dictionaries.Select(d => d[Custom]).ToArray();
            var copies = attributes.Select(a => a.Clone()).ToArray();
            return () => {
                Assert.That(mesh.PositionArray, Is.SameAs(positions), context); Assert.That(mesh.PositionArray, Is.EqualTo(pValues), context);
                Assert.That(mesh.FirstIndexArray, Is.SameAs(first), context); Assert.That(mesh.FirstIndexArray, Is.EqualTo(fValues), context);
                Assert.That(mesh.VertexIndexArray, Is.SameAs(indices), context); Assert.That(mesh.VertexIndexArray, Is.EqualTo(iValues), context);
                Assert.That(mesh.VertexCount, Is.EqualTo(vertexCount), context); Assert.That(mesh.VertexIndexCount, Is.EqualTo(indexCount), context);
                Assert.That(mesh.FaceCount, Is.EqualTo(faceCount), context); Assert.That(mesh.HasTopology, Is.EqualTo(topology), context);
                Assert.That(mesh.EulerCharacteristic, Is.EqualTo(euler), context);
                var current = new[] { mesh.VertexAttributes, mesh.FaceAttributes, mesh.FaceVertexAttributes };
                for (var i = 0; i < current.Length; i++) {
                    Assert.That(current[i], Is.SameAs(dictionaries[i]), context);
                    Assert.That(current[i][Custom], Is.SameAs(attributes[i]), context); Assert.That(current[i][Custom], Is.EqualTo(copies[i]), context);
                }
                Assert.That(mesh.PositionArray.All(p => !p.IsNaN && !p.IsInfinity), Is.True, context);
            };
        }
    }
}
