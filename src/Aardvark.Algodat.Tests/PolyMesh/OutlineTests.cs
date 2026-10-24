using Aardvark.Base;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class OutlineTests
    {
        private static readonly V3d[] Square = { V3d.Zero, V3d.IOO, new V3d(1, 1, 0), V3d.OIO };
        private static readonly int[][] Triangles = { new[] { 0, 1, 2 }, new[] { 0, 2, 3 } };

        [Test]
        public void SingleUsePolygonSourcesAreConsumedOnceAndReturnRepeatableFourEdgeOutlines()
        {
            Assert.Multiple(() =>
            {
                foreach (var throwOnReopen in new[] { false, true })
                {
                    var mesh = Mesh(Square, Triangles);
                    var unchanged = Snapshot(mesh);
                    var source = new SingleUsePolygons(mesh.Polygons.ToArray(), throwOnReopen);
                    var context = $"throwOnReopen={throwOnReopen}";
                    IEnumerable<Line3d> result = null;
                    Assert.DoesNotThrow(() => result = source.GetOutlines(), context);
                    Assert.That(source.Acquisitions, Is.EqualTo(1), context);
                    Assert.That(source.Disposals, Is.EqualTo(1), context);
                    Assert.That(source.Reads, Is.EqualTo(2), context);
                    var expected = Oracle(Square, Triangles);
                    Assert.That(expected.Count, Is.EqualTo(4));
                    if (result != null)
                    {
                        CheckEdges(result.ToArray(), expected, context);
                        CheckEdges(result.ToArray(), expected, context);
                    }
                    Assert.That(source.Acquisitions, Is.EqualTo(1), context);
                    Assert.That(source.Disposals, Is.EqualTo(1), context);
                    Assert.That(source.Reads, Is.EqualTo(2), context);
                    unchanged();
                }
            });
        }

        [Test]
        public void SourceFailuresAreEagerAndDisposeThePolygonEnumerator()
        {
            foreach (var throwOnReopen in new[] { false, true })
            {
                var mesh = Mesh(Square, Triangles);
                var unchanged = Snapshot(mesh);
                var source = new SingleUsePolygons(mesh.Polygons.ToArray(), throwOnReopen, throwAfter: 1);
                Assert.That(Assert.Throws<InvalidOperationException>(() => source.GetOutlines()), Is.SameAs(source.Failure));
                Assert.That(source.Acquisitions, Is.EqualTo(1));
                Assert.That(source.Reads, Is.EqualTo(1));
                Assert.That(source.Disposals, Is.EqualTo(1));
                unchanged();
            }
        }

        [Test]
        public void SharedRepeatedAndDegenerateEdgesMatchAnIndependentExactOracle()
        {
            var positions = new[] { V3d.Zero, V3d.IOO, new V3d(1, 1, 0), V3d.OIO,
                new V3d(3, 0, 0), new V3d(4, 0, 0), new V3d(3, 1, 0), V3d.IOO };
            var triangle = new[] { 0, 1, 2 };
            var layouts = new[]
            {
                Array.Empty<int[]>(), new[] { triangle }, new[] { new[] { 0, 1, 2, 3 } },
                Triangles, new[] { triangle, new[] { 2, 0, 3 } },
                new[] { triangle, triangle }, new[] { triangle, triangle, triangle },
                new[] { triangle, triangle, triangle, triangle },
                new[] { triangle, triangle, triangle, new[] { 0, 2, 3 } },
                new[] { triangle, new[] { 0, 2, 7 } },
                new[] { triangle, new[] { 4, 5, 6 } },
                new[] { new[] { 0 } }, new[] { new[] { 0 }, new[] { 0 }, new[] { 0 } },
                new[] { new[] { 0, 0 } }, new[] { new[] { 0, 1 } },
                new[] { new[] { 0, 0, 1, 2 } }
            };
            for (var i = 0; i < layouts.Length; i++)
            foreach (var reverseFaces in new[] { false, true })
            foreach (var reverseWinding in new[] { false, true })
            {
                var faces = (reverseFaces ? layouts[i].Reverse() : layouts[i])
                    .Select(f => reverseWinding ? f.Reverse().ToArray() : f).ToArray();
                CheckFixture(positions, faces, $"layout={i}, reverseFaces={reverseFaces}, reverseWinding={reverseWinding}");
            }
            var random = new Random(6319);
            for (var i = 0; i < 32; i++)
            {
                var faces = Enumerable.Range(0, random.Next(9)).Select(_ =>
                    Enumerable.Range(0, 3).Select(__ => random.Next(positions.Length)).ToArray()).ToArray();
                CheckFixture(positions, faces, $"seed=6319, fixture={i}, faces={string.Join(";", faces.Select(f => string.Join(",", f)))}");
            }
            var built = Mesh(Square, Triangles);
            built.BuildTopology();
            var unchanged = Snapshot(built);
            CheckEdges(built.GetOutlines().ToArray(), Oracle(Square, Triangles), "existing topology");
            unchanged();
        }

        [Test]
        public void FloatingPointEndpointsRetainExactCancellationAndDirectedNanCoalescing()
        {
            var nan1 = BitConverter.Int64BitsToDouble(0x7ff8000000000001L);
            var nan2 = BitConverter.Int64BitsToDouble(0x7ff8000000000042L);
            var negativeZero = BitConverter.Int64BitsToDouble(long.MinValue);
            var points = new[]
            {
                new V3d(nan1, 0, 0), new V3d(0, nan1, 0), new V3d(0, 0, nan1),
                new V3d(double.PositiveInfinity, 0, 0), new V3d(double.NegativeInfinity, 0, 0),
                new V3d(negativeZero, 0, 0), new V3d(double.Epsilon, 0, 0),
                new V3d(double.MaxValue, 0, 0)
            };
            foreach (var p in points)
            {
                var positions = new[] { p, V3d.IOO, V3d.OIO, p };
                var triangle = new[] { 0, 1, 2 };
                foreach (var faces in new[]
                {
                    new[] { triangle }, new[] { triangle, triangle }, new[] { triangle, triangle, triangle },
                    new[] { triangle, new[] { 0, 2, 1 } }, new[] { triangle, new[] { 3, 1, 2 } },
                    new[] { new[] { 0 } }, new[] { new[] { 0 }, new[] { 0 } }
                }) CheckFixture(positions, faces, $"point={p}, faces={string.Join(";", faces.Select(f => string.Join(",", f)))}");
            }
            CheckFixture(new[] { new V3d(nan1, negativeZero, 0), V3d.IOO, V3d.OIO, new V3d(nan2, 0, 0) },
                new[] { new[] { 0, 1, 2 }, new[] { 3, 1, 2 } }, "NaN payloads and signed zeros coalesce without cancellation");
            CheckFixture(new[] { V3d.Zero, V3d.IOO, V3d.OIO, new V3d(Math.BitIncrement(1), 0, 0) },
                Triangles, "one-ULP difference is not equal");
            CheckFixture(new[] { V3d.Zero, V3d.IOO, V3d.OIO, new V3d(negativeZero, 0, 0) },
                new[] { new[] { 0, 1, 2 }, new[] { 3, 1, 2 } }, "signed zero cancels");
        }

        private static PolyMesh Mesh(V3d[] positions, int[][] faces)
        {
            var first = new int[faces.Length + 1];
            for (var i = 0; i < faces.Length; i++) first[i + 1] = first[i] + faces[i].Length;
            return new PolyMesh { PositionArray = positions, FirstIndexArray = first, VertexIndexArray = faces.SelectMany(f => f).ToArray() };
        }

        private static void CheckFixture(V3d[] positions, int[][] faces, string context)
        {
            var mesh = Mesh(positions, faces);
            var unchanged = Snapshot(mesh);
            var expected = Oracle(positions, faces);
            var result = mesh.GetOutlines();
            CheckEdges(result.ToArray(), expected, context);
            CheckEdges(result.ToArray(), expected, context);
            CheckEdges(mesh.Polygons.GetOutlines().ToArray(), expected, context);
            unchanged();
        }

        private static List<Line3d> Oracle(V3d[] positions, int[][] faces)
        {
            var lines = faces.SelectMany(f => Enumerable.Range(0, f.Length)
                .Select(i => new Line3d(positions[f[i]], positions[f[(i + 1) % f.Length]]))).ToArray();
            var result = new List<Line3d>();
            for (var i = 0; i < lines.Length; i++)
            {
                var a = lines[i];
                var repeated = false;
                for (var j = 0; j < lines.Length; j++)
                {
                    if (i == j) continue;
                    var b = lines[j];
                    if ((EndpointEqual(a.P0, b.P0) && EndpointEqual(a.P1, b.P1)) ||
                        (EndpointEqual(a.P0, b.P1) && EndpointEqual(a.P1, b.P0))) repeated = true;
                }
                if (!repeated && !result.Any(b => DirectedEqual(a, b))) result.Add(a);
            }
            return result;
        }

        private static bool EndpointEqual(V3d a, V3d b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z;
        private static bool DirectedEqual(Line3d a, Line3d b) => ValueEqual(a.P0, b.P0) && ValueEqual(a.P1, b.P1);
        private static bool ValueEqual(V3d a, V3d b) => a.X.Equals(b.X) && a.Y.Equals(b.Y) && a.Z.Equals(b.Z);
        private static long[] Bits(V3d p) => new[] { BitConverter.DoubleToInt64Bits(p.X), BitConverter.DoubleToInt64Bits(p.Y), BitConverter.DoubleToInt64Bits(p.Z) };

        private static void CheckEdges(Line3d[] actual, List<Line3d> expected, string context)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Count), context);
            foreach (var edge in expected)
            {
                var matches = actual.Where(a => DirectedEqual(a, edge)).ToArray();
                Assert.That(matches.Length, Is.EqualTo(1), $"{context}, edge={edge}");
                Assert.That(Bits(matches[0].P0), Is.EqualTo(Bits(edge.P0)), context);
                Assert.That(Bits(matches[0].P1), Is.EqualTo(Bits(edge.P1)), context);
            }
        }

        private static Action Snapshot(PolyMesh mesh)
        {
            var positions = mesh.PositionArray;
            var first = mesh.FirstIndexArray;
            var indices = mesh.VertexIndexArray;
            var pointBits = positions.Select(Bits).ToArray();
            var firstCopy = (int[])first.Clone();
            var indexCopy = (int[])indices.Clone();
            var topology = mesh.HasTopology;
            var edgeCount = mesh.EdgeCount;
            var counts = new[] { mesh.VertexCount, mesh.FaceCount, mesh.VertexIndexCount };
            var attributes = new object[] { mesh.VertexAttributes, mesh.FaceAttributes, mesh.FaceVertexAttributes, mesh.InstanceAttributes };
            return () =>
            {
                Assert.That(mesh.PositionArray, Is.SameAs(positions));
                Assert.That(mesh.FirstIndexArray, Is.SameAs(first));
                Assert.That(mesh.VertexIndexArray, Is.SameAs(indices));
                Assert.That(mesh.PositionArray.Select(Bits), Is.EqualTo(pointBits));
                Assert.That(mesh.FirstIndexArray, Is.EqualTo(firstCopy));
                Assert.That(mesh.VertexIndexArray, Is.EqualTo(indexCopy));
                Assert.That(mesh.HasTopology, Is.EqualTo(topology));
                Assert.That(mesh.EdgeCount, Is.EqualTo(edgeCount));
                Assert.That(new[] { mesh.VertexCount, mesh.FaceCount, mesh.VertexIndexCount }, Is.EqualTo(counts));
                var current = new object[] { mesh.VertexAttributes, mesh.FaceAttributes, mesh.FaceVertexAttributes, mesh.InstanceAttributes };
                for (var i = 0; i < attributes.Length; i++) Assert.That(current[i], Is.SameAs(attributes[i]));
            };
        }

        private sealed class SingleUsePolygons : IEnumerable<PolyMesh.Polygon>
        {
            private readonly PolyMesh.Polygon[] polygons;
            private readonly bool throwOnReopen;
            private readonly int throwAfter;
            private int cursor;
            public int Acquisitions { get; private set; }
            public int Disposals { get; private set; }
            public int Reads { get; private set; }
            public InvalidOperationException Failure { get; } = new InvalidOperationException("polygon source failure");
            public SingleUsePolygons(PolyMesh.Polygon[] polygons, bool throwOnReopen, int throwAfter = -1)
            {
                this.polygons = polygons;
                this.throwOnReopen = throwOnReopen;
                this.throwAfter = throwAfter;
            }
            public IEnumerator<PolyMesh.Polygon> GetEnumerator()
            {
                Acquisitions++;
                if (throwOnReopen && Acquisitions > 1) throw new InvalidOperationException("source reopened");
                return Read();
            }
            private IEnumerator<PolyMesh.Polygon> Read()
            {
                try
                {
                    while (cursor < polygons.Length)
                    {
                        if (Reads == throwAfter) throw Failure;
                        Reads++;
                        yield return polygons[cursor++];
                    }
                }
                finally { Disposals++; }
            }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
