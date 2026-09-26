using Aardvark.Base;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class PolyMeshTopologyTests
    {
        private static int[] Rotated(int[] values, int offset)
            => Enumerable.Range(0, values.Length).Select(i => values[(i + offset) % values.Length]).ToArray();

        private static PolyMesh CreateMesh(int vertexCount, params int[][] faces)
        {
            var mesh = new PolyMesh();
            for (var i = 0; i < vertexCount; i++)
                mesh.AddVertex(new V3d(i % 4, i / 4, (i * 7) % 3));
            foreach (var face in faces) mesh.AddFace(face);
            return mesh;
        }

        private static IEnumerable<TestCaseData> EveryFaceSideCases()
        {
            foreach (var valence in new[] { 4, 5 })
            for (var side = 0; side < valence; side++)
            {
                yield return new TestCaseData(valence, side)
                    .SetName($"Analyze_SharedEdge_{valence}Sides_Side{side}");
            }
        }

        [TestCaseSource(nameof(EveryFaceSideCases))]
        public void AnalyzeFindsSharedEdgeAtEverySideOfEvenAndOddFaces(int valence, int sharedSide)
        {
            var primary = Enumerable.Range(0, valence).ToArray();
            var from = primary[sharedSide];
            var to = primary[(sharedSide + 1) % valence];
            var mesh = CreateMesh(valence + 1, primary, new[] { to, from, valence });
            mesh.BuildTopology();

            var componentCount = mesh.Analyze(out var components);
            var orientedComponentCount = mesh.Analyze(out var orientedComponents, out var reversed);
            var sharedEdge = mesh.GetFace(0).Edge(sharedSide);

            Assert.Multiple(() =>
            {
                Assert.That(componentCount, Is.EqualTo(1));
                Assert.That(components, Is.EqualTo(new uint[] { 0, 0 }));
                Assert.That(orientedComponentCount, Is.EqualTo(1));
                Assert.That(orientedComponents, Is.EqualTo(new uint[] { 0, 0 }));
                Assert.That(reversed, Is.EqualTo(new[] { false, false }));
                Assert.That(sharedEdge.Faces.Select(f => f.Index), Is.EquivalentTo(new[] { 0, 1 }));
                Assert.That(sharedEdge.Faces.Select(f => f.Index).Distinct().Count(), Is.EqualTo(2));
            });
        }

        private static IEnumerable<TestCaseData> RotatedMixedFaceCases()
        {
            for (var firstRotation = 0; firstRotation < 4; firstRotation++)
            for (var secondRotation = 0; secondRotation < 5; secondRotation++)
            {
                yield return new TestCaseData(firstRotation, secondRotation)
                    .SetName($"Analyze_MixedFaces_Rotated_{firstRotation}_{secondRotation}");
            }
        }

        [TestCaseSource(nameof(RotatedMixedFaceCases))]
        public void AnalyzeHandlesMixedFaceSizesDisconnectedComponentsAndRotatedStarts(
            int firstRotation, int secondRotation)
        {
            var first = Rotated(new[] { 0, 1, 2, 3 }, firstRotation);
            var second = Rotated(new[] { 2, 1, 4, 5, 6 }, secondRotation);
            var isolated = new[] { 7, 8, 9 };
            var mesh = CreateMesh(10, first, second, isolated);
            mesh.BuildTopology();

            var componentCount = mesh.Analyze(out var components);
            var orientedComponentCount = mesh.Analyze(out var orientedComponents, out var reversed);

            Assert.Multiple(() =>
            {
                Assert.That(componentCount, Is.EqualTo(2));
                Assert.That(components, Is.EqualTo(new uint[] { 0, 0, 1 }));
                Assert.That(orientedComponentCount, Is.EqualTo(2));
                Assert.That(orientedComponents, Is.EqualTo(new uint[] { 0, 0, 1 }));
                Assert.That(reversed, Is.EqualTo(new[] { false, false, false }));
            });
        }

        [Test]
        public void AnalyzeSeedsEachDisconnectedComponentWithItsOwnFaceSize()
        {
            var isolatedTriangle = new[] { 0, 1, 2 };
            var quad = new[] { 3, 4, 5, 6 };
            var connectedPentagon = new[] { 3, 6, 7, 8, 9 };
            var mesh = CreateMesh(10, isolatedTriangle, quad, connectedPentagon);
            mesh.BuildTopology();

            var componentCount = mesh.Analyze(out var components);
            var orientedComponentCount = mesh.Analyze(out var orientedComponents, out var reversed);

            Assert.Multiple(() =>
            {
                Assert.That(componentCount, Is.EqualTo(2));
                Assert.That(components, Is.EqualTo(new uint[] { 0, 1, 1 }));
                Assert.That(orientedComponentCount, Is.EqualTo(2));
                Assert.That(orientedComponents, Is.EqualTo(new uint[] { 0, 1, 1 }));
                Assert.That(reversed, Is.EqualTo(new[] { false, false, false }));
            });
        }

        [Test]
        public void AnalyzeRequiresBuiltTopologyInBothOverloads()
        {
            var mesh = CreateMesh(3, new[] { 0, 1, 2 });

            var componentError = Assert.Throws<InvalidOperationException>(
                () => mesh.Analyze(out uint[] _));
            var orientationError = Assert.Throws<InvalidOperationException>(
                () => mesh.Analyze(out uint[] _, out bool[] _));

            Assert.Multiple(() =>
            {
                Assert.That(componentError!.Message, Does.Contain("requires topology"));
                Assert.That(orientationError!.Message, Does.Contain("requires topology"));
            });
        }

        [Test]
        public void AnalyzeReportsWindingReversalAndManifoldCopyAppliesIt()
        {
            var first = new[] { 0, 1, 2 };
            var sameDirection = new[] { 1, 2, 3 };
            var mesh = CreateMesh(4, first, sameDirection);
            mesh.BuildTopology();
            Assert.That(mesh.IsNonManifold, Is.True);

            var unorientedCount = mesh.Analyze(out var unorientedComponents);
            var orientedCount = mesh.Analyze(out var orientedComponents, out var reversed);

            Assert.Multiple(() =>
            {
                Assert.That(unorientedCount, Is.EqualTo(2));
                Assert.That(unorientedComponents, Is.EqualTo(new uint[] { 0, 1 }));
                Assert.That(orientedCount, Is.EqualTo(1));
                Assert.That(orientedComponents, Is.EqualTo(new uint[] { 0, 0 }));
                Assert.That(reversed, Is.EqualTo(new[] { false, true }));
            });

            var sourceFaces = mesh.Faces.Select(f => f.VertexIndices.ToArray()).ToArray();
            var manifold = mesh.ManifoldCopy();
            var copiedFaces = manifold.Faces.Select(f => f.VertexIndices.ToArray()).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(mesh.Faces.Select(f => f.VertexIndices.ToArray()), Is.EqualTo(sourceFaces));
                Assert.That(manifold.HasTopology, Is.False);
                Assert.That(copiedFaces[0], Is.EqualTo(first));
                Assert.That(copiedFaces[1], Is.EqualTo(sameDirection.Reverse().ToArray()));
            });

            manifold.BuildTopology();
            Assert.Multiple(() =>
            {
                Assert.That(manifold.IsManifold, Is.True);
                Assert.That(manifold.Analyze(out var components), Is.EqualTo(1));
                Assert.That(components, Is.EqualTo(new uint[] { 0, 0 }));
            });
        }

        [Test]
        public void ConsistentWindingDoesNotRequestReversal()
        {
            var mesh = CreateMesh(4, new[] { 0, 1, 2 }, new[] { 2, 1, 3 });
            mesh.BuildTopology();

            var componentCount = mesh.Analyze(out var components, out var reversed);

            Assert.Multiple(() =>
            {
                Assert.That(componentCount, Is.EqualTo(1));
                Assert.That(components, Is.EqualTo(new uint[] { 0, 0 }));
                Assert.That(reversed, Is.EqualTo(new[] { false, false }));
            });
        }

        [Test]
        public void ConnectedEdgesAtReturnsOtherEndpointEdgesExactlyOnce()
        {
            const int valence = 8;
            var mesh = new PolyMesh();
            var center = mesh.AddVertex(V3d.Zero);
            for (var i = 0; i < valence; i++)
            {
                var angle = Constant.PiTimesTwo * i / valence;
                mesh.AddVertex(new V3d(Math.Cos(angle), Math.Sin(angle), 0));
            }
            var unrelated = mesh.AddVertex(new V3d(10, 10, 0));
            for (var i = 0; i < valence; i++)
                mesh.AddFace(center, 1 + i, 1 + (i + 1) % valence);
            mesh.BuildTopology();

            var edge = mesh.GetEdge(center, 1);
            Assert.That(edge.IsValid, Is.True);

            AssertEndpointNeighborhood(edge, center, valence - 1);
            AssertEndpointNeighborhood(edge, 1, 2);

            Assert.Multiple(() =>
            {
                Assert.That(edge.GetConnectedEdgesAt(unrelated), Is.Empty);
                Assert.That(edge.GetConnectedEdgesAt(mesh.GetVertex(unrelated)), Is.Empty);

                var otherMesh = CreateMesh(3, new[] { 0, 1, 2 });
                otherMesh.BuildTopology();
                Assert.That(edge.GetConnectedEdgesAt(otherMesh.GetVertex(center)), Is.Empty);
            });
        }

        private static void AssertEndpointNeighborhood(PolyMesh.Edge edge, int vertexIndex, int expectedCount)
        {
            var expected = edge.Mesh.GetVertex(vertexIndex).Edges
                .Where(e => e.Index != edge.Index)
                .Select(e => e.Index)
                .ToArray();
            var byIndex = edge.GetConnectedEdgesAt(vertexIndex).Select(e => e.Index).ToArray();
            var byVertex = edge.GetConnectedEdgesAt(edge.Mesh.GetVertex(vertexIndex)).Select(e => e.Index).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(byIndex, Is.EqualTo(expected));
                Assert.That(byVertex, Is.EqualTo(expected));
                Assert.That(byIndex, Has.Length.EqualTo(expectedCount));
                Assert.That(byIndex.Distinct().Count(), Is.EqualTo(byIndex.Length));
                Assert.That(byIndex, Does.Not.Contain(edge.Index));
                Assert.That(byIndex.All(i => edge.Mesh.GetEdge(i).IsConnectedToVertex(vertexIndex)), Is.True);
            });
        }
    }
}
