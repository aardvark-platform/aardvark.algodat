/*
    Copyright (C) 2006-2018. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System.Runtime.CompilerServices;
using System.Threading;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// An immutable point cloud octree node.
    /// </summary>
    public interface IPointCloudNode : IDisposable
    {
        /// <summary>
        /// Backing store.
        /// </summary>
        Storage Storage { get; }

        /// <summary>
        /// Writes this node to store.
        /// </summary>
        IPointCloudNode WriteToStore();

        /// <summary>
        /// Key.
        /// </summary>
        Guid Id { get; }

        /// <summary>
        /// This node's index/bounds.
        /// </summary>
        Cell Cell { get; }

        /// <summary>
        /// Center of this node's cell.
        /// </summary>
        V3d Center { get; }

        /// <summary>
        /// Exact bounding box of all points in this node (global space).
        /// </summary>
        Box3d BoundingBoxExactGlobal { get; }

        /// <summary>
        /// Octree. Number of points in this cell.
        /// Durable definition 172e1f20-0ffc-4d9c-9b3d-903fca41abe3.
        /// </summary>
        int PointCountCell { get; }

        /// <summary>
        /// Number of points in this tree (sum of leaf points).
        /// </summary>
        long PointCountTree { get; }

        /// <summary>
        /// Average distance of points in this cell.
        /// </summary>
        float PointDistanceAverage { get; }

        /// <summary>
        /// Standard deviation of distance of points in this cell.
        /// </summary>
        float PointDistanceStandardDeviation { get; }
        
        /// <summary>
        /// Subnodes (8), or null if leaf.
        /// Entries are null if there is no subnode.
        /// There is at least 1 non-null entry.
        /// </summary>
        PersistentRef<IPointCloudNode>[] Subnodes { get; }

        /// <summary>
        /// Returns new node with replaced subnodes.
        /// </summary>
        IPointCloudNode WithSubNodes(IPointCloudNode[] subnodes);

        /// <summary>
        /// Node has no subnodes.
        /// </summary>
        bool IsLeaf { get; }

        /// <summary>
        /// Returns true if node has given property.
        /// </summary>
        bool Has(Durable.Def what);

        /// <summary>
        /// </summary>
        bool TryGetValue(Durable.Def what, out object o);

        /// <summary>
        /// </summary>
        FilterState FilterState { get; }

        /// <summary>
        /// Returns new node with added/replaced data.
        /// If existing entry is replaced, then the node gets a new id.
        /// Node is NOT written to store. Use WriteToStore if you want this.
        /// </summary>
        IPointCloudNode WithUpsert(Durable.Def def, object x);



        /// <summary>
        /// </summary>
        bool HasPositions { get; }

        /// <summary>
        /// Octree. Per-point positions in local cell space (as offsets from cell's center).
        /// Durable definition 05eb38fa-1b6a-4576-820b-780163199db9.
        /// </summary>
        PersistentRef<V3f[]> Positions { get; }

        /// <summary>
        /// Octree. Per-point positions in global space.
        /// Durable definition 61ef7c1e-6aeb-45cd-85ed-ad0ed2584553.
        /// </summary>
        V3d[] PositionsAbsolute { get; }

        /// <summary>
        /// </summary>
        bool HasKdTree { get; }

        /// <summary>
        /// </summary>
        PersistentRef<PointRkdTreeF<V3f[], V3f>> KdTree { get; }



        /// <summary>
        /// </summary>
        bool HasColors { get; }

        /// <summary>
        /// Octree. Per-point colors C4b.
        /// Durable definition c91dfea3-243d-4272-9dba-b572931dba23.
        /// </summary>
        PersistentRef<C4b[]> Colors { get; }


        /// <summary>
        /// </summary>
        bool HasNormals { get; }

        /// <summary>
        /// Octree. Per-point V3f normals.
        /// Durable definition 712d0a0c-a8d0-42d1-bfc7-77eac2e4a755.
        /// </summary>
        PersistentRef<V3f[]> Normals { get; }



        /// <summary>
        /// </summary>
        bool HasIntensities { get; }

        /// <summary>
        /// Octree. Per-point int32 intensities..
        /// Durable definition 361027fd-ac58-4de8-89ee-98695f8c5520.
        /// </summary>
        PersistentRef<int[]> Intensities { get; }



        /// <summary>
        /// </summary>
        bool HasClassifications { get; }

        /// <summary>
        /// Octree. Per-point uint8 classifications.
        /// Durable definition bf0975e4-43bd-4742-9e61-c7469d81805d.
        /// </summary>
        PersistentRef<byte[]> Classifications { get; }
    }

    /// <summary>
    /// </summary>
    public static class IPointCloudNodeExtensions
    {
        #region ForEach (optionally traversing out-of-core nodes) 

        /// <summary>
        /// Calls action for each node in this tree.
        /// </summary>
        public static void ForEachNode(this IPointCloudNode self, bool outOfCore, Action<IPointCloudNode> action)
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
        /// Calls action for each (node, fullyInside) in this pointset, that is intersecting the given hull.
        /// </summary>
        public static void ForEachIntersectingNode(this IPointCloudNode self, bool outOfCore, Hull3d hull, bool doNotTraverseSubnodesWhenFullyInside,
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
                    if (n != null)
                    {
                        n.Value.ForEachIntersectingNode(outOfCore, hull, doNotTraverseSubnodesWhenFullyInside, action, ct);
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
                            node.ForEachIntersectingNode(outOfCore, hull, doNotTraverseSubnodesWhenFullyInside, action, ct);
                        }
                    }
                }
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
            self.BoundingBoxExactGlobal.GetMinMaxInDirection(plane.Normal, out V3d min, out V3d max);
            return plane.Height(min) > 0;
        }

        /// <summary>
        /// Returns true if this node is fully inside the negative halfspace defined by given plane.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool InsideNegativeHalfSpace(this IPointCloudNode self, in Plane3d plane)
        {
            self.BoundingBoxExactGlobal.GetMinMaxInDirection(-plane.Normal, out V3d min, out V3d max);
            return plane.Height(min) < 0;
        }

        #endregion

        #region Counts (optionally traversing out-of-core nodes)

        /// <summary>
        /// Total number of nodes.
        /// </summary>
        public static long CountNodes(this IPointCloudNode self, bool outOfCore)
        {
            var count = 1L;
            if (self.Subnodes != null)
            {
                if (outOfCore)
                {
                    for (var i = 0; i < 8; i++)
                    {
                        var n = self.Subnodes[i];
                        if (n != null) count += n.Value.CountNodes(outOfCore);
                    }
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
                    if (n != null) n.Value.GetAverageTreeDepth(outOfCore, depth, ref sum, ref count);
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

        /// <summary>
        /// Computes centroid of absolute (LoD) positions in this node, or null if no (LoD) positions.
        /// </summary>
        public static V3d? ComputeCentroid(this IPointCloudNode self) => self.HasPositions ? self.PositionsAbsolute.Average() : (V3d?)null;

    }
}
