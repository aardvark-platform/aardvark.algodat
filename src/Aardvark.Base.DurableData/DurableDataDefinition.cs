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
    public partial class DurableDataDefinition
    {
        /// <summary></summary>
        public class Unit
        {
            /// <summary></summary>
            public static readonly Unit Default = new Unit();
        }

        /// <summary>
        /// Gets definition from its id.
        /// </summary>
        public static DurableDataDefinition OfId(Guid id)
        {
            lock (s_defs)
            {
                return s_defs[id];
            }
        }

        private static readonly Dictionary<Guid, DurableDataDefinition> s_defs = new Dictionary<Guid, DurableDataDefinition>();

        /// <summary>
        /// None, nothing, null, etc.
        /// </summary>
        public static readonly DurableDataDefinition None = new DurableDataDefinition(
            Guid.Empty,
            "None",
            "None, nothing, null, etc.",
            DurablePrimitiveTypes.Unit
            );

        /// <summary>
        /// UTF8 encoded string.
        /// </summary>
        public static readonly DurableDataDefinition StringUtf8 = new DurableDataDefinition(
            new Guid("524a4466-1d7a-4717-8cfc-070920bc2756"),
            "StringUtf8",
            "UTF8 encoded string.",
            DurablePrimitiveTypes.UByteArray
            );
    }

    /// <summary>
    /// </summary>
    public partial class DurableDataDefinition
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
        /// Given as a language independent string.
        /// </summary>
        public DurablePrimitiveType Type { get; }

        /// <summary></summary>
        public DurableDataDefinition(Guid id, string name, string comment, DurablePrimitiveType type)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Comment = comment ?? throw new ArgumentNullException(nameof(comment));
            Type = type ?? throw new ArgumentNullException(nameof(type));

            lock (s_defs)
            {
                if (s_defs.ContainsKey(id))
                    throw new Exception($"Duplicate key {id} ({name}, {comment}, {type}).");
                s_defs[id] = this;
            }
        }

        /// <summary></summary>
        public override string ToString() => $"[{Name}, {Type.Name}]";
    }

    /// <summary>
    /// </summary>
    public class DurableDataDefinition<T> : DurableDataDefinition
    {
        /// <summary></summary>
        public DurableDataDefinition(Guid id, string name, string comment)
            : base(id, name, comment, DurablePrimitiveTypes.OfType(typeof(T)))
        {
        }
    }
}
