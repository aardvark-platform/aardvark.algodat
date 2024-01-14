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
        /// Max cache size in bytes.
        /// </summary>
        public readonly long MaxSizeInBytes;

        /// <summary>
        /// Current cache size in bytes.
        /// </summary>
        public long CurrentSizeInBytes { get; private set; } = 0;

        /// <summary>
        /// Number of operations processed so far.
        /// </summary>
        public long ProcessedCount => m_nextTimestamp;

        /// <summary>
        /// Logger on/off.
        /// </summary>
        public bool Verbose { get; set; }

        /// <summary>
        /// Used for logger.
        /// </summary>
        public string FriendlyName { get; }

        /// <summary>
        /// </summary>
        public KeepAliveCache(string friendlyName, long maxMemoryInBytes, bool verbose = false)
        {
            if (maxMemoryInBytes < 0) throw new ArgumentOutOfRangeException(nameof(maxMemoryInBytes));

            FriendlyName = friendlyName ?? "KeepAliveCache";
            MaxSizeInBytes = maxMemoryInBytes;
            Verbose = verbose;

            Register(this);
        }

        /// <summary>
        /// Adds or refreshes value.
        /// </summary>
        public void Add(object value, long sizeInBytes)
        {
            if (sizeInBytes > MaxSizeInBytes) throw new ArgumentOutOfRangeException(nameof(sizeInBytes));

            var x = new Command(CommandType.Add, value, sizeInBytes);
            lock (m_lock) { m_clientQueue.Add(x); }
        }

        /// <summary>
        /// Removes value from cache.
        /// </summary>
        public void Remove(object value)
        {
            var x = new Command(CommandType.Remove, value, 0);
            lock (m_lock) { m_clientQueue.Add(x); }
        }

        /// <summary>
        /// Removes all values from cache.
        /// </summary>
        public void Flush()
        {
            var x = new Command(CommandType.Flush, string.Empty, 0);
            lock (m_lock) { m_clientQueue.Add(x); }
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
        
        private long m_nextTimestamp = 0;
        private readonly Dictionary<object, Entry> m_entries = new();
        private List<Command> m_clientQueue = new();
        private List<Command> m_internalQueue = new();
        private readonly object m_lock = new();

        private void SwapQueues()
        {
            lock (m_lock)
            {
                (m_internalQueue, m_clientQueue) = (m_clientQueue, m_internalQueue);
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
            Unregister(this);
            if (disposing)
            {
            }
        }

        /// <summary></summary>
        ~KeepAliveCache()
        {
            Dispose(false);
        }



        private readonly static HashSet<KeepAliveCache> s_allCaches = new();
        private static bool s_active = false;

        private static void Register(KeepAliveCache cache)
        {
            if (cache == null) throw new ArgumentNullException(nameof(cache));

            lock (s_allCaches)
            {
                if (s_allCaches.Contains(cache)) Report.Error($"[KeepAliveCache.Register] already registered ({cache.FriendlyName}).");

                s_allCaches.Add(cache);
                Report.Warn($"[KeepAliveCache] registered {cache.FriendlyName}");

                if (s_allCaches.Count == 1)
                {
                    s_active = true;
                    new Thread(Keeper).Start();
                    new Thread(ConsoleLogger).Start();
                    // TODO: wait for startup of Keeper and Logger
                }
            }
        }

        private static void Unregister(KeepAliveCache cache)
        {
            if (cache == null) throw new ArgumentNullException(nameof(cache));

            lock (s_allCaches)
            {
                if (!s_allCaches.Contains(cache)) Report.Error($"[KeepAliveCache.Unregister] not registered ({cache.FriendlyName}).");

                s_allCaches.Remove(cache);
                Report.Warn($"[KeepAliveCache] unregistered {cache.FriendlyName}");

                if (s_allCaches.Count == 0)
                {
                    s_active = false;
                    // TODO: wait for shutdown of Keeper and Logger
                }
            }
        }

        private static void Keeper()
        {
            try
            {
                Report.Warn($"[KeepAliveCache] Keeper has started up.");
                while (s_active)
                {
                    // wait for command(s) to arrive
                    Thread.Sleep(10);

                    lock (s_allCaches)
                    {
                        foreach (var cache in s_allCaches)
                        {
                            if (cache.m_clientQueue.Count == 0) continue;

                            // swap client queue with internal queue,
                            // so clients can immediately resume ...
                            cache.SwapQueues();

                            // ... and we can process in parallel
                            foreach (var cmd in cache.m_internalQueue)
                            {
                                switch (cmd.Type)
                                {
                                    case CommandType.Add:
                                        {
                                            if (cache.m_entries.TryGetValue(cmd.Value, out Entry existing))
                                                cache.CurrentSizeInBytes -= existing.SizeInBytes;
                                            cache.CurrentSizeInBytes += cmd.SizeInBytes;
                                            cache.m_entries[cmd.Value] = new Entry(cmd.SizeInBytes, cache.m_nextTimestamp++);
                                        }
                                        break;

                                    case CommandType.Remove:
                                        {
                                            if (cache.m_entries.TryGetValue(cmd.Value, out Entry existing))
                                            {
                                                cache.CurrentSizeInBytes -= existing.SizeInBytes;
                                                cache.m_entries.Remove(cmd.Value);
                                            }
                                        }
                                        break;

                                    case CommandType.Flush:
                                        {
                                            cache.m_entries.Clear();
                                            cache.CurrentSizeInBytes = 0;
                                        }
                                        break;

                                    default:
                                        throw new Exception($"Unknown command \"{cmd.Type}\". Error 1bc83df9-84b6-4b42-85a1-1f0d1b10bb82.");
                                }
                            }

                            if (cache.CurrentSizeInBytes > cache.MaxSizeInBytes)
                            {
                                //Report.BeginTimed("[KeepAliveCache] collect");
                                var ordered = cache.m_entries.OrderBy(kv => kv.Value.Timestamp).ToArray();
                                foreach (var kv in ordered)
                                {
                                    cache.CurrentSizeInBytes -= kv.Value.SizeInBytes;
                                    cache.m_entries.Remove(kv.Key);

                                    if (cache.CurrentSizeInBytes <= cache.MaxSizeInBytes) break;
                                }
                                //Report.EndTimed();
                            }

                            cache.m_internalQueue.Clear();
                        }
                    }

                }
            }
            catch (Exception e)
            {
                Report.Error($"[KeepAliveCache] {e}");
            }
            finally
            {
                Report.Warn($"[KeepAliveCache] Keeper has shut down.");
            }
        }

        private double m_opsPerSecond = -1;
        private DateTimeOffset m_tPrev = DateTimeOffset.UtcNow;
        private long m_prevNextTimestamp = 0L;
        private void LogLine()
        {
            if (m_nextTimestamp == m_prevNextTimestamp) return;

            var tNow = DateTimeOffset.UtcNow;
            var x = (m_nextTimestamp - m_prevNextTimestamp) / (tNow - m_tPrev).TotalSeconds;
            m_opsPerSecond = m_opsPerSecond < 0 ? x : 0.9 * m_opsPerSecond + 0.1 * x;
            m_prevNextTimestamp = m_nextTimestamp;
            m_tPrev = tNow;

            var fillrate = MaxSizeInBytes > 0 ? CurrentSizeInBytes / (double)MaxSizeInBytes : 0.0;

            Report.Line($"[{FriendlyName}] {fillrate * 100,7:0.00}% | {m_entries.Count,8:N0} entries | {m_clientQueue.Count,8:N0} pending | {m_opsPerSecond,10:N1} ops/s | {m_nextTimestamp,14:N0} processed");
        }

        private static void ConsoleLogger()
        {
            try
            {
                Report.Warn($"[KeepAliveCache] Logger has started up.");
                var nextLogLine = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(1);
                while (s_active)
                {
                    Thread.Sleep(100);
                    if (DateTimeOffset.UtcNow < nextLogLine) continue;
                    nextLogLine = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);

                    lock (s_allCaches)
                    {
                        foreach (var cache in s_allCaches)
                        {
                            if (!s_active) return;

                            cache.LogLine();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Report.Error($"[KeepAliveCache] {e}");
            }
            finally
            {
                Report.Warn($"[KeepAliveCache] Logger has shut down.");
            }
        }
    }
}
