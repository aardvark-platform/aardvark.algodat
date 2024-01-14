/*
   Aardvark Platform
   Copyright (C) 2006-2024  Aardvark Platform Team
   https://aardvark.graphics

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/
using Aardvark.Base;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Aardvark.Data.Points
{
    /// <summary>
    /// </summary>
    public class Storage : IDisposable
    {
        private static readonly HashSet<Storage> s_storages = new();

        /// <summary>
        /// No storage. All functions are NOP.
        /// </summary>
        public static readonly Storage None = new(
            add: (_, _, _) => { },
            get: _ => null,
            getSlice: (_, _, _) => null,
            remove: _ => { },
            dispose: () => { },
            flush: () => { },
            cache: null
            );

        private bool m_isDisposed = false;

        /// <summary>add(key, value, create)</summary>
        public readonly Action<string, object, Func<byte[]>> f_add;

        /// <summary>
        /// Returns null if key does not exist.
        /// </summary>
        public readonly Func<string, byte[]?> f_get;

        /// <summary></summary>
        public readonly Func<string, long, int, byte[]?> f_getSlice;

        /// <summary></summary>
        public readonly Action<string> f_remove;
        
        /// <summary></summary>
        public readonly Action f_flush;

        /// <summary></summary>
        public readonly Action f_dispose;

        /// <summary></summary>
        public readonly LruDictionary<string, object>? Cache;

        /// <summary></summary>
        public Storage(
            Action<string, object, Func<byte[]>> add,
            Func<string, byte[]?> get,
            Func<string, long, int, byte[]?> getSlice,
            Action<string> remove,
            Action dispose,
            Action flush,
            LruDictionary<string, object>? cache
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
        [MemberNotNullWhen(true, nameof(Cache))]
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
    }
}