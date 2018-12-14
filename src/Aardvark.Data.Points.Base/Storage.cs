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
using System;
using System.Collections.Generic;
using System.Threading;

namespace Aardvark.Data.Points
{
    /// <summary>
    /// </summary>
    public class Storage : IDisposable
    {
        private bool m_isDisposed = false;

        /// <summary></summary>
        public readonly Action<string, object, Func<byte[]>, CancellationToken> f_add;

        /// <summary></summary>
        public readonly Func<string, CancellationToken, byte[]> f_get;

        /// <summary></summary>
        public readonly Action<string, CancellationToken> f_remove;

        /// <summary></summary>
        public readonly Func<string, CancellationToken, object> f_tryGetFromCache;

        /// <summary></summary>
        public readonly Action f_flush;

        /// <summary></summary>
        public readonly Action f_dispose;

        /// <summary></summary>
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

            Register(this);
        }

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



        /// <summary></summary>
        public const bool CACHE_ENABLED = true;
        /// <summary></summary>
        public static KeepAliveCache CACHE { get; private set; }

        private static void Register(Storage storage)
        {
#pragma warning disable CS0162 // Unreachable code detected
            if (!CACHE_ENABLED) return;
#pragma warning restore CS0162 // Unreachable code detected

            lock (s_storages)
            {
                if (s_storages.Contains(storage)) throw new InvalidOperationException();

                if (s_storages.Count == 0)
                {
                    if (CACHE != null) throw new InvalidOperationException();
                    CACHE = new KeepAliveCache("StorageCache", 1L * 1024 * 1024 * 1024);
                    Report.Warn("[Storage] created storage cache");
                }

                s_storages.Add(storage);
                Report.Warn($"[Storage] registered store (total {s_storages.Count})");
            }
        }

        private static void Unregister(Storage storage)
        {
            lock (s_storages)
            {
                if (!s_storages.Contains(storage)) throw new InvalidOperationException();

                s_storages.Remove(storage);
                Report.Warn($"[Storage] unregistered store ({s_storages.Count} left)");

                if (s_storages.Count == 0)
                {
                    if (CACHE == null) throw new InvalidOperationException();
                    CACHE.Dispose();
                    CACHE = null;
                    Report.Warn("[Storage] disposed storage cache");
                }
            }
        }

        private static readonly HashSet<Storage> s_storages = CACHE_ENABLED ? new HashSet<Storage>() : null;

    }
}