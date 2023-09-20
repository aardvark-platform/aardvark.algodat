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
using System.Collections.Immutable;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// Backwards compatibility.
    /// </summary>
    public static class ObsoleteNodeParser
    {
        /// <summary>
        /// Octree. Obsolete per-point positions. Raw V3f[].
        /// </summary>
        public static readonly Durable.Def ObsoletePositions = new(
            new Guid("7cb3be14-062e-47d0-9941-fe135a82c632"),
            "Octree.ObsoletePositions",
            "Octree. Obsolete per-point positions. Raw V3f[].",
            Guid.Empty,
            false
            );

        public static PointSetNode Parse(Storage storage, byte[] buffer)
        {
            return ParseBinary(buffer, storage);
        }

        /// <summary>
        /// </summary>
        [Flags]
        private enum PointSetAttributes : uint
        {
            /// <summary>
            /// V3f[] relative to Center.
            /// </summary>
            Positions = 1 << 0,

            /// <summary>
            /// C4b[].
            /// </summary>
            Colors = 1 << 1,

            /// <summary>
            /// V3f[].
            /// </summary>
            Normals = 1 << 2,

            /// <summary>
            /// int[].
            /// </summary>
            Intensities = 1 << 3,

            /// <summary>
            /// PointRkdTreeD&lt;V3f[], V3f&gt;.
            /// </summary>
            KdTree = 1 << 4,

            /// <summary>
            /// V3f[] relative to Center.
            /// </summary>
            [Obsolete]
            LodPositions = 1 << 5,

            /// <summary>
            /// C4b[].
            /// </summary>
            [Obsolete]
            LodColors = 1 << 6,

            /// <summary>
            /// V3f[].
            /// </summary>
            [Obsolete]
            LodNormals = 1 << 7,

            /// <summary>
            /// int[].
            /// </summary>
            [Obsolete]
            LodIntensities = 1 << 8,

            /// <summary>
            /// PointRkdTreeD&lt;V3f[], V3f&gt;.
            /// </summary>
            [Obsolete]
            LodKdTree = 1 << 9,

            /// <summary>
            /// byte[].
            /// </summary>
            Classifications = 1 << 10,

            /// <summary>
            /// byte[].
            /// </summary>
            [Obsolete]
            LodClassifications = 1 << 11,

            /// <summary>
            /// Cell attributes.
            /// </summary>
            HasCellAttributes = 1 << 23,
        }

        private static PointSetNode ParseBinary(byte[] buffer, Storage storage)
        {
            var masks = BitConverter.ToUInt32(buffer, 0);
            var subcellmask = masks >> 24;
            var attributemask = masks & 0b00000000_11111111_11111111_11111111;

            var offset = 4;
            var id = ParseGuid(buffer, ref offset);

            var cell = new Cell(
                BitConverter.ToInt64(buffer, 20),
                BitConverter.ToInt64(buffer, 28),
                BitConverter.ToInt64(buffer, 36),
                BitConverter.ToInt32(buffer, 44)
                );

            var pointCountTree = BitConverter.ToInt64(buffer, 48);

            offset = 56;

            Guid[]? subcellIds = null;
            if (subcellmask != 0)
            {
                subcellIds = new Guid[8];
                if ((subcellmask & 0x01) != 0) subcellIds[0] = ParseGuid(buffer, ref offset);
                if ((subcellmask & 0x02) != 0) subcellIds[1] = ParseGuid(buffer, ref offset);
                if ((subcellmask & 0x04) != 0) subcellIds[2] = ParseGuid(buffer, ref offset);
                if ((subcellmask & 0x08) != 0) subcellIds[3] = ParseGuid(buffer, ref offset);
                if ((subcellmask & 0x10) != 0) subcellIds[4] = ParseGuid(buffer, ref offset);
                if ((subcellmask & 0x20) != 0) subcellIds[5] = ParseGuid(buffer, ref offset);
                if ((subcellmask & 0x40) != 0) subcellIds[6] = ParseGuid(buffer, ref offset);
                if ((subcellmask & 0x80) != 0) subcellIds[7] = ParseGuid(buffer, ref offset);
            }

            var psId = (attributemask & (uint)PointSetAttributes.Positions) != 0 ? ParseGuid(buffer, ref offset) : (Guid?)null;
            var csId = (attributemask & (uint)PointSetAttributes.Colors) != 0 ? ParseGuid(buffer, ref offset) : (Guid?)null;
            var nsId = (attributemask & (uint)PointSetAttributes.Normals) != 0 ? ParseGuid(buffer, ref offset) : (Guid?)null;
            var isId = (attributemask & (uint)PointSetAttributes.Intensities) != 0 ? ParseGuid(buffer, ref offset) : (Guid?)null;
            var kdId = (attributemask & (uint)PointSetAttributes.KdTree) != 0 ? ParseGuid(buffer, ref offset) : (Guid?)null;
#pragma warning disable CS0612 // Type or member is obsolete
            var lodPsId = (attributemask & (uint)PointSetAttributes.LodPositions) != 0 ? ParseGuid(buffer, ref offset) : (Guid?)null;
            var lodCsId = (attributemask & (uint)PointSetAttributes.LodColors) != 0 ? ParseGuid(buffer, ref offset) : (Guid?)null;
            var lodNsId = (attributemask & (uint)PointSetAttributes.LodNormals) != 0 ? ParseGuid(buffer, ref offset) : (Guid?)null;
            var lodIsId = (attributemask & (uint)PointSetAttributes.LodIntensities) != 0 ? ParseGuid(buffer, ref offset) : (Guid?)null;
            var lodKdId = (attributemask & (uint)PointSetAttributes.LodKdTree) != 0 ? ParseGuid(buffer, ref offset) : (Guid?)null;
            var ksId = (attributemask & (uint)PointSetAttributes.Classifications) != 0 ? ParseGuid(buffer, ref offset) : (Guid?)null;
            var lodKsId = (attributemask & (uint)PointSetAttributes.LodClassifications) != 0 ? ParseGuid(buffer, ref offset) : (Guid?)null;
#pragma warning restore CS0612 // Type or member is obsolete

            #region backwards compatibility with obsolete lod entries

            if (lodPsId.HasValue) psId = lodPsId;
            if (lodCsId.HasValue) csId = lodCsId;
            if (lodNsId.HasValue) nsId = lodNsId;
            if (lodIsId.HasValue) isId = lodIsId;
            if (lodKdId.HasValue) kdId = lodKdId;
            if (lodKsId.HasValue) ksId = lodKsId;

            #endregion

            var data = ImmutableDictionary<Durable.Def, object>.Empty
                .Add(Durable.Octree.NodeId, id)
                .Add(Durable.Octree.Cell, cell)
                .Add(Durable.Octree.PointCountTreeLeafs, pointCountTree)
                ;

            if (psId.HasValue && !storage.Exists(psId.Value)) throw new Exception("Invalid format. Invariant 29215445-2a5e-42bb-a679-970ec8b7479a.");

            if (psId.HasValue) data = data.Add(Durable.Octree.PositionsLocal3fReference, psId.Value);
            if (csId.HasValue) data = data.Add(Durable.Octree.Colors4bReference, csId.Value);
            if (kdId.HasValue) data = data.Add(Durable.Octree.PointRkdTreeDDataReference, kdId.Value);
            if (nsId.HasValue) data = data.Add(Durable.Octree.Normals3fReference, nsId.Value);
            if (isId.HasValue) data = data.Add(Durable.Octree.Intensities1iReference, isId.Value);
            if (ksId.HasValue) data = data.Add(Durable.Octree.Classifications1bReference, ksId.Value);

            if (subcellIds != null)
            {
                data = data.Add(Durable.Octree.SubnodesGuids, subcellIds);
            }

            var result = new PointSetNode(data, storage, false);
            return result;
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
    }
}
