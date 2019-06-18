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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using static Aardvark.Data.Durable;

namespace Aardvark.Geometry.Points
{

    /// <summary>
    /// A filtered view onto a point cloud.
    /// </summary>
    public class FilteredNode : IPointCloudNode
    {
        #region Construction

        /// <summary>
        /// </summary>
        public static IPointCloudNode Create(Guid id, IPointCloudNode node, IFilter filter)
        {
            if (filter.IsFullyOutside(node)) return null;
            if (filter.IsFullyInside(node)) return node;
            return new FilteredNode(id, node, filter);
        }

        /// <summary></summary>
        private FilteredNode(Guid id, IPointCloudNode node, IFilter filter)
        {
            Id = id;
            Node = node ?? throw new ArgumentNullException(nameof(node));
            Filter = filter ?? throw new ArgumentNullException(nameof(filter));
            m_activePoints = Filter.FilterPoints(Node, m_activePoints);
        }

        /// <summary></summary>
        public static IPointCloudNode Create(IPointCloudNode node, IFilter filter)
            => Create(Guid.NewGuid(), node, filter);

        #endregion

        #region Properties

        private PersistentRef<IPointCloudNode>[] m_subnodes_cache;

        private readonly HashSet<int> m_activePoints;

        /// <summary></summary>
        public Guid Id { get; }

        /// <summary> </summary>
        public IPointCloudNode Node { get; }

        /// <summary></summary>
        public IFilter Filter { get; }

        /// <summary></summary>
        public Storage Storage => Node.Storage;

        /// <summary></summary>
        public bool IsMaterialized => false;

        /// <summary></summary>
        public IPointCloudNode Materialize()
        {
            var newId = Guid.NewGuid();
            var data = ImmutableDictionary<Def, object>.Empty
                .Add(Octree.NodeId, newId)
                .Add(Octree.Cell, Cell)
                .Add(Octree.BoundingBoxExactLocal, BoundingBoxExactLocal)
                ;

            if (IsLeaf)
            {
                data = data.Add(Octree.BoundingBoxExactGlobal, BoundingBoxExactGlobal);
            }
            else
            {
                var subnodes = Subnodes.Map(x => x?.Value.Materialize());
                var subnodeIds = subnodes.Map(x => x?.Id ?? Guid.Empty);
                var bbExactGlobal = new Box3d(subnodes.Where(x => x != null).Select(x => x.BoundingBoxExactGlobal));
                data = data
                    .Add(Octree.SubnodesGuids, subnodeIds)
                    .Add(Octree.BoundingBoxExactGlobal, bbExactGlobal)
                    ;
            }

            if (HasPositions)
            {
                var id = Guid.NewGuid();
                Storage.Add(id, Positions.Value);
                data = data.Add(Octree.PositionsLocal3fReference, id);
            }

            if (HasKdTree)
            {
                var id = Guid.NewGuid();
                Storage.Add(id, KdTree.Value.Data);
                data = data.Add(Octree.PointRkdTreeFDataReference, id);
            }

            if (HasColors)
            {
                var id = Guid.NewGuid();
                Storage.Add(id, Colors.Value);
                data = data.Add(Octree.Colors4bReference, id);
            }

            if (HasNormals)
            {
                var id = Guid.NewGuid();
                Storage.Add(id, Normals.Value);
                data = data.Add(Octree.Normals3fReference, id);
            }

            if (HasClassifications)
            {
                var id = Guid.NewGuid();
                Storage.Add(id, Classifications.Value);
                data = data.Add(Octree.Classifications1bReference, id);
            }

            if (HasIntensities)
            {
                var id = Guid.NewGuid();
                Storage.Add(id, Intensities.Value);
                data = data.Add(Octree.Intensities1iReference, id);
            }

            var result = new PointSetNode(data, Storage, writeToStore: false)
                .WithComputedTreeDepth()
                .WithComputedCentroid()
                .WithComputedPointDistance()
                .WriteToStore()
                ;
            return result;
        }

        /// <summary></summary>
        public Cell Cell => Node.Cell;

        /// <summary></summary>
        public V3d Center => Node.Center;

        /// <summary></summary>
        public long PointCountTree => Node.PointCountTree;

        /// <summary></summary>
        public bool Has(Def what) => Node.Has(what);

        /// <summary></summary>
        public bool TryGetValue(Def what, out object o) => Node.TryGetValue(what, out o);

        /// <summary></summary>
        public PersistentRef<IPointCloudNode>[] Subnodes
        {
            get
            {
                if (Node.Subnodes == null) return null;

                if (m_subnodes_cache == null)
                {
                    m_subnodes_cache = new PersistentRef<IPointCloudNode>[8];
                    for (var i = 0; i < 8; i++)
                    {
                        var id = (Id + "." + i).ToGuid();
                        var n0 = Node.Subnodes[i]?.Value;
                        var n = n0 != null ? new FilteredNode(id, n0, Filter) : null;
                        m_subnodes_cache[i] = new PersistentRef<IPointCloudNode>(id.ToString(), _ => n, _ => (true, n));
                    }
                }
                return m_subnodes_cache;
            }
        }

