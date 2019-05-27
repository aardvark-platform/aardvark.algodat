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
        /// </summary>
        public static IEnumerable<Chunk> QueryPoints(this PointSet node,
            Func<IPointCloudNode, bool> isNodeFullyInside,
            Func<IPointCloudNode, bool> isNodeFullyOutside,
            Func<V3d, bool> isPositionInside,
            int minCellExponent = int.MinValue
            )
            => QueryPoints(node.Octree.Value, isNodeFullyInside, isNodeFullyOutside, isPositionInside, minCellExponent);

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
                    var cs = node.HasColors() ? node.GetColors4b().Value : null;
                    var ns = node.HasNormals() ? node.GetNormals3f().Value : null;
                    var js = node.HasIntensities() ? node.GetIntensities().Value : null;
                    var ks = node.HasClassifications() ? node.GetClassifications().Value : null;
                    yield return new Chunk(node.GetPositionsAbsolute(), cs, ns, js, ks);
                }
                else // partially inside
                {
                    var psRaw = node.GetPositionsAbsolute();
                    var csRaw = node.HasColors() ? node.GetColors4b().Value : null;
                    var nsRaw = node.HasNormals() ? node.GetNormals3f().Value : null;
                    var jsRaw = node.HasIntensities() ? node.GetIntensities().Value : null;
                    var ksRaw = node.HasClassifications() ? node.GetClassifications().Value : null ;

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
                    var n = node.SubNodes[i];
                    if (n == null) continue;
                    var xs = QueryPoints(n.Value, isNodeFullyInside, isNodeFullyOutside, isPositionInside, minCellExponent);
                    foreach (var x in xs) yield return x;
                }
            }
        }

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
            => CountPoints(node.Octree.Value, isNodeFullyInside, isNodeFullyOutside, isPositionInside, minCellExponent);

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
                    return node.GetPositions().Value.Length;
                }
                else // partially inside
                {
                    var count = 0L;
                    var psRaw = node.GetPositionsAbsolute();
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
                    var n = node.SubNodes[i];
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
            => CountPointsApproximately(node.Octree.Value, isNodeFullyInside, isNodeFullyOutside, minCellExponent);

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
                return node.GetPositions().Value.Length;
            }
            else
            {
                var sum = 0L;
                for (var i = 0; i < 8; i++)
                {
                    var n = node.SubNodes[i];
                    if (n == null) continue;
                    sum += CountPointsApproximately(n.Value, isNodeFullyInside, isNodeFullyOutside, minCellExponent);
                }
                return sum;
            }
        }

        #endregion
    }
}
