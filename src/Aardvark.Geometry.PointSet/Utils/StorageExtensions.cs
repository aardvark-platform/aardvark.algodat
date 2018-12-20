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
using Aardvark.Data.Points;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
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

    /// <summary>
    /// </summary>
    public static class StorageExtensions
    {
        /// <summary></summary>
        public static ImportConfig WithInMemoryStore(this ImportConfig self)
            => self.WithStorage(new SimpleMemoryStore().ToPointCloudStore(cache: default));
        
        /// <summary>
        /// Wraps Uncodium.ISimpleStore into Storage.
        /// </summary>
        public static Storage ToPointCloudStore(this ISimpleStore x, LruDictionary<string, object> cache) => new Storage(
            x.Add, x.Get, x.Remove, x.Dispose, x.Flush, cache
            );

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
        
        #region byte[]

        /// <summary></summary>
        public static void Add(this Storage storage, Guid key, byte[] data) => Add(storage, key.ToString(), data);

        /// <summary></summary>
        public static void Add(this Storage storage, string key, byte[] data) => storage.f_add(key, data, () => data);

        /// <summary></summary>
        public static byte[] GetByteArray(this Storage storage, string key) => storage.f_get(key);

        /// <summary></summary>
        public static (bool, byte[]) TryGetByteArray(this Storage storage, string key)
        {
            var buffer = storage.f_get(key);
            return (buffer != null, buffer);
        }

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

        /// <summary></summary>
        public static void Add(this Storage storage, Guid key, PointRkdTreeDData data) => Add(storage, key.ToString(), data);

        /// <summary></summary>
        public static void Add(this Storage storage, string key, PointRkdTreeDData data)
            => storage.f_add(key, data, () => Codec.PointRkdTreeDDataToBuffer(data));

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
                (a, b) => V3f.Distance(a, b), (i, a, b) => b - a,
                (a, b, c) => VecFun.DistanceToLine(a, b, c), VecFun.Lerp, 1e-9,
                storage.GetPointRkdTreeDData(key)
                );

        #endregion

        #region PointSetNode

        /// <summary></summary>
        public static void Add(this Storage storage, string key, PointSetNode data)
        {
            storage.f_add(key, data, () =>
            {
                var buffer = data.ToBinary();
                return buffer;
            });
        }

        /// <summary></summary>
        public static PointSetNode GetPointSetNode(this Storage storage, string key)
        {
            if (storage.HasCache && storage.Cache.TryGetValue(key, out object o)) return (PointSetNode)o;

            var buffer = storage.f_get(key);
            if (buffer == null) return default;
            var data = PointSetNode.ParseBinary(buffer, storage);
            
            if (storage.HasCache) storage.Cache.Add(
                key, data, buffer.Length, onRemove: default
                );
            return data;
        }

        /// <summary></summary>
        public static (bool, PointSetNode) TryGetPointSetNode(this Storage storage, string key)
        {
            if (storage.HasCache && storage.Cache.TryGetValue(key, out object o))
            {
                return (true, (PointSetNode)o);
            }
            else
            {
                return (false, default);
            }
        }

        #endregion

        #region PointSet

        /// <summary></summary>
        public static void Add(this Storage storage, string key, PointSet data)
        {
            storage.f_add(key, data, () =>
            {
                var json = data.ToJson().ToString();
                var buffer = Encoding.UTF8.GetBytes(json);
                return buffer;
            });
        }

        /// <summary></summary>
        public static PointSet GetPointSet(this Storage storage, string key, IStoreResolver resolver)
        {
            if (storage.HasCache && storage.Cache.TryGetValue(key, out object o)) return (PointSet)o;

            var buffer = storage.f_get(key);
            if (buffer == null) return default;
            var json = JObject.Parse(Encoding.UTF8.GetString(buffer));
            var data = PointSet.Parse(json, storage, resolver);

            if (storage.HasCache) storage.Cache.Add(
                key, data, buffer.Length, onRemove: default
                );
            return data;
        }

        /// <summary></summary>
        public static (bool, PointSet) TryGetPointSet(this Storage storage, string key)
        {
            if (storage.HasCache && storage.Cache.TryGetValue(key, out object o))
            {
                return (true, (PointSet)o);
            }
            else
            {
                return (false, default);
            }
        }

        #endregion
    }
}
