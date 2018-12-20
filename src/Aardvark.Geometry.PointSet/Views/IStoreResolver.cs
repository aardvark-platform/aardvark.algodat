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
using Aardvark.Data.Points;
using System;
using System.Collections.Generic;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public interface IStoreResolver
    {
        /// <summary>
        /// Gets store for given storePath.
        /// </summary>
        Storage Resolve(string storePath, LruDictionary<string, object> cache);
    }

    /// <summary>
    /// An IStoreResolver using the literal path to create store.
    /// </summary>
    public class IdentityResolver : IStoreResolver
    {
        /// <summary>
        /// </summary>
        public static readonly IdentityResolver Default = new IdentityResolver();

        private readonly Dictionary<string, WeakReference<Storage>> m_cache = new Dictionary<string, WeakReference<Storage>>();
        
        /// <summary>
        /// </summary>
        public Storage Resolve(string storePath, LruDictionary<string, object> cache)
        {
            lock (m_cache)
            {
                if (m_cache.TryGetValue(storePath, out WeakReference<Storage> weakRefStorage))
                {
                    if (weakRefStorage.TryGetTarget(out Storage result)) return result;
                }

                var storage = PointCloud.OpenStore(storePath, cache);
                m_cache[storePath] = new WeakReference<Storage>(storage);
                return storage;
            }
        }

        private IdentityResolver() { }
    }

    /// <summary>
    /// Custom mapping from 'storePath' to real store.
    /// </summary>
    public class MapResolver : IStoreResolver
    {
        private readonly Dictionary<string, Storage> m_mapping = new Dictionary<string, Storage>();

        /// <summary>
        /// </summary>
        public MapResolver(params (string storePath, Storage store)[] mapping)
        {
            foreach (var (storePath, store) in mapping)
            {
                m_mapping[storePath] = store;
            }
        }

        /// <summary>
        /// </summary>
        public Storage Resolve(string storePath, LruDictionary<string, object> _) => m_mapping[storePath];
    }

    /// <summary>
    /// Resolves 'storePath' via custom pattern to real store.
    /// </summary>
    public class PatternResolver : IStoreResolver
    {
        private readonly Dictionary<string, Storage> m_pathToStore = new Dictionary<string, Storage>();

        /// <summary></summary>
        public string PatternStorePath { get; }
        
        /// <summary>
        /// </summary>
        public PatternResolver(string patternStorePath)
        {
            PatternStorePath = patternStorePath ?? throw new ArgumentNullException(nameof(patternStorePath));

            if (!PatternStorePath.Contains("%KEY%")) throw new ArgumentException("PatternStorePath must contain %KEY%.", nameof(patternStorePath));
        }

        /// <summary></summary>
        public Storage Resolve(string storePath, LruDictionary<string, object> cache)
        {
            lock (m_pathToStore)
            {
                if (!m_pathToStore.TryGetValue(storePath, out Storage storage))
                {
                    var realStorePath = PatternStorePath.Replace("%KEY%", storePath);
                    storage = PointCloud.OpenStore(realStorePath, cache);
                    m_pathToStore[storePath] = storage;
                }

                return storage;
            }
        }
    }
}
