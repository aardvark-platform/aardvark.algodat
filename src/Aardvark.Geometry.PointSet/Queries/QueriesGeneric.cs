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

namespace Aardvark.Geometry.Points;

/// <summary>
/// </summary>
public static partial class Queries
{
    #region Query points

    /// <summary>
    /// </summary>
    public static IEnumerable<Chunk> QueryPoints(this PointSet node,
        Func<IPointCloudNode, bool> isNodeFullyInside,
        Func<IPointCloudNode, bool> isNodeFullyOutside,
        Func<V3d, bool> isPositionInside,
        int minCellExponent = int.MinValue
        )
        => QueryPoints(node.Root.Value, isNodeFullyInside, isNodeFullyOutside, isPositionInside, minCellExponent);

    /// <summary>
    /// </summary>
    public static IEnumerable<Chunk> QueryPoints(this IPointCloudNode node,
        Func<IPointCloudNode, bool> isNodeFullyInside,
        Func<IPointCloudNode, bool> isNodeFullyOutside,
        Func<V3d, bool> isPositionInside,
        int minCellExponent = int.MinValue
        )
    {
        if (node.Cell.Exponent < minCellExponent) yield break;

        if (isNodeFullyOutside(node)) yield break;
        
        if (node.IsLeaf() || node.Cell.Exponent == minCellExponent)
        {
            if (isNodeFullyInside(node))
            {
                yield return node.ToChunk();
            }
            else // partially inside
            {
                var ps = node.PositionsAbsolute;
                var csRaw = node.HasColors ? node.Colors.Value : null;
                var nsRaw = node.HasNormals ? node.Normals.Value : null;
                var jsRaw = node.HasIntensities ? node.Intensities.Value : null;
                var ksRaw = node.HasClassifications ? node.Classifications!.Value : null;
                var qsRaw = node.PartIndices;

                var ia = new List<int>();
                for (var i = 0; i < ps.Length; i++) if (isPositionInside(ps[i])) ia.Add(i);

                if (ia.Count > 0) yield return new Chunk(
                    ps.Subset(ia), csRaw?.Subset(ia), nsRaw?.Subset(ia), jsRaw?.Subset(ia), ksRaw?.Subset(ia), PartIndexUtils.Subset(qsRaw, ia), partIndexRange: null, bbox: null
                    );
            }
        }
        else
        {
            for (var i = 0; i < 8; i++)
            {
                var n = node.Subnodes![i];
                if (n == null) continue;
                var xs = QueryPoints(n.Value, isNodeFullyInside, isNodeFullyOutside, isPositionInside, minCellExponent);
                foreach (var x in xs) yield return x;
            }
        }
    }

    /// <summary>
    /// Enumerates unfiltered chunks, equivalent to <see cref="QueryAllPoints(IPointCloudNode, int)"/>.
    /// By default, visits all leaves in depth-first octant order. At minCellExponent,
    /// emits the node's stored samples (including inner-node LoD data); coarser leaves
    /// are emitted without further subdivision. A node below the requested exponent
    /// contributes no chunks, so a cutoff above the root yields an empty enumeration.
    /// Cell exponent describes size (2^exponent), not relative tree depth.
    /// </summary>
    public static IEnumerable<Chunk> QueryPoints(this IPointCloudNode node,
        int minCellExponent = int.MinValue
        ) => QueryAllPoints(node, minCellExponent);

    #endregion

    #region Count exact

    /// <summary>
    /// Exact count.
    /// </summary>
    public static long CountPoints(this PointSet node,
        Func<IPointCloudNode, bool> isNodeFullyInside,
        Func<IPointCloudNode, bool> isNodeFullyOutside,
        Func<V3d, bool> isPositionInside,
        int minCellExponent = int.MinValue
        )
        => CountPoints(node.Root.Value, isNodeFullyInside, isNodeFullyOutside, isPositionInside, minCellExponent);

