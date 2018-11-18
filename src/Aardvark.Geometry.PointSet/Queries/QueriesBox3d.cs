/*
    Copyright (C) 2006-2018. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System;
using System.Collections.Generic;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public static partial class Queries
    {
        #region Query points

        /// <summary>
        /// All points inside axis-aligned box (including boundary).
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsInsideBox(
            this PointSet self, Box3d query, int minCellExponent = int.MinValue
            )
            => QueryPointsInsideBox(self.Octree.Value, query, minCellExponent);

        /// <summary>
        /// All points inside axis-aligned box (including boundary).
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsInsideBox(
            this IPointCloudNode self, Box3d query, int minCellExponent = int.MinValue
            )
            => QueryPoints(self,
                n => query.Contains(n.BoundingBoxExact),
                n => !query.Intersects(n.BoundingBoxExact),
                p => query.Contains(p),
                minCellExponent);

        /// <summary>
        /// All points outside axis-aligned box (excluding boundary).
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsOutsideBox(
            this PointSet self, Box3d query, int minCellExponent = int.MinValue
            )
            => QueryPointsOutsideBox(self.Octree.Value, query, minCellExponent);

        /// <summary>
        /// All points outside axis-aligned box (excluding boundary).
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsOutsideBox(
            this IPointCloudNode self, Box3d query, int minCellExponent = int.MinValue
            )
            => QueryPoints(self,
                n => !query.Intersects(n.BoundingBoxExact),
                n => query.Contains(n.BoundingBoxExact),
                p => !query.Contains(p),
                minCellExponent);

        #endregion

        #region Count exact

        /// <summary>
        /// Counts points inside axis-aligned box.
        /// </summary>
        public static long CountPointsInsideBox(
            this PointSet self, Box3d query, int minCellExponent = int.MinValue
            )
            => CountPointsInsideBox(self.Octree.Value, query, minCellExponent);

        /// <summary>
        /// Counts points inside axis-aligned box.
        /// </summary>
        public static long CountPointsInsideBox(
            this IPointCloudNode self, Box3d query, int minCellExponent = int.MinValue
            )
            => CountPoints(self,
                n => query.Contains(n.BoundingBoxExact),
                n => !query.Intersects(n.BoundingBoxExact),
                p => query.Contains(p),
                minCellExponent);

        /// <summary>
        /// Counts points outside axis-aligned box.
        /// </summary>
        public static long CountPointsOutsideBox(
            this PointSet self, Box3d query, int minCellExponent = int.MinValue
            )
            => CountPointsOutsideBox(self.Octree.Value, query, minCellExponent);

        /// <summary>
        /// Counts points outside axis-aligned box.
        /// </summary>
        public static long CountPointsOutsideBox(
            this IPointCloudNode self, Box3d query, int minCellExponent = int.MinValue
            )
            => CountPoints(self,
                n => !query.Intersects(n.BoundingBoxExact),
                n => query.Contains(n.BoundingBoxExact),
                p => !query.Contains(p),
                minCellExponent);

        #endregion

        #region Count approximately

        /// <summary>
        /// Counts points approximately inside axis-aligned box (cell granularity).
        /// Result is always equal or greater than exact number.
        /// Faster than CountPointsInsideBox.
        /// </summary>
        public static long CountPointsApproximatelyInsideBox(
            this PointSet self, Box3d query, int minCellExponent = int.MinValue
            )
            => CountPointsApproximatelyInsideBox(self.Root.Value, query, minCellExponent);

        /// <summary>
        /// Counts points approximately inside axis-aligned box (cell granularity).
        /// Result is always equal or greater than exact number.
        /// Faster than CountPointsInsideBox.
        /// </summary>
        public static long CountPointsApproximatelyInsideBox(
            this PointSetNode self, Box3d query, int minCellExponent = int.MinValue
            )
            => CountPointsApproximately(self,
                n => query.Contains(n.BoundingBox),
                n => !query.Intersects(n.BoundingBox),
                minCellExponent);

        /// <summary>
        /// Counts points approximately outside axis-aligned box (cell granularity).
        /// Result is always equal or greater than exact number.
        /// Faster than CountPointsOutsideBox.
        /// </summary>
        public static long CountPointsApproximatelyOutsideBox(
            this PointSet self, Box3d query, int minCellExponent = int.MinValue
            )
            => CountPointsApproximatelyOutsideBox(self.Root.Value, query, minCellExponent);

        /// <summary>
        /// Counts points approximately outside axis-aligned box (cell granularity).
        /// Result is always equal or greater than exact number.
        /// Faster than CountPointsOutsideBox.
        /// </summary>
        public static long CountPointsApproximatelyOutsideBox(
            this PointSetNode self, Box3d query, int minCellExponent = int.MinValue
            )
            => CountPointsApproximately(self,
                n => !query.Intersects(n.BoundingBox),
                n => query.Contains(n.BoundingBox),
                minCellExponent);

        #endregion
    }
}
