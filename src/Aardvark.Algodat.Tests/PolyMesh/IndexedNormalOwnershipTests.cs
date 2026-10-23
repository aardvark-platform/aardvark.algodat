#nullable enable

using System;
using System.Linq;
using Aardvark.Base;
using NUnit.Framework;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class IndexedNormalOwnershipTests
    {
        private static readonly Symbol Extra = "IndexedNormals.Retained";
        private static readonly Symbol Normal = PolyMesh.Property.Normals;
        private static readonly int[] Limits = { 0, 1, int.MaxValue };

        private static PolyMesh Mesh(bool topology, int faceNormals, int cornerNormals)
        {
            var mesh = new PolyMesh
            {
                PositionArray = new[] { V3d.Zero, V3d.IOO, V3d.OIO, V3d.OOI },
                FirstIndexArray = new[] { 0, 3, 6 },
                VertexIndexArray = new[] { 0, 1, 2, 1, 0, 3 }
            };
            mesh.NormalArray = new[] { V3d.IOO, V3d.IOO, V3d.IOO, V3d.IOO };
            mesh.VertexAttributes[Extra] = new[] { 10, 11, 12, 13 };
            mesh.FaceAttributes[Extra] = new[] { 20, 21 };
            mesh.FaceVertexAttributes[Extra] = new[] { 30, 31, 32, 33, 34, 35 };
            mesh.FaceVertexAttributes[-Extra] = new[] { new V2d(3, 7), new V2d(11, 13) };
            mesh.EdgeAttributes[Extra] = new[] { 40 };
            mesh.InstanceAttributes[Extra] = new object();
            if (faceNormals == 1) mesh.FaceAttributes[Normal] = new[] { V3d.OOI, V3d.OIO };
            if (faceNormals == 2)
            {
                mesh.FaceAttributes[Normal] = new[] { 1, 0 };
                mesh.FaceAttributes[-Normal] = new[] { new V3d(7, 8, 9), new V3d(-2, -3, -4) };
            }
            if (cornerNormals == 1) mesh.FaceVertexAttributes[Normal] = Enumerable.Repeat(-V3d.IOO, 6).ToArray();
            if (cornerNormals == 2)
            {
                mesh.FaceVertexAttributes[Normal] = new[] { 0, 0, 1, 1, 2, 2 };
                mesh.FaceVertexAttributes[-Normal] = new[] { V3d.IOO, -V3d.OIO, -V3d.OOI };
            }
            if (topology) mesh.BuildTopology();
            return mesh;
        }

        private sealed class Attributes
        {
            private readonly SymbolDict<Array> dictionary;
            private readonly (Symbol Key, Array? Reference, Array? Values)[] entries;
            public Attributes(SymbolDict<Array> dictionary)
            {
                this.dictionary = dictionary;
                entries = dictionary.Select(p => (p.Key, (Array?)p.Value, (Array?)p.Value?.Clone())).ToArray();
            }
            public void Check(SymbolDict<Array> actual, string context)
            {
                Assert.That(actual, Is.SameAs(dictionary), context);
                Assert.That(actual.Keys, Is.EquivalentTo(entries.Select(p => p.Key)), context);
                foreach (var entry in entries)
                {
                    Assert.That(actual[entry.Key], Is.SameAs(entry.Reference), $"{context}, key={entry.Key}");
                    Assert.That(actual[entry.Key], Is.EqualTo(entry.Values), $"{context}, key={entry.Key}");
                }
            }
        }

        private sealed class State
        {
            private readonly PolyMesh mesh;
            private readonly Attributes vertex, face, corner, edge;
            private readonly V3d[] positions;
            private readonly int[] first, indices;
            private readonly (int, int, int, bool, int, int) counts;
            private readonly SymbolDict<object> instance;
            private readonly object instanceValue;
            public State(PolyMesh mesh)
            {
                this.mesh = mesh;
                vertex = new Attributes(mesh.VertexAttributes);
                face = new Attributes(mesh.FaceAttributes);
                corner = new Attributes(mesh.FaceVertexAttributes);
                edge = new Attributes(mesh.EdgeAttributes);
                positions = mesh.PositionArray.ToArray();
                first = mesh.FirstIndexArray.ToArray();
                indices = mesh.VertexIndexArray.ToArray();
                counts = (mesh.VertexCount, mesh.FaceCount, mesh.VertexIndexCount, mesh.HasTopology, mesh.EdgeCount, mesh.BorderEdgeCount);
                instance = mesh.InstanceAttributes;
                instanceValue = instance[Extra];
            }
            public void Check(string context)
            {
                vertex.Check(mesh.VertexAttributes, context + ": vertex");
                face.Check(mesh.FaceAttributes, context + ": face");
                corner.Check(mesh.FaceVertexAttributes, context + ": corner");
                edge.Check(mesh.EdgeAttributes, context + ": edge");
                Assert.That(mesh.PositionArray, Is.EqualTo(positions), context);
                Assert.That(mesh.FirstIndexArray, Is.EqualTo(first), context);
                Assert.That(mesh.VertexIndexArray, Is.EqualTo(indices), context);
                Assert.That((mesh.VertexCount, mesh.FaceCount, mesh.VertexIndexCount, mesh.HasTopology, mesh.EdgeCount, mesh.BorderEdgeCount), Is.EqualTo(counts), context);
                Assert.That(mesh.InstanceAttributes, Is.SameAs(instance), context);
                Assert.That(instance[Extra], Is.SameAs(instanceValue), context);
            }
        }

        private static void CheckSharing(PolyMesh source, PolyMesh result, bool addedFaceNormals, string context)
        {
            Assert.That(result, Is.Not.SameAs(source), context);
            Assert.That(result.FaceVertexAttributes, Is.Not.SameAs(source.FaceVertexAttributes), context);
            if (addedFaceNormals) Assert.That(result.FaceAttributes, Is.Not.SameAs(source.FaceAttributes), context);
            else Assert.That(result.FaceAttributes, Is.SameAs(source.FaceAttributes), context);
            Assert.That(result.VertexAttributes, Is.SameAs(source.VertexAttributes), context);
            Assert.That(result.EdgeAttributes, Is.SameAs(source.EdgeAttributes), context);
            Assert.That(result.InstanceAttributes, Is.SameAs(source.InstanceAttributes), context);
            Assert.That(result.PositionArray, Is.SameAs(source.PositionArray), context);
            Assert.That(result.VertexIndexArray, Is.SameAs(source.VertexIndexArray), context);
            Assert.That(result.FirstIndexArray, Is.SameAs(source.FirstIndexArray), context);
            Assert.That(result.NormalArray, Is.SameAs(source.NormalArray), context);
            foreach (var pair in source.FaceAttributes)
                if (pair.Key != Normal) Assert.That(result.FaceAttributes[pair.Key], Is.SameAs(pair.Value), $"{context}, face key={pair.Key}");
            foreach (var pair in source.FaceVertexAttributes)
                if (pair.Key != Normal && pair.Key != -Normal) Assert.That(result.FaceVertexAttributes[pair.Key], Is.SameAs(pair.Value), $"{context}, corner key={pair.Key}");
            Assert.That(result.HasTopology, Is.True, context);
            Assert.That(result.EdgeCount, Is.EqualTo(5), context);
            Assert.That(result.BorderEdgeCount, Is.EqualTo(4), context);
            Assert.That(result.VertexCount, Is.EqualTo(4), context);
            Assert.That(result.FaceCount, Is.EqualTo(2), context);
            Assert.That(result.VertexIndexCount, Is.EqualTo(6), context);
        }

        private static void CheckNormals(PolyMesh mesh, bool smooth, string context, V3d? shared = null)
        {
            var indices = (int[])mesh.FaceVertexAttributes[Normal];
            var normals = (V3d[])mesh.FaceVertexAttributes[-Normal];
            Assert.That(indices, Is.EqualTo(smooth ? new[] { 0, 1, 2, 1, 0, 3 } : new[] { 0, 1, 2, 3, 4, 5 }), context);
            Assert.That(normals.Length, Is.EqualTo(smooth ? 4 : 6), context);
            var diagonal = shared ?? new V3d(0, 1 / Math.Sqrt(2), 1 / Math.Sqrt(2));
            var expected = smooth ? new[] { diagonal, diagonal, V3d.OOI, V3d.OIO } : new[] { V3d.OOI, V3d.OOI, V3d.OOI, V3d.OIO, V3d.OIO, V3d.OIO };
            for (var i = 0; i < expected.Length; ++i)
            {
                for (var axis = 0; axis < 3; ++axis) Assert.That(double.IsFinite(normals[i][axis]), Is.True, $"{context}, normal={i}, axis={axis}");
                Assert.That((normals[i] - expected[i]).Length, Is.LessThan(3e-15), $"{context}, normal={i}");
                Assert.That(normals[i].LengthSquared, Is.EqualTo(1).Within(5e-15), $"{context}, normal={i}");
            }
        }

        [Test]
        public void IndexedNormalGenerationPreservesSourceAttributesAndUnrelatedSharing()
        {
            foreach (var topology in new[] { false, true })
            foreach (var faceNormals in new[] { 0, 1, 2 })
            foreach (var cornerNormals in new[] { 0, 1, 2 })
            foreach (var limit in Limits)
            {
                var context = $"topology={topology}, face={faceNormals}, corner={cornerNormals}, limit={limit}";
                var source = Mesh(topology, faceNormals, cornerNormals);
                var state = new State(source);
                var result = source.WithPerVertexIndexedNormals(Math.PI, limit);
                state.Check(context);
                CheckSharing(source, result, faceNormals == 0, context);
                CheckNormals(result, limit > 0, context);
                if (faceNormals == 0) Assert.That(result.FaceAttributes[Normal], Is.EqualTo(new[] { V3d.OOI, V3d.OIO }), context);
                else Assert.That(result.FaceAttributes[Normal], Is.SameAs(source.FaceAttributes[Normal]), context);
            }
        }

        [Test]
        public void LaterSmoothAndHardGenerationPreserveEarlierResultsAcrossCopyChains()
        {
            foreach (var topology in new[] { false, true })
            foreach (var faceNormals in new[] { 0, 1, 2 })
            foreach (var cornerNormals in new[] { 0, 1, 2 })
            foreach (var limit in Limits)
            {
                var context = $"topology={topology}, face={faceNormals}, corner={cornerNormals}, limit={limit}";
                var source = Mesh(topology, faceNormals, cornerNormals);
                var sourceState = new State(source);
                var hard = source.WithPerVertexIndexedNormals(0.1, limit);
                var hardState = new State(hard);
                var smooth = source.WithPerVertexIndexedNormals(Math.PI, limit);
                hardState.Check(context + ": earlier hard");
                sourceState.Check(context + ": source");
                var smoothState = new State(smooth);
                var chained = hard.WithPerVertexIndexedNormals(Math.PI, limit);
                var hardened = smooth.WithPerVertexIndexedNormals(0.1, limit);
                hardState.Check(context + ": chained hard");
                smoothState.Check(context + ": earlier smooth");
                sourceState.Check(context + ": source after chain");
                CheckNormals(hard, false, context);
                CheckNormals(smooth, limit > 0, context);
                CheckNormals(chained, limit > 0, context);
                CheckNormals(hardened, false, context);
                CheckSharing(hard, chained, false, context);
                CheckSharing(smooth, hardened, false, context);
                Assert.That(smooth.FaceVertexAttributes, Is.Not.SameAs(hard.FaceVertexAttributes), context);
            }
        }

        [Test]
        public void CreaseThresholdsNeighbourLimitsAndGenericShallowCopiesRetainTheirContracts()
        {
            foreach (var topology in new[] { false, true })
            foreach (var faceNormals in new[] { 0, 1, 2 })
            foreach (var angle in new[] { 0.1, Math.PI / 2, Math.BitIncrement(Math.PI / 2), Math.PI })
            foreach (var limit in new[] { -1, 0, 1, int.MaxValue })
            {
                var source = Mesh(topology, faceNormals, 2);
                var result = source.WithPerVertexIndexedNormals(angle, limit);
                CheckNormals(result, angle > Math.PI / 2 && limit > 0, $"topology={topology}, face={faceNormals}, angle={angle:R}, limit={limit}");
            }
            foreach (var topology in new[] { false, true })
            {
                var source = Mesh(topology, 1, 2);
                source.FaceAttributes[Normal] = new[] { 2 * V3d.OOI, 3 * V3d.OIO };
                var result = source.WithPerVertexIndexedNormals(Math.PI);
                CheckNormals(result, true, "reuse supplied face-normal arithmetic", new V3d(0, 3 / Math.Sqrt(13), 2 / Math.Sqrt(13)));
                var copy = source.Copy();
                Assert.That(copy.FaceAttributes, Is.SameAs(source.FaceAttributes));
                Assert.That(copy.FaceVertexAttributes, Is.SameAs(source.FaceVertexAttributes));
                Assert.That(copy.VertexAttributes, Is.SameAs(source.VertexAttributes));
                Assert.That(copy.PositionArray, Is.SameAs(source.PositionArray));
                Assert.That(copy.HasTopology, Is.EqualTo(source.HasTopology));
            }
        }
    }
}
