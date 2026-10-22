#nullable enable

using System;
using System.Linq;
using Aardvark.Base;
using NUnit.Framework;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class NormalizeUVsTests
    {
        private static readonly Symbol Custom = "NormalizeUVs.Custom";
        private static Symbol Property(int route) => route == 2 ? Custom : PolyMesh.Property.DiffuseColorCoordinates;
        private static void Normalize(PolyMesh mesh, int route)
        {
            if (route == 0) mesh.NormalizeUVs();
            else mesh.NormalizeUVs(Property(route));
        }

        private static PolyMesh Mesh(int[] active, int extra)
        {
            var mesh = new PolyMesh();
            foreach (var p in new[] { new V3d(-9, -9, 0), V3d.Zero, V3d.IOO, V3d.OIO }) mesh.AddVertex(p);
            mesh.AddFace(active);
            if (extra >= 0)
            {
                var indices = new int[active.Length + extra];
                active.CopyTo(indices, 0);
                mesh.VertexIndexArray = indices;
                mesh.VertexIndexCount = active.Length;
            }
            return mesh;
        }

        private static V2d[] Coordinates(bool extended) => extended
            ? new[] { new V2d(100, 200), new V2d(10, 20), new V2d(14, 20), new V2d(10, 28), new V2d(-30, 44), new V2d(18, 12) }
            : new[] { new V2d(100, 200), new V2d(10, 20), new V2d(14, 20), new V2d(10, 28) };

        private static V2d[] Expected(bool extended) => extended
            ? new[] { new V2d(22.5, 22.5), V2d.Zero, V2d.IO, V2d.OI, new V2d(-10, 3), new V2d(2, -1) }
            : new[] { new V2d(22.5, 22.5), V2d.Zero, V2d.IO, V2d.OI };

        private sealed class Connectivity
        {
            private readonly PolyMesh mesh;
            private readonly int[]? indices, first, savedIndices, savedFirst;
            private readonly V3d[]? positions, savedPositions;
            private readonly (int, int, int, Range1i, bool, int, int) counts;
            public Connectivity(PolyMesh mesh)
            {
                this.mesh = mesh;
                indices = mesh.VertexIndexArray;
                first = mesh.FirstIndexArray;
                positions = mesh.PositionArray;
                savedIndices = indices?.ToArray();
                savedFirst = first?.ToArray();
                savedPositions = positions?.ToArray();
                counts = (mesh.VertexCount, mesh.FaceCount, mesh.VertexIndexCount, mesh.FaceVertexCountRange, mesh.HasTopology, mesh.EdgeCount, mesh.BorderEdgeCount);
            }
            public void Check(string context)
            {
                Assert.That(mesh.VertexIndexArray, Is.SameAs(indices), context);
                Assert.That(mesh.FirstIndexArray, Is.SameAs(first), context);
                Assert.That(mesh.PositionArray, Is.SameAs(positions), context);
                Assert.That(mesh.VertexIndexArray, Is.EqualTo(savedIndices), context);
                Assert.That(mesh.FirstIndexArray, Is.EqualTo(savedFirst), context);
                Assert.That(mesh.PositionArray, Is.EqualTo(savedPositions), context);
                Assert.That((mesh.VertexCount, mesh.FaceCount, mesh.VertexIndexCount, mesh.FaceVertexCountRange, mesh.HasTopology, mesh.EdgeCount, mesh.BorderEdgeCount), Is.EqualTo(counts), context);
                Assert.That(mesh[PolyMesh.Property.VertexIndexCount], Is.EqualTo(mesh.VertexIndexCount), context);
            }
        }

        private static void AssertCoordinates(V2d[] actual, V2d[] expected, string context, bool finite = true)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length), context);
            for (var i = 0; i < expected.Length; ++i)
            for (var axis = 0; axis < 2; ++axis)
            {
                if (finite) Assert.That(double.IsFinite(actual[i][axis]), Is.True, $"{context}, coordinate={i}, axis={axis}");
                Assert.That(BitConverter.DoubleToInt64Bits(actual[i][axis]),
                    Is.EqualTo(BitConverter.DoubleToInt64Bits(expected[i][axis])), $"{context}, coordinate={i}, axis={axis}");
            }
        }

        private static void CheckNormalization(PolyMesh mesh, int route, V2d[] coords, V2d[] expected, string context, bool finite = true)
        {
            var otherProperty = route == 2 ? PolyMesh.Property.DiffuseColorCoordinates : Custom;
            var other = new[] { new V2d(73, 91) };
            mesh.VertexAttributes[Property(route)] = coords;
            mesh.VertexAttributes[otherProperty] = other;
            var attributes = mesh.VertexAttributes;
            var connectivity = new Connectivity(mesh);
            Assert.DoesNotThrow(() => Normalize(mesh, route), context);
            Assert.That(mesh.VertexAttributes, Is.SameAs(attributes), context);
            Assert.That(mesh.VertexAttributes[Property(route)], Is.SameAs(coords), context);
            AssertCoordinates(coords, expected, context, finite);
            Assert.That(mesh.VertexAttributes[otherProperty], Is.SameAs(other), context);
            Assert.That(other[0], Is.EqualTo(new V2d(73, 91)), context);
            connectivity.Check(context);
        }

        [Test]
        public void ActiveCornerBoundsIgnoreSpareIndicesAndRemapTheWholeCoordinateArray()
        {
            Assert.Multiple(() =>
            {
                foreach (var repeated in new[] { false, true })
                foreach (var extra in new[] { -1, 1, 13, 32 })
                foreach (var extended in new[] { false, true })
                foreach (var route in new[] { 0, 1, 2 })
                foreach (var suffix in new[] { 0, -1, int.MaxValue, extended ? 6 : 4 })
                {
                    var active = repeated ? new[] { 1, 2, 2, 3, 1 } : new[] { 1, 2, 3 };
                    var mesh = Mesh(active, extra);
                    for (var i = mesh.VertexIndexCount; i < mesh.VertexIndexCapacity; ++i) mesh.VertexIndexArray[i] = suffix;
                    var context = $"repeated={repeated}, extra={extra}, extended={extended}, route={route}, suffix={suffix}";
                    CheckNormalization(mesh, route, Coordinates(extended), Expected(extended), context);
                }
                foreach (var capacity in new[] { 16, 31 })
                foreach (var route in new[] { 0, 1, 2 })
                foreach (var suffix in new[] { 0, -1, int.MaxValue })
                {
                    var mesh = Mesh(new[] { 1, 2, 3 }, capacity - 3);
                    mesh.VertexIndexCount = 0;
                    mesh.FirstIndexArray = new[] { 0 };
                    for (var i = 0; i < capacity; ++i) mesh.VertexIndexArray[i] = suffix;
                    var box = Box2d.Invalid;
                    var scale = 1.0 / (box.Max - box.Min);
                    var coords = Coordinates(false);
                    var expected = coords.Select(p => (p - box.Min) * scale).ToArray();
                    CheckNormalization(mesh, route, coords, expected, $"empty active prefix, capacity={capacity}, route={route}, suffix={suffix}", finite: false);
                }
            });
        }

        [Test]
        public void ExactCapacityAndRepeatedReferencesRetainNormalizationAndConnectivity()
        {
            Assert.Multiple(() =>
            {
                foreach (var repeated in new[] { false, true })
                foreach (var extra in new[] { 0, 17 })
                foreach (var extended in new[] { false, true })
                foreach (var route in new[] { 0, 1, 2 })
                {
                    var mesh = Mesh(repeated ? new[] { 1, 2, 2, 3, 1 } : new[] { 1, 2, 3 }, extra);
                    for (var i = mesh.VertexIndexCount; i < mesh.VertexIndexCapacity; ++i) mesh.VertexIndexArray[i] = 1;
                    if (!repeated) mesh.BuildTopology();
                    CheckNormalization(mesh, route, Coordinates(extended), Expected(extended), $"repeated={repeated}, extra={extra}, extended={extended}, route={route}");
                }
            });
        }

        [Test]
        public void InvalidActiveIndicesAndUnsupportedAttributesStillFailBeforeMutation()
        {
            Assert.Multiple(() =>
            {
                foreach (var extra in new[] { 0, 13 })
                foreach (var route in new[] { 0, 1, 2 })
                foreach (var offset in new[] { 0, 1, 2 })
                foreach (var invalid in new[] { -1, 4, int.MaxValue })
                {
                    var mesh = Mesh(new[] { 1, 2, 3 }, extra);
                    mesh.VertexIndexArray[offset] = invalid;
                    var coords = Coordinates(false);
                    mesh.VertexAttributes[Property(route)] = coords;
                    var state = new Connectivity(mesh);
                    var context = $"extra={extra}, route={route}, offset={offset}, invalid={invalid}";
                    Assert.Throws<IndexOutOfRangeException>(() => Normalize(mesh, route), context);
                    Assert.That(mesh.VertexAttributes[Property(route)], Is.SameAs(coords), context);
                    AssertCoordinates(coords, Coordinates(false), context);
                    state.Check(context);
                }
                foreach (var route in new[] { 0, 1, 2 })
                foreach (var kind in new[] { "missing", "null", "float", "indexed", "null-indices" })
                {
                    var mesh = Mesh(new[] { 1, 2, 3 }, 0);
                    var property = Property(route);
                    if (kind == "null") mesh.VertexAttributes[property] = null!;
                    if (kind == "float") mesh.VertexAttributes[property] = new V2f[4];
                    if (kind == "indexed")
                    {
                        mesh.VertexAttributes[property] = new[] { 0, 1, 2, 3 };
                        mesh.VertexAttributes[-property] = Coordinates(false);
                    }
                    if (kind == "null-indices")
                    {
                        mesh.VertexAttributes[property] = Coordinates(false);
                        mesh.VertexIndexArray = null!;
                    }
                    var state = new Connectivity(mesh);
                    var context = $"route={route}, kind={kind}";
                    if (kind == "null-indices")
                        Assert.That(Assert.Throws<ArgumentNullException>(() => Normalize(mesh, route))!.ParamName, Is.EqualTo("source"), context);
                    else if (kind == "float" || kind == "indexed") Assert.Throws<InvalidCastException>(() => Normalize(mesh, route), context);
                    else Assert.Throws<NullReferenceException>(() => Normalize(mesh, route), context);
                    if (kind == "indexed") AssertCoordinates((V2d[])mesh.VertexAttributes[-property], Coordinates(false), context);
                    state.Check(context);
                }
                Assert.Throws<NullReferenceException>(() => ((PolyMesh)null!).NormalizeUVs());
            });
        }

        [Test]
        public void EmptyAndZeroExtentInputsRetainExistingArithmetic()
        {
            var cases = new[]
            {
                (Indices: Array.Empty<int>(), Coordinates: Array.Empty<V2d>()),
                (Indices: Array.Empty<int>(), Coordinates: new[] { new V2d(3, 7), new V2d(-9, 11) }),
                (Indices: new[] { 0 }, Coordinates: new[] { new V2d(3, 7) }),
                (Indices: new[] { 0, 1 }, Coordinates: new[] { new V2d(3, 4), new V2d(3, 12) }),
                (Indices: new[] { 0, 1 }, Coordinates: new[] { new V2d(2, 7), new V2d(10, 7) }),
                (Indices: new[] { 0, 1 }, Coordinates: new[] { new V2d(-0.0, 1), new V2d(0.0, 2) })
            };
            Assert.Multiple(() =>
            {
                for (var c = 0; c < cases.Length; ++c)
                foreach (var route in new[] { 0, 1, 2 })
                {
                    var (indices, coords) = cases[c];
                    var mesh = Mesh(new[] { 1, 2, 3 }, 0);
                    mesh.VertexIndexArray = indices.ToArray();
                    mesh.FirstIndexArray = new[] { 0, indices.Length };
                    var box = new Box2d(indices.Select(i => coords[i]));
                    var scale = 1.0 / (box.Max - box.Min);
                    var expected = coords.Select(p => (p - box.Min) * scale).ToArray();
                    CheckNormalization(mesh, route, coords.ToArray(), expected, $"case={c}, route={route}", finite: false);
                }
            });
        }
    }
}
