using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Aardvark.Base;
using NUnit.Framework;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class ObjectHitInfoEdgeTests
    {
        private static V3d[] Polygon(int count) => count == 3 ? new[] { V3d.Zero, new V3d(2, 0, 0), new V3d(0, 2, 0) }
            : count == 4 ? new[] { V3d.Zero, new V3d(2, 0, 0), new V3d(2, 2, 0), new V3d(0, 2, 0) }
            : Enumerable.Range(0, count).Select(i => new V3d(2 * Math.Cos(2 * Math.PI * i / count), 2 * Math.Sin(2 * Math.PI * i / count), 0)).ToArray();

        private static (PolyMesh Mesh, int Face) Fixture(V3d[] points, bool prefix, bool supplied)
        {
            var positions = new List<V3d>(); var indices = new List<int>(); var first = new List<int> { 0 };
            void Add(V3d[] ps)
            {
                foreach (var p in ps) { indices.Add(positions.Count); positions.Add(p); }
                first.Add(indices.Count);
            }
            if (prefix) Add(new[] { new V3d(-32, -32, -4), new V3d(-30, -32, -4), new V3d(-32, -30, -4) });
            var face = first.Count - 1; Add(points);
            for (var i = 0; i < 16; i++)
            {
                var p = new V3d(64 + 8 * i, 64, -8 + i % 3);
                Add(new[] { p, p + new V3d(2, 0, 0), p + new V3d(0, 2, 0) });
            }
            positions.Add(new V3d(12345, 67890, -12345)); // Unreferenced position must remain untouched.
            positions.Reverse();
            var mesh = new PolyMesh { PositionArray = positions.ToArray(), FirstIndexArray = first.ToArray(),
                VertexIndexArray = indices.Select(i => positions.Count - 1 - i).ToArray() };
            if (supplied) mesh.FaceAttributes[PolyMesh.Property.Normals] = Enumerable.Range(0, mesh.FaceCount).Select(i => new V3d(-7 - i, 2 + i, .5)).ToArray();
            return (mesh, face);
        }

        private static V3d FanNormal(V3d[] points) => (points[1] - points[0]).Cross(points[2] - points[0]).Normalized;
        private static long[] Bits(V3d value) => new[] { BitConverter.DoubleToInt64Bits(value.X), BitConverter.DoubleToInt64Bits(value.Y), BitConverter.DoubleToInt64Bits(value.Z) };
        private static byte[] Bytes<T>(T[] values) where T : unmanaged => MemoryMarshal.AsBytes(values.AsSpan()).ToArray();
        private static void Finite(V3d p, string context) => Assert.That(double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z), Is.True, context);

        private static Action SourceCheck(PolyMesh mesh, string context)
        {
            var positions = mesh.PositionArray; var first = mesh.FirstIndexArray; var via = mesh.VertexIndexArray;
            var pb = Bytes(positions); var fb = Bytes(first); var vb = Bytes(via);
            var normals = mesh.FaceAttributeArray<V3d>(PolyMesh.Property.Normals); var nb = normals == null ? null : Bytes(normals);
            var counts = (mesh.VertexCount, mesh.FaceCount, mesh.VertexIndexCount);
            return () => {
                Assert.That((mesh.VertexCount, mesh.FaceCount, mesh.VertexIndexCount), Is.EqualTo(counts), context + ": source counts");
                Assert.That(mesh.PositionArray, Is.SameAs(positions), context + ": source positions identity");
                Assert.That(mesh.FirstIndexArray, Is.SameAs(first), context + ": source first identity");
                Assert.That(mesh.VertexIndexArray, Is.SameAs(via), context + ": source indices identity");
                Assert.That(Bytes(positions), Is.EqualTo(pb), context + ": source position bits");
                Assert.That(Bytes(first), Is.EqualTo(fb), context + ": source first bits");
                Assert.That(Bytes(via), Is.EqualTo(vb), context + ": source index bits");
                Assert.That(mesh.FaceAttributeArray<V3d>(PolyMesh.Property.Normals), Is.SameAs(normals), context + ": source normals identity");
                if (normals != null) Assert.That(Bytes(normals), Is.EqualTo(nb), context + ": source normal bits");
            };
        }

        private static void Check(ObjectHitInfo info, V3d[] expectedPoints, V3d expectedNormal, string context)
        {
            Assert.That(info.Points, Is.EqualTo(expectedPoints), context + ": stored point order");
            Assert.That(info.Edges.Length, Is.EqualTo(expectedPoints.Length), context + ": edge count");
            var sum = V3d.Zero;
            for (var i = 0; i < expectedPoints.Length; i++)
            {
                var from = expectedPoints[i]; var to = expectedPoints[(i + 1) % expectedPoints.Length];
                var edge = new V3d(to.X - from.X, to.Y - from.Y, to.Z - from.Z);
                Finite(info.Points[i], context + $": finite point{i}"); Finite(info.Edges[i], context + $": finite edge{i}");
                Assert.That(info.Edges[i], Is.EqualTo(edge), context + $": consecutive edge{i}");
                sum += info.Edges[i];
            }
            Assert.That(sum.Length, Is.LessThanOrEqualTo(1e-10), context + ": closed perimeter");
            Finite(info.Normal, context + ": finite normal");
            Assert.That(Bits(info.Normal), Is.EqualTo(Bits(expectedNormal)), context + ": unchanged normal bits");
        }

        [Test]
        public void DirectAndSubdividedTreeHitsExposeOrderedClosedPerimeters()
        {
            Assert.Multiple(() => {
                foreach (var count in new[] { 3, 4, 8, 32 })
                foreach (var reverse in new[] { false, true })
                foreach (var tilted in new[] { false, true })
                foreach (var prefix in new[] { false, true })
                foreach (var supplied in new[] { false, true })
                {
                    var points = Polygon(count);
                    if (reverse) points = points.Reverse().ToArray();
                    if (tilted) points = points.Select(p => new V3d(8 + p.X + .25 * p.Y, -4 + 2 * p.Y, 2 + .5 * p.X - .25 * p.Y)).ToArray();
                    var (mesh, face) = Fixture(points, prefix, supplied);
                    var context = $"count={count}, reverse={reverse}, tilted={tilted}, prefix={prefix}, supplied={supplied}";
                    var unchanged = SourceCheck(mesh, context);
                    var expectedNormal = supplied ? mesh.FaceAttributeArray<V3d>(PolyMesh.Property.Normals)[face] : FanNormal(points);
                    var direction = FanNormal(points);
                    var query = .45 * points[0] + .25 * points[1] + .3 * points[2];
                    var ray = new FastRay3d(query + 3 * direction, -direction);
                    var tree = new KdIntersectionTree(mesh, KdIntersectionTree.BuildFlags.Raytracing | KdIntersectionTree.BuildFlags.NoMultithreading);
                    Assert.That(tree.Tree.Leafs().Count(), Is.GreaterThan(1), context + ": genuine subdivision");
                    foreach (var viaTree in new[] { false, true })
                    {
                        var label = context + $", tree={viaTree}"; var hit = ObjectRayHit.MaxRange; var tag = new object(); hit.Tag = tag;
                        var found = viaTree ? tree.Intersect(ray, 0, 10, ref hit)
                            : mesh.ObjectsIntersectRay(new[] { int.MaxValue, face, -1 }, 1, 1, ray, null, null, 0, 10, ref hit);
                        Assert.That(found, Is.True, label + ": hit");
                        Assert.That(hit.SetObject.Set, Is.SameAs(mesh), label + ": mesh identity");
                        Assert.That(hit.SetObject.Index, Is.EqualTo(face), label + ": face identity");
                        Assert.That(hit.RayHit.Part, Is.Zero, label + ": first fan triangle");
                        Assert.That(double.IsFinite(hit.RayHit.T), Is.True, label + ": finite ray parameter");
                        Assert.That(hit.RayHit.T, Is.EqualTo(3).Within(1e-10), label + ": unchanged ray parameter");
                        Assert.That(hit.RayHit.Point.ApproximateEquals(query, 1e-10), Is.True, label + ": unchanged hit point");
                        Assert.That(hit.Tag, Is.SameAs(tag), label + ": tag");
                        var before = hit; var info = hit.GetIntersectionRayHitInfo();
                        Check(info, points, expectedNormal, label);
                        Assert.That(hit, Is.EqualTo(before), label + ": hit information does not modify hit");
                        Array.Clear(info.Points, 0, info.Points.Length); Array.Clear(info.Edges, 0, info.Edges.Length);
                        unchanged();
                    }
                }
            });
        }

        [Test]
        public void SuppliedAndFallbackNormalsRetainHistoricalArithmetic()
        {
            var random = new Random(17341); var roundingDifferences = 0;
            for (var i = 0; i < 64; i++)
            {
                var points = Enumerable.Range(0, 3).Select(_ => new V3d(random.NextDouble() * 1000, random.NextDouble() * 1000, random.NextDouble() * 1000)).ToArray();
                var expected = FanNormal(points);
                var perimeterNormal = (points[1] - points[0]).Cross(points[2] - points[1]).Normalized;
                if (!Bits(expected).SequenceEqual(Bits(perimeterNormal))) roundingDifferences++;
                var (mesh, face) = Fixture(points, true, false); var unchanged = SourceCheck(mesh, $"fan case{i}");
                var hit = ObjectRayHit.MaxRange; hit.Set(mesh, face); var info = hit.GetIntersectionRayHitInfo();
                Finite(info.Normal, $"finite fan case{i}");
                Assert.That(Bits(info.Normal), Is.EqualTo(Bits(expected)), $"fan arithmetic case{i}");
                Assert.That(info.Points, Is.EqualTo(points), $"fan points case{i}"); unchanged();
            }
            Assert.That(roundingDifferences, Is.GreaterThan(0), "fixture distinguishes perimeter-cross rounding from original fan arithmetic");
            foreach (var count in new[] { 1, 2, 3, 4 })
            foreach (var supplied in new[] { false, true })
            {
                var points = Enumerable.Range(0, count).Select(i => new V3d(i, 0, 0)).ToArray();
                var (mesh, face) = Fixture(points, true, supplied);
                var hit = ObjectRayHit.MaxRange; hit.Set(mesh, face);
                var info = new ObjectHitInfo { Normal = new V3d(-7, .5, -0.0) };
                var expected = supplied ? mesh.FaceAttributeArray<V3d>(PolyMesh.Property.Normals)[face] : count > 2 ? FanNormal(points) : info.Normal;
                mesh.ObjectHitInfo(hit, ref info);
                Assert.That(Bits(info.Normal), Is.EqualTo(Bits(expected)), $"legacy degenerate/supplied count={count}, supplied={supplied}");
                Assert.That(info.Points, Is.EqualTo(points), $"degenerate points count={count}");
            }
        }

        [Test]
        public void HitInfoRetainsExactlyTwoOutputArrayAllocations()
        {
            foreach (var count in new[] { 3, 4, 32, 256 })
            foreach (var supplied in new[] { false, true })
            {
                var (mesh, face) = Fixture(Polygon(count), false, supplied); var hit = ObjectRayHit.MaxRange; hit.Set(mesh, face);
                ObjectHitInfo info = default;
                for (var i = 0; i < 64; i++) mesh.ObjectHitInfo(hit, ref info);
                var before = GC.GetAllocatedBytesForCurrentThread();
                for (var i = 0; i < 128; i++) { GC.KeepAlive(new V3d[count]); GC.KeepAlive(new V3d[count]); }
                var expected = GC.GetAllocatedBytesForCurrentThread() - before;
                var oldPoints = info.Points; var oldEdges = info.Edges; var sink = 0.0;
                before = GC.GetAllocatedBytesForCurrentThread();
                for (var i = 0; i < 128; i++) { mesh.ObjectHitInfo(hit, ref info); sink += info.Points[0].X + info.Edges[count - 1].Y; }
                var actual = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.That(double.IsFinite(sink), Is.True, $"allocation sink count={count}");
                Assert.That(actual, Is.EqualTo(expected), $"two arrays only count={count}, supplied={supplied}");
                Assert.That(info.Points, Is.Not.SameAs(oldPoints)); Assert.That(info.Edges, Is.Not.SameAs(oldEdges));
            }
        }
    }
}
