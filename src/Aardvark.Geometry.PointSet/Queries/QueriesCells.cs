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
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
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
            public Chunk GetPoints(int fromRelativeDepth)
            {
                if (fromRelativeDepth < 0) throw new ArgumentException(
                       $"Parameter 'fromRelativeDepth' must not be negative (but is {fromRelativeDepth}). "
                       + "Invariant 574c0596-82e0-4cc2-91ea-b5153c6d742c.",
                       nameof(fromRelativeDepth)
                       );

                if (m_result == null) return Chunk.Empty;

                var d = Cell.Exponent - fromRelativeDepth;
                var chunk = m_result.Collect(n => n.IsLeaf || n.Cell.Exponent <= d);

                if (m_result.Cell != Cell)
                {
                    chunk = chunk.ImmutableFilterByCell(Cell);
                }

                return chunk;
            }

            /// <summary>
            /// </summary>
            public Chunk GetPoints(int fromRelativeDepth, Box3i kernel)
                => GetPoints(fromRelativeDepth, kernel, false);

            /// <summary>
            /// </summary>
            public Chunk GetPoints(int fromRelativeDepth, Box3i kernel, bool excludeInnerCell)
            {
                if (kernel.Min == V3i.OOO && kernel.Max == V3i.OOO) 
                    return excludeInnerCell ? Chunk.Empty : GetPoints(fromRelativeDepth);

                if (fromRelativeDepth < 0) throw new ArgumentException(
                       $"Parameter 'fromRelativeDepth' must not be negative (but is {fromRelativeDepth}). "
                       + "Invariant 02992ae8-22b5-41d0-857a-87e19c9e3db1.",
                       nameof(fromRelativeDepth)
                       );

                if (m_result == null) return Chunk.Empty;

                var result = Chunk.Empty;
                var min = new V3l(Cell.X, Cell.Y, Cell.Z) + (V3l)kernel.Min;
                var max = new V3l(Cell.X, Cell.Y, Cell.Z) + (V3l)kernel.Max;
                for (var x = min.X; x <= max.X; x++)
                {
                    for (var y = min.Y; y <= max.Y; y++)
                    {
                        for (var z = min.Z; z <= max.Z; z++)
                        {
                            if (excludeInnerCell && x == 0 && y == 0 && z == 0) continue;
                            var c = new Cell(x, y, z, Cell.Exponent);
                            var r = Root.QueryCell(c);
                            var chunk = r.GetPoints(fromRelativeDepth);
                            result = result.Union(chunk);
                        }
                    }
                }
                return result;
            }

            /// <summary>
            /// </summary>
            public Chunk GetPoints(int fromRelativeDepth, Box3i kernel2, Box3i outer, bool excludeKernel)
            {
                if (fromRelativeDepth < 0) throw new ArgumentException(
                       $"Parameter 'fromRelativeDepth' must not be negative (but is {fromRelativeDepth}). "
                       + "Invariant 7c4a9458-e65b-413f-bd3c-fc8b93b568b8.",
                       nameof(fromRelativeDepth)
                       );

                if (!outer.Contains(kernel2)) throw new ArgumentException(
                        $"Outer box ({outer}) must contain kernel box ({kernel2}). "
                        + "Invariant 98197924-0aea-454e-be6e-8c73e6c9274e.",
                        nameof(fromRelativeDepth)
                        );

                if (outer.Min == V3i.OOO && outer.Max == V3i.OOO) return GetPoints(fromRelativeDepth);

                if (m_result == null) return Chunk.Empty;

                var result = Chunk.Empty;
                var min = new V3l(Cell.X, Cell.Y, Cell.Z) + (V3l)outer.Min;
                var max = new V3l(Cell.X, Cell.Y, Cell.Z) + (V3l)outer.Max;
                for (var x = min.X; x <= max.X; x++)
                {
                    for (var y = min.Y; y <= max.Y; y++)
                    {
                        for (var z = min.Z; z <= max.Z; z++)
                        {
                            if (excludeKernel && kernel2.Contains(new V3i(x, y, z))) continue;
                            var c = new Cell(x, y, z, Cell.Exponent);
                            var r = Root.QueryCell(c);
                            var chunk = r.GetPoints(fromRelativeDepth);
                            result = result.Union(chunk);
                        }
                    }
                }
                return result;
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

        public struct CellQueryResultXY
        {
            public IPointCloudNode Root { get; }
            public V2l PositionXY { get; }

            public CellQueryResultXY(IPointCloudNode root, V2l positionXY)
            {
                Root = root;
                PositionXY = positionXY;
            }
        }

        public static IEnumerable<Chunk> QueryCellColumnZ(this IPointCloudNode root, V2l xy, int exponent)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            // column is fully outside point cloud
            var rb = root.Cell.GetRasterBounds(exponent);
            if (xy.X < rb.Min.X || xy.X >= rb.Max.X || xy.Y < rb.Min.Y || xy.Y >= rb.Max.Y)
            {
                return Enumerable.Empty<Chunk>();
            }

            // column fully includes point cloud
            if (root.Cell.Exponent <= exponent)
            {
                return new[] { root.ToChunk() };
            }

            return QueryRec(root);

            IEnumerable<Chunk> QueryRec(IPointCloudNode r)
            {
                if (r.Cell.Exponent < exponent)
                {
                    throw new InvalidOperationException("Invariant 4d8cbedf-a86c-43e0-a3d0-75335fa1fadf.");
                }

                if (r.Cell.Exponent == exponent)
                {
                    if (r.Cell.X == xy.X && r.Cell.Y == xy.Y)
                    {
                        yield return r.ToChunk();
                    }
                    else
                    {
                        yield break;
                    }
                }


            }
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
                $"Stride must be positive, but is {stride}." +
                " Invariant be88ccad-798f-4f7d-bcea-6d3eb96c4cb2."
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

        /// <summary>
        /// TODO
        /// </summary>
        public static IEnumerable<CellQueryResultXY> EnumerateCellsXY(this IPointCloudNode root, int cellExponent, V2i stride)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (stride.X < 1 || stride.Y < 1) throw new InvalidOperationException(
                $"Stride must be positive, but is {stride}." +
                " Invariant 255647c5-2d90-4317-831c-1cffb9efa38c."
                );

            if (root.Cell.Exponent <= cellExponent)
            {
                var c = root.Cell;
                while (c.Exponent < cellExponent) { c = c.Parent; }
                return new[] { new CellQueryResultXY(root, new V2l(c.X, c.Y)) };
            }

            throw new NotImplementedException();
        }
    }
}
