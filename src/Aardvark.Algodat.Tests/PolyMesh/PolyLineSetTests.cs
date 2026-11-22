using Aardvark.Base;
using Aardvark.Base.Coder;
using Aardvark.Rendering;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;

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
        public void ResolutionPreservesEncounterOrderCompleteTuplesAndCollisions()
        {
            foreach (var count in new[] { 64, 512 })
            foreach (var mode in new[] { 0, 1, 2 }) // distinct, repeated, no conflicts
            foreach (var collisions in new[] { false, true })
            foreach (var combined in new[] { false, true })
            {
                var context = $"lines={count}, mode={mode}, collisions={collisions}, combined={combined}";
                var vertices = new[] { V3d.Zero, V3d.IOO, new V3d(7, 8, 9) }; // vertex 2 stays unreferenced
                var runs = Enumerable.Range(0, count).Select(i => i % 2 == 0 ? new[] { 1, 0, 1 } : new[] { 0, 1 }).ToArray();
                var set = PolyLineSet.FromIndexed(vertices, runs);
                var normals = new[] { V3d.OOI, V3d.OIO, V3d.IOO };
                set.VertexAttributes[PolyLineSet.Property.Normals] = normals;
                set.VertexAttributes[PolyLineSet.Property.Colors] = new[] { C4f.White, C4f.White, C4f.White };
                var labels = Enumerable.Range(0, count).Select(i => mode == 0 ? i : mode == 1 ? i % 8 : 0).ToArray();
                set.LineAttributes["Label"] = labels.Select(i => i == 0 ? null : new ResolutionValue(i, collisions)).ToArray();
                set.LineAttributes[PolyLineSet.Property.Colors] = labels.Select(i => LineColor(collisions ? 0 : i)).ToArray();
                var corners = Enumerable.Range(0, set.VertexIndexCount).Select(i => mode == 2 ? set.VertexIndexArray[i] : i % 3).ToArray();
                if (combined)
                {
                    set.LineVertexAttributes[PolyLineSet.Property.Colors] = corners.Select(i => CornerColor(i)).ToArray();
                    set.LineVertexAttributes["Weight"] = corners.Select(i => i + 0.25f).ToArray();
                }
                set.InstanceAttributes["Name"] = "junction";
                var snapshots = SnapshotAttributes(set);
                var originalIndices = (int[])set.VertexIndexArray.Clone();
                var originalFirst = (int[])set.FirstIndexArray.Clone();
                var keys = new Dictionary<(int Vertex, int Label, int Corner), int>();
                var assigned = new bool[vertices.Length];
                var sources = new List<int>(Enumerable.Range(0, vertices.Length));
                var expectedLabels = new List<int>(Enumerable.Repeat(0, vertices.Length));
                var expectedCorners = new List<int>(Enumerable.Repeat(0, vertices.Length));
                var expectedIndices = new List<int>();
                for (var line = 0; line < count; line++)
                for (var corner = set.FirstIndexArray[line]; corner < set.FirstIndexArray[line + 1]; corner++)
                {
                    var vertex = originalIndices[corner];
                    var key = (vertex, labels[line], combined ? corners[corner] : 0);
                    if (!keys.TryGetValue(key, out var output))
                    {
                        output = assigned[vertex] ? sources.Count : vertex;
                        if (output == sources.Count) { sources.Add(vertex); expectedLabels.Add(0); expectedCorners.Add(0); }
                        assigned[vertex] = true;
                        expectedLabels[output] = labels[line]; expectedCorners[output] = corners[corner];
                        keys.Add(key, output);
                    }
                    expectedIndices.Add(output);
                }
                var resolved = set.WithResolvedLineAttributes();
                Assert.That(resolved.VertexIndexArray, Is.EqualTo(expectedIndices), context + ", first-encounter indices");
                Assert.That(resolved.VertexArray, Is.EqualTo(sources.Select(i => vertices[i]).ToArray()), context + ", positions");
                Assert.That(resolved.FirstIndexArray, Is.SameAs(set.FirstIndexArray), context + ", shared first indices");
                Assert.That(resolved.InstanceAttributes, Is.SameAs(set.InstanceAttributes), context + ", shared instance attributes");
                Assert.That(resolved.VertexIndexArray, Is.Not.SameAs(set.VertexIndexArray), context + ", owned index copy");
                Assert.That(resolved.LineAttributes.Count + resolved.LineVertexAttributes.Count, Is.Zero, context + ", resolved domains");
                Assert.That(resolved.VertexAttributes[PolyLineSet.Property.Normals], Is.TypeOf<V3d[]>(), context);
                Assert.That(resolved.VertexAttributes[PolyLineSet.Property.Normals], Is.EqualTo(sources.Select(i => normals[i]).ToArray()), context + ", copied vertex values");
                var actualLabels = (ResolutionValue[])resolved.VertexAttributes["Label"];
                Assert.That(actualLabels.Select(v => v == null ? 0 : v.Id).ToArray(), Is.EqualTo(expectedLabels), context + ", aligned labels/null defaults");
                var expectedColors = combined
                    ? (Array)expectedCorners.Select((c, i) => i == 2 ? default(C4f) : CornerColor(c)).ToArray()
                    : expectedLabels.Select((v, i) => i == 2 ? default(C4b) : LineColor(collisions ? 0 : v)).ToArray();
                Assert.That(resolved.VertexAttributes[PolyLineSet.Property.Colors].GetType(), Is.EqualTo(expectedColors.GetType()), context + ", runtime type/precedence");
                Assert.That(resolved.VertexAttributes[PolyLineSet.Property.Colors], Is.EqualTo(expectedColors), context + ", aligned colors");
                if (combined) Assert.That(resolved.VertexAttributes["Weight"], Is.EqualTo(expectedCorners.Select((c, i) => i == 2 ? 0f : c + 0.25f).ToArray()), context + ", corner weights");
                if (mode == 2)
                {
                    Assert.That(resolved.VertexArray, Is.SameAs(vertices), context + ", no-conflict positions sharing");
                    Assert.That(resolved.VertexAttributes[PolyLineSet.Property.Normals], Is.SameAs(normals), context + ", no-conflict attribute sharing");
                }
                var geometry = set.GetIndexedGeometry(true);
                Assert.That(geometry.Mode, Is.EqualTo(IndexedGeometryMode.LineList), context);
                Assert.That(geometry.IndexArray, Is.EqualTo(resolved.BuildLineListIndexArray()), context + ", geometry indices");
                Assert.That(geometry.IndexedAttributes[DefaultSemantic.Positions], Is.EqualTo(resolved.VertexArray.Select(p => (V3f)p).ToArray()), context + ", geometry positions");
                foreach (var kv in resolved.VertexAttributes)
                    Assert.That(geometry.IndexedAttributes[kv.Key], Is.EqualTo(kv.Value is V3d[] ds ? ds.Select(p => (V3f)p).ToArray() : kv.Value), context + ", geometry " + kv.Key);
                Assert.That(set.VertexArray, Is.SameAs(vertices), context + ", source positions");
                Assert.That(set.VertexArray, Is.EqualTo(new[] { V3d.Zero, V3d.IOO, new V3d(7, 8, 9) }), context);
                Assert.That(set.VertexIndexArray, Is.EqualTo(originalIndices), context + ", source indices");
                Assert.That(set.FirstIndexArray, Is.EqualTo(originalFirst), context + ", source first indices");
                foreach (var s in snapshots)
                {
                    Assert.That(s.Dictionary[s.Key], Is.SameAs(s.Original), context + ", source array identity");
                    Assert.That(s.Dictionary[s.Key], Is.EqualTo(s.Copy), context + ", source values");
                }
            }
        }

        [Test]
        public void NullAndShadowedValuesRemainPartOfTheCompleteTuple()
        {
            var set = PolyLineSet.FromIndexed(new[] { V3d.Zero, V3d.IOO, V3d.OOI },
                new[] { new[] { 0, 1 }, new[] { 0, 1 }, new[] { 0, 1 }, new[] { 1, 0 } });
            var original = new[] { "vertex0", "vertex1", "unused" };
            var lines = new[] { null, null, "shadowed", null };
            var corners = new[] { null, "x", null, "x", null, "x", "x", null };
            set.VertexAttributes["Tag"] = original;
            set.LineAttributes["Tag"] = lines;
            set.LineVertexAttributes["Tag"] = corners;
            var result = set.WithResolvedLineAttributes();
            // The differing line value still distinguishes tuples even though the corner value wins.
            Assert.That(result.VertexIndexArray, Is.EqualTo(new[] { 0, 1, 0, 1, 3, 4, 1, 0 }));
            Assert.That(result.VertexAttributes["Tag"], Is.TypeOf<string[]>());
            Assert.That(result.VertexAttributes["Tag"], Is.EqualTo(new[] { null, "x", null, null, "x" }));
            Assert.That(set.VertexAttributes["Tag"], Is.SameAs(original));
            Assert.That(set.LineAttributes["Tag"], Is.EqualTo(new[] { null, null, "shadowed", null }));
            Assert.That(set.LineVertexAttributes["Tag"], Is.EqualTo(new[] { null, "x", null, "x", null, "x", "x", null }));
            Assert.That(set.GetIndexedGeometry(true).IndexArray, Is.EqualTo(result.VertexIndexArray));
            Assert.That(result.WithResolvedLineAttributes(), Is.SameAs(result));
            var empty = new PolyLineSet(); empty.LineAttributes["Tag"] = Array.Empty<string>();
            Assert.That(empty.WithResolvedLineAttributes(), Is.SameAs(empty));

            var ieee = PolyLineSet.FromIndexed(new[] { V3d.Zero, V3d.IOO }, Enumerable.Range(0, 4).Select(_ => new[] { 0, 1 }));
            var nan = BitConverter.Int64BitsToDouble(0x7ff8000000000001);
            ieee.LineAttributes["Scalar"] = new[] { nan, BitConverter.Int64BitsToDouble(0x7ff8000000000002), 0.0, -0.0 };
            var ieeeResult = ieee.WithResolvedLineAttributes();
            Assert.That(ieeeResult.VertexIndexArray, Is.EqualTo(new[] { 0, 1, 0, 1, 2, 3, 2, 3 }));
            var scalars = (double[])ieeeResult.VertexAttributes["Scalar"];
            Assert.That(BitConverter.DoubleToInt64Bits(scalars[0]), Is.EqualTo(BitConverter.DoubleToInt64Bits(nan)), "first NaN payload retained");
            Assert.That(BitConverter.DoubleToInt64Bits(scalars[2]), Is.Zero, "equal signed zeros reuse the first value");
        }

        [Test, NonParallelizable]
        public void WellDistributedJunctionValuesUseBoundedEqualityWork()
        {
            foreach (var count in new[] { 256, 1024, 2048 })
            {
                var set = PolyLineSet.FromIndexed(new[] { V3d.Zero, V3d.IOO }, Enumerable.Range(0, count).Select(_ => new[] { 0, 1 }));
                set.LineAttributes["Label"] = Enumerable.Range(0, count).Select(i => new ResolutionValue(i, false)).ToArray();
                Interlocked.Exchange(ref ResolutionValue.Comparisons, 0);
                var resolved = set.WithResolvedLineAttributes();
                var comparisons = Interlocked.Read(ref ResolutionValue.Comparisons);
                Assert.That(comparisons, Is.LessThanOrEqualTo(8L * set.VertexIndexCount), $"lines={count}, comparisons={comparisons}");
                Assert.That(resolved.VertexIndexArray, Is.EqualTo(Enumerable.Range(0, 2 * count).ToArray()), $"lines={count}, exact output order");
                Assert.That(((ResolutionValue[])resolved.VertexAttributes["Label"]).Select(v => v.Id).ToArray(), Is.EqualTo(Enumerable.Range(0, 2 * count).Select(i => i / 2).ToArray()), $"lines={count}, labels");
            }
        }

        private sealed class ResolutionValue
        {
            public static long Comparisons;
            public readonly int Id;
            private readonly bool m_collides;
            public ResolutionValue(int id, bool collides) { Id = id; m_collides = collides; }
            public override int GetHashCode() => m_collides ? 0 : Id;
            public override bool Equals(object obj)
            {
                Interlocked.Increment(ref Comparisons);
                return obj is ResolutionValue other && Id == other.Id && m_collides == other.m_collides;
            }
        }

        private static C4b LineColor(int id) => new C4b((byte)(id % 251), (byte)31, (byte)71, (byte)201);
        private static C4f CornerColor(int id) => new C4f(id / 4f, 0.25f, 0.75f, 1f);
        private static (SymbolDict<Array> Dictionary, Symbol Key, Array Original, Array Copy)[] SnapshotAttributes(PolyLineSet set)
            => new[] { set.VertexAttributes, set.LineAttributes, set.LineVertexAttributes }
                .SelectMany(d => d.Select(kv => (d, kv.Key, kv.Value, (Array)kv.Value.Clone()))).ToArray();

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
