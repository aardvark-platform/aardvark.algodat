using Aardvark.Base;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class PlaneNormalTests
    {
        private static readonly Func<V2d, PolyMesh>[] Planes = { PolyMeshPrimitives.PlaneXY, PolyMeshPrimitives.PlaneXZ, PolyMeshPrimitives.PlaneYZ };
        private static readonly V2d[] Sizes = { new V2d(1, 1), new V2d(2, 2), new V2d(2, 3), new V2d(0.125, 0.5), new V2d(8, 0.25), new V2d(31, 7) };
        private static readonly V3d[] U = { V3d.IOO, V3d.IOO, V3d.OIO };
        private static readonly V3d[] V = { V3d.OIO, V3d.OOI, V3d.OOI };
        private static readonly int[] Corners = { 0, 1, 3, 0, 3, 2 };
        private static readonly V2d[] Uvs = { V2d.OO, V2d.IO, V2d.OI, V2d.II };

        [Test]
        public void StoredPlaneNormalsAgreeWithBothIndependentTriangleCrossProducts()
        {
            for (var plane = 0; plane < Planes.Length; plane++)
            foreach (var size in Sizes)
            {
                var mesh = Planes[plane](size);
                var context = $"plane={plane}, size={size}";
                CheckLayout(mesh, plane, size, context);
                var normal = ((V3d[])mesh.FaceAttributes[PolyMesh.Property.Normals]).Single();
                for (var triangle = 0; triangle < 2; triangle++)
                {
                    var p = mesh.PositionArray;
                    var cross = Cross(p[Corners[3 * triangle + 1]] - p[Corners[3 * triangle]], p[Corners[3 * triangle + 2]] - p[Corners[3 * triangle]]);
                    Assert.That(Dot(cross, cross), Is.GreaterThan(0), context);
                    Equal(normal, Unit(cross), $"{context}, triangle={triangle}");
                }
                Equal(normal, Unit(Cross(U[plane], V[plane])), context);
            }
        }

        [Test]
        public void DefaultExportNormalsAgreeWithBothExportedTrianglesWithoutChangingAttributes()
        {
            for (var plane = 0; plane < Planes.Length; plane++)
            foreach (var size in Sizes)
            {
                var mesh = Planes[plane](size);
                var context = $"export plane={plane}, size={size}";
                var unchanged = Snapshot(mesh, context);
                var geometry = mesh.GetIndexedGeometry();
                var positions = (V3f[])geometry.IndexedAttributes[PolyMesh.Property.Positions];
                var normals = (V3f[])geometry.IndexedAttributes[PolyMesh.Property.Normals];
                var coordinates = (V2f[])geometry.IndexedAttributes[PolyMesh.Property.DiffuseColorCoordinates];
                var ut = (V3f[])geometry.IndexedAttributes[PolyMesh.Property.DiffuseColorUTangents];
                var vt = (V3f[])geometry.IndexedAttributes[PolyMesh.Property.DiffuseColorVTangents];
                var indices = (int[])geometry.IndexArray;
                Assert.That(indices.Length, Is.EqualTo(6), context);
                foreach (var length in new[] { positions.Length, normals.Length, coordinates.Length, ut.Length, vt.Length })
                    Assert.That(length, Is.EqualTo(4), context);
                for (var triangle = 0; triangle < 2; triangle++)
                {
                    var i = triangle * 3;
                    for (var k = 0; k < 3; k++) Assert.That(indices[i + k], Is.InRange(0, 3), context);
                    var a = (V3d)positions[indices[i]];
                    var b = (V3d)positions[indices[i + 1]];
                    var c = (V3d)positions[indices[i + 2]];
                    var expectedNormal = Unit(Cross(b - a, c - a));
                    for (var k = 0; k < 3; k++)
                    {
                        var index = indices[i + k];
                        Equal((V3d)positions[index], Position(plane, size, Corners[i + k]), context);
                        Equal((V3d)normals[index], expectedNormal, $"{context}, triangle={triangle}, corner={k}");
                        Assert.That(coordinates[index], Is.EqualTo((V2f)Uvs[Corners[i + k]]), context);
                        Equal((V3d)ut[index], U[plane], context);
                        Equal((V3d)vt[index], V[plane], context);
                    }
                }
                unchanged();
                CheckLayout(mesh, plane, size, context);
            }
        }

        [Test]
        public void TwoSidedActualRayHitsKeepTheirGeometryAndReportWindingAlignedNormals()
        {
            for (var plane = 0; plane < Planes.Length; plane++)
            foreach (var size in Sizes)
            {
                var mesh = Planes[plane](size);
                var unchanged = Snapshot(mesh, $"ray plane={plane}, size={size}");
                var expectedNormal = Unit(Cross(U[plane], V[plane]));
                for (var triangle = 0; triangle < 2; triangle++)
                foreach (var side in new[] { -1, 1 })
                foreach (var distance in new[] { 1.0, 3.5 })
                foreach (var speed in new[] { 1.0, 2.0 })
                {
                    var fractions = triangle == 0 ? new V2d(0.25, -0.125) : new V2d(-0.125, 0.25);
                    var point = U[plane] * (size.X * fractions.X) + V[plane] * (size.Y * fractions.Y);
                    var ray = new Ray3d(point + expectedNormal * (side * distance), expectedNormal * (-side * speed));
                    var context = $"ray plane={plane}, size={size}, triangle={triangle}, side={side}, distance={distance}, speed={speed}";
                    var tag = new object();
                    var stack = new List<SetObject> { new SetObject(mesh, 17) };
                    var hit = new ObjectRayHit(100) { Tag = tag, ObjectStack = stack };
                    Assert.That(mesh.ObjectsIntersectRay(new[] { 0 }, 0, 1, new FastRay3d(ray), null, null, 0, 100, ref hit), Is.True, context);
                    Assert.That(hit.SetObject.Set, Is.SameAs(mesh), context);
                    Assert.That(hit.SetObject.Index, Is.Zero, context);
                    Assert.That(hit.RayHit.Part, Is.EqualTo(triangle), context);
                    Assert.That(hit.RayHit.T, Is.EqualTo(distance / speed).Within(1e-13), context);
                    Equal(hit.RayHit.Point, point, context);
                    Assert.That(hit.RayHit.Coord, Is.EqualTo(new V2d(0.375, 0.375)), context);
                    Assert.That(hit.RayHit.BackSide, Is.EqualTo(side < 0), context);
                    Assert.That(hit.Tag, Is.SameAs(tag), context);
                    Assert.That(hit.ObjectStack, Is.SameAs(stack), context);
                    Assert.That(stack.Single().Index, Is.EqualTo(17), context);
                    Assert.That(mesh.Intersects(ray, out var publicHit), Is.True, context);
                    Assert.That(publicHit.T, Is.EqualTo(hit.RayHit.T).Within(1e-13), context);
                    Equal(publicHit.Point, point, context);
                    Assert.That(publicHit.Part, Is.Zero, context);
                    Assert.That(publicHit.BackSide, Is.EqualTo(hit.RayHit.BackSide), context);
                    Assert.That(double.IsNaN(publicHit.Coord.X) && double.IsNaN(publicHit.Coord.Y), Is.True, context);
                    var all = mesh.GetRayIntersections(ray);
                    Assert.That(all.Count, Is.EqualTo(1), context);
                    Equal(all[0].Point, point, context);
                    Assert.That(all[0].T, Is.EqualTo(hit.RayHit.T).Within(1e-13), context);
                    var info = hit.GetIntersectionRayHitInfo();
                    Assert.That(info.HasValidNormal, Is.True, context);
                    Equal(info.Normal, expectedNormal, context); // Winding-aligned, not flipped toward either ray origin.
                    Assert.That(info.Points, Is.EqualTo(new[] { mesh.PositionArray[0], mesh.PositionArray[1], mesh.PositionArray[3], mesh.PositionArray[2] }), context);
                }
                unchanged();
                CheckLayout(mesh, plane, size, $"after rays plane={plane}, size={size}");
            }
        }

        [Test]
        public void ConstructionPreservesPositionsIndicesUvsTangentsAndUnvalidatedSizes()
        {
            var sizes = Sizes.Concat(new[] { V2d.Zero, new V2d(-2, 3), new V2d(2, -3), new V2d(-2, -3) });
            for (var plane = 0; plane < Planes.Length; plane++)
            foreach (var size in sizes) CheckLayout(Planes[plane](size), plane, size, $"layout plane={plane}, size={size}");
        }

        private static void CheckLayout(PolyMesh mesh, int plane, V2d size, string context)
        {
            Assert.That(mesh.VertexCount, Is.EqualTo(4), context);
            Assert.That(mesh.FaceCount, Is.EqualTo(1), context);
            Assert.That(mesh.VertexIndexCount, Is.EqualTo(4), context);
            Assert.That(mesh.PositionArray.Length, Is.EqualTo(4), context);
            for (var i = 0; i < 4; i++) Equal(mesh.PositionArray[i], Position(plane, size, i), context);
            Assert.That(mesh.FirstIndexArray, Is.EqualTo(new[] { 0, 4 }), context);
            Assert.That(mesh.VertexIndexArray, Is.EqualTo(new[] { 0, 1, 3, 2 }), context);
            Assert.That(mesh.VertexAttributes.Keys, Is.EquivalentTo(new[] { PolyMesh.Property.Positions, PolyMesh.Property.DiffuseColorCoordinates }), context);
            Assert.That(mesh.FaceAttributes.Keys, Is.EquivalentTo(new[] { PolyMesh.Property.Normals, PolyMesh.Property.DiffuseColorUTangents, PolyMesh.Property.DiffuseColorVTangents }), context);
            Assert.That(mesh.VertexAttributes[PolyMesh.Property.DiffuseColorCoordinates], Is.TypeOf<V2d[]>(), context);
            Assert.That(mesh.VertexAttributes[PolyMesh.Property.DiffuseColorCoordinates], Is.EqualTo(Uvs), context);
            foreach (var key in mesh.FaceAttributes.Keys)
            {
                Assert.That(mesh.FaceAttributes[key], Is.TypeOf<V3d[]>(), context);
                Assert.That(mesh.FaceAttributes[key].Length, Is.EqualTo(1), context);
            }
            Equal(((V3d[])mesh.FaceAttributes[PolyMesh.Property.DiffuseColorUTangents])[0], U[plane], context);
            Equal(((V3d[])mesh.FaceAttributes[PolyMesh.Property.DiffuseColorVTangents])[0], V[plane], context);
            Assert.That(mesh.FaceVertexAttributes.Count, Is.Zero, context);
            Assert.That(mesh.InstanceAttributes.Count, Is.Zero, context);
            Assert.That(mesh.HasTopology, Is.False, context);
        }

        private static Action Snapshot(PolyMesh mesh, string context)
        {
            var vertex = mesh.VertexAttributes;
            var face = mesh.FaceAttributes;
            var first = mesh.FirstIndexArray;
            var indices = mesh.VertexIndexArray;
            var arrays = vertex.Concat(face).ToArray();
            var copies = arrays.Select(kv => kv.Value.Clone()).ToArray();
            return () =>
            {
                Assert.That(mesh.VertexAttributes, Is.SameAs(vertex), context);
                Assert.That(mesh.FaceAttributes, Is.SameAs(face), context);
                Assert.That(mesh.FirstIndexArray, Is.SameAs(first), context);
                Assert.That(mesh.VertexIndexArray, Is.SameAs(indices), context);
                var current = vertex.Concat(face).ToArray();
                Assert.That(current.Select(kv => kv.Key), Is.EqualTo(arrays.Select(kv => kv.Key)), context);
                for (var i = 0; i < arrays.Length; i++)
                {
                    Assert.That(current[i].Value, Is.SameAs(arrays[i].Value), context);
                    Assert.That(current[i].Value, Is.EqualTo(copies[i]), context);
                }
            };
        }

        private static V3d Position(int plane, V2d size, int corner)
        {
            var x = size.X * (corner == 0 || corner == 2 ? -0.5 : 0.5);
            var y = size.Y * (corner < 2 ? -0.5 : 0.5);
            return plane == 0 ? new V3d(x, y, 0) : plane == 1 ? new V3d(x, 0, y) : new V3d(0, x, y);
        }
        private static V3d Cross(V3d a, V3d b) => new V3d(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        private static double Dot(V3d a, V3d b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        private static V3d Unit(V3d p) => p / Math.Sqrt(Dot(p, p));
        private static void Equal(V3d actual, V3d expected, string context)
        {
            Assert.That(actual.IsNaN || actual.IsInfinity, Is.False, context);
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(1e-13), context);
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(1e-13), context);
            Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(1e-13), context);
        }
    }
}
