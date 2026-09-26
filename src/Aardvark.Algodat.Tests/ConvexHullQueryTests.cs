/*
    Copyright (C) 2006-2026. Aardvark Platform Team. http://github.com/aardvark-platform.
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.
    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/
using Aardvark.Base;
using Aardvark.Data.Points;
using Aardvark.Geometry.Points;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class ConvexHullQueryTests
    {
        [Test]
        public void BoxHullLeafQueriesUseExactComplementAndBoundarySemantics()
        {
            var positions = new[]
            {
                new V3d(0.50, 0.50, 0.50),
                new V3d(0.25, 0.50, 0.50),
                new V3d(0.75, 0.50, 0.50),
                new V3d(0.50, 0.25, 0.50),
                new V3d(0.50, 0.75, 0.50),
                new V3d(0.50, 0.50, 0.25),
                new V3d(0.50, 0.50, 0.75),
                new V3d(0.25, 0.25, 0.50),
                new V3d(0.25, 0.25, 0.25),
                new V3d(0.10, 0.50, 0.50),
                new V3d(0.90, 0.50, 0.50),
                new V3d(0.50, 0.10, 0.50),
                new V3d(0.50, 0.90, 0.50),
                new V3d(0.50, 0.50, 0.10),
                new V3d(0.50, 0.50, 0.90),
                new V3d(0.10, 0.10, 0.50),
                new V3d(0.90, 0.90, 0.90)
            };
            var perPointParts = new int[positions.Length].SetByIndex(i => 2000 + i);
            var fixture = CreateFixture(positions, splitLimit: 64, generateLod: false, perPointParts);
            var hull = new Hull3d(new Box3d(new V3d(0.25), new V3d(0.75)));
            var root = fixture.PointSet.Root.Value;
            ClassicAssert.IsTrue(root.IsLeaf);
            var front = fixture.PointSet.QueryAllPoints().ToArray();

            AssertAllOverloads(fixture.PointSet, root, hull, front, int.MinValue);

            var inside = ResultIntensities(fixture.PointSet.QueryPointsInsideConvexHull(hull));
            var outside = ResultIntensities(fixture.PointSet.QueryPointsOutsideConvexHull(hull));
            for (var i = 0; i <= 8; i++)
            {
                ClassicAssert.IsTrue(inside.Contains(1000 + i), $"Expected boundary/interior source index {i} inside.");
                ClassicAssert.IsFalse(outside.Contains(1000 + i), $"Expected boundary/interior source index {i} excluded from outside.");
            }
            for (var i = 9; i < positions.Length; i++)
            {
                ClassicAssert.IsFalse(inside.Contains(1000 + i), $"Expected source index {i} outside.");
                ClassicAssert.IsTrue(outside.Contains(1000 + i), $"Expected source index {i} in complement.");
            }
        }

        [Test]
        public void ObliqueHullSubdividedQueriesMatchBruteForce()
        {
            var positions = ObliquePositions();
            var fixture = CreateFixture(positions, splitLimit: 8, generateLod: true, partIndices: 77);
            var hull = CreateObliqueHull();
            var root = fixture.PointSet.Root.Value;
            ClassicAssert.IsFalse(root.IsLeaf);
            var front = fixture.PointSet.QueryAllPoints().ToArray();

            AssertAllOverloads(fixture.PointSet, root, hull, front, int.MinValue);

            var inside = ResultIntensities(root.QueryPointsInsideConvexHull(hull));
            var outside = ResultIntensities(root.QueryPointsOutsideConvexHull(hull));
            for (var i = 0; i <= 2; i++)
            {
                ClassicAssert.IsTrue(inside.Contains(1000 + i));
                ClassicAssert.IsFalse(outside.Contains(1000 + i));
            }
            for (var i = 3; i <= 9; i++)
            {
                ClassicAssert.IsFalse(inside.Contains(1000 + i));
                ClassicAssert.IsTrue(outside.Contains(1000 + i));
            }
        }

        [Test]
        public void ObliqueHullLodFrontMatchesBruteForce()
        {
            var fixture = CreateFixture(ObliquePositions(), splitLimit: 8, generateLod: true, partIndices: 77);
            var hull = CreateObliqueHull();
            var root = fixture.PointSet.Root.Value;
            const int minCellExponent = -2;
            var front = root.QueryPoints(_ => true, _ => false, _ => true, minCellExponent).ToArray();
            ClassicAssert.IsTrue(front.Length > 1);
            ClassicAssert.IsTrue(front.Sum(x => x.Count) > 0);

            AssertAllOverloads(fixture.PointSet, root, hull, front, minCellExponent);
        }

        [Test]
        public void HullContainsBoxMatchesCornerReferenceWithoutChangingBoundaries()
        {
            var hulls = new[]
            {
                new Hull3d(new Box3d(new V3d(0.25), new V3d(0.75))),
                CreateObliqueHull()
            };
            var boxes = new List<Box3d>
            {
                new(new V3d(0.30), new V3d(0.70)),
                new(new V3d(0.25), new V3d(0.75)),
                new(new V3d(0.10), new V3d(0.20)),
                new(new V3d(0.70), new V3d(0.80)),
                new(new V3d(0.25, 0.50, 0.50), new V3d(0.25, 0.60, 0.60))
            };
            var random = new Random(1969);
            for (var i = 0; i < 500; i++)
            {
                var min = new V3d(random.NextDouble(), random.NextDouble(), random.NextDouble());
                var max = min + new V3d(random.NextDouble(), random.NextDouble(), random.NextDouble()) * 0.2;
                boxes.Add(new Box3d(min, max));
            }

            foreach (var hull in hulls)
            {
                foreach (var box in boxes)
                {
                    var expected = box.Corners.All(p => hull.Contains(p));
                    ClassicAssert.AreEqual(expected, hull.Contains(box), $"Hull/box mismatch for {box}.");
                }
            }
        }

        private sealed class Fixture
        {
            public PointSet PointSet { get; }

            public Fixture(PointSet pointSet)
            {
                PointSet = pointSet;
            }
        }

        private sealed class PointRecord
        {
            public V3d Position { get; }
            public C4b Color { get; }
            public V3f Normal { get; }
            public int Intensity { get; }
            public byte Classification { get; }
            public int PartIndex { get; }

            public PointRecord(
                V3d position,
                C4b color,
                V3f normal,
                int intensity,
                byte classification,
                int partIndex
                )
            {
                Position = position;
                Color = color;
                Normal = normal;
                Intensity = intensity;
                Classification = classification;
                PartIndex = partIndex;
            }
        }

        private static Fixture CreateFixture(
            V3d[] positions,
            int splitLimit,
            bool generateLod,
            object partIndices
            )
        {
            var colors = new C4b[positions.Length].SetByIndex(i =>
                new C4b((byte)(i % 251), (byte)((i + 31) % 251), (byte)((i + 67) % 251), byte.MaxValue)
                );
            var normals = new V3f[positions.Length].SetByIndex(i => new V3f(i + 1, i + 2, i + 3).Normalized);
            var intensities = new int[positions.Length].SetByIndex(i => 1000 + i);
            var classifications = new byte[positions.Length].SetByIndex(i => (byte)(i % 200));
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var root = InMemoryPointSet.Build(
                positions, colors, normals, intensities, classifications, partIndices,
                Cell.Unit, splitLimit
                ).ToPointSetNode(storage, isTemporaryImportNode: true);
            var pointSet = new PointSet(storage, Guid.NewGuid().ToString(), root.Id, splitLimit);
            if (generateLod)
                pointSet = pointSet.GenerateLod(ImportConfig.Default.WithRandomKey());
            return new Fixture(pointSet);
        }

        private static V3d[] ObliquePositions()
        {
            var positions = new List<V3d>
            {
                new(0.50, 0.50, 0.50),
                new(0.25, 0.50, 0.50),
                new(0.25, 0.75, 0.75),
                new(0.125, 0.50, 0.50),
                new(0.50, 0.125, 0.50),
                new(0.50, 0.50, 0.125),
                new(0.75, 0.75, 0.50),
                new(0.125, 0.125, 0.50),
                new(0.125, 0.125, 0.125),
                new(0.875, 0.875, 0.875)
            };
            var coordinates = new[] { 0.125, 0.25, 0.375, 0.50, 0.625, 0.75, 0.875 };
            foreach (var x in coordinates)
                foreach (var y in coordinates)
                    foreach (var z in coordinates)
                    {
                        var p = new V3d(x, y, z);
                        if (!positions.Contains(p)) positions.Add(p);
                    }
            return positions.ToArray();
        }

        private static Hull3d CreateObliqueHull()
        {
            var slantedNormal = new V3d(1.0, 1.0, 1.0).Normalized;
            return new Hull3d(new[]
            {
                new Plane3d(-V3d.XAxis, new V3d(0.25, 0.0, 0.0)),
                new Plane3d(-V3d.YAxis, new V3d(0.0, 0.25, 0.0)),
                new Plane3d(-V3d.ZAxis, new V3d(0.0, 0.0, 0.25)),
                new Plane3d(slantedNormal, new V3d(0.25, 0.75, 0.75))
            });
        }

        private static void AssertAllOverloads(
            PointSet pointSet,
            IPointCloudNode root,
            Hull3d hull,
            Chunk[] front,
            int minCellExponent
            )
        {
            var all = Records(front);
            var expectedInside = all.Values.Where(x => hull.Contains(x.Position)).ToDictionary(x => x.Intensity);
            var expectedOutside = all.Values.Where(x => !hull.Contains(x.Position)).ToDictionary(x => x.Intensity);
            ClassicAssert.AreEqual(all.Count, expectedInside.Count + expectedOutside.Count);

            AssertResult(pointSet.QueryPointsInsideConvexHull(hull, minCellExponent), expectedInside);
            AssertResult(root.QueryPointsInsideConvexHull(hull, minCellExponent), expectedInside);
            AssertResult(pointSet.QueryPointsOutsideConvexHull(hull, minCellExponent), expectedOutside);
            AssertResult(root.QueryPointsOutsideConvexHull(hull, minCellExponent), expectedOutside);

            ClassicAssert.AreEqual(expectedInside.Count, pointSet.CountPointsInsideConvexHull(hull, minCellExponent));
            ClassicAssert.AreEqual(expectedInside.Count, root.CountPointsInsideConvexHull(hull, minCellExponent));
            ClassicAssert.AreEqual(expectedOutside.Count, pointSet.CountPointsOutsideConvexHull(hull, minCellExponent));
            ClassicAssert.AreEqual(expectedOutside.Count, root.CountPointsOutsideConvexHull(hull, minCellExponent));

            var approximateInsidePointSet = pointSet.CountPointsApproximatelyInsideConvexHull(hull, minCellExponent);
            var approximateInsideNode = root.CountPointsApproximatelyInsideConvexHull(hull, minCellExponent);
            var approximateOutsidePointSet = pointSet.CountPointsApproximatelyOutsideConvexHull(hull, minCellExponent);
            var approximateOutsideNode = root.CountPointsApproximatelyOutsideConvexHull(hull, minCellExponent);
            ClassicAssert.AreEqual(approximateInsidePointSet, approximateInsideNode);
            ClassicAssert.AreEqual(approximateOutsidePointSet, approximateOutsideNode);
            ClassicAssert.GreaterOrEqual(approximateInsidePointSet, expectedInside.Count);
            ClassicAssert.GreaterOrEqual(approximateOutsidePointSet, expectedOutside.Count);
            ClassicAssert.LessOrEqual(approximateInsidePointSet, all.Count);
            ClassicAssert.LessOrEqual(approximateOutsidePointSet, all.Count);
        }

        private static Dictionary<int, PointRecord> Records(IEnumerable<Chunk> chunks)
        {
            var result = new Dictionary<int, PointRecord>();
            foreach (var chunk in chunks)
            {
                ClassicAssert.IsTrue(chunk.HasColors);
                ClassicAssert.IsTrue(chunk.HasNormals);
                ClassicAssert.IsTrue(chunk.HasIntensities);
                ClassicAssert.IsTrue(chunk.HasClassifications);
                ClassicAssert.IsTrue(chunk.HasPartIndices);
                var partIndices = chunk.TryGetPartIndices();
                for (var i = 0; i < chunk.Count; i++)
                {
                    var intensity = chunk.Intensities[i];
                    ClassicAssert.IsTrue(result.TryAdd(intensity, new PointRecord(
                        chunk.Positions[i], chunk.Colors[i], chunk.Normals[i], intensity,
                        chunk.Classifications[i], partIndices[i]
                        )));
                }
            }
            return result;
        }

        private static void AssertResult(
            IEnumerable<Chunk> chunks,
            Dictionary<int, PointRecord> expected
            )
        {
            var actual = Records(chunks);
            CollectionAssert.AreEquivalent(expected.Keys, actual.Keys);
            foreach (var (intensity, expectedPoint) in expected)
            {
                var actualPoint = actual[intensity];
                ClassicAssert.AreEqual(expectedPoint.Position, actualPoint.Position);
                ClassicAssert.AreEqual(expectedPoint.Color, actualPoint.Color);
                ClassicAssert.AreEqual(expectedPoint.Normal, actualPoint.Normal);
                ClassicAssert.AreEqual(expectedPoint.Intensity, actualPoint.Intensity);
                ClassicAssert.AreEqual(expectedPoint.Classification, actualPoint.Classification);
                ClassicAssert.AreEqual(expectedPoint.PartIndex, actualPoint.PartIndex);
            }
        }

        private static HashSet<int> ResultIntensities(IEnumerable<Chunk> chunks)
            => chunks.SelectMany(x => x.Intensities).ToHashSet();
    }
}
