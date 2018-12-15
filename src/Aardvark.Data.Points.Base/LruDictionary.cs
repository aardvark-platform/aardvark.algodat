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
        
        private class Entry
        {
            public Entry Prev;
            public Entry Next;
            public long Size;
            public V Value;
            public Action<K, V, long> OnRemove;
        }
        private Entry m_first = null;
        private Entry m_last = null;
        private readonly Dictionary<K, Entry> m_k2e = new Dictionary<K, Entry>();

        private void Unlink(Entry e)
        {
            if (e.Prev != null) e.Prev.Next = e.Next; else m_first = e.Next;
            if (e.Next != null) e.Next.Prev = e.Prev; else m_last = e.Prev;
        }
        private void InsertAtFront(Entry e)
        {
            if (m_first != null)
            {
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
            lock (m_k2e)
            {
                if (m_last == null) return;
                m_last = m_last.Prev;
                m_last.Next = null;
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

        /// <summary>
        /// </summary>
        public int Count => m_k2e.Count;

        /// <summary>
        /// Adds or refreshes key/value pair.
        /// Returns true if key did not exist.
        /// </summary>
        public bool Add(K key, V value, long size, Action<K, V, long> onRemove = null)
        {
            if (size > MaxSize) throw new ArgumentOutOfRangeException(nameof(size));

            Entry e = null;
            lock (m_k2e)
            {
                if (m_k2e.TryGetValue(key, out e))
                {
                    Unlink(e);
                    CurrentSize -= e.Size; e.Size = size; 
                }
                else
                {
                    e = new Entry { Value = value, Size = size, OnRemove = onRemove };
                    m_k2e[key] = e;
                }
            }

            InsertAtFront(e);
            CurrentSize += e.Size;

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
            Entry e = null;

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
                    value = default;
                    return false;
                }
            }
        }

        /// <summary>
        /// </summary>
        public V GetOrCreate(K key, Func<(V, long)> create, Action<K, V, long> onRemove = null)
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
            }
        }

        #region IDictionary<K, V>

        /// <summary>
        /// Gets keys. No specific order.
        /// </summary>
        public ICollection<K> Keys => m_k2e.Keys.ToArray();

        /// <summary>
        /// Gets values from most recently used to least recently used.
        /// </summary>
        public ICollection<V> Values => GetEntriesInOrder().Map(e => e.Value);

        /// <summary></summary>
        public bool IsReadOnly => false;

        /// <summary></summary>
        public V this[K key] { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        /// <summary></summary>
        public void Add(K key, V value) => throw new InvalidOperationException("Item size is required.");

        /// <summary></summary>
        public void Add(KeyValuePair<K, V> item) => throw new InvalidOperationException("Item size is required.");

        /// <summary></summary>
        public bool Remove(K key) => Remove(key, true);

        /// <summary></summary>
        public bool Remove(KeyValuePair<K, V> item) => Remove(item.Key, true);

        /// <summary></summary>
        public bool Contains(KeyValuePair<K, V> item)
        {
            if (TryGetValue(item.Key, out V value))
            {
                return item.Value.Equals(value);
            }
            return false;
        }

        /// <summary></summary>
        public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
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
