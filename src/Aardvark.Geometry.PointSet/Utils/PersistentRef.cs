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

namespace Aardvark.Geometry.Points;


/// <summary>
/// </summary>
public class PersistentRef<T> 
    where T : notnull
{
    private readonly Func<string, T> f_get;
    private readonly TryGetFunc f_tryGetFromCache;

    public delegate bool TryGetFunc(string key, [NotNullWhen(true)] out T? result);

    /// <summary>
    /// </summary>
    public PersistentRef(Guid id, Func<string, T> get, TryGetFunc tryGetFromCache)
        : this(id.ToString(), get, tryGetFromCache)
    {
    }

    /// <summary>
    /// </summary>
    public PersistentRef(string id, Func<string, T> get, TryGetFunc tryGetFromCache)
    {
        Id = id;
        f_get = get ?? throw new ArgumentNullException(nameof(get));
        f_tryGetFromCache = tryGetFromCache ?? throw new ArgumentNullException(nameof(tryGetFromCache));
    }

    /// <summary>
    /// </summary>
    public PersistentRef(Guid id, T valueInMemory)
        : this(id.ToString(), valueInMemory)
    {
    }

    /// <summary>
    /// </summary>
    public PersistentRef(string id, T valueInMemory)
    {
        Id = id;
        f_get = _ => valueInMemory;
        bool tryGetFromCache(string key, [NotNullWhen(true)] out T? result)
        {
            result = valueInMemory;
            return true;
        }
        f_tryGetFromCache = tryGetFromCache;
    }

    /// <summary>
    /// </summary>
    public string Id { get; }
    
    /// <summary>
    /// </summary>
    public T Value => f_get(Id);

    /// <summary>
    /// </summary>
    public bool TryGetFromCache([NotNullWhen(true)] out T? value)
        => f_tryGetFromCache(Id, out value);

}
