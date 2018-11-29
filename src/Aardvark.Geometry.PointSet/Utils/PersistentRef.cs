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

//#define BREAK_ON_RELOAD

using System;
using System.Diagnostics;
using System.Threading;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public class PersistentRef<T> where T : class
    {
        private readonly Func<string, CancellationToken, T> f_get;
        private WeakReference<T> m_value;
#if BREAK_ON_RELOAD
        private bool m_loadedOnce = false;
#endif
        /// <summary>
        /// </summary>
        public PersistentRef(string id, Func<string, CancellationToken, T> get, T cachedValue = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            f_get = get ?? throw new ArgumentNullException(nameof(get));
            if (cachedValue != null) m_value = new WeakReference<T>(cachedValue);
        }

        /// <summary>
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// </summary>
        public T GetValue(CancellationToken ct)
        {
            if (m_value != null && m_value.TryGetTarget(out T result)) return result;
#if BREAK_ON_RELOAD
            if (m_loadedOnce) Debugger.Break();
            m_loadedOnce = true;
#endif
            result = f_get(Id, ct);
            m_value = new WeakReference<T>(result);
            return result;
        }

        /// <summary>
        /// </summary>
        public bool TryGetValue(out T value)
        {
            if (m_value != null)
            {
                return m_value.TryGetTarget(out value);
            }
            else
            {
                value = null;
                return false;
            }
        }

        /// <summary>
        /// </summary>
        public T Value => GetValue(CancellationToken.None);
    }
}
