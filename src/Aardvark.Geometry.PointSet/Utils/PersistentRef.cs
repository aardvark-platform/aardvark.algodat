/*
    Copyright (C) 2006-2023. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System.Diagnostics.CodeAnalysis;

namespace Aardvark.Geometry.Points
{

    /// <summary>
    /// </summary>
    public class PersistentRef<T> 
        where T : notnull
    {
        private readonly Func<string, T?> f_get;
        private readonly Func<string, (bool, T?)> f_tryGet;

        /// <summary>
        /// </summary>
        public PersistentRef(Guid id, Func<string, T> get, Func<string, (bool, T?)> tryGet)
            : this(id.ToString(), get, tryGet)
        {
        }

        /// <summary>
        /// </summary>
        public PersistentRef(string id, Func<string, T?> get, Func<string, (bool, T?)> tryGet)
        {
            Id = id; //?? throw new ArgumentNullException(nameof(id));
            f_get = get ?? throw new ArgumentNullException(nameof(get));
            f_tryGet = tryGet ?? throw new ArgumentNullException(nameof(tryGet));
        }
        
        /// <summary>
        /// </summary>
        public string Id { get; }
        
        /// <summary>
        /// </summary>
        public bool TryGetValue([NotNullWhen(true)]out T? value)
        {
            bool isSome = false;
            T? x = default;
            (isSome, x) = f_tryGet(Id);
            value = x;
            return isSome;
        }

        /// <summary>
        /// </summary>
        public (bool hasValue, T? value) TryGetValue() => f_tryGet(Id);

        /// <summary>
        /// </summary>
        public T? Value => f_get(Id);
    }
}
