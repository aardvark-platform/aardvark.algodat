/*
    Copyright (C) 2006-2020. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;
using Uncodium.SimpleStore;

namespace Aardvark.Geometry.Points
{
    /// <summary></summary>
    public static class Codec
    {
        #region Generic

        /// <summary>V3f[] -> byte[]</summary>
        public static byte[] ArrayToBuffer<T>(T[] data, int elementSizeInBytes, Action<BinaryWriter, T> writeElement)
        {
            if (data == null) return null;
            var buffer = new byte[data.Length * elementSizeInBytes];
            using var ms = new MemoryStream(buffer);
            using var bw = new BinaryWriter(ms);
            for (var i = 0; i < data.Length; i++) writeElement(bw, data[i]);
            return buffer;
        }

        /// <summary>IList&lt;V3f&gt; -> byte[]</summary>
        public static byte[] ArrayToBuffer<T>(IList<T> data, int elementSizeInBytes, Action<BinaryWriter, T> writeElement)
        {
            if (data == null) return null;
            var buffer = new byte[data.Count * elementSizeInBytes];
            using var ms = new MemoryStream(buffer);
            using var bw = new BinaryWriter(ms);
            for (var i = 0; i < data.Count; i++) writeElement(bw, data[i]);
            return buffer;
        }

        /// <summary>byte[] -> T[]</summary>
        public static T[] BufferToArray<T>(byte[] buffer, int elementSizeInBytes, Func<BinaryReader, T> readElement)
        {
            if (buffer == null) return null;
            var data = new T[buffer.Length / elementSizeInBytes];
            using var ms = new MemoryStream(buffer);
            using var br = new BinaryReader(ms);
            for (var i = 0; i < data.Length; i++) data[i] = readElement(br);
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

        #region V2f[]

        /// <summary>V2f[] -> byte[]</summary>
        public static byte[] V2fArrayToBuffer(V2f[] data)
            => ArrayToBuffer(data, 8, (bw, x) => { bw.Write(x.X); bw.Write(x.Y); });

        /// <summary>IList&lt;V2f[]&gt; -> byte[]</summary>
        public static byte[] V2fArrayToBuffer(IList<V2f> data)
            => ArrayToBuffer(data, 8, (bw, x) => { bw.Write(x.X); bw.Write(x.Y); });

        /// <summary>byte[] -> V2f[]</summary>
        public static V2f[] BufferToV2fArray(byte[] buffer)
            => BufferToArray(buffer, 8, br => new V2f(br.ReadSingle(), br.ReadSingle()));

        #endregion

        #region V2d[]

        /// <summary>V2d[] -> byte[]</summary>
        public static byte[] V2dArrayToBuffer(V2d[] data)
            => ArrayToBuffer(data, 16, (bw, x) => { bw.Write(x.X); bw.Write(x.Y); });

        /// <summary>IList&lt;V2d[]&gt; -> byte[]</summary>
        public static byte[] V2dArrayToBuffer(IList<V2d> data)
            => ArrayToBuffer(data, 16, (bw, x) => { bw.Write(x.X); bw.Write(x.Y); });

        /// <summary>byte[] -> V2d[]</summary>
        public static V2d[] BufferToV2dArray(byte[] buffer)
            => BufferToArray(buffer, 16, br => new V2d(br.ReadDouble(), br.ReadDouble()));

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
            using var ms = new MemoryStream(buffer);
            
            for (var i = 0; i < data.Length; i++)
            {
                ms.WriteByte(data[i].R); ms.WriteByte(data[i].G); ms.WriteByte(data[i].B); ms.WriteByte(data[i].A);
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
            using var coder = new BinaryWritingCoder(ms);
            object x = data; coder.Code(ref x);
            return ms.ToArray();
        }

        /// <summary>byte[] -> PointRkdTreeDData</summary>
        public static PointRkdTreeDData BufferToPointRkdTreeDData(byte[] buffer)
        {
            if (buffer == null) return null;
            using var ms = new MemoryStream(buffer);
            using var coder = new BinaryReadingCoder(ms);
            object o = null;
            coder.Code(ref o);
            return (PointRkdTreeDData)o;
        }

        #endregion

        #region PointRkdTreeFData

        /// <summary>PointRkdTreeDData -> byte[]</summary>
        public static byte[] PointRkdTreeFDataToBuffer(PointRkdTreeFData data)
        {
            if (data == null) return null;
            var ms = new MemoryStream();
            using var coder = new BinaryWritingCoder(ms);
            object x = data; coder.Code(ref x);
            return ms.ToArray();
        }

        /// <summary>byte[] -> PointRkdTreeDData</summary>
        public static PointRkdTreeFData BufferToPointRkdTreeFData(byte[] buffer)
        {
            if (buffer == null) return null;
            using var ms = new MemoryStream(buffer);
            using var coder = new BinaryReadingCoder(ms);
            object o = null;
            coder.Code(ref o);
            return (PointRkdTreeFData)o;
        }

        #endregion
    }

    /// <summary>
    /// </summary>
    public static class StorageExtensions
    {
        #region Stores

        /// <summary></summary>
        public static ImportConfig WithInMemoryStore(this ImportConfig self)
            => self.WithStorage(new SimpleMemoryStore().ToPointCloudStore(cache: default));

        /// <summary>
        /// Wraps Uncodium.ISimpleStore into Storage.
        /// </summary>
        public static Storage ToPointCloudStore(this ISimpleStore x, LruDictionary<string, object> cache) {
            Action<string, object, Func<byte[]>> add = (name, value, create) => x.Add(name, create());
            return new Storage(add, x.Get, x.GetSlice, x.Remove, x.Dispose, x.Flush, cache);
        }

        /// <summary>
        /// Wraps Uncodium.ISimpleStore into Storage with default 1GB cache.
        /// </summary>
        public static Storage ToPointCloudStore(this ISimpleStore x)
        {
            Action<string, object, Func<byte[]>> add = (name, value, create) => x.Add(name, create());
            return new Storage(add, x.Get, x.GetSlice, x.Remove, x.Dispose, x.Flush, new LruDictionary<string, object>(1024 * 1024 * 1024));
        }

        #endregion

        #region Exists

        /// <summary>
        /// Returns if given key exists in store.
        /// </summary>
        public static bool Exists(this Storage storage, Guid key) => Exists(storage, key.ToString());

        /// <summary>
        /// Returns if given key exists in store.
        /// </summary>
        public static bool Exists(this Storage storage, string key) => storage.f_get(key) != null;

        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Add(this Storage storage, Guid key, Array data)
            => Add(storage, key.ToString(), data);

        public static void Add(this Storage storage, string key, Array data)
        {
            switch (data)
            {
                case null: throw new ArgumentNullException(nameof(data));
                case byte[] xs: storage.f_add(key, xs, () => xs); break;
                case int[]  xs: storage.f_add(key, xs, () => Codec.IntArrayToBuffer(xs)); break;
                case V2f[]  xs: storage.f_add(key, xs, () => Codec.V2fArrayToBuffer(xs)); break;
                case V2d[]  xs: storage.f_add(key, xs, () => Codec.V2dArrayToBuffer(xs)); break;
                case V3f[]  xs: storage.f_add(key, xs, () => Codec.V3fArrayToBuffer(xs)); break;
                case V3d[]  xs: storage.f_add(key, xs, () => Codec.V3dArrayToBuffer(xs)); break;
                case C4b[]  xs: storage.f_add(key, xs, () => Codec.C4bArrayToBuffer(xs)); break;
                default: throw new Exception($"Type {data.GetType()} not supported.");
            }
        }

        #region byte[]

        /// <summary></summary>
        public static void Add(this Storage storage, Guid key, byte[] data) => Add(storage, key.ToString(), data);

        /// <summary></summary>
        public static void Add(this Storage storage, string key, byte[] data) => storage.f_add(key, data, () => data);

        /// <summary></summary>
        public static byte[] GetByteArray(this Storage storage, string key) => storage.f_get(key);

        /// <summary></summary>
        public static byte[] GetByteArray(this Storage storage, Guid key) => storage.f_get(key.ToString());

        /// <summary></summary>
        public static (bool, byte[]) TryGetByteArray(this Storage storage, string key)
        {
            var buffer = storage.f_get(key);
            return (buffer != null, buffer);
        }

        public static byte[] UnGZip(this byte[] gzip)
        {
            using var stream = new GZipStream(new MemoryStream(gzip), CompressionMode.Decompress);

            const int size = 4096;
            byte[] buffer = new byte[size];
            using var ms = new MemoryStream();

            int count = 0;
            do
            {
                count = stream.Read(buffer, 0, size);
                if (count > 0) ms.Write(buffer, 0, count);
            }
            while (count > 0);
            return ms.ToArray();
        }

        #endregion

        #region string/json

        /// <summary></summary>
        public static void Add(this Storage storage, Guid key, string s)
            => Add(storage, key, Encoding.UTF8.GetBytes(s));

        /// <summary></summary>
        public static void Add(this Storage storage, string key, string s)
            => Add(storage, key, Encoding.UTF8.GetBytes(s));

        /// <summary></summary>
        public static void Add(this Storage storage, Guid key, JObject json)
            => Add(storage, key, Encoding.UTF8.GetBytes(json.ToString(Formatting.Indented)));

        /// <summary></summary>
        public static void Add(this Storage storage, string key, JObject json)
            => Add(storage, key, Encoding.UTF8.GetBytes(json.ToString(Formatting.Indented)));

        #endregion

        #region V3f[]

        /// <summary></summary>
        public static void Add(this Storage storage, Guid key, V3f[] data) => Add(storage, key.ToString(), data);
        
        /// <summary></summary>
        public static void Add(this Storage storage, Guid key, IList<V3f> data) => Add(storage, key.ToString(), data);

        /// <summary></summary>
        public static void Add(this Storage storage, string key, V3f[] data)
            => storage.f_add(key, data, () => Codec.V3fArrayToBuffer(data));
        
        /// <summary></summary>
        public static void Add(this Storage storage, string key, IList<V3f> data)
            => storage.f_add(key, data, () => Codec.V3fArrayToBuffer(data));

        /// <summary></summary>
        public static V3f[] GetV3fArray(this Storage storage, string key)
        {
            if (storage.HasCache && storage.Cache.TryGetValue(key, out object o)) return (V3f[])o;
            
            var buffer = storage.f_get(key);
            var data = Codec.BufferToV3fArray(buffer);
            
            if (data != null && storage.HasCache)
                storage.Cache.Add(key, data, buffer.Length, onRemove: default);

            return data;
        }

        /// <summary></summary>
        public static (bool, V3f[]) TryGetV3fArray(this Storage storage, string key)
        {
            if (storage.HasCache && storage.Cache.TryGetValue(key, out object o))
            {
                return (true, (V3f[])o);
            }
            else
            {
                return (false, default);
            }
        }

        #endregion

        #region int[]

        /// <summary></summary>
        public static void Add(this Storage storage, Guid key, int[] data) => Add(storage, key.ToString(), data);

        /// <summary></summary>
        public static void Add(this Storage storage, string key, int[] data)
            => storage.f_add(key, data, () => Codec.IntArrayToBuffer(data));

        /// <summary></summary>
        public static int[] GetIntArray(this Storage storage, string key)
        {
            if (storage.HasCache && storage.Cache.TryGetValue(key, out object o)) return (int[])o;

            var buffer = storage.f_get(key);
            var data = Codec.BufferToIntArray(buffer);

            if (data != null && storage.HasCache)
                storage.Cache.Add(key, data, buffer.Length, onRemove: default);

            return data;
        }

        /// <summary></summary>
        public static (bool, int[]) TryGetIntArray(this Storage storage, string key)
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
        public static void Add(this Storage storage, Guid key, C4b[] data) => Add(storage, key.ToString(), data);

        /// <summary></summary>
        public static void Add(this Storage storage, string key, C4b[] data)
            => storage.f_add(key, data, () => Codec.C4bArrayToBuffer(data));

        /// <summary></summary>
        public static C4b[] GetC4bArray(this Storage storage, string key)
        {
            if (storage.HasCache && storage.Cache.TryGetValue(key, out object o)) return (C4b[])o;

            var buffer = storage.f_get(key);
            var data = Codec.BufferToC4bArray(buffer);

            if (data != null && storage.HasCache)
                storage.Cache.Add(key, data, buffer.Length, onRemove: default);

            return data;
        }

        /// <summary></summary>
        public static (bool, C4b[]) TryGetC4bArray(this Storage storage, string key)
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

        ///// <summary></summary>
        //public static void Add(this Storage storage, Guid key, PointRkdTreeDData data) => Add(storage, key.ToString(), data);

        ///// <summary></summary>
        //public static void Add(this Storage storage, string key, PointRkdTreeDData data)
        //    => storage.f_add(key, data, () => Codec.PointRkdTreeDDataToBuffer(data));

        /// <summary></summary>
        public static PointRkdTreeDData GetPointRkdTreeDData(this Storage storage, string key)
        {
            if (storage.HasCache && storage.Cache.TryGetValue(key, out object o)) return (PointRkdTreeDData)o;
            
            var buffer = storage.f_get(key);
            if (buffer == null) return default;
            var data = Codec.BufferToPointRkdTreeDData(buffer);
            if (storage.HasCache) storage.Cache.Add(key, data, buffer.Length, onRemove: default);
            return data;
        }

        /// <summary></summary>
        public static (bool, PointRkdTreeDData) TryGetPointRkdTreeDData(this Storage storage, string key)
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

        /// <summary>
        /// </summary>
        public static PointRkdTreeD<V3f[], V3f> GetKdTree(this Storage storage, string key, V3f[] positions)
            => new PointRkdTreeD<V3f[], V3f>(
                3, positions.Length, positions,
                (xs, i) => xs[(int)i], (v, i) => (float)v[i],
                (a, b) => Vec.Distance(a, b), (i, a, b) => b - a,
                (a, b, c) => Vec.DistanceToLine(a, b, c), (t,a,b) => Fun.Lerp((float)t,a,b), 1e-9,
                storage.GetPointRkdTreeDData(key)
                );

        #endregion

        #region PointRkdTreeFData

        /// <summary></summary>
        public static void Add(this Storage storage, Guid key, PointRkdTreeFData data) => Add(storage, key.ToString(), data);

        /// <summary></summary>
        public static void Add(this Storage storage, string key, PointRkdTreeFData data)
            => storage.f_add(key, data, () => Codec.PointRkdTreeFDataToBuffer(data));

        /// <summary></summary>
        public static PointRkdTreeFData GetPointRkdTreeFData(this Storage storage, string key)
        {
            if (storage.HasCache && storage.Cache.TryGetValue(key, out object o)) return (PointRkdTreeFData)o;

            var buffer = storage.f_get(key);
            if (buffer == null) return default;
            var data = Codec.BufferToPointRkdTreeFData(buffer);
            if (storage.HasCache) storage.Cache.Add(key, data, buffer.Length, onRemove: default);
            return data;
        }

        /// <summary></summary>
        public static PointRkdTreeFData GetPointRkdTreeFDataFromD(this Storage storage, string key)
        {
            if (storage.HasCache && storage.Cache.TryGetValue(key, out object o)) return (PointRkdTreeFData)o;

            var buffer = storage.f_get(key);
            if (buffer == null) return default;
            var data0 = Codec.BufferToPointRkdTreeDData(buffer);
            var data = new PointRkdTreeFData
            {
                AxisArray = data0.AxisArray,
                PermArray = data0.PermArray,
                RadiusArray = data0.RadiusArray.Map(x => (float)x)
            };
            if (storage.HasCache) storage.Cache.Add(key, data, buffer.Length, onRemove: default);
            return data;
        }

        /// <summary></summary>
        public static (bool, PointRkdTreeFData) TryGetPointRkdTreeFData(this Storage storage, string key)
        {
            if (storage.HasCache && storage.Cache.TryGetValue(key, out object o))
            {
                return (true, (PointRkdTreeFData)o);
            }
            else
            {
                return (false, default);
            }
        }

        /// <summary></summary>
        public static (bool, PointRkdTreeFData) TryGetPointRkdTreeFDataFromD(this Storage storage, string key)
        {
            if (storage.HasCache && storage.Cache.TryGetValue(key, out object o))
            {
                return (true, (PointRkdTreeFData)o);
            }
            else
            {
                return (false, default);
            }
        }

        ///// <summary>
        ///// </summary>
        //public static PointRkdTreeF<V3f[], V3f> GetKdTreeF(this Storage storage, string key, V3f[] positions)
        //    => new PointRkdTreeF<V3f[], V3f>(
        //        3, positions.Length, positions,
        //        (xs, i) => xs[(int)i], (v, i) => (float)v[i],
        //        (a, b) => V3f.Distance(a, b), (i, a, b) => b - a,
        //        (a, b, c) => VecFun.DistanceToLine(a, b, c), VecFun.Lerp, 1e-6f,
        //        storage.GetPointRkdTreeFData(key)
        //        );

        ///// <summary>
        ///// </summary>
        //public static (bool, PointRkdTreeF<V3f[], V3f>) TryGetKdTreeF(this Storage storage, string key, V3f[] positions)
        //{
        //    if (storage.HasCache && storage.Cache.TryGetValue(key, out object o))
        //    {
        //        return (true, (PointRkdTreeF<V3f[], V3f>)o);
        //    }
        //    else
        //    {
        //        return (false, default);
        //    }
        //}

        #endregion

        #region PointSet

        public static byte[] Encode(this PointSet self)
        {
            var json = self.ToJson().ToString();
            var buffer = Encoding.UTF8.GetBytes(json);
            return buffer;
        }

        /// <summary></summary>
        public static void Add(this Storage storage, string key, PointSet data)
        {
            storage.f_add(key, data, () => data.Encode());
        }
        /// <summary></summary>
        public static void Add(this Storage storage, Guid key, PointSet data)
            => Add(storage, key.ToString(), data);

        /// <summary></summary>
        public static PointSet GetPointSet(this Storage storage, string key)
        {
            if (storage.HasCache && storage.Cache.TryGetValue(key, out object o)) return (PointSet)o;

            var buffer = storage.f_get(key);
            if (buffer == null) return default;
            var json = JObject.Parse(Encoding.UTF8.GetString(buffer));
            var data = PointSet.Parse(json, storage);

            if (storage.HasCache) storage.Cache.Add(
                key, data, buffer.Length, onRemove: default
                );
            return data;
        }
        public static PointSet GetPointSet(this Storage storage, Guid key)
            => GetPointSet(storage, key.ToString());

        #endregion

        #region PointSetNode

        /// <summary></summary>
        public static void Add(this Storage storage, string key, PointSetNode data)
        {
            storage.f_add(key, data, () =>
            {
                if (key != data.Id.ToString()) throw new InvalidOperationException("Invariant b5b13ca6-0182-4e00-a7fe-41ccd9362beb.");
                return data.Encode();
            });
        }

        /// <summary></summary>
        public static void Add(this Storage storage, string key, IPointCloudNode data)
        {
            storage.f_add(key, data, () =>
            {
                if (key != data.Id.ToString()) throw new InvalidOperationException("Invariant a1e63dbc-6996-481e-996f-afdac047be0b.");
                return data.Encode();
            });
        }

        /// <summary></summary>
        public static IPointCloudNode GetPointCloudNode(this Storage storage, Guid key)
            => GetPointCloudNode(storage, key.ToString());

        /// <summary></summary>
        public static IPointCloudNode GetPointCloudNode(this Storage storage, string key)
        {
            if (key == null) return null;

            if (storage.HasCache && storage.Cache.TryGetValue(key, out object o))
            {
                if (o == null) return null;
                if (!(o is IPointCloudNode r)) throw new InvalidOperationException("Invariant d1cb769c-36b6-4374-8248-b8c1ca31d495. [GetPointCloudNode] Store key "+key+" is not IPointCloudNode.");
                return r;
            }

            var buffer = storage.f_get(key);
            if (buffer == null) return null;

            try
            {
                return TryDecodeBuffer(buffer);
            }
            catch 
            {
                try
                {
                    return TryDecodeBuffer(UnGzipBuffer(buffer));
                }
                catch //(Exception e)
                {
                    //Report.Warn("Failed to decode PointSetNode. Unknown format.");
                    //Report.Warn($"{e}");
                    return ObsoleteNodeParser.Parse(storage, buffer);
                }
            }

            IPointCloudNode TryDecodeBuffer(byte[] buffer)
            {
                var guid = new Guid(buffer.TakeToArray(16));
                if (guid == Durable.Octree.Node.Id)
                {
                    var data = PointSetNode.Decode(storage, buffer);
                    if (key != data.Id.ToString()) throw new InvalidOperationException("Invariant 32554e4b-1e53-4e30-8b3c-c218c5b63c46.");

                    if (storage.HasCache) storage.Cache.Add(
                        key, data, buffer.Length, onRemove: default
                        );
                    return data;
                }
                else if (guid == FilteredNode.Defs.FilteredNode.Id)
                {
                    var fn = FilteredNode.Decode(storage, buffer);
                    if (key != fn.Id.ToString()) throw new InvalidOperationException("Invariant 14511080-b605-4f6b-ac49-2495899ccdec.");


                    if (storage.HasCache) storage.Cache.Add(
                        key, fn, buffer.Length, onRemove: default // what to add here?
                        );

                    return fn;
                }
                else if (guid == Durable.Octree.MultiNodeIndex.Id)
                {
                    var index = MultiNodeIndex.Decode(buffer);
                    var (offset, size) = index.GetOffsetAndSize(index.RootNodeId);
                    var bufferRootNode = storage.f_getSlice(index.TreeBlobId, offset, size).UnGZip();
                    //var rootNode = DurableCodec.Deserialize(bufferRootNode);
                    var rootNode = PointSetNode.Decode(storage, bufferRootNode);
                    var multiNode = new MultiNode(index, rootNode);
                    return multiNode;
                }
                else
                {
                    // if all fails, may be obsolete node format ...
                    return ObsoleteNodeParser.Parse(storage, buffer);
                }
            }

            byte[] UnGzipBuffer(byte[] buffer)
            {
                using var stream = new GZipStream(new MemoryStream(buffer), CompressionMode.Decompress);
                var size = 4096 * 4;
                var tmp = new byte[size];
                using var memory = new MemoryStream();
                var count = -1;
                while (count != 0)
                {
                    count = stream.Read(tmp, 0, size);
                    if (count > 0) memory.Write(tmp, 0, count);
                }

                var result = memory.ToArray();
                return result;
            }
        }

        /// <summary></summary>
        public static (bool, IPointCloudNode) TryGetPointCloudNode(this Storage storage, string key)
        {
            if (storage.HasCache && storage.Cache.TryGetValue(key, out object o))
            {
                return (true, (PointSetNode)o);
            }
            else
            {
                try
                {
                    return (true, storage.GetPointCloudNode(key));
                }
                catch
                {
                    return (false, default);
                }
            }
        }

        #endregion

        #region Safe octree load

        /// <summary>
        /// Try to load PointSet or PointCloudNode with given key.
        /// Returns octree root node when found.
        /// </summary>
        public static bool TryGetOctree(this Storage storage, string key, out IPointCloudNode root)
        {
            var bs = storage.f_get.Invoke(key);
            if (bs != null)
            {
                try
                {
                    var ps = storage.GetPointSet(key);
                    root = ps.Root.Value;
                    return true;
                }
                catch
                {
                    try
                    {
                        root = storage.GetPointCloudNode(key);
                        return true;
                    }
                    catch (Exception en)
                    {
                        Report.Warn($"Invariant 70012c8d-b994-4ddf-adb6-a5481434561a. {en}.");
                        root = null;
                        return false;
                    }
                }
            }
            else
            {
                Report.Warn($"Key {key} does not exist in store. Invariant af97e19a-63e8-46ad-a752-fe1200828ced.");
                root = null;
                return false;
            }
        }

        #endregion

        #region Durable

        /// <summary>
        /// Binary encodes (and optionally gzips) a durable map with given definition from a sequence of key/value pairs.
        /// Entries are encoded in the same order as given by the data sequence.
        /// </summary>
        public static byte[] DurableEncode(this IEnumerable<KeyValuePair<Durable.Def, object>> data, Durable.Def def, bool gzipped)
        {
            if (data == null) throw new Exception("Invariant c61b7125-b523-4f19-8f51-5e8be5d06dde.");
            if (def == null) throw new Exception("Invariant a126aff5-8352-4e7c-9864-5459bbf0a5d6.");

            using var ms = new MemoryStream();
            using var zs = gzipped ? new GZipStream(ms, CompressionLevel.Optimal) : (Stream)ms;
            using var bw = new BinaryWriter(zs);
            Data.Codec.Serialize(bw, def, data);
            bw.Flush();
            var buffer = ms.ToArray();
            return buffer;
        }

        public static byte[] Add(this Storage storage, string key, Durable.Def def, IEnumerable<KeyValuePair<Durable.Def, object>> data, bool gzipped)
        {
            if (key == null) throw new Exception("Invariant 40e0143a-af01-4e77-b278-abb1a0c182f2.");
            if (def == null) throw new Exception("Invariant a7a6516e-e019-46ea-b7db-69b559a2aad4.");
            if (data == null) throw new Exception("Invariant ec5b1c03-d92c-4b2d-9b5c-a30f935369e5.");

            using var ms = new MemoryStream();
            using var zs = gzipped ? new GZipStream(ms, CompressionLevel.Optimal) : (Stream)ms;
            using var bw = new BinaryWriter(zs);
            Data.Codec.Serialize(bw, def, data);

            bw.Flush();
            var buffer = ms.ToArray();
            storage.Add(key, buffer);

            return buffer;
        }
        public static byte[] Add(this Storage storage, Guid key, Durable.Def def, ImmutableDictionary<Durable.Def, object> data, bool gzipped)
            => Add(storage, key.ToString(), def, data, gzipped);
        public static byte[] Add(this Storage storage, Guid key, Durable.Def def, IEnumerable<KeyValuePair<Durable.Def, object>> data, bool gzipped)
            => Add(storage, key.ToString(), def, data, gzipped);

        public static (Durable.Def, object) DurableDecode(this byte[] buffer)
            => Data.Codec.Deserialize(buffer);
        public static T DurableDecode<T>(this byte[] buffer)
            => (T)Data.Codec.Deserialize(buffer).Item2;

        public static (Durable.Def, object) GetDurable(this Storage storage, string key)
        {
            if (key == null) return (null, null);
            var buffer = storage.f_get(key);
            if (buffer == null) return (null, null);
            return Data.Codec.Deserialize(buffer);
        }
        public static (Durable.Def, object) GetDurable(this Storage storage, Guid key)
            => GetDurable(storage, key.ToString());

        #endregion
    }
}
