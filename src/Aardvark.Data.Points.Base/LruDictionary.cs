/*
   Aardvark Platform
   Copyright (C) 2006-2025  Aardvark Platform Team
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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Aardvark.Base
{
    /// <summary>
    /// Thread-safe, size-bounded dictionary that evicts the least recently used entries.
    /// Successful value retrieval and replacement refresh recency; membership checks do not.
    /// </summary>
    public class LruDictionary<K, V> : IDictionary<K, V>
    {
        /// <summary>Maximum combined entry size retained by the dictionary.</summary>
        public readonly long MaxSize;

        /// <summary>Gets the combined size of all currently retained entries.</summary>
        public long CurrentSize
        {
            get { lock (m_k2e) return m_currentSize; }
        }

        /// <summary>
        /// Creates an empty dictionary with the supplied positive size limit.
        /// </summary>
        public LruDictionary(long maxSize)
        {
            if (maxSize < 1) throw new ArgumentOutOfRangeException(nameof(maxSize));
            MaxSize = maxSize;
        }

        #region Private state

        private class Entry(K key, V value, long size, Action<K, V, long>? onRemove)
        {
            public Entry? Prev;
            public Entry? Next;
            public K Key = key;
            public V Value = value;
            public long Size = size;
            public Action<K, V, long>? OnRemove = onRemove;
        }

        private Entry? m_first = null;
        private Entry? m_last = null;
        private readonly Dictionary<K, Entry> m_k2e = [];
        private long m_currentSize;

        private void UnlinkLocked(Entry e)
        {
            if (e.Prev != null) e.Prev.Next = e.Next; else m_first = e.Next;
            if (e.Next != null) e.Next.Prev = e.Prev; else m_last = e.Prev;
            e.Prev = null;
            e.Next = null;
        }

        private void InsertAtFrontLocked(Entry e)
        {
            if (m_first != null)
            {
                m_first.Prev = e;
                e.Next = m_first;
                m_first = e;
            }
            else
            {
                if (m_last != null) throw new InvalidOperationException();
                m_first = m_last = e;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void TouchLocked(Entry e)
        {
            var first = m_first;
            if (ReferenceEquals(first, e)) return;

            var previous = e.Prev!;
            var next = e.Next;
            previous.Next = next;
            if (next != null) next.Prev = previous; else m_last = previous;

            e.Prev = null;
            e.Next = first;
            first!.Prev = e;
            m_first = e;
        }

        private Entry RemoveEntryLocked(Entry e)
        {
            if (!m_k2e.Remove(e.Key)) throw new InvalidOperationException();
            UnlinkLocked(e);
            m_currentSize -= e.Size;
            return e;
        }

        private Entry? EvictLocked()
        {
            Entry? first = null;
            Entry? last = null;
            while (m_currentSize > MaxSize)
            {
                var e = RemoveEntryLocked(m_last ?? throw new InvalidOperationException());
                if (e.OnRemove != null)
                {
                    if (last != null) last.Next = e; else first = e;
                    last = e;
                }
            }
            return first;
        }

        private bool AddOrUpdateLocked(
            K key, V value, long size, Action<K, V, long>? onRemove,
            out Entry? evicted)
        {
            bool added;
            if (m_k2e.TryGetValue(key, out var e))
            {
                m_currentSize -= e.Size;
                e.Value = value;
                e.Size = size;
                e.OnRemove = onRemove;
                TouchLocked(e);
                added = false;
            }
            else
            {
                e = new Entry(key, value, size, onRemove);
                m_k2e.Add(key, e);
                InsertAtFrontLocked(e);
                added = true;
            }

            m_currentSize += size;
            evicted = EvictLocked();
            return added;
        }

        private static void InvokeRemovalCallbacks(Entry? entry)
        {
            while (entry != null)
            {
                var next = entry.Next;
                entry.Next = null;
                entry.OnRemove?.Invoke(entry.Key, entry.Value, entry.Size);
                entry = next;
            }
        }

        private List<Entry> GetEntriesInOrder()
        {
            var es = new List<Entry>();
            lock (m_k2e)
            {
                var e = m_first;
                while (e != null)
                {
                    es.Add(e);
                    e = e.Next;
                }
                return es;
            }
        }

        #endregion

        /// <summary>Gets the number of currently retained entries.</summary>
        public int Count
        {
            get { lock (m_k2e) return m_k2e.Count; }
        }

        /// <summary>
        /// Adds a new entry or atomically replaces an existing entry's value, size, and removal callback,
        /// moving it to the most-recently-used position. Returns true only when a new key was inserted.
        /// Replacement does not invoke the previous callback. Capacity-eviction callbacks receive the
        /// latest tuple and run after the dictionary state lock has been released.
        /// </summary>
        public bool Add(K key, V value, long size, Action<K, V, long>? onRemove = null)
        {
            if (size > MaxSize || size < 0) throw new ArgumentOutOfRangeException(nameof(size));

            bool added;
            Entry? evicted;
            lock (m_k2e)
                added = AddOrUpdateLocked(key, value, size, onRemove, out evicted);

            InvokeRemovalCallbacks(evicted);
            return added;
        }

        /// <summary>
        /// Removes the entry with the supplied key. Returns false when no such key exists.
        /// If requested, invokes its current removal callback after releasing the state lock.
        /// </summary>
        public bool Remove(K key, bool callOnRemove)
        {
            Entry e;
            lock (m_k2e)
            {
                if (!m_k2e.TryGetValue(key, out e)) return false;
                RemoveEntryLocked(e);
            }

            if (callOnRemove) e.OnRemove?.Invoke(e.Key, e.Value, e.Size);
            return true;
        }

        /// <summary>
        /// Tests membership without changing recency.
        /// </summary>
        public bool ContainsKey(K key)
        {
            lock (m_k2e)
            {
                return m_k2e.ContainsKey(key);
            }
        }

        /// <summary>
        /// Gets a value and moves a successful hit to the most-recently-used position.
        /// Misses do not alter recency.
        /// </summary>
        public bool TryGetValue(K key, out V value)
        {
            lock (m_k2e)
            {
                if (m_k2e.TryGetValue(key, out Entry e))
                {
                    TouchLocked(e);
                    value = e.Value;
                    return true;
                }
                else
                {
#pragma warning disable CS8601
                    value = default;
#pragma warning restore CS8601
                    return false;
                }
            }
        }

        /// <summary>
        /// Returns an existing value and refreshes its recency, or creates and inserts a value on a miss.
        /// The factory may run concurrently for the same key; only the first inserted tuple is retained.
        /// Capacity-eviction callbacks run after the state lock has been released.
        /// </summary>
        public V GetOrCreate(K key, Func<(V, long)> create, Action<K, V, long>? onRemove = null)
        {
            if (TryGetValue(key, out V value)) return value;

            var (createdValue, size) = create();
            Entry? evicted;
            lock (m_k2e)
            {
                if (m_k2e.TryGetValue(key, out var e))
                {
                    TouchLocked(e);
                    return e.Value;
                }

                if (size > MaxSize || size < 0) throw new ArgumentOutOfRangeException(nameof(size));
                AddOrUpdateLocked(key, createdValue, size, onRemove, out evicted);
            }

            InvokeRemovalCallbacks(evicted);
            return createdValue;
        }

        /// <summary>
        /// Removes all entries without invoking removal callbacks.
        /// </summary>
        public void Clear()
        {
            lock (m_k2e)
            {
                m_k2e.Clear();
                m_first = null;
                m_last = null;
                m_currentSize = 0;
            }
        }

        #region IDictionary<K, V>

        /// <summary>
        /// Gets keys from most recently used to least recently used.
        /// </summary>
        public ICollection<K> Keys => GetEntriesInOrder().Map(e => e.Key);

        /// <summary>
        /// Gets values from most recently used to least recently used.
        /// </summary>
        public ICollection<V> Values => GetEntriesInOrder().Map(e => e.Value);

        /// <summary></summary>
        public bool IsReadOnly => false;

        /// <summary>Gets a value and refreshes its recency. Setting is unsupported because entry size is required.</summary>
        public V this[K key]
        {
            get => TryGetValue(key, out V value) ? value : throw new KeyNotFoundException($"Key '{key}' not found. Use TryGetValue instead.");
            set => throw new InvalidOperationException("Item size is required. Use Add(key, value, size) instead.");
        }

        /// <summary></summary>
        public void Add(K key, V value) => throw new InvalidOperationException("Item size is required. Use Add(key, value, size) instead.");

        /// <summary></summary>
        public void Add(KeyValuePair<K, V> item) => throw new InvalidOperationException("Item size is required. Use Add(key, value, size) instead.");

        /// <summary></summary>
        public bool Remove(K key) => Remove(key, true);

        /// <summary>
        /// Removes an entry only when both its key and current value match, invoking its current callback outside the lock.
        /// </summary>
        public bool Remove(KeyValuePair<K, V> item)
        {
            Entry e;
            lock (m_k2e)
            {
                if (!m_k2e.TryGetValue(item.Key, out e)) return false;
                if (!EqualityComparer<V>.Default.Equals(e.Value, item.Value)) return false;
                RemoveEntryLocked(e);
            }

            e.OnRemove?.Invoke(e.Key, e.Value, e.Size);
            return true;
        }

        /// <summary>Tests for an exact key/value pair without changing recency.</summary>
        public bool Contains(KeyValuePair<K, V> item)
        {
            lock (m_k2e)
            {
                return m_k2e.TryGetValue(item.Key, out var e)
                    && EqualityComparer<V>.Default.Equals(e.Value, item.Value);
            }
        }

        /// <summary></summary>
        public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex)
        {
            throw new Exception("Not supported. Error 96c651a1-e404-4791-81f2-84d7c4dd1902.");
        }

        /// <summary></summary>
        public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            lock (m_k2e)
            {
                return m_k2e.Select(kv => new KeyValuePair<K, V>(kv.Key, kv.Value.Value)).ToList().GetEnumerator();
            }
        }

        /// <summary></summary>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion
    }
}
