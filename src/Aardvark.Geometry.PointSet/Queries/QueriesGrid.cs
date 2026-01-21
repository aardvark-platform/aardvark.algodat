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

#pragma warning disable CS9113 // Parameter is unread.

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
        => new GridQueryXY(self.Root.Value.ToPointNode()).EnumerateGridCellsXY(gridCellExponent);

    /// <summary>
    /// Enumerate over point cloud in a grid of cells of size gridCellExponent.
    /// Empty grid cells are skipped.
    /// </summary>
    public static IEnumerable<IGridQueryXY> EnumerateGridCellsXY(
        this IPointNode self, int gridCellExponent
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
        private IPointNode[] Roots { get; }
        private Chunk Rest { get; }
        public Cell2d Footprint { get; }
        public long Count { get; }

        public GridQueryXY(IPointNode root)
        {
            var rootCell = new Cell(root.CellBounds);
            var pct = 0L;
            if(root is PointNodeAdapter pna)
            {
                pct = pna.OriginalNode.PointCountTree;
            }
            else
            {
                Report.Error("PointCountTree not implemented for node type {0}.", root.GetType().Name);
            }
            Footprint = new Cell2d(rootCell.X, rootCell.Y, rootCell.Exponent);
            Roots = [root];
            Rest = Chunk.Empty;
            Count = pct;
        }

        private GridQueryXY(Cell2d footprint, IPointNode[]? roots, Chunk rest)
        {
            if ((roots == null || roots.Length == 0) && (rest == null || rest.Count == 0))
                throw new InvalidOperationException("Invariant 0ee8c852-9580-44fb-9c19-a9f2f2dd7c93.");

            Footprint = footprint;
            Roots = roots ?? [];
            Rest = rest ?? Chunk.Empty;
            Count = Roots.Sum(r => { 
                    var pct = 0L;
                    if(r is PointNodeAdapter pna)
                    {
                        pct = pna.OriginalNode.PointCountTree;
                    }
                    else
                    {
                        Report.Error("PointCountTree not implemented for node type {0}.", r.GetType().Name);
                    }
                return pct;
            }) + Rest.Count;
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
                    List<IPointNode>[]? newRoots = null;
                    void addRoot(int i, IPointNode? n)
                    {
                        if (n == null) return;
                        newRoots ??= new List<IPointNode>[4];
                        if (newRoots[i] == null) newRoots[i] = [n];
                        else newRoots[i].Add(n);
                    }
                    foreach (var r in Roots)
                    {
                        if (r.Children.Length == 0)
                        {
                            var leafChunk = r.ToChunk();
                            qbbs.Map((bb, i) =>
                                newRests[i] = newRests[i].ImmutableMergeWith(leafChunk.ImmutableFilterByBoxXY(bb))
                                );
                        }
                        else
                        {
                            var ns = r.Children;
                            for (var i = 0; i < 8; i++) addRoot(i & 0b11, ns[i]);
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

    //public class GridQueryResult
    //{
    //    /// <summary>Grid cell bounding box.</summary>
    //    public Cell2d Footprint { get; }

    //    /// <summary>Total number of points in grid cell.</summary>
    //    public long Count { get; }

    //    /// <summary>All points in cell.</summary>
    //    public Chunk[] Points { get; }

    //    public GridQueryResult(Cell2d footprint, IEnumerable<Chunk> points)
    //    {
    //        Footprint = footprint;
    //        Points = points;
    //    }
    //}

    ///// <summary>
    ///// </summary>
    //public static IEnumerable<GridQueryResult> QueryGridXY(
    //    this PointSet self, int stride, int minCellExponent = int.MinValue
    //    )
    //    => QueryGridXY(self.Root.Value, stride, minCellExponent);

    ///// <summary>
    ///// </summary>
    //public static IEnumerable<GridQueryResult> QueryGridXY(
    //    this IPointCloudNode self, int stride, int minCellExponent = int.MinValue
    //    )
    //{
    //    var bbw = self.BoundingBoxExactGlobal;  // bounding box (world space)
    //    var bbt = new Box2l(                    // bounding box (tile space)
    //        new V2l((long)Math.Floor(bbw.Min.X / stride.X), (long)Math.Floor(bbw.Min.Y / stride.Y)),
    //        new V2l((long)Math.Floor(bbw.Max.X / stride.X) + 1L, (long)Math.Floor(bbw.Max.Y / stride.Y) + 1L)
    //        );

    //    return QueryRecGridXY(bbt, stride, minCellExponent, new List<IPointCloudNode> { self });
    //}

    //private static IEnumerable<GridQueryResult> QueryRecGridXY(Box2l bb, int stride, int minCellExponent, List<IPointCloudNode> roots)
    //{
    //    var area = bb.Area;
    //    if (area == 0 || roots.Count == 0) yield break;

    //    var q = new Box2d(bb.Min.X * stride.X, bb.Min.Y * stride.Y, bb.Max.X * stride.X, bb.Max.Y * stride.Y);

    //    if (area == 1)
    //    {
    //        yield return new GridQueryBox2dResult(q, roots.SelectMany(root => root.QueryPointsInsideBoxXY(q)));
    //    }
    //    else
    //    {
    //        var newRoots = new List<IPointCloudNode>();
    //        foreach (var r in roots)
    //        {
    //            if (r.IsLeaf) newRoots.Add(r);
    //            else
    //            {
    //                var _bb = r.BoundingBoxExactGlobal.XY;
    //                if (!q.Intersects(_bb)) { }
    //                else if (q.Contains(_bb)) newRoots.Add(r);
    //                else
    //                {
    //                    var sub = r.Subnodes;
    //                    void add(int i) { if (sub[i] != null) { newRoots.Add(sub[i].Value); } }
    //                    var c = r.Center.XY;
    //                    if (q.Max.X < c.X)
    //                    {
    //                        // left cells
    //                        if (q.Max.Y < c.Y) { add(0); add(4); } // left/bottom
    //                        else if (q.Min.Y >= c.Y) { add(2); add(6); } // left/top
    //                        else { add(0); add(4); add(2); add(6); }
    //                    }
    //                    else if (q.Min.X >= c.X)
    //                    {
    //                        // right cells
    //                        if (q.Max.Y < c.Y) { add(1); add(5); } // right/bottom
    //                        else if (q.Min.Y >= c.Y) { add(3); add(7); } // right/top
    //                        else { add(1); add(5); add(3); add(7); }
    //                    }
    //                    else
    //                    {
    //                        // left/right cells
    //                        if (q.Max.Y < c.Y) { add(0); add(1); add(4); add(5); } // bottom
    //                        else if (q.Min.Y >= c.Y) { add(2); add(3); add(6); add(7); } // top
    //                        else { newRoots.Add(r); }
    //                    }
    //                }
    //            }
    //        }

    //        var sbbs = bb.SplitAtCenter();
    //        foreach (var sbb in sbbs)
    //        {
    //            if (sbb.Min.X == sbb.Max.X || sbb.Min.Y == sbb.Max.Y) continue;
    //            var xs = QueryRecGridXY(sbb, stride, minCellExponent, newRoots);
    //            foreach (var x in xs) yield return x;
    //        }
    //    }
    //}

    #endregion

    #region grid query (arbitrary stride)

    /// <summary>
    /// </summary>
    /// <param name="Footprint">Grid cell bounding box.</param>
    /// <param name="Points"></param>
    public class GridQueryBox2dResult(Box2d Footprint, IEnumerable<Chunk> Points)
    {
    }

    /// <summary>
    /// </summary>
    public static IEnumerable<GridQueryBox2dResult> QueryGridXY(
        this PointSet self, V2d stride, int minCellExponent = int.MinValue
        )
        => QueryGridXY(self, stride, minCellExponent);

    /// <summary>
    /// </summary>
    public static IEnumerable<GridQueryBox2dResult> QueryGridXY(
        this IPointNode self, V2d stride, int maxInMemoryPointCount = 10 * 1024 * 1024, int minCellExponent = int.MinValue
        )
    {
        var bbw = self.DataBounds;  // bounding box (world space)
        var bbt = new Box2l(                    // bounding box (tile space)
            new V2l((long)Math.Floor(bbw.Min.X / stride.X), (long)Math.Floor(bbw.Min.Y / stride.Y)),
            new V2l((long)Math.Floor(bbw.Max.X / stride.X) + 1L, (long)Math.Floor(bbw.Max.Y / stride.Y) + 1L)
            ) ;

        return QueryGridRecXY(bbt, stride, maxInMemoryPointCount, minCellExponent, [self]);
    }

    private static IEnumerable<GridQueryBox2dResult> QueryGridRecInMemoryXY(Box2l bb, V2d stride, Chunk chunk)
    {
        var area = bb.Area;
        if (area == 0 || chunk.Count == 0) yield break;

        var q = new Box2d(bb.Min.X * stride.X, bb.Min.Y * stride.Y, bb.Max.X * stride.X, bb.Max.Y * stride.Y);

        var newChunk = chunk.ImmutableFilterByBoxXY(q);
        if (newChunk.Count == 0) yield break;

        if (area == 1)
        {
            yield return new GridQueryBox2dResult(q, [newChunk]);
        }
        else
        {
            var sbbs = bb.SplitAtCenter();
            foreach (var sbb in sbbs)
            {
                if (sbb.Min.X == sbb.Max.X || sbb.Min.Y == sbb.Max.Y) continue;
                var xs = QueryGridRecInMemoryXY(sbb, stride, newChunk);
                foreach (var x in xs) yield return x;
            }
        }
    }
    private static IEnumerable<GridQueryBox2dResult> QueryGridRecXY(Box2l bb, V2d stride, int maxInMemoryPointCount, int minCellExponent, List<IPointNode> roots)
    {
        var area = bb.Area;
        if (area == 0 || roots.Count == 0) yield break;

        var q = new Box2d(bb.Min.X * stride.X, bb.Min.Y * stride.Y, bb.Max.X * stride.X, bb.Max.Y * stride.Y);

        if (area == 1)
        {
            yield return new GridQueryBox2dResult(q, roots.SelectMany(root => root.QueryPointsInsideBoxXY(q)));
        }
        else
        {
            var newRoots = new List<IPointNode>();
            foreach (var r in roots)
            {
                if (r.Children.Length == 0) newRoots.Add(r);
                else
                {
                    var _bb = r.DataBounds.XY;
                    if (!q.Intersects(_bb)) { }
                    else if (q.Contains(_bb)) newRoots.Add(r);
                    else
                    {
                        var sub = r.Children;
                        void add(int i) { if (sub[i] != null) { newRoots.Add(sub[i]); } }
                        var c = r.DataBounds.Center.XY;
                        if (q.Max.X < c.X)
                        {
                            // left cells
                            if (q.Max.Y < c.Y) { add(0); add(4); } // left/bottom
                            else if (q.Min.Y >= c.Y) { add(2); add(6); } // left/top
                            else { add(0); add(4); add(2); add(6); }
                        }
                        else if (q.Min.X >= c.X)
                        {
                            // right cells
                            if (q.Max.Y < c.Y) { add(1); add(5); } // right/bottom
                            else if (q.Min.Y >= c.Y) { add(3); add(7); } // right/top
                            else { add(1); add(5); add(3); add(7); }
                        }
                        else
                        {
                            // left/right cells
                            if (q.Max.Y < c.Y) { add(0); add(1); add(4); add(5); } // bottom
                            else if (q.Min.Y >= c.Y) { add(2); add(3); add(6); add(7); } // top
                            else { newRoots.Add(r); }
                        }
                    }
                }
            }

            var sbbs = bb.SplitAtCenter();
            var total = newRoots.Sum(r =>
            {
                var pct = 0L;
                if (r is PointNodeAdapter pna)
                {
                    pct = pna.OriginalNode.PointCountTree;
                }
                else
                {
                    Report.Error("PointCountTree not implemented for node type {0}.", r.GetType().Name);
                }

                return pct;
            });
            if (total <= maxInMemoryPointCount)
            {
                var chunk = Chunk.ImmutableMerge(newRoots.SelectMany(r => r.QueryPointsInsideBoxXY(q)));
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
    }

    #endregion
}
