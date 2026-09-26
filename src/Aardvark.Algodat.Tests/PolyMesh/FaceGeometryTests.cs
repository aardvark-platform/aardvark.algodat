using Aardvark.Base;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class FaceGeometryTests
    {
        private static readonly V2d[] U =
        {
            new(0, 0), new(5, 0), new(10, 0), new(10, 10), new(9, 10),
            new(9, 1), new(2, 1), new(2, 10), new(0, 10)
        };

        private static IEnumerable<(string Name, V2d[] Points)> Shapes()
        {
            yield return ("u-midpoint", U);
            yield return ("u-without-midpoint", U.Where((_, i) => i != 1).ToArray());
            yield return ("u-collinear-prefix", new[] { new V2d(0, 0), new V2d(1, 0), new V2d(2, 0), new V2d(3, 0) }.Concat(U.Skip(1)).ToArray());
            yield return ("u-repeated-prefix", new[] { U[0], U[0], U[0], U[1], U[1] }.Concat(U.Skip(2)).ToArray());
            yield return ("triangle", new[] { new V2d(0, 0), new V2d(4, 0), new V2d(0, 3) });
            yield return ("quad", new[] { new V2d(0, 0), new V2d(4, 0), new V2d(4, 3), new V2d(0, 3) });
            yield return ("convex", new[] { new V2d(0, 0), new V2d(4, 0), new V2d(6, 2), new V2d(3, 5), new V2d(-1, 2) });
            yield return ("last-fan-only", new[] { new V2d(0, 0), new V2d(0, 0), new V2d(0, 0), new V2d(4, 0), new V2d(0, 3) });
        }

        public static IEnumerable<TestCaseData> CyclicCases()
        {
            foreach (var (name, points) in Shapes())
            foreach (var reversed in new[] { false, true })
            for (var start = 0; start < points.Length; start++)
                yield return new TestCaseData(Order(points, start, reversed)).SetName($"FaceGeometry_{name}_{start}_{reversed}");
        }

        [Test]
        public void CollinearMidpointPreservesRectangleSubtractionCentroid()
        {
            var mesh = Mesh(U);
            mesh.AddFaceNormalsAreasCentroids(false);
            // A 10x10 rectangle minus a 7x9 notch centred at (5.5, 5.5).
            AssertFace(mesh, 0, 100 - 7 * 9, new V3d(307.0 / 74, 307.0 / 74, 0), V3d.ZAxis);
        }

        [TestCaseSource(nameof(CyclicCases))]
        public void CyclicStartsAndWindingMatchIndependentMoments(V2d[] points)
        {
            var expected = Moments(points);
            var mesh = Mesh(points);
            mesh.AddFaceNormalsAreasCentroids();
            AssertFace(mesh, 0, expected.Area, expected.Centroid, expected.Normal);
        }

        public static IEnumerable<TestCaseData> RigidCases()
        {
            for (var transform = 0; transform < 5; transform++)
            foreach (var reversed in new[] { false, true })
            for (var start = 0; start < U.Length; start++)
                yield return new TestCaseData(transform, start, reversed);
        }

        [TestCaseSource(nameof(RigidCases))]
        public void RigidTransformsPreserveAreaAndTransformCentroidAndNormal(int transform, int start, bool reversed)
        {
            var points = Order(U, start, reversed);
            var expected = Moments(points);
            var mesh = Mesh(points);
            mesh.PositionArray = mesh.PositionArray.Select(p => Rotate(p, transform) + Translation(transform)).ToArray();
            mesh.AddFaceNormalsAreasCentroids(false);
            AssertFace(mesh, 0, expected.Area, Rotate(expected.Centroid, transform) + Translation(transform), Rotate(expected.Normal, transform));
        }

        [TestCase(0.0009765625)]
        [TestCase(0.125)]
        [TestCase(1024.0)]
        public void ScalingPreservesSignedMoments(double scale)
        {
            var mesh = Mesh(U.Select(p => p * scale).ToArray());
            mesh.AddFaceNormalsAreasCentroids(false);
            AssertFace(mesh, 0, 37 * scale * scale, new V3d(307.0 / 74 * scale, 307.0 / 74 * scale, 0), V3d.ZAxis);
        }

        [Test]
        public void SeededNotchesMatchRectangleSubtraction([Range(0, 31)] int seed)
        {
            var random = new Random(seed);
            var width = random.Next(8, 65); var height = random.Next(4, 65);
            var left = random.Next(1, width / 2); var right = random.Next(width / 2 + 1, width);
            var bottom = random.Next(1, height);
            var removed = (right - left) * (height - bottom);
            var area = width * height - removed;
            var centroid = new V3d(
                (width * height * (width / 2.0) - removed * ((left + right) / 2.0)) / area,
                (width * height * (height / 2.0) - removed * ((bottom + height) / 2.0)) / area, 0);
            var points = Enumerable.Repeat(V2d.Zero, 1 + seed % 4).Concat(new[]
            {
                new V2d(width / 4.0, 0), new V2d(width / 2.0, 0), new V2d(width, 0), new V2d(width, height),
                new V2d(right, height), new V2d(right, bottom), new V2d(left, bottom), new V2d(left, height), new V2d(0, height)
            }).ToArray();
            foreach (var reverse in new[] { false, true })
            for (var start = 0; start < points.Length; start++)
            {
                var mesh = Mesh(Order(points, start, reverse));
                mesh.AddFaceNormalsAreasCentroids(false);
                AssertFace(mesh, 0, area, centroid, reverse ? -V3d.ZAxis : V3d.ZAxis);
            }
        }

        public static IEnumerable<TestCaseData> DegenerateCases()
        {
            yield return new TestCaseData(new[] { new V3d(7, 8, 9), new V3d(7, 8, 9), new V3d(7, 8, 9) });
            yield return new TestCaseData(new[] { V3d.Zero, V3d.XAxis, 2 * V3d.XAxis });
            yield return new TestCaseData(Enumerable.Range(0, 64).Select(i => new V3d(i, 2 * i, -i)).ToArray());
            yield return new TestCaseData(new[] { V3d.Zero, V3d.XAxis, V3d.YAxis, V3d.XAxis }); // Retraced, cancelling fan.
        }

        [TestCaseSource(nameof(DegenerateCases))]
        public void DegenerateFacesHaveZeroNormalAreaAndCentroid(V3d[] points)
        {
            var mesh = Mesh(points.Select(p => p.XY).ToArray());
            mesh.PositionArray = points;
            mesh.AddFaceNormalsAreasCentroids(false);
            AssertFace(mesh, 0, 0, V3d.Zero, V3d.Zero);
        }

        [Test]
        public void MixedValenceFacesDoNotShareOrientationStateOrChangeInputs()
        {
            var shapes = Shapes().Select(x => x.Points).Concat(new[]
            {
                new[] { V2d.Zero, V2d.Zero, V2d.Zero },
                Enumerable.Range(0, 7).Select(i => new V2d(i, i)).ToArray()
            }).ToArray();
            var faces = shapes.Concat(shapes.Reverse().Select(x => x.Reverse().ToArray())).ToArray();
            var mesh = Mesh(faces);
            var positions = mesh.PositionArray; var indices = mesh.VertexIndexArray; var first = mesh.FirstIndexArray;
            var positionCopy = (V3d[])positions.Clone(); var indexCopy = (int[])indices.Clone(); var firstCopy = (int[])first.Clone();
            var faceTag = Enumerable.Range(0, mesh.FaceCount).ToArray();
            var cornerTag = new int[indices.Length]; var vertexTag = new double[positions.Length];
            mesh.FaceAttributes["face-tag"] = faceTag; mesh.FaceVertexAttributes["corner-tag"] = cornerTag;
            mesh.VertexAttributes["vertex-tag"] = vertexTag;
            for (var repeat = 0; repeat < 3; repeat++)
            {
                mesh.AddFaceNormalsAreasCentroids(false);
                for (var i = 0; i < faces.Length; i++)
                {
                    var expected = Moments(faces[i]);
                    AssertFace(mesh, i, expected.Area, expected.Centroid, expected.Normal);
                }
            }
            Assert.That(mesh.PositionArray, Is.SameAs(positions).And.EqualTo(positionCopy));
            Assert.That(mesh.VertexIndexArray, Is.SameAs(indices).And.EqualTo(indexCopy));
            Assert.That(mesh.FirstIndexArray, Is.SameAs(first).And.EqualTo(firstCopy));
            Assert.That(mesh.FaceAttributes["face-tag"], Is.SameAs(faceTag));
            Assert.That(mesh.FaceVertexAttributes["corner-tag"], Is.SameAs(cornerTag));
            Assert.That(mesh.VertexAttributes["vertex-tag"], Is.SameAs(vertexTag));
            Assert.That(mesh.HasTopology, Is.False);
        }

        [Test]
        public void NoncontiguousSharedIndicesAndUnusedPrefixAreRespected()
        {
            var points = new[] { U[0], U[0], U[1], U[1] }.Concat(U.Skip(2)).ToArray();
            var unique = points.Distinct().Reverse().ToArray();
            var indices = points.Select(p => Array.IndexOf(unique, p)).ToArray();
            var mesh = new PolyMesh
            {
                PositionArray = unique.Select(p => new V3d(p, 0)).ToArray(),
                FirstIndexArray = new[] { 3, 3 + indices.Length, 3 + 2 * indices.Length },
                VertexIndexArray = new[] { -1, -1, -1 }.Concat(indices).Concat(indices.Reverse()).ToArray()
            };
            mesh.AddFaceNormalsAreasCentroids(false);
            AssertFace(mesh, 0, 37, new V3d(307.0 / 74, 307.0 / 74, 0), V3d.ZAxis);
            AssertFace(mesh, 1, 37, new V3d(307.0 / 74, 307.0 / 74, 0), -V3d.ZAxis);
        }

        [Test]
        public void RecalculationReplacesOutputsWithoutChangingTopology()
        {
            var mesh = Mesh(Shapes().First(x => x.Name == "quad").Points);
            mesh.BuildTopology();
            var edgeCount = mesh.EdgeCount;
            mesh.AddFaceNormalsAreasCentroids(false);
            var normals = mesh.FaceAttributes[PolyMesh.Property.Normals];
            var areas = mesh.FaceAttributes[PolyMesh.Property.Areas];
            var centroids = mesh.FaceAttributes[PolyMesh.Property.Centroids];
            mesh.PositionArray = mesh.PositionArray.Select(p => p * 2 + V3d.ZAxis).ToArray();
            mesh.AddFaceNormalsAreasCentroids(false);
            AssertFace(mesh, 0, 48, new V3d(4, 3, 1), V3d.ZAxis);
            Assert.That(mesh.FaceAttributes[PolyMesh.Property.Normals], Is.Not.SameAs(normals));
            Assert.That(mesh.FaceAttributes[PolyMesh.Property.Areas], Is.Not.SameAs(areas));
            Assert.That(mesh.FaceAttributes[PolyMesh.Property.Centroids], Is.Not.SameAs(centroids));
            Assert.That(mesh.HasTopology, Is.True);
            Assert.That(mesh.EdgeCount, Is.EqualTo(edgeCount));
        }

        [TestCase(false)]
        [TestCase(true)]
        [NonParallelizable]
        public void WarningFlagPreservesZeroAndNaNDiagnostics(bool warn)
        {
            var mesh = Mesh(U, new[] { V2d.Zero, V2d.Zero, V2d.Zero }, new[] { new V2d(double.NaN, 0), V2d.XAxis, V2d.YAxis });
            var log = new StringBuilder(); var target = Report.RootTarget;
            try
            {
                Report.RootTarget = new TextLogTarget((_, _, _, text) => log.Append(text));
                mesh.AddFaceNormalsAreasCentroids(warn);
            }
            finally { Report.RootTarget = target; }
            AssertFace(mesh, 0, 37, new V3d(307.0 / 74, 307.0 / 74, 0), V3d.ZAxis);
            AssertFace(mesh, 1, 0, V3d.Zero, V3d.Zero);
            AssertFace(mesh, 2, 0, V3d.Zero, V3d.Zero);
            if (warn)
            {
                Assert.That(log.ToString(), Does.Contain("1 zero normal vectors"));
                Assert.That(log.ToString(), Does.Contain("1 nan normal vectors"));
            }
            else Assert.That(log.Length, Is.Zero);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(256)]
        [TestCase(4096)]
        public void WarmedCallsAllocateOnlyOutputArrays(int count)
        {
            var shape = Shapes().First(x => x.Name == "u-repeated-prefix").Points;
            var mesh = Mesh(Enumerable.Repeat(shape, count).ToArray());
            for (var i = 0; i < 16; i++) mesh.AddFaceNormalsAreasCentroids(false);
            var start = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 8; i++) mesh.AddFaceNormalsAreasCentroids(false);
            var actual = GC.GetAllocatedBytesForCurrentThread() - start;
            start = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 8; i++)
            {
                GC.KeepAlive(new V3d[count]); GC.KeepAlive(new double[count]); GC.KeepAlive(new V3d[count]);
            }
            var expected = GC.GetAllocatedBytesForCurrentThread() - start;
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(mesh.FaceAttributes[PolyMesh.Property.Normals].Length, Is.EqualTo(count));
            Assert.That(mesh.FaceAttributes[PolyMesh.Property.Areas].Length, Is.EqualTo(count));
            Assert.That(mesh.FaceAttributes[PolyMesh.Property.Centroids].Length, Is.EqualTo(count));
        }

        private static V2d[] Order(V2d[] points, int start, bool reverse) =>
            Enumerable.Range(0, points.Length).Select(i => points[(start + (reverse ? points.Length - i : i)) % points.Length]).ToArray();

        private static PolyMesh Mesh(params V2d[][] faces)
        {
            var positions = faces.SelectMany(face => face.Select(p => new V3d(p, 0))).ToArray();
            var first = new int[faces.Length + 1];
            for (var i = 0; i < faces.Length; i++) first[i + 1] = first[i] + faces[i].Length;
            return new PolyMesh { PositionArray = positions, FirstIndexArray = first, VertexIndexArray = Enumerable.Range(0, positions.Length).ToArray() };
        }

        // Edge moments (shoelace/Green's theorem), independent of the production fan decomposition.
        private static (double Area, V3d Centroid, V3d Normal) Moments(V2d[] points)
        {
            double twiceArea = 0, x = 0, y = 0;
            for (var i = 0; i < points.Length; i++)
            {
                var a = points[i]; var b = points[(i + 1) % points.Length];
                var cross = a.X * b.Y - b.X * a.Y;
                twiceArea += cross; x += (a.X + b.X) * cross; y += (a.Y + b.Y) * cross;
            }
            return twiceArea == 0 ? (0, V3d.Zero, V3d.Zero) :
                (Math.Abs(twiceArea) / 2, new V3d(x / (3 * twiceArea), y / (3 * twiceArea), 0), Math.Sign(twiceArea) * V3d.ZAxis);
        }

        private static V3d Translation(int transform) => transform == 4 ? V3d.Zero : new V3d(16, -32, 8);
        private static V3d Rotate(V3d p, int transform) => transform switch
        {
            0 => p,
            1 => new V3d(p.Z, p.X, p.Y),
            2 => new V3d(-p.X, p.Y, -p.Z),
            3 => new V3d(p.X, Math.Cos(0.7) * p.Y - Math.Sin(0.7) * p.Z, Math.Sin(0.7) * p.Y + Math.Cos(0.7) * p.Z),
            _ => p.X * new V3d(1 / Math.Sqrt(2), 1 / Math.Sqrt(2), 0) +
                 p.Y * new V3d(-1 / Math.Sqrt(6), 1 / Math.Sqrt(6), 2 / Math.Sqrt(6)) +
                 p.Z * new V3d(1 / Math.Sqrt(3), -1 / Math.Sqrt(3), 1 / Math.Sqrt(3))
        };

        private static void AssertFace(PolyMesh mesh, int face, double area, V3d centroid, V3d normal)
        {
            var actualNormal = ((V3d[])mesh.FaceAttributes[PolyMesh.Property.Normals])[face];
            var actualArea = ((double[])mesh.FaceAttributes[PolyMesh.Property.Areas])[face];
            var actualCentroid = ((V3d[])mesh.FaceAttributes[PolyMesh.Property.Centroids])[face];
            Assert.That(actualArea, Is.EqualTo(area).Within(1e-10 * Math.Max(1, area)), $"face {face} area");
            Assert.That(actualArea, Is.GreaterThanOrEqualTo(0));
            Assert.That((actualNormal - normal).Length, Is.LessThan(1e-10), $"face {face} normal");
            Assert.That((actualCentroid - centroid).Length, Is.LessThan(1e-10 * Math.Max(1, centroid.Length)), $"face {face} centroid");
        }
    }
}
