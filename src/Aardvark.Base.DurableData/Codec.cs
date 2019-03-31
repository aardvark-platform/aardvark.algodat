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
using System.Runtime.InteropServices;
using System.Text;

namespace Aardvark.Base.DurableDataCodec
{
    /// <summary>
    /// Binary read/write of basic data types.
    /// </summary>
    public static class Codec
    {
        /// <summary>
        /// Writes struct/primitive to byte array.
        /// </summary>
        public static void Write<T>(byte[] dst, ref int index, T value) where T : struct
        {
            var gc = GCHandle.Alloc(dst, GCHandleType.Pinned);
            Marshal.StructureToPtr(value, gc.AddrOfPinnedObject() + index, true);
        }

        /// <summary>
        /// Writes array of structs/primitives to byte array.
        /// </summary>
        public static T Read<T>(byte[] src, ref int index) where T : struct
        {
            var gc = GCHandle.Alloc(src, GCHandleType.Pinned);
            return Marshal.PtrToStructure<T>(gc.AddrOfPinnedObject() + index);
        }


        /// <summary>
        /// Reads array of structs/primitives from byte array.
        /// </summary>
        public static void Write<T>(byte[] dst, ref int index, params T[] value) where T : struct
        {
            var gc = GCHandle.Alloc(value, GCHandleType.Pinned);
            var size = value.Length * Marshal.SizeOf<T>();
            try { Marshal.Copy(gc.AddrOfPinnedObject(), dst, index, size); index += size; }
            finally { gc.Free(); }
        }

        /// <summary>
        /// Reads array of structs/primitives from byte array.
        /// </summary>
        public static T[] Read<T>(byte[] src, ref int index, int elementCount) where T : struct
        {
            var value = new T[elementCount];
            var gc = GCHandle.Alloc(value, GCHandleType.Pinned);
            var size = value.Length * Marshal.SizeOf<T>();
            try { Marshal.Copy(src, index, gc.AddrOfPinnedObject(), size); index += size; return value; }
            finally { gc.Free(); }
        }

        /// <summary>
        /// Reads struct/primitive from byte array.
        /// </summary>
        public static void Read<T>(byte[] src, ref int index, ref T[] value) where T : struct
        {
            var gc = GCHandle.Alloc(value, GCHandleType.Pinned);
            var size = value.Length * Marshal.SizeOf<T>();
            try { Marshal.Copy(src, index, gc.AddrOfPinnedObject(), size); index += size; }
            finally { gc.Free(); }
        }
    }
}
