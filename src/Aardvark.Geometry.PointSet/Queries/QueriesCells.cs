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

                var chunks = m_result.CollectFromRelativeDepth(fromRelativeDepth);

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
            /// </summary>
            public IEnumerable<Chunk> GetPoints(int fromRelativeDepth, Box3i outer, Box3i inner, bool excludeInner)
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
                                if (excludeInner && inner.Contains(new V3i(x, y, z))) continue;
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

        #region query cell columns

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

                return Root.QueryCellColumnXY(Cell, fromRelativeDepth);
            }

            /// <summary>
            /// </summary>
            public IEnumerable<Chunk> GetPoints(int fromRelativeDepth, Box2i kernel)
                => GetPoints(fromRelativeDepth, kernel, false);

            /// <summary>
            /// </summary>
            public IEnumerable<Chunk> GetPoints(int fromRelativeDepth, Box2i kernel, bool excludeInnerCell)
            {
                if (fromRelativeDepth < 0) throw new ArgumentException(
                       $"Parameter 'fromRelativeDepth' must not be negative (but is {fromRelativeDepth}). "
                       + "Invariant a11da7c9-1b6b-474c-af12-9e6e160f2390.",
                       nameof(fromRelativeDepth)
                       );

                if (kernel.Min == V2i.OO && kernel.Max == V2i.OO)
                {
                    if (excludeInnerCell)
                    {
                        yield break;
                    }
                    else
                    {
                        var chunks = GetPoints(fromRelativeDepth);
                        foreach (var chunk in chunks) yield return chunk;
                    }
                }

                var min = new V2l(Cell.X, Cell.Y) + (V2l)kernel.Min;
                var max = new V2l(Cell.X, Cell.Y) + (V2l)kernel.Max;
                for (var x = min.X; x <= max.X; x++)
                {
                    for (var y = min.Y; y <= max.Y; y++)
                    {
                        if (excludeInnerCell && x == 0 && y == 0) continue;
                        var c = new Cell2d(x, y, Cell.Exponent);
                        var chunks = Root.QueryCellColumnXY(c, fromRelativeDepth);
                        foreach (var chunk in chunks) yield return chunk;
                    }
                }
            }

            /// <summary>
            /// </summary>
            public IEnumerable<Chunk> GetPoints(int fromRelativeDepth, Box2i kernel, Box2i inner, bool excludeInner)
            {
                if (fromRelativeDepth < 0) throw new ArgumentException(
                       $"Parameter 'fromRelativeDepth' must not be negative (but is {fromRelativeDepth}). "
                       + "Invariant 217d4237-cb64-4a15-ab1f-62566a0c65a9.",
                       nameof(fromRelativeDepth)
                       );

                if (!kernel.Contains(inner)) throw new ArgumentException(
                        $"Outer box ({kernel}) must contain kernel box ({inner}). "
                        + "Invariant 31e0421e-339f-4354-8193-e508214bc364.",
                        nameof(fromRelativeDepth)
                        );

                if (inner.Min == V2i.OO && inner.Max == V2i.OO)
                {
                    var chunks = GetPoints(fromRelativeDepth);
                    foreach (var chunk in chunks) yield return chunk;
                }

                var min = new V2l(Cell.X, Cell.Y) + (V2l)inner.Min;
                var max = new V2l(Cell.X, Cell.Y) + (V2l)inner.Max;
                for (var x = min.X; x <= max.X; x++)
                {
                    for (var y = min.Y; y <= max.Y; y++)
                    {
                        if (excludeInner && kernel.Contains(new V2i(x, y))) continue;
                        var c = new Cell2d(x, y, Cell.Exponent);
                        var chunks = Root.QueryCellColumnXY(c, fromRelativeDepth);
                        foreach (var chunk in chunks) yield return chunk;
                    }
                }
            }

            /// <summary>
            /// Represents a cell 'resultCell' inside an octree ('root').
            /// </summary>
            internal CellQueryResult2d(IPointCloudNode root, Cell2d resultCell)
            {
                Root = root ?? throw new ArgumentNullException(nameof(root));
                Cell = resultCell;
            }
        }

        /// <summary>
        /// Enumerates all columns of a given cell size (given by cellExponent).
        /// Cell size is 2^cellExponent, e.g. -2 gives 0.25, -1 gives 0.50, 0 gives 1.00, 1 gives 2.00, and so on.
        /// Stride is step size (default is V3i.III), which must be greater 0 for each coordinate axis.
        /// </summary>
        public static IEnumerable<Cell2d> EnumerateCellColumns(this IPointCloudNode root, int cellExponent, V2i stride)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (stride.X < 1 || stride.Y < 1) throw new InvalidOperationException(
                $"Stride must be positive, but is {stride}. Invariant 6b7a86a4-6bde-41f1-9af8-e7dc75177e68."
                );

            var bounds = root.Cell.GetRasterBounds(cellExponent);
            for (var x = bounds.Min.X; x < bounds.Max.X; x++)
            {
                if (x % stride.X != 0) continue;
                for (var y = bounds.Min.Y; y < bounds.Max.Y; y++)
                {
                    if (y % stride.Y != 0) continue;
                    yield return new Cell2d(x, y, cellExponent);
                }
            }
        }

        /// <summary>
        /// Returns points in cell column along z-axis at given xy-position.
        /// </summary>
        public static IEnumerable<Chunk> QueryCellColumnXY(this IPointCloudNode root, Cell2d columnXY, int fromRelativeDepth)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (columnXY.IsCenteredAtOrigin) throw new InvalidOperationException(
                "Column centered at origin is not supported. Invariant bf3eb487-72d7-4a4a-9203-69c54490f608."
                );

            if (fromRelativeDepth < 0) throw new ArgumentException(
                $"Parameter 'fromRelativeDepth' must not be negative (but is {fromRelativeDepth}). "
                + "Invariant c8f409cd-c8a0-4b3e-ac9b-03d23843ff8b.",
                nameof(fromRelativeDepth)
                );

            var cloudXY = new Cell2d(root.Cell.X, root.Cell.Y, root.Cell.Exponent);

            // column fully includes point cloud
            if (columnXY.Contains(cloudXY))
            {
                var x = root.ToChunk();
                return x.Count > 0 ? new[] { x } : Enumerable.Empty<Chunk>();
            }

            // column is fully outside point cloud
            if (!cloudXY.Contains(columnXY))
            {
                return Enumerable.Empty<Chunk>();
            }

            return QueryRec(root);

            IEnumerable<Chunk> QueryRec(IPointCloudNode n)
            {
                if (n.Cell.Exponent < columnXY.Exponent)
                {
                    // recursion should have stopped at column size ?!
                    throw new InvalidOperationException("Invariant 4d8cbedf-a86c-43e0-a3d0-75335fa1fadf.");
                }

                // node is same size as column
                if (n.Cell.Exponent == columnXY.Exponent)
                {
                    if (n.Cell.X == columnXY.X && n.Cell.Y == columnXY.Y)
                    {
                        var xs = n.CollectFromRelativeDepth(fromRelativeDepth);
                        foreach (var x in xs) if (x.Count > 0) yield return x;
                    }
                    else
                    {
                        yield break;
                    }
                }

                // or, node is a leaf, but still bigger than column
                else if (n.IsLeaf)
                {
                    var b = n.Cell.BoundingBox;
                    var c = columnXY.BoundingBox;
                    var f = new Box3d(new V3d(c.Min.X, c.Min.Y, b.Min.Z), new V3d(c.Max.X, c.Max.Y, b.Max.Z));
                    var x = n.ToChunk().ImmutableFilterByBox3d(f);
                    if (x.Count > 0) yield return x; else yield break;
                }

                // or finally query subnodes inside column recursively ...
                else
                {
                    foreach (var subnode in n.Subnodes)
                    {
                        if (subnode == null) continue;
                        var c = subnode.Value.Cell;
                        if (columnXY.Intersects(new Cell2d(c.X, c.Y, c.Exponent)))
                        {
                            var xs = QueryRec(subnode.Value);
                            foreach (var x in xs) yield return x;
                        }
                    }
                }
            }
        }

        #endregion
    }
}
