using Aardvark.Base;
using Aardvark.Data.Points;
using System;
using System.Collections.Generic;
using System.Text;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public static partial class View
    {
        /// <summary>
        /// </summary>
        public static IPointCloudNode Filter(this IPointCloudNode node,
            Func<IPointCloudNode, bool> isNodeFullyInside,
            Func<IPointCloudNode, bool> isNodeFullyOutside,
            Func<V3d, bool> isPositionInside,
            int minCellExponent = int.MinValue
            )
        {
            if (node.Cell.Exponent < minCellExponent) return null;

            if (isNodeFullyOutside(node)) return null;

            if (node.IsLeaf() || node.Cell.Exponent == minCellExponent)
            {
                if (isNodeFullyInside(node))
                {
                    return node;
                }
                else // partially inside
                {
                    var psRaw = node.HasPositions ? node.PositionsAbsolute : node.LodPositionsAbsolute;
                    var csRaw = node.HasColors ? node.Colors?.Value : node.LodColors?.Value;
                    var nsRaw = node.HasNormals ? node.Normals?.Value : node.LodNormals?.Value;
                    var ps = new List<V3d>();
                    var cs = csRaw != null ? new List<C4b>() : null;
                    var ns = nsRaw != null ? new List<V3f>() : null;
                    for (var i = 0; i < psRaw.Length; i++)
                    {
                        var p = psRaw[i];
                        if (isPositionInside(p))
                        {
                            ps.Add(p);
                            if (csRaw != null) cs.Add(csRaw[i]);
                            if (nsRaw != null) ns.Add(nsRaw[i]);
                        }
                    }
                    if (ps.Count > 0)
                    {
                        yield return new Chunk(ps, cs, ns);
                    }
                }
            }
            else
            {
                for (var i = 0; i < 8; i++)
                {
                    var n = node.Subnodes[i];
                    if (n == null) continue;
                    var xs = Filter(n.Value, isNodeFullyInside, isNodeFullyOutside, isPositionInside, minCellExponent);
                    foreach (var x in xs) yield return x;
                }
            }
        }
    }
}
