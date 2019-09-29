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
            public readonly IPointCloudNode Root;

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

                if (m_cache == null) m_cache = new Dictionary<int, Chunk>();
                if (!m_cache.TryGetValue(fromRelativeDepth, out var chunk))
                {
                    var d = Cell.Exponent + fromRelativeDepth;
                    chunk = m_result.Collect(n => n.IsLeaf || n.Cell.Exponent == d);

                    if (m_result.Cell != Cell)
                    {
                        chunk = chunk.ImmutableFilterByCell(Cell);
                    }

                    m_cache[fromRelativeDepth] = chunk;
                }

                return chunk;
            }

            ///// <summary>
            ///// </summary>
            //public Chunk GetPoints(int fromRelativeDepth, int kernelRadius)
            //{
            //    if (fromRelativeDepth < 0) throw new ArgumentException(
            //           $"Parameter 'fromRelativeDepth' must not be negative (but is {fromRelativeDepth}). "
            //           + "Invariant 574c0596-82e0-4cc2-91ea-b5153c6d742c.",
            //           nameof(fromRelativeDepth)
            //           );

            //    if (m_result == null) return Chunk.Empty;

            //    if (m_cache == null) m_cache = new Dictionary<int, Chunk>();
            //    if (!m_cache.TryGetValue(fromRelativeDepth, out var chunk))
            //    {
            //        var d = Cell.Exponent + fromRelativeDepth;
            //        chunk = m_result.Collect(n => n.IsLeaf || n.Cell.Exponent == d);

            //        if (m_result.Cell != Cell)
            //        {
            //            chunk = chunk.ImmutableFilterByCell(Cell);
            //        }

            //        m_cache[fromRelativeDepth] = chunk;
            //    }

            //    return chunk;
            //}

            /// <summary>
            /// Represents a cell 'resultCell' inside an octree ('root'),
            /// where 'resultNode' is root's smallest subnode (incl. root) containing 'resultCell'.
            /// </summary>
            public CellQueryResult(IPointCloudNode root, Cell resultCell, IPointCloudNode resultNode)
            {
                Root = root ?? throw new ArgumentNullException(nameof(root));
                Cell = resultCell;
                m_result = resultNode;

                if (!root.Cell.Contains(resultNode.Cell)) throw new Exception(
                    $"Root node {root.Cell} must contain resultNode {resultNode.Cell}. Invariant fb8dc278-fa35-4022-8aa8-281855dd41af."
                    );

                if (resultNode != null && !resultNode.Cell.Contains(resultCell)) throw new Exception(
                    $"Result node {resultNode.Cell} must contain resultCell {resultCell}. Invariant 62bff5cc-61b1-4cec-a9f8-b2e1136c19d1."
                    );
            }

            /// <summary>
            /// Result node corresponding to result cell (same cell, or parent if octree is not deep enough).
            /// </summary>
            private readonly IPointCloudNode m_result;

            /// <summary>
            /// Cached value.
            /// Subset if result node is parent of result cell.
            /// </summary>
            private Dictionary<int, Chunk> m_cache;
        }

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
                throw new InvalidOperationException(
                    $"Octree ({root.Cell}) must contain requested cell ({cell}). " +
                    "Invariant 4d67081e-37de-48d4-87df-5f5022f9051f."
                    );
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
                        throw new InvalidOperationException(
                            $"Node ({n.Cell}) must contain requested cell ({cell}). " +
                            "Invariant fbde10dd-1c07-41c8-9fc0-8a98b74d3948."
                            );
                    }
                }
            }
        }

        /// <summary>
        /// Enumerates all points in chunks of a given cell size (given by cellExponent).
        /// Cell size is 2^cellExponent, e.g. -2 gives 0.25, -1 gives 0.50, 0 gives 1.00, 1 gives 2.00, and so on.
        /// </summary>
        public static IEnumerable<CellQueryResult> QueryCells(this IPointCloudNode root, int cellExponent)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (root.Cell.Exponent < cellExponent)
            {
                throw new InvalidOperationException(
                    $"Exponent of given root node ({root.Cell}) must not be smaller than the given minCellExponent ({cellExponent}). " +
                    "Invariant 4114f7c3-7ca2-4171-9229-d51d4c7e3d98."
                    );
            }

            return EnumerateCellsOfSizeRecursive(root);

            IEnumerable<CellQueryResult> EnumerateCellsOfSizeRecursive(IPointCloudNode n)
            {
                if (n.Cell.Exponent == cellExponent)
                {
                    // done (reached requested size)
                    yield return new CellQueryResult(root, n.Cell, n);
                }
                else if (n.IsLeaf())
                {
                    // reached leaf which is still too big => split
                    var xs = Split(n.Cell, n.ToChunk());
                    foreach (var x in xs)
                    {
                        yield return new CellQueryResult(root, x.cell, n);
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
    }
}
