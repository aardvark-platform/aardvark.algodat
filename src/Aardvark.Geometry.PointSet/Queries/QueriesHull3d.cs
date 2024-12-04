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
using System.Linq;

namespace Aardvark.Geometry.Points;

/// <summary>
/// </summary>
public static partial class Queries
{
    #region Query points

    /// <summary>
    /// All points inside convex hull (including boundary).
    /// </summary>
    public static IEnumerable<Chunk> QueryPointsInsideConvexHull(
        this PointSet self, Hull3d query, int minCellExponent = int.MinValue
        )
        => QueryPointsInsideConvexHull(self.Root.Value, query, minCellExponent);

    /// <summary>
    /// All points inside convex hull (including boundary).
    /// </summary>
    public static IEnumerable<Chunk> QueryPointsInsideConvexHull(
        this IPointCloudNode self, Hull3d query, int minCellExponent = int.MinValue
        )
        => QueryPoints(self,
            n => query.Contains(n.BoundingBoxExactGlobal),
            n => !query.Intersects(n.BoundingBoxExactGlobal),
            p => query.Contains(p),
            minCellExponent);

    /// <summary>
    /// All points outside convex hull (excluding boundary).
    /// </summary>
    public static IEnumerable<Chunk> QueryPointsOutsideConvexHull(
        this PointSet self, Hull3d query, int minCellExponent = int.MinValue
        )
        => QueryPointsOutsideConvexHull(self.Root.Value, query, minCellExponent);

    /// <summary>
    /// All points outside convex hull (excluding boundary).
    /// </summary>
    public static IEnumerable<Chunk> QueryPointsOutsideConvexHull(
        this IPointCloudNode self, Hull3d query, int minCellExponent = int.MinValue
        )
        => QueryPointsInsideConvexHull(self, query.Reversed(), minCellExponent);

    #endregion

    #region Count exact

    /// <summary>
    /// Counts points inside convex hull.
    /// </summary>
    internal static long CountPointsInsideConvexHull(
        this PointSet self, Hull3d query, int minCellExponent = int.MinValue
        )
        => CountPointsInsideConvexHull(self.Root.Value, query, minCellExponent);

    /// <summary>
    /// Counts points inside convex hull.
    /// </summary>
    internal static long CountPointsInsideConvexHull(
        this IPointCloudNode self, Hull3d query, int minCellExponent = int.MinValue
        )
        => CountPoints(self,
            n => query.Contains(n.BoundingBoxExactGlobal),
            n => !query.Intersects(n.BoundingBoxExactGlobal),
            p => query.Contains(p),
            minCellExponent);

    /// <summary>
    /// Counts points outside convex hull.
    /// </summary>
    internal static long CountPointsOutsideConvexHull(
        this PointSet self, Hull3d query, int minCellExponent = int.MinValue
        )
        => CountPointsOutsideConvexHull(self.Root.Value, query, minCellExponent);

    /// <summary>
    /// Counts points outside convex hull.
    /// </summary>
    internal static long CountPointsOutsideConvexHull(
        this IPointCloudNode self, Hull3d query, int minCellExponent = int.MinValue
        )
        => CountPointsInsideConvexHull(self, query.Reversed(), minCellExponent);

    #endregion

    #region Count approximately

    /// <summary>
    /// Counts points inside convex hull (approximately).
    /// Result is always equal or greater than exact number.
    /// </summary>
    internal static long CountPointsApproximatelyInsideConvexHull(
        this PointSet self, Hull3d query, int minCellExponent = int.MinValue
        )
        => CountPointsApproximatelyInsideConvexHull(self.Root.Value, query, minCellExponent);

    /// <summary>
    /// Counts points inside convex hull (approximately).
    /// Result is always equal or greater than exact number.
    /// </summary>
    internal static long CountPointsApproximatelyInsideConvexHull(
        this IPointCloudNode self, Hull3d query, int minCellExponent = int.MinValue
        )
        => CountPointsApproximately(self,
            n => query.Contains(n.BoundingBoxExactGlobal),
            n => !query.Intersects(n.BoundingBoxExactGlobal),
            minCellExponent);

    /// <summary>
    /// Counts points outside convex hull (approximately).
    /// Result is always equal or greater than exact number.
    /// </summary>
    internal static long CountPointsApproximatelyOutsideConvexHull(
        this PointSet self, Hull3d query, int minCellExponent = int.MinValue
        )
        => CountPointsApproximatelyOutsideConvexHull(self.Root.Value, query, minCellExponent);

    /// <summary>
    /// Counts points outside convex hull (approximately).
    /// Result is always equal or greater than exact number.
    /// </summary>
    internal static long CountPointsApproximatelyOutsideConvexHull(
        this IPointCloudNode self, Hull3d query, int minCellExponent = int.MinValue
        )
        => CountPointsApproximately(self,
            n => !query.Intersects(n.BoundingBoxExactGlobal),
            n => query.Contains(n.BoundingBoxExactGlobal),
            minCellExponent);

    #endregion
}
