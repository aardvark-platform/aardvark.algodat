/*
    Copyright (C) 2006-2024. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System.Collections.Generic;

namespace Aardvark.Geometry.Points;

/// <summary>
/// </summary>
public static partial class Queries
{
    private static bool IsOutsideClosedBox(Box2d query, Box2d bounds)
        => bounds.Max.X < query.Min.X || bounds.Min.X > query.Max.X
        || bounds.Max.Y < query.Min.Y || bounds.Min.Y > query.Max.Y;

    #region Query points

    /// <summary>
    /// All points p.XY inside axis-aligned box (including boundary).
    /// Bounds are pruned only when strictly separated from the closed query box.
    /// </summary>
    public static IEnumerable<Chunk> QueryPointsInsideBoxXY(
        this PointSet self, Box2d query, int minCellExponent = int.MinValue
        )
        => QueryPointsInsideBoxXY(self.Root.Value, query, minCellExponent);

    /// <summary>
    /// All points p.XY inside axis-aligned box (including boundary).
    /// Cell bounds touching the closed query box are not pruned.
    /// </summary>
    public static IEnumerable<Chunk> QueryPointsInsideBoxXY(
        this IPointCloudNode self, Box2d query, int minCellExponent = int.MinValue
        )
        => QueryPoints(self,
            n => query.Contains(n.Cell.BoundingBox.XY),
            n => IsOutsideClosedBox(query, n.Cell.BoundingBox.XY),
            p => query.Contains(p.XY),
            minCellExponent);

    /// <summary>
    /// Tests whether points p.XY lie inside the closed axis-aligned box.
    /// Cell bounds are pruned only when strictly separated.
    /// </summary>
    public static bool QueryContainsPointsInsideBoxXY(
        this IPointCloudNode self, Box2d query, int minCellExponent = int.MinValue
        )
        => QueryContainsPoints(self,
            n => query.Contains(n.Cell.BoundingBox.XY),
            n => IsOutsideClosedBox(query, n.Cell.BoundingBox.XY),
            p => query.Contains(p.XY),
            minCellExponent);

    /// <summary>
    /// All points p.XY outside axis-aligned box (excluding boundary).
    /// </summary>
    public static IEnumerable<Chunk> QueryPointsOutsideBoxXY(
        this PointSet self, Box2d query, int minCellExponent = int.MinValue
        )
        => QueryPointsOutsideBoxXY(self.Root.Value, query, minCellExponent);

    /// <summary>
    /// All points p.Y outside axis-aligned box (excluding boundary).
    /// </summary>
    public static IEnumerable<Chunk> QueryPointsOutsideBoxXY(
        this IPointCloudNode self, Box2d query, int minCellExponent = int.MinValue
        )
        => QueryPoints(self,
            n => !query.Intersects(n.BoundingBoxExactGlobal.XY),
            n => query.Contains(n.BoundingBoxExactGlobal.XY),
            p => !query.Contains(p.XY),
            minCellExponent);

    #endregion

    #region Count exact

    /// <summary>
    /// Counts points p.XY inside the closed axis-aligned box, including boundary contacts.
    /// </summary>
    public static long CountPointsInsideBoxXY(
        this PointSet self, Box2d query, int minCellExponent = int.MinValue
        )
        => CountPointsInsideBoxXY(self.Root.Value, query, minCellExponent);

    /// <summary>
    /// Counts points p.XY inside the closed axis-aligned box.
    /// Exact bounds are pruned only when strictly separated.
    /// </summary>
    public static long CountPointsInsideBoxXY(
        this IPointCloudNode self, Box2d query, int minCellExponent = int.MinValue
        )
        => CountPoints(self,
            n => query.Contains(n.BoundingBoxExactGlobal.XY),
            n => IsOutsideClosedBox(query, n.BoundingBoxExactGlobal.XY),
            p => query.Contains(p.XY),
            minCellExponent);

    /// <summary>
    /// Counts points p.XY outside axis-aligned box.
    /// </summary>
    public static long CountPointsOutsideBoxXY(
        this PointSet self, Box2d query, int minCellExponent = int.MinValue
        )
        => CountPointsOutsideBoxXY(self.Root.Value, query, minCellExponent);

    /// <summary>
    /// Counts points p.XY outside axis-aligned box.
    /// </summary>
    public static long CountPointsOutsideBoxXY(
        this IPointCloudNode self, Box2d query, int minCellExponent = int.MinValue
        )
        => CountPoints(self,
            n => !query.Intersects(n.BoundingBoxExactGlobal.XY),
            n => query.Contains(n.BoundingBoxExactGlobal.XY),
            p => !query.Contains(p.XY),
            minCellExponent);

    #endregion

    #region Count approximately

    /// <summary>
    /// Counts points p.XY approximately inside the closed axis-aligned box (cell granularity).
    /// Touching bounds are retained; result is always equal or greater than exact number.
    /// Faster than CountPointsInsideBoxXY.
    /// </summary>
    public static long CountPointsApproximatelyInsideBoxXY(
        this PointSet self, Box2d query, int minCellExponent = int.MinValue
        )
        => CountPointsApproximatelyInsideBoxXY(self.Root.Value, query, minCellExponent);

    /// <summary>
    /// Counts points p.XY approximately inside the closed axis-aligned box (cell granularity).
    /// Exact bounds are pruned only when strictly separated.
    /// Result is always equal or greater than exact number.
    /// Faster than CountPointsInsideBoxXY.
    /// </summary>
    public static long CountPointsApproximatelyInsideBoxXY(
        this IPointCloudNode self, Box2d query, int minCellExponent = int.MinValue
        )
        => CountPointsApproximately(self,
            n => query.Contains(n.BoundingBoxExactGlobal.XY),
            n => IsOutsideClosedBox(query, n.BoundingBoxExactGlobal.XY),
            minCellExponent);

    /// <summary>
    /// Counts points p.XY approximately outside axis-aligned box (cell granularity).
    /// Result is always equal or greater than exact number.
    /// Faster than CountPointsOutsideBoxXY.
    /// </summary>
    public static long CountPointsApproximatelyOutsideBoxXY(
        this PointSet self, Box2d query, int minCellExponent = int.MinValue
        )
        => CountPointsApproximatelyOutsideBoxXY(self.Root.Value, query, minCellExponent);

    /// <summary>
    /// Counts points p.XY approximately outside axis-aligned box (cell granularity).
    /// Result is always equal or greater than exact number.
    /// Faster than CountPointsOutsideBoxXY.
    /// </summary>
    public static long CountPointsApproximatelyOutsideBoxXY(
        this IPointCloudNode self, Box2d query, int minCellExponent = int.MinValue
        )
        => CountPointsApproximately(self,
            n => !query.Intersects(n.BoundingBoxExactGlobal.XY),
            n => query.Contains(n.BoundingBoxExactGlobal.XY),
            minCellExponent);

    #endregion
}
