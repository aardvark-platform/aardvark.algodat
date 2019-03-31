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
    /// Language independent primitive type definition.
    /// </summary>
    public class DurablePrimitiveType
    {
        /// <summary>
        /// Language-independent type.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Size (in bytes) of a single element of this type.
        /// I.e. the same for T and T[].
        /// </summary>
        public int ElementSizeInBytes { get; }

        /// <summary>
        /// Is this an array of primitive elements.
        /// </summary>
        public bool IsArray { get; }

        /// <summary>
        /// </summary>
        public DurablePrimitiveType(string name, int elementSizeInBytes, bool isArray)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            ElementSizeInBytes = elementSizeInBytes;
            IsArray = isArray;
        }
    }
}
