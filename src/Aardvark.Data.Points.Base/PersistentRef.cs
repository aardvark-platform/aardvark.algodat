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
using System.Diagnostics.CodeAnalysis;

namespace Aardvark.Geometry.Points;

/// <summary>
/// </summary>
public class PersistentRef<T> 
    where T : notnull
{
    private readonly Func<string, T> f_get;
    private readonly TryGetFunc f_tryGetFromCache;

    /// <summary>
    /// </summary>
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
