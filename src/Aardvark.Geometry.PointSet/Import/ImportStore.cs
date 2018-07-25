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
using Aardvark.Base;
using Aardvark.Data.Points;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Uncodium.SimpleStore;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// Importers for various formats.
    /// </summary>
    public static partial class PointCloud
    {
        /// <summary>
        /// Gets general info for store dataset.
        /// </summary>
        public static PointFileInfo StoreInfo(string storePath, string key)
        {
            if (!Directory.Exists(storePath)) return new PointFileInfo(storePath, PointCloudFileFormat.Unknown, 0, 0, Box3d.Invalid);

            var store = OpenStore(storePath);
            var pointset = store.GetPointSet(key, CancellationToken.None);
            return new PointFileInfo(storePath, PointCloudFileFormat.Store, 0L, pointset.PointCount, pointset.Bounds);
        }

        /// <summary>
        /// Loads point cloud from store.
        /// </summary>
        public static PointSet Load(string key, string storePath)
        {
            if (!Directory.Exists(storePath)) throw new InvalidOperationException($"Not a store ({storePath}).");

            var store = OpenStore(storePath);
            var result = store.GetPointSet(key, CancellationToken.None);
            if (result == null) throw new InvalidOperationException($"Key {key} not found in {storePath}.");
            return result;
        }

        /// <summary>
        /// Loads point cloud from store.
        /// </summary>
        public static PointSet Load(string key, Storage store)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            var result = store.GetPointSet(key, CancellationToken.None);
            if (result == null) throw new InvalidOperationException($"Key {key} not found in store.");
            return result;
        }

        /// <summary>
        /// Opens or creates a store at the specified location.
        /// </summary>
        public static Storage OpenStore(string storePath)
        {
            lock (s_stores)
            {
                if (s_stores.TryGetValue(storePath, out WeakReference<Storage> x) && x.TryGetTarget(out Storage cached))
                {
                    if (!cached.IsDisposed) return cached;
                }

                var store = new SimpleDiskStore(storePath).ToPointCloudStore();
                s_stores[storePath] = new WeakReference<Storage>(store);
                return store;
            }
        }
        private static Dictionary<string, WeakReference<Storage>> s_stores = new Dictionary<string, WeakReference<Storage>>();

        /// <summary>
        /// Creates an in-memory store.
        /// </summary>
        public static Storage CreateInMemoryStore() => new SimpleMemoryStore().ToPointCloudStore();
    }
}
