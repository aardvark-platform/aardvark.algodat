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
using System;
using System.Collections.Generic;
using System.Linq;
using Aardvark.Base;
using Aardvark.Data.Points;

namespace Aardvark.Geometry.Points;

/// <summary>
/// </summary>
public static partial class Queries
{
    #region Query points

    /// <summary>
    /// All points within maxDistance of given plane.
    /// </summary>
    public static IEnumerable<Chunk> QueryPointsNearPlane(
        this PointSet self, Plane3d plane, double maxDistance, int minCellExponent = int.MinValue
        )
        => QueryPointsNearPlane(self.Root.Value.ToPointNode(), plane, maxDistance, minCellExponent);

    /// <summary>
    /// All points within maxDistance of given plane.
    /// </summary>
    public static IEnumerable<Chunk> QueryPointsNearPlane(
        this IPointNode node, Plane3d plane, double maxDistance, int minCellExponent = int.MinValue
        )
        => QueryPoints(node,
            n => plane.Contains(maxDistance, node.DataBounds),
            n => !node.DataBounds.Intersects(plane, maxDistance),
            p => Math.Abs(plane.Height(p)) <= maxDistance,
            minCellExponent
            );

    /// <summary>
    /// All points within maxDistance of ANY of the given planes.
    /// </summary>
    public static IEnumerable<Chunk> QueryPointsNearPlanes(
        this PointSet self, Plane3d[] planes, double maxDistance, int minCellExponent = int.MinValue
        )
        => QueryPointsNearPlanes(self.Root.Value.ToPointNode(), planes, maxDistance, minCellExponent);

    /// <summary>
    /// All points within maxDistance of ANY of the given planes.
    /// </summary>
    public static IEnumerable<Chunk> QueryPointsNearPlanes(
        this IPointNode node, Plane3d[] planes, double maxDistance, int minCellExponent = int.MinValue
        )
        => QueryPoints(node,
            n => planes.Any(plane => plane.Contains(maxDistance, node.DataBounds)),
            n => !planes.Any(plane => node.DataBounds.Intersects(plane, maxDistance)),
            p => planes.Any(plane => Math.Abs(plane.Height(p)) <= maxDistance),
            minCellExponent
            );

    /// <summary>
    /// All points NOT within maxDistance of given plane.
    /// </summary>
    public static IEnumerable<Chunk> QueryPointsNotNearPlane(
        this PointSet self, Plane3d plane, double maxDistance, int minCellExponent = int.MinValue
        )
        => QueryPointsNotNearPlane(self.Root.Value.ToPointNode(), plane, maxDistance, minCellExponent);

    /// <summary>
    /// All points NOT within maxDistance of given plane.
    /// </summary>
    public static IEnumerable<Chunk> QueryPointsNotNearPlane(
        this IPointNode node, Plane3d plane, double maxDistance, int minCellExponent = int.MinValue
        )
        => QueryPoints(node,
            n => !node.DataBounds.Intersects(plane, maxDistance),
            n => plane.Contains(maxDistance, node.DataBounds),
            p => Math.Abs(plane.Height(p)) > maxDistance,
            minCellExponent
            );

    /// <summary>
    /// All points NOT within maxDistance of ALL the given planes.
    /// </summary>
    public static IEnumerable<Chunk> QueryPointsNotNearPlanes(
        this PointSet self, Plane3d[] planes, double maxDistance, int minCellExponent = int.MinValue
        )
        => QueryPointsNotNearPlanes(self.Root.Value.ToPointNode(), planes, maxDistance, minCellExponent);

    /// <summary>
    /// All points NOT within maxDistance of ALL the given planes.
    /// </summary>
    public static IEnumerable<Chunk> QueryPointsNotNearPlanes(
        this IPointNode node, Plane3d[] planes, double maxDistance, int minCellExponent = int.MinValue
        )
        => QueryPoints(node,
            n => !planes.Any(plane => node.DataBounds.Intersects(plane, maxDistance)),
            n => planes.Any(plane => plane.Contains(maxDistance, node.DataBounds)),
            p => !planes.Any(plane => Math.Abs(plane.Height(p)) <= maxDistance),
            minCellExponent
            );

