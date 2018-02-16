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
using System;
using System.IO;
using System.Text;
using System.Threading;
using Aardvark.Base;
using Aardvark.Base.Coder;
using Newtonsoft.Json.Linq;
using Uncodium.SimpleStore;

namespace Aardvark.Geometry.Points
{
    public class Storage : IDisposable
    {
        private bool m_isDisposed = false;

        internal readonly Action<string, object, Func<byte[]>, CancellationToken> f_add;

        internal readonly Func<string, CancellationToken, byte[]> f_get;

        internal readonly Action<string, CancellationToken> f_remove;

        internal readonly Func<string, CancellationToken, object> f_tryGetFromCache;

        internal readonly Action f_flush;

        internal readonly Action f_dispose;
        
        public Storage(
            Action<string, object, Func<byte[]>, CancellationToken> add,
            Func<string, CancellationToken, byte[]> get,
            Action<string, CancellationToken> remove,
            Func<string, CancellationToken, object> tryGetFromCache,
            Action dispose,
            Action flush
            )
        {
            f_add = add;
            f_get = get;
            f_remove = remove;
            f_tryGetFromCache = tryGetFromCache;
            f_dispose = dispose;
            f_flush = flush;
        }
        
        public void Flush() => f_flush();
        
        public void Dispose()
        {
            m_isDisposed = true;
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                f_dispose();
            }
        }
        
        ~Storage()
        {
            Dispose(false);
        }

