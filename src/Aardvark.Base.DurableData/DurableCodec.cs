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
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Aardvark.Base
{
    /// <summary>
    /// Binary encode/decode of durable data types.
    /// </summary>
    public static class DurableCodec
    {
        #region Sizes

        /// <summary>
        /// [T].
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOf<T>() where T : struct
        {
            return Marshal.SizeOf<T>();
        }

        /// <summary>
        /// [T].
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOf(Type t)
        {
            return Marshal.SizeOf(t);
        }

        /// <summary>
        /// [count:int32][Ts...]
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOf<T>(T[] xs) where T : struct => xs != null ? 4 + xs.Length * Marshal.SizeOf<T>() : 4;

        /// <summary>
        /// [Guid][T].
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOfDurable<T>() where T : struct => 16 + Marshal.SizeOf<T>();

        /// <summary>
        /// [Guid][count:int32][Ts...].
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOfDurable<T>(T[] xs) where T : struct => xs != null ? 20 + xs.Length * Marshal.SizeOf<T>() : 20;

        #endregion

        #region Primitive types

        /// <summary>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Encode<T>(byte[] dst, ref int index, in T value) where T : struct
        {
            if (dst == null) throw new ArgumentNullException(nameof(dst));
            var gc = GCHandle.Alloc(dst, GCHandleType.Pinned);
            var size = SizeOf<T>();
            Marshal.StructureToPtr(value, gc.AddrOfPinnedObject() + index, true);
            index += size;
        }

        /// <summary>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] Encode<T>(in T value) where T : struct
        {
            var size = SizeOf<T>();
            var dst = new byte[size];
            var gc = GCHandle.Alloc(dst, GCHandleType.Pinned);
            Marshal.StructureToPtr(value, gc.AddrOfPinnedObject(), true);
            return dst;
        }

        /// <summary>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Decode<T>(byte[] src, ref int index, out T value) where T : struct
        {
            var gc = GCHandle.Alloc(src, GCHandleType.Pinned);
            var size = SizeOf<T>();
            value = Marshal.PtrToStructure<T>(gc.AddrOfPinnedObject() + index);
            index += size;
        }
        /// <summary>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Decode(DurablePrimitiveType t, byte[] src, ref int index, out object value)
        {
            var gc = GCHandle.Alloc(src, GCHandleType.Pinned);
            var size = SizeOf(t.ElementType);
            value = Marshal.PtrToStructure(gc.AddrOfPinnedObject() + index, t.ElementType);
            index += size;
        }

        /// <summary>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Decode<T>(byte[] src, ref int index) where T : struct
        {
            var gc = GCHandle.Alloc(src, GCHandleType.Pinned);
            var size = SizeOf<T>();
            var value = Marshal.PtrToStructure<T>(gc.AddrOfPinnedObject() + index);
            index += size;
            return value;
        }

        /// <summary>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Decode<T>(byte[] src, out T value) where T : struct
        {
            var gc = GCHandle.Alloc(src, GCHandleType.Pinned);
            value = Marshal.PtrToStructure<T>(gc.AddrOfPinnedObject());
        }

        /// <summary>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Decode<T>(byte[] src) where T : struct
        {
            var gc = GCHandle.Alloc(src, GCHandleType.Pinned);
            return Marshal.PtrToStructure<T>(gc.AddrOfPinnedObject());
        }



        /// <summary>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EncodeArray<T>(byte[] dst, ref int index, in T[] values) where T : struct
        {
            if (dst == null) throw new ArgumentNullException(nameof(dst));
            if (values == null) throw new ArgumentNullException(nameof(values));

            var gc = GCHandle.Alloc(values, GCHandleType.Pinned);
            try
            {
                // encode count
                if (index + 4 >= dst.Length) throw new IndexOutOfRangeException();
                unsafe
                {
                    fixed (byte* p = dst)
                    {
                        *((int*)(p + index)) = values.Length;
                    }
                }
                index += 4;

                // encode array values
                var size = values.Length * SizeOf<T>();
                if (index + size >= dst.Length) throw new IndexOutOfRangeException();
                Marshal.Copy(gc.AddrOfPinnedObject(), dst, index, size);
                index += size;
            }
            finally
            {
                gc.Free();
            }
        }
        
        /// <summary>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] EncodeArray<T>(in T[] values) where T : struct
        {
            if (values == null) throw new ArgumentNullException(nameof(values));

            var totalSize = 4 + values.Length * SizeOf<T>();
            var dst = new byte[totalSize];
            var gc = GCHandle.Alloc(values, GCHandleType.Pinned);
            try
            {
                // encode count
                unsafe
                {
                    fixed (byte* p = dst)
                    {
                        *((int*)p) = values.Length;
                    }
                }

                // encode array values
                var size = values.Length * SizeOf<T>();
                Marshal.Copy(gc.AddrOfPinnedObject(), dst, 4, size);
                return dst;
            }
            finally
            {
                gc.Free();
            }
        }

        /// <summary>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DecodeArray<T>(byte[] src, ref int index, out T[] values) where T : struct
        {
            // decode count
            var count = BitConverter.ToInt32(src, index);
            index += 4;

            // decode array values
            values = new T[count];
            var pDst = GCHandle.Alloc(values, GCHandleType.Pinned);
            try
            {
                var size = count * SizeOf<T>();
                Marshal.Copy(src, index, pDst.AddrOfPinnedObject(), size);
                index += size;
            }
            finally
            {
                pDst.Free();
            }
        }

        /// <summary>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[] DecodeArray<T>(byte[] src, ref int index) where T : struct
        {
            // decode count
            var count = BitConverter.ToInt32(src, index);
            index += 4;

            // decode array values
            var values = new T[count];
            var pDst = GCHandle.Alloc(values, GCHandleType.Pinned);
            try
            {
                var size = count * SizeOf<T>();
                Marshal.Copy(src, index, pDst.AddrOfPinnedObject(), size);
                index += size;
                return values;
            }
            finally
            {
                pDst.Free();
            }
        }

        /// <summary>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DecodeArray<T>(byte[] src, out T[] values) where T : struct
        {
            // decode count
            var count = BitConverter.ToInt32(src, 0);

            // decode array values
            values = new T[count];
            var pDst = GCHandle.Alloc(values, GCHandleType.Pinned);
            try
            {
                var size = count * SizeOf<T>();
                Marshal.Copy(src, 4, pDst.AddrOfPinnedObject(), size);
            }
            finally
            {
                pDst.Free();
            }
        }

        /// <summary>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[] DecodeArray<T>(byte[] src) where T : struct
        {
            // decode count
            var count = BitConverter.ToInt32(src, 0);

            // decode array values
            var values = new T[count];
            var pDst = GCHandle.Alloc(values, GCHandleType.Pinned);
            try
            {
                var size = count * SizeOf<T>();
                Marshal.Copy(src, 4, pDst.AddrOfPinnedObject(), size);
                return values;
            }
            finally
            {
                pDst.Free();
            }
        }



        /// <summary>
        /// Encodes durable primitive type.
        /// </summary>
        public static byte[] EncodeString(string value) => EncodeArray(Encoding.UTF8.GetBytes(value));

        /// <summary>
        /// Encodes durable primitive type.
        /// </summary>
        public static byte[] EncodeStringArray(string[] values)
        {
            using (var ms = new MemoryStream())
            {
                using (var bw = new BinaryWriter(ms))
                {
                    bw.Write((int)values.Length);
                    foreach (var s in values)
                    {
                        bw.Write(EncodeArray(Encoding.UTF8.GetBytes(s)));
                    }
                }
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Decodes durable primitive type.
        /// </summary>
        public static string DecodeString(byte[] src) => Encoding.UTF8.GetString(DecodeArray<byte>(src));

        /// <summary>
        /// Decodes durable primitive type.
        /// </summary>
        public static string[] DecodeStringArray(byte[] src)
        {
            var count = BitConverter.ToInt32(src, 0);
            var result = new string[count];
            var index = 4;
            for (var i = 0; i < count; i++)
                result[i] = Encoding.UTF8.GetString(DecodeArray<byte>(src, ref index));
            return result;
        }

        #endregion

        #region Durable

        /// <summary>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EncodeDurable<T>(DurableDataDefinition def, byte[] dst, ref int index, in T value) where T : struct
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (dst == null) throw new ArgumentNullException(nameof(dst));

            var t = DurablePrimitiveTypes.OfType(typeof(T));
            if (def.Type != t) throw new InvalidOperationException();

            Encode(dst, ref index, def.Id);
            Encode(dst, ref index, value);
        }

        /// <summary>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] EncodeDurable<T>(DurableDataDefinition def, in T value) where T : struct
        {
            if (def == null) throw new ArgumentNullException(nameof(def));

            var dst = new byte[SizeOfDurable<T>()];
            var t = DurablePrimitiveTypes.OfType(typeof(T));
            if (def.Type != t) throw new InvalidOperationException();

            var index = 0;
            Encode(dst, ref index, def.Id);
            Encode(dst, ref index, value);

            return dst;
        }

        /// <summary>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DecodeDurable<T>(byte[] src, ref int index, out (DurableDataDefinition def, T value) result) where T : struct
        {
            if (src == null) throw new ArgumentNullException(nameof(src));

            Decode(src, ref index, out Guid id);
            var def = DurableDataDefinition.OfId(id);

            var t = DurablePrimitiveTypes.OfType(typeof(T));
            if (def.Type != t) throw new InvalidOperationException();

            Decode(src, ref index, out T value);
            result = (def, value);
        }

        /// <summary>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (DurableDataDefinition def, T value) DecodeDurable<T>(byte[] src, ref int index) where T : struct
        {
            if (src == null) throw new ArgumentNullException(nameof(src));

            Decode(src, ref index, out Guid id);
            var def = DurableDataDefinition.OfId(id);

            var t = DurablePrimitiveTypes.OfType(typeof(T));
            if (def.Type != t) throw new InvalidOperationException();

            Decode(src, ref index, out T value);
            return (def, value);
        }

        /// <summary>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DecodeDurable<T>(byte[] src, out (DurableDataDefinition def, T value) result) where T : struct
        {
            if (src == null) throw new ArgumentNullException(nameof(src));

            var index = 0;
            Decode(src, ref index, out Guid id);
            var def = DurableDataDefinition.OfId(id);

            var t = DurablePrimitiveTypes.OfType(typeof(T));
            if (def.Type != t) throw new InvalidOperationException();

            Decode(src, ref index, out T value);
            result = (def, value);
        }

        /// <summary>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (DurableDataDefinition def, T value) DecodeDurable<T>(byte[] src) where T : struct
        {
            if (src == null) throw new ArgumentNullException(nameof(src));

            var index = 0;
            Decode(src, ref index, out Guid id);
            var def = DurableDataDefinition.OfId(id);

            var t = DurablePrimitiveTypes.OfType(typeof(T));
            if (def.Type != t) throw new InvalidOperationException();

            Decode(src, ref index, out T value);
            return (def, value);
        }

        /// <summary>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (DurableDataDefinition def, object value) DecodeDurable(byte[] src)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));

            var index = 0;
            Decode(src, ref index, out Guid id);
            var def = DurableDataDefinition.OfId(id);
            
            Decode(def.Type, src, ref index, out object value);
            return (def, value);
        }

        #endregion
    }
}
