/*
    Copyright (C) 2006-2020. Aardvark Platform Team. http://github.com/aardvark-platform.
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
                    var psRaw = node.PositionsAbsolute;
                    var csRaw = node.HasColors ? node.Colors.Value : null;
                    var nsRaw = node.HasNormals ? node.Normals.Value : null;
                    var jsRaw = node.HasIntensities ? node.Intensities.Value : null;
                    var ksRaw = node.HasClassifications ? node.Classifications.Value : null ;

                    var ps = new List<V3d>();
                    var cs = csRaw != null ? new List<C4b>() : null;
                    var ns = nsRaw != null ? new List<V3f>() : null;
                    var js = jsRaw != null ? new List<int>() : null;
                    var ks = ksRaw != null ? new List<byte>() : null;

                    for (var i = 0; i < psRaw.Length; i++)
                    {
                        var p = psRaw[i];
                        if (isPositionInside(p))
                        {
                            ps.Add(p);
                            if (csRaw != null) cs.Add(csRaw[i]);
                            if (nsRaw != null) ns.Add(nsRaw[i]);
                            if (jsRaw != null) js.Add(jsRaw[i]);
                            if (ksRaw != null) ks.Add(ksRaw[i]);
                        }
                    }
                    if (ps.Count > 0)
                    {
                        yield return new Chunk(ps, cs, ns, js, ks);
                    }
                }
            }
            else
            {
                for (var i = 0; i < 8; i++)
                {
                    var n = node.Subnodes[i];
                    if (n == null) continue;
                    var xs = QueryPoints(n.Value, isNodeFullyInside, isNodeFullyOutside, isPositionInside, minCellExponent);
                    foreach (var x in xs) yield return x;
                }
            }
        }

        /// <summary>
        /// Enumerates cells/front at given cell exponent (or higher if given depth is not reached).
        /// E.g. with minCellExponent = 0 all cells of size 1 (or larger) are numerated.
        /// </summary>
        public static IEnumerable<Chunk> QueryPoints(this IPointCloudNode node,
            int minCellExponent = int.MinValue
            ) => QueryPoints(
                node, 
                _ => true, 
                _ => throw new InvalidOperationException("Invariant 482cbeed-88f2-46af-9cc0-6b0f6f1fc61a."),
                _ => throw new InvalidOperationException("Invariant 31b005a8-f65d-406f-b2a1-96f133d357d3.")
                );

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
                    var n = node.Subnodes[i];
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
                    var n = node.Subnodes[i];
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
                var sum = 0L;
                for (var i = 0; i < 8; i++)
                {
                    var n = node.Subnodes[i];
                    if (n == null) continue;
                    if (QueryContainsPoints(n.Value, isNodeFullyInside, isNodeFullyOutside, isPositionInside, minCellExponent)) return true;
                }
                return false;
            }
        }

        #endregion
    }
}