    #endregion

    #region Count exact

    /// <summary>
    /// Count points within maxDistance of given plane.
    /// </summary>
    public static long CountPointsNearPlane(
        this PointSet self, Plane3d plane, double maxDistance, int minCellExponent = int.MinValue
        )
        => CountPointsNearPlane(self.Root.Value.ToPointNode(), plane, maxDistance, minCellExponent);

    /// <summary>
    /// Count points within maxDistance of given plane.
    /// </summary>
    public static long CountPointsNearPlane(
        this IPointNode node, Plane3d plane, double maxDistance, int minCellExponent = int.MinValue
        )
        => CountPoints(node,
            n => plane.Contains(maxDistance, node.DataBounds),
            n => !node.DataBounds.Intersects(plane, maxDistance),
            p => Math.Abs(plane.Height(p)) <= maxDistance,
            minCellExponent
            );

    /// <summary>
    /// Count points within maxDistance of ANY of the given planes.
    /// </summary>
    public static long CountPointsNearPlanes(
        this PointSet self, Plane3d[] planes, double maxDistance, int minCellExponent = int.MinValue
        )
        => CountPointsNearPlanes(self.Root.Value.ToPointNode(), planes, maxDistance, minCellExponent);

    /// <summary>
    /// Count points within maxDistance of ANY of the given planes.
    /// </summary>
    public static long CountPointsNearPlanes(
        this IPointNode node, Plane3d[] planes, double maxDistance, int minCellExponent = int.MinValue
        )
        => CountPoints(node,
            n => planes.Any(plane => plane.Contains(maxDistance, node.DataBounds)),
            n => !planes.Any(plane => node.DataBounds.Intersects(plane, maxDistance)),
            p => planes.Any(plane => Math.Abs(plane.Height(p)) <= maxDistance),
            minCellExponent
            );

    /// <summary>
    /// Count points NOT within maxDistance of given plane.
    /// </summary>
    public static long CountPointsNotNearPlane(
        this PointSet self, Plane3d plane, double maxDistance, int minCellExponent = int.MinValue
        )
        => CountPointsNotNearPlane(self.Root.Value.ToPointNode(), plane, maxDistance, minCellExponent);

    /// <summary>
    /// Count points NOT within maxDistance of given plane.
    /// </summary>
    public static long CountPointsNotNearPlane(
        this IPointNode node, Plane3d plane, double maxDistance, int minCellExponent = int.MinValue
        )
        => CountPoints(node,
            n => !node.DataBounds.Intersects(plane, maxDistance),
            n => plane.Contains(maxDistance, node.DataBounds),
            p => Math.Abs(plane.Height(p)) > maxDistance,
            minCellExponent
            );

    /// <summary>
    /// Count points NOT within maxDistance of ALL the given planes.
    /// </summary>
    public static long CountPointsNotNearPlanes(
        this PointSet self, Plane3d[] planes, double maxDistance, int minCellExponent = int.MinValue
        )
        => CountPointsNotNearPlanes(self.Root.Value.ToPointNode(), planes, maxDistance, minCellExponent);

    /// <summary>
    /// Count points NOT within maxDistance of ALL the given planes.
    /// </summary>
    public static long CountPointsNotNearPlanes(
        this IPointNode node, Plane3d[] planes, double maxDistance, int minCellExponent = int.MinValue
        )
        => CountPoints(node,
            n => !planes.Any(plane => node.DataBounds.Intersects(plane, maxDistance)),
            n => planes.Any(plane => plane.Contains(maxDistance, node.DataBounds)),
            p => !planes.Any(plane => Math.Abs(plane.Height(p)) <= maxDistance),
            minCellExponent
            );

    #endregion

    #region Count approximately

    /// <summary>
    /// Count points approximately within maxDistance of given plane.
    /// Result is always equal or greater than exact number.
    /// Faster than CountPointsNearPlane.
    /// </summary>
    public static long CountPointsApproximatelyNearPlane(
        this PointSet self, Plane3d plane, double maxDistance, int minCellExponent = int.MinValue
        )
        => CountPointsApproximatelyNearPlane(self.Root.Value.ToPointNode(), plane, maxDistance, minCellExponent);

