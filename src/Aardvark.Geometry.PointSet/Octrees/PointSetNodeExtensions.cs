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
using System;
using System.Collections.Generic;
using System.Linq;
using Aardvark.Base;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public static partial class PointSetNodeExtensions
    {
        /// <summary>
        /// Computes centroid of absolute (LoD) positions in this node, or null if no (LoD) positions.
        /// </summary>
        public static V3d? ComputeCentroid(this PointSetNode self)
            => self.HasPositions
                ? self.PositionsAbsolute.Average()
                : (self.HasLodPositions ? self.LodPositionsAbsolute.Average() : (V3d?)null)
                ;

        /// <summary>
        /// Returns true if this node intersects the space within a given distance to a plane.
        /// </summary>
        public static bool Intersects(this PointSetNode self, Plane3d plane, double distance)
            => self.BoundingBox.Intersects(plane, distance);

        /// <summary>
        /// Returns true if this node intersects the positive halfspace defined by given plane.
        /// </summary>
        public static bool IntersectsPositiveHalfSpace(this PointSetNode self, Plane3d plane)
            => self.Corners.Any(p => plane.Height(p) > 0);

        /// <summary>
        /// Returns true if this node intersects the negative halfspace defined by given plane.
        /// </summary>
        public static bool IntersectsNegativeHalfSpace(this PointSetNode self, Plane3d plane)
            => self.Corners.Any(p => plane.Height(p) < 0);

        /// <summary>
        /// Returns true if this node is fully inside the positive halfspace defined by given plane.
        /// </summary>
        public static bool InsidePositiveHalfSpace(this PointSetNode self, Plane3d plane)
        {
            self.BoundingBox.GetMinMaxInDirection(plane.Normal, out V3d min, out V3d max);
            return plane.Height(min) > 0;
        }
        
        /// <summary>
        /// Returns true if this node is fully inside the negative halfspace defined by given plane.
        /// </summary>
        public static bool InsideNegativeHalfSpace(this PointSetNode self, Plane3d plane)
        {
            self.BoundingBox.GetMinMaxInDirection(-plane.Normal, out V3d min, out V3d max);
            return plane.Height(min) < 0;
        }

        /// <summary>
        /// Eagerly counts all nodes in tree.
        /// </summary>
        public static long CountNodes(this PointSetNode self)
        {
            if (self == null) return 0;

            var count = 1L;
            if (self.Subnodes != null)
            {
                for (var i = 0; i < 8; i++)
                {
                    var n = self.Subnodes[i];
                    if (n != null) count += CountNodes(n.Value);
                }
            }
            return count;
        }

        /// <summary>
        /// Eagerly counts all points in octree (without using PointCountTree property).
        /// </summary>
        public static long CountPoints(this PointSetNode self)
        {
            if (self == null) return 0;

            if (self.IsLeaf)
            {
                return self.Positions.Value.Length;
            }
            else
            {
                var count = 0L;
                if (self.Subnodes != null)
                {
                    for (var i = 0; i < 8; i++)
                    {
                        var n = self.Subnodes[i];
                        if (n != null) count += CountPoints(n.Value);
                    }
                }
                return count;
            }
        }

        /// <summary>
        /// Eagerly counts all points in octree (without using PointCountTree property).
        /// </summary>
        public static long CountPoints(this IPointCloudNode self)
        {
            if (self == null) return 0;

            if (self.IsLeaf())
            {
                return self.GetPositions().Value.Length;
            }
            else
            {
                var count = 0L;
                if (self.SubNodes != null)
                {
                    for (var i = 0; i < 8; i++)
                    {
                        var n = self.SubNodes[i];
                        if (n != null) count += CountPoints(n.Value);
                    }
                }
                return count;
            }
        }

        /// <summary>
        /// Gets minimum point count of all tree nodes (eager).
        /// </summary>
        public static long GetMinimumNodePointCount(this PointSetNode self)
        {
            if (self == null) return 0;

            var min = self.PointCount;
            if (self.Subnodes != null)
            {
                for (var i = 0; i < 8; i++)
                {
                    var n = self.Subnodes[i];
                    if (n != null)
                    {
                        var x = GetMinimumNodePointCount(n.Value);
                        if (x < min) min = x;
                    }
                }
            }
            return min;
        }

        /// <summary>
        /// Gets average point count of all tree nodes (eager).
        /// </summary>
        public static double GetAverageNodePointCount(this PointSetNode self)
        {
            if (self == null) return 0;
            long sum = 0, count = 0;
            _GetAverageNodePointCount(self, ref sum, ref count);
            return sum / (double)count;
        }
        private static void _GetAverageNodePointCount(this PointSetNode self, ref long sum, ref long count)
        {
            sum += self.PointCount;
            count++;

            if (self.Subnodes == null) return;

            for (var i = 0; i < 8; i++)
            {
                var n = self.Subnodes[i];
                if (n != null) _GetAverageNodePointCount(n.Value, ref sum, ref count);
            }
        }

        /// <summary>
        /// Gets maximum point count of all tree nodes (eager).
        /// </summary>
        public static long GetMaximumNodePointCount(this PointSetNode self)
        {
            if (self == null) return 0;

            var max = self.PointCount;
            if (self.Subnodes != null)
            {
                for (var i = 0; i < 8; i++)
                {
                    var n = self.Subnodes[i];
                    if (n != null)
                    {
                        var x = GetMaximumNodePointCount(n.Value);
                        if (x > max) max = x;
                    }
                }
            }
            return max;
        }
        
        /// <summary>
        /// Average depth of tree (eager).
        /// </summary>
        public static double GetAverageTreeDepth(this PointSetNode self)
        {
            if (self == null) return 0;

            long sum = 0, count = 0;
            _GetAverageTreeDepth(self, 1, ref sum, ref count);
            return sum / (double)count;
        }
        private static void _GetAverageTreeDepth(PointSetNode self, int depth, ref long sum, ref long count)
        {
            if (self.Subnodes == null)
            {
                sum += depth; count++;
                return;
            }

            ++depth;
            for (var i = 0; i < 8; i++)
            {
                var n = self.Subnodes[i];
                if (n != null) _GetAverageTreeDepth(n.Value, depth, ref sum, ref count);
            }
        }

        /// <summary>
        /// Calls action for each node in this tree.
        /// </summary>
        public static IEnumerable<PointSetNode> ForEachNode(
            this PointSetNode self, int minCellExponent = int.MinValue
            )
            => _ForEachNode(self, minCellExponent);

        private static IEnumerable<PointSetNode> _ForEachNode(
            this PointSetNode self, int minCellExponent = int.MinValue
            )
        {
            if (self == null) yield break;

            if (self.Cell.Exponent < minCellExponent) yield break;

            yield return self;

            if (self.Subnodes != null)
            {
                for (var i = 0; i < 8; i++)
                {
                    var n = self.Subnodes[i];
                    if (n == null) continue;
                    foreach (var x in _ForEachNode(n.Value, minCellExponent)) yield return x;
                }
            }
        }

        /// <summary>
        /// </summary>
        public static IEnumerable<IPointCloudNode> ForEachNode(
           this IPointCloudNode self, int minCellExponent = int.MinValue
           )
        {
            if (self == null) yield break;

            if (self.Cell.Exponent < minCellExponent) yield break;

            yield return self;

            if (self.SubNodes != null)
            {
                for (var i = 0; i < 8; i++)
                {
                    var n = self.SubNodes[i];
                    if (n == null) continue;
                    foreach (var x in ForEachNode(n.Value, minCellExponent)) yield return x;
                }
            }
        }

        /// <summary>
        /// Calls action for each (node, fullyInside) in this tree that is intersecting the given hull.
        /// </summary>
        public static IEnumerable<CellQueryResult> ForEachNodeIntersecting(this IPointCloudNode self,
            Hull3d hull, bool doNotTraverseSubnodesWhenFullyInside, int minCellExponent = int.MinValue
            )
        {
            if (self == null) yield break;

            if (self.Cell.Exponent < minCellExponent) yield break;

            for (var i = 0; i < hull.PlaneCount; i++)
            {
                if (!self.IntersectsNegativeHalfSpace(hull.PlaneArray[i])) yield break;
            }

            bool fullyInside = true;
            for (var i = 0; i < hull.PlaneCount; i++)
            {
                if (!self.InsideNegativeHalfSpace(hull.PlaneArray[i]))
                {
                    fullyInside = false;
                    break;
                }
            }

            yield return new CellQueryResult(self, fullyInside);

            if (fullyInside && doNotTraverseSubnodesWhenFullyInside) yield break;

            if (self.SubNodes == null) yield break;
            for (var i = 0; i < 8; i++)
            {
                var n = self.SubNodes[i];
                if (n == null) continue;
                var xs = ForEachNodeIntersecting(n.Value, hull, doNotTraverseSubnodesWhenFullyInside, minCellExponent);
                foreach (var x in xs) yield return x;
            }
        }
    }
}
