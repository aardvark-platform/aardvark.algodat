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
using System;
using System.Collections.Generic;
using System.Linq;
using Aardvark.Base;
using Aardvark.Data.Points;
using Aardvark.Geometry.Points;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Aardvark.Geometry.Tests;

[TestFixture]
public class PlaneQueryTests
{
    private const int Side = 32;
    private const double MaxDistance = 0.02;

    private PointSet _pointSet;

    [OneTimeSetUp]
    public void SetUp()
    {
        var positions = new V3d[Side * Side * Side];
        var index = 0;
        for (var x = 0; x < Side; x++)
        for (var y = 0; y < Side; y++)
        for (var z = 0; z < Side; z++)
            positions[index++] = new V3d(
                (x + 0.5) / Side,
                (y + 0.5) / Side,
                (z + 0.5) / Side
                );

        var config = ImportConfig.Default
            .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
            .WithKey("plane-query-subtree-regression")
            .WithOctreeSplitLimit(32);
        _pointSet = PointCloud.Chunks(new Chunk(positions), config);

        ClassicAssert.IsTrue(_pointSet.Root.Value.IsNotLeaf());
    }

    [Test]
    public void PlaneQueriesClassifyCurrentSubtrees()
    {
        var node = _pointSet.Root.Value;
        var plane = new Plane3d(V3d.ZAxis, new V3d(0.0, 0.0, 0.46875));
        var planes = new[]
        {
            plane,
            new Plane3d(V3d.XAxis, new V3d(0.21875, 0.0, 0.0))
        };
        var all = _pointSet.QueryAllPoints().SelectMany(chunk => chunk.Positions).ToArray();

        var nearPlane = all.Where(p => IsNear(p, plane)).ToArray();
        var nearPlanes = all.Where(p => planes.Any(q => IsNear(p, q))).ToArray();
        var notNearPlane = all.Where(p => !IsNear(p, plane)).ToArray();
        var notNearPlanes = all.Where(p => !planes.Any(q => IsNear(p, q))).ToArray();

        AssertExact(
            nearPlane,
            _pointSet.QueryPointsNearPlane(plane, MaxDistance),
            node.QueryPointsNearPlane(plane, MaxDistance),
            _pointSet.CountPointsNearPlane(plane, MaxDistance),
            node.CountPointsNearPlane(plane, MaxDistance)
            );
        AssertExact(
            nearPlanes,
            _pointSet.QueryPointsNearPlanes(planes, MaxDistance),
            node.QueryPointsNearPlanes(planes, MaxDistance),
            _pointSet.CountPointsNearPlanes(planes, MaxDistance),
            node.CountPointsNearPlanes(planes, MaxDistance)
            );
        AssertExact(
            notNearPlane,
            _pointSet.QueryPointsNotNearPlane(plane, MaxDistance),
            node.QueryPointsNotNearPlane(plane, MaxDistance),
            _pointSet.CountPointsNotNearPlane(plane, MaxDistance),
            node.CountPointsNotNearPlane(plane, MaxDistance)
            );
        AssertExact(
            notNearPlanes,
            _pointSet.QueryPointsNotNearPlanes(planes, MaxDistance),
            node.QueryPointsNotNearPlanes(planes, MaxDistance),
            _pointSet.CountPointsNotNearPlanes(planes, MaxDistance),
            node.CountPointsNotNearPlanes(planes, MaxDistance)
            );

        AssertApproximate(
            nearPlane.LongLength, all.LongLength,
            _pointSet.CountPointsApproximatelyNearPlane(plane, MaxDistance),
            node.CountPointsApproximatelyNearPlane(plane, MaxDistance),
            mustPrune: true
            );
        AssertApproximate(
            nearPlanes.LongLength, all.LongLength,
            _pointSet.CountPointsApproximatelyNearPlanes(planes, MaxDistance),
            node.CountPointsApproximatelyNearPlanes(planes, MaxDistance),
            mustPrune: true
            );
        AssertApproximate(
            notNearPlane.LongLength, all.LongLength,
            _pointSet.CountPointsApproximatelyNotNearPlane(plane, MaxDistance),
            node.CountPointsApproximatelyNotNearPlane(plane, MaxDistance),
            mustPrune: true
            );
        AssertApproximate(
            notNearPlanes.LongLength, all.LongLength,
            _pointSet.CountPointsApproximatelyNotNearPlanes(planes, MaxDistance),
            node.CountPointsApproximatelyNotNearPlanes(planes, MaxDistance),
            mustPrune: true
            );
    }