    /// <summary>
    /// Exact count.
    /// </summary>
    public static long CountPoints(this IPointCloudNode node,
        Func<IPointCloudNode, bool> isNodeFullyInside,
        Func<IPointCloudNode, bool> isNodeFullyOutside,
        Func<V3d, bool> isPositionInside,
        int minCellExponent = int.MinValue
        )
    {
        if (node.Cell.Exponent < minCellExponent) return 0L;

        if (isNodeFullyOutside(node)) return 0L;

        if (node.IsLeaf() || node.Cell.Exponent == minCellExponent)
        {
            if (isNodeFullyInside(node))
            {
                return node.Positions.Value.Length;
            }
            else // partially inside
            {
                var count = 0L;
                var psRaw = node.PositionsAbsolute;
                for (var i = 0; i < psRaw.Length; i++)
                {
                    var p = psRaw[i];
                    if (isPositionInside(p)) count++;
                }
                return count;
            }
        }
        else
        {
            var sum = 0L;
            for (var i = 0; i < 8; i++)
            {
                var n = node.Subnodes![i];
                if (n == null) continue;
                sum += CountPoints(n.Value, isNodeFullyInside, isNodeFullyOutside, isPositionInside, minCellExponent);
            }
            return sum;
        }
    }

    #endregion

    #region Count approximately

    /// <summary>
    /// Approximate count (cell granularity).
    /// Result is always equal or greater than exact number.
    /// </summary>
    public static long CountPointsApproximately(this PointSet node,
        Func<IPointCloudNode, bool> isNodeFullyInside,
        Func<IPointCloudNode, bool> isNodeFullyOutside,
        int minCellExponent = int.MinValue
        )
        => CountPointsApproximately(node.Root.Value, isNodeFullyInside, isNodeFullyOutside, minCellExponent);

    /// <summary>
    /// Approximate count (cell granularity).
    /// Result is always equal or greater than exact number.
    /// </summary>
    public static long CountPointsApproximately(this IPointCloudNode node,
        Func<IPointCloudNode, bool> isNodeFullyInside,
        Func<IPointCloudNode, bool> isNodeFullyOutside,
        int minCellExponent = int.MinValue
        )
    {
        if (node.Cell.Exponent < minCellExponent) return 0L;

        if (isNodeFullyOutside(node)) return 0L;

        if (node.IsLeaf() || node.Cell.Exponent == minCellExponent)
        {
            return node.Positions.Value.Length;
        }
        else
        {
            var sum = 0L;
            for (var i = 0; i < 8; i++)
            {
                var n = node.Subnodes![i];
                if (n == null) continue;
                sum += CountPointsApproximately(n.Value, isNodeFullyInside, isNodeFullyOutside, minCellExponent);
            }
            return sum;
        }
    }

    #endregion

    #region QueryContainsPoints

    /// <summary>
    /// Exact count.
    /// </summary>
    public static bool QueryContainsPoints(this PointSet node,
        Func<IPointCloudNode, bool> isNodeFullyInside,
        Func<IPointCloudNode, bool> isNodeFullyOutside,
        Func<V3d, bool> isPositionInside,
        int minCellExponent = int.MinValue
        )
        => QueryContainsPoints(node.Root.Value, isNodeFullyInside, isNodeFullyOutside, isPositionInside, minCellExponent);

    /// <summary>
    /// Exact count.
    /// </summary>
    public static bool QueryContainsPoints(this IPointCloudNode node,
        Func<IPointCloudNode, bool> isNodeFullyInside,
        Func<IPointCloudNode, bool> isNodeFullyOutside,
        Func<V3d, bool> isPositionInside,
        int minCellExponent = int.MinValue
        )
    {
        if (node.Cell.Exponent < minCellExponent) return false;

        if (isNodeFullyOutside(node)) return false;

        if (node.IsLeaf() || node.Cell.Exponent == minCellExponent)
        {
            if (isNodeFullyInside(node))
            {
                return true;
            }
            else // partially inside
            {
                var psRaw = node.PositionsAbsolute;
                for (var i = 0; i < psRaw.Length; i++)
                {
                    var p = psRaw[i];
                    if (isPositionInside(p)) return true;
                }
                return false;
            }
        }
        else
        {
            for (var i = 0; i < 8; i++)
            {
                var n = node.Subnodes![i];
                if (n == null) continue;
                if (QueryContainsPoints(n.Value, isNodeFullyInside, isNodeFullyOutside, isPositionInside, minCellExponent)) return true;
            }
            return false;
        }
    }

    #endregion
}
