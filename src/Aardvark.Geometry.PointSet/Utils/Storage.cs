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
    /// <summary>
    /// </summary>
    public static class StorageExtensions
    {
        /// <summary></summary>
        public static ImportConfig WithInMemoryStore(this ImportConfig self) => self.WithStorage(new SimpleMemoryStore().ToPointCloudStore());
        
        /// <summary>
        /// Wraps Uncodium.ISimpleStore into Storage.
        /// </summary>
        public static Storage ToPointCloudStore(this ISimpleStore x) => new Storage(
            (id, value, getEncodedValue, _) => x.Add(id, value, getEncodedValue),
            (id, _) => x.Get(id),
            (id, _) => x.Remove(id),
            (id, _) => x.TryGetFromCache(id),
            x.Dispose,
            x.Flush
            );

        #region Exists

        /// <summary>
        /// Returns if given key exists in store.
        /// </summary>
        public static bool Exists(this Storage storage, Guid key, CancellationToken ct) => Exists(storage, key.ToString(), ct);

        /// <summary>
        /// Returns if given key exists in store.
        /// </summary>
        public static bool Exists(this Storage storage, string key, CancellationToken ct)
        {
            var data = storage.f_tryGetFromCache(key, ct);
            if (data != null) return true;
            var buffer = storage.f_get(key, ct);
            if (buffer != null) return true;
            return false;
        }

        #endregion
        
        #region byte[]

        /// <summary></summary>
        public static void Add(this Storage storage, Guid key, byte[] data) => Add(storage, key.ToString(), data);

        /// <summary></summary>
        public static void Add(this Storage storage, string key, byte[] data)
            => storage.f_add(key, data, () => data, default);

        /// <summary></summary>
        public static byte[] GetByteArray(this Storage storage, string key)
        {
            var data = (byte[])storage.f_tryGetFromCache(key, default);
            if (data != null)
            {
                if (Storage.CACHE_ENABLED) Storage.CACHE.Add(data, data.Length);
                return data;
            }

            var buffer = storage.f_get(key, default);
            if (buffer == null) return null;

            storage.f_add(key, buffer, null, default);
            
            if (Storage.CACHE_ENABLED) Storage.CACHE.Add(buffer, buffer.Length);
            return buffer;
        }

        /// <summary></summary>
        public static (bool, byte[]) TryGetByteArray(this Storage storage, string key)
        {
            var data = (byte[])storage.f_tryGetFromCache(key, default);
            if (data != null)
            {
                if (Storage.CACHE_ENABLED) Storage.CACHE.Add(data, data.Length);
                return (true, data);
            }
            else
            {
                return (false, default);
            }
        }

        #endregion

        #region V3f[]

        /// <summary></summary>
        public static void Add(this Storage storage, Guid key, V3f[] data) => Add(storage, key.ToString(), data);
        
        /// <summary></summary>
        public static void Add(this Storage storage, Guid key, IList<V3f> data) => Add(storage, key.ToString(), data);

        /// <summary></summary>
        public static void Add(this Storage storage, string key, V3f[] data)
        {
            storage.f_add(key, data, () =>
            {
                var buffer = new byte[data.Length * 12];
                using (var ms = new MemoryStream(buffer))
                using (var bw = new BinaryWriter(ms))
                {
                    for (var i = 0; i < data.Length; i++)
                    {
                        bw.Write(data[i].X); bw.Write(data[i].Y); bw.Write(data[i].Z);
                    }
                }
                return buffer;
            }, default);
        }

        /// <summary></summary>
        public static void Add(this Storage storage, string key, IList<V3f> data)
        {
            storage.f_add(key, data, () =>
            {
                var buffer = new byte[data.Count * 12];
                using (var ms = new MemoryStream(buffer))
                using (var bw = new BinaryWriter(ms))
                {
                    for (var i = 0; i < data.Count; i++)
                    {
                        bw.Write(data[i].X); bw.Write(data[i].Y); bw.Write(data[i].Z);
                    }
                }
                return buffer;
            }, default);
        }

        /// <summary></summary>
        public static V3f[] GetV3fArray(this Storage storage, string key)
        {
            var data = (V3f[])storage.f_tryGetFromCache(key, default);
            if (data != null)
            {
                if (Storage.CACHE_ENABLED) Storage.CACHE.Add(data, data.Length * 12);
                return data;
            }

            var buffer = storage.f_get(key, default);
            if (buffer == null) return null;
            data = new V3f[buffer.Length / 12];
            using (var ms = new MemoryStream(buffer))
            using (var br = new BinaryReader(ms))
            {
                for (var i = 0; i < data.Length; i++)
                {
                    data[i] = new V3f(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                }
            }
            storage.f_add(key, data, null, default);
            
            if (Storage.CACHE_ENABLED) Storage.CACHE.Add(data, buffer.Length);
            return data;
        }

        /// <summary></summary>
        public static (bool, V3f[]) TryGetV3fArray(this Storage storage, string key)
        {
            var data = (V3f[])storage.f_tryGetFromCache(key, default);
            if (data != null)
            {
                if (Storage.CACHE_ENABLED) Storage.CACHE.Add(data, data.Length * 12);
                return (true, data);
            }
            else
            {
                return (false, default);
            }
        }

        #endregion

        #region int[]

        /// <summary></summary>
        public static void Add(this Storage storage, Guid key, int[] data, CancellationToken ct = default) => Add(storage, key.ToString(), data, ct);

        /// <summary></summary>
        public static void Add(this Storage storage, string key, int[] data, CancellationToken ct = default)
        {
            storage.f_add(key, data, () =>
            {
                var buffer = new byte[data.Length * sizeof(int)];
                using (var ms = new MemoryStream(buffer))
                using (var bw = new BinaryWriter(ms))
                {
                    for (var i = 0; i < data.Length; i++) bw.Write(data[i]);
                }
                return buffer;
            }, ct);
        }

        /// <summary></summary>
        public static int[] GetIntArray(this Storage storage, string key)
        {
            var data = (int[])storage.f_tryGetFromCache(key, default);
            if (data != null)
            {
                if (Storage.CACHE_ENABLED) Storage.CACHE.Add(data, data.Length * 4);
                return data;
            }

            var buffer = storage.f_get(key, default);
            if (buffer == null) return default;
            data = new int[buffer.Length / 4];
            using (var ms = new MemoryStream(buffer))
            using (var br = new BinaryReader(ms))
            {
                for (var i = 0; i < data.Length; i++)
                {
                    data[i] = br.ReadInt32();
                }
            }
            storage.f_add(key, data, null, default);
            
            if (Storage.CACHE_ENABLED) Storage.CACHE.Add(buffer, buffer.Length);
            return data;
        }

        /// <summary></summary>
        public static (bool, int[]) TryGetIntArray(this Storage storage, string key)
        {
            var data = (int[])storage.f_tryGetFromCache(key, default);
            if (data != null)
            {
                if (Storage.CACHE_ENABLED) Storage.CACHE.Add(data, data.Length * 4);
                return (true, data);
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
        {
            storage.f_add(key, data, () =>
            {
                var buffer = new byte[data.Length * 4];
                using (var ms = new MemoryStream(buffer))
                {
                    for (var i = 0; i < data.Length; i++)
                    {
                        ms.WriteByte(data[i].R); ms.WriteByte(data[i].G); ms.WriteByte(data[i].B); ms.WriteByte(data[i].A);
                    }
                }
                return buffer;
            }, default);
        }

        /// <summary></summary>
        public static C4b[] GetC4bArray(this Storage storage, string key)
        {
            var data = (C4b[])storage.f_tryGetFromCache(key, default);
            if (data != null)
            {
                if (Storage.CACHE_ENABLED) Storage.CACHE.Add(data, data.Length * 4);
                return data;
            }

            var buffer = storage.f_get(key, default);
            if (buffer == null) return default;
            data = new C4b[buffer.Length / 4];
            for (int i = 0, j = 0; i < data.Length; i++)
            {
                data[i] = new C4b(buffer[j++], buffer[j++], buffer[j++], buffer[j++]);
            }
            storage.f_add(key, data, null, default);

            if (Storage.CACHE_ENABLED) Storage.CACHE.Add(data, buffer.Length);
            return data;
        }

        /// <summary></summary>
        public static (bool, C4b[]) TryGetC4bArray(this Storage storage, string key)
        {
            var data = (C4b[])storage.f_tryGetFromCache(key, default);
            if (data != null)
            {
                if (Storage.CACHE_ENABLED) Storage.CACHE.Add(data, data.Length * 4);
                return (true, data);
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
        {
            storage.f_add(key, data,() =>
            {
                var ms = new MemoryStream();
                using (var coder = new BinaryWritingCoder(ms))
                {
                    object x = data; coder.Code(ref x);
                }
                return ms.ToArray();
            }, default);
        }

        /// <summary></summary>
        public static PointRkdTreeDData GetPointRkdTreeDData(this Storage storage, string key)
        {
            var data = storage.f_tryGetFromCache(key, default);
            if (data != null)
            {
                var data2 = (PointRkdTreeDData)data;
                if (Storage.CACHE_ENABLED) Storage.CACHE.Add(data2, data2.AxisArray.Length * 4 + data2.PermArray.Length * 8 + data2.RadiusArray.Length * 8);
                return data2;
            }

            var buffer = storage.f_get(key, default);
            if (buffer == null) return default;
            using (var ms = new MemoryStream(buffer))
            using (var coder = new BinaryReadingCoder(ms))
            {
                coder.Code(ref data);
            }
            storage.f_add(key, data, null, default);

            var data3 = (PointRkdTreeDData)data;
            if (Storage.CACHE_ENABLED) Storage.CACHE.Add(data3, data3.AxisArray.Length * 4 + data3.PermArray.Length * 8 + data3.RadiusArray.Length * 8);
            return data3;
        }

        /// <summary></summary>
        public static (bool, PointRkdTreeDData) TryGetPointRkdTreeDData(this Storage storage, string key)
        {
            var data = storage.f_tryGetFromCache(key, default);
            if (data != null)
            {
                var data2 = (PointRkdTreeDData)data;
                if (Storage.CACHE_ENABLED) Storage.CACHE.Add(data2, data2.AxisArray.Length * 4 + data2.PermArray.Length * 8 + data2.RadiusArray.Length * 8);
                return (true, data2);
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
            }, default);
        }

        /// <summary></summary>
        public static PointSetNode GetPointSetNode(this Storage storage, string key)
        {
            var data = (PointSetNode)storage.f_tryGetFromCache(key, default);
            if (data != null)
            {
                if (Storage.CACHE_ENABLED) Storage.CACHE.Add(data, data.SerializedSizeInBytes * 2);
                return data;
            }

            var buffer = storage.f_get(key, default);
            if (buffer == null) return null;
            data = PointSetNode.ParseBinary(buffer, storage);
            storage.f_add(key, data, null, default);
            
            if (Storage.CACHE_ENABLED) Storage.CACHE.Add(data, buffer.Length * 2);
            return data;
        }

        /// <summary></summary>
        public static (bool, PointSetNode) TryGetPointSetNode(this Storage storage, string key)
        {
            var data = (PointSetNode)storage.f_tryGetFromCache(key, default);
            if (data != null)
            {
                if (Storage.CACHE_ENABLED) Storage.CACHE.Add(data, data.SerializedSizeInBytes * 2);
                return (true, data);
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
            }, default);
        }

        /// <summary></summary>
        public static PointSet GetPointSet(this Storage storage, string key)
        {
            var data = (PointSet)storage.f_tryGetFromCache(key, default);
            if (data != null) return data;

            var buffer = storage.f_get(key, default);
            if (buffer == null) return null;
            var json = JObject.Parse(Encoding.UTF8.GetString(buffer));
            data = PointSet.Parse(json, storage);
            storage.f_add(key, data, null, default);
            return data;
        }

        /// <summary></summary>
        public static (bool, PointSet) TryGetPointSet(this Storage storage, string key)
        {
            var data = (PointSet)storage.f_tryGetFromCache(key, default);
            return (data != null, data);
        }

        #endregion
    }
}
