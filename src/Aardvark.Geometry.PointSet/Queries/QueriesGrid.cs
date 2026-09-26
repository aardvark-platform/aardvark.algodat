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
    #region grid query (cell stride)

    /// <summary>
    /// Enumerate over point cloud in a grid of cells of size gridCellExponent. 
    /// </summary>
    public static IEnumerable<IGridQueryXY> EnumerateGridCellsXY(
        this PointSet self, int gridCellExponent
        )
        => new GridQueryXY(self.Root.Value).EnumerateGridCellsXY(gridCellExponent);

    /// <summary>
    /// Enumerate over point cloud in a grid of cells of size gridCellExponent.
    /// Empty grid cells are skipped.
    /// </summary>
    public static IEnumerable<IGridQueryXY> EnumerateGridCellsXY(
        this IPointCloudNode self, int gridCellExponent
        )
        => new GridQueryXY(self).EnumerateGridCellsXY(gridCellExponent);

    public interface IGridQueryXY
    {
        public long Count { get; }
        public Cell2d Footprint { get; }
        public IEnumerable<Chunk> CollectPoints(int minCellExponent = int.MinValue);
        public IEnumerable<IGridQueryXY> EnumerateGridCellsXY(int subgridCellExponent);
    }

    public class GridQueryXY : IGridQueryXY
    {
        private IPointCloudNode[] Roots { get; }
        private Chunk Rest { get; }
        public Cell2d Footprint { get; }
        //public int GridCellExponent { get; }
        public long Count { get; }

        public GridQueryXY(IPointCloudNode root)
        {
            Footprint = new Cell2d(root.Cell.X, root.Cell.Y, root.Cell.Exponent);
            Roots = [root];
            Rest = Chunk.Empty;
            Count = root.PointCountTree;
            //GridCellExponent = gridCellExponent;
        }

        private GridQueryXY(Cell2d footprint, IPointCloudNode[]? roots, Chunk rest)
        {
            if ((roots == null || roots.Length == 0) && (rest == null || rest.Count == 0))
                throw new InvalidOperationException("Invariant 0ee8c852-9580-44fb-9c19-a9f2f2dd7c93.");

            Footprint = footprint;
            Roots = roots ?? [];
            Rest = rest ?? Chunk.Empty;
            //GridCellExponent = gridCellExponent;
            Count = Roots.Sum(r => r.PointCountTree) + Rest.Count;
        }

        /// <summary>
        /// Get all points in this grid cell.
        /// </summary>
        public IEnumerable<Chunk> CollectPoints(int minCellExponent = int.MinValue)
        {
            if (Rest.Count > 0)
            {
                yield return Rest;
            }

            foreach (var root in Roots)
            {
                foreach (var chunk in root.QueryAllPoints(minCellExponent))
                {
                    yield return chunk;
                }
            }
        }

        /// <summary>
        /// Enumerate over this grid cell in a grid of subcells of size subgridCellExponent. 
        /// </summary>
        public IEnumerable<IGridQueryXY> EnumerateGridCellsXY(int subgridCellExponent)
        {
            if (Footprint.Exponent <= subgridCellExponent)
            {
                yield return this;
            }
            else
            {
                var hasRest = Rest.Count > 0;
                var hasRoots = Roots.Length > 0;

                if (!hasRoots && Rest.Count == 1)
                {

                    var fp = Footprint;
                    while (fp.Exponent > subgridCellExponent)
                    {
                        var c = fp.GetCenter();
                        var p = Rest.Positions[0].XY;
                        var i = (p.X < c.X) ? (p.Y < c.Y ? 0 : 2) : (p.Y < c.Y ? 1 : 3);
                        fp = fp.GetQuadrant(i);
                    }
                    yield return new GridQueryXY(fp, Roots, Rest);
                }
                else
                {
                    var c = Footprint.GetCenter();
                    var qs = Footprint.Children;
                    var qbbs = qs.Map(q => q.BoundingBox);

                    // split rest ...
                    var newRests = hasRest
                        ? qbbs.Map(Rest.ImmutableFilterByBoxXY)
                        : new Chunk[4].Set(Chunk.Empty)
                        ;

                    // split roots ...
                    List<IPointCloudNode>[]? newRoots = null;
                    void addRoot(int i, IPointCloudNode? n)
                    {
                        if (n == null) return;
                        newRoots ??= new List<IPointCloudNode>[4];
                        if (newRoots[i] == null) newRoots[i] = [n];
                        else newRoots[i].Add(n);
                    }
                    foreach (var r in Roots)
                    {
                        if (r.IsLeaf)
                        {
                            var leafChunk = r.ToChunk();
                            qbbs.Map((bb, i) =>
                                newRests[i] = newRests[i].ImmutableMergeWith(leafChunk.ImmutableFilterByBoxXY(bb))
                                );
                        }
                        else
                        {
                            var ns = r.Subnodes!;
                            for (var i = 0; i < 8; i++) addRoot(i & 0b11, ns[i]?.Value);
                        }
                    }

                    // foreach quadrant: yield grid cells (recursively)
                    for (var i = 0; i < 4; i++)
                    {
                        var a = newRoots?[i]?.ToArray();
                        var b = newRests[i];
                        if (a == null && b.Count == 0) continue;
                        var qgrid = new GridQueryXY(qs[i], a, b);
                        foreach (var x in qgrid.EnumerateGridCellsXY(subgridCellExponent)) yield return x;
                    }
                }
            }
        }
    }

    #endregion

    #region grid query (arbitrary stride)

    /// <summary>
    /// Result for one half-open grid cell.
    /// </summary>
    public class GridQueryBox2dResult
    {
        /// <summary>Grid cell bounding box. Points on Min are included; points on Max are excluded.</summary>
        public Box2d Footprint { get; }

        /// <summary>Points in this grid cell.</summary>
        public IEnumerable<Chunk> Points { get; }

        /// <summary>
        /// Creates a result for one grid cell.
        /// </summary>
        public GridQueryBox2dResult(Box2d footprint, IEnumerable<Chunk> points)
        {
            Footprint = footprint;
            Points = points;
        }
    }

    /// <summary>
    /// Lazily partitions points into half-open grid cells [Min, Max).
    /// </summary>
    public static IEnumerable<GridQueryBox2dResult> QueryGridXY(
        this PointSet self, V2d stride, int minCellExponent = int.MinValue
        )
        => QueryGridXY(self.Root.Value, stride, minCellExponent: minCellExponent);

    /// <summary>
    /// Lazily partitions points into half-open grid cells [Min, Max).
    /// </summary>
    public static IEnumerable<GridQueryBox2dResult> QueryGridXY(
        this IPointCloudNode self, V2d stride, int maxInMemoryPointCount = 10 * 1024 * 1024, int minCellExponent = int.MinValue
        )
    {
        var bbw = self.BoundingBoxExactGlobal;  // bounding box (world space)
        var bbt = new Box2l(                    // bounding box (tile space)
            new V2l((long)Math.Floor(bbw.Min.X / stride.X), (long)Math.Floor(bbw.Min.Y / stride.Y)),
            new V2l((long)Math.Floor(bbw.Max.X / stride.X) + 1L, (long)Math.Floor(bbw.Max.Y / stride.Y) + 1L)
            );

        return QueryGridRecXY(bbt, stride, maxInMemoryPointCount, minCellExponent, [self]);
    }

    private static IEnumerable<GridQueryBox2dResult> QueryGridRecInMemoryXY(Box2l bb, V2d stride, Chunk chunk)
    {
        var area = bb.Area;
        if (area == 0 || chunk.Count == 0) yield break;

        var q = GetGridBounds(bb, stride);
        var newChunk = chunk.ImmutableFilterByBoxXY(q);
        if (newChunk.Count == 0) yield break;

        if (area == 1)
        {
            yield return new GridQueryBox2dResult(q, [newChunk]);
        }
        else
        {
            foreach (var sbb in bb.SplitAtCenter())
            {
                if (sbb.Min.X == sbb.Max.X || sbb.Min.Y == sbb.Max.Y) continue;
                var xs = QueryGridRecInMemoryXY(sbb, stride, newChunk);
                foreach (var x in xs) yield return x;
            }
        }
    }

    private static IEnumerable<GridQueryBox2dResult> QueryGridRecXY(
        Box2l bb,
        V2d stride,
        int maxInMemoryPointCount,
        int minCellExponent,
        List<IPointCloudNode> roots
        )
    {
        var area = bb.Area;
        if (area == 0 || roots.Count == 0) yield break;

        var q = GetGridBounds(bb, stride);
        if (area == 1)
        {
            if (ContainsPointsInsideHalfOpenBoxXY(roots, q, minCellExponent))
            {
                var points = QueryPointsInsideHalfOpenBoxXY(roots, q, minCellExponent);
                yield return new GridQueryBox2dResult(q, points);
            }
            yield break;
        }

        var newRoots = new List<IPointCloudNode>();
        foreach (var root in roots)
        {
            CollectIntersectingNodes(root, q, minCellExponent, newRoots);
        }
        if (newRoots.Count == 0) yield break;

        var sbbs = bb.SplitAtCenter();
        var total = newRoots.Sum(root => root.PointCountTree);
        if (total <= maxInMemoryPointCount)
        {
            var chunk = Chunk.ImmutableMerge(
                QueryPointsInsideHalfOpenBoxXY(newRoots, q, minCellExponent)
                );
            foreach (var sbb in sbbs)
            {
                if (sbb.Min.X == sbb.Max.X || sbb.Min.Y == sbb.Max.Y) continue;
                var xs = QueryGridRecInMemoryXY(sbb, stride, chunk);
                foreach (var x in xs) yield return x;
            }
        }
        else
        {
            foreach (var sbb in sbbs)
            {
                if (sbb.Min.X == sbb.Max.X || sbb.Min.Y == sbb.Max.Y) continue;
                var xs = QueryGridRecXY(sbb, stride, maxInMemoryPointCount, minCellExponent, newRoots);
                foreach (var x in xs) yield return x;
            }
        }
    }

    private static void CollectIntersectingNodes(
        IPointCloudNode node,
        Box2d query,
        int minCellExponent,
        List<IPointCloudNode> result
        )
    {
        if (node.Cell.Exponent < minCellExponent) return;

        var bounds = node.BoundingBoxExactGlobal.XY;
        if (!IntersectsHalfOpen(query, bounds)) return;

        if (ContainsHalfOpen(query, bounds) || node.IsLeaf || node.Cell.Exponent == minCellExponent)
        {
            result.Add(node);
            return;
        }

        foreach (var subnode in node.Subnodes!)
        {
            if (subnode == null) continue;
            var child = subnode.Value;
            if (child.Cell.Exponent >= minCellExponent &&
                IntersectsHalfOpen(query, child.BoundingBoxExactGlobal.XY))
            {
                result.Add(child);
            }
        }
    }

    private static bool ContainsPointsInsideHalfOpenBoxXY(
        List<IPointCloudNode> roots,
        Box2d query,
        int minCellExponent
        )
    {
        var stack = new Stack<(IPointCloudNode Node, bool FullyInside)>(roots.Count);
        for (var i = roots.Count - 1; i >= 0; i--) stack.Push((roots[i], false));

        while (stack.Count > 0)
        {
            var entry = stack.Pop();
            var node = entry.Node;
            if (node.Cell.Exponent < minCellExponent) continue;

            var fullyInside = entry.FullyInside;
            if (!fullyInside)
            {
                var bounds = node.BoundingBoxExactGlobal.XY;
                if (!IntersectsHalfOpen(query, bounds)) continue;
                fullyInside = ContainsHalfOpen(query, bounds);
            }

            if (node.IsLeaf || node.Cell.Exponent == minCellExponent)
            {
                if (fullyInside && node.PointCountCell > 0) return true;
                foreach (var position in node.PositionsAbsolute)
                {
                    if (ContainsHalfOpen(query, position.XY)) return true;
                }
                continue;
            }

            var subnodes = node.Subnodes!;
            for (var i = subnodes.Length - 1; i >= 0; i--)
            {
                var subnode = subnodes[i];
                if (subnode != null) stack.Push((subnode.Value, fullyInside));
            }
        }

        return false;
    }

    private static IEnumerable<Chunk> QueryPointsInsideHalfOpenBoxXY(
        List<IPointCloudNode> roots,
        Box2d query,
        int minCellExponent
        )
    {
        var stack = new Stack<(IPointCloudNode Node, bool FullyInside)>(roots.Count);
        for (var i = roots.Count - 1; i >= 0; i--) stack.Push((roots[i], false));

        while (stack.Count > 0)
        {
            var entry = stack.Pop();
            var node = entry.Node;
            if (node.Cell.Exponent < minCellExponent) continue;

            var fullyInside = entry.FullyInside;
            if (!fullyInside)
            {
                var bounds = node.BoundingBoxExactGlobal.XY;
                if (!IntersectsHalfOpen(query, bounds)) continue;
                fullyInside = ContainsHalfOpen(query, bounds);
            }

            if (node.IsLeaf || node.Cell.Exponent == minCellExponent)
            {
                var chunk = node.ToChunk();
                if (!fullyInside) chunk = chunk.ImmutableFilterByBoxXY(query);
                if (chunk.Count > 0) yield return chunk;
                continue;
            }

            var subnodes = node.Subnodes!;
            for (var i = subnodes.Length - 1; i >= 0; i--)
            {
                var subnode = subnodes[i];
                if (subnode != null) stack.Push((subnode.Value, fullyInside));
            }
        }
    }

    private static Box2d GetGridBounds(Box2l bounds, V2d stride)
        => new(
            bounds.Min.X * stride.X,
            bounds.Min.Y * stride.Y,
            bounds.Max.X * stride.X,
            bounds.Max.Y * stride.Y
            );

    private static bool IntersectsHalfOpen(Box2d query, Box2d bounds)
        => bounds.Max.X >= query.Min.X && bounds.Min.X < query.Max.X &&
           bounds.Max.Y >= query.Min.Y && bounds.Min.Y < query.Max.Y;

    private static bool ContainsHalfOpen(Box2d query, Box2d bounds)
        => bounds.Min.X >= query.Min.X && bounds.Max.X < query.Max.X &&
           bounds.Min.Y >= query.Min.Y && bounds.Max.Y < query.Max.Y;

    private static bool ContainsHalfOpen(Box2d query, V2d point)
        => point.X >= query.Min.X && point.X < query.Max.X &&
           point.Y >= query.Min.Y && point.Y < query.Max.Y;

    #endregion
}
