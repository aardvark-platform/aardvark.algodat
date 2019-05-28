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
        /// <summary></summary>
        public class Unit
        {
            /// <summary></summary>
            public static readonly Unit Default = new Unit();
        }

        /// <summary>
        /// Basic durable data types (language independent).
        /// Numeric values are little endian, unless otherwise specified.
        /// </summary>
        public static class Types
        {
            /// <summary>
            /// Unit.
            /// </summary>
            public const string Unit = "unit";

            /// <summary>
            /// Floating point value (32-bit).
            /// </summary>
            public const string Float32 = "float32";

            /// <summary>
            /// Floating point value (64-bit).
            /// </summary>
            public const string Float64 = "float64";

            /// <summary>
            /// Signed 16-bit integer. 2-complement.
            /// </summary>
            public const string Int16 = "int16";

            /// <summary>
            /// Signed 32-bit integer. 2-complement.
            /// </summary>
            public const string Int32 = "int32";

            /// <summary>
            /// Signed 64-bit integer. 2-complement.
            /// </summary>
            public const string Int64 = "int64";

            /// <summary>
            /// Unsigned 16-bit integer.
            /// </summary>
            public const string UInt16 = "uint16";

            /// <summary>
            /// Unsigned 32-bit integer.
            /// </summary>
            public const string UInt32 = "uint32";

            /// <summary>
            /// Unsigned 64-bit integer.
            /// </summary>
            public const string UInt64 = "uint64";
        }
    }
}
