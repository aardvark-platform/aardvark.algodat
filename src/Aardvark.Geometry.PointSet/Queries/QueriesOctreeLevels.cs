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
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry.Points;

/// <summary>
/// </summary>
public static partial class Queries
{
    /// <summary>
    /// Max tree depth.
    /// </summary>
    public static int CountOctreeLevels(this PointSet self)
        => CountOctreeLevels(self.Root.Value.ToPointNode());

    /// <summary>
    /// Max tree depth.
    /// </summary>
    public static int CountOctreeLevels(this IPointNode? root)
    {
        if (root == null) return 0;
        if (root.Children.Length == 0) return 1;
        return root.Children.Select(n => CountOctreeLevels(n)).Max() + 1;
    }
    


    /// <summary>
    /// Finds deepest octree level which still contains less than given number of points. 
    /// </summary>
    public static int GetMaxOctreeLevelWithLessThanGivenPointCount(
        this PointSet self, long maxPointCount
        )
        => GetMaxOctreeLevelWithLessThanGivenPointCount(self.Root.Value.ToPointNode(), maxPointCount);

    /// <summary>
    /// Finds deepest octree level which still contains less than given number of points. 
    /// </summary>
    public static int GetMaxOctreeLevelWithLessThanGivenPointCount(
        this IPointNode node, long maxPointCount
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
        => GetMaxOctreeLevelWithLessThanGivenPointCount(self.Root.Value.ToPointNode(), maxPointCount, bounds);

    /// <summary>
    /// Finds deepest octree level which still contains less than given number of points within given bounds. 
    /// </summary>
    public static int GetMaxOctreeLevelWithLessThanGivenPointCount(
        this IPointNode node, long maxPointCount, Box3d bounds
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
        => CountPointsInOctreeLevel(self.Root.Value.ToPointNode(), level);

    /// <summary>
    /// Gets total number of lod-points in all cells at given octree level.
    /// </summary>
    public static long CountPointsInOctreeLevel(
        this IPointNode node, int level
        )
    {
        if (level < 0) return 0;

        if (level == 0 || node.Children.Length == 0)
        {
            return node.Positions.Length;
        }
        else
        {
            var nextLevel = level - 1;
            var sum = 0L;
            foreach(var n in node.Children)
            {
                sum += CountPointsInOctreeLevel(n, nextLevel);
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
        => CountPointsInOctreeLevel(self.Root.Value.ToPointNode(), level, bounds);

    /// <summary>
    /// Gets approximate number of points at given octree level within given bounds.
    /// For cells that only partially overlap the specified bounds all points are counted anyway.
    /// For performance reasons, in order to avoid per-point bounds checks.
    /// </summary>
    public static long CountPointsInOctreeLevel(
        this IPointNode node, int level, Box3d bounds
        )
    {
        if (level < 0) return 0;
        if (!node.DataBounds.Intersects(bounds)) return 0;

        if (level == 0 || node.Children.Length == 0)
        {
            return node.Positions.Length;
        }
        else
        {
            var nextLevel = level - 1;
            var sum = 0L;
            foreach(var n in node.Children)
            {
                sum += CountPointsInOctreeLevel(n, nextLevel, bounds);
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
        => QueryPointsInOctreeLevel(self.Root.Value.ToPointNode(), level);

    /// <summary>
    /// Returns LoD points for given octree depth/front, where level 0 is the root node.
    /// Front will include leafs higher up than given level.
    /// </summary>
    public static IEnumerable<Chunk> QueryPointsInOctreeLevel(
        this IPointNode node, int level
        )
    {
        if (level < 0) yield break;

        if (level == 0 || node.Children.Length == 0)
        {
            var ps = node.Positions;

            T[]? Verified<T>(T[]? xs, string name)
            {
                if (ps == null! || xs == null) return xs;

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

            C4b[] cs = null!;
            if(node.TryGetAttribute(PointNodeAttributes.Colors, out var csData) && csData is C4b[] csArr)
            {
                cs = Verified(csArr, "colors")!;
            }
            V3f[] ns = null!;
            if(node.TryGetAttribute(PointNodeAttributes.Normals, out var nsData) && nsData is V3f[] nsArr)
            {
                ns = Verified(nsArr, "normals")!;
            }
            int[] js = null!;
            if(node.TryGetAttribute(PointNodeAttributes.Intensities, out var jsData) && jsData is int[] jsArr)
            {
                js = Verified(jsArr, "intensities")!;
            }
            byte[] ks = null!;
            if(node.TryGetAttribute(PointNodeAttributes.Classifications, out var ksData) && ksData is byte[] ksArr )
            {
                ks = Verified(ksArr, "classifications")!;
            }
            node.TryGetAttribute(PointNodeAttributes.PartIndices, out var qs);

            var chunk = new Chunk(ps, cs, ns, js, ks, qs, partIndexRange: null, bbox: null);
            yield return chunk;
        }
        else
        {
            foreach (var c in node.Children) {
                foreach (var x in QueryPointsInOctreeLevel(c, level - 1)) yield return x;
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
        => QueryPointsInOctreeLevel(self.Root.Value.ToPointNode(), level, bounds);

    /// <summary>
    /// Returns lod points for given octree depth/front of cells intersecting given bounds, where level 0 is the root node.
    /// Front will include leafs higher up than given level.
    /// </summary>
    public static IEnumerable<Chunk> QueryPointsInOctreeLevel(
        this IPointNode node, int level, Box3d bounds
        )
    {
        if (level < 0) yield break;
        if (!node.DataBounds.Intersects(bounds)) yield break;

        if (level == 0 || node.Children.Length == 0)
        {
            yield return node.ToChunk();
        }
        else
        {
            foreach(var c in node.Children) {
                foreach (var x in QueryPointsInOctreeLevel(c, level - 1, bounds)) yield return x;
            }
        }
    }
}