    [Test]
    public void PlaneQueriesRespectMinCellExponent()
    {
        const int minCellExponent = -3;
        var node = _pointSet.Root.Value;
        var plane = new Plane3d(V3d.ZAxis, new V3d(0.0, 0.0, 0.46875));
        var planes = new[]
        {
            plane,
            new Plane3d(V3d.XAxis, new V3d(0.21875, 0.0, 0.0))
        };
        var front = node.QueryAllPoints(minCellExponent)
            .SelectMany(chunk => chunk.Positions)
            .ToArray();

        ClassicAssert.IsTrue(front.LongLength > 0);
        ClassicAssert.IsTrue(front.LongLength < _pointSet.PointCount);

        var nearPlane = front.Where(p => IsNear(p, plane)).ToArray();
        var nearPlanes = front.Where(p => planes.Any(q => IsNear(p, q))).ToArray();
        var notNearPlane = front.Where(p => !IsNear(p, plane)).ToArray();
        var notNearPlanes = front.Where(p => !planes.Any(q => IsNear(p, q))).ToArray();

        AssertExact(
            nearPlane,
            _pointSet.QueryPointsNearPlane(plane, MaxDistance, minCellExponent),
            node.QueryPointsNearPlane(plane, MaxDistance, minCellExponent),
            _pointSet.CountPointsNearPlane(plane, MaxDistance, minCellExponent),
            node.CountPointsNearPlane(plane, MaxDistance, minCellExponent)
            );
        AssertExact(
            nearPlanes,
            _pointSet.QueryPointsNearPlanes(planes, MaxDistance, minCellExponent),
            node.QueryPointsNearPlanes(planes, MaxDistance, minCellExponent),
            _pointSet.CountPointsNearPlanes(planes, MaxDistance, minCellExponent),
            node.CountPointsNearPlanes(planes, MaxDistance, minCellExponent)
            );
        AssertExact(
            notNearPlane,
            _pointSet.QueryPointsNotNearPlane(plane, MaxDistance, minCellExponent),
            node.QueryPointsNotNearPlane(plane, MaxDistance, minCellExponent),
            _pointSet.CountPointsNotNearPlane(plane, MaxDistance, minCellExponent),
            node.CountPointsNotNearPlane(plane, MaxDistance, minCellExponent)
            );
        AssertExact(
            notNearPlanes,
            _pointSet.QueryPointsNotNearPlanes(planes, MaxDistance, minCellExponent),
            node.QueryPointsNotNearPlanes(planes, MaxDistance, minCellExponent),
            _pointSet.CountPointsNotNearPlanes(planes, MaxDistance, minCellExponent),
            node.CountPointsNotNearPlanes(planes, MaxDistance, minCellExponent)
            );

        AssertApproximate(
            nearPlane.LongLength, front.LongLength,
            _pointSet.CountPointsApproximatelyNearPlane(plane, MaxDistance, minCellExponent),
            node.CountPointsApproximatelyNearPlane(plane, MaxDistance, minCellExponent),
            mustPrune: true
            );
        AssertApproximate(
            nearPlanes.LongLength, front.LongLength,
            _pointSet.CountPointsApproximatelyNearPlanes(planes, MaxDistance, minCellExponent),
            node.CountPointsApproximatelyNearPlanes(planes, MaxDistance, minCellExponent),
            mustPrune: true
            );
        AssertApproximate(
            notNearPlane.LongLength, front.LongLength,
            _pointSet.CountPointsApproximatelyNotNearPlane(plane, MaxDistance, minCellExponent),
            node.CountPointsApproximatelyNotNearPlane(plane, MaxDistance, minCellExponent),
            mustPrune: false
            );
        AssertApproximate(
            notNearPlanes.LongLength, front.LongLength,
            _pointSet.CountPointsApproximatelyNotNearPlanes(planes, MaxDistance, minCellExponent),
            node.CountPointsApproximatelyNotNearPlanes(planes, MaxDistance, minCellExponent),
            mustPrune: false
            );
    }

    private static bool IsNear(V3d position, Plane3d plane)
        => Math.Abs(plane.Height(position)) <= MaxDistance;

    private static void AssertExact(
        V3d[] expected,
        IEnumerable<Chunk> pointSetResult,
        IEnumerable<Chunk> nodeResult,
        long pointSetCount,
        long nodeCount
        )
    {
        var expectedSet = expected.ToHashSet();
        ClassicAssert.AreEqual(expected.LongLength, expectedSet.Count);

        AssertPositions(expectedSet, expected.LongLength, pointSetResult);
        AssertPositions(expectedSet, expected.LongLength, nodeResult);
        ClassicAssert.AreEqual(expected.LongLength, pointSetCount);
        ClassicAssert.AreEqual(expected.LongLength, nodeCount);
    }

    private static void AssertPositions(
        HashSet<V3d> expected,
        long expectedCount,
        IEnumerable<Chunk> chunks
        )
    {
        var actual = chunks.SelectMany(chunk => chunk.Positions).ToArray();
        ClassicAssert.AreEqual(expectedCount, actual.LongLength);
        ClassicAssert.IsTrue(expected.SetEquals(actual));
    }

    private static void AssertApproximate(
        long exact,
        long available,
        long pointSetCount,
        long nodeCount,
        bool mustPrune
        )
    {
        ClassicAssert.AreEqual(pointSetCount, nodeCount);
        ClassicAssert.GreaterOrEqual(pointSetCount, exact);
        ClassicAssert.LessOrEqual(pointSetCount, available);
        if (mustPrune) ClassicAssert.Less(pointSetCount, available);
    }
}
