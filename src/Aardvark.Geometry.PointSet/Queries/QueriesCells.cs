/*
    Copyright (C) 2006-2019. Aardvark Platform Team. http://github.com/aardvark-platform.
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.
    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License
    along with this program. If not, see <http://www.gnu.org/licenses/>.
*/
using Aardvark.Base;
using Aardvark.Data.Points;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public static partial class Queries
    {
        #region query cells (3d)

        /// <summary>
        /// </summary>
        public class CellQueryResult
        {
            /// <summary>
            /// Query root node.
            /// </summary>
            private readonly IPointCloudNode Root;

            /// <summary>
            /// Result cell.
            /// </summary>
            public readonly Cell Cell;

            /// <summary>
            /// Returns points inside cell from LoD at given relative depth,
            /// where 0 means points in cell itself, 1 means points from subcells, aso.
            /// </summary>
            public IEnumerable<Chunk> GetPoints(int fromRelativeDepth)
            {
                if (fromRelativeDepth < 0) throw new ArgumentException(
                       $"Parameter 'fromRelativeDepth' must not be negative (but is {fromRelativeDepth}). "
                       + "Invariant 574c0596-82e0-4cc2-91ea-b5153c6d742c.",
                       nameof(fromRelativeDepth)
                       );

                if (m_result == null) yield break;

                var chunks = m_result.Collect(fromRelativeDepth);

                if (m_result.Cell != Cell)
                {
                    chunks = chunks.Select(c => c.ImmutableFilterByCell(Cell));
                }

                foreach (var chunk in chunks) yield return chunk;
            }

            /// <summary>
            /// </summary>
            public IEnumerable<Chunk> GetPoints(int fromRelativeDepth, Box3i outer)
                => GetPoints(fromRelativeDepth, outer, false);

            /// <summary>
            /// </summary>
            public IEnumerable<Chunk> GetPoints(int fromRelativeDepth, Box3i outer, bool excludeInnerCell)
                => GetPoints(fromRelativeDepth, outer, new Box3i(V3i.OOO, V3i.OOO), excludeInnerCell);

            /// <summary>
            /// Inner cells are excluded.
            /// </summary>
            public IEnumerable<Chunk> GetPoints(int fromRelativeDepth, Box3i outer, Box3i inner)
                => GetPoints(fromRelativeDepth, outer, inner, true);

            /// <summary>
            /// </summary>
            public IEnumerable<Chunk> GetPoints(int fromRelativeDepth, Box3i outer, Box3i inner, bool excludeInnerCells)
            {
                if (fromRelativeDepth < 0) throw new ArgumentException(
                       $"Parameter 'fromRelativeDepth' must not be negative (but is {fromRelativeDepth}). "
                       + "Invariant 7c4a9458-e65b-413f-bd3c-fc8b93b568b8.",
                       nameof(fromRelativeDepth)
                       );

                if (!outer.Contains(inner)) throw new ArgumentException(
                        $"Outer box ({outer}) must contain inner box ({inner}). "
                        + "Invariant 98197924-0aea-454e-be6e-8c73e6c9274e.",
                        nameof(fromRelativeDepth)
                        );

                if (m_result == null)
                {
                    yield break;
                }
                else
                {
                    for (var x = outer.Min.X; x <= outer.Max.X; x++)
                    {
                        for (var y = outer.Min.Y; y <= outer.Max.Y; y++)
                        {
                            for (var z = outer.Min.Z; z <= outer.Max.Z; z++)
                            {
                                if (excludeInnerCells && inner.Contains(new V3i(x, y, z))) continue;
                                var c = new Cell(Cell.X + x, Cell.Y + y, Cell.Z + z, Cell.Exponent);
                                var r = Root.QueryCell(c);
                                var chunks = r.GetPoints(fromRelativeDepth);
                                foreach (var chunk in chunks) yield return chunk;
                            }
                        }
                    }
                }
            }

            /// <summary>
            /// Represents a cell 'resultCell' inside an octree ('root'),
            /// where 'resultNode' is root's smallest subnode (incl. root) containing 'resultCell'.
            /// </summary>
            internal CellQueryResult(IPointCloudNode root, Cell resultCell, IPointCloudNode resultNode)
            {
                Root = root ?? throw new ArgumentNullException(nameof(root));
                Cell = resultCell;
                m_result = resultNode;

                if (!root.Cell.Contains(resultNode.Cell)) throw new Exception(
                    $"Root node {root.Cell} must contain resultNode {resultNode.Cell}. Invariant fb8dc278-fa35-4022-8aa8-281855dd41af."
                    );
            }

            /// <summary>
            /// Result node corresponding to result cell (same cell, or parent if octree is not deep enough).
            /// </summary>
            private readonly IPointCloudNode m_result;
        }

        /// <summary>
        /// Returns points in given cell,
        /// or null if octree does not cover given cell.
        /// Result chunk contains 0 points, if cell is covered by octree, but no points are inside given cell.
        /// </summary>
        public static CellQueryResult QueryCell(this PointSet pointset, Cell cell)
            => pointset.Root.Value != null ? QueryCell(pointset.Root.Value, cell) : null;

        /// <summary>
        /// Returns points in given cell,
        /// or null if octree does not cover given cell.
        /// Result chunk contains 0 points, if cell is covered by octree, but no points are inside given cell.
        /// </summary>
        public static CellQueryResult QueryCell(this IPointCloudNode root, Cell cell)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (!root.Cell.Contains(cell))
            {
                return new CellQueryResult(root, cell, root);
            }

            return QueryCellRecursive(root);

            CellQueryResult QueryCellRecursive(IPointCloudNode n)
            {
                if (n.Cell == cell)
                {
                    // found!
                    return new CellQueryResult(root, cell, n);
                }
                else
                {
                    // continue search in subnode ...
                    var octant = n.Cell.GetOctant(cell);
                    if (octant.HasValue)
                    {
                        var subNodeRef = n.Subnodes[octant.Value];
                        if (subNodeRef != null)
                        {
                            return QueryCellRecursive(subNodeRef.Value);
                        }
                        else
                        {
                            // we can't go deeper
                            return new CellQueryResult(root, cell, n);
                        }
                    }
                    else
                    {
                        return new CellQueryResult(root, cell, root);
                    }
                }
            }
        }

        /// <summary>
        /// Enumerates all points in chunks of a given cell size (given by cellExponent).
        /// Cell size is 2^cellExponent, e.g. -2 gives 0.25, -1 gives 0.50, 0 gives 1.00, 1 gives 2.00, and so on.
        /// </summary>
        public static IEnumerable<CellQueryResult> EnumerateCells(this PointSet pointset, int cellExponent)
            => pointset.Root.Value != null ? EnumerateCells(pointset.Root.Value, cellExponent) : null;

        /// <summary>
        /// Enumerates all points in chunks of a given cell size (given by cellExponent).
        /// Cell size is 2^cellExponent, e.g. -2 gives 0.25, -1 gives 0.50, 0 gives 1.00, 1 gives 2.00, and so on.
        /// </summary>
        public static IEnumerable<CellQueryResult> EnumerateCells(this IPointCloudNode root, int cellExponent)
            => EnumerateCells(root, cellExponent, V3i.III);

        /// <summary>
        /// Enumerates all points in chunks of a given cell size (given by cellExponent).
        /// Cell size is 2^cellExponent, e.g. -2 gives 0.25, -1 gives 0.50, 0 gives 1.00, 1 gives 2.00, and so on.
        /// </summary>
        public static IEnumerable<CellQueryResult> EnumerateCells(this PointSet pointset, int cellExponent, V3i stride)
            => pointset.Root.Value != null ? EnumerateCells(pointset.Root.Value, cellExponent, stride) : null;

        /// <summary>
        /// Enumerates all points in chunks of a given cell size (given by cellExponent).
        /// Cell size is 2^cellExponent, e.g. -2 gives 0.25, -1 gives 0.50, 0 gives 1.00, 1 gives 2.00, and so on.
        /// Stride is step size (default is V3i.III), which must be greater 0 for each coordinate axis.
        /// </summary>
        public static IEnumerable<CellQueryResult> EnumerateCells(this IPointCloudNode root, int cellExponent, V3i stride)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (stride.X < 1 || stride.Y < 1 || stride.Z < 1) throw new InvalidOperationException(
                $"Stride must be positive, but is {stride}. Invariant be88ccad-798f-4f7d-bcea-6d3eb96c4cb2."
                );

            if (root.Cell.Exponent < cellExponent)
            { 
                var c = root.Cell;
                do { c = c.Parent; } while (c.Exponent < cellExponent);
                return new[] { new CellQueryResult(root, c, root) };
            }

            return EnumerateCellsOfSizeRecursive(root);

            bool IsOnStride(Cell c) => c.X % stride.X == 0 && c.Y % stride.Y == 0 && c.Z % stride.Z == 0;

            IEnumerable<CellQueryResult> EnumerateCellsOfSizeRecursive(IPointCloudNode n)
            {
                if (n.Cell.Exponent == cellExponent)
                {
                    // done (reached requested cell size)
                    if (IsOnStride(n.Cell))
                    {
                        yield return new CellQueryResult(root, n.Cell, n);
                    }
                    else
                    {
                        yield break;
                    }
                }
                else if (n.IsLeaf())
                {
                    // reached leaf which is still too big => split
                    var xs = Split(n.Cell, n.ToChunk());
                    foreach (var (c, _) in xs)
                    {
                        if (IsOnStride(c))
                        {
                            yield return new CellQueryResult(root, c, n);
                        }
                    }
                }
                else
                {
                    for (var i = 0; i < 8; i++)
                    {
                        var subnode = n.Subnodes[i];
                        if (subnode != null)
                        {
                            var xs = EnumerateCellsOfSizeRecursive(subnode.Value);
                            foreach (var x in xs) yield return x;
                        }
                    }
                }
            }

            IEnumerable<(Cell cell, Chunk chunk)> Split(Cell c, Chunk chunk)
            {
                if (c.Exponent < cellExponent)
                    throw new InvalidOperationException("Invariant c8117f73-be8d-40d9-8559-ed35bbf5df71.");

                if (c.Exponent == cellExponent)
                {
                    // reached requested size
                    yield return (c, chunk);
                }
                else
                {
                    // split
                    for (var i = 0; i < 8; i++)
                    {
                        var octant = c.GetOctant(i);
                        var part = chunk.ImmutableFilterByPosition(octant.BoundingBox.Contains);
                        if (part.IsEmpty) continue;

                        var xs = Split(octant, part);
                        foreach (var x in xs) yield return x;
                    }
                }
            }
        }

        #endregion

        #region query cell columns (2d)

        internal class CellQueryResult2dCache
        {
            public Box2i Size { get; private set; }
            public Dictionary<int, Dictionary<Cell2d, Chunk>> Cache { get; }

            public void UpdateSize(Box2i kernelSize)
            {
                Size = Size.ExtendedBy(kernelSize);
            }

            public CellQueryResult2dCache()
            {
                Size = Box2i.Invalid;
                Cache = new Dictionary<int, Dictionary<Cell2d, Chunk>>();
            }
        }

        /// <summary>
        /// </summary>
        public class CellQueryResult2d
        {
            /// <summary>
            /// Query root node.
            /// </summary>
            private readonly IPointCloudNode Root;

            /// <summary>
            /// Result cell column.
            /// </summary>
            public readonly Cell2d Cell;

            /// <summary>
            /// Returns points inside cell column from LoD at given relative depth,
            /// where 0 means points in cell column itself, 1 means points from subcells, aso.
            /// </summary>
            public IEnumerable<Chunk> GetPoints(int fromRelativeDepth)
            {
                if (fromRelativeDepth < 0) throw new ArgumentException(
                       $"Parameter 'fromRelativeDepth' must not be negative (but is {fromRelativeDepth}). "
                       + "Invariant e2548002-ecb4-421f-959f-daeb3293db60.",
                       nameof(fromRelativeDepth)
                       );

                return Root.CollectColumnXY(Cell, fromRelativeDepth);
            }

            /// <summary>
            /// </summary>
            public IEnumerable<Chunk> GetPoints(int fromRelativeDepth, Box2i outer)
                => GetPoints(fromRelativeDepth, outer, false);

            /// <summary>
            /// </summary>
            public IEnumerable<Chunk> GetPoints(int fromRelativeDepth, Box2i outer, bool excludeInnerCell)
                => GetPoints(fromRelativeDepth, outer, new Box2i(V2i.OO, V2i.OO), excludeInnerCell);

            /// <summary>
            /// Inner cells are excluded.
            /// </summary>
            public IEnumerable<Chunk> GetPoints(int fromRelativeDepth, Box2i outer, Box2i inner)
                => GetPoints(fromRelativeDepth, outer, inner, true);

            /// <summary>
            /// </summary>
            public IEnumerable<Chunk> GetPoints(int fromRelativeDepth, Box2i outer, Box2i inner, bool excludeInnerCells)
            {
                if (fromRelativeDepth < 0) throw new ArgumentException(
                       $"Parameter 'fromRelativeDepth' must not be negative (but is {fromRelativeDepth}). "
                       + "Invariant 217d4237-cb64-4a15-ab1f-62566a0c65a9.",
                       nameof(fromRelativeDepth)
                       );

                if (!outer.Contains(inner)) throw new ArgumentException(
                        $"Outer box ({outer}) must contain inner box ({inner}). "
                        + "Invariant 31e0421e-339f-4354-8193-e508214bc364.",
                        nameof(fromRelativeDepth)
                        );

                Dictionary<Cell2d, Chunk> cache;
                lock (Cache)
                {
                    Cache.UpdateSize(outer);
                    if (!Cache.Cache.TryGetValue(fromRelativeDepth, out cache))
                        cache = Cache.Cache[fromRelativeDepth] = new Dictionary<Cell2d, Chunk>();
                }

                //var cacheHits = 0;
                //var cacheMisses = 0;

                inner += new V2i(Cell.X, Cell.Y);
                outer += new V2i(Cell.X, Cell.Y);
                for (var x = outer.Min.X; x <= outer.Max.X; x++)
                {
                    for (var y = outer.Min.Y; y <= outer.Max.Y; y++)
                    {
                        if (excludeInnerCells && inner.Contains(new V2i(x, y))) continue;
                        var c = new Cell2d(x, y, Cell.Exponent);

                        if (!cache.TryGetValue(c, out var chunks))
                        {
                            chunks = Chunk.ImmutableMerge(Root.CollectColumnXY(c, fromRelativeDepth).ToArray());
                            lock (Cache) cache[c] = chunks;
                            //cacheMisses++;
                        }
                        else
                        {
                            //cacheHits++;
                        }

                        yield return chunks;
                    }
                }
                lock (Cache)
                {
                    var foo = Cache.Size + new V2i(Cell.X, Cell.Y);
                    var ks = cache.Keys.Where(k => !foo.Contains(new V2i(k.X, k.Y))).ToArray();
                    foreach (var k in ks) cache.Remove(k);
                }
                //Report.Warn($"hits = {cacheHits,2}, misses = {cacheMisses,2}");
            }

            /// <summary>
            /// Represents a cell 'resultCell' inside an octree ('root').
            /// </summary>
            internal CellQueryResult2d(IPointCloudNode root, Cell2d resultCell, CellQueryResult2dCache cache)
            {
                Root = root ?? throw new ArgumentNullException(nameof(root));
                Cell = resultCell;
                Cache = cache ?? throw new ArgumentNullException(nameof(cache));
            }

            private CellQueryResult2dCache Cache { get; }
        }

        /// <summary>
        /// Enumerates all columns of a given cell size (given by cellExponent).
        /// Cell size is 2^cellExponent, e.g. -2 gives 0.25, -1 gives 0.50, 0 gives 1.00, 1 gives 2.00, and so on.
        /// Stride is step size (default is V3i.III), which must be greater 0 for each coordinate axis.
        /// </summary>
        public static IEnumerable<CellQueryResult2d> EnumerateCellColumns(this PointSet pointset, int cellExponent)
            => pointset.Root.Value != null ? EnumerateCellColumns(pointset.Root.Value, cellExponent) : null;

        /// <summary>
        /// Enumerates all columns of a given cell size (given by cellExponent).
        /// Cell size is 2^cellExponent, e.g. -2 gives 0.25, -1 gives 0.50, 0 gives 1.00, 1 gives 2.00, and so on.
        /// Stride is step size (default is V3i.III), which must be greater 0 for each coordinate axis.
        /// </summary>
        public static IEnumerable<CellQueryResult2d> EnumerateCellColumns(this IPointCloudNode root, int cellExponent)
            => EnumerateCellColumns(root, cellExponent, V2i.II);

        /// <summary>
        /// Enumerates all columns of a given cell size (given by cellExponent).
        /// Cell size is 2^cellExponent, e.g. -2 gives 0.25, -1 gives 0.50, 0 gives 1.00, 1 gives 2.00, and so on.
        /// Stride is step size (default is V3i.III), which must be greater 0 for each coordinate axis.
        /// </summary>
        public static IEnumerable<CellQueryResult2d> EnumerateCellColumns(this PointSet pointset, int cellExponent, V2i stride)
            => pointset.Root.Value != null ? EnumerateCellColumns(pointset.Root.Value, cellExponent, stride) : null;

        /// <summary>
        /// Enumerates all columns of a given cell size (given by cellExponent).
        /// Cell size is 2^cellExponent, e.g. -2 gives 0.25, -1 gives 0.50, 0 gives 1.00, 1 gives 2.00, and so on.
        /// Stride is step size (default is V3i.III), which must be greater 0 for each coordinate axis.
        /// </summary>
        public static IEnumerable<CellQueryResult2d> EnumerateCellColumns(this IPointCloudNode root, int cellExponent, V2i stride)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (stride.X < 1 || stride.Y < 1) throw new InvalidOperationException(
                $"Stride must be positive, but is {stride}. Invariant 6b7a86a4-6bde-41f1-9af8-e7dc75177e68."
                );

            var cache = new CellQueryResult2dCache();
            var bounds = root.Cell.GetRasterBounds(cellExponent);
            for (var x = bounds.Min.X; x <= bounds.Max.X; x++)
            {
                if (x % stride.X != 0) continue;
                for (var y = bounds.Min.Y; y <= bounds.Max.Y; y++)
                {
                    if (y % stride.Y != 0) continue;
                    yield return new CellQueryResult2d(root, new Cell2d(x, y, cellExponent), cache);
                }
            }
        }

        private static IEnumerable<Cell2d> EnumerateCells2d(this IPointCloudNode root, int cellExponent, V2i stride)
        {
            if (root == null) yield break;

            if (root.PointCountTree == 0) 
                throw new InvalidOperationException("Invariant 8e03ba15-4efd-446b-84da-839cfc1a6c20.");

            if (root.Cell.Exponent < cellExponent) 
                throw new InvalidOperationException("Invariant 472f1a3f-ce36-432c-b9a4-45273c98f230.");

            if (root.IsLeaf)
            {
                if (root.Cell.Exponent > cellExponent)
                {
                    // subdivide leaf ...
                    throw new NotImplementedException("subdivide leaf ...");
                }
                else
                {
                    if (root.Cell.Exponent != cellExponent)
                        throw new InvalidOperationException("Invariant 2b1a686d-0e95-4263-ade4-47d1202183be.");

                    if (root.Cell.X % stride.X == 0 && root.Cell.Y % stride.Y == 0)
                        yield return new Cell2d(root.Cell.X, root.Cell.Y, root.Cell.Exponent);
                    else
                        yield break;
                }
            }
            else
            {
                // inner node ...
                var c0a = root.Subnodes[0]?.Value;
                var c0b = root.Subnodes[4]?.Value;

                throw new NotImplementedException("inner node ...");
            }
        }

        #endregion
    }
}
