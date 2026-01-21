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
        Func<IPointNode, bool> isNodeFullyInside,
        Func<IPointNode, bool> isNodeFullyOutside,
        Func<V3d, bool> isPositionInside,
        int minCellExponent = int.MinValue
        )
        => QueryPoints(node.Root.Value.ToPointNode(), isNodeFullyInside, isNodeFullyOutside, isPositionInside, minCellExponent);

    /// <summary>
    /// </summary>
    public static IEnumerable<Chunk> QueryPoints(this IPointNode node,
        Func<IPointNode, bool> isNodeFullyInside,
        Func<IPointNode, bool> isNodeFullyOutside,
        Func<V3d, bool> isPositionInside,
        int minCellExponent = int.MinValue
        )
    {
        var minCellSize = Math.Pow(2.0, minCellExponent);
        if (isNodeFullyOutside(node)) yield break;
        
        if (node.Children.Length==0 || node.CellBounds.Size.NormMax <= minCellSize)
        {
            if (isNodeFullyInside(node))
            {
                yield return node.ToChunk();
            }
            else // partially inside
            {
                var ps = node.Positions;

                var ia = new HashSet<int>();
                for (var i = 0; i < ps.Length; i++) if (isPositionInside(ps[i])) ia.Add(i);

                if (ia.Count > 0) yield return node.ToChunk(ia);
            }
        }
        else
        {
            foreach (var n in node.Children)
            {
                var xs = QueryPoints(n, isNodeFullyInside, isNodeFullyOutside, isPositionInside, minCellExponent);
                foreach (var x in xs) yield return x;
            }
        }
    }

    /// <summary>
    /// Enumerates cells/front at given cell exponent (or higher if given depth is not reached).
    /// E.g. with minCellExponent = 0 all cells of size 1 (or larger) are numerated.
    /// </summary>
    public static IEnumerable<Chunk> QueryPoints(this IPointNode node,
        int minCellExponent = int.MinValue
        ) => QueryPoints(
            node, 
            _ => true, 
            _ => throw new InvalidOperationException("Invariant 482cbeed-88f2-46af-9cc0-6b0f6f1fc61a."),
            _ => throw new InvalidOperationException("Invariant 31b005a8-f65d-406f-b2a1-96f133d357d3."),
            minCellExponent
            );

    #endregion

    #region Count exact

    /// <summary>
    /// Exact count.
    /// </summary>
    public static long CountPoints(this PointSet node,
        Func<IPointNode, bool> isNodeFullyInside,
        Func<IPointNode, bool> isNodeFullyOutside,
        Func<V3d, bool> isPositionInside,
        int minCellExponent = int.MinValue
        )
        => CountPoints(node.Root.Value.ToPointNode(), isNodeFullyInside, isNodeFullyOutside, isPositionInside, minCellExponent);

    /// <summary>
    /// Exact count.
    /// </summary>
    public static long CountPoints(this IPointNode node,
        Func<IPointNode, bool> isNodeFullyInside,
        Func<IPointNode, bool> isNodeFullyOutside,
        Func<V3d, bool> isPositionInside,
        int minCellExponent = int.MinValue
        )
    {
        var minCellSize = Math.Pow(2.0, minCellExponent);
        if (isNodeFullyOutside(node)) return 0L;
        
        if (node.Children.Length==0 || node.CellBounds.Size.NormMax <= minCellSize)
        {
            if (isNodeFullyInside(node))
            {
                return node.Positions.Length;
            }
            else // partially inside
            {
                var count = 0L;
                var psRaw = node.Positions;
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
            foreach (var n in node.Children)
            {
                sum += CountPoints(n, isNodeFullyInside, isNodeFullyOutside, isPositionInside, minCellExponent);
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
        Func<IPointNode, bool> isNodeFullyInside,
        Func<IPointNode, bool> isNodeFullyOutside,
        int minCellExponent = int.MinValue
        )
        => CountPointsApproximately(node.Root.Value.ToPointNode(), isNodeFullyInside, isNodeFullyOutside, minCellExponent);

    /// <summary>
    /// Approximate count (cell granularity).
    /// Result is always equal or greater than exact number.
    /// </summary>
    public static long CountPointsApproximately(this IPointNode node,
        Func<IPointNode, bool> isNodeFullyInside,
        Func<IPointNode, bool> isNodeFullyOutside,
        int minCellExponent = int.MinValue
        )
    {
        var minCellSize = Math.Pow(2.0, minCellExponent);
        if (isNodeFullyOutside(node)) return 0L;
        
        if (node.Children.Length==0 || node.CellBounds.Size.NormMax <= minCellSize)
        {
            return node.Positions.Length;
        }
        else
        {
            var sum = 0L;
            foreach (var n in node.Children)
            {
                sum += CountPointsApproximately(n, isNodeFullyInside, isNodeFullyOutside, minCellExponent);
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
        Func<IPointNode, bool> isNodeFullyInside,
        Func<IPointNode, bool> isNodeFullyOutside,
        Func<V3d, bool> isPositionInside,
        int minCellExponent = int.MinValue
        )
        => QueryContainsPoints(node.Root.Value.ToPointNode(), isNodeFullyInside, isNodeFullyOutside, isPositionInside, minCellExponent);
    
    /// <summary>
    /// Exact count.
    /// </summary>
    public static bool QueryContainsPoints(this IPointNode node,
        Func<IPointNode, bool> isNodeFullyInside,
        Func<IPointNode, bool> isNodeFullyOutside,
        Func<V3d, bool> isPositionInside,
        int minCellExponent = int.MinValue
        )
    {
        var minCellSize = Math.Pow(2.0, minCellExponent);
        if (isNodeFullyOutside(node)) return false;
        
        if (node.Children.Length==0 || node.CellBounds.Size.NormMax <= minCellSize)
        {
            if (isNodeFullyInside(node))
            {
                return true;
            }
            else // partially inside
            {
                var psRaw = node.Positions;
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
            foreach (var n in node.Children)
            {
                if (QueryContainsPoints(n, isNodeFullyInside, isNodeFullyOutside, isPositionInside, minCellExponent)) return true;
            }
            return false;
        }
    }
    
    #endregion
}
