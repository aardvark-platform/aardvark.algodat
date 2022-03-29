/*
    Copyright (C) 2006-2022. Aardvark Platform Team. http://github.com/aardvark-platform.
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

namespace Aardvark.Geometry.Points
{
    internal class Disposable : IDisposable
    {
        private readonly object m_lock = new();
        private bool m_isDisposed = false;
        private Action m_onDispose;

        public Disposable(Action onDispose)
        {
            m_onDispose = onDispose ?? throw new ArgumentNullException(nameof(onDispose));
        }

        public void Dispose()
        {
            lock (m_lock)
            {
                if (m_isDisposed) throw new ObjectDisposedException("Disposable");
                m_isDisposed = true;
            }

            m_onDispose();
            m_onDispose = null;
        }
    }
}
