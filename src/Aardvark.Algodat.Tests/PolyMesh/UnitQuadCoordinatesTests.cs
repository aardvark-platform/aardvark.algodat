using Aardvark.Base;
using Aardvark.Rendering;
using NUnit.Framework;
using System;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class UnitQuadCoordinatesTests
    {
        private static readonly Symbol Uv = PolyMesh.Property.DiffuseColorCoordinates;
        private static readonly V3d[] Positions = { new V3d(0, 0, 0), new V3d(1, 0, 0), new V3d(0, 1, 0), new V3d(1, 1, 0) };
        private static readonly V2f[] Coordinates = { new V2f(0, 0), new V2f(1, 0), new V2f(0, 1), new V2f(1, 1) };
        private static readonly int[] Corners = { 0, 1, 2, 1, 3, 2 };

        [Test]
        public void UnitQuadStoresFourExactDirectVertexUvsRatherThanSixCornerValues()
        {
            var mesh = PolyMeshPrimitives.UnitQuad();
            Assert.That(mesh.VertexAttributes.ContainsKey(Uv), Is.True, "the four generated UVs belong to the vertex domain");
            Assert.That(mesh.VertexAttributes.Keys, Is.EquivalentTo(new[] { PolyMesh.Property.Positions, Uv }));
            Assert.That(mesh.VertexAttributes[Uv], Is.TypeOf<V2f[]>());
            Assert.That((V2f[])mesh.VertexAttributes[Uv], Is.EqualTo(Coordinates));
            Assert.That(mesh.VertexAttributes[Uv].Length, Is.EqualTo(mesh.VertexCount));
            Assert.That(mesh.VertexIndexCount, Is.EqualTo(6));
            Assert.That(mesh.VertexAttributes.ContainsKey(-Uv), Is.False, "UVs are direct values, not an indexed pool");
            Assert.That(mesh.FaceVertexAttributes.ContainsKey(Uv), Is.False);
            Assert.That(mesh.FaceVertexAttributes.ContainsKey(-Uv), Is.False);
        }

        [Test]
        public void SeparateCallsOwnTheirCoordinateArraysAndAttributeDictionaries()
        {
            var first = PolyMeshPrimitives.UnitQuad();
            var second = PolyMeshPrimitives.UnitQuad();
            Assert.That(first.VertexAttributes.ContainsKey(Uv) && second.VertexAttributes.ContainsKey(Uv), Is.True);
            Assert.That(first.VertexAttributes, Is.Not.SameAs(second.VertexAttributes));
            Assert.That(first.FaceAttributes, Is.Not.SameAs(second.FaceAttributes));
            Assert.That(first.FaceVertexAttributes, Is.Not.SameAs(second.FaceVertexAttributes));
            Assert.That(first.EdgeAttributes, Is.Not.SameAs(second.EdgeAttributes));
            Assert.That(first.InstanceAttributes, Is.Not.SameAs(second.InstanceAttributes));
            Assert.That(first.VertexAttributes[Uv], Is.Not.SameAs(second.VertexAttributes[Uv]));
            Assert.That(first.PositionArray, Is.Not.SameAs(second.PositionArray));
            Assert.That(first.VertexIndexArray, Is.Not.SameAs(second.VertexIndexArray));
            Assert.That(first.FirstIndexArray, Is.Not.SameAs(second.FirstIndexArray));
            ((V2f[])first.VertexAttributes[Uv])[0] = new V2f(7, 11);
            first.PositionArray[0] = new V3d(2, 3, 4);
            var custom = (Symbol)"UnitQuadCustom";
            first.VertexAttributes.Add(custom, new[] { 17 });
            Assert.That(second.VertexAttributes.ContainsKey(custom), Is.False);
            Assert.That(second.VertexAttributes[Uv], Is.EqualTo(Coordinates));
            Assert.That(second.PositionArray, Is.EqualTo(Positions));
            var later = PolyMeshPrimitives.UnitQuad();
            Assert.That(later.VertexAttributes[Uv], Is.EqualTo(Coordinates));
            Assert.That(later.PositionArray, Is.EqualTo(Positions));
            ((V2f[])second.VertexAttributes[Uv])[1] = new V2f(-3, -5);
            Assert.That(later.VertexAttributes[Uv], Is.EqualTo(Coordinates));
            Assert.That(((V2f[])first.VertexAttributes[Uv])[1], Is.EqualTo(Coordinates[1]));
        }

        [Test]
        public void IndexedGeometryAlignsUvsAndPositionsForAllOptionsAndDefaultForwarding()
        {
            var mesh = PolyMeshPrimitives.UnitQuad();
            var positions = mesh.PositionArray; var indices = mesh.VertexIndexArray; var first = mesh.FirstIndexArray;
            var dictionary = mesh.VertexAttributes;
            var coordinates = dictionary.GetOrDefault(Uv);
            // Equivalent flags are one discovered behavior; UV storage remains V2f for every combination.
            for (var mask = 0; mask < 16; mask++)
            {
                var options = (PolyMesh.GetGeometryOptions)mask;
                var context = $"GetIndexedGeometry options={options} (0x{mask:X})";
                CheckExport(mesh.GetIndexedGeometry(options), options, context);
                CheckExport(mesh.GetIndexedGeometry(new[] { 0, 1 }, options), options, context + ", explicit faces");
                CheckSource(mesh, positions, indices, first, dictionary, coordinates, context);
            }
            var defaultOptions = PolyMesh.GetGeometryOptions.Default;
            var direct = mesh.GetIndexedGeometry(defaultOptions);
            var implicitDefault = mesh.GetIndexedGeometry();
            var faceDefault = mesh.GetIndexedGeometry(new[] { 0, 1 });
            CheckExport(implicitDefault, defaultOptions, "implicit default");
            CheckExport(faceDefault, defaultOptions, "explicit-face implicit default");
            Assert.That(implicitDefault.IndexArray, Is.EqualTo(direct.IndexArray));
            Assert.That(faceDefault.IndexArray, Is.EqualTo(direct.IndexArray));
            foreach (var name in new[] { PolyMesh.Property.Positions, Uv })
            {
                Assert.That(implicitDefault.IndexedAttributes[name], Is.EqualTo(direct.IndexedAttributes[name]));
                Assert.That(faceDefault.IndexedAttributes[name], Is.EqualTo(direct.IndexedAttributes[name]));
            }
            CheckSource(mesh, positions, indices, first, dictionary, coordinates, "default forwarding");
        }

        [Test]
        public void GeometryCountsBoundsFaceIntervalsAndWindingRemainUnchanged()
        {
            var mesh = PolyMeshPrimitives.UnitQuad();
            Assert.That(mesh.PositionArray, Is.EqualTo(Positions));
            Assert.That(mesh.VertexIndexArray, Is.EqualTo(Corners));
            Assert.That(mesh.FirstIndexArray, Is.EqualTo(new[] { 0, 3, 6 }));
            Assert.That(mesh.VertexCount, Is.EqualTo(4));
            Assert.That(mesh.Get<int>(PolyMesh.Property.VertexCount), Is.EqualTo(4));
            Assert.That(mesh.VertexIndexCount, Is.EqualTo(6));
            Assert.That(mesh.FaceCount, Is.EqualTo(2));
            Assert.That(mesh.FaceVertexCountRange.Min, Is.EqualTo(3));
            Assert.That(mesh.FaceVertexCountRange.Max, Is.EqualTo(3));
            Assert.That(mesh.BoundingBox3d, Is.EqualTo(new Box3d(V3d.Zero, new V3d(1, 1, 0))));
            Assert.That(mesh.NormalArray, Is.Null);
            Assert.That(mesh.ColorArray, Is.Null);
            Assert.That(mesh.HasTopology, Is.False);
            Assert.That(mesh.FaceAttributes, Is.Empty);
            Assert.That(mesh.FaceVertexAttributes, Is.Empty);
            Assert.That(mesh.EdgeAttributes, Is.Empty);
            Assert.That(mesh.InstanceAttributes, Is.Empty);
            for (var face = 0; face < 2; face++)
            {
                Assert.That(mesh.VertexCountOfFace(face), Is.EqualTo(3));
                var a = mesh.PositionArray[Corners[3 * face]];
                var b = mesh.PositionArray[Corners[3 * face + 1]];
                var c = mesh.PositionArray[Corners[3 * face + 2]];
                Assert.That((b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X), Is.EqualTo(1), $"+Z winding, face={face}");
            }
        }

        private static void CheckExport(IndexedGeometry geometry, PolyMesh.GetGeometryOptions options, string context)
        {
            Assert.That(geometry.Mode, Is.EqualTo(IndexedGeometryMode.TriangleList), context);
            Assert.That(geometry.IndexedAttributes.ContainsKey(Uv), Is.True, context + ", UVs must survive export");
            Assert.That(geometry.IndexedAttributes.Keys, Is.EquivalentTo(new[] { PolyMesh.Property.Positions, Uv }), context);
            var positions = geometry.IndexedAttributes[PolyMesh.Property.Positions];
            Assert.That(positions.GetType(), Is.EqualTo((options & PolyMesh.GetGeometryOptions.FloatVectors) != 0 ? typeof(V3f[]) : typeof(V3d[])), context);
            Assert.That(geometry.IndexedAttributes[Uv], Is.TypeOf<V2f[]>(), context);
            var coordinates = (V2f[])geometry.IndexedAttributes[Uv];
            Assert.That(positions.Length, Is.EqualTo((options & PolyMesh.GetGeometryOptions.Compact) != 0 ? 4 : 6), context);
            Assert.That(coordinates.Length, Is.EqualTo(positions.Length), context);
            Assert.That(geometry.IndexArray, Is.TypeOf((options & PolyMesh.GetGeometryOptions.IntIndices) != 0 ? typeof(int[]) : typeof(short[])), context);
            Assert.That(geometry.IndexArray.Length, Is.EqualTo(6), context);
            for (var corner = 0; corner < 6; corner++)
            {
                var index = Convert.ToInt32(geometry.IndexArray.GetValue(corner));
                Assert.That(index, Is.InRange(0, positions.Length - 1), context + $", corner={corner}");
                var p = positions is V3d[] doubles ? doubles[index] : (V3d)((V3f[])positions)[index];
                Assert.That(p.IsNaN || p.IsInfinity, Is.False, context);
                Assert.That(p, Is.EqualTo(Positions[Corners[corner]]), context + $", position at corner={corner}");
                Assert.That(coordinates[index], Is.EqualTo(Coordinates[Corners[corner]]), context + $", UV at corner={corner}");
                Assert.That(float.IsFinite(coordinates[index].X) && float.IsFinite(coordinates[index].Y), Is.True, context);
                Assert.That(coordinates[index].X, Is.EqualTo((float)p.X), context);
                Assert.That(coordinates[index].Y, Is.EqualTo((float)p.Y), context);
            }
        }
        private static void CheckSource(PolyMesh mesh, V3d[] positions, int[] indices, int[] first, SymbolDict<Array> attributes, Array coordinates, string context)
        {
            Assert.That(mesh.PositionArray, Is.SameAs(positions), context); Assert.That(mesh.PositionArray, Is.EqualTo(Positions), context);
            Assert.That(mesh.VertexIndexArray, Is.SameAs(indices), context); Assert.That(mesh.VertexIndexArray, Is.EqualTo(Corners), context);
            Assert.That(mesh.FirstIndexArray, Is.SameAs(first), context); Assert.That(mesh.FirstIndexArray, Is.EqualTo(new[] { 0, 3, 6 }), context);
            Assert.That(mesh.VertexAttributes, Is.SameAs(attributes), context);
            Assert.That(mesh.VertexAttributes[Uv], Is.SameAs(coordinates), context);
            Assert.That(mesh.VertexAttributes[Uv], Is.EqualTo(Coordinates), context);
            Assert.That(mesh.FaceVertexAttributes, Is.Empty, context);
            Assert.That(mesh.VertexCount, Is.EqualTo(4), context); Assert.That(mesh.VertexIndexCount, Is.EqualTo(6), context);
            Assert.That(mesh.FaceCount, Is.EqualTo(2), context); Assert.That(mesh.HasTopology, Is.False, context);
        }
    }
}