        public bool IsDisposed => m_isDisposed;
    }

    /// <summary>
    /// </summary>
    public static class StorageExtensions
    {
        public static void Add(this Storage storage, Guid key, byte[] data, CancellationToken ct) => Add(storage, key.ToString(), data, ct);
        
        public static void Add(this Storage storage, string key, byte[] data, CancellationToken ct)
            => storage.f_add(key, data, () => data, ct);
        
        public static byte[] GetByteArray(this Storage storage, string key, CancellationToken ct)
        {
            var data = (byte[])storage.f_tryGetFromCache(key, ct);
            if (data != null) return data;

            var buffer = storage.f_get(key, ct);
            if (buffer == null) return null;
            
            storage.f_add(key, buffer, null, ct);
            return buffer;
        }


        /// <summary></summary>
        public static void Add(this Storage storage, Guid key, V3f[] data, CancellationToken ct) => Add(storage, key.ToString(), data, ct);

        /// <summary></summary>
        public static void Add(this Storage storage, string key, V3f[] data, CancellationToken ct)
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
            }, ct);
        }

        /// <summary></summary>
        public static V3f[] GetV3fArray(this Storage storage, string key, CancellationToken ct)
        {
            var data = (V3f[])storage.f_tryGetFromCache(key, ct);
            if (data != null) return data;

            var buffer = storage.f_get(key, ct);
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
            storage.f_add(key, data, null, ct);
            return data;
        }

        /// <summary></summary>
        public static int[] GetIntArray(this Storage storage, string key, CancellationToken ct)
        {
            var data = (int[])storage.f_tryGetFromCache(key, ct);
            if (data != null) return data;

            var buffer = storage.f_get(key, ct);
            if (buffer == null) return null;
            data = new int[buffer.Length / 4];
            using (var ms = new MemoryStream(buffer))
            using (var br = new BinaryReader(ms))
            {
                for (var i = 0; i < data.Length; i++)
                {
                    data[i] = br.ReadInt32();
                }
            }
            storage.f_add(key, data, null, ct);
            return data;
        }

        /// <summary></summary>
        public static void Add(this Storage storage, Guid key, C4b[] data, CancellationToken ct) => Add(storage, key.ToString(), data, ct);

        /// <summary></summary>
        public static void Add(this Storage storage, string key, C4b[] data, CancellationToken ct)
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
            }, ct);
        }

        /// <summary></summary>
        public static C4b[] GetC4bArray(this Storage storage, string key, CancellationToken ct)
        {
            var data = (C4b[])storage.f_tryGetFromCache(key, ct);
            if (data != null) return data;

            var buffer = storage.f_get(key, ct);
            if (buffer == null) return null;
            data = new C4b[buffer.Length / 4];
            for (int i = 0, j = 0; i < data.Length; i++)
            {
                data[i] = new C4b(buffer[j++], buffer[j++], buffer[j++], buffer[j++]);
            }
            storage.f_add(key, data, null, ct);
            return data;
        }

        /// <summary></summary>
        public static void Add(this Storage storage, Guid key, PointRkdTreeDData data, CancellationToken ct) => Add(storage, key.ToString(), data, ct);

        /// <summary></summary>
        public static void Add(this Storage storage, string key, PointRkdTreeDData data, CancellationToken ct)
        {
            storage.f_add(key, data,() =>
            {
                var ms = new MemoryStream();
                using (var coder = new BinaryWritingCoder(ms))
                {
                    object x = data; coder.Code(ref x);
                }
                return ms.ToArray();
            }, ct);
        }

        /// <summary></summary>
        public static PointRkdTreeDData GetPointRkdTreeDData(this Storage storage, string key, CancellationToken ct)
        {
            var data = storage.f_tryGetFromCache(key, ct);
            if (data != null) return (PointRkdTreeDData)data;

            var buffer = storage.f_get(key, ct);
            if (buffer == null) return null;
            using (var ms = new MemoryStream(buffer))
            using (var coder = new BinaryReadingCoder(ms))
            {
                coder.Code(ref data);
            }
            storage.f_add(key, data, null, ct);
            return (PointRkdTreeDData)data;
        }

        /// <summary></summary>
        public static void Add(this Storage storage, string key, PointSetNode data, CancellationToken ct)
        {
            storage.f_add(key, data, () =>
            {
                //var json = data.ToJson().ToString();
                //var buffer = Encoding.UTF8.GetBytes(json);
                var buffer = data.ToBinary();
                return buffer;
            }, ct);
        }

        /// <summary></summary>
        public static PointSetNode GetPointSetNode(this Storage storage, string key, CancellationToken ct)
        {
            var data = (PointSetNode)storage.f_tryGetFromCache(key, ct);
            if (data != null) return data;

            var buffer = storage.f_get(key, ct);
            if (buffer == null) return null;
            //var json = JObject.Parse(Encoding.UTF8.GetString(buffer));
            //data = PointSetCell.Parse(json, storage);
            data = PointSetNode.ParseBinary(buffer, storage);
            storage.f_add(key, data, null, ct);
            return data;
        }
        
        /// <summary></summary>
        public static void Add(this Storage storage, string key, PointSet data, CancellationToken ct)
        {
            storage.f_add(key, data, () =>
            {
                var json = data.ToJson().ToString();
                var buffer = Encoding.UTF8.GetBytes(json);
                return buffer;
            }, ct);
        }

        /// <summary></summary>
        public static PointSet GetPointSet(this Storage storage, string key, CancellationToken ct)
        {
            var data = (PointSet)storage.f_tryGetFromCache(key, ct);
            if (data != null) return data;

            var buffer = storage.f_get(key, ct);
            if (buffer == null) return null;
            var json = JObject.Parse(Encoding.UTF8.GetString(buffer));
            data = PointSet.Parse(json, storage);
            storage.f_add(key, data, null, ct);
            return data;
        }

        /// <summary></summary>
        public static bool Exists(this Storage storage, string key, CancellationToken ct)
        {
            var data = storage.f_tryGetFromCache(key, ct);
            if (data != null) return true;
            var buffer = storage.f_get(key, ct);
            if (buffer != null) return true;
            return false;
        }

        /// <summary></summary>
        public static Storage ToPointCloudStore(this ISimpleStore x) => new Storage(
            (a, b, c, _) => x.Add(a, b, c), (a, _) => x.Get(a), (a, _) => x.Remove(a),
            (a, _) => x.TryGetFromCache(a), x.Dispose, x.Flush);
    }

}
