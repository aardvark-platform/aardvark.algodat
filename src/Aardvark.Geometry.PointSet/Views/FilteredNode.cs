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
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

namespace Aardvark.Geometry.Points
{

    /// <summary>
    /// A filtered view onto a point cloud.
    /// </summary>
    public class FilteredNode : IPointCloudNode
    {
        /// <summary>
        /// 
        /// </summary>
        public static readonly Durable.Def Def = new Durable.Def(
            new Guid("a5dd1687-ea0b-4735-9be1-b74b969e0673"),
            "Octree.Node",
            "Octree. A filtered octree node.",
            Durable.Primitives.StringUTF8.Id,
            false
            );

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

        #region Properties (state to serialize)

        private PersistentRef<IPointCloudNode>[] m_subnodes_cache;

        private readonly HashSet<int> m_activePoints;

        /// <summary></summary>
        public Guid Id { get; }

        /// <summary> </summary>
        public IPointCloudNode Node { get; }

        /// <summary></summary>
        public IFilter Filter { get; }

        #endregion

        #region Properties (derived/runtime, non-serialized)

        /// <summary></summary>
        public Storage Storage => Node.Storage;

        /// <summary></summary>
        public Cell Cell => Node.Cell;

        /// <summary></summary>
        public V3d Center => Node.Center;

        /// <summary></summary>
        public Box3d BoundingBoxExactGlobal => Node.BoundingBoxExactGlobal;

        /// <summary></summary>
        public long PointCountTree => Node.PointCountTree;

        /// <summary></summary>
        public float PointDistanceAverage => Node.PointDistanceAverage;

        /// <summary></summary>
        public float PointDistanceStandardDeviation => Node.PointDistanceStandardDeviation;

        /// <summary></summary>
        public bool Has(Durable.Def what) => Node.Has(what);

        /// <summary></summary>
        public bool TryGetValue(Durable.Def what, out object o) => Node.TryGetValue(what, out o);

        /// <summary></summary>
        public void Dispose() => Node.Dispose();

        #endregion



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

        private PersistentRef<T[]> GetSubArray<T>(PersistentRef<T[]> originalValue)
        {
            var key = (Id + originalValue.Id).ToGuid().ToString();
            var xs = originalValue.Value.Where((_, i) => m_activePoints.Contains(i)).ToArray();
            return new PersistentRef<T[]>(key, _ => xs, _ => (true, xs));
        }

        /// <summary></summary>
        public int PointCountCell => m_activePoints.Count;

        /// <summary></summary>
        public bool HasPositions => Node.HasPositions;

        /// <summary></summary>
        public bool HasKdTree => Node.HasKdTree;

        /// <summary></summary>
        public bool HasColors => Node.HasColors;

        /// <summary></summary>
        public bool HasNormals => Node.HasNormals;

        /// <summary></summary>
        public bool HasIntensities => Node.HasIntensities;

        /// <summary></summary>
        public bool HasClassifications => Node.HasClassifications;

        /// <summary></summary>
        public bool IsLeaf => Node.IsLeaf;

        /// <summary></summary>
        public PersistentRef<V3f[]> Positions => GetSubArray(Node.Positions);

        /// <summary></summary>
        public V3d[] PositionsAbsolute { get { var c = Center; return Positions.Value.Map(p => (V3d)p + c); } }

        /// <summary></summary>
        public PersistentRef<PointRkdTreeF<V3f[], V3f>> KdTree => throw new NotImplementedException();

        /// <summary></summary>
        public PersistentRef<C4b[]> Colors => GetSubArray(Node.Colors);

        /// <summary></summary>
        public PersistentRef<V3f[]> Normals => GetSubArray(Node.Normals);

        /// <summary></summary>
        public PersistentRef<int[]> Intensities => GetSubArray(Node.Intensities);

        /// <summary></summary>
        public PersistentRef<byte[]> Classifications => GetSubArray(Node.Classifications);

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

        /// <summary></summary>
        public IPointCloudNode WriteToStore()
        {
            var buffer = Encode();
            Storage.Add(Id, buffer);
            return this;
        }

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

        /// <summary>
        /// </summary>
        public byte[] Encode()
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                var s = "TODO";
                Data.Codec.Encode(bw, Durable.Primitives.GuidDef, Def.Id);
                Data.Codec.Encode(bw, Def, s);
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
                var r = Data.Codec.Decode(br);
                if (r.Item1 != Durable.Octree.Node) throw new InvalidOperationException("Invariant 24b085b9-f745-4af6-8897-b04bfbe830ad.");
                var data = (ImmutableDictionary<Durable.Def, object>)r.Item2;
                return new PointSetNode(data, storage, false);
            }
        }

        #endregion
    }
}
