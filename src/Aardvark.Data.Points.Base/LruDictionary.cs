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
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Base
{
    /// <summary>
    /// </summary>
    public class LruDictionary<K, V> : IDictionary<K, V>
    {
        /// <summary></summary>
        public readonly long MaxSize;

        /// <summary></summary>
        public long CurrentSize { get; private set; }

        /// <summary>
        /// </summary>
        public LruDictionary(long maxSize)
        {
            if (maxSize < 1) throw new ArgumentOutOfRangeException(nameof(maxSize));
            MaxSize = maxSize;
        }

        #region Private state

        private class Entry
        {
            public Entry? Prev;
            public Entry? Next;
            public K Key;
            public V Value;
            public long Size;
            public Action<K, V, long>? OnRemove;

            public Entry(K key, V value, long size, Action<K, V, long>? onRemove)
            {
                Key = key;
                Value = value;
                Size = size;
                OnRemove = onRemove;
            }
        }
        private Entry? m_first = null;
        private Entry? m_last = null;
        private readonly Dictionary<K, Entry> m_k2e = new();

        private void Unlink(Entry e)
        {
            if (e.Prev != null) e.Prev.Next = e.Next; else m_first = e.Next;
            if (e.Next != null) e.Next.Prev = e.Prev; else m_last = e.Prev;
        }
        private void InsertAtFront(Entry e)
        {
            if (m_first != null)
            {
                m_first.Prev = e;
                e.Prev = null;
                e.Next = m_first;
                m_first = e;
            }
            else
            {
                if (m_last != null) throw new InvalidOperationException();
                m_first = m_last = e;
            }
        }
        private void RemoveLast()
        {
            Entry? removed = null;
            lock (m_k2e)
            {
                if (CurrentSize <= MaxSize) return;
                if (m_last == null) return;
                removed = m_last;
                m_k2e.Remove(removed.Key);
                CurrentSize -= m_last.Size;
                m_last = m_last.Prev;
                if (m_last == null) throw new InvalidOperationException();
                m_last.Next = null;
            }

            removed.OnRemove?.Invoke(removed.Key, removed.Value, removed.Size);
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

        /// <summary>
        /// </summary>
        public int Count => m_k2e.Count;

        /// <summary>
        /// Adds or refreshes key/value pair.
        /// Returns true if key did not exist.
        /// </summary>
        public bool Add(K key, V value, long size, Action<K, V, long>? onRemove = null)
        {
            if (size > MaxSize || size < 0) throw new ArgumentOutOfRangeException(nameof(size));

            Entry? e = null;
            lock (m_k2e)
            {
                if (m_k2e.TryGetValue(key, out e))
                {
                    Unlink(e);
                    CurrentSize -= e.Size; e.Size = size; 
                }
                else
                {
                    e = new Entry(key, value, size, onRemove);
                    m_k2e[key] = e;
                }
                
                InsertAtFront(e);
                CurrentSize += e.Size;
            }
            
            while (CurrentSize > MaxSize)
            {
                RemoveLast();
            }

            return e == null;
        }

        /// <summary>
        /// Removes entry with given key, or nothing if key does not exist.
        /// Returns true if entry did exist.
        /// </summary>
        public bool Remove(K key, bool callOnRemove)
        {
            Entry? e = null;

            lock (m_k2e)
            {
                if (m_k2e.TryGetValue(key, out e))
                {
                    m_k2e.Remove(key);
                    if (e.Prev != null) e.Prev.Next = e.Next; else m_first = e.Next;
                    if (e.Next != null) e.Next.Prev = e.Prev; else m_last = e.Prev;
                    CurrentSize -= e.Size;
                }
                else
                {
                    return false;
                }
            }

            if (callOnRemove)
            {
                e.OnRemove?.Invoke(key, e.Value, e.Size);
            }
            return true;
        }

        /// <summary>
        /// </summary>
        public bool ContainsKey(K key)
        {
            lock (m_k2e)
            {
                return m_k2e.ContainsKey(key);
            }
        }

        /// <summary>
        /// </summary>
        public bool TryGetValue(K key, out V value)
        {
            lock (m_k2e)
            {
                if (m_k2e.TryGetValue(key, out Entry e))
                {
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
        /// </summary>
        public V GetOrCreate(K key, Func<(V, long)> create, Action<K, V, long>? onRemove = null)
        {
            if (TryGetValue(key, out V value)) return value;

            var (createdValue, size) = create();
            lock (m_k2e)
            {
                if (TryGetValue(key, out V v)) return v;
                Add(key, createdValue, size, onRemove);
                return createdValue;
            }
        }

        /// <summary>
        /// </summary>
        public void Clear()
        {
            lock (m_k2e)
            {
                m_k2e.Clear();
                m_first = null;
                m_last = null;
                CurrentSize = 0;
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

        /// <summary></summary>
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

        /// <summary></summary>
        public bool Remove(KeyValuePair<K, V> item) => Remove(item.Key, true);

        /// <summary></summary>
        public bool Contains(KeyValuePair<K, V> item)
        {
            if (TryGetValue(item.Key, out V value))
            {
                if (item.Value != null) return item.Value.Equals(value);
                else return value == null;
            }
            return false;
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
