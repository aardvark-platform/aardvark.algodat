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
    public abstract partial class DurableData
    {
        /// <summary>
        /// None, nothing, null, etc.
        /// </summary>
        public static readonly DurableData<Unit> None =
            new DurableData<Unit>(
                Guid.Empty,
                "None",
                "None, nothing, null, etc.",
                "unit",
                null
            );

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

        /// <summary>
        /// List of durable data IDs this data depends on.
        /// </summary>
        public Guid[] DependsOn { get; }

        /// <summary></summary>
        public abstract object Compute(IDictionary<Guid, DurableData> attributes);

        /// <summary></summary>
        public DurableData(Guid id, string name, string comment, string type, params Guid[] dependsOn)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(type));
            Comment = comment ?? throw new ArgumentNullException(nameof(type));
            Type = type ?? throw new ArgumentNullException(nameof(type));
            DependsOn = dependsOn ?? Array.Empty<Guid>();
        }

        /// <summary></summary>
        public DurableData(Guid id, string name, string comment, string type, params DurableData[] dependsOn)
            : this(id, name, comment, type, MapToGuids(dependsOn))
        {
        }

        private static Guid[] MapToGuids(DurableData[] xs)
        {
            if (xs == null) return Array.Empty<Guid>();
            var rs = new Guid[xs.Length];
            for (var i = 0; i < xs.Length; i++) rs[i] = xs[i].Id;
            return rs;
        }
    }

    /// <summary></summary>
    public class DurableData<T> : DurableData
    {
        /// <summary></summary>
        private readonly Func<DurableData<T>, IDictionary<Guid, DurableData>, T> m_compute;

        /// <summary></summary>
        public DurableData(
            Guid id,
            string name,
            string comment,
            string type,
            Func<DurableData<T>, IDictionary<Guid, DurableData>, T> compute,
            params DurableData[] depends
            )
            : base(id, name, comment, type, depends)
        {
            m_compute = compute;
        }

        /// <summary></summary>
        public override object Compute(IDictionary<Guid, DurableData> attributes)
            => m_compute(this, attributes);
    }
}
