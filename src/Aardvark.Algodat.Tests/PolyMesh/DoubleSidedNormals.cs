using Aardvark.Base;
using Aardvark.Geometry;
using NUnit.Framework;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class DoubleSidedNormals
    {
        [Test]
        public void TestDoubleSidedPrimitive()
        {
            //var mesh = PolyMeshPrimitives.Sphere(50, 1, C4b.White);

            var simple = new PolyMesh()
            {
                PositionArray = new[]
                {
                    new V3d(0, 0, 0),
                    new V3d(2, 0, 0),
                    new V3d(1, 1, 0),
                    new V3d(1, 2, 0),
                },
                FirstIndexArray = new[] { 0, 3, 6, 9 },
                VertexIndexArray = new[]
                {
                    0, 1, 2,
                    1, 3, 2,
                    0, 2, 3
                }
            };

            var dd = MakeDoubleSided(simple);

            dd = dd.WithPerVertexIndexedNormals(30 * Constant.RadiansPerDegree);

            var faceNormals = (V3d[])dd.FaceAttributes[PolyMesh.Property.Normals];
            var creaseNormals = (V3d[])dd.FaceVertexAttributes[-PolyMesh.Property.Normals];
            var creaseNormalIndices = (int[])dd.FaceVertexAttributes[PolyMesh.Property.Normals];

            Assert.IsTrue(creaseNormals.Length == 8);
        }

        static PolyMesh MakeDoubleSided(PolyMesh mesh)
        {
            // duplicate indices to backside
            var via = mesh.VertexIndexArray;
            var fia = mesh.FirstIndexArray;

            var viaDouble = new int[via.Length * 2].SetByIndex(via.Length, i => via[i]);
            var fiaDouble = new int[fia.Length * 2 - 1].SetByIndex(fia.Length, i => fia[i]);

            for (int fi = 0; fi < mesh.FaceCount; fi++)
            {
                int fvs = fia[fi];
                int fve = fia[fi + 1];

                // end of current face
                fiaDouble[fia.Length + fi] = via.Length + fve;

                for (int fvi = fvs; fvi < fve; fvi++)
                {
                    int revInd = fve - (fvi - fvs) - 1; // reverse index order
                    viaDouble[via.Length + fvi] = via[revInd];
                }
            }

            // create new mesh with just positions and double-sided face indices
            return new PolyMesh()
            {
                PositionArray = mesh.PositionArray,
                VertexIndexArray = viaDouble,
                FirstIndexArray = fiaDouble,
            };
        }
    }
}
