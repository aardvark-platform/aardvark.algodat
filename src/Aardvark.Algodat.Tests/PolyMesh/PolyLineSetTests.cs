using Aardvark.Base;
using Aardvark.Rendering;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.Linq;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class PolyLineSetTests
    {
        private static readonly V3d[] s_square = { V3d.Zero, V3d.IOO, V3d.IIO, V3d.OIO };

        private static PolyLineSet TwoPolyLines() => PolyLineSet.FromPolyLines(new[]
        {
            new[] { V3d.Zero, V3d.IOO, new V3d(2, 0, 0) },
            new[] { V3d.OIO, V3d.IIO },
        });

        [Test]
        public void FromPolyLinesCounts()
        {
            var set = TwoPolyLines();

            ClassicAssert.AreEqual(2, set.PolyLineCount);
            ClassicAssert.AreEqual(5, set.VertexCount);
            ClassicAssert.AreEqual(5, set.VertexIndexCount);
            ClassicAssert.AreEqual(3, set.SegmentCount);
            ClassicAssert.AreEqual(3, set.GetVertexCount(0));
            ClassicAssert.AreEqual(2, set.GetSegmentCount(0));
            ClassicAssert.AreEqual(2, set.GetVertexCount(1));
            ClassicAssert.AreEqual(1, set.GetSegmentCount(1));
            ClassicAssert.AreEqual(new[] { 0, 3, 5 }, set.FirstIndexArray);
            ClassicAssert.AreEqual(new[] { V3d.OIO, V3d.IIO }, set.GetPolyLine(1).ToArray());
            ClassicAssert.AreEqual(3, set.Segments.Count());
            ClassicAssert.AreEqual(new Line3d(V3d.IOO, new V3d(2, 0, 0)), set.GetSegments(0).Last());
        }

        [Test]
        public void FromIndexedSharesVerticesAcrossGap()
        {
            // two polylines that meet in vertex 2 but are stored separately, the gap case of an IFC indexed poly curve
            var set = PolyLineSet.FromIndexed(s_square, new[] { new[] { 0, 1, 2 }, new[] { 2, 3 } });

            ClassicAssert.AreSame(s_square, set.VertexArray);
            ClassicAssert.AreEqual(2, set.PolyLineCount);
            ClassicAssert.AreEqual(4, set.VertexCount);
            ClassicAssert.AreEqual(5, set.VertexIndexCount);
            ClassicAssert.AreEqual(3, set.SegmentCount);
            ClassicAssert.AreEqual(new[] { 0, 1, 2, 2, 3 }, set.VertexIndexArray);
            ClassicAssert.AreEqual(V3d.IIO, set.GetPolyLine(1).First());
        }

        [Test]
        public void FromIndexedRejectsIndexOutsideVertexArray()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PolyLineSet.FromIndexed(s_square, new[] { new[] { 0, 4 } }));
        }

        [Test]
        public void DegeneratePolyLinesAreDropped()
        {
            var set = PolyLineSet.FromPolyLines(new[] { new[] { V3d.Zero }, Array.Empty<V3d>(), null, new[] { V3d.Zero, V3d.IOO } });

            ClassicAssert.AreEqual(1, set.PolyLineCount);
            ClassicAssert.AreEqual(2, set.VertexCount);

            var indexed = PolyLineSet.FromIndexed(s_square, new[] { new[] { 1 }, new[] { 2, 3 } });
            ClassicAssert.AreEqual(1, indexed.PolyLineCount);
            ClassicAssert.AreEqual(new[] { 2, 3 }, indexed.VertexIndexArray);
        }

        [Test]
        public void EmptySet()
        {
            var set = PolyLineSet.FromPolyLines(new[] { new[] { V3d.Zero } });

            ClassicAssert.AreEqual(0, set.PolyLineCount);
            ClassicAssert.AreEqual(0, set.VertexCount);
            ClassicAssert.AreEqual(0, set.VertexIndexCount);
            ClassicAssert.AreEqual(0, set.SegmentCount);
            ClassicAssert.IsNull(set.VertexArray);
            ClassicAssert.IsNull(set.VertexIndexArray);
            ClassicAssert.IsNull(set.FirstIndexArray);
            ClassicAssert.IsEmpty(set.BuildLineListIndexArray());
            ClassicAssert.IsFalse(set.BoundingBox3d.IsValid);
            ClassicAssert.IsNull(set.GetIndexedGeometry());
            ClassicAssert.AreEqual(0, new PolyLineSet().PolyLineCount);
        }

        [Test]
        public void BuildLineListIndexArray()
        {
            var set = PolyLineSet.FromIndexed(s_square, new[] { new[] { 0, 1, 2 }, new[] { 3, 0 } });

            ClassicAssert.AreEqual(new[] { 0, 1, 1, 2, 3, 0 }, set.BuildLineListIndexArray());
        }

        [Test]
        public void Transformed()
        {
            var set = TwoPolyLines();
            set.VertexAttributes[PolyLineSet.Property.Normals] = new V3d[] { V3d.OOI, V3d.OOI, V3d.OOI, V3d.OOI, V3d.OOI };
            set.LineAttributes[PolyLineSet.Property.Colors] = new[] { C4b.Red, C4b.Blue };

            var trafo = Trafo3d.Translation(10, 0, 0) * Trafo3d.RotationX(Constant.PiHalf);
            var transformed = set.Transformed(trafo);

            ClassicAssert.AreSame(set.VertexIndexArray, transformed.VertexIndexArray);
            ClassicAssert.AreSame(set.FirstIndexArray, transformed.FirstIndexArray);
            ClassicAssert.AreSame(set.LineAttributes, transformed.LineAttributes);
            ClassicAssert.AreNotSame(set.VertexAttributes, transformed.VertexAttributes);
            ClassicAssert.AreEqual(set.PolyLineCount, transformed.PolyLineCount);
            ClassicAssert.IsTrue(transformed.GetPolyLine(0).Last().ApproximateEquals(new V3d(12, 0, 0), 1e-9));
            ClassicAssert.IsTrue(transformed.GetPolyLine(1).First().ApproximateEquals(new V3d(10, 0, 1), 1e-9));

            var normals = (V3d[])transformed.VertexAttributes[PolyLineSet.Property.Normals];
            ClassicAssert.AreEqual(5, normals.Length);
            ClassicAssert.IsTrue(normals[0].ApproximateEquals(-V3d.OIO, 1e-9));

            // the source is untouched
            ClassicAssert.AreEqual(V3d.Zero, set.GetPolyLine(0).First());
            ClassicAssert.AreEqual(V3d.OOI, ((V3d[])set.VertexAttributes[PolyLineSet.Property.Normals])[0]);
        }

        [Test]
        public void BoundingBoxIgnoresUnreferencedVertices()
        {
            var vertices = new[] { V3d.Zero, V3d.IOO, new V3d(100, 100, 100) };
            var set = PolyLineSet.FromIndexed(vertices, new[] { new[] { 0, 1 } });

            ClassicAssert.AreEqual(new Box3d(V3d.Zero, V3d.IOO), set.BoundingBox3d);
        }

        [Test]
        public void GetIndexedGeometry()
        {
            var set = PolyLineSet.FromIndexed(s_square, new[] { new[] { 0, 1, 2 }, new[] { 2, 3 } });
            set.VertexAttributes[PolyLineSet.Property.Colors] = new[] { C4b.Red, C4b.Green, C4b.Blue, C4b.White };
            set.InstanceAttributes["Name"] = "grid";

            var geometry = set.GetIndexedGeometry();

            ClassicAssert.AreEqual(IndexedGeometryMode.LineList, geometry.Mode);
            ClassicAssert.AreEqual(new[] { 0, 1, 1, 2, 2, 3 }, (int[])geometry.IndexArray);
            ClassicAssert.AreEqual(3, geometry.FaceCount);
            ClassicAssert.IsInstanceOf<V3f[]>(geometry.IndexedAttributes[DefaultSemantic.Positions]);
            ClassicAssert.AreEqual(4, geometry.IndexedAttributes[DefaultSemantic.Positions].Length);
            ClassicAssert.AreEqual(4, geometry.IndexedAttributes[DefaultSemantic.Colors].Length);
            ClassicAssert.AreEqual("grid", geometry.SingleAttributes["Name"]);
        }
    }
}
