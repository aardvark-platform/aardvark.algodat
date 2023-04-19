/*
    Copyright (C) 2006-2023. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using Aardvark.Data;
using Aardvark.Data.Points;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public static class IPointCloudNodeExtensions
    {
        #region enumerate nodes

        /// <summary>
        /// Enumerates all nodes depth-first.
        /// </summary>
        public static IEnumerable<IPointCloudNode> EnumerateNodes(this IPointCloudNode root)
        {
            if (root.Subnodes != null)
            {
                foreach (var subnode in root.Subnodes)
                {
                    if (subnode == null) continue;
                    foreach (var n in EnumerateNodes(subnode.Value)) yield return n;
                }
            }

            yield return root;
        }

        #endregion

        #region ForEach (optionally traversing out-of-core nodes) 

        /// <summary>
        /// Calls action for each node in this tree.
        /// </summary>
        public static void ForEachNode(
            this IPointCloudNode self, bool outOfCore, Action<IPointCloudNode> action)
        {
            action(self);

            if (self.Subnodes == null) return;

            if (outOfCore)
            {
                for (var i = 0; i < 8; i++)
                {
                    self.Subnodes[i]?.Value.ForEachNode(outOfCore, action);
                }
            }
            else
            {
                for (var i = 0; i < 8; i++)
                {
                    var n = self.Subnodes[i];
                    if (n != null)
                    {
                        if (n.TryGetValue(out IPointCloudNode node)) node.ForEachNode(outOfCore, action);
                    }
                }
            }
        }

        /// <summary>
        /// </summary>
        public static IEnumerable<IPointCloudNode> ForEachNode(
           this IPointCloudNode self, int minCellExponent = int.MinValue
           )
        {
            if (self == null) yield break;

            if (self.Cell.Exponent < minCellExponent) yield break;

            yield return self;

            if (self.Subnodes != null)
            {
                for (var i = 0; i < 8; i++)
                {
                    var n = self.Subnodes[i];
                    if (n == null) continue;
                    foreach (var x in ForEachNode(n.Value, minCellExponent)) yield return x;
                }
            }
        }

        /// <summary>
        /// Calls action for each (node, fullyInside) in this pointset, that is intersecting the given hull.
        /// </summary>
        public static void ForEachIntersectingNode(
            this IPointCloudNode self, bool outOfCore, Hull3d hull, bool doNotTraverseSubnodesWhenFullyInside,
            Action<IPointCloudNode, bool> action, CancellationToken ct = default
            )
        {
            ct.ThrowIfCancellationRequested();

            for (var i = 0; i < hull.PlaneCount; i++)
            {
                if (!self.IntersectsNegativeHalfSpace(hull.PlaneArray[i])) return;
            }

            bool fullyInside = true;
            for (var i = 0; i < hull.PlaneCount; i++)
            {
                if (!self.InsideNegativeHalfSpace(hull.PlaneArray[i]))
                {
                    fullyInside = false;
                    break;
                }
            }

            action(self, fullyInside);

            if (fullyInside && doNotTraverseSubnodesWhenFullyInside) return;

            if (self.Subnodes == null) return;

            if (outOfCore)
            {
                for (var i = 0; i < 8; i++)
                {
                    var n = self.Subnodes[i];
                    n?.Value.ForEachIntersectingNode(outOfCore, hull, doNotTraverseSubnodesWhenFullyInside, action, ct);
                }
            }
            else
            {
                for (var i = 0; i < 8; i++)
                {
                    var n = self.Subnodes[i];
                    if (n != null)
                    {
                        if (n.TryGetValue(out IPointCloudNode node))
                        {
                            node.ForEachIntersectingNode(outOfCore, hull, doNotTraverseSubnodesWhenFullyInside, action, ct);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Calls action for each (node, fullyInside) in this tree that is intersecting the given hull.
        /// </summary>
        public static IEnumerable<CellQueryResult> ForEachNodeIntersecting(
            this IPointCloudNode self,
            Hull3d hull, bool doNotTraverseSubnodesWhenFullyInside, int minCellExponent = int.MinValue
            )
        {
            if (self == null) yield break;

            if (self.Cell.Exponent < minCellExponent) yield break;

            for (var i = 0; i < hull.PlaneCount; i++)
            {
                if (!self.IntersectsNegativeHalfSpace(hull.PlaneArray[i])) yield break;
            }

            bool fullyInside = true;
            for (var i = 0; i < hull.PlaneCount; i++)
            {
                if (!self.InsideNegativeHalfSpace(hull.PlaneArray[i]))
                {
                    fullyInside = false;
                    break;
                }
            }

            yield return new CellQueryResult(self, fullyInside);

            if (fullyInside && doNotTraverseSubnodesWhenFullyInside) yield break;

            if (self.Subnodes == null) yield break;
            for (var i = 0; i < 8; i++)
            {
                var n = self.Subnodes[i];
                if (n == null) continue;
                var xs = ForEachNodeIntersecting(n.Value, hull, doNotTraverseSubnodesWhenFullyInside, minCellExponent);
                foreach (var x in xs) yield return x;
            }
        }

        #endregion

        #region Intersections, inside/outside, ...

        /// <summary>
        /// Index of subnode for given point.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetSubIndex(this IPointCloudNode self, in V3d p)
        {
            var i = 0;
            var c = self.Center;
            if (p.X > c.X) i = 1;
            if (p.Y > c.Y) i += 2;
            if (p.Z > c.Z) i += 4;
            return i;
        }

        /// <summary>
        /// Returns true if this node intersects the space within a given distance to a plane.
        /// </summary>
        public static bool Intersects(this IPointCloudNode self, Plane3d plane, double distance)
            => self.BoundingBoxExactGlobal.Intersects(plane, distance);

        /// <summary>
        /// Returns true if this node intersects the positive halfspace defined by given plane.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IntersectsPositiveHalfSpace(this IPointCloudNode self, in Plane3d plane)
        {
            var corners = self.BoundingBoxExactGlobal.ComputeCorners();
            for (var i = 0; i < 8; i++)
            {
                if (plane.Height(corners[i]) > 0) return true;
            }
            return false;
        }

        /// <summary>
        /// Returns true if this node intersects the negative halfspace defined by given plane.
        /// </summary>
        public static bool IntersectsNegativeHalfSpace(this IPointCloudNode self, in Plane3d plane)
        {
            var corners = self.BoundingBoxExactGlobal.ComputeCorners();
            for (var i = 0; i < 8; i++)
            {
                if (plane.Height(corners[i]) < 0) return true;
            }
            return false;
        }

        /// <summary>
        /// Returns true if this node is fully inside the positive halfspace defined by given plane.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool InsidePositiveHalfSpace(this IPointCloudNode self, in Plane3d plane)
        {
            self.BoundingBoxExactGlobal.GetMinMaxInDirection(plane.Normal, out V3d min, out V3d _);
            return plane.Height(min) > 0;
        }

        /// <summary>
        /// Returns true if this node is fully inside the negative halfspace defined by given plane.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool InsideNegativeHalfSpace(this IPointCloudNode self, in Plane3d plane)
        {
            self.BoundingBoxExactGlobal.GetMinMaxInDirection(-plane.Normal, out V3d min, out V3d _);
            return plane.Height(min) < 0;
        }

        #endregion

        #region Counts (optionally traversing out-of-core nodes)

        /// <summary>
        /// Eagerly counts all points in octree (without using PointCountTree property).
        /// </summary>
        public static long CountPoints(this IPointCloudNode self)
        {
            if (self == null) return 0;

            if (self.IsLeaf)
            {
                return self.Positions.Value.Length;
            }
            else
            {
                var count = 0L;
                if (self.Subnodes != null)
                {
                    for (var i = 0; i < 8; i++)
                    {
                        var n = self.Subnodes[i];
                        if (n != null) count += CountPoints(n.Value);
                    }
                }
                return count;
            }
        }

        /// <summary>
        /// Gets minimum point count of all tree nodes (eager).
        /// </summary>
        public static long GetMinimumNodePointCount(this IPointCloudNode self)
        {
            if (self == null) return 0;

            long min = self.PointCountCell;
            if (self.Subnodes != null)
            {
                for (var i = 0; i < 8; i++)
                {
                    var n = self.Subnodes[i];
                    if (n != null)
                    {
                        var x = GetMinimumNodePointCount(n.Value);
                        if (x < min) min = x;
                    }
                }
            }
            return min;
        }

        /// <summary>
        /// Gets maximum point count of all tree nodes (eager).
        /// </summary>
        public static long GetMaximumNodePointCount(this IPointCloudNode self)
        {
            if (self == null) return 0;

            long max = self.PointCountCell;
            if (self.Subnodes != null)
            {
                for (var i = 0; i < 8; i++)
                {
                    var n = self.Subnodes[i];
                    if (n != null)
                    {
                        var x = GetMaximumNodePointCount(n.Value);
                        if (x > max) max = x;
                    }
                }
            }
            return max;
        }

        /// <summary>
        /// Gets average point count of all tree nodes (eager).
        /// </summary>
        public static double GetAverageNodePointCount(this IPointCloudNode self)
        {
            if (self == null) return 0;
            long sum = 0, count = 0;
            GetAverageNodePointCountImpl(self, ref sum, ref count);
            return sum / (double)count;
        }
        private static void GetAverageNodePointCountImpl(this IPointCloudNode self, ref long sum, ref long count)
        {
            sum += self.PointCountCell;
            count++;

            if (self.Subnodes == null) return;

            for (var i = 0; i < 8; i++)
            {
                var n = self.Subnodes[i];
                if (n != null) GetAverageNodePointCountImpl(n.Value, ref sum, ref count);
            }
        }

        /// <summary>
        /// Total number of nodes.
        /// </summary>
        public static long CountNodes(this IPointCloudNode self, bool outOfCore)
        {
            var count = 1L;
            if (self.Subnodes != null)
            {
                if (outOfCore && self is not FilteredNode)
                {
                    long FastCount(Guid key)
                    {
                        if (key == Guid.Empty) return 0L;

                        var acc = 1L;

                        var (def, obj) = self.Storage.GetDurable(key);
                        if (def == Durable.Octree.Node)
                        {
                            var data = (IDictionary<Durable.Def, object>)obj;
                            if (data.TryGetValue(Durable.Octree.SubnodesGuids, out var o))
                            {
                                var snids = (Guid[])o;
                                foreach(var snid in snids)
                                {
                                    if (snid == Guid.Empty) continue;
                                    acc += FastCount(snid);
                                }
                            }
                        }
                        else
                        {
                            var n = self.Storage.GetPointCloudNode(key);
                            foreach (var sn in n.Subnodes)
                            {
                                if (sn == null) continue;
                                acc += FastCount(sn.Value.Id);
                            }
                        }
                        
                        return acc;
                    }

                    return FastCount(self.Id);
                }
                else
                {
                    for (var i = 0; i < 8; i++)
                    {
                        var n = self.Subnodes[i];
                        if (n != null)
                        {
                            if (n.TryGetValue(out IPointCloudNode node)) count += node.CountNodes(outOfCore);
                        }
                    }
                }
            }
            return count;
        }

        /// <summary>
        /// Number of leaf nodes.
        /// </summary>
        public static long CountLeafNodes(this IPointCloudNode self, bool outOfCore)
        {
            if (self.Subnodes == null) return 1;

            var count = 0L;
            if (outOfCore)
            {
                for (var i = 0; i < 8; i++)
                {
                    var n = self.Subnodes[i];
                    if (n != null) count += n.Value.CountLeafNodes(outOfCore);
                }
            }
            else
            {
                for (var i = 0; i < 8; i++)
                {
                    var n = self.Subnodes[i];
                    if (n != null)
                    {
                        if (n.TryGetValue(out IPointCloudNode node)) count += node.CountLeafNodes(outOfCore);
                    }
                }
            }
            return count;
        }

        /// <summary>
        /// Gets minimum point count of leaf nodes.
        /// </summary>
        public static long GetMinimumLeafPointCount(this IPointCloudNode self, bool outOfCore)
        {
            var min = long.MaxValue;
            if (self.Subnodes != null)
            {
                if (outOfCore)
                {
                    for (var i = 0; i < 8; i++)
                    {
                        var n = self.Subnodes[i];
                        if (n != null)
                        {
                            var x = n.Value.GetMinimumLeafPointCount(outOfCore);
                            if (x < min) min = x;
                        }
                    }
                }
                else
                {
                    for (var i = 0; i < 8; i++)
                    {
                        var n = self.Subnodes[i];
                        if (n != null)
                        {
                            if (n.TryGetValue(out IPointCloudNode node))
                            {
                                var x = node.GetMinimumLeafPointCount(outOfCore);
                                if (x < min) min = x;
                            }
                        }
                    }
                }
            }
            return min;
        }

        /// <summary>
        /// Gets maximum point count of leaf nodes.
        /// </summary>
        public static long GetMaximumLeafPointCount(this IPointCloudNode self, bool outOfCore)
        {
            var max = long.MinValue;
            if (self.Subnodes != null)
            {
                if (outOfCore)
                {
                    for (var i = 0; i < 8; i++)
                    {
                        var n = self.Subnodes[i];
                        if (n != null)
                        {
                            var x = n.Value.GetMinimumLeafPointCount(outOfCore);
                            if (x > max) max = x;
                        }
                    }
                }
                else
                {
                    for (var i = 0; i < 8; i++)
                    {
                        var n = self.Subnodes[i];
                        if (n != null)
                        {
                            if (n.TryGetValue(out IPointCloudNode node))
                            {
                                var x = node.GetMinimumLeafPointCount(outOfCore);
                                if (x > max) max = x;
                            }
                        }
                    }
                }
            }
            return max;
        }

        /// <summary>
        /// Gets average point count of leaf nodes.
        /// </summary>
        public static double GetAverageLeafPointCount(this IPointCloudNode self, bool outOfCore)
        {
            return self.PointCountTree / (double)self.CountNodes(outOfCore);
        }

        /// <summary>
        /// Depth of tree (minimum).
        /// </summary>
        public static int GetMinimumTreeDepth(this IPointCloudNode self, bool outOfCore)
        {
            if (self.Subnodes == null) return 1;

            var min = int.MaxValue;

            if (outOfCore)
            {
                for (var i = 0; i < 8; i++)
                {
                    var n = self.Subnodes[i];
                    if (n != null)
                    {
                        var x = n.Value.GetMinimumTreeDepth(outOfCore);
                        if (x < min) min = x;
                    }
                }
            }
            else
            {
                for (var i = 0; i < 8; i++)
                {
                    var n = self.Subnodes[i];
                    if (n != null)
                    {
                        if (n.TryGetValue(out IPointCloudNode node))
                        {
                            var x = node.GetMinimumTreeDepth(outOfCore);
                            if (x < min) min = x;
                        }
                    }
                }
            }
            return 1 + (min != int.MaxValue ? min : 0);
        }

        /// <summary>
        /// Depth of tree (maximum).
        /// </summary>
        public static int GetMaximiumTreeDepth(this IPointCloudNode self, bool outOfCore)
        {
            if (self.Subnodes == null) return 1;

            var max = 0;

            if (outOfCore)
            {
                for (var i = 0; i < 8; i++)
                {
                    var n = self.Subnodes[i];
                    if (n != null)
                    {
                        var x = n.Value.GetMaximiumTreeDepth(outOfCore);
                        if (x > max) max = x;
                    }
                }
            }
            else
            {
                for (var i = 0; i < 8; i++)
                {
                    var n = self.Subnodes[i];
                    if (n != null)
                    {
                        if (n.TryGetValue(out IPointCloudNode node))
                        {
                            var x = node.GetMaximiumTreeDepth(outOfCore);
                            if (x > max) max = x;
                        }
                    }
                }
            }
            return 1 + max;
        }

        /// <summary>
        /// Depth of tree (average).
        /// </summary>
        public static double GetAverageTreeDepth(this IPointCloudNode self, bool outOfCore)
        {
            long sum = 0, count = 0;
            self.GetAverageTreeDepth(outOfCore, 1, ref sum, ref count);
            return sum / (double)count;
        }
        private static void GetAverageTreeDepth(this IPointCloudNode self, bool outOfCore, int depth, ref long sum, ref long count)
        {
            if (self.Subnodes == null)
            {
                sum += depth; count++;
                return;
            }

            ++depth;

            if (outOfCore)
            {
                for (var i = 0; i < 8; i++)
                {
                    var n = self.Subnodes[i];
                    n?.Value.GetAverageTreeDepth(outOfCore, depth, ref sum, ref count);
                }
            }
            else
            {
                for (var i = 0; i < 8; i++)
                {
                    var n = self.Subnodes[i];
                    if (n != null)
                    {
                        if (n.TryGetValue(out IPointCloudNode node)) node.GetAverageTreeDepth(outOfCore, depth, ref sum, ref count);
                    }
                }
            }
        }

        #endregion

        #region TryGet*

        /// <summary>Returns null if node has no colors.</summary>
        public static PersistentRef<C4b[]> TryGetColors4b(this IPointCloudNode self)
            => self.HasColors ? self.Colors : null;

        /// <summary>Returns null if node has no normals.</summary>
        public static PersistentRef<V3f[]> TryGetNormals3f(this IPointCloudNode self)
            => self.HasNormals ? self.Normals : null;

        /// <summary>Returns null if node has no intensities.</summary>
        public static PersistentRef<int[]> TryGetIntensities(this IPointCloudNode self)
            => self.HasIntensities ? self.Intensities : null;

        /// <summary>Returns null if node has no classifications.</summary>
        public static PersistentRef<byte[]> TryGetClassifications(this IPointCloudNode self)
            => self.HasClassifications ? self.Classifications : null;

        #endregion

        #region Collect points from cells and cell columns

        /// <summary>
        /// Collects all points from nodes for which predicate is true.
        /// Subnodes of nodes for which predicate is true are not traversed.  
        /// </summary>
        public static IEnumerable<Chunk> Collect(this IPointCloudNode self, Func<IPointCloudNode, bool> predicate)
        {
            if (self == null) yield break;
            if (self.IsLeaf)
            {
                yield return self.ToChunk();
            }
            else
            {
                var chunks = CollectRec(self, predicate);
                foreach (var chunk in chunks) yield return chunk;
            }

            static IEnumerable<Chunk> CollectRec(IPointCloudNode n, Func<IPointCloudNode, bool> _collectMe)
            {
                if (n == null) yield break;
                
                if (_collectMe(n))
                {
                    yield return n.ToChunk();
                }
                else
                {
                    foreach (var x in n.Subnodes)
                    {
                        if (x != null)
                        {
                            var chunks = CollectRec(x.Value, _collectMe);
                            foreach (var chunk in chunks) yield return chunk;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Collects all points from nodes at given relative depth.
        /// E.g. 0 returns points from self, 1 gets points from children, aso.
        /// </summary>
        public static IEnumerable<Chunk> Collect(this IPointCloudNode self, int fromRelativeDepth)
        {
            var d = self.Cell.Exponent - fromRelativeDepth;
            return self.Collect(x => x.IsLeaf || x.Cell.Exponent <= d);
        }

        /// <summary>
        /// Returns points in cells column along z-axis at given xy-position.
        /// </summary>
        public static IEnumerable<Chunk> CollectColumnXY(this IPointCloudNode root, Cell2d columnXY, int fromRelativeDepth)
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
                return root.Collect(fromRelativeDepth);
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
                        var xs = n.Collect(fromRelativeDepth);
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


        /// <summary></summary>
        public static bool IsLeaf(this IPointCloudNode self) => self.Subnodes == null;

        /// <summary></summary>
        public static bool IsNotLeaf(this IPointCloudNode self) => self.Subnodes != null;

        /// <summary>
        /// Throws if node misses any standard derived attributes.
        /// </summary>
        public static void CheckDerivedAttributes(this IPointCloudNode self)
        {
            var isTmpNode = self.Has(PointSetNode.TemporaryImportNode);
            if (self.HasPositions)
            {
                if (!self.HasKdTree && !isTmpNode) throw new InvalidOperationException(
                    "Missing KdTree. Invariant ef8b6f10-a5ce-4dfd-826e-78319acd9faa."
                    );
                if (!self.HasBoundingBoxExactLocal) throw new InvalidOperationException(
                    "Missing BoundingBoxExactLocal. Invariant f91b261b-2aa2-41a0-9ada-4d03cbaf0507."
                    );
                if (!self.HasBoundingBoxExactGlobal) throw new InvalidOperationException(
                    "Missing BoundingBoxExactGlobal. Invariant 9bb641c6-ce54-4a1e-9cf9-943b85e3bf81."
                    );
                if (!self.HasCentroidLocal) throw new InvalidOperationException(
                    "Missing CentroidLocal. Invariant 72b18b6e-4c95-4a79-b9dd-11902c097649."
                    );
                if (!self.HasCentroidLocalStdDev) throw new InvalidOperationException(
                    "Missing CentroidLocalStdDev. Invariant 429bb1c1-9a52-4e4c-bab1-f3635e143061."
                    );
                //if (!self.HasPointDistanceAverage && !isTmpNode) throw new InvalidOperationException(
                //    "Missing PointDistanceAverage. Invariant e52003ff-72ca-4fc2-8242-f20d7f039473."
                //    );
                //if (!self.HasPointDistanceStandardDeviation && !isTmpNode) throw new InvalidOperationException(
                //    "Missing PointDistanceStandardDeviation. Invariant 4a03c3f5-a625-4124-91f6-6f79fd1b5d0e."
                //    );
            }
            if (!self.HasMaxTreeDepth) throw new InvalidOperationException(
                "Missing MaxTreeDepth. Invariant c7c3c337-5404-4773-aae3-01d213e575b0."
                );
            if (!self.HasMinTreeDepth) throw new InvalidOperationException(
                "Missing MinTreeDepth. Invariant 2df9fb7b-684a-4103-8f14-07785607d2f4."
                );
        }

        /// <summary>
        /// Converts node to Chunk.
        /// </summary>
        public static Chunk ToChunk(this IPointCloudNode self)
        {
            var cs = self.HasColors ? self.Colors.Value : null;
            var ns = self.HasNormals ? self.Normals.Value : null;
            var js = self.HasIntensities ? self.Intensities.Value : null;
            var ks = self.HasClassifications ? self.Classifications.Value : null;
            return new Chunk(self.PositionsAbsolute, cs, ns, js, ks);
        }

        /// <summary>
        /// Converts node to Chunks (by collecting subnodes from given relative depth).
        /// </summary>
        public static IEnumerable<Chunk> ToChunk(this IPointCloudNode self, int fromRelativeDepth)
        {
            if (fromRelativeDepth < 0) throw new InvalidOperationException(
                   $"FromRelativeDepth must be positive, but is {fromRelativeDepth}. Invariant f94781c3-cad7-4f46-b2e1-573cd1df227b."
                   );

            if (fromRelativeDepth == 0 || self.IsLeaf)
            {
                var cs = self.HasColors ? self.Colors.Value : null;
                var ns = self.HasNormals ? self.Normals.Value : null;
                var js = self.HasIntensities ? self.Intensities.Value : null;
                var ks = self.HasClassifications ? self.Classifications.Value : null;
                yield return new Chunk(self.PositionsAbsolute, cs, ns, js, ks);
            }
            else
            {
                foreach (var x in self.Subnodes)
                {
                    if (x != null)
                    {
                        foreach (var y in x.Value.ToChunk(fromRelativeDepth - 1)) yield return y;
                    }
                }
            }
        }
    }
}
