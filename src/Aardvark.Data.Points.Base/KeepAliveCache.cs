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

            RegisterCache(this);
        }

        /// <summary>
        /// Adds or refreshes value.
        /// </summary>
        public void Add(object value, long sizeInBytes, Action<object> onRemoved = default)
        {
            if (sizeInBytes > MaxSizeInBytes) throw new ArgumentOutOfRangeException(nameof(sizeInBytes));

            var x = new Command(CommandType.Add, value, sizeInBytes, onRemoved);
            lock (m_lock) { m_clientQueue.Add(x); }
        }

        /// <summary>
        /// Removes value from cache.
        /// </summary>
        public void Remove(object value)
        {
            var x = new Command(CommandType.Remove, value, default, default);
            lock (m_lock) { m_clientQueue.Add(x); }
        }

        /// <summary>
        /// Removes all values from cache.
        /// </summary>
        public void Flush()
        {
            var x = new Command(CommandType.Flush, default, default, default);
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
            public readonly Action<object> OnRemoved;
            public Command(CommandType type, object value, long sizeInBytes, Action<object> onRemoved)
            {
                if (sizeInBytes < 0) throw new ArgumentOutOfRangeException(nameof(sizeInBytes));
                Type = type;
                Value = value;
                SizeInBytes = sizeInBytes;
                OnRemoved = onRemoved;
            }
        }

        private struct Entry
        {
            public readonly long SizeInBytes;
            public readonly long Timestamp;
            public readonly Action<object> OnRemoved;

            public Entry(long sizeInBytes, long timestamp, Action<object> onRemoved)
            {
                SizeInBytes = sizeInBytes;
                Timestamp = timestamp;
                OnRemoved = onRemoved;
            }
        }

        private long m_nextTimestamp = 0;
        private readonly Dictionary<object, Entry> m_entries = new Dictionary<object, Entry>();
        private List<Command> m_clientQueue = new List<Command>();
        private List<Command> m_internalQueue = new List<Command>();
        private readonly object m_lock = new object();

        private void SwapQueues()
        {
            lock (m_lock)
            {
                var tmp = m_clientQueue;
                m_clientQueue = m_internalQueue;
                m_internalQueue = tmp;
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
            UnregisterCache(this);
            if (disposing)
            {
            }
        }

        /// <summary></summary>
        ~KeepAliveCache()
        {
            Dispose(false);
        }



        private readonly static HashSet<KeepAliveCache> s_allCaches = new HashSet<KeepAliveCache>();
        private static bool s_active = false;
        private static ManualResetEventSlim s_startedUpKeeper = new ManualResetEventSlim(true);
        private static ManualResetEventSlim s_startedUpLogger = new ManualResetEventSlim(true);
        private static ManualResetEventSlim s_shutdownKeeper = new ManualResetEventSlim(true);
        private static ManualResetEventSlim s_shutdownLogger = new ManualResetEventSlim(true);

        private static void RegisterCache(KeepAliveCache cache)
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

                    if (s_startedUpKeeper.IsSet)
                    {
                        s_startedUpKeeper.Reset();
                    }
                    else
                    {
                        var msg = $"[KeepAliveCache] RegisterCache({cache.FriendlyName}) failed due to inconsistent Keeper thread state.";
                        Report.Error(msg);
                        throw new InvalidOperationException(msg);
                    }
                    if (s_startedUpLogger.IsSet)
                    {
                        s_startedUpLogger.Reset();
                    }
                    else
                    {
                        var msg = $"[KeepAliveCache] RegisterCache({cache.FriendlyName}) failed due to inconsistent Logger thread state.";
                        Report.Error(msg);
                        throw new InvalidOperationException(msg);
                    }

                    new Thread(Keeper) { IsBackground = true }.Start();
                    new Thread(ConsoleLogger) { IsBackground = true }.Start();

                    s_startedUpKeeper.Wait();
                    s_startedUpLogger.Wait();
                }
            }
        }

        private static void UnregisterCache(KeepAliveCache cache)
        {
            if (cache == null) throw new ArgumentNullException(nameof(cache));

            var shutdown = false;
            lock (s_allCaches)
            {
                if (!s_allCaches.Contains(cache)) Report.Error($"[KeepAliveCache.Unregister] not registered ({cache.FriendlyName}).");

                s_allCaches.Remove(cache);
                Report.Warn($"[KeepAliveCache] unregistered {cache.FriendlyName}");

                if (s_allCaches.Count == 0)
                {
                    if (s_shutdownKeeper.IsSet)
                    {
                        s_shutdownKeeper.Reset();
                    }
                    else
                    {
                        var msg = $"[KeepAliveCache] UnregisterCache({cache.FriendlyName}) failed due to inconsistent Keeper thread state.";
                        Report.Error(msg);
                        throw new InvalidOperationException(msg);
                    }
                    if (s_shutdownLogger.IsSet)
                    {
                        s_shutdownLogger.Reset();
                    }
                    else
                    {
                        var msg = $"[KeepAliveCache] UnregisterCache({cache.FriendlyName}) failed due to inconsistent Logger thread state.";
                        Report.Error(msg);
                        throw new InvalidOperationException(msg);
                    }

                    s_active = false;
                    shutdown = true;
                }
            }

            if (shutdown)
            {
                s_shutdownKeeper.Wait();
                s_shutdownLogger.Wait();
            }
        }

        private static void Keeper()
        {
            try
            {
                s_startedUpKeeper.Set();
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
                                            cache.m_entries[cmd.Value] = new Entry(cmd.SizeInBytes, cache.m_nextTimestamp++, cmd.OnRemoved);
                                        }
                                        break;

                                    case CommandType.Remove:
                                        {
                                            if (cache.m_entries.TryGetValue(cmd.Value, out Entry existing))
                                            {
                                                cache.CurrentSizeInBytes -= existing.SizeInBytes;
                                                cache.m_entries.Remove(cmd.Value);
                                                cmd.OnRemoved?.Invoke(cmd.Value);
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
                                        throw new NotImplementedException($"Command not implemented: {cmd.Type}");
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
                                    kv.Value.OnRemoved?.Invoke(kv.Key);

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
                s_shutdownKeeper.Set();
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
                s_startedUpLogger.Set();
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
                s_shutdownLogger.Set();
                Report.Warn($"[KeepAliveCache] Logger has shut down.");
            }
        }
    }
}
