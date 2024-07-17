using Aardvark.Base;
using Aardvark.Base.Coder;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.IO;
using System.Linq;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class PointClusteringTest
    {
        [Test]
        public void TestPointClustering()
        {
            // for some reson Aardvark.Init() crashes when in UnitTest -> manually register TypeCoder
            Base.Coder.TypeInfo.Add(typeof(PolyMesh));

            var possiblePaths = new[]
                        {
                "../../../src/Aardvark.Algodat.Tests/PolyMesh/test.mesh",
                "src/Aardvark.Algodat.Tests/PolyMesh/test.mesh",
                "Aardvark.Algodat.Tests/PolyMesh/test.mesh",
                "PolyMesh/test.mesh"
            };

            var filename = possiblePaths.FirstOrDefault(File.Exists);
            if (filename == null) throw new Exception($"Could not find 'test.mesh' in current directory: {Environment.CurrentDirectory}");
            var data = File.ReadAllBytes(filename);

            var mesh = data.Decode<PolyMesh>();
            mesh = mesh.WithoutDegeneratedEdges();
            mesh = mesh.WithoutDegeneratedFaces();
            mesh.BuildTopology();
        }

    }
}
