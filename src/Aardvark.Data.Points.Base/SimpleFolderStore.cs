using System;
using System.IO;
using System.Threading;

namespace Uncodium.SimpleStore
{
    /// <summary>
    /// SimpleStore with folder backend.
    /// TODO: extract to Uncodium.SimpleStore.
    /// </summary>
    public class SimpleFolderStore : ISimpleStore
    {
        /// <summary>
        /// The store folder.
        /// </summary>
        public string Folder { get; }

        private string GetFileNameFromId(string id) => Path.Combine(Folder, id);
        private Stats m_stats = new Stats();

        /// <summary>
        /// Creates a store in the given folder.
        /// </summary>
        public SimpleFolderStore(string folder)
        {
            Folder = folder;
            if (!Directory.Exists(Folder)) Directory.CreateDirectory(folder);
        }

        /// <summary></summary>
        public Stats Stats => m_stats;

        /// <summary></summary>
        public void Add(string id, object value, Func<byte[]> getEncodedValue)
        {
            Interlocked.Increment(ref m_stats.CountAdd);
            File.WriteAllBytes(GetFileNameFromId(id), getEncodedValue());
        }

        /// <summary></summary>
        public void Dispose() { }

        /// <summary></summary>
        public void Flush() { Interlocked.Increment(ref m_stats.CountFlush); }

        /// <summary></summary>
        public byte[] Get(string id)
        {
            Interlocked.Increment(ref m_stats.CountGet);
            try
            {
                var buffer = File.ReadAllBytes(GetFileNameFromId(id));
                Interlocked.Increment(ref m_stats.CountGetCacheMiss);
                return buffer;
            }
            catch
            {
                Interlocked.Increment(ref m_stats.CountGetInvalidKey);
                return null;
            }
        }

        /// <summary></summary>
        public void Remove(string id)
        {
            try
            {
                File.Delete(GetFileNameFromId(id));
                Interlocked.Increment(ref m_stats.CountRemove);
            }
            catch
            {
                Interlocked.Increment(ref m_stats.CountRemoveInvalidKey);
            }
        }

        /// <summary></summary>
        public string[] SnapshotKeys()
        {
            Interlocked.Increment(ref m_stats.CountSnapshotKeys);
            return Directory.GetFiles(Folder);
        }

        /// <summary></summary>
        public object TryGetFromCache(string id) => null;
    }

}
