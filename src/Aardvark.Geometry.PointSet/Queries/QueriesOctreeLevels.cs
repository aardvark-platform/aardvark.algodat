/*
    Copyright (C) 2006-2023. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System.Linq;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public static partial class Queries
    {
        /// <summary>
        /// Max tree depth.
        /// </summary>
        public static int CountOctreeLevels(this PointSet self)
            => CountOctreeLevels(self.Root.Value);

        /// <summary>
        /// Max tree depth.
        /// </summary>
        public static int CountOctreeLevels(this IPointCloudNode? root)
        {
            if (root == null) return 0;
            if (root.Subnodes == null) return 1;
            return root.Subnodes.Select(n => CountOctreeLevels(n?.Value)).Max() + 1;
        }
        


        /// <summary>
        /// Finds deepest octree level which still contains less than given number of points. 
        /// </summary>
        public static int GetMaxOctreeLevelWithLessThanGivenPointCount(
            this PointSet self, long maxPointCount
            )
            => GetMaxOctreeLevelWithLessThanGivenPointCount(self.Root.Value, maxPointCount);

        /// <summary>
        /// Finds deepest octree level which still contains less than given number of points. 
        /// </summary>
        public static int GetMaxOctreeLevelWithLessThanGivenPointCount(
            this IPointCloudNode node, long maxPointCount
            )
        {
            var imax = node.CountOctreeLevels();
            for (var i = 0; i < imax; i++)
            {
                var count = node.CountPointsInOctreeLevel(i);
                if (count >= maxPointCount) return i - 1;
            }

            return imax - 1;
        }



        /// <summary>
        /// Finds deepest octree level which still contains less than given number of points within given bounds. 
        /// </summary>
        public static int GetMaxOctreeLevelWithLessThanGivenPointCount(
            this PointSet self, long maxPointCount, Box3d bounds
            )
            => GetMaxOctreeLevelWithLessThanGivenPointCount(self.Root.Value, maxPointCount, bounds);

        /// <summary>
        /// Finds deepest octree level which still contains less than given number of points within given bounds. 
        /// </summary>
        public static int GetMaxOctreeLevelWithLessThanGivenPointCount(
            this IPointCloudNode node, long maxPointCount, Box3d bounds
            )
        {
            var imax = node.CountOctreeLevels();
            for (var i = 0; i < imax; i++)
            {
                var count = node.CountPointsInOctreeLevel(i, bounds);
                if (count >= maxPointCount) return i - 1;
            }

            return imax - 1;
        }



        /// <summary>
        /// Gets total number of points in all cells at given octree level.
        /// </summary>
        public static long CountPointsInOctreeLevel(
            this PointSet self, int level
            )
            => CountPointsInOctreeLevel(self.Root.Value, level);

        /// <summary>
        /// Gets total number of lod-points in all cells at given octree level.
        /// </summary>
        public static long CountPointsInOctreeLevel(
            this IPointCloudNode node, int level
            )
        {
            if (level < 0) return 0;

            if (level == 0 || node.IsLeaf())
            {
                return node.Positions.Value.Count();
            }
            else
            {
                var nextLevel = level - 1;
                var sum = 0L;
                for (var i = 0; i < 8; i++)
                {
                    var n = node.Subnodes![i];
                    if (n == null) continue;
                    sum += CountPointsInOctreeLevel(n.Value, nextLevel);
                }
                return sum;
            }
        }



        /// <summary>
        /// Gets approximate number of points at given octree level within given bounds.
        /// For cells that only partially overlap the specified bounds all points are counted anyway.
        /// For performance reasons, in order to avoid per-point bounds checks.
        /// </summary>
        public static long CountPointsInOctreeLevel(
            this PointSet self, int level, Box3d bounds
            )
            => CountPointsInOctreeLevel(self.Root.Value, level, bounds);

        /// <summary>
        /// Gets approximate number of points at given octree level within given bounds.
        /// For cells that only partially overlap the specified bounds all points are counted anyway.
        /// For performance reasons, in order to avoid per-point bounds checks.
        /// </summary>
        public static long CountPointsInOctreeLevel(
            this IPointCloudNode node, int level, Box3d bounds
            )
        {
            if (level < 0) return 0;
            if (!node.BoundingBoxExactGlobal.Intersects(bounds)) return 0;

            if (level == 0 || node.IsLeaf())
            {
                return node.Positions.Value.Length;
            }
            else
            {
                var sum = 0L;
                if (node.Subnodes != null)
                {
                    var nextLevel = level - 1;
                    for (var i = 0; i < 8; i++)
                    {
                        var n = node.Subnodes[i];
                        if (n == null) continue;
                        sum += CountPointsInOctreeLevel(n.Value, nextLevel, bounds);
                    }
                }
                return sum;
            }
        }



        /// <summary>
        /// Returns points in given octree level, where level 0 is the root node.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsInOctreeLevel(
            this PointSet self, int level
            )
            => QueryPointsInOctreeLevel(self.Root.Value, level);

        /// <summary>
        /// Returns LoD points for given octree depth/front, where level 0 is the root node.
        /// Front will include leafs higher up than given level.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsInOctreeLevel(
            this IPointCloudNode node, int level
            )
        {
            if (level < 0) yield break;

            if (level == 0 || node.IsLeaf())
            {
                var ps = node.PositionsAbsolute;

                T[]? Verified<T>(T[]? xs, string name)
                {
                    if (ps == null || xs == null) return xs;

                    if (ps.Length == xs.Length) return xs;
                    Report.ErrorNoPrefix($"[QueryPointsInOctreeLevel] inconsistent length: {ps.Length} positions, but {xs.Length} {name}.");

                    var rs = new T[ps.Length];
                    if (rs.Length == 0) return rs;
                    var lastX = xs[xs.Length - 1];
                    var imax = Math.Min(ps.Length, xs.Length);
                    for (var i = 0; i < imax; i++) rs[i] = xs[i];
                    for (var i = imax; i < ps.Length; i++) rs[i] = lastX;
                    return rs;
                }
                var cs = Verified(node.TryGetColors4b()?.Value, "colors");
                var ns = Verified(node.TryGetNormals3f()?.Value, "normals");
                var js = Verified(node.TryGetIntensities()?.Value, "intensities");
                var ks = Verified(node.TryGetClassifications()?.Value, "classifications");
                var qs = node.PartIndices;

                var chunk = new Chunk(ps, cs, ns, js, ks, qs, bbox: null);
                yield return chunk;
            }
            else
            {
                if (node.Subnodes == null) yield break;

                for (var i = 0; i < 8; i++)
                {
                    var n = node.Subnodes[i];
                    if (n == null) continue;
                    foreach (var x in QueryPointsInOctreeLevel(n.Value, level - 1)) yield return x;
                }
            }
        }



        /// <summary>
        /// Returns lod points for given octree depth/front of cells intersecting given bounds, where level 0 is the root node.
        /// Front will include leafs higher up than given level.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsInOctreeLevel(
            this PointSet self, int level, Box3d bounds
            )
            => QueryPointsInOctreeLevel(self.Root.Value, level, bounds);

        /// <summary>
        /// Returns lod points for given octree depth/front of cells intersecting given bounds, where level 0 is the root node.
        /// Front will include leafs higher up than given level.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsInOctreeLevel(
            this IPointCloudNode node, int level, Box3d bounds
            )
        {
            if (level < 0) yield break;
            if (!node.BoundingBoxExactGlobal.Intersects(bounds)) yield break;

            if (level == 0 || node.IsLeaf())
            {
                var chunk = new Chunk(
                    node.PositionsAbsolute, node.Colors?.Value, node.Normals?.Value, node.Intensities?.Value, node.Classifications?.Value, node.PartIndices,
                    bbox: null
                    );
                yield return chunk;
            }
            else
            {
                if (node.Subnodes == null) yield break;

                for (var i = 0; i < 8; i++)
                {
                    var n = node.Subnodes[i];
                    if (n == null) continue;
                    foreach (var x in QueryPointsInOctreeLevel(n.Value, level - 1, bounds)) yield return x;
                }
            }
        }
    }
}
