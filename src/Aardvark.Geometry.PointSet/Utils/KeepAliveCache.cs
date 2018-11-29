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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Aardvark.Base
{
    /// <summary>
    /// Keeps alive objects via strong reference.
    /// If max memory is reached, then objects will be removed based on a LRU policy.
    /// </summary>
    public class KeepAliveCache : IDisposable
    {
        /// <summary>
        /// Default cache with 4 GB and verbosity enabled.
        /// </summary>
        public static readonly Lazy<KeepAliveCache> Default = new Lazy<KeepAliveCache>(() => new KeepAliveCache(4L * 1024 * 1024 * 1024, true), true);

        /// <summary>
        /// If queue is longer, then clients will be delayed on Add, Remove and Flush calls.
        /// </summary>
        public const int QueueSizeToDelayClients = 4096;

        /// <summary>
        /// Max cache size in bytes.
        /// </summary>
        public readonly long MaxSize;

        /// <summary>
        /// Current cache size in bytes.
        /// </summary>
        public long CurrentSize { get; private set; } = 0;

        /// <summary>
        /// Number of commands processed so far.
        /// </summary>
        public long ProcessedCount => m_nextTimestamp;

        /// <summary>
        /// </summary>
        public KeepAliveCache(long maxMemoryInBytes, bool verbose = false)
        {
            if (maxMemoryInBytes < 0) throw new ArgumentOutOfRangeException(nameof(maxMemoryInBytes));
            MaxSize = maxMemoryInBytes;

            new Thread(Keeper).Start();

            if (verbose) new Thread(ConsoleLogger).Start();
        }

        /// <summary>
        /// Adds or refreshes value.
        /// </summary>
        public void Add(object value, long sizeInBytes)
        {
            if (sizeInBytes > MaxSize) throw new ArgumentOutOfRangeException(nameof(sizeInBytes));

            var x = new Command(CommandType.Add, value, sizeInBytes);
            lock (m_lock) { m_clientQueue.Add(x); m_mre.Set(); }

            if (m_clientQueue.Count > QueueSizeToDelayClients) Thread.Sleep(1);
        }

        /// <summary>
        /// Removes value from cache.
        /// </summary>
        public void Remove(object value)
        {
            var x = new Command(CommandType.Remove, value, 0);
            lock (m_lock) { m_clientQueue.Add(x); m_mre.Set(); }

            if (m_clientQueue.Count > QueueSizeToDelayClients) Thread.Sleep(1);
        }

        /// <summary>
        /// Removes all values from cache.
        /// </summary>
        public void Flush()
        {
            var x = new Command(CommandType.Flush, null, 0);
            lock (m_lock) { m_clientQueue.Add(x); m_mre.Set(); }

            if (m_clientQueue.Count > QueueSizeToDelayClients) Thread.Sleep(1);
        }


        private enum CommandType
        {
            Add,
            Remove,
            Flush
        }

        private struct Command
        {
            public readonly CommandType Type;
            public readonly object Value;
            public readonly long SizeInBytes;
            public Command(CommandType type, object value, long sizeInBytes)
            {
                if (sizeInBytes < 0) throw new ArgumentOutOfRangeException(nameof(sizeInBytes));
                Type = type;
                Value = value;
                SizeInBytes = sizeInBytes;
            }
        }

        private struct Entry
        {
            public readonly long SizeInBytes;
            public readonly long Timestamp;

            public Entry(long sizeInBytes, long timestamp)
            {
                SizeInBytes = sizeInBytes;
                Timestamp = timestamp;
            }
        }

        private ManualResetEventSlim m_mre = new ManualResetEventSlim();
        private bool m_active = true;
        private long m_nextTimestamp = 0;
        private readonly Dictionary<object, Entry> m_entries = new Dictionary<object, Entry>();
        private List<Command> m_clientQueue = new List<Command>();
        private List<Command> m_internalQueue = new List<Command>();
        private readonly object m_lock = new object();

        private void SwapQueues()
        {
            var tmp = m_clientQueue;
            m_clientQueue = m_internalQueue;
            m_internalQueue = tmp;
        }

        private void Keeper()
        {
            try
            {
                while (m_active)
                {
                    // wait for command(s) to arrive
                    if (!m_mre.Wait(1000)) continue;
                    if (m_clientQueue.Count == 0) throw new InvalidOperationException();

                    // swap client queue with internal queue,
                    // so clients can immediately resume ...
                    lock (m_lock)
                    {
                        SwapQueues();
                        m_mre.Reset();
                    }

                    // ... and we can process in parallel
                    foreach (var cmd in m_internalQueue)
                    {
                        switch (cmd.Type)
                        {
                            case CommandType.Add:
                                {
                                    if (m_entries.TryGetValue(cmd.Value, out Entry existing)) CurrentSize -= existing.SizeInBytes;
                                    CurrentSize += cmd.SizeInBytes;
                                    m_entries[cmd.Value] = new Entry(cmd.SizeInBytes, m_nextTimestamp++);
                                }
                                break;

                            case CommandType.Remove:
                                {
                                    if (m_entries.TryGetValue(cmd.Value, out Entry existing))
                                    {
                                        CurrentSize -= existing.SizeInBytes;
                                        m_entries.Remove(cmd.Value);
                                    }
                                }
                                break;

                            case CommandType.Flush:
                                {
                                    m_entries.Clear();
                                    CurrentSize = 0;
                                }
                                break;

                            default:
                                throw new NotImplementedException($"Command not implemented: {cmd.Type}");
                        }

                        if (CurrentSize > MaxSize)
                        {
                            foreach (var kv in m_entries.OrderBy(kv => kv.Value.Timestamp))
                            {
                                CurrentSize -= kv.Value.SizeInBytes;
                                m_entries.Remove(kv.Key);

                                if (CurrentSize <= MaxSize) break;
                            }
                        }
                    }
                    m_internalQueue.Clear();
                }
            }
            catch (Exception e)
            {
                Report.Error($"[KeepAliveCache] {e}");
            }
        }

        private void ConsoleLogger()
        {
            try
            {
                var t0 = DateTimeOffset.UtcNow;
                var prevNextTimestamp = 0L;
                while (m_active)
                {
                    Thread.Sleep(1000);
                    if (!m_active) break;

                    if (m_nextTimestamp == prevNextTimestamp) continue;
                    prevNextTimestamp = m_nextTimestamp;

                    var fillrate = MaxSize > 0 ? CurrentSize / (double)MaxSize : 0.0;
                    var cps = m_nextTimestamp / (DateTimeOffset.UtcNow - t0).TotalSeconds;
                    Report.Line($"[KeepAliveCache] {fillrate,7:0.000}% | {m_entries.Count,8:N0} entries | {m_clientQueue.Count,8:N0} pending | {cps,10:N1} cmd/s | {m_nextTimestamp,14:N0} processed");
                }
            }
            catch (Exception e)
            {
                Report.Error($"[KeepAliveCache] {e}");
            }
        }

        /// <summary></summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary></summary>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                m_active = false;
            }
        }

        /// <summary></summary>
        ~KeepAliveCache()
        {
            Dispose(false);
        }
    }
}
