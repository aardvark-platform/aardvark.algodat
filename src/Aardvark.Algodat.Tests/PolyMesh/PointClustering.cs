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
            // for some reson Aardvark.Init() crashes when in UnitTest -> manually register TypeCoder
            Aardvark.Base.Coder.TypeInfo.Add(typeof(PolyMesh));

#if NETCOREAPP
            var data = File.ReadAllBytes("../../../src/Aardvark.Algodat.Tests/PolyMesh/test.mesh");
#else
            var data = File.ReadAllBytes("src/Aardvark.Algodat.Tests/PolyMesh/test.mesh");
#endif

            var mesh = data.Decode<PolyMesh>();
            mesh = mesh.WithoutDegeneratedEdges();
            mesh = mesh.WithoutDegeneratedFaces();
            mesh.BuildTopology();
        }

    }
}
