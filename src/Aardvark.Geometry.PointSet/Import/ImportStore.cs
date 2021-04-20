/*
    Copyright (C) 2006-2021. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System.Linq;
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

            var store = OpenStore(storePath, cache: default);
            var pointset = store.GetPointSet(key);
            return new PointFileInfo(storePath, PointCloudFileFormat.Store, 0L, pointset.PointCount, pointset.Bounds);
        }

        /// <summary>
        /// Loads point cloud from store.
        /// </summary>
        public static PointSet Load(string key, string storePath, LruDictionary<string, object> cache)
        {
            //if (!Directory.Exists(storePath)) throw new InvalidOperationException($"Not a store ({storePath}).");

            var store = OpenStore(storePath, cache);
            var result = store.GetPointSet(key);
            if (result == null) throw new InvalidOperationException($"Key {key} not found in {storePath}.");
            return result;
        }

        /// <summary>
        /// Loads point cloud from store.
        /// </summary>
        public static PointSet Load(string key, Storage store)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            var result = store.GetPointSet(key);
            if (result == null) throw new InvalidOperationException($"Key {key} not found in store.");
            return result;
        }

        /// <summary>
        /// Opens or creates a store at the specified location.
        /// </summary>
        public static Storage OpenStore(string storePath, LruDictionary<string, object> cache, Action<string[]> logLines = null)
        {
            lock (s_stores)
            {
                // if we are lucky, then this storePath is already cached
                if (s_stores.TryGetValue(storePath, out WeakReference<Storage> x) && x.TryGetTarget(out Storage cached))
                {
                    if (!cached.IsDisposed)
                    {
                        // cache hit -> we are done
                        return cached;
                    }
                }

                // cache miss ...
                ISimpleStore simpleStore = null;
                ISimpleStore initDiskStore() => logLines is null ? new SimpleDiskStore(storePath) : new SimpleDiskStore(storePath, logLines);
                ISimpleStore initFolderStore() => new SimpleFolderStore(storePath);

                if (File.Exists(storePath))
                {
                    // if storePath is a file, then open as Uncodium.SimpleDiskStore v3
                    simpleStore = initDiskStore();

                    // .. or fail
                }
                else
                {
                    // if storePath is a folder, then ...
                    if (Directory.Exists(storePath))
                    {
                        // check for a file named 'data.bin'
                        var hasDataFile  = File.Exists(Path.Combine(storePath, "data.bin"));
                        if (hasDataFile)
                        {
                            try
                            {
                                // a 'data.bin' file indicates that this could be
                                // - a pre-v3 store (then there is also an index.bin file) -> this will be automatically upgraded to v3 format
                                // - or the result of an automatic upgrade from a pre-v3 store (data.bin has been upgraded to v3 format and index.bin has been imported and deleted)
                                simpleStore = initDiskStore();
                            }
                            catch (Exception e)
                            {
                                // mmmh, unfortunately, the 'data.bin' file is not a SimpleDiskStore
                                // -> probably the intention was to open the folder as a SimpleFolderStore,
                                // but by accident there is a key/file named 'index.bin'
                                Report.Warn(
                                    $"Failed to open the folder '{storePath}' (containing a file named index.bin) as an Uncodium.SimpleDiskStore. " +
                                    $"Opening folder as an Uncodium.SimpleFolderStore instead. " +
                                    $"Exception was {e}.");
                                simpleStore = initFolderStore();
                            }
                        }
                        else
                        {
                            // we have a folder without a 'data.bin' file -> open folder as SimpleFolderStore
                            simpleStore = initFolderStore();
                        }
                    }
                    else
                    {
                        // storePath does not exist (neither file nor folder)
                        // -> just create new SimpleDiskStore
                        simpleStore = initDiskStore();
                    }
                }

                // insert into cache and return
                var store = simpleStore.ToPointCloudStore(cache);
                s_stores[storePath] = new WeakReference<Storage>(store);
                return store;
            }
        }
        private static readonly Dictionary<string, WeakReference<Storage>> s_stores = new Dictionary<string, WeakReference<Storage>>();

        /// <summary>
        /// Creates an in-memory store.
        /// </summary>
        public static Storage CreateInMemoryStore(LruDictionary<string, object> cache)
            => new SimpleMemoryStore().ToPointCloudStore(cache ?? new LruDictionary<string, object>(1024 * 1024 * 1024))
            ;
    }
}
