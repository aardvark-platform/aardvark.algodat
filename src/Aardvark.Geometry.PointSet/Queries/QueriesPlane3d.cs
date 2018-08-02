/*
    Copyright (C) 2017. Aardvark Platform Team. http://github.com/aardvark-platform.
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

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public static partial class Queries
    {
        /// <summary>
        /// All points within maxDistance of given plane.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNearPlane(
            this PointSet self, Plane3d plane, double maxDistance, int minCellExponent = int.MinValue
            )
            => QueryPointsNearPlane(self.Root.Value, plane, maxDistance, minCellExponent);

        /// <summary>
        /// All points within maxDistance of given plane.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNearPlane(
            this PointSetNode node, Plane3d plane, double maxDistance, int minCellExponent = int.MinValue
            )
            => QueryPoints(node,
                n => plane.Contains(maxDistance, node.BoundingBox),
                n => !node.BoundingBox.Intersects(plane, maxDistance),
                p => Math.Abs(plane.Height(p)) <= maxDistance,
                minCellExponent
                );

        /// <summary>
        /// All points within maxDistance of ANY of the given planes.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNearPlanes(
            this PointSet self, Plane3d[] planes, double maxDistance, int minCellExponent = int.MinValue
            )
            => QueryPointsNearPlanes(self.Root.Value, planes, maxDistance, minCellExponent);

        /// <summary>
        /// All points within maxDistance of ANY of the given planes.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNearPlanes(
            this PointSetNode node, Plane3d[] planes, double maxDistance, int minCellExponent = int.MinValue
            )
            => QueryPoints(node,
                n => planes.Any(plane => plane.Contains(maxDistance, node.BoundingBox)),
                n => !planes.Any(plane => node.BoundingBox.Intersects(plane, maxDistance)),
                p => planes.Any(plane => Math.Abs(plane.Height(p)) <= maxDistance),
                minCellExponent
                );

        /// <summary>
        /// All points NOT within maxDistance of given plane.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNotNearPlane(
            this PointSet self, Plane3d plane, double maxDistance, int minCellExponent = int.MinValue
            )
            => QueryPointsNotNearPlane(self.Root.Value, plane, maxDistance, minCellExponent);

        /// <summary>
        /// All points NOT within maxDistance of given plane.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNotNearPlane(
            this PointSetNode node, Plane3d plane, double maxDistance, int minCellExponent = int.MinValue
            )
            => QueryPoints(node,
                n => !node.BoundingBox.Intersects(plane, maxDistance),
                n => plane.Contains(maxDistance, node.BoundingBox),
                p => Math.Abs(plane.Height(p)) > maxDistance,
                minCellExponent
                );

        /// <summary>
        /// All points NOT within maxDistance of ALL the given planes.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNotNearPlanes(
            this PointSet self, Plane3d[] planes, double maxDistance, int minCellExponent = int.MinValue
            )
            => QueryPointsNotNearPlanes(self.Root.Value, planes, maxDistance, minCellExponent);

        /// <summary>
        /// All points NOT within maxDistance of ALL the given planes.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNotNearPlanes(
            this PointSetNode node, Plane3d[] planes, double maxDistance, int minCellExponent = int.MinValue
            )
            => QueryPoints(node,
                n => !planes.Any(plane => node.BoundingBox.Intersects(plane, maxDistance)),
                n => planes.Any(plane => plane.Contains(maxDistance, node.BoundingBox)),
                p => !planes.Any(plane => Math.Abs(plane.Height(p)) <= maxDistance),
                minCellExponent
                );
    }
}
