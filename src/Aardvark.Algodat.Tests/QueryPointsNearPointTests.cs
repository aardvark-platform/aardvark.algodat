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
    public class QueryPointsNearPointTests
    {
        [Test]
        public void TemporaryLeafUsesBoundedScanWithAlignedAttributes()
        {
            var query = new V3d(0.5, 0.5, 0.5);
            var fixture = CreateFixture(
                new[]
                {
                    query,
                    query + new V3d(0.1, 0.0, 0.0),
                    query + new V3d(0.0, 0.2, 0.0),
                    query + new V3d(0.0, 0.0, 0.3),
                    query + new V3d(0.4, 0.0, 0.0)
                },
                splitLimit: 32,
                isTemporary: true,
                partIndices: new[] { 200, 201, 202, 203, 204 }
                );
            var root = fixture.PointSet.Root.Value;
            ClassicAssert.IsTrue(root.IsLeaf);
            ClassicAssert.IsFalse(root.HasKdTree);

            var result = fixture.PointSet.QueryPointsNearPoint(query, 0.35, 3);

            AssertResult(result, fixture, query, new[] { 0, 1, 2 }, i => 200 + i);
            ClassicAssert.IsFalse(root.HasKdTree);
        }

        [Test]
        public void SubdividedTemporaryTreePreservesQueryAcrossEmptyOctant()
        {
            var query = new V3d(0.25, 0.25, 0.25);
            var fixture = CreateFixture(
                new[]
                {
                    new V3d(0.55, 0.55, 0.55),
                    new V3d(0.65, 0.55, 0.55),
                    new V3d(0.55, 0.70, 0.55),
                    new V3d(0.55, 0.55, 0.80),
                    new V3d(0.75, 0.75, 0.55),
                    new V3d(0.90, 0.60, 0.70)
                },
                splitLimit: 2,
                isTemporary: true,
                partIndices: 42
                );
            var root = fixture.PointSet.Root.Value;
            ClassicAssert.IsFalse(root.IsLeaf);
            ClassicAssert.IsNull(root.Subnodes![root.GetSubIndex(query)]);
            ClassicAssert.IsTrue(Leaves(root).All(leaf => !leaf.HasKdTree));
            var expected = BruteForceIndices(fixture.Positions, query, 2.0, 3);

            var result = fixture.PointSet.QueryPointsNearPoint(query, 2.0, 3);

            AssertResult(result, fixture, query, expected, _ => 42);
            ClassicAssert.AreEqual(query, result.Object);
            ClassicAssert.IsTrue(Leaves(root).All(leaf => !leaf.HasKdTree));
        }

        [Test]
        public void TemporaryAndProcessedTreesMatchBruteForce()
        {
            var random = new Random(1977);
            var positions = new V3d[96].SetByIndex(_ => new V3d(
                0.01 + random.NextDouble() * 0.98,
                0.01 + random.NextDouble() * 0.98,
                0.01 + random.NextDouble() * 0.98
                ));
            var perPointParts = new int[positions.Length].SetByIndex(i => 300 + i);
            var temporary = CreateFixture(positions, 4, isTemporary: true, perPointParts);
            var processed = CreateFixture(positions, 4, isTemporary: false, perPointParts);
            var query = new V3d(0.31, 0.43, 0.57);
            const double radius = 0.5;
            const int maxCount = 11;
            var expected = BruteForceIndices(processed.Positions, query, radius, maxCount);

            ClassicAssert.IsTrue(Leaves(temporary.PointSet.Root.Value).All(leaf => !leaf.HasKdTree));
            ClassicAssert.IsTrue(Leaves(processed.PointSet.Root.Value).All(leaf => leaf.HasKdTree));

            var scanned = temporary.PointSet.QueryPointsNearPoint(query, radius, maxCount);
            var indexed = processed.PointSet.QueryPointsNearPoint(query, radius, maxCount);

            AssertResult(scanned, temporary, query, expected, i => perPointParts[i]);
            AssertResult(indexed, processed, query, expected, i => perPointParts[i]);

            var scannedDistances = DistancesBySourceIndex(scanned);
            var indexedDistances = DistancesBySourceIndex(indexed);
            foreach (var sourceIndex in expected)
                ClassicAssert.AreEqual(indexedDistances[sourceIndex], scannedDistances[sourceIndex], 1e-7);
        }

        [Test]
        public void ZeroMaxCountIsAlwaysEmpty()
        {
            var positions = new[]
            {
                new V3d(0.2, 0.2, 0.2),
                new V3d(0.4, 0.4, 0.4),
                new V3d(0.8, 0.8, 0.8)
            };
            var temporary = CreateFixture(positions, 16, isTemporary: true, partIndices: null);
            var processed = CreateFixture(positions, 16, isTemporary: false, partIndices: null);

            var scanned = temporary.PointSet.QueryPointsNearPoint(V3d.Zero, double.MaxValue, 0);
            var indexed = processed.PointSet.QueryPointsNearPoint(V3d.Zero, double.MaxValue, 0);
            var nodeResult = temporary.PointSet.Root.Value.QueryPointsNearPoint(V3d.Zero, double.MaxValue, 0);

            ClassicAssert.IsTrue(scanned.IsEmpty);
            ClassicAssert.IsTrue(indexed.IsEmpty);
            ClassicAssert.IsTrue(nodeResult.IsEmpty);
            ClassicAssert.AreEqual(0, scanned.Count);
            ClassicAssert.AreEqual(0, indexed.Count);
            ClassicAssert.AreEqual(0, nodeResult.Count);
        }

        [Test]
        public void FiniteRadiusReturnsEveryAvailablePointBelowCap()
        {
            var query = new V3d(0.1, 0.1, 0.1);
            var positions = new[]
            {
                query + new V3d(0.05, 0.0, 0.0),
                query + new V3d(0.15, 0.0, 0.0),
                query + new V3d(0.25, 0.0, 0.0),
                query + new V3d(0.35, 0.0, 0.0),
                query + new V3d(0.55, 0.0, 0.0)
            };
            var fixture = CreateFixture(positions, 1, isTemporary: true, partIndices: 7);
            var expected = BruteForceIndices(fixture.Positions, query, 0.4, 10);

            var result = fixture.PointSet.QueryPointsNearPoint(query, 0.4, 10);

            AssertResult(result, fixture, query, expected, _ => 7);
            ClassicAssert.AreEqual(4, result.Count);
        }

        private sealed class Fixture
        {
            public PointSet PointSet { get; }
            public V3d[] Positions { get; }
            public C4b[] Colors { get; }
            public V3f[] Normals { get; }
            public int[] Intensities { get; }
            public byte[] Classifications { get; }

            public Fixture(
                PointSet pointSet,
                V3d[] positions,
                C4b[] colors,
                V3f[] normals,
                int[] intensities,
                byte[] classifications
                )
            {
                PointSet = pointSet;
                Positions = positions;
                Colors = colors;
                Normals = normals;
                Intensities = intensities;
                Classifications = classifications;
            }
        }

        private static Fixture CreateFixture(
            V3d[] positions,
            int splitLimit,
            bool isTemporary,
            object partIndices
            )
        {
            var colors = new C4b[positions.Length].SetByIndex(i =>
                new C4b((byte)(i + 1), (byte)(i + 21), (byte)(i + 41), byte.MaxValue)
                );
            var normals = new V3f[positions.Length].SetByIndex(i => new V3f(i + 1, i + 2, i + 3).Normalized);
            var intensities = new int[positions.Length].SetByIndex(i => 1000 + i);
            var classifications = new byte[positions.Length].SetByIndex(i => (byte)(50 + i));
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var root = InMemoryPointSet.Build(
                positions, colors, normals, intensities, classifications, partIndices,
                Cell.Unit, splitLimit
                ).ToPointSetNode(storage, isTemporaryImportNode: true);
            var pointSet = new PointSet(storage, Guid.NewGuid().ToString(), root.Id, splitLimit);
            if (!isTemporary)
                pointSet = pointSet.GenerateLod(ImportConfig.Default.WithRandomKey());
            var storedPositions = new V3d[positions.Length];
            foreach (var chunk in pointSet.QueryAllPoints())
            {
                for (var i = 0; i < chunk.Count; i++)
                    storedPositions[chunk.Intensities[i] - 1000] = chunk.Positions[i];
            }
            return new Fixture(pointSet, storedPositions, colors, normals, intensities, classifications);
        }

        private static IEnumerable<IPointCloudNode> Leaves(IPointCloudNode node)
        {
            if (node.IsLeaf)
            {
                yield return node;
                yield break;
            }

            foreach (var subnode in node.Subnodes!)
            {
                if (subnode == null) continue;
                foreach (var leaf in Leaves(subnode.Value)) yield return leaf;
            }
        }

        private static int[] BruteForceIndices(
            V3d[] positions,
            V3d query,
            double radius,
            int maxCount
            )
            => positions
                .Select((position, index) => (Index: index, Distance: (position - query).Length))
                .Where(x => x.Distance <= radius)
                .OrderBy(x => x.Distance)
                .Take(maxCount)
                .Select(x => x.Index)
                .ToArray();

        private static Dictionary<int, double> DistancesBySourceIndex(PointsNearObject<V3d> result)
        {
            var distances = new Dictionary<int, double>();
            for (var i = 0; i < result.Count; i++)
                distances.Add(result.Intensities![i] - 1000, result.Distances![i]);
            return distances;
        }

        private static void AssertResult(
            PointsNearObject<V3d> result,
            Fixture fixture,
            V3d query,
            int[] expectedIndices,
            Func<int, int> expectedPartIndex
            )
        {
            ClassicAssert.AreEqual(query, result.Object);
            ClassicAssert.AreEqual(expectedIndices.Length, result.Count);
            ClassicAssert.IsNotNull(result.Colors);
            ClassicAssert.IsNotNull(result.Normals);
            ClassicAssert.IsNotNull(result.Intensities);
            ClassicAssert.IsNotNull(result.Classifications);
            ClassicAssert.IsNotNull(result.PartIndices);
            ClassicAssert.IsNotNull(result.Distances);
            ClassicAssert.AreEqual(result.Count, result.Colors!.Length);
            ClassicAssert.AreEqual(result.Count, result.Normals!.Length);
            ClassicAssert.AreEqual(result.Count, result.Intensities!.Length);
            ClassicAssert.AreEqual(result.Count, result.Classifications!.Length);
            ClassicAssert.AreEqual(result.Count, result.PartIndices!.Length);
            ClassicAssert.AreEqual(result.Count, result.Distances!.Length);

            var expected = expectedIndices.ToHashSet();
            var actual = new HashSet<int>();
            for (var i = 0; i < result.Count; i++)
            {
                var sourceIndex = result.Intensities[i] - 1000;
                ClassicAssert.IsTrue(expected.Contains(sourceIndex));
                ClassicAssert.IsTrue(actual.Add(sourceIndex));
                ClassicAssert.AreEqual(fixture.Positions[sourceIndex], result.Positions[i]);
                ClassicAssert.AreEqual(fixture.Colors[sourceIndex], result.Colors[i]);
                ClassicAssert.AreEqual(fixture.Normals[sourceIndex], result.Normals[i]);
                ClassicAssert.AreEqual(fixture.Intensities[sourceIndex], result.Intensities[i]);
                ClassicAssert.AreEqual(fixture.Classifications[sourceIndex], result.Classifications[i]);
                ClassicAssert.AreEqual(expectedPartIndex(sourceIndex), result.PartIndices[i]);
                ClassicAssert.AreEqual((result.Positions[i] - query).Length, result.Distances[i], 1e-6);
            }
            ClassicAssert.IsTrue(expected.SetEquals(actual));
        }
    }
}
