using Aardvark.Base;
using Aardvark.Base.Coder;
using Aardvark.Rendering;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.IO;
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

        [Test]
        public void ResolveLineAttributesWithoutSharedVertices()
        {
            var set = TwoPolyLines();
            set.LineAttributes[PolyLineSet.Property.Colors] = new[] { C4b.Red, C4b.Blue };

            var resolved = set.WithResolvedLineAttributes();

            // nothing to split, the arrays are shared and only the colours move to the vertices
            ClassicAssert.AreSame(set.VertexArray, resolved.VertexArray);
            ClassicAssert.AreSame(set.FirstIndexArray, resolved.FirstIndexArray);
            ClassicAssert.AreEqual(set.VertexIndexArray, resolved.VertexIndexArray);
            ClassicAssert.AreEqual(0, resolved.LineAttributes.Count);
            ClassicAssert.AreEqual(0, resolved.LineVertexAttributes.Count);
            ClassicAssert.AreEqual(new[] { C4b.Red, C4b.Red, C4b.Red, C4b.Blue, C4b.Blue }, (C4b[])resolved.VertexAttributes[PolyLineSet.Property.Colors]);

            // the source is untouched
            ClassicAssert.AreEqual(1, set.LineAttributes.Count);
            ClassicAssert.AreEqual(0, set.VertexAttributes.Count);
        }

        [Test]
        public void ResolveLineAttributesSplitsConflictingVertex()
        {
            // three polylines through vertex 2: red, blue and red again, so vertex 2 needs one blue duplicate
            var vertices = new[] { V3d.Zero, V3d.IOO, V3d.IIO, V3d.OIO, V3d.OOI };
            var set = PolyLineSet.FromIndexed(vertices, new[] { new[] { 0, 1, 2 }, new[] { 2, 3 }, new[] { 2, 4 } });
            set.VertexAttributes[PolyLineSet.Property.Normals] = new[] { V3d.OOI, V3d.OOI, V3d.OIO, V3d.OOI, V3d.OOI };
            set.LineAttributes[PolyLineSet.Property.Colors] = new[] { C4b.Red, C4b.Blue, C4b.Red };

            var resolved = set.WithResolvedLineAttributes();

            ClassicAssert.AreEqual(6, resolved.VertexCount);
            ClassicAssert.AreEqual(V3d.IIO, resolved.VertexArray[5]);
            ClassicAssert.AreEqual(new[] { 0, 1, 2, 5, 3, 2, 4 }, resolved.VertexIndexArray);
            ClassicAssert.AreEqual(new[] { C4b.Red, C4b.Red, C4b.Red, C4b.Blue, C4b.Red, C4b.Blue }, (C4b[])resolved.VertexAttributes[PolyLineSet.Property.Colors]);
            ClassicAssert.AreEqual(V3d.OIO, ((V3d[])resolved.VertexAttributes[PolyLineSet.Property.Normals])[5]);

            // the polylines still run through the same positions
            for (var pi = 0; pi < set.PolyLineCount; pi++)
                ClassicAssert.AreEqual(set.GetPolyLine(pi).ToArray(), resolved.GetPolyLine(pi).ToArray());
        }

        [Test]
        public void ResolveLineVertexAttributes()
        {
            // a closed loop whose last colour differs from its first, so the start vertex is split
            var set = PolyLineSet.FromIndexed(s_square, new[] { new[] { 0, 1, 2, 3, 0 } });
            set.LineVertexAttributes[PolyLineSet.Property.Colors] = new[] { C4b.Red, C4b.Red, C4b.Green, C4b.Green, C4b.Blue };

            var resolved = set.WithResolvedLineAttributes();

            ClassicAssert.AreEqual(5, resolved.VertexCount);
            ClassicAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, resolved.VertexIndexArray);
            ClassicAssert.AreEqual(new[] { C4b.Red, C4b.Red, C4b.Green, C4b.Green, C4b.Blue }, (C4b[])resolved.VertexAttributes[PolyLineSet.Property.Colors]);
            ClassicAssert.AreEqual(V3d.Zero, resolved.VertexArray[4]);
        }

        [Test]
        public void ResolveLineAttributesWithNothingToResolve()
        {
            var set = TwoPolyLines();
            ClassicAssert.AreSame(set, set.WithResolvedLineAttributes());
            ClassicAssert.AreEqual(5, set.GetIndexedGeometry(true).IndexedAttributes[DefaultSemantic.Positions].Length);
        }

        [Test]
        public void GetIndexedGeometryResolvesLineAttributes()
        {
            var set = PolyLineSet.FromIndexed(s_square, new[] { new[] { 0, 1, 2 }, new[] { 2, 3 } });
            set.LineAttributes[PolyLineSet.Property.Colors] = new[] { C4b.Red, C4b.Blue };

            ClassicAssert.IsFalse(set.GetIndexedGeometry().IndexedAttributes.ContainsKey(DefaultSemantic.Colors));

            var geometry = set.GetIndexedGeometry(true);

            ClassicAssert.AreEqual(5, geometry.IndexedAttributes[DefaultSemantic.Positions].Length);
            ClassicAssert.AreEqual(new[] { C4b.Red, C4b.Red, C4b.Red, C4b.Blue, C4b.Blue }, (C4b[])geometry.IndexedAttributes[DefaultSemantic.Colors]);
            ClassicAssert.AreEqual(new[] { 0, 1, 1, 2, 4, 3 }, (int[])geometry.IndexArray);
        }

        [Test]
        public void FieldCodingRoundTrip()
        {
            // the test host cannot run Aardvark.Init(), so the type is registered by hand as in PointClustering
            Base.Coder.TypeInfo.Add(typeof(PolyLineSet));

            var set = PolyLineSet.FromIndexed(s_square, new[] { new[] { 0, 1, 2 }, new[] { 2, 3 } });
            set.VertexAttributes[PolyLineSet.Property.Normals] = new[] { V3d.OOI, V3d.OOI, V3d.OOI, V3d.OOI };
            set.LineAttributes[PolyLineSet.Property.Colors] = new[] { C4b.Red, C4b.Blue };
            set.LineVertexAttributes["Weights"] = new[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f };
            set.InstanceAttributes["Name"] = "grid";

            var stream = new MemoryStream();
            using (var writer = new BinaryWritingCoder(stream))
            {
                object o = set;
                writer.Code(ref o);
            }

            stream.Position = 0;
            PolyLineSet decoded;
            using (var reader = new BinaryReadingCoder(stream))
            {
                object o = null;
                reader.Code(ref o);
                decoded = (PolyLineSet)o;
            }

            ClassicAssert.AreEqual(set.VertexArray, decoded.VertexArray);
            ClassicAssert.AreEqual(set.VertexIndexArray, decoded.VertexIndexArray);
            ClassicAssert.AreEqual(set.FirstIndexArray, decoded.FirstIndexArray);
            ClassicAssert.AreEqual(set.VertexAttributes[PolyLineSet.Property.Normals], decoded.VertexAttributes[PolyLineSet.Property.Normals]);
            ClassicAssert.AreEqual(set.LineAttributes[PolyLineSet.Property.Colors], decoded.LineAttributes[PolyLineSet.Property.Colors]);
            ClassicAssert.AreEqual(set.LineVertexAttributes["Weights"], decoded.LineVertexAttributes["Weights"]);
            ClassicAssert.AreEqual("grid", decoded.InstanceAttributes["Name"]);
            ClassicAssert.AreEqual(set.SegmentCount, decoded.SegmentCount);
        }
    }
}