        /// <summary></summary>
        public int PointCountCell => m_activePoints.Count;

        /// <summary></summary>
        public bool IsLeaf => Node.IsLeaf;

        #region Positions

        /// <summary></summary>
        public bool HasPositions => Node.HasPositions;

        /// <summary></summary>
        public PersistentRef<V3f[]> Positions
        {
            get
            {
                EnsurePositionsAndDerived();
                return (PersistentRef<V3f[]>)m_cache[Octree.PositionsLocal3f.Id];
            }
        }

        /// <summary></summary>
        public V3d[] PositionsAbsolute
        {
            get
            {
                EnsurePositionsAndDerived();
                return (V3d[])m_cache[Octree.PositionsGlobal3d.Id];
            }
        }

        private bool m_ensuredPositionsAndDerived = false;
        private void EnsurePositionsAndDerived()
        {
            if (m_ensuredPositionsAndDerived) return;

            var result = GetSubArray(Octree.PositionsLocal3f, Node.Positions);
            var psLocal = result.Value;

            var c = Center;
            var psGlobal = psLocal.Map(p => (V3d)p + c);
            m_cache[Octree.PositionsGlobal3d.Id] = psGlobal;

            var bboxLocal = psLocal.Length > 0 ? new Box3f(psLocal) : Box3f.Invalid;
            m_cache[Octree.BoundingBoxExactLocal.Id] = bboxLocal;

            var kd = psLocal.BuildKdTree();
            var pRefKd = new PersistentRef<PointRkdTreeF<V3f[], V3f>>(Guid.NewGuid().ToString(), _ => kd, _ => (true, kd));
            m_cache[Octree.PointRkdTreeFData.Id] = pRefKd;

            m_ensuredPositionsAndDerived = true;
        }


        #endregion

        #region BoundingBoxExactLocal

        /// <summary></summary>
        public bool HasBoundingBoxExactLocal => Node.HasBoundingBoxExactLocal;

        /// <summary></summary>
        public Box3f BoundingBoxExactLocal
        {
            get
            {
                EnsurePositionsAndDerived();
                return (Box3f)m_cache[Octree.BoundingBoxExactLocal.Id];
            }
        }

        #endregion

        #region BoundingBoxExactGlobal

        /// <summary></summary>
        public bool HasBoundingBoxExactGlobal => Node.HasBoundingBoxExactGlobal;

        /// <summary></summary>
        public Box3d BoundingBoxExactGlobal => Node.BoundingBoxExactGlobal;

        #endregion

        #region KdTree

        /// <summary></summary>
        public bool HasKdTree => Node.HasKdTree;

        /// <summary></summary>
        public PersistentRef<PointRkdTreeF<V3f[], V3f>> KdTree
        {
            get
            {
                EnsurePositionsAndDerived();
                return (PersistentRef<PointRkdTreeF<V3f[], V3f>>)m_cache[Octree.PointRkdTreeFData.Id];
            }
        }

        #endregion

        #region Colors

        /// <summary></summary>
        public bool HasColors => Node.HasColors;

        /// <summary></summary>
        public PersistentRef<C4b[]> Colors => GetSubArray(Octree.Colors4b, Node.Colors);

        #endregion

        #region Normals

        /// <summary></summary>
        public bool HasNormals => Node.HasNormals;

        /// <summary></summary>
        public PersistentRef<V3f[]> Normals => GetSubArray(Octree.Normals3f, Node.Normals);

        #endregion

        #region Intensities

        /// <summary></summary>
        public bool HasIntensities => Node.HasIntensities;

        /// <summary></summary>
        public PersistentRef<int[]> Intensities => GetSubArray(Octree.Intensities1i, Node.Intensities);

        #endregion

        #region Classifications

        /// <summary></summary>
        public bool HasClassifications => Node.HasClassifications;

        /// <summary></summary>
        public PersistentRef<byte[]> Classifications => GetSubArray(Octree.Classifications1b, Node.Classifications);

        #endregion

        #region CentroidLocal

        /// <summary></summary>
        public bool HasCentroidLocal => Node.HasCentroidLocal;

        /// <summary></summary>
        public V3f CentroidLocal => Node.CentroidLocal;

        /// <summary></summary>
        public bool HasCentroidLocalAverageDist => Node.HasCentroidLocalAverageDist;

        /// <summary></summary>
        public float CentroidLocalAverageDist => Node.CentroidLocalAverageDist;

        /// <summary></summary>
        public bool HasCentroidLocalStdDev => Node.HasCentroidLocalStdDev;

        /// <summary></summary>
        public float CentroidLocalStdDev => Node.CentroidLocalStdDev;

        #endregion

        #region TreeDepth

