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
using Aardvark.Base.Coder;
using Aardvark.Data;
using Aardvark.Data.Points;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Runtime.InteropServices;
using Uncodium.SimpleStore;

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
        public static readonly Durable.Def ObsoletePositions = new Durable.Def(
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

            Guid[] subcellIds = null;
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

        /// <summary>
        /// </summary>
        private static unsafe void Write<T>(byte[] dst, ref int index, params T[] value) where T : struct
        {
            var gc = GCHandle.Alloc(value, GCHandleType.Pinned);
            var size = value.Length * Marshal.SizeOf<T>();
            try { Marshal.Copy(gc.AddrOfPinnedObject(), dst, index, size); index += size; }
            finally { gc.Free(); }
        }
        /// <summary>
        /// </summary>
        private static unsafe T Read<T>(byte[] src, ref int index) where T : struct
        {
            var value = new T[1];
            var gc = GCHandle.Alloc(value, GCHandleType.Pinned);
            var size = Marshal.SizeOf<T>();
            try { Marshal.Copy(src, index, gc.AddrOfPinnedObject(), size); index += size; return value[0]; }
            finally { gc.Free(); }
        }

        /// <summary>
        /// </summary>
        private static int SizeOf(object value)
        {
            if (value == null) return 1;
            else if (value is bool) return 2;
            else if (value is IntPtr || value is UIntPtr) return 9;
            else return 1 + Marshal.SizeOf(value);
        }

        /// <summary>
        /// </summary>
        private static void Write(object value, byte[] dst, ref int index)
        {
            if (value == null) { dst[index++] = 0; }

            else if (value is byte) { dst[index++] = 1; dst[index++] = (byte)value; }
            else if (value is sbyte) { dst[index++] = 2; dst[index++] = (byte)(sbyte)value; }
            else if (value is ushort) { dst[index++] = 3; Write(dst, ref index, (ushort)value); }
            else if (value is short) { dst[index++] = 4; Write(dst, ref index, (short)value); }
            else if (value is uint) { dst[index++] = 5; Write(dst, ref index, (uint)value); }
            else if (value is int) { dst[index++] = 6; Write(dst, ref index, (int)value); }
            else if (value is ulong) { dst[index++] = 7; Write(dst, ref index, (ulong)value); }
            else if (value is long) { dst[index++] = 8; Write(dst, ref index, (long)value); }

            else if (value is float) { dst[index++] = 9; Write(dst, ref index, (float)value); }
            else if (value is double) { dst[index++] = 10; Write(dst, ref index, (double)value); }
            else if (value is decimal) { dst[index++] = 11; Write(dst, ref index, (decimal)value); }

            else if (value is char) { dst[index++] = 12; Write(dst, ref index, (char)value); }
            else if (value is bool) { dst[index++] = 13; Write(dst, ref index, (byte)((bool)value ? 1 : 0)); }

            // ids broken
            else if (value is V2i) { dst[index++] = 14; Write(dst, ref index, (V2i)value); }
            else if (value is V3i) { dst[index++] = 15; Write(dst, ref index, (V3i)value); }
            else if (value is V4i) { dst[index++] = 16; Write(dst, ref index, (V4i)value); }
            else if (value is V2l) { dst[index++] = 17; Write(dst, ref index, (V2l)value); }
            else if (value is V3l) { dst[index++] = 18; Write(dst, ref index, (V3l)value); }
            else if (value is V4l) { dst[index++] = 19; Write(dst, ref index, (V4l)value); }
            else if (value is V2f) { dst[index++] = 20; Write(dst, ref index, (V2f)value); }
            else if (value is V3f) { dst[index++] = 21; Write(dst, ref index, (V3f)value); }
            else if (value is V4f) { dst[index++] = 22; Write(dst, ref index, (V4f)value); }
            else if (value is V2d) { dst[index++] = 23; Write(dst, ref index, (V2d)value); }
            else if (value is V3d) { dst[index++] = 24; Write(dst, ref index, (V3d)value); }
            else if (value is V4d) { dst[index++] = 25; Write(dst, ref index, (V4d)value); }

            else if (value is C3b) { dst[index++] = 26; Write(dst, ref index, (C3b)value); }
            else if (value is C4b) { dst[index++] = 27; Write(dst, ref index, (C4b)value); }
            else if (value is C3us) { dst[index++] = 28; Write(dst, ref index, (C3us)value); }
            else if (value is C4us) { dst[index++] = 29; Write(dst, ref index, (C4us)value); }
            else if (value is C3ui) { dst[index++] = 30; Write(dst, ref index, (C3ui)value); }
            else if (value is C4ui) { dst[index++] = 31; Write(dst, ref index, (C4ui)value); }
            else if (value is C3f) { dst[index++] = 32; Write(dst, ref index, (C3f)value); }
            else if (value is C4f) { dst[index++] = 33; Write(dst, ref index, (C4f)value); }
            else if (value is C3d) { dst[index++] = 34; Write(dst, ref index, (C3d)value); }
            else if (value is C4d) { dst[index++] = 35; Write(dst, ref index, (C4d)value); }

            else if (value is Range1i) { dst[index++] = 36; Write(dst, ref index, (Range1i)value); }
            else if (value is Range1l) { dst[index++] = 37; Write(dst, ref index, (Range1l)value); }
            else if (value is Range1f) { dst[index++] = 38; Write(dst, ref index, (Range1f)value); }
            else if (value is Range1d) { dst[index++] = 39; Write(dst, ref index, (Range1d)value); }
            else if (value is Box2i) { dst[index++] = 40; Write(dst, ref index, (Box2i)value); }
            else if (value is Box2l) { dst[index++] = 41; Write(dst, ref index, (Box2l)value); }
            else if (value is Box2f) { dst[index++] = 42; Write(dst, ref index, (Box2f)value); }
            else if (value is Box2d) { dst[index++] = 43; Write(dst, ref index, (Box2d)value); }
            else if (value is Box3i) { dst[index++] = 44; Write(dst, ref index, (Box3i)value); }
            else if (value is Box3l) { dst[index++] = 45; Write(dst, ref index, (Box3l)value); }
            else if (value is Box3f) { dst[index++] = 46; Write(dst, ref index, (Box3f)value); }
            else if (value is Box3d) { dst[index++] = 47; Write(dst, ref index, (Box3d)value); }

            else if (value is UIntPtr) { dst[index++] = 48; Write(dst, ref index, (ulong)(UIntPtr)value); }
            else if (value is IntPtr) { dst[index++] = 49; Write(dst, ref index, (long)(IntPtr)value); }

            else if (value is Guid) { dst[index++] = 50; Write(dst, ref index, (Guid)value); }

            else throw new NotImplementedException();
        }

        /// <summary>
        /// </summary>
        private static object Read(byte[] src, ref int index)
        {
            var typeId = src[index++];
            switch (typeId)
            {
                case 0: return null;
                case 1: return Read<byte>(src, ref index);
                case 2: return Read<sbyte>(src, ref index);
                case 3: return Read<ushort>(src, ref index);
                case 4: return Read<short>(src, ref index);
                case 5: return Read<uint>(src, ref index);
                case 6: return Read<int>(src, ref index);
                case 7: return Read<ulong>(src, ref index);
                case 8: return Read<long>(src, ref index);

                case 9: return Read<float>(src, ref index);
                case 10: return Read<double>(src, ref index);
                case 11: return Read<decimal>(src, ref index);

                case 12: return Read<char>(src, ref index);
                case 13: return Read<byte>(src, ref index) != 0;

                // ids broken
                case 14: return Read<V2i>(src, ref index);
                case 15: return Read<V3i>(src, ref index);
                case 16: return Read<V4i>(src, ref index);
                case 17: return Read<V2l>(src, ref index);
                case 18: return Read<V3l>(src, ref index);
                case 19: return Read<V4l>(src, ref index);
                case 20: return Read<V2f>(src, ref index);
                case 21: return Read<V3f>(src, ref index);
                case 22: return Read<V4f>(src, ref index);
                case 23: return Read<V2d>(src, ref index);
                case 24: return Read<V3d>(src, ref index);
                case 25: return Read<V4d>(src, ref index);

                case 26: return Read<C3b>(src, ref index);
                case 27: return Read<C4b>(src, ref index);
                case 28: return Read<C3us>(src, ref index);
                case 29: return Read<C4us>(src, ref index);
                case 30: return Read<C3ui>(src, ref index);
                case 31: return Read<C4ui>(src, ref index);
                case 32: return Read<C3f>(src, ref index);
                case 33: return Read<C4f>(src, ref index);
                case 34: return Read<C3d>(src, ref index);
                case 35: return Read<C4d>(src, ref index);

                case 36: return Read<Range1i>(src, ref index);
                case 37: return Read<Range1l>(src, ref index);
                case 38: return Read<Range1f>(src, ref index);
                case 39: return Read<Range1d>(src, ref index);
                case 40: return Read<Box2i>(src, ref index);
                case 41: return Read<Box2l>(src, ref index);
                case 42: return Read<Box2f>(src, ref index);
                case 43: return Read<Box2d>(src, ref index);
                case 44: return Read<Box3i>(src, ref index);
                case 45: return Read<Box3l>(src, ref index);
                case 46: return Read<Box3f>(src, ref index);
                case 47: return Read<Box3d>(src, ref index);

                case 48: return (UIntPtr)Read<ulong>(src, ref index);
                case 49: return (IntPtr)Read<long>(src, ref index);
                case 50: return Read<Guid>(src, ref index);

                default: throw new NotImplementedException();
            }
        }

        /// <summary></summary>
        private static class Codec
        {
            #region Generic

            /// <summary>V3f[] -> byte[]</summary>
            public static byte[] ArrayToBuffer<T>(T[] data, int elementSizeInBytes, Action<BinaryWriter, T> writeElement)
            {
                if (data == null) return null;
                var buffer = new byte[data.Length * elementSizeInBytes];
                using (var ms = new MemoryStream(buffer))
                using (var bw = new BinaryWriter(ms))
                {
                    for (var i = 0; i < data.Length; i++) writeElement(bw, data[i]);
                }
                return buffer;
            }

            /// <summary>IList&lt;V3f&gt; -> byte[]</summary>
            public static byte[] ArrayToBuffer<T>(IList<T> data, int elementSizeInBytes, Action<BinaryWriter, T> writeElement)
            {
                if (data == null) return null;
                var buffer = new byte[data.Count * elementSizeInBytes];
                using (var ms = new MemoryStream(buffer))
                using (var bw = new BinaryWriter(ms))
                {
                    for (var i = 0; i < data.Count; i++) writeElement(bw, data[i]);
                }
                return buffer;
            }

            /// <summary>byte[] -> T[]</summary>
            public static T[] BufferToArray<T>(byte[] buffer, int elementSizeInBytes, Func<BinaryReader, T> readElement)
            {
                if (buffer == null) return null;
                var data = new T[buffer.Length / elementSizeInBytes];
                using (var ms = new MemoryStream(buffer))
                using (var br = new BinaryReader(ms))
                {
                    for (var i = 0; i < data.Length; i++) data[i] = readElement(br);
                }
                return data;
            }

            #endregion

            #region int[]

            /// <summary>int[] -> byte[]</summary>
            public static byte[] IntArrayToBuffer(int[] data)
                => ArrayToBuffer(data, sizeof(int), (bw, x) => bw.Write(x));

            /// <summary>byte[] -> int[]</summary>
            public static int[] BufferToIntArray(byte[] buffer)
                => BufferToArray(buffer, sizeof(int), br => br.ReadInt32());

            #endregion

            #region V3f[]

            /// <summary>V3f[] -> byte[]</summary>
            public static byte[] V3fArrayToBuffer(V3f[] data)
                => ArrayToBuffer(data, 12, (bw, x) => { bw.Write(x.X); bw.Write(x.Y); bw.Write(x.Z); });

            /// <summary>IList&lt;V3f[]&gt; -> byte[]</summary>
            public static byte[] V3fArrayToBuffer(IList<V3f> data)
                => ArrayToBuffer(data, 12, (bw, x) => { bw.Write(x.X); bw.Write(x.Y); bw.Write(x.Z); });

            /// <summary>byte[] -> V3f[]</summary>
            public static V3f[] BufferToV3fArray(byte[] buffer)
                => BufferToArray(buffer, 12, br => new V3f(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()));

            #endregion

            #region V3d[]

            /// <summary>V3d[] -> byte[]</summary>
            public static byte[] V3dArrayToBuffer(V3d[] data)
                => ArrayToBuffer(data, 24, (bw, x) => { bw.Write(x.X); bw.Write(x.Y); bw.Write(x.Z); });

            /// <summary>byte[] -> V3d[]</summary>
            public static V3d[] BufferToV3dArray(byte[] buffer)
                => BufferToArray(buffer, 24, br => new V3d(br.ReadDouble(), br.ReadDouble(), br.ReadDouble()));

            #endregion

            #region C4b[]

            /// <summary>C4b[] -> byte[]</summary>
            public static byte[] C4bArrayToBuffer(C4b[] data)
            {
                if (data == null) return null;
                var buffer = new byte[data.Length * 4];
                using (var ms = new MemoryStream(buffer))
                {
                    for (var i = 0; i < data.Length; i++)
                    {
                        ms.WriteByte(data[i].R); ms.WriteByte(data[i].G); ms.WriteByte(data[i].B); ms.WriteByte(data[i].A);
                    }
                }
                return buffer;
            }

            /// <summary>byte[] -> C4b[]</summary>
            public static C4b[] BufferToC4bArray(byte[] buffer)
            {
                if (buffer == null) return null;
                var data = new C4b[buffer.Length / 4];
                for (int i = 0, j = 0; i < data.Length; i++)
                {
                    data[i] = new C4b(buffer[j++], buffer[j++], buffer[j++], buffer[j++]);
                }
                return data;
            }

            #endregion

            #region PointRkdTreeDData

            /// <summary>PointRkdTreeDData -> byte[]</summary>
            public static byte[] PointRkdTreeDDataToBuffer(PointRkdTreeDData data)
            {
                if (data == null) return null;
                var ms = new MemoryStream();
                using (var coder = new BinaryWritingCoder(ms))
                {
                    object x = data; coder.Code(ref x);
                }
                return ms.ToArray();
            }

            /// <summary>byte[] -> PointRkdTreeDData</summary>
            public static PointRkdTreeDData BufferToPointRkdTreeDData(byte[] buffer)
            {
                if (buffer == null) return null;
                using (var ms = new MemoryStream(buffer))
                using (var coder = new BinaryReadingCoder(ms))
                {
                    object o = null;
                    coder.Code(ref o);
                    return (PointRkdTreeDData)o;
                }
            }

            #endregion
        }

        ///// <summary></summary>
        //public static ImportConfig WithInMemoryStore(this ImportConfig self)
        //    => self.WithStorage(new SimpleMemoryStore().ToPointCloudStore(cache: default));

        #region byte[]

        /// <summary></summary>
        private static byte[] GetByteArray(this Storage storage, string key) => storage.f_get(key);

        /// <summary></summary>
        private static (bool, byte[]) TryGetByteArray(this Storage storage, string key)
        {
            var buffer = storage.f_get(key);
            return (buffer != null, buffer);
        }

        #endregion

        #region V3f[]

        ///// <summary></summary>
        //private static V3f[] GetV3fArray(this Storage storage, string key)
        //{
        //    if (storage.HasCache && storage.Cache.TryGetValue(key, out object o)) return (V3f[])o;

        //    var buffer = storage.f_get(key);
        //    var data = Codec.BufferToV3fArray(buffer);

        //    if (data != null && storage.HasCache)
        //        storage.Cache.Add(key, data, buffer.Length, onRemove: default);

        //    return data;
        //}

        ///// <summary></summary>
        //private static (bool, V3f[]) TryGetV3fArray(this Storage storage, string key)
        //{
        //    if (storage.HasCache && storage.Cache.TryGetValue(key, out object o))
        //    {
        //        return (true, (V3f[])o);
        //    }
        //    else
        //    {
        //        return (false, default);
        //    }
        //}

        #endregion

        #region int[]

        /// <summary></summary>
        private static int[] GetIntArray(this Storage storage, string key)
        {
            if (storage.HasCache && storage.Cache.TryGetValue(key, out object o)) return (int[])o;

            var buffer = storage.f_get(key);
            var data = Codec.BufferToIntArray(buffer);

            if (data != null && storage.HasCache)
                storage.Cache.Add(key, data, buffer.Length, onRemove: default);

            return data;
        }

        /// <summary></summary>
        private static (bool, int[]) TryGetIntArray(this Storage storage, string key)
        {
            if (storage.HasCache && storage.Cache.TryGetValue(key, out object o))
            {
                return (true, (int[])o);
            }
            else
            {
                return (false, default);
            }
        }

        #endregion

        #region C4b[]

        /// <summary></summary>
        private static C4b[] GetC4bArray(this Storage storage, string key)
        {
            if (storage.HasCache && storage.Cache.TryGetValue(key, out object o)) return (C4b[])o;

            var buffer = storage.f_get(key);
            var data = Codec.BufferToC4bArray(buffer);

            if (data != null && storage.HasCache)
                storage.Cache.Add(key, data, buffer.Length, onRemove: default);

            return data;
        }

        /// <summary></summary>
        private static (bool, C4b[]) TryGetC4bArray(this Storage storage, string key)
        {
            if (storage.HasCache && storage.Cache.TryGetValue(key, out object o))
            {
                return (true, (C4b[])o);
            }
            else
            {
                return (false, default);
            }
        }

        #endregion

        #region PointRkdTreeDData

        /// <summary></summary>
        private static PointRkdTreeDData GetPointRkdTreeDData(this Storage storage, string key)
        {
            if (storage.HasCache && storage.Cache.TryGetValue(key, out object o)) return (PointRkdTreeDData)o;

            var buffer = storage.f_get(key);
            if (buffer == null) return default;
            var data = Codec.BufferToPointRkdTreeDData(buffer);
            if (storage.HasCache) storage.Cache.Add(key, data, buffer.Length, onRemove: default);
            return data;
        }

        /// <summary></summary>
        private static (bool, PointRkdTreeDData) TryGetPointRkdTreeDData(this Storage storage, string key)
        {
            if (storage.HasCache && storage.Cache.TryGetValue(key, out object o))
            {
                return (true, (PointRkdTreeDData)o);
            }
            else
            {
                return (false, default);
            }
        }

        /// <summary></summary>
        private static PointRkdTreeD<V3f[], V3f> GetKdTree(this Storage storage, string key, V3f[] positions)
            => new PointRkdTreeD<V3f[], V3f>(
                3, positions.Length, positions,
                (xs, i) => xs[(int)i], (v, i) => (float)v[i],
                (a, b) => V3f.Distance(a, b), (i, a, b) => b - a,
                (a, b, c) => VecFun.DistanceToLine(a, b, c), VecFun.Lerp, 1e-9,
                GetPointRkdTreeDData(storage, key)
                );

        #endregion
    }
}
