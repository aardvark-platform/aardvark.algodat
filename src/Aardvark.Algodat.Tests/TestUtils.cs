using Aardvark.Base;
using Aardvark.Geometry.Points;
using NUnit.Framework;
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
                Assert.IsTrue(node.PointCountTree > 0);
                Assert.IsTrue(node.HasBoundingBoxExactGlobal || node.HasBoundingBoxExactLocal);

                if (node.IsLeaf || hasLod)
                {
                    Assert.IsTrue(node.PointCountCell > 0);
                    Assert.IsTrue(node.HasPositions);
                    var ps = node.PositionsAbsolute;
                    var realBb = node.BoundingBoxExactGlobal;
                    var bb = realBb.EnlargedByRelativeEps(0.01);
                    Assert.IsTrue(ps.All(p => bb.Contains(p)));
                    Assert.IsTrue(ps.Length <= 1 || node.HasKdTree);

                    if (node.HasNormals) Assert.IsTrue(node.Normals.Value.Length == ps.Length);
                    if (node.HasColors) Assert.IsTrue(node.Colors.Value.Length == ps.Length);
                    if (node.HasIntensities) Assert.IsTrue(node.Intensities.Value.Length == ps.Length);
                    if (node.HasClassifications) Assert.IsTrue(node.Classifications.Value.Length == ps.Length);
                }

                if (node.IsLeaf)
                {
                    var ps = node.PositionsAbsolute;
                    Assert.IsTrue(node.PointCountCell == node.PointCountTree);
                    Assert.IsTrue(ps.Length == node.PointCountTree);
                    Assert.IsTrue(ps.Length <= splitLimit);
                }
                else
                {
                    var realBb = node.BoundingBoxExactGlobal;
                    var bb = realBb.EnlargedByRelativeEps(0.01);
                    Assert.IsTrue(node.PointCountTree > splitLimit);
                    var nodes = node.Subnodes.Map(n => n?.Value);
                    var nodeBB = new Box3d(nodes.Select(n => n != null ? n.BoundingBoxExactGlobal : Box3d.Invalid));
                    Assert.IsTrue(realBb == nodeBB);
                    Assert.IsTrue(node.PointCountTree == nodes.Sum(n => n != null ? n.PointCountTree : 0));

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
