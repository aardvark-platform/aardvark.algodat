using Aardvark.Base;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.Collections.Generic;
using System.Text;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class PolyMeshGrouping
    {
        [Test]
        [Ignore("@lui: please fix or remove.")]
        public void TestPolyMeshGrouping()
        {
            var mesh = PolyMeshPrimitives.Cone(12, 1, 0.5, C4b.Red);

            // NOTE: mesh has FaceVertexNormals (non-indexed)
            ValidateNormals(mesh);

            var grouping = mesh.IntoArray().Group();

            var meshWithBrokenFaceVertexAttributes = grouping.Mesh; // normal array broken

            // NOTE: grouped mesh will also have non-indexed FaceVertexNormals, but half of the data is "zero"
            ValidateNormals(meshWithBrokenFaceVertexAttributes);
        }

        static void ValidateNormals(PolyMesh mesh)
        {
            mesh.Faces.ForEach(f =>
            {
                var fn = f.Polygon3d.ComputeNormal();
                for (int i = 0; i < f.VertexCount; i++)
                {
                    // check if face vertex normal points in similar direction
                    var fvn = f.GetVertexAttribute<V3d>(PolyMesh.Property.Normals, 0);
                    ClassicAssert.True(fvn.Dot(fn) > 0);
                }
            });
        }
    }
}
