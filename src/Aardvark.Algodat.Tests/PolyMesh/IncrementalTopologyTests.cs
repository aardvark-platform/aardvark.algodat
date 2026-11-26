using Aardvark.Base;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class IncrementalTopologyTests
    {
        // Keep capacity inspection local: topology buffers are not a public API.
        private static readonly FieldInfo FaceEdges = typeof(PolyMesh).GetField(
            "m_faceEdgeRefArray", BindingFlags.Instance | BindingFlags.NonPublic);

        private static int[] Buffer(PolyMesh mesh) => (int[])FaceEdges.GetValue(mesh);

        [Test]
        public void SpareAndExactFitCapacityReuseTheExistingBuffer()
        {
            foreach (var valence in new[] { 3, 4, 5 })
            foreach (var spare in new[] { 0, 1, 17 })
            {
                var faces = new List<int[]>();
                var holes = new List<int[]>();
                var mesh = CreatePyramids(new[] { valence }, faces, holes);
                var next = holes.Single();
                var oldCount = mesh.VertexIndexCount;
                var buffer = Enumerable.Repeat(-12345, oldCount + valence + spare).ToArray();
                Array.Copy(Buffer(mesh), buffer, oldCount);
                FaceEdges.SetValue(mesh, buffer);
                var prefix = buffer.Take(oldCount).ToArray();
                var context = $"valence={valence}, spare={spare}";

                Assert.That(mesh.AddFaceWithTopology(next), Is.EqualTo(faces.Count), context);
                faces.Add(next);
                Assert.That(Buffer(mesh), Is.SameAs(buffer), context);
                Assert.That(buffer.Take(oldCount), Is.EqualTo(prefix), context);
                Assert.That(buffer.Skip(mesh.VertexIndexCount), Is.All.EqualTo(-12345), context);
                CheckTopology(mesh, faces, context);
            }
        }

        [Test]
        public void GrowthKeepsActiveReferencesAndTheExistingGrowthFormula()
        {
            foreach (var valence in new[] { 3, 4, 5 })
            foreach (var shortBy in new[] { 1, valence })
            {
                var faces = new List<int[]>();
                var holes = new List<int[]>();
                var mesh = CreatePyramids(new[] { valence }, faces, holes);
                var next = holes.Single();
                var oldCount = mesh.VertexIndexCount;
                var buffer = new int[oldCount + valence - shortBy];
                Array.Copy(Buffer(mesh), buffer, oldCount);
                FaceEdges.SetValue(mesh, buffer);
                var prefix = buffer.Take(oldCount).ToArray();
                var context = $"valence={valence}, shortBy={shortBy}";

                Assert.That(mesh.AddFaceWithTopology(next), Is.EqualTo(faces.Count), context);
                faces.Add(next);
                Assert.That(Buffer(mesh), Is.Not.SameAs(buffer), context);
                Assert.That(Buffer(mesh).Length, Is.EqualTo((int)(oldCount * 1.5 + valence)), context);
                Assert.That(Buffer(mesh).Take(oldCount), Is.EqualTo(prefix), context);
                Assert.That(buffer.Take(oldCount), Is.EqualTo(prefix), context);
                CheckTopology(mesh, faces, context);
            }
        }

        [Test]
        public void RepeatedTriangleAndMixedValenceInsertionsKeepTopology()
        {
            foreach (var mixed in new[] { false, true })
            {
                var faces = new List<int[]>();
                var holes = new List<int[]>();
                var mesh = CreatePyramids(Enumerable.Range(0, 24).Select(i => mixed ? 3 + i % 3 : 3), faces, holes);
                for (var i = 0; i < holes.Count; i++)
                {
                    var hole = holes[i];
                    var context = $"pyramids mixed={mixed}, insertion={i}";
                    if (mixed && i == 1)
                    {
                        // A diagonal between existing vertices also takes the new-edge path.
                        AppendAndCheck(mesh, faces, new[] { hole[0], hole[1], hole[2] }, context + ", first half");
                        AppendAndCheck(mesh, faces, new[] { hole[0], hole[2], hole[3] }, context + ", second half");
                    }
                    else AppendAndCheck(mesh, faces, hole, context);
                }
                // Reject an already two-sided edge before any other side is changed.
                var refs = Buffer(mesh).Take(mesh.VertexIndexCount).ToArray();
                var edgeCount = mesh.EdgeCount;
                Assert.That(mesh.AddFaceWithTopology(holes[0]), Is.EqualTo(-1));
                Assert.That(Buffer(mesh).Take(mesh.VertexIndexCount), Is.EqualTo(refs));
                Assert.That(mesh.EdgeCount, Is.EqualTo(edgeCount));
                CheckTopology(mesh, faces, $"rejected pyramids mixed={mixed}");
            }

            foreach (var size in new[] { 4, 8, 16 })
            {
                var positions = new V3d[(size + 1) * (size + 1)];
                for (var y = 0; y <= size; y++)
                for (var x = 0; x <= size; x++) positions[y * (size + 1) + x] = new V3d(x, y, 0);
                var faces = new List<int[]>();
                var holes = new List<int[]>();
                for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var a = y * (size + 1) + x;
                    var d = a + size + 2;
                    var triangle = new[] { a, a + 1, d };
                    if (x % 2 == 1 && y % 2 == 1 && x < size - 1 && y < size - 1)
                        holes.Add(triangle);
                    else faces.Add(triangle);
                    faces.Add(new[] { a, d, d - 1 });
                }
                var mesh = CreateMesh(positions, faces);
                var edges = mesh.EdgeCount;
                CheckTopology(mesh, faces, $"grid={size}, initial");
                for (var i = 0; i < holes.Count; i++)
                {
                    AppendAndCheck(mesh, faces, holes[i], $"grid={size}, hole={i}");
                    Assert.That(mesh.EdgeCount, Is.EqualTo(edges), $"grid={size}, hole={i}");
                }
                Assert.That(mesh.FaceCount, Is.EqualTo(2 * size * size));
            }
        }

        private static void AppendAndCheck(PolyMesh mesh, List<int[]> faces, int[] next, string context)
        {
            var before = Buffer(mesh);
            var count = mesh.VertexIndexCount;
            var prefix = before.Take(count).ToArray();
            // These metadata counters are not recomputed by incremental insertion.
            var metadata = (mesh.EulerCharacteristic, mesh.BorderEdgeCount, mesh.NonManifoldEdgeCount);
            Assert.That(mesh.AddFaceWithTopology(next), Is.EqualTo(faces.Count), context);
            faces.Add(next);
            Assert.That(Buffer(mesh).Take(count), Is.EqualTo(prefix), context);
            if (count + next.Length <= before.Length)
                Assert.That(Buffer(mesh), Is.SameAs(before), context);
            else
            {
                Assert.That(Buffer(mesh), Is.Not.SameAs(before), context);
                Assert.That(Buffer(mesh).Length, Is.EqualTo((int)(count * 1.5 + next.Length)), context);
            }
            Assert.That((mesh.EulerCharacteristic, mesh.BorderEdgeCount, mesh.NonManifoldEdgeCount),
                Is.EqualTo(metadata), context);
            CheckTopology(mesh, faces, context);
        }

        private static PolyMesh CreatePyramids(IEnumerable<int> valences, List<int[]> faces, List<int[]> holes)
        {
            var positions = new List<V3d>();
            foreach (var valence in valences)
            {
                var apex = positions.Count;
                positions.Add(new V3d(apex * 3, 0, 1));
                for (var i = 0; i < valence; i++)
                {
                    var angle = 2 * Math.PI * i / valence;
                    positions.Add(new V3d(apex * 3 + Math.Cos(angle), Math.Sin(angle), 0));
                    faces.Add(new[] { apex, apex + 1 + i, apex + 1 + (i + 1) % valence });
                }
                holes.Add(Enumerable.Range(apex + 1, valence).Reverse().ToArray());
            }
            return CreateMesh(positions.ToArray(), faces);
        }

        private static PolyMesh CreateMesh(V3d[] positions, List<int[]> faces)
        {
            var mesh = new PolyMesh { PositionArray = positions };
            foreach (var face in faces) mesh.AddFace(face);
            mesh.BuildTopology();
            return mesh;
        }

        private static void CheckTopology(PolyMesh mesh, List<int[]> faces, string context)
        {
            // Independent directed face-side map, built only from the fixture's polygons.
            var sides = new Dictionary<(int From, int To), (int Face, int Side)>();
            var undirected = new HashSet<(int, int)>();
            var corners = 0;
            for (var fi = 0; fi < faces.Count; fi++)
            {
                var face = faces[fi];
                Assert.That(mesh.FirstIndexArray[fi], Is.EqualTo(corners), context);
                for (var side = 0; side < face.Length; side++)
                {
                    var from = face[side];
                    var to = face[(side + 1) % face.Length];
                    sides.Add((from, to), (fi, side));
                    undirected.Add((Math.Min(from, to), Math.Max(from, to)));
                    Assert.That(mesh.VertexIndexArray[corners++], Is.EqualTo(from), context);
                }
            }
            Assert.That(mesh.HasTopology, Is.True, context);
            Assert.That(mesh.FaceCount, Is.EqualTo(faces.Count), context);
            Assert.That(mesh.VertexIndexCount, Is.EqualTo(corners), context);
            Assert.That(mesh.FirstIndexArray[mesh.FaceCount], Is.EqualTo(corners), context);
            Assert.That(mesh.EdgeCount, Is.EqualTo(undirected.Count), context);
            Assert.That(mesh.FaceVertexCountRange, Is.EqualTo(new Range1i(
                faces.Min(f => f.Length), faces.Max(f => f.Length))), context);
            Assert.That(mesh.VertexCount, Is.EqualTo(mesh.PositionArray.Length), context);
            Assert.That(mesh.Get<int>(PolyMesh.Property.FaceCount), Is.EqualTo(faces.Count), context);
            Assert.That(mesh.Get<int>(PolyMesh.Property.VertexIndexCount), Is.EqualTo(corners), context);
            var edgeIds = new Dictionary<(int, int), int>();
            foreach (var entry in sides)
            {
                var (from, to) = entry.Key;
                var (fi, side) = entry.Value;
                var where = $"{context}, face={fi}, side={side}, {from}->{to}";
                var edge = mesh.GetFace(fi).Edge(side);
                Assert.That(edge.IsValid, Is.True, where);
                Assert.That((edge.FromVertexIndex, edge.ToVertexIndex), Is.EqualTo((from, to)), where);
                Assert.That((edge.Face.Index, edge.Face.Side), Is.EqualTo((fi, side)), where);
                Assert.That(edge.Face.Edge(0).Ref, Is.EqualTo(edge.Ref), where);
                Assert.That(edge.Opposite.Opposite.Ref, Is.EqualTo(edge.Ref), where);
                Assert.That(edge.NextEdge.Ref, Is.EqualTo(mesh.GetFace(fi).Edge((side + 1) % faces[fi].Length).Ref), where);
                var key = (Math.Min(from, to), Math.Max(from, to));
                if (edgeIds.TryGetValue(key, out var id)) Assert.That(edge.Index, Is.EqualTo(id), where);
                else edgeIds.Add(key, edge.Index);
                if (sides.TryGetValue((to, from), out var opposite))
                {
                    Assert.That(edge.OppositeIsBorder, Is.False, where);
                    Assert.That((edge.OppositeFace.Index, edge.OppositeFace.Side), Is.EqualTo(opposite), where);
                    Assert.That(mesh.GetFace(opposite.Face).Edge(opposite.Side).Ref, Is.EqualTo(edge.Ref ^ 1), where);
                }
                else
                {
                    Assert.That(edge.OppositeIsBorder, Is.True, where);
                    Assert.That(edge.OppositeFace.IsValid, Is.False, where);
                }
            }
            Assert.That(edgeIds.Values.Distinct().Count(), Is.EqualTo(undirected.Count), context);
        }
    }
}
