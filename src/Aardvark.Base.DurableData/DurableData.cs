/*
    Copyright (C) 2006-2018. Aardvark Platform Team. http://github.com/aardvark-platform.

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

namespace Aardvark.Base
{
    /// <summary>
    /// </summary>
    public partial class DurableData
    {
        /// <summary></summary>
        public class Unit
        {
            /// <summary></summary>
            public static readonly Unit Default = new Unit();
        }

        /// <summary>
        /// None, nothing, null, etc.
        /// </summary>
        public static readonly DurableData None = new DurableData(Guid.Empty, "None", "None, nothing, null, etc.", DurableTypes.Unit);
    }

    /// <summary>
    /// </summary>
    public partial class DurableData
    {
        /// <summary></summary>
        public Guid Id { get; }

        /// <summary>
        /// User readable name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Description.
        /// </summary>
        public string Comment { get; }

        /// <summary>
        /// Data type of this durable data.
        /// Given as language independent string.
        /// </summary>
        public string Type { get; }

        /// <summary></summary>
        public DurableData(Guid id, string name, string comment, string type)
        {
            lock (s_guids)
                if (!s_guids.Add(id))
                    throw new Exception($"Duplicate key {id} ({name}, {comment}, {type}).");

            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Comment = comment ?? throw new ArgumentNullException(nameof(comment));
            Type = type ?? throw new ArgumentNullException(nameof(type));
        }

        private static readonly HashSet<Guid> s_guids = new HashSet<Guid>();
    }

    /// <summary>
    /// </summary>
    public class DurableData<T> : DurableData
    {
        /// <summary></summary>
        public DurableData(Guid id, string name, string comment)
            : base(id, name, comment, DurableTypes.OfType(typeof(T)))
        {
        }
    }
}
