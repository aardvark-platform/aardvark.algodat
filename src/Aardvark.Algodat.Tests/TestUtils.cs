using Aardvark.Base;
using Aardvark.Geometry.Points;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.Linq;

namespace Aardvark.Geometry.Tests
{
    public static class TestUtils
    {

        public static void ValidateTree(this PointSet set, bool hasLod = true)
        {
            set.Root?.Value?.ValidateTree(set.SplitLimit, hasLod);
        }

        public static void ValidateTree(this IPointCloudNode node, int splitLimit, bool hasLod = true)
        {
            if (node != null)
            {
                //ClassicAssert.IsTrue(node.PointCountTree > 0);
                ClassicAssert.IsTrue(node.HasBoundingBoxExactGlobal || node.HasBoundingBoxExactLocal);

                if (node.IsLeaf || hasLod)
                {
                    if (node.Id != Guid.Empty)
                    {
                        ClassicAssert.IsTrue(node.PointCountCell > 0);
                        ClassicAssert.IsTrue(node.HasPositions);
                        var ps = node.PositionsAbsolute;
                        var realBb = node.BoundingBoxExactGlobal;
                        var bb = realBb.EnlargedByRelativeEps(0.01);
                        ClassicAssert.IsTrue(ps.Length == 0 || ps.All(p => bb.Contains(p)));
                        ClassicAssert.IsTrue(ps.Length <= 1 || node.HasKdTree);
                    }

                    if (node.HasNormals        ) ClassicAssert.IsTrue(node.Normals        .Value.Length == node.PointCountCell);
                    if (node.HasColors         ) ClassicAssert.IsTrue(node.Colors         .Value.Length == node.PointCountCell);
                    if (node.HasIntensities    ) ClassicAssert.IsTrue(node.Intensities    .Value.Length == node.PointCountCell);
                    if (node.HasClassifications) ClassicAssert.IsTrue(node.Classifications.Value.Length == node.PointCountCell);
                }

                if (node.IsLeaf)
                {
                    ClassicAssert.IsTrue(node.PointCountCell == node.PointCountTree);
                    if (node.Id != Guid.Empty)
                    {
                        var ps = node.PositionsAbsolute;
                        ClassicAssert.IsTrue(ps.Length == node.PointCountTree);
                        ClassicAssert.IsTrue(ps.Length <= splitLimit);
                    }
                }
                else
                {
                    var nodes = node.Subnodes.Map(n => n?.Value);
                    ClassicAssert.IsTrue(node.PointCountTree > splitLimit);
                    ClassicAssert.IsTrue(node.PointCountTree == nodes.Sum(n => n != null ? n.PointCountTree : 0));

                    if (node.Id != Guid.Empty)
                    {
                        var realBb = node.BoundingBoxExactGlobal;
                        var bb = realBb.EnlargedByRelativeEps(0.01);
                        var nodeBB = new Box3d(nodes.Select(n => n != null ? n.BoundingBoxExactGlobal : Box3d.Invalid));
                        ClassicAssert.IsTrue(realBb == nodeBB);
                    }

                    foreach (var n in nodes)
                    {
                        ValidateTree(n, splitLimit, hasLod);
                    }
                }
            }
        }


        public static bool NoPointIn(this IPointCloudNode node, Func<V3d, bool> bad) =>
            node == null || (
                !node.PositionsAbsolute.Any(p => bad(p)) &&
                (node.IsLeaf || node.Subnodes.All(c => c?.Value?.NoPointIn(bad) ?? true))
            );

    }
}
