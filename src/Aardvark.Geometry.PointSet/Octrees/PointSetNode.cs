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
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

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
        public PointSetNode(Storage storage, bool writeToStore, params (Durable.Def, object)[] props)
            : this(
                  ImmutableDictionary<Durable.Def, object>.Empty.AddRange(props.Select(x => new KeyValuePair<Durable.Def, object>(x.Item1, x.Item2))),
                  storage, writeToStore
                  )
        {
        }

        /// <summary>
        /// Creates node.
        /// </summary>
        public PointSetNode(
            ImmutableDictionary<Durable.Def, object> data,
            Storage storage, bool writeToStore
            )
        {
            if (!data.ContainsKey(Durable.Octree.NodeId)) throw new ArgumentException(
                "Missing Durable.Octree.NodeId. Invariant a14a3d76-36cd-430b-b160-42d7a53f916d."
                );

            if (!data.ContainsKey(Durable.Octree.Cell)) throw new ArgumentException(
                "Missing Durable.Octree.Cell. Invariant f6f13915-c254-4d88-b60f-5e673c13ef59."
                );

            Storage = storage;
            Data = data;

            var bboxCell = Cell.BoundingBox;
            Center = bboxCell.Center;
            Corners = bboxCell.ComputeCorners();

#if DEBUG
            Report.Line($"new PointSetNode({Id})");
#endif

            #region Subnodes

            if (Data.TryGetValue(Durable.Octree.SubnodesGuids, out object o) && o is Guid[] subNodeIds)
            {
                Subnodes = new PersistentRef<IPointCloudNode>[8];
                for (var i = 0; i < 8; i++)
                {
                    if (subNodeIds[i] == Guid.Empty) continue;
                    var pRef = new PersistentRef<IPointCloudNode>(subNodeIds[i].ToString(), storage.GetPointCloudNode, storage.TryGetPointCloudNode);
                    Subnodes[i] = pRef;
                }
            }

            #endregion

            #region Per-point attributes

            var psId = PositionsId;
            var csId = ColorsId;
            var kdId = KdTreeId;
            var nsId = NormalsId;
            var isId = IntensitiesId;
            var ksId = ClassificationsId;

            if (psId != null) PersistentRefs[Durable.Octree.PositionsLocal3fReference] = new PersistentRef<V3f[]>(psId.ToString(), storage.GetV3fArray, storage.TryGetV3fArray);
            if (csId != null) PersistentRefs[Durable.Octree.Colors4bReference] = new PersistentRef<C4b[]>(csId.ToString(), storage.GetC4bArray, storage.TryGetC4bArray);
            if (kdId != null) PersistentRefs[Durable.Octree.PointRkdTreeFDataReference] = new PersistentRef<PointRkdTreeF<V3f[], V3f>>(kdId.ToString(), LoadKdTree, TryLoadKdTree);
            if (nsId != null) PersistentRefs[Durable.Octree.Normals3fReference] = new PersistentRef<V3f[]>(nsId.ToString(), storage.GetV3fArray, storage.TryGetV3fArray);
            if (isId != null) PersistentRefs[Durable.Octree.Intensities1iReference] = new PersistentRef<int[]>(isId.ToString(), storage.GetIntArray, storage.TryGetIntArray);
            if (ksId != null) PersistentRefs[Durable.Octree.Classifications1bReference] = new PersistentRef<byte[]>(ksId.ToString(), storage.GetByteArray, storage.TryGetByteArray);

            #endregion

            #region Durable.Octree.BoundingBoxExactLocal, Durable.Octree.BoundingBoxExactGlobal

            if (HasPositions && (!HasBoundingBoxExactLocal || !HasBoundingBoxExactGlobal))
            {
                var bboxExactLocal = Positions.Value.Length > 0
                    ? (HasBoundingBoxExactLocal ? BoundingBoxExactLocal : new Box3f(Positions.Value))
                    : Box3f.Invalid
                    ;

                if (!HasBoundingBoxExactLocal)
                {
                    Data = Data.Add(Durable.Octree.BoundingBoxExactLocal, bboxExactLocal);
                }

                if (!HasBoundingBoxExactGlobal)
                {
                    if (IsLeaf)
                    {
                        var bboxExactGlobal = (Box3d)bboxExactLocal + Center;
                        Data = Data.Add(Durable.Octree.BoundingBoxExactGlobal, bboxExactGlobal);
                    }
                    else
                    {
                        var bboxExactGlobal = new Box3d(Subnodes.Where(x => x != null).Select(x => x.Value.BoundingBoxExactGlobal));
                        Data = Data.Add(Durable.Octree.BoundingBoxExactGlobal, bboxExactGlobal);
                    }
                }
            }

            #endregion

            #region Durable.Octree.PointCountCell

            if (!Has(Durable.Octree.PointCountCell))
            {
                Data = Data.Add(Durable.Octree.PointCountCell, HasPositions ? Positions.Value.Length : 0);
            }

            #endregion

            #region Durable.Octree.PointCountTreeLeafs

            if (!Has(Durable.Octree.PointCountTreeLeafs))
            {
                if (IsLeaf)
                {
                    Data = Data.Add(Durable.Octree.PointCountTreeLeafs, (long)PointCountCell);
                }
                else
                {
                    Data = Data.Add(Durable.Octree.PointCountTreeLeafs, Subnodes.Where(x => x != null).Sum(x => x.Value.PointCountTree));
                }
            }

            #endregion

            #region Centroid*

            if (HasPositions && (!HasCentroidLocal || !HasCentroidLocalAverageDist || !HasCentroidLocalStdDev))
            {
                var ps = Positions.Value;
                var centroid = ps.ComputeCentroid();

                if (!HasCentroidLocal)
                {
                    Data = Data.Add(Durable.Octree.PositionsLocal3fCentroid, centroid);
                }

                var dists = ps.Map(p => (p - centroid).Length);
                var (avg, sd) = dists.ComputeAvgAndStdDev();

                if (!HasCentroidLocalAverageDist)
                {
                    Data = Data.Add(Durable.Octree.PositionsLocal3fDistToCentroidAverage, avg);
                }

                if (!HasCentroidLocalStdDev)
                {
                    Data = Data.Add(Durable.Octree.PositionsLocal3fDistToCentroidStdDev, sd);
                }
            }
            #endregion

            #region TreeDepth*

            if (!HasMinTreeDepth || !HasMaxTreeDepth)
            {
                if (IsLeaf)
                {
                    if (!HasMinTreeDepth)
                    {
                        Data = Data.Add(Durable.Octree.MinTreeDepth, 0);
                    }
                    if (!HasMaxTreeDepth)
                    {
                        Data = Data.Add(Durable.Octree.MaxTreeDepth, 0);
                    }
                }
                else
                {
                    var min = 1;
                    var max = 0;
                    for (var i = 0; i < 8; i++)
                    {
                        var r = Subnodes[i];
                        if (r == null) continue;
                        var n = r.Value;
                        min = Math.Min(min, 1 + n.MinTreeDepth);
                        max = Math.Max(max, 1 + n.MaxTreeDepth);
                    }
                    if (!HasMinTreeDepth)
                    {
                        Data = Data.Add(Durable.Octree.MinTreeDepth, min);
                    }
                    if (!HasMaxTreeDepth)
                    {
                        Data = Data.Add(Durable.Octree.MaxTreeDepth, max);
                    }
                }
            }

            #endregion

            #region PointDistance*

            if (HasPositions && (!HasPointDistanceAverage || !HasPointDistanceStandardDeviation))
            {
                var ps = Positions.Value;
                var kd = KdTree.Value;

                if (ps.Length < 2)
                {
                    if (!HasPointDistanceAverage)
                    {
                        Data = Data.Add(Durable.Octree.AveragePointDistance, 0.0f);
                    }
                    if (!HasPointDistanceStandardDeviation)
                    {
                        Data = Data.Add(Durable.Octree.AveragePointDistanceStdDev, 0.0f);
                    }
                }
                else if (ps.Length == 3)
                {
                    var d = V3f.Distance(ps[0], ps[1]);
                    if (!HasPointDistanceAverage)
                    {
                        Data = Data.Add(Durable.Octree.AveragePointDistance, d);
                    }
                    if (!HasPointDistanceStandardDeviation)
                    {
                        Data = Data.Add(Durable.Octree.AveragePointDistanceStdDev, 0.0f);
                    }
                }
                else
                {
                    var indexDists = ps.Map(p => kd.GetClosest(p, float.MaxValue, 2));
                    var ds = indexDists.Map(x => V3f.Distance(ps[x[0].Index], ps[x[1].Index]));
                    var (avg, sd) = ds.ComputeAvgAndStdDev();
                    if (!HasPointDistanceAverage)
                    {
                        Data = Data.Add(Durable.Octree.AveragePointDistance, avg);
                    }
                    if (!HasPointDistanceStandardDeviation)
                    {
                        Data = Data.Add(Durable.Octree.AveragePointDistanceStdDev, sd);
                    }
                }
            }

            #endregion

            if (writeToStore)
            {
                Report.Warn($"[writeToStore] {Id}");
                WriteToStore();
            }

#if DEBUG
            if (IsLeaf)
            {
                if (PointCountCell != PointCountTree) throw new InvalidOperationException("Invariant 9464f38c-dc98-4d68-a8ac-0baed9f182b4.");
                if (!Has(Durable.Octree.PositionsLocal3fReference)) throw new ArgumentException("Missing Durable.Octree.PositionsLocal3fReference.");
                if (PositionsId == null) throw new InvalidOperationException();
                if (KdTreeId == null) throw new InvalidOperationException();
            }
#endif
#if PEDANTIC
            if (PositionsId != null && Positions.Value.Length != PointCount) throw new InvalidOperationException("Invariant 926ca077-845d-44ba-a1db-07dfe06e7cc3.");
#endif

            PointRkdTreeF<V3f[], V3f> LoadKdTree(string key)
            {
                var value = Storage.GetPointRkdTreeFData(key);
                var ps = Positions.Value;
                return new PointRkdTreeF<V3f[], V3f>(
                    3, ps.Length, ps,
                    (xs, i) => xs[(int)i], (v, i) => (float)v[i],
                    (a, b) => V3f.Distance(a, b), (i, a, b) => b - a,
                    (a, b, c) => VecFun.DistanceToLine(a, b, c), VecFun.Lerp, 1e-6f,
                    value
                    );
            }

            (bool, PointRkdTreeF<V3f[], V3f>) TryLoadKdTree(string key)
            {
                var (ok, value) = Storage.TryGetPointRkdTreeFData(key);
                if (ok == false) return (false, default);
                var ps = Positions.Value;
                return (true, new PointRkdTreeF<V3f[], V3f>(
                    3, ps.Length, ps,
                    (xs, i) => xs[(int)i], (v, i) => (float)v[i],
                    (a, b) => V3f.Distance(a, b), (i, a, b) => b - a,
                    (a, b, c) => VecFun.DistanceToLine(a, b, c), VecFun.Lerp, 1e-6f,
                    value
                    ));
            }
        }

        /// <summary>
        /// Writes this node to store.
        /// </summary>
        public IPointCloudNode WriteToStore()
        {
            this.CheckDerivedAttributes();
            Storage.Add(Id.ToString(), this);
            return this;
        }

        #endregion

        #region Properties (state to serialize)

        /// <summary>
        /// Durable properties.
        /// </summary>
        private ImmutableDictionary<Durable.Def, object> Data { get; } = ImmutableDictionary<Durable.Def, object>.Empty;

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
        [JsonIgnore]
        private Dictionary<Durable.Def, object> PersistentRefs { get; } = new Dictionary<Durable.Def, object>();

        #region Cell attributes

        /// <summary>
        /// This node's unique id (16 bytes).
        /// </summary>
        [JsonIgnore]
        public Guid Id => (Guid)Data.Get(Durable.Octree.NodeId);

        /// <summary>
        /// This node's index/bounds.
        /// </summary>
        [JsonIgnore]
        public Cell Cell => (Cell)Data.Get(Durable.Octree.Cell);

        /// <summary>
        /// Octree. Number of points in this cell.
        /// Durable definition 172e1f20-0ffc-4d9c-9b3d-903fca41abe3.
        /// </summary>
        [JsonIgnore]
        public int PointCountCell => (int)Data.Get(Durable.Octree.PointCountCell);


        /// <summary>
        /// Number of points in this tree (sum of leaves).
        /// </summary>
        [JsonIgnore]
        public long PointCountTree => (long)Data.Get(Durable.Octree.PointCountTreeLeafs);

        /// <summary>
        /// Subnodes (8), or null if leaf.
        /// </summary>
        [JsonIgnore]
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

        #region BoundingBoxExactLocal

        /// <summary></summary>
        [JsonIgnore]
        public bool HasBoundingBoxExactLocal => Data.ContainsKey(Durable.Octree.BoundingBoxExactLocal);

        /// <summary></summary>
        [JsonIgnore]
        public Box3f BoundingBoxExactLocal => Data.TryGetValue(Durable.Octree.BoundingBoxExactLocal, out var o) ? (Box3f)o : default;

        #endregion

        #region BoundingBoxExactGlobal

        /// <summary></summary>
        [JsonIgnore]
        public bool HasBoundingBoxExactGlobal => Data.ContainsKey(Durable.Octree.BoundingBoxExactGlobal);

        /// <summary></summary>
        [JsonIgnore]
        public Box3d BoundingBoxExactGlobal => Data.TryGetValue(Durable.Octree.BoundingBoxExactGlobal, out var o) ? (Box3d)o : default;

        #endregion

        #region Colors

        /// <summary></summary>
        [JsonIgnore]
        public bool HasColors => Data.ContainsKey(Durable.Octree.Colors4bReference);

        /// <summary></summary>
        [JsonIgnore]
        public Guid? ColorsId => Data.TryGetValue(Durable.Octree.Colors4bReference, out var id) ? (Guid?)id : null;

        /// <summary>
        /// Point colors, or null if no points.
        /// </summary>
        [JsonIgnore]
        public PersistentRef<C4b[]> Colors => PersistentRefs.TryGetValue(Durable.Octree.Colors4bReference, out object x) ? (PersistentRef<C4b[]>)x : null;

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
        public PersistentRef<PointRkdTreeF<V3f[], V3f>> KdTree => PersistentRefs.TryGetValue(Durable.Octree.PointRkdTreeFDataReference, out object x) ? (PersistentRef<PointRkdTreeF<V3f[], V3f>>)x : null;

        #endregion

        #region CentroidLocal

        /// <summary></summary>
        public bool HasCentroidLocal => Data.ContainsKey(Durable.Octree.PositionsLocal3fCentroid);

        /// <summary></summary>
        public V3f CentroidLocal => (V3f)Data.Get(Durable.Octree.PositionsLocal3fCentroid);

        /// <summary></summary>
        public bool HasCentroidLocalAverageDist => Data.ContainsKey(Durable.Octree.PositionsLocal3fDistToCentroidAverage);

        /// <summary></summary>
        public float CentroidLocalAverageDist => (float)Data.Get(Durable.Octree.PositionsLocal3fDistToCentroidAverage);

        /// <summary></summary>
        public bool HasCentroidLocalStdDev => Data.ContainsKey(Durable.Octree.PositionsLocal3fDistToCentroidStdDev);

        /// <summary></summary>
        public float CentroidLocalStdDev => (float)Data.Get(Durable.Octree.PositionsLocal3fDistToCentroidStdDev);

        #endregion

        #region TreeDepth

        /// <summary></summary>
        public bool HasMinTreeDepth => Data.ContainsKey(Durable.Octree.MinTreeDepth);

        /// <summary></summary>
        public int MinTreeDepth => (int)Data.Get(Durable.Octree.MinTreeDepth);

        /// <summary></summary>
        public bool HasMaxTreeDepth => Data.ContainsKey(Durable.Octree.MaxTreeDepth);

        /// <summary></summary>
        public int MaxTreeDepth => (int)Data.Get(Durable.Octree.MaxTreeDepth);

        #endregion

        #region PointDistance

        /// <summary></summary>
        public bool HasPointDistanceAverage => Data.ContainsKey(Durable.Octree.AveragePointDistance);

        /// <summary></summary>
        public float PointDistanceAverage => (Data.TryGetValue(Durable.Octree.AveragePointDistance, out var value) && value is float x) ? x : -1.0f;

        /// <summary></summary>
        public bool HasPointDistanceStandardDeviation => Data.ContainsKey(Durable.Octree.AveragePointDistanceStdDev);

        /// <summary></summary>
        public float PointDistanceStandardDeviation => (Data.TryGetValue(Durable.Octree.AveragePointDistanceStdDev, out var value) && value is float x) ? x : -1.0f;

        #endregion

        /// <summary>
        /// Subnodes (8), or null if leaf.
        /// </summary>
        [JsonIgnore]
        public readonly PersistentRef<IPointCloudNode>[] Subnodes;

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

        #region Immutable updates (With...)

        /// <summary>
        /// Returns new node with replaced subnodes.
        /// </summary>
        public IPointCloudNode WithSubNodes(IPointCloudNode[] subnodes)
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

            return this
                .WithUpsert(Durable.Octree.Cell, Cell)
                .WithUpsert(Durable.Octree.PointCountTreeLeafs, pointCountTree ?? 0L)
                .WithUpsert(Durable.Octree.SubnodesGuids, subnodes.Map(x => x?.Id ?? Guid.Empty))
                .WriteToStore()
                ;
        }

        /// <summary>
        /// Returns new node with added/replaced data.
        /// If existing entry is replaced, then the node gets a new id.
        /// Node is NOT written to store. Use WriteToStore if you want this.
        /// </summary>
        public PointSetNode WithUpsert(Durable.Def def, object x)
        {
            var data = Data
                .RemoveRange(new[] { def, Durable.Octree.NodeId })
                .Add(Durable.Octree.NodeId, Guid.NewGuid())
                .Add(def, x)
                ;
            
            return new PointSetNode(data, Storage, false);
        }

        /// <summary>
        /// Returns new node with given entry removed or same node if entry does not exist.
        /// </summary>
        internal PointSetNode Without(Durable.Def def, bool writeToStore = true)
        {
            if (Data.ContainsKey(def))
            {
                var data = Data
                    .RemoveRange(new[] { def, Durable.Octree.NodeId })
                    .Add(Durable.Octree.NodeId, Guid.NewGuid())
                    ;
                return new PointSetNode(data, Storage, writeToStore);
            }
            else
            {
                return this;
            }
        }

        #endregion

        #region Durable codec

        /// <summary>
        /// </summary>
        public byte[] Encode()
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                Aardvark.Data.Codec.Encode(bw, Durable.Octree.Node, Data);
                bw.Flush();
                return ms.ToArray();
            }
        }

        /// <summary>
        /// </summary>
        public static PointSetNode Decode(Storage storage, byte[] buffer)
        {
            using (var ms = new MemoryStream(buffer))
            using (var br = new BinaryReader(ms))
            {
                var r = Aardvark.Data.Codec.Decode(br);
                if (r.Item1 != Durable.Octree.Node) throw new InvalidOperationException("Invariant 5cc4cbfe-07c8-4b92-885d-b1d397210e41.");
                var data = (ImmutableDictionary<Durable.Def, object>)r.Item2;
                return new PointSetNode(data, storage, false);
            }
        }

        #endregion

        #region IPointCloudNode

        Guid IPointCloudNode.Id => Id;

        Cell IPointCloudNode.Cell => Cell;

        V3d IPointCloudNode.Center => Center;

        long IPointCloudNode.PointCountTree => PointCountTree;

        /// <summary></summary>
        public bool Has(Durable.Def what) => Data.ContainsKey(what);

        /// <summary></summary>
        public bool TryGetValue(Durable.Def what, out object o) => Data.TryGetValue(what, out o);

        Storage IPointCloudNode.Storage => Storage;

        /// <summary></summary>
        public bool IsMaterialized => true;

        /// <summary></summary>
        public IPointCloudNode Materialize() => this;

        PersistentRef<IPointCloudNode>[] IPointCloudNode.Subnodes => Subnodes;

        IPointCloudNode IPointCloudNode.WithUpsert(Durable.Def def, object x) => WithUpsert(def, x);

        #endregion
    }
}
