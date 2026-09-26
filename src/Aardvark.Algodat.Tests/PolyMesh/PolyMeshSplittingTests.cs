using Aardvark.Base;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class PolyMeshSplittingTests
    {
        public enum Layout { None, Direct, Indexed }

        private static readonly Symbol SourceFace = "SplitSourceFace";
        private static readonly Symbol FaceData = "SplitFaceData";
        private static readonly Symbol VertexData = "SplitVertexData";
        private static readonly Symbol CornerData = "SplitCornerData";
        private static readonly Symbol InstanceData = "SplitInstanceData";
        private static readonly Plane3d Plane = new Plane3d(V3d.IOO, 0);
        private const double Epsilon = 1e-9;

        [Test]
        public void CrossingQuadHonorsOptions([Values] SplitterOptions options)
        {
            var source = Quads((-1.0, 1.0));
            CheckSplit(source, Plane, options);
        }

        [Test]
        public void IndexedFaceUsesOriginalAttributeIndex()
        {
            var source = Quads((-1.0, 1.0));
            source.FaceAttributes[FaceData] = new[] { 1 };
            source.FaceAttributes[-FaceData] = new[] { 11, 22 };
            var (negative, positive) = CheckSplit(source, Plane, SplitterOptions.NegativeAndPositive);
            foreach (var part in new[] { negative, positive })
            {
                Assert.That(part.FaceAttributes[FaceData], Is.EqualTo(new[] { 0 }));
                Assert.That(part.FaceAttributes[-FaceData], Is.TypeOf<int[]>().And.EqualTo(new[] { 22 }));
            }
        }

        [Test]
        public void DirectCornersInterpolateTheOriginalEdges()
        {
            var source = Quads((-1.0, 1.0));
            source.FaceVertexAttributes[CornerData] = new[] { 10.0, 20.0, 30.0, 40.0 };
            var (negative, positive) = CheckSplit(source, Plane, SplitterOptions.NegativeAndPositive);
            Assert.That(negative.FaceVertexAttributes[CornerData], Is.EqualTo(new[] { 10.0, 15.0, 35.0, 40.0 }));
            Assert.That(positive.FaceVertexAttributes[CornerData], Is.EqualTo(new[] { 15.0, 20.0, 30.0, 35.0 }));
        }

        [Test]
        public void MixedRetainedAndCutAttributesStayAligned(
            [Values] SplitterOptions options, [Values] Layout vertices,
            [Values] Layout corners, [Values] bool indexedFaces)
        {
            var source = Quads((-3.0, -2.0), (-1.0, 1.0), (2.0, 3.0));
            AddAttributes(source, vertices, corners, indexedFaces);
            CheckSplit(source, Plane, options);
        }

        [Test]
        public void RetainedOnlyIndexedFacesAreCompact([Values] SplitterOptions options)
        {
            var source = Quads((-3.0, -2.0), (2.0, 3.0), (-5.0, -4.0));
            source.FaceAttributes[FaceData] = new[] { 2, 3, 2 };
            source.FaceAttributes[-FaceData] = new[] { "unused", "also unused", "negative", "positive", "unused tail" };
            AddVaryingAttributes(source, Layout.Indexed, Layout.Direct);
            var (negative, positive) = CheckSplit(source, Plane, options);
            if (negative != null)
            {
                Assert.That(negative.FaceAttributes[FaceData], Is.EqualTo(new[] { 0, 0 }));
                Assert.That(negative.FaceAttributes[-FaceData], Is.TypeOf<string[]>().And.EqualTo(new[] { "negative" }));
            }
            if (positive != null)
            {
                Assert.That(positive.FaceAttributes[FaceData], Is.EqualTo(new[] { 0 }));
                Assert.That(positive.FaceAttributes[-FaceData], Is.TypeOf<string[]>().And.EqualTo(new[] { "positive" }));
            }
        }

        [Test]
        public void RetainedAndGeneratedFacesShareAttributeValues([Values] SplitterOptions options)
        {
            var source = Quads((-3.0, -2.0), (-1.0, 1.0), (2.0, 3.0));
            source.FaceAttributes[FaceData] = new[] { 1, 1, 1 };
            source.FaceAttributes[-FaceData] = new[] { 11.0f, 22.0f, 33.0f };
            var result = CheckSplit(source, Plane, options);
            foreach (var part in new[] { result.Item1, result.Item2 }.Where(x => x != null))
            {
                Assert.That(part.FaceAttributes[FaceData], Is.EqualTo(new[] { 0, 0 }));
                Assert.That(part.FaceAttributes[-FaceData], Is.TypeOf<float[]>().And.EqualTo(new[] { 22.0f }));
            }
        }

        [Test]
        public void SharedEdgeCutsReuseVerticesButKeepCornerSeams(
            [Values] SplitterOptions options, [Values] Layout vertices, [Values] bool indexedCorners)
        {
            // The common edge is traversed in opposite directions. Its cut is at
            // t = 1/4 or 3/4, so reusing the first face's interpolation parameter
            // for the second face's corner attributes would be incorrect.
            var source = Mesh(new[] { new V3d(-1, 0, 0), new V3d(3, 0, 0), new V3d(3, 1, 0), new V3d(-1, 1, 0) },
                new[] { 0, 1, 2 }, new[] { 0, 2, 3 });
            AddAttributes(source, vertices, indexedCorners ? Layout.Indexed : Layout.Direct, true);
            var result = CheckSplit(source, Plane, options);
            foreach (var part in new[] { result.Item1, result.Item2 }.Where(x => x != null))
            {
                var shared = Array.FindIndex(part.PositionArray, p => (p - new V3d(0, 0.25, 0)).Length < Epsilon);
                Assert.That(shared, Is.GreaterThanOrEqualTo(0));
                Assert.That(part.PositionArray.Count(p => (p - new V3d(0, 0.25, 0)).Length < Epsilon), Is.EqualTo(1));
                var corners = Enumerable.Range(0, part.VertexIndexCount).Where(i => part.VertexIndexArray[i] == shared).ToArray();
                Assert.That(corners.Length, Is.EqualTo(2));
                Assert.That(Value(part.FaceVertexAttributes, CornerData, corners[0]),
                    Is.Not.EqualTo(Value(part.FaceVertexAttributes, CornerData, corners[1])), "The seam must not be welded.");
            }
        }

        [Test]
        public void PlaneVerticesAndWindingArePreserved(
            [Values(0, 1, 2, 3)] int rotation, [Values] bool reverse, [Values] SplitterOptions options)
        {
            var points = new[] { new V3d(0, -1, 0), new V3d(1, 0, 0), new V3d(0, 1, 0), new V3d(-1, 0, 0) };
            var indices = Enumerable.Range(0, 4).Select(i => (i + rotation) % 4).ToArray();
            if (reverse) Array.Reverse(indices);
            var source = Mesh(points, indices);
            AddAttributes(source, Layout.Indexed, Layout.Direct, true);
            CheckSplit(source, Plane, options);
        }

        [Test]
        public void TrianglesWithOnePlaneVertexKeepBothFragments(
            [Values(0, 1, 2)] int rotation, [Values] SplitterOptions options)
        {
            var source = Mesh(new[] { new V3d(0, 1, 0), new V3d(-1, 0, 0), new V3d(1, 0, 0) },
                Enumerable.Range(0, 3).Select(i => (i + rotation) % 3).ToArray());
            AddAttributes(source, Layout.Direct, Layout.Indexed, true);
            CheckSplit(source, Plane, options);
        }

        [Test]
        public void ToleranceBoundaryVerticesAreNotProjected(
            [Values(-1.0, -0.5, 0.0, 0.5, 1.0)] double height, [Values] SplitterOptions options)
        {
            var onPlane = new V3d(height * Epsilon, 1, 0);
            var source = Mesh(new[] { onPlane, new V3d(-1, 0, 0), new V3d(1, 0, 0) }, new[] { 0, 1, 2 });
            AddAttributes(source, Layout.Indexed, Layout.Direct, true);
            var result = CheckSplit(source, Plane, options);
            foreach (var part in new[] { result.Item1, result.Item2 }.Where(x => x != null))
                Assert.That(part.PositionArray.Contains(onPlane), Is.True);
        }

        [Test]
        public void NoneDoesNotAllocateGeometrySizedStorage()
        {
            var source = Quads((-1.0, 1.0));
            source.PositionArray = Enumerable.Range(0, 65536).Select(i => new V3d((i & 1) == 0 ? -1 : 1, i, 0)).ToArray();
            for (int i = 0; i < 64; i++) source.SplitOnPlane(Plane, Epsilon, SplitterOptions.None);
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 256; i++) source.SplitOnPlane(Plane, Epsilon, SplitterOptions.None);
            var bytesPerCall = (GC.GetAllocatedBytesForCurrentThread() - before) / 256;
            // A small captured-plane closure is permitted, but neither a height
            // array nor any side's mesh, maps, or fragments should be allocated.
            Assert.That(bytesPerCall, Is.LessThan(256));
        }

        [Test]
        public void WholeSideAndEmptyInputsRetainIdentity(
            [Values(-1, 0, 1)] int side, [Values] SplitterOptions options)
        {
            var source = side == 0 ? Mesh(Array.Empty<V3d>()) : Quads((side * 3.0 - 0.5, side * 3.0 + 0.5));
            AddAttributes(source, Layout.Indexed, Layout.Indexed, true);
            var snapshot = new Snapshot(source);
            var (negative, positive) = source.SplitOnPlane(Plane, Epsilon, options);
            var expectPositive = (options & SplitterOptions.Positive) != 0 && side >= 0;
            var expectNegative = (options & SplitterOptions.Negative) != 0 && side <= 0 && !expectPositive;
            Assert.That(negative, expectNegative ? Is.SameAs(source) : Is.Null);
            Assert.That(positive, expectPositive ? Is.SameAs(source) : Is.Null);
            snapshot.AssertUnchanged(source);
        }

        [Test]
        public void WholeCoplanarInputKeepsExistingSidePreference(
            [Values(-0.5, 0.0, 0.5)] double offset, [Values] SplitterOptions options)
        {
            var x = offset * Epsilon;
            var source = Mesh(new[] { new V3d(x, 0, 0), new V3d(x, 1, 0), new V3d(x, 1, 1), new V3d(x, 0, 1) }, new[] { 0, 1, 2, 3 });
            AddAttributes(source, Layout.Direct, Layout.Indexed, true);
            var snapshot = new Snapshot(source);
            var (negative, positive) = source.SplitOnPlane(Plane, Epsilon, options);
            var doPos = (options & SplitterOptions.Positive) != 0;
            var doNeg = (options & SplitterOptions.Negative) != 0 && !doPos;
            Assert.That(negative, doNeg ? Is.SameAs(source) : Is.Null);
            Assert.That(positive, doPos ? Is.SameAs(source) : Is.Null);
            snapshot.AssertUnchanged(source);
        }

        [Test]
        public void CoplanarFaceInMixedInputStaysPositive([Values] SplitterOptions options)
        {
            var source = Mesh(new[] {
                new V3d(-2, 0, 0), new V3d(-1, 0, 0), new V3d(-1, 1, 0),
                new V3d(1, 0, 0), new V3d(2, 0, 0), new V3d(1, 1, 0),
                new V3d(0, 0, 0), new V3d(0, 1, 0), new V3d(0, 0, 1) },
                new[] { 0, 1, 2 }, new[] { 3, 4, 5 }, new[] { 6, 7, 8 });
            AddAttributes(source, Layout.Indexed, Layout.Direct, true);
            var snapshot = new Snapshot(source);
            var (negative, positive) = source.SplitOnPlane(Plane, Epsilon, options);
            if ((options & SplitterOptions.Negative) != 0)
            {
                Assert.That(negative.FaceAttributes[SourceFace], Is.EqualTo(new[] { 0 }));
                AssertOutput(source, negative, 0, Plane);
            }
            else Assert.That(negative, Is.Null);
            if ((options & SplitterOptions.Positive) != 0)
            {
                Assert.That(positive.FaceAttributes[SourceFace], Is.EqualTo(new[] { 1, 2 }));
                AssertOutput(source, positive, 1, Plane);
            }
            else Assert.That(positive, Is.Null);
            snapshot.AssertUnchanged(source);
        }

        [Test]
        public void SeededConvexPolygonsMatchIndependentClipping([Values(7, 71, 719)] int seed)
        {
            var random = new Random(seed);
            for (int sample = 0; sample < 60; sample++)
            {
                var count = random.Next(3, 13);
                var phase = random.NextDouble() * 2 * Math.PI;
                var cx = random.NextDouble() - 0.5;
                var points = Enumerable.Range(0, count).Select(i => {
                    var angle = phase + i * 2 * Math.PI / count;
                    return new V3d(cx + 2 * Math.Cos(angle), Math.Sin(angle), 0);
                }).ToArray();
                var indices = Enumerable.Range(0, count).ToArray();
                if ((sample & 1) != 0) Array.Reverse(indices);
                var source = Mesh(points, indices);
                AddAttributes(source, (Layout)(1 + sample % 2), (Layout)(1 + (sample / 2) % 2), (sample & 4) != 0);
                var plane = new Plane3d(new V3d(3, 0, 0), 0.3);
                foreach (SplitterOptions options in Enum.GetValues(typeof(SplitterOptions)))
                    CheckSplit(source, plane, options);
            }
        }

        [Test]
        public void AttributeProjectionHandlesEmptyRetainedAndFragmentMaps([Values] bool indexed)
        {
            var values = new[] { "unused", "a", "b" };
            var indices = indexed ? new[] { 2, 1, 2 } : null;
            Func<double, string, string, string> interpolate = (t, a, b) => a;
            var attribute = new PolyMesh.Attribute<string>(FaceData, indices, values, interpolate);
            var result = (PolyMesh.Attribute<string>)attribute.SplitFaceAttribute(Array.Empty<int>(), new List<PolygonSplitter.Face>());
            Assert.That(result.Name, Is.EqualTo(FaceData));
            Assert.That(result.TypedValueArray, Is.TypeOf<string[]>().And.Empty);
            Assert.That(result.Interpolator, Is.SameAs(interpolate));
            if (indexed) Assert.That(result.IndexArray, Is.Empty);
            else Assert.That(result.IndexArray, Is.Null);
            Assert.That(values, Is.EqualTo(new[] { "unused", "a", "b" }));
            if (indexed) Assert.That(indices, Is.EqualTo(new[] { 2, 1, 2 }));
        }

        [Test]
        public void PolygonSplitterNoneHasNoResult()
        {
            var source = Quads((-1.0, 1.0));
            var splitter = new PolygonSplitter(source.FirstIndexArray, source.FaceCount, source.VertexIndexArray,
                4, source.PositionArray.Select(p => p.X).ToArray(), Epsilon, SplitterOptions.None);
            Assert.That(splitter.FirstIndexArray(0), Is.Null);
            Assert.That(splitter.FirstIndexArray(1), Is.Null);
            Assert.That(splitter.VertexIndexArray(0), Is.Null);
            Assert.That(splitter.VertexIndexArray(1), Is.Null);
        }

        private static PolyMesh Mesh(V3d[] points, params int[][] faces)
        {
            var first = new int[faces.Length + 1];
            for (int i = 0; i < faces.Length; i++) first[i + 1] = first[i] + faces[i].Length;
            var mesh = new PolyMesh { PositionArray = points, FirstIndexArray = first, VertexIndexArray = faces.SelectMany(x => x).ToArray() };
            mesh.FaceAttributes[SourceFace] = Enumerable.Range(0, faces.Length).ToArray();
            mesh.InstanceAttributes[InstanceData] = "preserved";
            return mesh;
        }

        private static PolyMesh Quads(params (double lo, double hi)[] ranges)
        {
            var points = ranges.SelectMany((r, i) => new[] {
                new V3d(r.lo, i * 2, 0), new V3d(r.hi, i * 2, 0),
                new V3d(r.hi, i * 2 + 1, 0), new V3d(r.lo, i * 2 + 1, 0) }).ToArray();
            return Mesh(points, Enumerable.Range(0, ranges.Length).Select(i => new[] { 4 * i, 4 * i + 1, 4 * i + 2, 4 * i + 3 }).ToArray());
        }

        private static void AddAttributes(PolyMesh mesh, Layout vertices, Layout corners, bool indexedFaces)
        {
            if (indexedFaces)
            {
                mesh.FaceAttributes[FaceData] = Enumerable.Range(0, mesh.FaceCount).Select(i => (i & 1) == 0 ? 3 : 1).ToArray();
                mesh.FaceAttributes[-FaceData] = new[] { 11, 22, 33, 44, 55 };
            }
            else mesh.FaceAttributes[FaceData] = Enumerable.Range(0, mesh.FaceCount).Select(i => 100 + i).ToArray();
            AddVaryingAttributes(mesh, vertices, corners);
        }

        private static void AddVaryingAttributes(PolyMesh mesh, Layout vertices, Layout corners)
        {
            Put(mesh.VertexAttributes, VertexData, mesh.PositionArray.Select(p => 10 + 4 * p.X + 2 * p.Y - 3 * p.Z).ToArray(), vertices);
            Put(mesh.FaceVertexAttributes, CornerData, Enumerable.Range(0, mesh.VertexIndexCount).Select(i => 10.0 + i * 10).ToArray(), corners);
        }

        private static void Put(SymbolDict<Array> attributes, Symbol name, double[] data, Layout layout)
        {
            if (layout == Layout.Direct) attributes[name] = data;
            if (layout == Layout.Indexed)
            {
                attributes[name] = Enumerable.Range(0, data.Length).Select(i => data.Length - i).ToArray();
                attributes[-name] = new[] { -999.0 }.Concat(data.Reverse()).Concat(new[] { 999.0 }).ToArray();
            }
        }

        private static object Value(SymbolDict<Array> attributes, Symbol name, int index)
        {
            return attributes.TryGetValue(-name, out var values)
                ? values.GetValue(((int[])attributes[name])[index]) : attributes[name].GetValue(index);
        }

        private static V3d[] FacePoints(PolyMesh mesh, int face)
            => Enumerable.Range(mesh.FirstIndexArray[face], mesh.FirstIndexArray[face + 1] - mesh.FirstIndexArray[face])
                .Select(i => mesh.PositionArray[mesh.VertexIndexArray[i]]).ToArray();

        private static V3d Area(V3d[] points)
        {
            var area = V3d.Zero;
            for (int i = 0; i < points.Length; i++) area += Vec.Cross(points[i], points[(i + 1) % points.Length]);
            return area * 0.5;
        }

        // Independent Sutherland-Hodgman oracle; it does not use splitter maps,
        // edge deduplication, or the implementation's retained/fragment ordering.
        private static V3d[] Clip(V3d[] points, Plane3d plane, int side)
        {
            var result = new List<V3d>();
            for (int i = 0; i < points.Length; i++)
            {
                var a = points[i]; var b = points[(i + 1) % points.Length];
                var ha = plane.Height(a); var hb = plane.Height(b);
                var keepA = side == 0 ? ha <= Epsilon : ha >= -Epsilon;
                var keepB = side == 0 ? hb <= Epsilon : hb >= -Epsilon;
                if (keepA) result.Add(a);
                if (keepA != keepB && Math.Abs(ha) > Epsilon && Math.Abs(hb) > Epsilon)
                    result.Add(a + ha / (ha - hb) * (b - a));
            }
            return result.ToArray();
        }

        private static (PolyMesh, PolyMesh) CheckSplit(PolyMesh source, Plane3d plane, SplitterOptions options)
        {
            var snapshot = new Snapshot(source);
            var both = source.SplitOnPlane(plane, Epsilon);
            var result = source.SplitOnPlane(plane, Epsilon, options);
            for (int side = 0; side < 2; side++)
            {
                var expected = side == 0 ? both.Item1 : both.Item2;
                var part = side == 0 ? result.Item1 : result.Item2;
                if ((options & (side == 0 ? SplitterOptions.Negative : SplitterOptions.Positive)) == 0)
                    Assert.That(part, Is.Null);
                else
                {
                    AssertOutput(source, part, side, plane);
                    Assert.That(part.FirstIndexArray, Is.EqualTo(expected.FirstIndexArray));
                    Assert.That(part.VertexIndexArray, Is.EqualTo(expected.VertexIndexArray));
                    foreach (var (actual, reference) in new[] { (part.VertexAttributes, expected.VertexAttributes),
                        (part.FaceAttributes, expected.FaceAttributes), (part.FaceVertexAttributes, expected.FaceVertexAttributes) })
                        foreach (var entry in reference) Assert.That(actual[entry.Key], Is.EqualTo(entry.Value));
                }
            }
            var originalArea = Enumerable.Range(0, source.FaceCount).Aggregate(V3d.Zero, (sum, f) => sum + Area(FacePoints(source, f)));
            var splitArea = new[] { both.Item1, both.Item2 }.Where(p => p != null).SelectMany(p => Enumerable.Range(0, p.FaceCount).Select(f => Area(FacePoints(p, f))))
                .Aggregate(V3d.Zero, (sum, area) => sum + area);
            Assert.That((originalArea - splitArea).Length, Is.LessThan(1e-8));
            snapshot.AssertUnchanged(source);
            return result;
        }

        private static void AssertOutput(PolyMesh source, PolyMesh part, int side, Plane3d plane)
        {
            Assert.That(part, Is.Not.Null);
            Assert.That(part.FirstIndexArray.Length, Is.EqualTo(part.FaceCount + 1));
            Assert.That(part.FirstIndexArray[0], Is.Zero);
            Assert.That(part.FirstIndexArray[part.FaceCount], Is.EqualTo(part.VertexIndexCount));
            Assert.That(part.VertexIndexArray.Distinct().OrderBy(i => i), Is.EqualTo(Enumerable.Range(0, part.VertexCount)));
            Assert.That(part.InstanceAttributes, Is.SameAs(source.InstanceAttributes));
            foreach (var p in part.PositionArray)
                Assert.That(side == 0 ? plane.Height(p) : -plane.Height(p), Is.LessThanOrEqualTo(Epsilon * 2));
            foreach (var (attributes, count) in new[] { (part.FaceAttributes, part.FaceCount),
                (part.VertexAttributes, part.VertexCount), (part.FaceVertexAttributes, part.VertexIndexCount) })
            {
                foreach (var name in attributes.Keys.Where(k => k.IsPositive))
                {
                    Assert.That(attributes[name].Length, Is.EqualTo(count));
                    if (attributes.TryGetValue(-name, out var values))
                    {
                        var indices = (int[])attributes[name];
                        Assert.That(indices.All(i => i >= 0 && i < values.Length), Is.True);
                        Assert.That(indices.Distinct().OrderBy(i => i), Is.EqualTo(Enumerable.Range(0, values.Length)), "Indexed values must be compact.");
                    }
                }
            }
            if (source.VertexAttributes.Contains(VertexData))
                for (int i = 0; i < part.VertexCount; i++)
                {
                    var p = part.PositionArray[i];
                    Assert.That((double)Value(part.VertexAttributes, VertexData, i), Is.EqualTo(10 + 4 * p.X + 2 * p.Y - 3 * p.Z).Within(1e-8));
                }
            for (int face = 0; face < part.FaceCount; face++)
            {
                var old = (int)Value(part.FaceAttributes, SourceFace, face);
                var points = FacePoints(part, face);
                var original = FacePoints(source, old);
                var expected = Clip(original, plane, side);
                Assert.That(points.Length, Is.GreaterThanOrEqualTo(3).And.EqualTo(expected.Length));
                foreach (var p in expected) Assert.That(points.Any(q => (p - q).Length < 1e-8), Is.True);
                Assert.That((Area(points) - Area(expected)).Length, Is.LessThan(1e-8));
                Assert.That(Vec.Dot(Area(points), Area(original)), Is.GreaterThan(0), "Winding must be preserved.");
                foreach (var name in source.FaceAttributeNames)
                    Assert.That(Value(part.FaceAttributes, name, face), Is.EqualTo(Value(source.FaceAttributes, name, old)));
                if (!source.FaceVertexAttributes.Contains(CornerData)) continue;
                for (int i = part.FirstIndexArray[face]; i < part.FirstIndexArray[face + 1]; i++)
                {
                    var point = part.PositionArray[part.VertexIndexArray[i]];
                    var oldFirst = source.FirstIndexArray[old];
                    var expectedValue = double.NaN;
                    for (int edge = 0; edge < original.Length; edge++)
                    {
                        var next = (edge + 1) % original.Length;
                        var delta = original[next] - original[edge];
                        var t = Vec.Dot(point - original[edge], delta) / delta.LengthSquared;
                        if (t < -Epsilon || t > 1 + Epsilon || (original[edge] + t * delta - point).Length > 1e-8) continue;
                        var a = (double)Value(source.FaceVertexAttributes, CornerData, oldFirst + edge);
                        var b = (double)Value(source.FaceVertexAttributes, CornerData, oldFirst + next);
                        expectedValue = a + t * (b - a);
                        break;
                    }
                    Assert.That((double)Value(part.FaceVertexAttributes, CornerData, i), Is.EqualTo(expectedValue).Within(1e-8));
                }
            }
        }

        private sealed class Snapshot
        {
            private readonly int faces, vertices, corners;
            private readonly SymbolDict<object> instanceAttributes;
            private readonly (Array reference, Array values)[] arrays;
            private readonly SymbolDict<Array>[] dictionaries;
            private readonly Symbol[][] keys;
            public Snapshot(PolyMesh mesh)
            {
                faces = mesh.FaceCount; vertices = mesh.VertexCount; corners = mesh.VertexIndexCount;
                instanceAttributes = mesh.InstanceAttributes;
                dictionaries = new[] { mesh.FaceAttributes, mesh.VertexAttributes, mesh.FaceVertexAttributes };
                keys = dictionaries.Select(d => d.Keys.ToArray()).ToArray();
                arrays = new[] { mesh.FirstIndexArray, mesh.VertexIndexArray }.Cast<Array>()
                    .Concat(dictionaries.SelectMany(d => d.Values)).Select(a => (a, (Array)a.Clone())).ToArray();
            }
            public void AssertUnchanged(PolyMesh mesh)
            {
                Assert.That(mesh.FaceCount, Is.EqualTo(faces));
                Assert.That(mesh.VertexCount, Is.EqualTo(vertices));
                Assert.That(mesh.VertexIndexCount, Is.EqualTo(corners));
                Assert.That(mesh.InstanceAttributes, Is.SameAs(instanceAttributes));
                var current = new[] { mesh.FaceAttributes, mesh.VertexAttributes, mesh.FaceVertexAttributes };
                for (int i = 0; i < current.Length; i++)
                {
                    Assert.That(current[i], Is.SameAs(dictionaries[i]));
                    Assert.That(current[i].Keys, Is.EquivalentTo(keys[i]));
                }
                var actual = new[] { mesh.FirstIndexArray, mesh.VertexIndexArray }.Cast<Array>().Concat(current.SelectMany(d => d.Values)).ToArray();
                for (int i = 0; i < actual.Length; i++)
                {
                    Assert.That(actual[i], Is.SameAs(arrays[i].reference));
                    Assert.That(actual[i], Is.EqualTo(arrays[i].values));
                }
                Assert.That(mesh.InstanceAttributes[InstanceData], Is.EqualTo("preserved"));
            }
        }
    }
}
