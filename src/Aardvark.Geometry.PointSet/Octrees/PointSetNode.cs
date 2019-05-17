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
        public PointSetNode(Guid id,
            Cell cell, long pointCountTree,
            ImmutableDictionary<DurableDataDefinition, object> data,
            Guid?[] subnodeIds, Storage storage, bool writeToStore
            )
        {
            Storage = storage;
            Id = id;
            Cell = cell;
            PointCountTree = pointCountTree;
            SubnodeIds = subnodeIds;
            Data = data;

            if (IsLeaf && PointCount != PointCountTree) throw new InvalidOperationException();

            var psId = PositionsId;
            var csId = ColorsId;
            var kdId = KdTreeId;
            var nsId = NormalsId;
            var isId = IntensitiesId;
            var ksId = ClassificationsId;

            if (psId != null) PersistentRefs[OctreeAttributes.RefPositionsLocal3f] = new PersistentRef<V3f[]>(psId.ToString(), storage.GetV3fArray, storage.TryGetV3fArray);
            if (csId != null) PersistentRefs[OctreeAttributes.RefColors3b] = new PersistentRef<C4b[]>(csId.ToString(), storage.GetC4bArray, storage.TryGetC4bArray);
            if (kdId != null) PersistentRefs[OctreeAttributes.RefKdTreeLocal3f] = new PersistentRef<PointRkdTreeD<V3f[], V3f>>(kdId.ToString(), LoadKdTree, TryLoadKdTree);
            if (nsId != null) PersistentRefs[OctreeAttributes.RefNormals3f] = new PersistentRef<V3f[]>(nsId.ToString(), storage.GetV3fArray, storage.TryGetV3fArray);
            if (isId != null) PersistentRefs[OctreeAttributes.RefIntensities1i] = new PersistentRef<int[]>(isId.ToString(), storage.GetIntArray, storage.TryGetIntArray);
            if (ksId != null) PersistentRefs[OctreeAttributes.RefClassifications1b]  = new PersistentRef<byte[]>(ksId.ToString(), storage.GetByteArray, storage.TryGetByteArray);

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

            BoundingBox = Cell.BoundingBox;
            Center = BoundingBox.Center;
            Corners = BoundingBox.ComputeCorners();
            
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

        /// <summary>
        /// Creates inner node.
        /// </summary>
        internal PointSetNode(
            Cell cell, long pointCountTree, ImmutableDictionary<DurableDataDefinition, object> data,
            Guid?[] subnodeIds,
            Storage storage
            ) : this(Guid.NewGuid(), cell, pointCountTree, data, subnodeIds, storage, true)
        {
        }

        /// <summary>
        /// Creates leaf node.
        /// </summary>
        internal PointSetNode(
            Cell cell, long pointCountTree, ImmutableDictionary<DurableDataDefinition, object> data,
            Storage storage
            ) : this(Guid.NewGuid(), cell, pointCountTree, data, null,       storage, true)
        {
        }

        #endregion

        #region Properties (state to serialize)

        /// <summary>
        /// </summary>
        public ImmutableDictionary<DurableDataDefinition, object> Data { get; } = ImmutableDictionary<DurableDataDefinition, object>.Empty;

        /// <summary>
        /// This node's unique id (16 bytes).
        /// </summary>
        public readonly Guid Id;

        /// <summary>
        /// This node's index/bounds.
        /// </summary>
        public readonly Cell Cell;

        /// <summary>
        /// Number of points in this tree (sum of leaves).
        /// </summary>
        public readonly long PointCountTree;
        
        /// <summary>
        /// Subnodes (8), or null if leaf.
        /// </summary>
        public readonly Guid?[] SubnodeIds;

        #endregion

        #region Serialization

        ///// <summary></summary>
        //public int SerializedSizeInBytes =>
        //    1 + 3 +                 // subcellmask (8bit), attribute mask (24bit)
        //    16 +                    // Guid
        //    (3 * 8 + 4) +           // Bounds (Cell)
        //    8 +                     // PointCountTree
        //    SubnodeCount * 16 +     // subcell keys
        //    Attributes.Count * 16 + // attribute keys
        //    4 + CustomAttributes.Values.Sum((o) => 16 + Binary.SizeOf(o))
        //    ;

        ///// <summary>
        ///// </summary>
        //public byte[] ToBinary()
        //{
        //    var count = SerializedSizeInBytes;
        //    var buffer = new byte[count];
        //    using (var ms = new MemoryStream(buffer))
        //    using (var bw = new BinaryWriter(ms))
        //    {
        //        var attributemask = (uint)AttributeMask;
        //        bw.Write(attributemask | ((uint)SubnodeMask << 24));
        //        bw.Write(Id.ToByteArray());
        //        bw.Write(Cell.X); bw.Write(Cell.Y); bw.Write(Cell.Z); bw.Write(Cell.Exponent);
        //        bw.Write(PointCountTree);
        //        if (SubnodeIds != null)
        //        {
        //            foreach (var x in SubnodeIds) if (x.HasValue) bw.Write(x.Value.ToByteArray());
        //        }
        //        var a = 1u;
        //        for (var i = 0; i < 23; i++, a <<= 1)
        //        {
        //            if ((attributemask & a) == 0) continue;
        //            bw.Write(Attributes[(PointSetAttributes)a].ToByteArray());
        //        }

        //        if ((attributemask & (uint)PointSetAttributes.HasCellAttributes) != 0)
        //        {
        //            var temp = new byte[512];
        //            int offset;

        //            bw.Write(CustomAttributes.Count);
        //            foreach (var kvp in CustomAttributes)
        //            {
        //                offset = 0;
        //                Binary.Write(temp, ref offset, kvp.Key);
        //                Binary.Write(kvp.Value, temp, ref offset);
        //                bw.Write(temp, 0, offset);
        //            }


        //            ////  4 bytes (uint)
        //            //bw.Write((uint)CellAttributeMask);

        //            //// 24 bytes (Box3f)
        //            //if ((CellAttributeMask | CellAttributes.BoundingBoxExactLocal) != 0)
        //            //{
        //            //    ref readonly var min = ref BoundingBoxExactLocal.Min;
        //            //    ref readonly var max = ref BoundingBoxExactLocal.Max;
        //            //    bw.Write(min.X); bw.Write(min.Y); bw.Write(min.Z);
        //            //    bw.Write(max.X); bw.Write(max.Y); bw.Write(max.Z);
        //            //}

        //            ////  8 bytes (float + float)
        //            //if ((CellAttributeMask | CellAttributes.PointDistance) != 0)
        //            //{
        //            //    bw.Write(PointDistanceAverage);
        //            //    bw.Write(PointDistanceStandardDeviation);
        //            //}
        //        }
        //    }
        //    return buffer;
        //}
        
//        /// <summary>
//        /// </summary>
//        public static PointSetNode ParseBinary(byte[] buffer, Storage storage)
//        {
//            var masks = BitConverter.ToUInt32(buffer, 0);
//            var subcellmask = masks >> 24;
//            var attributemask = masks & 0b00000000_11111111_11111111_11111111;

//            var offset = 4;
//            var id = ParseGuid(buffer, ref offset);
            
//            var cell = new Cell(
//                BitConverter.ToInt64(buffer, 20),
//                BitConverter.ToInt64(buffer, 28),
//                BitConverter.ToInt64(buffer, 36),
//                BitConverter.ToInt32(buffer, 44)
//                );

//            var pointCountTree = BitConverter.ToInt64(buffer, 48);
            
//            offset = 56;

//            Guid?[] subcellIds = null;
//            if (subcellmask != 0)
//            {
//                subcellIds = new Guid?[8];
//                if ((subcellmask & 0x01) != 0) subcellIds[0] = ParseGuid(buffer, ref offset);
//                if ((subcellmask & 0x02) != 0) subcellIds[1] = ParseGuid(buffer, ref offset);
//                if ((subcellmask & 0x04) != 0) subcellIds[2] = ParseGuid(buffer, ref offset);
//                if ((subcellmask & 0x08) != 0) subcellIds[3] = ParseGuid(buffer, ref offset);
//                if ((subcellmask & 0x10) != 0) subcellIds[4] = ParseGuid(buffer, ref offset);
//                if ((subcellmask & 0x20) != 0) subcellIds[5] = ParseGuid(buffer, ref offset);
//                if ((subcellmask & 0x40) != 0) subcellIds[6] = ParseGuid(buffer, ref offset);
//                if ((subcellmask & 0x80) != 0) subcellIds[7] = ParseGuid(buffer, ref offset);
//            }

//            var psId = (attributemask & (uint)PointSetAttributes.Positions) != 0 ? ParseGuid(buffer, ref offset) : (Guid?)null;
//            var csId = (attributemask & (uint)PointSetAttributes.Colors) != 0 ? ParseGuid(buffer, ref offset) : (Guid?)null;
//            var nsId = (attributemask & (uint)PointSetAttributes.Normals) != 0 ? ParseGuid(buffer, ref offset) : (Guid?)null;
//            var isId = (attributemask & (uint)PointSetAttributes.Intensities) != 0 ? ParseGuid(buffer, ref offset) : (Guid?)null;
//            var kdId = (attributemask & (uint)PointSetAttributes.KdTree) != 0 ? ParseGuid(buffer, ref offset) : (Guid?)null;
//#pragma warning disable CS0612 // Type or member is obsolete
//            var lodPsId = (attributemask & (uint)PointSetAttributes.LodPositions) != 0 ? ParseGuid(buffer, ref offset) : (Guid?)null;
//            var lodCsId = (attributemask & (uint)PointSetAttributes.LodColors) != 0 ? ParseGuid(buffer, ref offset) : (Guid?)null;
//            var lodNsId = (attributemask & (uint)PointSetAttributes.LodNormals) != 0 ? ParseGuid(buffer, ref offset) : (Guid?)null;
//            var lodIsId = (attributemask & (uint)PointSetAttributes.LodIntensities) != 0 ? ParseGuid(buffer, ref offset) : (Guid?)null;
//            var lodKdId = (attributemask & (uint)PointSetAttributes.LodKdTree) != 0 ? ParseGuid(buffer, ref offset) : (Guid?)null;
//            var ksId = (attributemask & (uint)PointSetAttributes.Classifications) != 0 ? ParseGuid(buffer, ref offset) : (Guid?)null;
//            var lodKsId = (attributemask & (uint)PointSetAttributes.LodClassifications) != 0 ? ParseGuid(buffer, ref offset) : (Guid?)null;
//#pragma warning restore CS0612 // Type or member is obsolete

//            #region backwards compatibility with obsolete lod entries

//            if (lodPsId.HasValue)
//            {
//                //if (psId.HasValue) throw new InvalidOperationException();
//                psId = lodPsId;
//            }
//            if (lodCsId.HasValue)
//            {
//                //if (csId.HasValue) throw new InvalidOperationException();
//                csId = lodCsId;
//            }
//            if (lodNsId.HasValue)
//            {
//                //if (nsId.HasValue) throw new InvalidOperationException();
//                nsId = lodNsId;
//            }
//            if (lodIsId.HasValue)
//            {
//                //if (isId.HasValue) throw new InvalidOperationException();
//                isId = lodIsId;
//            }
//            if (lodKdId.HasValue)
//            {
//                //if (kdId.HasValue) throw new InvalidOperationException();
//                kdId = lodKdId;
//            }
//            if (lodKsId.HasValue)
//            {
//                //if (ksId.HasValue) throw new InvalidOperationException();
//                ksId = lodKsId;
//            }

//            #endregion

//            //Box3f? exactBoundingBoxLocal = null;
//            //float? pointDistanceAverage = null;
//            //float? pointDistanceStandardDeviation = null;
//            //if ((attributemask & (uint)PointSetAttributes.HasCellAttributes) != 0)
//            //{
//            //    var cellAttributeMask = BitConverter.ToUInt32(buffer, offset); offset += sizeof(uint);
//            //    if ((cellAttributeMask & (uint)CellAttributes.BoundingBoxExactLocal) != 0)
//            //    {
//            //        Box3f ebb = default;
//            //        ref var min = ref ebb.Min;
//            //        ref var max = ref ebb.Max;
//            //        min.X = BitConverter.ToSingle(buffer, offset); offset += sizeof(float);
//            //        min.Y = BitConverter.ToSingle(buffer, offset); offset += sizeof(float);
//            //        min.Z = BitConverter.ToSingle(buffer, offset); offset += sizeof(float);
//            //        max.X = BitConverter.ToSingle(buffer, offset); offset += sizeof(float);
//            //        max.Y = BitConverter.ToSingle(buffer, offset); offset += sizeof(float);
//            //        max.Z = BitConverter.ToSingle(buffer, offset); offset += sizeof(float);
//            //        exactBoundingBoxLocal = ebb;
//            //    }

//            //    if ((cellAttributeMask & (uint)CellAttributes.PointDistance) != 0)
//            //    {
//            //        pointDistanceAverage = BitConverter.ToSingle(buffer, offset); offset += sizeof(float);
//            //        pointDistanceStandardDeviation = BitConverter.ToSingle(buffer, offset); offset += sizeof(float);
//            //    }
//            //}

//            //if (!exactBoundingBoxLocal.HasValue)
//            //{
//            //    var bb = cell.BoundingBox;
//            //    var c = bb.Center;
//            //    exactBoundingBoxLocal = new Box3f(new V3f(bb.Min - c), new V3f(bb.Max - c));
//            //}

//            var data = ImmutableDictionary<DurableData, object>.Empty;
//            if((attributemask & (uint)PointSetAttributes.HasCellAttributes) != 0)
//            {
//                var cnt = BitConverter.ToInt32(buffer, offset); offset += 4;
//                for(var i = 0; i < cnt; i++)
//                {
//                    var key = ParseGuid(buffer, ref offset);
//                    var value = Binary.Read(buffer, ref offset);
//                    data = data.Add(key, value);
//                }
//            }

//            return new PointSetNode(
//                id, cell, pointCountTree, data,
//                //psId, csId, kdId, nsId, isId, ksId,
//                subcellIds, storage, false
//                );
//        }

        private static Guid ParseGuid(byte[] buffer, ref int offset)
        {
            var guid = new Guid(
                BitConverter.ToUInt32(buffer, offset),
                BitConverter.ToUInt16(buffer, offset + 4),
                BitConverter.ToUInt16(buffer, offset + 6),
                buffer[offset + 8], buffer[offset + 9], buffer[offset + 10], buffer[offset + 11],
                buffer[offset + 12], buffer[offset + 13], buffer[offset + 14], buffer[offset + 15]
                );
            offset += 16;
            return guid;
        }

        #endregion

        #region Properties (derived/runtime, non-serialized)

        private Dictionary<DurableDataDefinition, object> PersistentRefs = new Dictionary<DurableDataDefinition, object>();

        #region Positions

        /// <summary></summary>
        [JsonIgnore]
        public Guid? PositionsId => Data.TryGetValue(OctreeAttributes.RefPositionsLocal3f, out var id) ? (Guid?)id : null;

        /// <summary>
        /// Point positions relative to cell's center, or null if no positions.
        /// </summary>
        [JsonIgnore]
        public PersistentRef<V3f[]> Positions => PersistentRefs.TryGetValue(OctreeAttributes.RefPositionsLocal3f, out object x) ? (PersistentRef<V3f[]>)x : null;

        /// <summary></summary>
        [JsonIgnore]
        public bool HasPositions => PersistentRefs.ContainsKey(OctreeAttributes.RefPositionsLocal3f);

        /// <summary>
        /// Point positions (absolute), or null if no positions.
        /// </summary>
        [JsonIgnore]
        public V3d[] PositionsAbsolute => Positions?.Value.Map(p => new V3d(Center.X + p.X, Center.Y + p.Y, Center.Z + p.Z));

        #endregion

        #region Colors

        /// <summary></summary>
        [JsonIgnore]
        public Guid? ColorsId => Data.TryGetValue(OctreeAttributes.RefColors3b, out var id) ? (Guid?)id : null;

        /// <summary>
        /// Point colors, or null if no points.
        /// </summary>
        [JsonIgnore]
        public PersistentRef<C4b[]> Colors => PersistentRefs.TryGetValue(OctreeAttributes.RefColors3b, out object x) ? (PersistentRef<C4b[]>)x : null;

        /// <summary></summary>
        [JsonIgnore]
        public bool HasColors => PersistentRefs.ContainsKey(OctreeAttributes.RefColors3b);

        #endregion

        #region Normals

        /// <summary></summary>
        [JsonIgnore]
        public Guid? NormalsId => Data.TryGetValue(OctreeAttributes.RefNormals3f, out var id) ? (Guid?)id : null;

        /// <summary></summary>
        [JsonIgnore]
        public PersistentRef<V3f[]> Normals => PersistentRefs.TryGetValue(OctreeAttributes.RefNormals3f, out object x) ? (PersistentRef<V3f[]>)x : null;

        /// <summary></summary>
        [JsonIgnore]
        public bool HasNormals => PersistentRefs.ContainsKey(OctreeAttributes.RefNormals3f);

        #endregion

        #region Intensities

        /// <summary></summary>
        [JsonIgnore]
        public Guid? IntensitiesId => Data.TryGetValue(OctreeAttributes.RefIntensities1i, out var id) ? (Guid?)id : null;

        /// <summary></summary>
        [JsonIgnore]
        public PersistentRef<int[]> Intensities => PersistentRefs.TryGetValue(OctreeAttributes.RefIntensities1i, out object x) ? (PersistentRef<int[]>)x : null;

        /// <summary></summary>
        [JsonIgnore]
        public bool HasIntensities => PersistentRefs.ContainsKey(OctreeAttributes.RefIntensities1i);

        #endregion

        #region Classifications

        /// <summary></summary>
        [JsonIgnore]
        public Guid? ClassificationsId => Data.TryGetValue(OctreeAttributes.RefClassifications1b, out var id) ? (Guid?)id : null;

        /// <summary></summary>
        [JsonIgnore]
        public PersistentRef<byte[]> Classifications => PersistentRefs.TryGetValue(OctreeAttributes.RefClassifications1b, out object x) ? (PersistentRef<byte[]>) x : null;

        /// <summary></summary>
        [JsonIgnore]
        public bool HasClassifications => PersistentRefs.ContainsKey(OctreeAttributes.RefClassifications1b);

        #endregion

        #region KdTree

        /// <summary></summary>
        [JsonIgnore]
        public Guid? KdTreeId => Data.TryGetValue(OctreeAttributes.RefKdTreeLocal3f, out var id) ? (Guid?)id : null;

        /// <summary></summary>
        [JsonIgnore]
        public PersistentRef<PointRkdTreeD<V3f[], V3f>> KdTree => PersistentRefs.TryGetValue(OctreeAttributes.RefKdTreeLocal3f, out object x) ? (PersistentRef<PointRkdTreeD<V3f[], V3f>>)x : null;

        /// <summary></summary>
        [JsonIgnore]
        public bool HasKdTree => PersistentRefs.ContainsKey(OctreeAttributes.RefKdTreeLocal3f);

        #endregion

        /// <summary>
        /// </summary>
        [JsonIgnore]
        public readonly Storage Storage;

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

        /// <summary>
        /// Bitmask indicating which subnodes exist (0 means leaf, 255 means all 8 subnodes).
        /// </summary>
        [JsonIgnore]
        public byte SubnodeMask => SubnodeIds == null
            ? (byte)0
            : (byte)(
              (SubnodeIds[0].HasValue ? 0b00000001 : 0  ) |
              (SubnodeIds[1].HasValue ? 0b00000010 : 0  ) |
              (SubnodeIds[2].HasValue ? 0b00000100 : 0  ) |
              (SubnodeIds[3].HasValue ? 0b00001000 : 0  ) |
              (SubnodeIds[4].HasValue ? 0b00010000 : 0  ) |
              (SubnodeIds[5].HasValue ? 0b00100000 : 0  ) |
              (SubnodeIds[6].HasValue ? 0b01000000 : 0  ) |
              (SubnodeIds[7].HasValue ? 0b10000000 : 0  ))
            ;

        ///// <summary>
        ///// Bitmask indicating which attributes exist (attribute flags or'ed together).
        ///// </summary>
        //[JsonIgnore]
        //public PointSetAttributes AttributeMask
        //{
        //    get
        //    {
        //        PointSetAttributes mask = 0u;
        //        foreach (var key in Attributes.Keys) mask |= key;
        //        if (CustomAttributes.Count > 0) mask |= PointSetAttributes.HasCellAttributes;
        //        return mask;
        //    }
        //}
        
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

//        /// <summary>
//        /// Makes inner node from leaf node.
//        /// Removes local points and attaches given subnodes.
//        /// </summary>
//        internal PointSetNode ToInnerNode(PointSetNode[] subnodes)
//        {
//            if (subnodes == null) throw new ArgumentNullException(nameof(subnodes));
//            if (IsNotLeaf) throw new InvalidOperationException();
//#if DEBUG
//            for (var i = 0; i < 8; i++)
//            {
//                var sn = subnodes[i]; if (sn == null) continue;
//                if (sn.Cell.Exponent != this.Cell.Exponent - 1)
//                {
//                    throw new InvalidOperationException("Invariant 173c8cef-f87f-4c17-b22e-a3a3abf016db.");
//                }
//            }
//#endif

//            var pointCountTree = subnodes.Sum(x => x?.PointCountTree);
//            return new PointSetNode(Guid.NewGuid(), Cell, pointCountTree.Value,
//                null, null, null, null, null,
//                LodPositionsId, LodColorsId, LodKdTreeId, LodNormalsId, LodIntensitiesId,
//                subnodes.Map(x => x?.Id), Storage, true
//                );
//        }

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
            return new PointSetNode(Guid.NewGuid(), Cell, pointCountTree.Value, Data, subnodes.Map(x => x?.Id), Storage, true);
        }

        /// <summary>
        /// Makes new node with added data. Existing entries are replaced.
        /// </summary>
        internal PointSetNode WithAddedOrReplacedData(ImmutableDictionary<DurableDataDefinition, object> additionalData)
        {
            return new PointSetNode(Guid.NewGuid(),
                Cell, PointCountTree, Data.AddRange(additionalData),
                SubnodeIds, Storage, true
                );
        }



        ///// <summary>
        ///// Returns node with normals and subnodes replaced.
        ///// </summary>
        //public PointSetNode WithNormals(Guid? nsId, Guid?[] subnodeIds)
        //{
        //    return new PointSetNode(Guid.NewGuid(),
        //        Cell, PointCountTree,
        //        CustomAttributes,
        //        PositionsId, ColorsId, KdTreeId, nsId, IntensitiesId, ClassificationsId,
        //        subnodeIds, Storage, true
        //        );
        //}

        ///// <summary>
        ///// Returns node with normals and subnodes replaced.
        ///// </summary>
        //public PointSetNode WithNormals(Guid? nsId, PointSetNode[] subnodes)
        //{
        //    return new PointSetNode(Guid.NewGuid(),
        //        Cell, PointCountTree,  Data,
        //        //PositionsId, ColorsId, KdTreeId, nsId, IntensitiesId, ClassificationsId,
        //        subnodes?.Map(n => (Guid?)n.Id), Storage, true
        //        );
        //}

        ///// <summary>
        ///// Returns node with normals replaced.
        ///// </summary>
        //public PointSetNode WithNormals(Guid? nsId)
        //{
        //    return new PointSetNode(Guid.NewGuid(),
        //        Cell, PointCountTree,  Data,
        //        //PositionsId, ColorsId, KdTreeId, nsId, IntensitiesId, ClassificationsId,
        //        SubnodeIds, Storage, true
        //        );
        //}
        

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

        string IPointCloudNode.Id => Id.ToString();

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
                if (Data.TryGetValue(OctreeAttributes.BoundingBoxExactLocal, out var value) && value is Box3f)
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
                if (Data.TryGetValue(OctreeAttributes.BoundingBoxExactLocal, out var value) && value is Box3f)
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
                if (Data.TryGetValue(OctreeAttributes.AveragePointDistance, out var value) && value is float)
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
                if (Data.TryGetValue(OctreeAttributes.AveragePointDistanceStdDev, out var value) && value is float)
                    return (float)value;
                else
                    return -1.0f;
            }
        }
        
        ///// <summary></summary>
        //public bool TryGetPropertyKey(DurableData property, out string key)
        //{
        //    if (Attributes.TryGetValue(property, out Guid guid))
        //    {
        //        key = guid.ToString();
        //        return true;
        //    }
        //    else
        //    {
        //        key = null;
        //        return false;
        //    }
        //}

        ///// <summary></summary>
        //public bool TryGetPropertyValue(DurableData property, out object value)
        //{
        //    switch (property)
        //    {
        //        case OctreeAttributes.RefClassifications1b: value = Classifications; return value != null;

        //        case PointCloudAttribute.Colors:                value = Colors;             return value != null;
        //        case PointCloudAttribute.Intensities:           value = Intensities;        return value != null;
        //        case PointCloudAttribute.KdTree:                value = KdTree;             return value != null;
        //        case PointCloudAttribute.Normals:               value = Normals;            return value != null;
        //        case PointCloudAttribute.Positions:             value = Positions;          return value != null;

        //        default: throw new InvalidOperationException($"Unknown property '{property}'.");
        //    }
        //}

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
