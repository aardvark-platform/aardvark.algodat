using Aardvark.Base;
using Aardvark.Base.Coder;
using Aardvark.Geometry.Clustering;
using NUnit.Framework;
using System.IO;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class PointClusteringTest
    {
        [Test]
        public void TestPointClustering()
        {
            Aardvark.Base.Aardvark.Init();
            var data = File.ReadAllBytes("C:\\temp\\test.mesh");
            //var data = File.ReadAllBytes("..\\..\\..\\src\\Aardvark.Algodat.Tests\\PolyMesh\\test.mesh");
            
            PolyMesh mesh = data.Decode<PolyMesh>();

            //var mesh = mesh.VertexClusteredCopy(new PointClustering(mesh.PositionArray, 1e-4));
            mesh.WithoutDegeneratedEdges();
            mesh.WithoutDegeneratedFaces();
            mesh.BuildTopology();
        }

    }
}
