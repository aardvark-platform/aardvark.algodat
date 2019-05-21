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

//#define PEDANTIC

using Aardvark.Base;
using Aardvark.Data;
using Aardvark.Data.Points;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// An immutable point cloud node.
    /// </summary>
    public class PointSetNode : IPointCloudNode
    {
        #region Construction

        /// <summary>
        /// Creates node.
        /// </summary>
        public PointSetNode(
            ImmutableDictionary<Durable.Def, object> data,
            Storage storage, bool writeToStore
            )
        {
            if (!data.ContainsKey(Durable.Octree.NodeId)) throw new ArgumentException("Missing Durable.Octree.NodeId.");
            if (!data.ContainsKey(Durable.Octree.Cell)) throw new ArgumentException("Missing Durable.Octree.Cell.");

            Data = data;
            Storage = storage;
            BoundingBox = Cell.BoundingBox;
            Center = BoundingBox.Center;
            Corners = BoundingBox.ComputeCorners();

            if (IsLeaf && PointCount != PointCountTree) throw new InvalidOperationException("Invariant 9464f38c-dc98-4d68-a8ac-0baed9f182b4.");

            var psId = PositionsId;
            var csId = ColorsId;
            var kdId = KdTreeId;
            var nsId = NormalsId;
            var isId = IntensitiesId;
            var ksId = ClassificationsId;

            if (psId != null) PersistentRefs[Durable.Octree.PositionsLocal3fReference] = new PersistentRef<V3f[]>(psId.ToString(), storage.GetV3fArray, storage.TryGetV3fArray);
            if (csId != null) PersistentRefs[Durable.Octree.Colors3bReference] = new PersistentRef<C4b[]>(csId.ToString(), storage.GetC4bArray, storage.TryGetC4bArray);
            if (kdId != null) PersistentRefs[Durable.Octree.PointRkdTreeFDataReference] = new PersistentRef<PointRkdTreeD<V3f[], V3f>>(kdId.ToString(), LoadKdTree, TryLoadKdTree);
            if (nsId != null) PersistentRefs[Durable.Octree.Normals3fReference] = new PersistentRef<V3f[]>(nsId.ToString(), storage.GetV3fArray, storage.TryGetV3fArray);
            if (isId != null) PersistentRefs[Durable.Octree.Intensities1iReference] = new PersistentRef<int[]>(isId.ToString(), storage.GetIntArray, storage.TryGetIntArray);
            if (ksId != null) PersistentRefs[Durable.Octree.Classifications1bReference] = new PersistentRef<byte[]>(ksId.ToString(), storage.GetByteArray, storage.TryGetByteArray);

            if (Data.TryGetValue(Durable.Octree.SubnodesGuids, out object _subnodeIds))
            {
                var subnodeIds = (Guid[])_subnodeIds;

                if (subnodeIds != null)
                {
                    Subnodes = new PersistentRef<PointSetNode>[8];
                    for (var i = 0; i < 8; i++)
                    {
                        if (subnodeIds[i] == null) continue;
                        var pRef = new PersistentRef<PointSetNode>(subnodeIds[i].ToString(), storage.GetPointSetNode, storage.TryGetPointSetNode);
                        Subnodes[i] = pRef;

#if DEBUG && PEDANTIC
                    var subNodeIndex = pRef.Value.Cell;
                    if (Cell.GetOctant(i) != subNodeIndex) throw new InvalidOperationException();
                    if (!Cell.Contains(subNodeIndex)) throw new InvalidOperationException();
                    if (Cell.Exponent != subNodeIndex.Exponent + 1) throw new InvalidOperationException();
#endif
                    }
#if DEBUG && PEDANTIC
                if (PointCountTree != PointCount + Subnodes.Map(x => x?.Value != null ? x.Value.PointCountTree : 0).Sum()) throw new InvalidOperationException();
#endif
                }
            }

            if (writeToStore) storage.Add(Id.ToString(), this);

#if DEBUG
            if (PositionsId == null && PointCount != 0) throw new InvalidOperationException();
#if PEDANTIC
            if (PositionsId != null && Positions.Value.Length != PointCount) throw new InvalidOperationException();
#endif
            if (IsLeaf)
            {
                if (PositionsId == null) throw new InvalidOperationException();
                if (KdTreeId == null) throw new InvalidOperationException();
            }
#endif

            PointRkdTreeD<V3f[], V3f> LoadKdTree(string key)
            {
                var value = Storage.GetPointRkdTreeDData(key);
                var ps = Positions.Value;
                return new PointRkdTreeD<V3f[], V3f>(
                    3, ps.Length, ps,
                    (xs, i) => xs[(int)i], (v, i) => (float)v[i],
                    (a, b) => V3f.Distance(a, b), (i, a, b) => b - a,
                    (a, b, c) => VecFun.DistanceToLine(a, b, c), VecFun.Lerp, 1e-9,
                    value
                    );
            }

            (bool, PointRkdTreeD<V3f[], V3f>) TryLoadKdTree(string key)
            {
                var (ok, value) = Storage.TryGetPointRkdTreeDData(key);
                if (ok == false) return (false, default);
                var ps = Positions.Value;
                return (true, new PointRkdTreeD<V3f[], V3f>(
                    3, ps.Length, ps,
                    (xs, i) => xs[(int)i], (v, i) => (float)v[i],
                    (a, b) => V3f.Distance(a, b), (i, a, b) => b - a,
                    (a, b, c) => VecFun.DistanceToLine(a, b, c), VecFun.Lerp, 1e-9,
                    value
                    ));
            }
        }

        #endregion

        #region Properties (state to serialize)

        /// <summary>
        /// Durable properties.
        /// </summary>
        public ImmutableDictionary<Durable.Def, object> Data { get; } = ImmutableDictionary<Durable.Def, object>.Empty;

        #endregion

        #region Properties (derived/runtime, non-serialized)

        /// <summary>
        /// Runtime.
        /// </summary>
        [JsonIgnore]
        public readonly Storage Storage;

        /// <summary>
        /// Runtime.
        /// </summary>
        private Dictionary<Durable.Def, object> PersistentRefs { get; } = new Dictionary<Durable.Def, object>();

        #region Cell attributes

        /// <summary>
        /// This node's unique id (16 bytes).
        /// </summary>
        public Guid Id => (Guid)Data.Get(Durable.Octree.NodeId);

        /// <summary>
        /// This node's index/bounds.
        /// </summary>
        public Cell Cell => (Cell)Data.Get(Durable.Octree.Cell);

        /// <summary>
        /// Number of points in this tree (sum of leaves).
        /// </summary>
        public long PointCountTree => (long)Data.Get(Durable.Octree.PointCountTreeLeafs);

        /// <summary>
        /// Subnodes (8), or null if leaf.
        /// </summary>
        public Guid?[] SubnodeIds
        {
            get
            {
                if (Data.TryGetValue(Durable.Octree.SubnodesGuids, out object o))
                {
                    return ((Guid[])o).Map(x => x != Guid.Empty ? x : (Guid?)null);
                }
                else
                {
                    return null;
                }
            }
        }

        #endregion

        #region Positions

        /// <summary></summary>
        [JsonIgnore]
        public bool HasPositions => Data.ContainsKey(Durable.Octree.PositionsLocal3fReference);

        /// <summary></summary>
        [JsonIgnore]
        public Guid? PositionsId => Data.TryGetValue(Durable.Octree.PositionsLocal3fReference, out var id) ? (Guid?)id : null;

        /// <summary>
        /// Point positions relative to cell's center, or null if no positions.
        /// </summary>
        [JsonIgnore]
        public PersistentRef<V3f[]> Positions => PersistentRefs.TryGetValue(Durable.Octree.PositionsLocal3fReference, out object x) ? (PersistentRef<V3f[]>)x : null;

        /// <summary>
        /// Point positions (absolute), or null if no positions.
        /// </summary>
        [JsonIgnore]
        public V3d[] PositionsAbsolute => Positions?.Value.Map(p => new V3d(Center.X + p.X, Center.Y + p.Y, Center.Z + p.Z));

        #endregion

        #region Colors

        /// <summary></summary>
        [JsonIgnore]
        public bool HasColors => Data.ContainsKey(Durable.Octree.Colors3bReference);

        /// <summary></summary>
        [JsonIgnore]
        public Guid? ColorsId => Data.TryGetValue(Durable.Octree.Colors3bReference, out var id) ? (Guid?)id : null;

        /// <summary>
        /// Point colors, or null if no points.
        /// </summary>
        [JsonIgnore]
        public PersistentRef<C4b[]> Colors => PersistentRefs.TryGetValue(Durable.Octree.Colors3bReference, out object x) ? (PersistentRef<C4b[]>)x : null;

        #endregion

        #region Normals

        /// <summary></summary>
        [JsonIgnore]
        public bool HasNormals => Data.ContainsKey(Durable.Octree.Normals3fReference);

        /// <summary></summary>
        [JsonIgnore]
        public Guid? NormalsId => Data.TryGetValue(Durable.Octree.Normals3fReference, out var id) ? (Guid?)id : null;

        /// <summary></summary>
        [JsonIgnore]
        public PersistentRef<V3f[]> Normals => PersistentRefs.TryGetValue(Durable.Octree.Normals3fReference, out object x) ? (PersistentRef<V3f[]>)x : null;

        #endregion

        #region Intensities

        /// <summary></summary>
        [JsonIgnore]
        public bool HasIntensities => Data.ContainsKey(Durable.Octree.Intensities1iReference);

        /// <summary></summary>
        [JsonIgnore]
        public Guid? IntensitiesId => Data.TryGetValue(Durable.Octree.Intensities1iReference, out var id) ? (Guid?)id : null;

        /// <summary></summary>
        [JsonIgnore]
        public PersistentRef<int[]> Intensities => PersistentRefs.TryGetValue(Durable.Octree.Intensities1iReference, out object x) ? (PersistentRef<int[]>)x : null;

        #endregion

        #region Classifications

        /// <summary></summary>
        [JsonIgnore]
        public bool HasClassifications => Data.ContainsKey(Durable.Octree.Classifications1bReference);

        /// <summary></summary>
        [JsonIgnore]
        public Guid? ClassificationsId => Data.TryGetValue(Durable.Octree.Classifications1bReference, out var id) ? (Guid?)id : null;

        /// <summary></summary>
        [JsonIgnore]
        public PersistentRef<byte[]> Classifications => PersistentRefs.TryGetValue(Durable.Octree.Classifications1bReference, out object x) ? (PersistentRef<byte[]>) x : null;

        #endregion

        #region KdTree

        /// <summary></summary>
        [JsonIgnore]
        public bool HasKdTree => Data.ContainsKey(Durable.Octree.PointRkdTreeFDataReference);

        /// <summary></summary>
        [JsonIgnore]
        public Guid? KdTreeId => Data.TryGetValue(Durable.Octree.PointRkdTreeFDataReference, out var id) ? (Guid?)id : null;

        /// <summary></summary>
        [JsonIgnore]
        public PersistentRef<PointRkdTreeD<V3f[], V3f>> KdTree => PersistentRefs.TryGetValue(Durable.Octree.PointRkdTreeFDataReference, out object x) ? (PersistentRef<PointRkdTreeD<V3f[], V3f>>)x : null;

        #endregion

        /// <summary>
        /// Number of points in this node (without subnodes).
        /// Is always 0 for inner nodes. 
        /// </summary>
        [JsonIgnore]
        public long PointCount => IsLeaf ? PointCountTree : 0;
        
        /// <summary>
        /// Subnodes (8), or null if leaf.
        /// </summary>
        [JsonIgnore]
        public readonly PersistentRef<PointSetNode>[] Subnodes;

        /// <summary>
        /// Bounding box of this node's cell.
        /// </summary>
        [JsonIgnore]
        public readonly Box3d BoundingBox;

        /// <summary>
        /// Center of this node's cell.
        /// </summary>
        [JsonIgnore]
        public readonly V3d Center;

        /// <summary>
        /// Corners of this node's cell.
        /// </summary>
        [JsonIgnore]
        public readonly V3d[] Corners;

        /// <summary>
        /// Node has no subnodes.
        /// </summary>
        [JsonIgnore]
        public bool IsLeaf => SubnodeIds == null;

        /// <summary>
        /// Node has subnodes.
        /// </summary>
        [JsonIgnore]
        public bool IsNotLeaf => SubnodeIds != null;
        
        /// <summary>
        /// Gets whether this node is centered at the origin.
        /// </summary>
        [JsonIgnore]
        public bool IsCenteredAtOrigin => Cell.IsCenteredAtOrigin;

        /// <summary>
        /// Gets number of subnodes.
        /// </summary>
        [JsonIgnore]
        public int SubnodeCount => SubnodeIds == null ? 0 : SubnodeIds.Count(x => x != null);

        #endregion

        #region Counts (optionally traversing out-of-core nodes)

        /// <summary>
        /// Total number of nodes.
        /// </summary>
        public long CountNodes(bool outOfCore)
        {
            var count = 1L;
            if (Subnodes != null)
            {
                if (outOfCore)
                {
                    for (var i = 0; i < 8; i++)
                    {
                        var n = Subnodes[i];
                        if (n != null) count += n.Value.CountNodes(outOfCore);
                    }
                }
                else
                {
                    for (var i = 0; i < 8; i++)
                    {
                        var n = Subnodes[i];
                        if (n != null)
                        {
                            if (n.TryGetValue(out PointSetNode node)) count += node.CountNodes(outOfCore);
                        }
                    }
                }
            }
            return count;
        }

        /// <summary>
        /// Number of inner nodes.
        /// </summary>
        public long CountInnerNodes(bool outOfCore)
        {
            long count = 0;
            if (Subnodes != null)
            {
                if (outOfCore)
                {
                    for (var i = 0; i < 8; i++)
                    {
                        var n = Subnodes[i];
                        if (n != null) count += n.Value.CountInnerNodes(outOfCore);
                    }
                }
                else
                {
                    for (var i = 0; i < 8; i++)
                    {
                        var n = Subnodes[i];
                        if (n != null)
                        {
                            if (n.TryGetValue(out PointSetNode node)) count += node.CountInnerNodes(outOfCore);
                        }
                    }
                }
            }
            return count;
        }

        /// <summary>
        /// Number of leaf nodes.
        /// </summary>
        public long CountLeafNodes(bool outOfCore)
        {
            if (Subnodes == null) return 1;

            var count = 0L;
            if (outOfCore)
            {
                for (var i = 0; i < 8; i++)
                {
                    var n = Subnodes[i];
                    if (n != null) count += n.Value.CountLeafNodes(outOfCore);
                }
            }
            else
            {
                for (var i = 0; i < 8; i++)
                {
                    var n = Subnodes[i];
                    if (n != null)
                    {
                        if (n.TryGetValue(out PointSetNode node)) count += node.CountLeafNodes(outOfCore);
                    }
                }
            }
            return count;
        }

        /// <summary>
        /// Gets minimum point count of leaf nodes.
        /// </summary>
        public long GetMinimumLeafPointCount(bool outOfCore)
        {
            var min = long.MaxValue;
            if (Subnodes != null)
            {
                if (outOfCore)
                {
                    for (var i = 0; i < 8; i++)
                    {
                        var n = Subnodes[i];
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
                        var n = Subnodes[i];
                        if (n != null)
                        {
                            if (n.TryGetValue(out PointSetNode node))
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
        public long GetMaximumLeafPointCount(bool outOfCore)
        {
            var max = long.MinValue;
            if (Subnodes != null)
            {
                if (outOfCore)
                {
                    for (var i = 0; i < 8; i++)
                    {
                        var n = Subnodes[i];
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
                        var n = Subnodes[i];
                        if (n != null)
                        {
                            if (n.TryGetValue(out PointSetNode node))
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
        public double GetAverageLeafPointCount(bool outOfCore)
        {
            return PointCountTree / (double)CountNodes(outOfCore);
        }

        /// <summary>
        /// Depth of tree (minimum).
        /// </summary>
        public int GetMinimumTreeDepth(bool outOfCore)
        {
            if (Subnodes == null) return 1;

            var min = int.MaxValue;

            if (outOfCore)
            {
                for (var i = 0; i < 8; i++)
                {
                    var n = Subnodes[i];
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
                    var n = Subnodes[i];
                    if (n != null)
                    {
                        if (n.TryGetValue(out PointSetNode node))
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
        public int GetMaximiumTreeDepth(bool outOfCore)
        {
            if (Subnodes == null) return 1;

            var max = 0;

            if (outOfCore)
            {
                for (var i = 0; i < 8; i++)
                {
                    var n = Subnodes[i];
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
                    var n = Subnodes[i];
                    if (n != null)
                    {
                        if (n.TryGetValue(out PointSetNode node))
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
        public double GetAverageTreeDepth(bool outOfCore)
        {
            long sum = 0, count = 0;
            GetAverageTreeDepth(outOfCore, 1, ref sum, ref count);
            return sum / (double)count;
        }
        private void GetAverageTreeDepth(bool outOfCore, int depth, ref long sum, ref long count)
        {
            if (Subnodes == null)
            {
                sum += depth; count++;
                return;
            }

            ++depth;

            if (outOfCore)
            {
                for (var i = 0; i < 8; i++)
                {
                    var n = Subnodes[i];
                    if (n != null) n.Value.GetAverageTreeDepth(outOfCore, depth, ref sum, ref count);
                }
            }
            else
            {
                for (var i = 0; i < 8; i++)
                {
                    var n = Subnodes[i];
                    if (n != null)
                    {
                        if (n.TryGetValue(out PointSetNode node)) node.GetAverageTreeDepth(outOfCore, depth, ref sum, ref count);
                    }
                }
            }
        }

        #endregion

        #region ForEach (optionally traversing out-of-core nodes) 

        /// <summary>
        /// Calls action for each node in this tree.
        /// </summary>
        public void ForEachNode(bool outOfCore, Action<PointSetNode> action)
        {
            action(this);

            if (Subnodes == null) return;
            
            if (outOfCore)
            {
                for (var i = 0; i < 8; i++)
                {
                    Subnodes[i]?.Value.ForEachNode(outOfCore, action);
                }
            }
            else
            {
                for (var i = 0; i < 8; i++)
                {
                    var n = Subnodes[i];
                    if (n != null)
                    {
                        if (n.TryGetValue(out PointSetNode node)) node.ForEachNode(outOfCore, action);
                    }
                }
            }
        }
        
        /// <summary>
        /// Calls action for each (node, fullyInside) in this pointset, that is intersecting the given hull.
        /// </summary>
        public void ForEachIntersectingNode(bool outOfCore, Hull3d hull, bool doNotTraverseSubnodesWhenFullyInside,
            Action<PointSetNode, bool> action, CancellationToken ct = default(CancellationToken))
        {
            ct.ThrowIfCancellationRequested();

            for (var i = 0; i < hull.PlaneCount; i++)
            {
                if (!IntersectsNegativeHalfSpace(hull.PlaneArray[i])) return;
            }

            bool fullyInside = true;
            for (var i = 0; i < hull.PlaneCount; i++)
            {
                if (!InsideNegativeHalfSpace(hull.PlaneArray[i]))
                {
                    fullyInside = false;
                    break;
                }
            }

            action(this, fullyInside);

            if (fullyInside && doNotTraverseSubnodesWhenFullyInside) return;

            if (Subnodes == null) return;
            
            if (outOfCore)
            {
                for (var i = 0; i < 8; i++)
                {
                    var n = Subnodes[i];
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
                    var n = Subnodes[i];
                    if (n != null)
                    {
                        if (n.TryGetValue(out PointSetNode node))
                        {
                            node.ForEachIntersectingNode(outOfCore, hull, doNotTraverseSubnodesWhenFullyInside, action, ct);
                        }
                    }
                }
            }
        }

        #endregion

        #region Immutable updates (With...)

        /// <summary>
        /// Replaces subnodes.
        /// </summary>
        internal PointSetNode WithSubNodes(PointSetNode[] subnodes)
        {
            if (subnodes == null) throw new ArgumentNullException(nameof(subnodes));

            if (IsLeaf) throw new InvalidOperationException();
#if DEBUG
            for (var i = 0; i < 8; i++)
            {
                var sn = subnodes[i]; if (sn == null) continue;
                if (sn.Cell.Exponent != this.Cell.Exponent - 1)
                {
                    throw new InvalidOperationException("Invariant c79cd9a4-3e44-46c8-9a7f-5f7e09627f1a.");
                }
            }
#endif

            var pointCountTree = subnodes.Sum(x => x?.PointCountTree);
            var data = Data
                .Add(Durable.Octree.Cell, Cell)
                .Add(Durable.Octree.PointCountTreeLeafs, pointCountTree ?? 0)
                .Add(Durable.Octree.SubnodesGuids, subnodes.Map(x => x?.Id ?? Guid.Empty))
                ;
            return new PointSetNode(data, Storage, true);
        }

        /// <summary>
        /// Makes new node with added data. Existing entries are replaced.
        /// </summary>
        internal PointSetNode WithAddedOrReplacedData(ImmutableDictionary<Durable.Def, object> additionalData)
        {
            var data = Data.AddRange(additionalData)
                   .Add(Durable.Octree.Cell, Cell)
                   .Add(Durable.Octree.PointCountTreeLeafs, PointCountTree)
                   .Add(Durable.Octree.SubnodesGuids, SubnodeIds.Map(x => x ?? Guid.Empty))
                   ;
            return new PointSetNode(data, Storage, true);
        }

        #endregion

        #region Intersections, inside/outside, ...

        /// <summary>
        /// Index of subnode for given point.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetSubIndex(in V3d p)
        {
            var i = 0;
            if (p.X > Center.X) i = 1;
            if (p.Y > Center.Y) i += 2;
            if (p.Z > Center.Z) i += 4;
            return i;
        }

        /// <summary>
        /// Returns true if this node intersects the positive halfspace defined by given plane.
        /// </summary>
        public bool IntersectsPositiveHalfSpace(in Plane3d plane)
        {
            for (var i = 0; i < 8; i++)
            {
                if (plane.Height(Corners[i]) > 0) return true;
            }
            return false;
        }

        /// <summary>
        /// Returns true if this node intersects the negative halfspace defined by given plane.
        /// </summary>
        public bool IntersectsNegativeHalfSpace(in Plane3d plane)
        {
            for (var i = 0; i < 8; i++)
            {
                if (plane.Height(Corners[i]) < 0) return true;
            }
            return false;
        }

        /// <summary>
        /// Returns true if this node is fully inside the positive halfspace defined by given plane.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool InsidePositiveHalfSpace(in Plane3d plane)
        {
            BoundingBox.GetMinMaxInDirection(plane.Normal, out V3d min, out V3d max);
            return plane.Height(min) > 0;
        }

        /// <summary>
        /// Returns true if this node is fully inside the negative halfspace defined by given plane.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool InsideNegativeHalfSpace(in Plane3d plane)
        {
            BoundingBox.GetMinMaxInDirection(-plane.Normal, out V3d min, out V3d max);
            return plane.Height(min) < 0;
        }

        #endregion

        #region IPointCloudNode

        Guid IPointCloudNode.Id => Id;

        Cell IPointCloudNode.Cell => Cell;

        V3d IPointCloudNode.Center => Center;

        long IPointCloudNode.PointCountTree => PointCountTree;

        PersistentRef<IPointCloudNode>[] IPointCloudNode.SubNodes
        {
            get
            {
                if (Subnodes == null) return null;
                return new[]
                {
                    Subnodes[0] == null ? null : new PersistentRef<IPointCloudNode>(Subnodes[0].Id, _ => Subnodes[0].Value, _ => Subnodes[0].TryGetValue()),
                    Subnodes[1] == null ? null : new PersistentRef<IPointCloudNode>(Subnodes[1].Id, _ => Subnodes[1].Value, _ => Subnodes[1].TryGetValue()),
                    Subnodes[2] == null ? null : new PersistentRef<IPointCloudNode>(Subnodes[2].Id, _ => Subnodes[2].Value, _ => Subnodes[2].TryGetValue()),
                    Subnodes[3] == null ? null : new PersistentRef<IPointCloudNode>(Subnodes[3].Id, _ => Subnodes[3].Value, _ => Subnodes[3].TryGetValue()),
                    Subnodes[4] == null ? null : new PersistentRef<IPointCloudNode>(Subnodes[4].Id, _ => Subnodes[4].Value, _ => Subnodes[4].TryGetValue()),
                    Subnodes[5] == null ? null : new PersistentRef<IPointCloudNode>(Subnodes[5].Id, _ => Subnodes[5].Value, _ => Subnodes[5].TryGetValue()),
                    Subnodes[6] == null ? null : new PersistentRef<IPointCloudNode>(Subnodes[6].Id, _ => Subnodes[6].Value, _ => Subnodes[6].TryGetValue()),
                    Subnodes[7] == null ? null : new PersistentRef<IPointCloudNode>(Subnodes[7].Id, _ => Subnodes[7].Value, _ => Subnodes[7].TryGetValue()),
                };
            }
        }

        Storage IPointCloudNode.Storage => Storage;


        
        /// <summary></summary>
        public Box3f BoundingBoxExactLocal
        {
            get
            {
                if (Data.TryGetValue(Durable.Octree.BoundingBoxExactLocal, out var value) && value is Box3f)
                {
                    return (Box3f)value;
                }
                else
                {
                    var hs = 0.5f * (V3f)BoundingBox.Size;
                    return new Box3f(-hs, hs);
                }
            }
        }
        /// <summary></summary>
        Box3d IPointCloudNode.BoundingBoxExact
        {
            get
            {
                if (Data.TryGetValue(Durable.Octree.BoundingBoxExactLocal, out var value) && value is Box3f)
                {
                    var box = (Box3f)value;
                    var c = BoundingBox.Center;
                    return new Box3d(c + (V3d)box.Min, c + (V3d)box.Max);
                }
                else return BoundingBox;
            }
        }
        /// <summary></summary>
        float IPointCloudNode.PointDistanceAverage
        {
            get
            {
                if (Data.TryGetValue(Durable.Octree.AveragePointDistance, out var value) && value is float)
                    return (float)value;
                else
                    return -1.0f;
            }
        }

        /// <summary></summary>
        float IPointCloudNode.PointDistanceStandardDeviation
        {
            get
            {
                if (Data.TryGetValue(Durable.Octree.AveragePointDistanceStdDev, out var value) && value is float)
                    return (float)value;
                else
                    return -1.0f;
            }
        }
        
        /// <summary></summary>
        public FilterState FilterState => FilterState.FullyInside;
        
        /// <summary></summary>
        public JObject ToJson()
        {
            throw new NotImplementedException();
        }

        /// <summary></summary>
        public string NodeType => "PointSetNode";

        /// <summary></summary>
        public void Dispose() { }
        
        #endregion
    }
}
