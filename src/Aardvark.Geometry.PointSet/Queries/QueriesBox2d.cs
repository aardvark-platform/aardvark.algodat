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
    #region Query points

    /// <summary>
    /// All points p.XY inside axis-aligned box (including boundary).
    /// </summary>
    public static IEnumerable<Chunk> QueryPointsInsideBoxXY(
        this PointSet self, Box2d query, int minCellExponent = int.MinValue
        )
        => QueryPointsInsideBoxXY(self.Root.Value.ToPointNode(), query, minCellExponent);

    /// <summary>
    /// All points p.XY inside axis-aligned box (including boundary).
    /// </summary>
    public static IEnumerable<Chunk> QueryPointsInsideBoxXY(
        this IPointNode self, Box2d query, int minCellExponent = int.MinValue
        )
        => QueryPoints(self,
            n => query.Contains(n.CellBounds.XY),
            n => !query.Intersects(n.CellBounds.XY),
            p => query.Contains(p.XY),
            minCellExponent);

    /// <summary>
    /// All points p.XY inside axis-aligned box (including boundary).
    /// </summary>
    public static bool QueryContainsPointsInsideBoxXY(
        this IPointNode self, Box2d query, int minCellExponent = int.MinValue
        )
        => QueryContainsPoints(self,
            n => query.Contains(n.CellBounds.XY),
            n => !query.Intersects(n.CellBounds.XY),
            p => query.Contains(p.XY),
            minCellExponent);

    /// <summary>
    /// All points p.XY outside axis-aligned box (excluding boundary).
    /// </summary>
    public static IEnumerable<Chunk> QueryPointsOutsideBoxXY(
        this PointSet self, Box2d query, int minCellExponent = int.MinValue
        )
        => QueryPointsOutsideBoxXY(self.Root.Value.ToPointNode(), query, minCellExponent);

    /// <summary>
    /// All points p.Y outside axis-aligned box (excluding boundary).
    /// </summary>
    public static IEnumerable<Chunk> QueryPointsOutsideBoxXY(
        this IPointNode self, Box2d query, int minCellExponent = int.MinValue
        )
        => QueryPoints(self,
            n => !query.Intersects(n.DataBounds.XY),
            n => query.Contains(n.DataBounds.XY),
            p => !query.Contains(p.XY),
            minCellExponent);

    #endregion

    #region Count exact

    /// <summary>
    /// Counts points p.XY inside axis-aligned box.
    /// </summary>
    public static long CountPointsInsideBoxXY(
        this PointSet self, Box2d query, int minCellExponent = int.MinValue
        )
        => CountPointsInsideBoxXY(self.Root.Value.ToPointNode(), query, minCellExponent);

    /// <summary>
    /// Counts points p.XY inside axis-aligned box.
    /// </summary>
    public static long CountPointsInsideBoxXY(
        this IPointNode self, Box2d query, int minCellExponent = int.MinValue
        )
        => CountPoints(self,
            n => query.Contains(n.DataBounds.XY),
            n => !query.Intersects(n.DataBounds.XY),
            p => query.Contains(p.XY),
            minCellExponent);

    /// <summary>
    /// Counts points p.XY outside axis-aligned box.
    /// </summary>
    public static long CountPointsOutsideBoxXY(
        this PointSet self, Box2d query, int minCellExponent = int.MinValue
        )
        => CountPointsOutsideBoxXY(self.Root.Value.ToPointNode(), query, minCellExponent);

    /// <summary>
    /// Counts points p.XY outside axis-aligned box.
    /// </summary>
    public static long CountPointsOutsideBoxXY(
        this IPointNode self, Box2d query, int minCellExponent = int.MinValue
        )
        => CountPoints(self,
            n => !query.Intersects(n.DataBounds.XY),
            n => query.Contains(n.DataBounds.XY),
            p => !query.Contains(p.XY),
            minCellExponent);

    #endregion

    #region Count approximately

    /// <summary>
    /// Counts points p.XY approximately inside axis-aligned box (cell granularity).
    /// Result is always equal or greater than exact number.
    /// Faster than CountPointsInsideBoxXY.
    /// </summary>
    public static long CountPointsApproximatelyInsideBoxXY(
        this PointSet self, Box2d query, int minCellExponent = int.MinValue
        )
        => CountPointsApproximatelyInsideBoxXY(self.Root.Value.ToPointNode(), query, minCellExponent);

    /// <summary>
    /// Counts points p.XY approximately inside axis-aligned box (cell granularity).
    /// Result is always equal or greater than exact number.
    /// Faster than CountPointsInsideBoxXY.
    /// </summary>
    public static long CountPointsApproximatelyInsideBoxXY(
        this IPointNode self, Box2d query, int minCellExponent = int.MinValue
        )
        => CountPointsApproximately(self,
            n => query.Contains(n.DataBounds.XY),
            n => !query.Intersects(n.DataBounds.XY),
            minCellExponent);

    /// <summary>
    /// Counts points p.XY approximately outside axis-aligned box (cell granularity).
    /// Result is always equal or greater than exact number.
    /// Faster than CountPointsOutsideBoxXY.
    /// </summary>
    public static long CountPointsApproximatelyOutsideBoxXY(
        this PointSet self, Box2d query, int minCellExponent = int.MinValue
        )
        => CountPointsApproximatelyOutsideBoxXY(self.Root.Value.ToPointNode(), query, minCellExponent);

    /// <summary>
    /// Counts points p.XY approximately outside axis-aligned box (cell granularity).
    /// Result is always equal or greater than exact number.
    /// Faster than CountPointsOutsideBoxXY.
    /// </summary>
    public static long CountPointsApproximatelyOutsideBoxXY(
        this IPointNode self, Box2d query, int minCellExponent = int.MinValue
        )
        => CountPointsApproximately(self,
            n => !query.Intersects(n.DataBounds.XY),
            n => query.Contains(n.DataBounds.XY),
            minCellExponent);

    #endregion
}
