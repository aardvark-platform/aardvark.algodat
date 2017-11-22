/*
    Copyright (C) 2017. Aardvark Platform Team. http://github.com/aardvark-platform.
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Aardvark.Base;
using Newtonsoft.Json;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// An immutable point octree node.
    /// </summary>
    public class PointSetNode
    {
        private const uint C_POSITIONS = 1;
        private const uint C_COLORS = 2;
        private const uint C_NORMALS = 4;
        private const uint C_INTENSITIES = 8;
        private const uint C_KDTREE = 16;
        private const uint C_LOD_POSITIONS = 32;
        private const uint C_LOD_COLORS = 64;
        private const uint C_LOD_NORMALS = 128;
        private const uint C_LOD_INTENSITIES = 256;
        private const uint C_LOD_KDTREE = 512;
        private const int C_ATTRIBUTE_COUNT = 10;

        #region Construction

        private PointRkdTreeD<V3f[], V3f> LoadKdTree(string key, CancellationToken ct)
        {
            var data = Storage.GetPointRkdTreeDData(key, ct);
            var ps = Positions.Value;
            return new PointRkdTreeD<V3f[], V3f>(
                3, ps.Length, ps,
                (xs, i) => xs[(int)i], (v, i) => (float) v[i],
                (a, b) => V3f.Distance(a, b), (i, a, b) => b - a,
                (a, b, c) => VecFun.DistanceToLine(a, b, c), VecFun.Lerp, 1e-9,
                data
                );
        }

        private PointSetNode(Guid id,
            Cell cell, long pointCountTree,
            Guid? psId, Guid? csId, Guid? kdId,
            Guid? lodPsId, Guid? lodCsId, Guid? lodKdId,
            Guid?[] subnodeIds, Storage storage, bool writeToStore
            )
        {
            Storage = storage;
            Id = id;
            Cell = cell;
            PointCountTree = pointCountTree;
            SubnodeIds = subnodeIds;

            if (psId.HasValue) Attributes[C_POSITIONS] = psId.Value;
            if (csId.HasValue) Attributes[C_COLORS] = csId.Value;
            if (kdId.HasValue) Attributes[C_KDTREE] = kdId.Value;
            if (lodPsId.HasValue) Attributes[C_LOD_POSITIONS] = lodPsId.Value;
            if (lodCsId.HasValue) Attributes[C_LOD_COLORS] = lodCsId.Value;
            if (lodKdId.HasValue) Attributes[C_LOD_KDTREE] = lodKdId.Value;

            if (IsLeaf && PointCount != PointCountTree) throw new InvalidOperationException();

            if (psId != null) Positions = new PersistentRef<V3f[]>(psId.ToString(), storage.GetV3fArray);
            if (csId != null) Colors = new PersistentRef<C4b[]>(csId.ToString(), storage.GetC4bArray);
            if (kdId != null) KdTree = new PersistentRef<PointRkdTreeD<V3f[], V3f>>(kdId.ToString(), LoadKdTree);
            if (lodPsId != null) LodPositions = new PersistentRef<V3f[]>(lodPsId.ToString(), storage.GetV3fArray);
            if (lodCsId != null) LodColors = new PersistentRef<C4b[]>(lodCsId.ToString(), storage.GetC4bArray);
            if (lodKdId != null) LodKdTree = new PersistentRef<PointRkdTreeD<V3f[], V3f>>(lodKdId.ToString(), LoadKdTree);

            if (subnodeIds != null)
            {
                Subnodes = new PersistentRef<PointSetNode>[8];
                for (var i = 0; i < 8; i++)
                {
                    if (subnodeIds[i] == null) continue;
                    var pRef = new PersistentRef<PointSetNode>(subnodeIds[i].ToString(), storage.GetPointSetNode);
                    Subnodes[i] = pRef;

#if DEBUG && PEDANTIC
                    var subNodeIndex = pRef.Value.Cell;
                    if (Cell.GetOctant(i) != subNodeIndex) throw new InvalidOperationException();
                    if (!Cell.Contains(subNodeIndex)) throw new InvalidOperationException();
                    if (Cell.Exponent != subNodeIndex.Exponent + 1) throw new InvalidOperationException();
                    if (PointCountTree != PointCount + Subnodes.Map(x => x?.Value != null ? x.Value.PointCountTree : 0).Sum()) throw new InvalidOperationException();
#endif
                }
            }

            BoundingBox = Cell.BoundingBox;
            Center = BoundingBox.Center;
            Corners = BoundingBox.ComputeCorners();

            if (writeToStore) storage.Add(Id.ToString(), this, CancellationToken.None);

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
            else
            {
                if (PositionsId != null) throw new InvalidOperationException();
                if (ColorsId != null) throw new InvalidOperationException();
                if (KdTreeId != null) throw new InvalidOperationException();
            }
#endif
        }

        /// <summary>
        /// </summary>
        internal PointSetNode(
            Cell cell, long pointCountTree,
            Guid?[] subnodeIds, Storage storage
            ) : this(Guid.NewGuid(), cell, pointCountTree, null, null, null, null, null, null, subnodeIds, storage, true)
        {
        }

        /// <summary>
        /// Creates leaf node.
        /// </summary>
        internal PointSetNode(
            Cell cell, long pointCountTree,
            Guid? psId, Guid? csId, Guid? kdId,
            Storage storage
            ) : this(Guid.NewGuid(), cell, pointCountTree, psId, csId, kdId, null, null, null, null, storage, true)
        {
        }

        /// <summary>
        /// </summary>
        public static PointSetNode Create(
            Cell cell, V3f[] ps, C4b[] cs, PointSetNode[] subnodes, Storage storage
            )
        {
            if (ps?.Length != cs?.Length) throw new ArgumentException($"ps.Length != cs.Length ({ps?.Length} != {cs?.Length})");
            if (ps?.Length == 0) { ps = null; cs = null; }

#if DEBUG
            if (ps != null)
            {
                var bb = cell.BoundingBox;
                var center = bb.Center;
                for (var i = 0; i < ps.Length; i++)
                {
                    if (!bb.Contains((V3d)ps[i] + center)) throw new Exception($"Point {ps[i]} is outside cell {cell} ({bb}).");
                }
            }
#endif
            Guid? psId = null;
            Guid? csId = null;
            Guid? kdId = null;

            if (ps != null)
            {
                var kd = new PointRkdTreeD<V3f[], V3f>(
                    3, ps.Length, ps,
                    (xs, i) => xs[(int)i], (v, i) => (float)v[i],
                    (a, b) => V3f.Distance(a, b), (i, a, b) => b - a,
                    (a, b, c) => VecFun.DistanceToLine(a, b, c), VecFun.Lerp, 1e-9
                    );

                psId = Guid.NewGuid();
                csId = Guid.NewGuid();
                kdId = Guid.NewGuid();

                storage.Add(psId.ToString(), ps, CancellationToken.None);
                storage.Add(csId.ToString(), cs, CancellationToken.None);
                storage.Add(kdId.ToString(), kd.Data, CancellationToken.None);
            }

            var pointCountTree = ps?.Length + subnodes?.Sum(x => x.PointCountTree);
            var subnodeIds = subnodes?.Map(x => x?.Id);

#if DEBUG
            if (!pointCountTree.HasValue) throw new InvalidOperationException();
#endif

            return new PointSetNode(Guid.NewGuid(), cell, pointCountTree.Value,
                psId, csId, kdId,
                null, null, null,
                subnodeIds, storage, true
                );
        }

        #endregion

        #region Properties (state to serialize)

        /// <summary>
        /// </summary>
        public readonly Dictionary<uint, Guid> Attributes = new Dictionary<uint, Guid>();

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
        
        /// <summary>
        /// </summary>
        public byte[] ToBinary()
        {
            var count =
                1 + 3 +                 // subcellmask (8bit), attribute mask (24bit)
                16 +                    // Guid
                (3 * 8 + 4) +           // Bounds (Cell)
                8 +                     // PointCountTree
                SubnodeCount * 16 +     // subcell keys
                Attributes.Count * 16   // attribute keys
                ;
            var buffer = new byte[count];
            using (var ms = new MemoryStream(buffer))
            using (var bw = new BinaryWriter(ms))
            {
                var attributemask = AttributeMask;
                bw.Write(attributemask | ((uint)SubnodeMask << 24));
                bw.Write(Id.ToByteArray());
                bw.Write(Cell.X); bw.Write(Cell.Y); bw.Write(Cell.Z); bw.Write(Cell.Exponent);
                bw.Write(PointCountTree);
                if (SubnodeIds != null)
                {
                    foreach (var x in SubnodeIds) if (x.HasValue) bw.Write(x.Value.ToByteArray());
                }
                var a = 1u;
                for (var i = 0; i < C_ATTRIBUTE_COUNT; i++, a <<= 1)
                {
                    if ((attributemask & a) == 0) continue;
                    bw.Write(Attributes[a].ToByteArray());
                }
            }
            return buffer;
        }
        
        /// <summary>
        /// </summary>
        public static PointSetNode ParseBinary(byte[] buffer, Storage storage)
        {
            var masks = BitConverter.ToUInt32(buffer, 0);
            var subcellmask = masks >> 24;
            var attributemask = masks & 0b00000000_11111111_11111111_11111111;

            var offset = 4;
            var id = ParseGuid(buffer, ref offset);
            
            var cellIndex = new Cell(
                BitConverter.ToInt64(buffer, 20),
                BitConverter.ToInt64(buffer, 28),
                BitConverter.ToInt64(buffer, 36),
                BitConverter.ToInt32(buffer, 44)
                );

            var pointCountTree = BitConverter.ToInt64(buffer, 48);
            
            offset = 56;

            Guid?[] subcellIds = null;
            if (subcellmask != 0)
            {
                subcellIds = new Guid?[8];
                if ((subcellmask & 0x01) != 0) subcellIds[0] = ParseGuid(buffer, ref offset);
                if ((subcellmask & 0x02) != 0) subcellIds[1] = ParseGuid(buffer, ref offset);
                if ((subcellmask & 0x04) != 0) subcellIds[2] = ParseGuid(buffer, ref offset);
                if ((subcellmask & 0x08) != 0) subcellIds[3] = ParseGuid(buffer, ref offset);
                if ((subcellmask & 0x10) != 0) subcellIds[4] = ParseGuid(buffer, ref offset);
                if ((subcellmask & 0x20) != 0) subcellIds[5] = ParseGuid(buffer, ref offset);
                if ((subcellmask & 0x40) != 0) subcellIds[6] = ParseGuid(buffer, ref offset);
                if ((subcellmask & 0x80) != 0) subcellIds[7] = ParseGuid(buffer, ref offset);
            }

            var psId = (attributemask & C_POSITIONS) != 0 ? ParseGuid(buffer, ref offset) : (Guid?)null;
            var csId = (attributemask & C_COLORS) != 0 ? ParseGuid(buffer, ref offset) : (Guid?)null;
            var nsId = (attributemask & C_NORMALS) != 0 ? ParseGuid(buffer, ref offset) : (Guid?)null;
            var isId = (attributemask & C_INTENSITIES) != 0 ? ParseGuid(buffer, ref offset) : (Guid?)null;
            var kdId = (attributemask & C_KDTREE) != 0 ? ParseGuid(buffer, ref offset) : (Guid?)null;
            var lodPsId = (attributemask & C_LOD_POSITIONS) != 0 ? ParseGuid(buffer, ref offset) : (Guid?)null;
            var lodCsId = (attributemask & C_LOD_COLORS) != 0 ? ParseGuid(buffer, ref offset) : (Guid?)null;
            var lodNsId = (attributemask & C_LOD_NORMALS) != 0 ? ParseGuid(buffer, ref offset) : (Guid?)null;
            var lodIsId = (attributemask & C_LOD_INTENSITIES) != 0 ? ParseGuid(buffer, ref offset) : (Guid?)null;
            var lodKdId = (attributemask & C_LOD_KDTREE) != 0 ? ParseGuid(buffer, ref offset) : (Guid?)null;
            
            return new PointSetNode(
                id, cellIndex, pointCountTree,
                psId, csId, kdId,
                lodPsId, lodCsId, lodKdId,
                subcellIds,
                storage, false
                );
        }

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
        
        #region Positions

        /// <summary></summary>
        [JsonIgnore]
        public Guid? PositionsId => Attributes.TryGetValue(C_POSITIONS, out Guid id) ? (Guid?)id : null;

        /// <summary>
        /// Point positions relative to cell's center, or null if no positions.
        /// </summary>
        [JsonIgnore]
        public readonly PersistentRef<V3f[]> Positions;
        
        /// <summary></summary>
        [JsonIgnore]
        public bool HasPositions => Positions != null;

        /// <summary>
        /// Point positions (absolute), or null if no positions.
        /// </summary>
        [JsonIgnore]
        public V3d[] PositionsAbsolute => Positions?.Value.Map(p => new V3d(Center.X + p.X, Center.Y + p.Y, Center.Z + p.Z));

        #endregion

        #region Colors

        /// <summary></summary>
        [JsonIgnore]
        public Guid? ColorsId => Attributes.TryGetValue(C_COLORS, out Guid id) ? (Guid?)id : null;

        /// <summary>Point colors, or null if no points.
        /// </summary>
        [JsonIgnore]
        public readonly PersistentRef<C4b[]> Colors;

        /// <summary></summary>
        [JsonIgnore]
        public bool HasColors => Colors != null;

        #endregion

        #region Normals

        /// <summary></summary>
        [JsonIgnore]
        public Guid? NormalsId => Attributes.TryGetValue(C_NORMALS, out Guid id) ? (Guid?)id : null;

        /// <summary></summary>
        [JsonIgnore]
        public readonly PersistentRef<C3f[]> Normals;

        /// <summary></summary>
        [JsonIgnore]
        public bool HasNormals => Normals != null;

        #endregion

        #region Intensities

        /// <summary></summary>
        [JsonIgnore]
        public Guid? IntensitiesId => Attributes.TryGetValue(C_INTENSITIES, out Guid id) ? (Guid?)id : null;

        /// <summary></summary>
        [JsonIgnore]
        public readonly PersistentRef<int[]> Intensities;

        /// <summary></summary>
        [JsonIgnore]
        public bool HasIntensities => Intensities != null;

        #endregion

        #region KdTree

        /// <summary></summary>
        [JsonIgnore]
        public Guid? KdTreeId => Attributes.TryGetValue(C_KDTREE, out Guid id) ? (Guid?)id : null;

        /// <summary></summary>
        [JsonIgnore]
        public readonly PersistentRef<PointRkdTreeD<V3f[], V3f>> KdTree;
        
        /// <summary></summary>
        [JsonIgnore]
        public bool HasKdTree => KdTree != null;

        #endregion

        #region LodPositions

        /// <summary></summary>
        [JsonIgnore]
        public Guid? LodPositionsId => Attributes.TryGetValue(C_LOD_POSITIONS, out Guid id) ? (Guid?)id : null;

        /// <summary>
        /// LoD-Positions relative to cell's center, or null if no positions.
        /// </summary>
        [JsonIgnore]
        public readonly PersistentRef<V3f[]> LodPositions;

        /// <summary></summary>
        [JsonIgnore]
        public bool HasLodPositions => LodPositions != null;

        /// <summary>
        /// Lod-Positions (absolute), or null if no positions.
        /// </summary>
        [JsonIgnore]
        public V3d[] LodPositionsAbsolute => LodPositions?.Value.Map(p => new V3d(Center.X + p.X, Center.Y + p.Y, Center.Z + p.Z));

        #endregion

        #region LodColors

        /// <summary></summary>
        [JsonIgnore]
        public Guid? LodColorsId => Attributes.TryGetValue(C_LOD_COLORS, out Guid id) ? (Guid?)id : null;

        /// <summary></summary>
        [JsonIgnore]
        public readonly PersistentRef<C4b[]> LodColors;

        /// <summary></summary>
        [JsonIgnore]
        public bool HasLodColors => LodColors != null;

        #endregion

        #region LodNormals
        
        /// <summary></summary>
        [JsonIgnore]
        public Guid? LodNormalsId => Attributes.TryGetValue(C_LOD_NORMALS, out Guid id) ? (Guid?)id : null;

        /// <summary></summary>
        [JsonIgnore]
        public readonly PersistentRef<V3f[]> LodNormals;

        /// <summary></summary>
        [JsonIgnore]
        public bool HasLodNormals => LodNormals != null;

        #endregion

        #region LodIntensities
        
        /// <summary></summary>
        [JsonIgnore]
        public Guid? LodIntensitiesId => Attributes.TryGetValue(C_LOD_INTENSITIES, out Guid id) ? (Guid?)id : null;

        /// <summary></summary>
        [JsonIgnore]
        public readonly PersistentRef<int[]> LodIntensities;

        /// <summary></summary>
        [JsonIgnore]
        public bool HasLodIntensities => LodIntensities != null;

        #endregion

        #region LodKdTree

        /// <summary></summary>
        [JsonIgnore]
        public Guid? LodKdTreeId => Attributes.TryGetValue(C_LOD_KDTREE, out Guid id) ? (Guid?)id : null;

        /// <summary>
        /// LoD-Point kd-tree, or null if no LoD data.
        /// </summary>
        [JsonIgnore]
        public readonly PersistentRef<PointRkdTreeD<V3f[], V3f>> LodKdTree;

        /// <summary></summary>
        [JsonIgnore]
        public bool HasLodKdTree => LodKdTree != null;

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
        /// Number of lod points in this node (without subnodes).
        /// </summary>
        [JsonIgnore]
        public long LodPointCount => (LodPositions != null) ? LodPositions.Value.Length : 0;

        /// <summary>
        /// Subnodes (8), or null if leaf.
        /// </summary>
        [JsonIgnore]
        public readonly PersistentRef<PointSetNode>[] Subnodes;

        /// <summary>
        /// Bounding box of this node.
        /// </summary>
        [JsonIgnore]
        public readonly Box3d BoundingBox;

        /// <summary>
        /// Center of this node's bounding box.
        /// </summary>
        [JsonIgnore]
        public readonly V3d Center;

        /// <summary>
        /// Corners of this node's bounding box.
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

        /// <summary>
        /// Bitmask indicating which attributes exist (attribute flags or'ed together).
        /// </summary>
        [JsonIgnore]
        public uint AttributeMask
        {
            get
            {
                var mask = 0u;
                foreach (var key in Attributes.Keys) mask |= key;
                return mask;
            }
        }

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
        /// Makes inner node from leaf node.
        /// Removes local points and attaches given subnodes.
        /// </summary>
        internal PointSetNode ToInnerNode(PointSetNode[] subnodes)
        {
            if (subnodes == null) throw new ArgumentNullException(nameof(subnodes));
            if (IsNotLeaf) throw new InvalidOperationException();

            var pointCountTree = subnodes.Sum(x => x?.PointCountTree);
            return new PointSetNode(Guid.NewGuid(), Cell, pointCountTree.Value,
                null, null, null,
                LodPositionsId, LodColorsId, LodKdTreeId,
                subnodes.Map(x => x?.Id), Storage, true
                );
        }

        /// <summary>
        /// Replaces subnodes.
        /// </summary>
        internal PointSetNode WithSubNodes(PointSetNode[] subnodes)
        {
            if (subnodes == null) throw new ArgumentNullException(nameof(subnodes));
            if (IsLeaf) throw new InvalidOperationException();

            var pointCountTree = subnodes.Sum(x => x?.PointCountTree);
            return new PointSetNode(Guid.NewGuid(), Cell, pointCountTree.Value,
                null, null, null, LodPositionsId, LodColorsId, LodKdTreeId,
                subnodes.Map(x => x?.Id), Storage, true
                );
        }

        /// <summary>
        /// Makes node with LoD data from inner node.
        /// </summary>
        internal PointSetNode WithLod(Guid? lodPsId, Guid? lodCsId, Guid? lodKdId, PointSetNode[] subnodes)
        {
            if (IsLeaf) throw new InvalidOperationException();
            if (subnodes == null) throw new InvalidOperationException();
            var pointCountTree = subnodes.Sum(n => n != null ? n.PointCountTree : 0);
            return new PointSetNode(Guid.NewGuid(),
                Cell, pointCountTree,
                null, null, null,
                lodPsId, lodCsId, lodKdId,
                subnodes?.Map(x => x?.Id), Storage, true
                );
        }

        /// <summary>
        /// Makes node with LoD data from leaf node.
        /// </summary>
        internal PointSetNode WithLod()
        {
            if (IsNotLeaf) throw new InvalidOperationException();
            return new PointSetNode(Guid.NewGuid(), Cell, PointCountTree,
                PositionsId, ColorsId, KdTreeId,
                PositionsId, ColorsId, KdTreeId,
                SubnodeIds, Storage, true
                );
        }

        #endregion

        #region Intersections, inside/outside, ...

        /// <summary>
        /// Index of subnode for given point.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetSubIndex(V3d p)
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
        public bool IntersectsPositiveHalfSpace(Plane3d plane)
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
        public bool IntersectsNegativeHalfSpace(Plane3d plane)
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
        public bool InsidePositiveHalfSpace(Plane3d plane)
        {
            BoundingBox.GetMinMaxInDirection(plane.Normal, out V3d min, out V3d max);
            return plane.Height(min) > 0;
        }

        /// <summary>
        /// Returns true if this node is fully inside the negative halfspace defined by given plane.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool InsideNegativeHalfSpace(Plane3d plane)
        {
            BoundingBox.GetMinMaxInDirection(-plane.Normal, out V3d min, out V3d max);
            return plane.Height(min) < 0;
        }

        #endregion
    }
}
