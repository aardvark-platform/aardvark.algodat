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
        Storage Resolve(string storePath);
    }

    /// <summary>
    /// An IStoreResolver using the literal path to create store.
    /// </summary>
    public class IdentityResolver : IStoreResolver
    {
        private readonly Dictionary<string, WeakReference<Storage>> m_cache = new Dictionary<string, WeakReference<Storage>>();
        
        /// <summary>
        /// </summary>
        public Storage Resolve(string storePath)
        {
            lock (m_cache)
            {
                if (m_cache.TryGetValue(storePath, out WeakReference<Storage> weakRefStorage))
                {
                    if (weakRefStorage.TryGetTarget(out Storage result)) return result;
                }

                var storage = PointCloud.OpenStore(storePath);
                m_cache[storePath] = new WeakReference<Storage>(storage);
                return storage;
            }
        }
    }

    /// <summary>
    /// An IStoreResolver using a simple custom mapping from path to store.
    /// This means, that given paths are used as keys and not literal paths.
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
        public Storage Resolve(string storePath) => m_mapping[storePath];
    }
}
