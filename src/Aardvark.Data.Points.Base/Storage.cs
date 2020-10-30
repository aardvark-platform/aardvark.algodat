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
using System;
using System.Collections.Generic;

namespace Aardvark.Data.Points
{
    /// <summary>
    /// </summary>
    public class Storage : IDisposable
    {
        private bool m_isDisposed = false;

        /// <summary>add(key, value, create)</summary>
        public readonly Action<string, object, Func<byte[]>> f_add;

        /// <summary></summary>
        public readonly Func<string, byte[]> f_get;

        /// <summary></summary>
        public readonly Func<string, long, int, byte[]> f_getSlice;

        /// <summary></summary>
        public readonly Action<string> f_remove;
        
        /// <summary></summary>
        public readonly Action f_flush;

        /// <summary></summary>
        public readonly Action f_dispose;

        /// <summary></summary>
        public readonly LruDictionary<string, object> Cache;

        /// <summary></summary>
        public Storage(
            Action<string, object, Func<byte[]>> add,
            Func<string, byte[]> get,
            Func<string, long, int, byte[]> getSlice,
            Action<string> remove,
            Action dispose,
            Action flush,
            LruDictionary<string, object> cache
            )
        {
            f_add = add;
            f_get = get;
            f_getSlice = getSlice;
            f_remove = remove;
            f_dispose = dispose;
            f_flush = flush;
            Cache = cache;

            Register(this);
        }

        /// <summary></summary>
        public bool HasCache => Cache != null;

        /// <summary>
        /// Writes all pending changes to store.
        /// </summary>
        public void Flush() => f_flush();

        /// <summary></summary>
        public void Dispose()
        {
            m_isDisposed = true;
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary></summary>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                f_dispose();
                Unregister(this);
            }
        }

        /// <summary></summary>
        ~Storage()
        {
            Dispose(false);
        }

        /// <summary></summary>
        public bool IsDisposed => m_isDisposed;
        
        private static void Register(Storage storage)
        {
            lock (s_storages)
            {
                if (s_storages.Contains(storage)) throw new InvalidOperationException();

                //if (s_storages.Count == 0)
                //{
                //    Report.Line("[Storage] created storage cache");
                //}

                s_storages.Add(storage);
                //Report.Line($"[Storage] registered store (total {s_storages.Count})");
            }
        }

        private static void Unregister(Storage storage)
        {
            lock (s_storages)
            {
                if (!s_storages.Contains(storage)) throw new InvalidOperationException();

                s_storages.Remove(storage);
                //Report.Line($"[Storage] unregistered store ({s_storages.Count} left)");

                //if (s_storages.Count == 0)
                //{
                //    Report.Line("[Storage] disposed storage cache");
                //}
            }
        }

        private static readonly HashSet<Storage> s_storages = new HashSet<Storage>();

    }
}