        /// <summary></summary>
        public bool HasMinTreeDepth => Node.HasMinTreeDepth;

        /// <summary></summary>
        public int MinTreeDepth => Node.MinTreeDepth;

        /// <summary></summary>
        public bool HasMaxTreeDepth => Node.HasMaxTreeDepth;

        /// <summary></summary>
        public int MaxTreeDepth => Node.MaxTreeDepth;

        #endregion

        #region PointDistance

        /// <summary></summary>
        public bool HasPointDistanceAverage => Node.HasPointDistanceAverage;

        /// <summary>
        /// Average distance of points in this cell.
        /// </summary>
        public float PointDistanceAverage => Node.PointDistanceAverage;

        /// <summary></summary>
        public bool HasPointDistanceStandardDeviation => Node.HasPointDistanceStandardDeviation;

        /// <summary>
        /// Standard deviation of distance of points in this cell.
        /// </summary>
        public float PointDistanceStandardDeviation => Node.PointDistanceStandardDeviation;

        #endregion

        private readonly Dictionary<Guid, object> m_cache = new Dictionary<Guid, object>();
        private PersistentRef<T[]> GetSubArray<T>(Def def, PersistentRef<T[]> originalValue)
        {
            if (m_cache.TryGetValue(def.Id, out var o) && o is PersistentRef<T[]> x) return x;

            if (originalValue == null) return null;
            var key = (Id + originalValue.Id).ToGuid().ToString();
            var xs = originalValue.Value.Where((_, i) => m_activePoints.Contains(i)).ToArray();
            var result = new PersistentRef<T[]>(key, _ => xs, _ => (true, xs));
            m_cache[def.Id] = result;
            return result;
        }

        #endregion

        #region Not supported ...

        /// <summary>
        /// Filtered not does not support WithUpsert.
        /// </summary>
        public IPointCloudNode WithUpsert(Durable.Def def, object x)
            => throw new InvalidOperationException("Invariant 3de7dad1-668d-4104-838b-552eae03f7a8.");

        /// <summary></summary>
        public IPointCloudNode WithSubNodes(IPointCloudNode[] subnodes)
            => throw new InvalidOperationException("Invariant 62e6dab8-133a-452d-8d8c-f0b0eb5f286c.");

        #endregion

        #region Durable codec

        /// <summary></summary>
        public static class Defs
        {
            /// <summary></summary>
            public static readonly Def FilteredNode = new Def(
                new Guid("a5dd1687-ea0b-4735-9be1-b74b969e0673"),
                "Octree.FilteredNode", "Octree.FilteredNode. A filtered octree node.",
                Primitives.DurableMap.Id, false
                );

            /// <summary></summary>
            public static readonly Def FilteredNodeRootId = new Def(
                new Guid("f9a7c994-35b3-4d50-b5b0-80af05896987"),
                "Octree.FilteredNode.RootId", "Octree.FilteredNode. Node id of the node to be filtered.",
                Primitives.GuidDef.Id, false
                );

            /// <summary></summary>
            public static readonly Def FilteredNodeFilter = new Def(
                new Guid("1d2298b6-df47-4170-8fc2-4bd899ea6153"),
                "Octree.FilteredNode.Filter", "Octree.FilteredNode. Filter definition as UTF8-encoded JSON string.",
                Primitives.StringUTF8.Id, false
                );
        }

        /// <summary></summary>
        public IPointCloudNode WriteToStore()
        {
            var buffer = Encode();
            Storage.Add(Id, buffer);
            return this;
        }

        /// <summary>
        /// </summary>
        public byte[] Encode()
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                var filter = Filter.Serialize().ToString(Formatting.Indented);

                var x = ImmutableDictionary<Def, object>.Empty
                    .Add(Octree.NodeId, Id)
                    .Add(Defs.FilteredNodeRootId, Node.Id)
                    .Add(Defs.FilteredNodeFilter, filter)
                    ;

                Data.Codec.Encode(bw, Defs.FilteredNode, x);
                bw.Flush();
                return ms.ToArray();
            }
        }

        /// <summary>
        /// </summary>
        public static FilteredNode Decode(Storage storage, byte[] buffer)
        {
            using (var ms = new MemoryStream(buffer))
            using (var br = new BinaryReader(ms))
            {
                var r = Data.Codec.Decode(br);
                if (r.Item1 != Defs.FilteredNode) throw new InvalidOperationException("Invariant c03cfd90-a083-44f2-a00f-cb36b1735f37.");
                var data = (ImmutableDictionary<Def, object>)r.Item2;
                var id = (Guid)data.Get(Octree.NodeId);
                var filterString = (string)data.Get(Defs.FilteredNodeFilter);
                var filter = Points.Filter.Deserialize(filterString);
                var rootId = (Guid)data.Get(Defs.FilteredNodeRootId);
                var root = storage.GetPointCloudNode(rootId);
                return new FilteredNode(id, root, filter);
            }
        }

        #endregion
    }
}