    /// <summary>
    /// Count points approximately within maxDistance of given plane.
    /// Result is always equal or greater than exact number.
    /// Faster than CountPointsNearPlane.
    /// </summary>
    public static long CountPointsApproximatelyNearPlane(
        this IPointNode node, Plane3d plane, double maxDistance, int minCellExponent = int.MinValue
        )
        => CountPointsApproximately(node,
            n => plane.Contains(maxDistance, node.DataBounds),
            n => !node.DataBounds.Intersects(plane, maxDistance),
            minCellExponent
            );

    /// <summary>
    /// Count points approximately within maxDistance of ANY of the given planes.
    /// Result is always equal or greater than exact number.
    /// Faster than CountPointsNearPlanes.
    /// </summary>
    public static long CountPointsApproximatelyNearPlanes(
        this PointSet self, Plane3d[] planes, double maxDistance, int minCellExponent = int.MinValue
        )
        => CountPointsApproximatelyNearPlanes(self.Root.Value.ToPointNode(), planes, maxDistance, minCellExponent);

    /// <summary>
    /// Count points approximately within maxDistance of ANY of the given planes.
    /// Result is always equal or greater than exact number.
    /// Faster than CountPointsNearPlanes.
    /// </summary>
    public static long CountPointsApproximatelyNearPlanes(
        this IPointNode node, Plane3d[] planes, double maxDistance, int minCellExponent = int.MinValue
        )
        => CountPointsApproximately(node,
            n => planes.Any(plane => plane.Contains(maxDistance, node.DataBounds)),
            n => !planes.Any(plane => node.DataBounds.Intersects(plane, maxDistance)),
            minCellExponent
            );

    /// <summary>
    /// Count points approximately NOT within maxDistance of given plane.
    /// Result is always equal or greater than exact number.
    /// Faster than CountPointsNotNearPlane.
    /// </summary>
    public static long CountPointsApproximatelyNotNearPlane(
        this PointSet self, Plane3d plane, double maxDistance, int minCellExponent = int.MinValue
        )
        => CountPointsApproximatelyNotNearPlane(self.Root.Value.ToPointNode(), plane, maxDistance, minCellExponent);

    /// <summary>
    /// Count points approximately NOT within maxDistance of given plane.
    /// Result is always equal or greater than exact number.
    /// Faster than CountPointsNotNearPlane.
    /// </summary>
    public static long CountPointsApproximatelyNotNearPlane(
        this IPointNode node, Plane3d plane, double maxDistance, int minCellExponent = int.MinValue
        )
        => CountPointsApproximately(node,
            n => !node.DataBounds.Intersects(plane, maxDistance),
            n => plane.Contains(maxDistance, node.DataBounds),
            minCellExponent
            );

    /// <summary>
    /// Count points approximately NOT within maxDistance of ALL the given planes.
    /// Result is always equal or greater than exact number.
    /// Faster than CountPointsNotNearPlanes.
    /// </summary>
    public static long CountPointsApproximatelyNotNearPlanes(
        this PointSet self, Plane3d[] planes, double maxDistance, int minCellExponent = int.MinValue
        )
        => CountPointsApproximatelyNotNearPlanes(self.Root.Value.ToPointNode(), planes, maxDistance, minCellExponent);

    /// <summary>
    /// Count points approximately NOT within maxDistance of ALL the given planes.
    /// Result is always equal or greater than exact number.
    /// Faster than CountPointsNotNearPlanes.
    /// </summary>
    public static long CountPointsApproximatelyNotNearPlanes(
        this IPointNode node, Plane3d[] planes, double maxDistance, int minCellExponent = int.MinValue
        )
        => CountPointsApproximately(node,
            n => !planes.Any(plane => node.DataBounds.Intersects(plane, maxDistance)),
            n => planes.Any(plane => plane.Contains(maxDistance, node.DataBounds)),
            minCellExponent
            );
    
    #endregion
}
