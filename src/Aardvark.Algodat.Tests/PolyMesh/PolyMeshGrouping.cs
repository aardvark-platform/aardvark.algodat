using Aardvark.Base;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class PolyMeshGrouping
    {
        private static readonly Symbol FaceValue = "GroupingFaceValue";
        private static readonly Symbol VertexValue = "GroupingVertexValue";
        private static readonly Symbol FaceVertexValue = "GroupingFaceVertexValue";

        private enum AttributeRepresentation
        {
            Direct,
            Indexed,
        }

        private sealed class MeshSnapshot
        {
            private readonly int[] m_firstIndexArray;
            private readonly int[] m_firstIndexValues;
            private readonly int[] m_vertexIndexArray;
            private readonly int[] m_vertexIndexValues;
            private readonly Dictionary<Symbol, (Array Reference, Array Values)> m_faceAttributes;
            private readonly Dictionary<Symbol, (Array Reference, Array Values)> m_vertexAttributes;
            private readonly Dictionary<Symbol, (Array Reference, Array Values)> m_faceVertexAttributes;

            public MeshSnapshot(PolyMesh mesh)
            {
                m_firstIndexArray = mesh.FirstIndexArray;
                m_firstIndexValues = (int[])m_firstIndexArray.Clone();
                m_vertexIndexArray = mesh.VertexIndexArray;
                m_vertexIndexValues = (int[])m_vertexIndexArray.Clone();
                m_faceAttributes = Capture(mesh.FaceAttributes);
                m_vertexAttributes = Capture(mesh.VertexAttributes);
                m_faceVertexAttributes = Capture(mesh.FaceVertexAttributes);
            }

            private static Dictionary<Symbol, (Array Reference, Array Values)> Capture(SymbolDict<Array> attributes)
                => attributes.ToDictionary(kvp => kvp.Key, kvp => (kvp.Value, (Array)kvp.Value.Clone()));

            private static void AssertUnchanged(
                SymbolDict<Array> attributes,
                Dictionary<Symbol, (Array Reference, Array Values)> snapshot)
            {
                ClassicAssert.AreEqual(snapshot.Count, attributes.Count);
                foreach (var kvp in snapshot)
                {
                    ClassicAssert.IsTrue(attributes.TryGetValue(kvp.Key, out var current));
                    ClassicAssert.AreSame(kvp.Value.Reference, current);
                    CollectionAssert.AreEqual(kvp.Value.Values, current);
                }
            }

            public void AssertUnchanged(PolyMesh mesh)
            {
                ClassicAssert.AreSame(m_firstIndexArray, mesh.FirstIndexArray);
                ClassicAssert.AreSame(m_vertexIndexArray, mesh.VertexIndexArray);
                CollectionAssert.AreEqual(m_firstIndexValues, mesh.FirstIndexArray);
                CollectionAssert.AreEqual(m_vertexIndexValues, mesh.VertexIndexArray);
                AssertUnchanged(mesh.FaceAttributes, m_faceAttributes);
                AssertUnchanged(mesh.VertexAttributes, m_vertexAttributes);
                AssertUnchanged(mesh.FaceVertexAttributes, m_faceVertexAttributes);
            }
        }

        [Test]
        public void GroupPreservesDirectConeFaceVertexNormals()
        {
            var mesh = PolyMeshPrimitives.Cone(12, 1, 0.5, C4b.Red);
            ValidateNormals(mesh);

            var grouping = mesh.IntoArray().Group();
            ClassicAssert.IsTrue(grouping.Valid);

            ValidateNormals(grouping.Mesh);
        }

        [Test]
        public void GroupProjectsDirectAttributesFromReorderedFaceSubsets()
            => AssertGroupedAttributes(AttributeRepresentation.Direct, AttributeRepresentation.Direct);

        [Test]
        public void GroupProjectsIndexedAttributesFromReorderedFaceSubsets()
            => AssertGroupedAttributes(AttributeRepresentation.Indexed, AttributeRepresentation.Indexed);

        [Test]
        public void GroupProjectsMixedAttributesFromOriginalSelectedElements()
            => AssertGroupedAttributes(AttributeRepresentation.Direct, AttributeRepresentation.Indexed);

        private static void AssertGroupedAttributes(
            AttributeRepresentation meshBRepresentation,
            AttributeRepresentation meshARepresentation)
        {
            var meshA = CreateMesh(1, meshARepresentation);
            var meshB = CreateMesh(2, meshBRepresentation);
            var snapshotA = new MeshSnapshot(meshA);
            var snapshotB = new MeshSnapshot(meshB);

            var selectedFaces = new[]
            {
                meshB.GetFace(2), meshA.GetFace(3), meshB.GetFace(0), meshA.GetFace(1)
            };
            var expectedFaces = new[]
            {
                meshB.GetFace(2), meshB.GetFace(0), meshA.GetFace(3), meshA.GetFace(1)
            };
            var expectedVertices = new[]
            {
                meshB.GetVertex(4), meshB.GetVertex(5), meshB.GetVertex(0), meshB.GetVertex(2), meshB.GetVertex(1),
                meshA.GetVertex(6), meshA.GetVertex(7), meshA.GetVertex(1), meshA.GetVertex(0),
                meshA.GetVertex(2), meshA.GetVertex(3), meshA.GetVertex(4), meshA.GetVertex(5)
            };

            var grouping = selectedFaces.Group();

            ClassicAssert.IsTrue(grouping.Valid);
            ClassicAssert.AreEqual(2, grouping.MeshCount);
            CollectionAssert.AreEqual(new[] { meshB, meshA }, grouping.Meshes.ToArray());
            CollectionAssert.AreEqual(new[] { meshB, meshA }, grouping.MatchingMeshes.ToArray());

            var grouped = grouping.Mesh;
            var outputIsIndexed = meshBRepresentation == AttributeRepresentation.Indexed
                               || meshARepresentation == AttributeRepresentation.Indexed;

            ClassicAssert.AreEqual(4, grouped.FaceCount);
            ClassicAssert.AreEqual(13, grouped.VertexCount);
            CollectionAssert.AreEqual(new[] { 0, 4, 7, 11, 15 }, grouped.FirstIndexArray);
            CollectionAssert.AreEqual(
                new[] { 0, 1, 2, 3, 2, 4, 3, 5, 6, 7, 8, 9, 10, 11, 12 },
                grouped.VertexIndexArray);
            CollectionAssert.AreEqual(expectedVertices.Select(v => v.Position), grouped.PositionArray);

            var expectedFaceValues = expectedFaces.Select(GetFaceValue).ToArray();
            var expectedVertexValues = expectedVertices.Select(GetVertexValue).ToArray();
            var expectedFaceVertexValues = expectedFaces
                .SelectMany(face => Enumerable.Range(0, face.VertexCount)
                    .Select(side => GetFaceVertexValue(face, side)))
                .ToArray();

            for (var fi = 0; fi < grouped.FaceCount; fi++)
            {
                var actualFace = grouped.GetFace(fi);
                var expectedFace = expectedFaces[fi];
                CollectionAssert.AreEqual(
                    expectedFace.Vertices.Select(v => v.Position),
                    actualFace.Vertices.Select(v => v.Position));
                ClassicAssert.AreEqual(
                    expectedFaceValues[fi],
                    outputIsIndexed
                        ? actualFace.GetIndexedAttribute<string>(FaceValue)
                        : actualFace.GetAttribute<string>(FaceValue));

                for (var side = 0; side < actualFace.VertexCount; side++)
                {
                    var expected = GetFaceVertexValue(expectedFace, side);
                    var actual = outputIsIndexed
                        ? actualFace.GetVertexIndexedAttribute<V2d>(FaceVertexValue, side)
                        : actualFace.GetVertexAttribute<V2d>(FaceVertexValue, side);
                    ClassicAssert.AreEqual(expected, actual);
                }
            }

            for (var vi = 0; vi < grouped.VertexCount; vi++)
            {
                var actual = outputIsIndexed
                    ? grouped.GetVertex(vi).GetIndexedAttribute<double>(VertexValue)
                    : grouped.GetVertex(vi).GetAttribute<double>(VertexValue);
                ClassicAssert.AreEqual(expectedVertexValues[vi], actual);
            }

            if (outputIsIndexed)
            {
                AssertCompactIndexedAttribute(grouped.FaceAttributes, FaceValue, expectedFaceValues);
                AssertCompactIndexedAttribute(grouped.VertexAttributes, VertexValue, expectedVertexValues);
                AssertCompactIndexedAttribute(grouped.FaceVertexAttributes, FaceVertexValue, expectedFaceVertexValues);
            }
            else
            {
                AssertDirectAttribute(grouped.FaceAttributes, FaceValue, expectedFaceValues);
                AssertDirectAttribute(grouped.VertexAttributes, VertexValue, expectedVertexValues);
                AssertDirectAttribute(grouped.FaceVertexAttributes, FaceVertexValue, expectedFaceVertexValues);
            }

            snapshotA.AssertUnchanged(meshA);
            snapshotB.AssertUnchanged(meshB);
        }

        private static PolyMesh CreateMesh(int meshId, AttributeRepresentation representation)
        {
            int[] firstIndices;
            int[] vertexIndices;
            if (meshId == 1)
            {
                firstIndices = new[] { 0, 3, 7, 10, 14 };
                vertexIndices = new[]
                {
                    0, 1, 2,
                    2, 3, 4, 5,
                    5, 6, 0,
                    6, 7, 1, 0
                };
            }
            else
            {
                firstIndices = new[] { 0, 3, 6, 10 };
                vertexIndices = new[]
                {
                    0, 1, 2,
                    2, 3, 4,
                    4, 5, 0, 2
                };
            }

            var vertexCount = vertexIndices.Max() + 1;
            var positions = new V3d[vertexCount]
                .SetByIndex(i => new V3d(meshId * 100 + i, meshId, -i));
            var mesh = new PolyMesh
            {
                FirstIndexArray = firstIndices,
                VertexIndexArray = vertexIndices,
                PositionArray = positions,
            };

            var directFaces = new string[mesh.FaceCount]
                .SetByIndex(i => $"mesh-{meshId}-face-{i}");
            var directVertices = new double[mesh.VertexCount]
                .SetByIndex(i => meshId * 1000.0 + i + 0.25);
            var directFaceVertices = new V2d[mesh.VertexIndexArray.Length]
                .SetByIndex(i => new V2d(meshId * 100 + i, -i - 0.5));

            if (representation == AttributeRepresentation.Direct)
            {
                mesh.FaceAttributes[FaceValue] = directFaces;
                mesh.VertexAttributes[VertexValue] = directVertices;
                mesh.FaceVertexAttributes[FaceVertexValue] = directFaceVertices;
            }
            else
            {
                var faceValues = new string[3]
                    .SetByIndex(i => $"mesh-{meshId}-indexed-face-{i}");
                mesh.FaceAttributes[FaceValue] = new int[mesh.FaceCount]
                    .SetByIndex(i => (i * 2 + meshId) % faceValues.Length);
                mesh.FaceAttributes[-FaceValue] = faceValues;

                var vertexValues = new double[4]
                    .SetByIndex(i => meshId * 1000.0 + i * 10.0 + 0.5);
                mesh.VertexAttributes[VertexValue] = new int[mesh.VertexCount]
                    .SetByIndex(i => (i * 3 + meshId) % vertexValues.Length);
                mesh.VertexAttributes[-VertexValue] = vertexValues;

                var faceVertexValues = new V2d[5]
                    .SetByIndex(i => new V2d(meshId * 100 + i * 10, -meshId - i));
                mesh.FaceVertexAttributes[FaceVertexValue] = new int[mesh.VertexIndexArray.Length]
                    .SetByIndex(i => (i * 2 + meshId) % faceVertexValues.Length);
                mesh.FaceVertexAttributes[-FaceVertexValue] = faceVertexValues;
            }

            return mesh;
        }

        private static string GetFaceValue(PolyMesh.Face face)
            => face.Mesh.FaceAttributes.Contains(-FaceValue)
                ? face.GetIndexedAttribute<string>(FaceValue)
                : face.GetAttribute<string>(FaceValue);

        private static double GetVertexValue(PolyMesh.Vertex vertex)
            => vertex.Mesh.VertexAttributes.Contains(-VertexValue)
                ? vertex.GetIndexedAttribute<double>(VertexValue)
                : vertex.GetAttribute<double>(VertexValue);

        private static V2d GetFaceVertexValue(PolyMesh.Face face, int side)
            => face.Mesh.FaceVertexAttributes.Contains(-FaceVertexValue)
                ? face.GetVertexIndexedAttribute<V2d>(FaceVertexValue, side)
                : face.GetVertexAttribute<V2d>(FaceVertexValue, side);

        private static void AssertDirectAttribute<T>(
            SymbolDict<Array> attributes,
            Symbol name,
            T[] expected)
        {
            ClassicAssert.AreEqual(typeof(T[]), attributes[name].GetType());
            ClassicAssert.IsFalse(attributes.Contains(-name));
            CollectionAssert.AreEqual(expected, (T[])attributes[name]);
        }

        private static void AssertCompactIndexedAttribute<T>(
            SymbolDict<Array> attributes,
            Symbol name,
            T[] expected)
        {
            ClassicAssert.AreEqual(typeof(int[]), attributes[name].GetType());
            ClassicAssert.AreEqual(typeof(T[]), attributes[-name].GetType());

            var actualIndices = (int[])attributes[name];
            var actualValues = (T[])attributes[-name];
            var expectedIndices = new int[expected.Length];
            var expectedValues = new List<T>();
            for (var i = 0; i < expected.Length; i++)
            {
                var index = expectedValues.IndexOf(expected[i]);
                if (index < 0)
                {
                    index = expectedValues.Count;
                    expectedValues.Add(expected[i]);
                }
                expectedIndices[i] = index;
            }

            CollectionAssert.AreEqual(expectedIndices, actualIndices);
            CollectionAssert.AreEqual(expectedValues, actualValues);
        }

        private static void ValidateNormals(PolyMesh mesh)
        {
            foreach (var face in mesh.Faces)
            {
                var faceNormal = face.Polygon3d.ComputeNormal();
                for (var side = 0; side < face.VertexCount; side++)
                {
                    var faceVertexNormal = face.GetVertexAttribute<V3d>(PolyMesh.Property.Normals, side);
                    ClassicAssert.Greater(faceVertexNormal.Dot(faceNormal), 0.0);
                }
            }
        }
    }
}
