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
    /// Primitive durable data types (language independent).
    /// Numeric values are little endian, unless otherwise specified.
    /// </summary>
    public static class DurablePrimitiveTypes
    {
        #region Primitives

        /// <summary>
        /// Unit.
        /// </summary>
        public static readonly DurablePrimitiveType Unit = new DurablePrimitiveType<DurableDataDefinition.Unit>("unit", 0, false);


        /// <summary>
        /// 16 bytes GUID (https://tools.ietf.org/html/rfc4122).
        /// </summary>
        public static readonly DurablePrimitiveType Guid = new DurablePrimitiveType<Guid>("guid", 16, false);
        /// <summary>
        /// Array of 16 bytes GUID (https://tools.ietf.org/html/rfc4122).
        /// </summary>
        public static readonly DurablePrimitiveType GuidArray = new DurablePrimitiveType<Guid>("guid[]", 16, true);


        /// <summary>
        /// Signed 8-bit integer. 2-complement.
        /// </summary>
        public static readonly DurablePrimitiveType SByte = new DurablePrimitiveType<sbyte>("sbyte", 1, false);
        /// <summary>
        /// Array of signed 8-bit integer. 2-complement.
        /// </summary>
        public static readonly DurablePrimitiveType SByteArray = new DurablePrimitiveType<sbyte>("sbyte[]", 1, true);

        /// <summary>
        /// Unsigned 8-bit integer.
        /// </summary>
        public static readonly DurablePrimitiveType UByte = new DurablePrimitiveType<byte>("ubyte", 1, false);
        /// <summary>
        /// Array of unsigned 8-bit integer.
        /// </summary>
        public static readonly DurablePrimitiveType UByteArray = new DurablePrimitiveType<byte>("ubyte[]", 1, true);


        /// <summary>
        /// Signed 16-bit integer. 2-complement.
        /// </summary>
        public static readonly DurablePrimitiveType Int16 = new DurablePrimitiveType<Int16>("int16", 2, false);
        /// <summary>
        /// Array of signed 16-bit integer. 2-complement.
        /// </summary>
        public static readonly DurablePrimitiveType Int16Array = new DurablePrimitiveType<Int16>("int16[]", 2, true);

        /// <summary>
        /// Unsigned 16-bit integer.
        /// </summary>
        public static readonly DurablePrimitiveType UInt16 = new DurablePrimitiveType<UInt16>("uint16", 2, false);
        /// <summary>
        /// Array of unsigned 16-bit integer.
        /// </summary>
        public static readonly DurablePrimitiveType UInt16Array = new DurablePrimitiveType<UInt16>("uint16[]", 2, true);


        /// <summary>
        /// Signed 32-bit integer. 2-complement.
        /// </summary>
        public static readonly DurablePrimitiveType Int32 = new DurablePrimitiveType<Int32>("int32", 4, false);
        /// <summary>
        /// Array of signed 32-bit integer. 2-complement.
        /// </summary>
        public static readonly DurablePrimitiveType Int32Array = new DurablePrimitiveType<Int32>("int32[]", 4, true);

        /// <summary>
        /// Unsigned 32-bit integer.
        /// </summary>
        public static readonly DurablePrimitiveType UInt32 = new DurablePrimitiveType<UInt32>("uint32", 4, false);
        /// <summary>
        /// Array of unsigned 32-bit integer.
        /// </summary>
        public static readonly DurablePrimitiveType UInt32Array = new DurablePrimitiveType<UInt32>("uint32[]", 4, true);


        /// <summary>
        /// Signed 64-bit integer. 2-complement.
        /// </summary>
        public static readonly DurablePrimitiveType Int64 = new DurablePrimitiveType<Int64>("int64", 8, false);
        /// <summary>
        /// Array of signed 64-bit integer. 2-complement.
        /// </summary>
        public static readonly DurablePrimitiveType Int64Array = new DurablePrimitiveType<Int64>("int64[]", 8, true);

        /// <summary>
        /// Unsigned 64-bit integer.
        /// </summary>
        public static readonly DurablePrimitiveType UInt64 = new DurablePrimitiveType<UInt64>("uint64", 8, false);
        /// <summary>
        /// Array of unsigned 64-bit integer.
        /// </summary>
        public static readonly DurablePrimitiveType UInt64Array = new DurablePrimitiveType<UInt64>("uint64[]", 8, true);


        /// <summary>
        /// Floating point value (32-bit).
        /// </summary>
        public static readonly DurablePrimitiveType Float32 = new DurablePrimitiveType<float>("float32", 4, false);
        /// <summary>
        /// Array of floating point value (32-bit).
        /// </summary>
        public static readonly DurablePrimitiveType Float32Array = new DurablePrimitiveType<float>("float32[]", 4, true);

        /// <summary>
        /// Floating point value (64-bit).
        /// </summary>
        public static readonly DurablePrimitiveType Float64 = new DurablePrimitiveType<double>("float64", 8, false);
        /// <summary>
        /// Array of Floating point value (64-bit).
        /// </summary>
        public static readonly DurablePrimitiveType Float64Array = new DurablePrimitiveType<double>("float64[]", 8, true);

        #endregion

        #region Ranges

        /// <summary>Aardvark.Base.Cell [X:int64,Y:int64,Z:int64,E:int32]</summary>
        public static readonly DurablePrimitiveType Cell = new DurablePrimitiveType<Cell>("Cell", 80, false);

        /// <summary>Aardvark.Base.Range1sb [MIN:sbyte,MAX:sbyte]</summary>
        public static readonly DurablePrimitiveType Range1sb = new DurablePrimitiveType<Range1sb>("Range1sb", 2, false);

        /// <summary>Aardvark.Base.Range1b [MIN:ubyte,MAX:ubyte]</summary>
        public static readonly DurablePrimitiveType Range1b = new DurablePrimitiveType<Range1b>("Range1b", 2, false);

        /// <summary>Aardvark.Base.Range1sb [MIN:int16,MAX:int16]</summary>
        public static readonly DurablePrimitiveType Range1s = new DurablePrimitiveType<Range1s>("Range1s", 4, false);

        /// <summary>Aardvark.Base.Range1s [MIN:uint16,MAX:uint16]</summary>
        public static readonly DurablePrimitiveType Range1us = new DurablePrimitiveType<Range1us>("Range1us", 4, false);

        /// <summary>Aardvark.Base.Range1sb [MIN:int32,MAX:int32]</summary>
        public static readonly DurablePrimitiveType Range1i = new DurablePrimitiveType<Range1i>("Range1i", 8, false);

        /// <summary>Aardvark.Base.Range1s [MIN:uint32,MAX:uint32]</summary>
        public static readonly DurablePrimitiveType Range1ui = new DurablePrimitiveType<Range1ui>("Range1ui", 8, false);

        /// <summary>Aardvark.Base.Range1sb [MIN:int64,MAX:int64]</summary>
        public static readonly DurablePrimitiveType Range1l = new DurablePrimitiveType<Range1l>("Range1l", 16, false);

        /// <summary>Aardvark.Base.Range1s [MIN:uint64,MAX:uint64]</summary>
        public static readonly DurablePrimitiveType Range1ul = new DurablePrimitiveType<Range1ul>("Range1ul", 16, false);

        /// <summary>Aardvark.Base.Range1f [MIN:float32,MAX:float32]</summary>
        public static readonly DurablePrimitiveType Range1f = new DurablePrimitiveType<Range1f>("Range1f", 8, false);

        /// <summary>Aardvark.Base.Range1f [MIN:float32,MAX:float32]</summary>
        public static readonly DurablePrimitiveType Range1d = new DurablePrimitiveType<Range1d>("Range1d", 16, false);
        

        /// <summary>Aardvark.Base.Box2f [MIN:V2i,MAX:V2i]</summary>
        public static readonly DurablePrimitiveType Box2i = new DurablePrimitiveType<Box2i>("Box2i", 16, false);

        /// <summary>Aardvark.Base.Box3f [MIN:V3i,MAX:V3i]</summary>
        public static readonly DurablePrimitiveType Box3i = new DurablePrimitiveType<Box3i>("Box3i", 24, false);


        /// <summary>Aardvark.Base.Box2f [MIN:V2l,MAX:V2l]</summary>
        public static readonly DurablePrimitiveType Box2l = new DurablePrimitiveType<Box2l>("Box2l", 32, false);

        /// <summary>Aardvark.Base.Box3f [MIN:V3l,MAX:V3l]</summary>
        public static readonly DurablePrimitiveType Box3l = new DurablePrimitiveType<Box3l>("Box3l", 48, false);


        /// <summary>Aardvark.Base.Box2f [MIN:V2f,MAX:V2f]</summary>
        public static readonly DurablePrimitiveType Box2f = new DurablePrimitiveType<Box2f>("Box2f", 16, false);

        /// <summary>Aardvark.Base.Box3f [MIN:V3f,MAX:V3f]</summary>
        public static readonly DurablePrimitiveType Box3f = new DurablePrimitiveType<Box3f>("Box3f", 24, false);


        /// <summary>Aardvark.Base.Box2f [MIN:V2d,MAX:V2d]</summary>
        public static readonly DurablePrimitiveType Box2d = new DurablePrimitiveType<Box2d>("Box2d", 32, false);

        /// <summary>Aardvark.Base.Box3f [MIN:V3d,MAX:V3d]</summary>
        public static readonly DurablePrimitiveType Box3d = new DurablePrimitiveType<Box3d>("Box3d", 48, false);

        #endregion

        #region Vectors

        /// <summary>Aardvark.Base.V2i [X:int32,Y:int32]</summary>
        public static readonly DurablePrimitiveType V2i = new DurablePrimitiveType<V2i>("V2i", 8, false);
        /// <summary>Aardvark.Base.V2i array [X:int32,Y:int32]</summary>
        public static readonly DurablePrimitiveType V2iArray = new DurablePrimitiveType<V2i>("V2i[]", 8, true);

        /// <summary>Aardvark.Base.V3i [X:int32,Y:int32,Z:int32]</summary>
        public static readonly DurablePrimitiveType V3i = new DurablePrimitiveType<V3i>("V3i", 12, false);
        /// <summary>Aardvark.Base.V3i array [X:int32,Y:int32,Z:int32]</summary>
        public static readonly DurablePrimitiveType V3iArray = new DurablePrimitiveType<V3i>("V3i[]", 12, true);

        /// <summary>Aardvark.Base.V4i [X:int32,Y:int32,Z:int32,W:int32]</summary>
        public static readonly DurablePrimitiveType V4i = new DurablePrimitiveType<V4i>("V4i", 16, false);
        /// <summary>Aardvark.Base.V4i array [X:int32,Y:int32,Z:int32,W:int32]</summary>
        public static readonly DurablePrimitiveType V4iArray = new DurablePrimitiveType<V4i>("V4i[]", 16, true);


        /// <summary>Aardvark.Base.V2l [X:int64,Y:int64]</summary>
        public static readonly DurablePrimitiveType V2l = new DurablePrimitiveType<V2l>("V2l", 16, false);
        /// <summary>Aardvark.Base.V2l array [X:int64,Y:int64]</summary>
        public static readonly DurablePrimitiveType V2lArray = new DurablePrimitiveType<V2l>("V2l[]", 16, true);

        /// <summary>Aardvark.Base.V3l [X:int64,Y:int64,Z:int64]</summary>
        public static readonly DurablePrimitiveType V3l = new DurablePrimitiveType<V3l>("V3l", 24, false);
        /// <summary>Aardvark.Base.V3l array [X:int64,Y:int64,Z:int64]</summary>
        public static readonly DurablePrimitiveType V3lArray = new DurablePrimitiveType<V3l>("V3l[]", 24, true);

        /// <summary>Aardvark.Base.V4l [X:int64,Y:int64,Z:int64,W:int64]</summary>
        public static readonly DurablePrimitiveType V4l = new DurablePrimitiveType<V4l>("V4l", 32, false);
        /// <summary>Aardvark.Base.V4l array [X:int64,Y:int64,Z:int64,W:int64]</summary>
        public static readonly DurablePrimitiveType V4lArray = new DurablePrimitiveType<V4l>("V4l[]", 32, true);


        /// <summary>Aardvark.Base.V2f [X:float32,Y:float32]</summary>
        public static readonly DurablePrimitiveType V2f = new DurablePrimitiveType<V2f>("V2f", 8, false);
        /// <summary>Aardvark.Base.V2f array [X:float32,Y:float32]</summary>
        public static readonly DurablePrimitiveType V2fArray = new DurablePrimitiveType<V2f>("V2f[]", 8, true);

        /// <summary>Aardvark.Base.V3f [X:float32,Y:float32,Z:float32]</summary>
        public static readonly DurablePrimitiveType V3f = new DurablePrimitiveType<V3f>("V3f", 12, false);
        /// <summary>Aardvark.Base.V3f array [X:float32,Y:float32,Z:float32]</summary>
        public static readonly DurablePrimitiveType V3fArray = new DurablePrimitiveType<V3f>("V3f[]", 12, true);

        /// <summary>Aardvark.Base.V4f [X:float32,Y:float32,Z:float32,W:float32]</summary>
        public static readonly DurablePrimitiveType V4f = new DurablePrimitiveType<V4f>("V4f", 16, false);
        /// <summary>Aardvark.Base.V4f array [X:float32,Y:float32,Z:float32,W:float32]</summary>
        public static readonly DurablePrimitiveType V4fArray = new DurablePrimitiveType<V4f>("V4f[]", 16, true);


        /// <summary>Aardvark.Base.V2d [X:float64,Y:float64]</summary>
        public static readonly DurablePrimitiveType V2d = new DurablePrimitiveType<V2d>("V2d", 16, false);
        /// <summary>Aardvark.Base.V2d array [X:float64,Y:float64]</summary>
        public static readonly DurablePrimitiveType V2dArray = new DurablePrimitiveType<V2d>("V2d[]", 16, true);

        /// <summary>Aardvark.Base.V3d [X:float64,Y:float64,Z:float64]</summary>
        public static readonly DurablePrimitiveType V3d = new DurablePrimitiveType<V3d>("V3d", 24, false);
        /// <summary>Aardvark.Base.V3d array [X:float64,Y:float64,Z:float64]</summary>
        public static readonly DurablePrimitiveType V3dArray = new DurablePrimitiveType<V3d>("V3d[]", 24, true);

        /// <summary>Aardvark.Base.V4d [X:float64,Y:float64,Z:float64,W:float64]</summary>
        public static readonly DurablePrimitiveType V4d = new DurablePrimitiveType<V4d>("V4d", 32, false);
        /// <summary>Aardvark.Base.V4d array [X:float64,Y:float64,Z:float64,W:float64]</summary>
        public static readonly DurablePrimitiveType V4dArray = new DurablePrimitiveType<V4d>("V4d[]", 32, true);

        #endregion

        #region Colors

        /// <summary>Aardvark.Base.C3b [R:ubyte,G:ubyte,B:ubyte]</summary>
        public static readonly DurablePrimitiveType C3b = new DurablePrimitiveType<C3b>("C3b", 3, false);

        /// <summary>Aardvark.Base.C4b [R:ubyte,G:ubyte,B:ubyte,A:ubyte]</summary>
        public static readonly DurablePrimitiveType C4b = new DurablePrimitiveType<C4b>("C4b", 4, false);


        /// <summary>Aardvark.Base.C3f [R:float32,G:float32,B:float32]</summary>
        public static readonly DurablePrimitiveType C3f = new DurablePrimitiveType<C3f>("C3f", 12, false);

        /// <summary>Aardvark.Base.C4f [R:float32,G:float32,B:float32,A:float32]</summary>
        public static readonly DurablePrimitiveType C4f = new DurablePrimitiveType<C4f>("C4f", 16, false);

        #endregion



        /// <summary>
        /// Gets durable definition of given type.
        /// </summary>
        public static DurablePrimitiveType OfType(Type t) => s_map[t];

        /// <summary>
        /// Gets durable definition of given type.
        /// </summary>
        public static DurablePrimitiveType OfType<T>() => s_map[typeof(T)];

        private static readonly Dictionary<Type, DurablePrimitiveType> s_map = new Dictionary<Type, DurablePrimitiveType>
        {
            #region Primitives

            { typeof(DurableDataDefinition.Unit), DurablePrimitiveTypes.Unit },
            { typeof(Guid),             DurablePrimitiveTypes.Guid },
            { typeof(sbyte),            DurablePrimitiveTypes.SByte },
            { typeof(byte),             DurablePrimitiveTypes.UByte },
            { typeof(short),            DurablePrimitiveTypes.Int16 },
            { typeof(ushort),           DurablePrimitiveTypes.UInt16 },
            { typeof(int),              DurablePrimitiveTypes.Int32 },
            { typeof(uint),             DurablePrimitiveTypes.UInt32 },
            { typeof(long),             DurablePrimitiveTypes.Int64 },
            { typeof(ulong),            DurablePrimitiveTypes.UInt64 },
            { typeof(float),            DurablePrimitiveTypes.Float32 },
            { typeof(double),           DurablePrimitiveTypes.Float64 },

            { typeof(Guid[]),           DurablePrimitiveTypes.GuidArray },
            { typeof(sbyte[]),          DurablePrimitiveTypes.SByteArray },
            { typeof(byte[]),           DurablePrimitiveTypes.UByteArray },
            { typeof(short[]),          DurablePrimitiveTypes.Int16Array },
            { typeof(ushort[]),         DurablePrimitiveTypes.UInt16Array },
            { typeof(int[]),            DurablePrimitiveTypes.Int32Array },
            { typeof(uint[]),           DurablePrimitiveTypes.UInt32Array },
            { typeof(long[]),           DurablePrimitiveTypes.Int64Array },
            { typeof(ulong[]),          DurablePrimitiveTypes.UInt64Array },
            { typeof(float[]),          DurablePrimitiveTypes.Float32Array },
            { typeof(double[]),         DurablePrimitiveTypes.Float64Array },

            #endregion

            #region Ranges

            { typeof(Base.Cell),        DurablePrimitiveTypes.Cell },

            { typeof(Base.Range1sb),    DurablePrimitiveTypes.Range1sb },
            { typeof(Base.Range1b),     DurablePrimitiveTypes.Range1b },
            { typeof(Base.Range1s),     DurablePrimitiveTypes.Range1s },
            { typeof(Base.Range1us),    DurablePrimitiveTypes.Range1us },
            { typeof(Base.Range1i),     DurablePrimitiveTypes.Range1i },
            { typeof(Base.Range1ui),    DurablePrimitiveTypes.Range1ui },
            { typeof(Base.Range1l),     DurablePrimitiveTypes.Range1l },
            { typeof(Base.Range1ul),    DurablePrimitiveTypes.Range1ul },
            { typeof(Base.Range1f),     DurablePrimitiveTypes.Range1f },
            { typeof(Base.Range1d),     DurablePrimitiveTypes.Range1d },

            { typeof(Base.Box2i),       DurablePrimitiveTypes.Box2i },
            { typeof(Base.Box3i),       DurablePrimitiveTypes.Box3i },

            { typeof(Base.Box2l),       DurablePrimitiveTypes.Box2l },
            { typeof(Base.Box3l),       DurablePrimitiveTypes.Box3l },

            { typeof(Base.Box2f),       DurablePrimitiveTypes.Box2f },
            { typeof(Base.Box3f),       DurablePrimitiveTypes.Box3f },

            { typeof(Base.Box2d),       DurablePrimitiveTypes.Box2d },
            { typeof(Base.Box3d),       DurablePrimitiveTypes.Box3d },

            #endregion

            #region Vectors

            { typeof(Base.V2i),         DurablePrimitiveTypes.V2i },
            { typeof(Base.V3i),         DurablePrimitiveTypes.V3i },
            { typeof(Base.V4i),         DurablePrimitiveTypes.V4i },
            { typeof(Base.V2l),         DurablePrimitiveTypes.V2l },
            { typeof(Base.V3l),         DurablePrimitiveTypes.V3l },
            { typeof(Base.V4l),         DurablePrimitiveTypes.V4l },
            { typeof(Base.V2f),         DurablePrimitiveTypes.V2f },
            { typeof(Base.V3f),         DurablePrimitiveTypes.V3f },
            { typeof(Base.V4f),         DurablePrimitiveTypes.V4f },
            { typeof(Base.V2d),         DurablePrimitiveTypes.V2d },
            { typeof(Base.V3d),         DurablePrimitiveTypes.V3d },
            { typeof(Base.V4d),         DurablePrimitiveTypes.V4d },

            { typeof(Base.V2i[]),       DurablePrimitiveTypes.V2iArray },
            { typeof(Base.V3i[]),       DurablePrimitiveTypes.V3iArray },
            { typeof(Base.V4i[]),       DurablePrimitiveTypes.V4iArray },
            { typeof(Base.V2l[]),       DurablePrimitiveTypes.V2lArray },
            { typeof(Base.V3l[]),       DurablePrimitiveTypes.V3lArray },
            { typeof(Base.V4l[]),       DurablePrimitiveTypes.V4lArray },
            { typeof(Base.V2f[]),       DurablePrimitiveTypes.V2fArray },
            { typeof(Base.V3f[]),       DurablePrimitiveTypes.V3fArray },
            { typeof(Base.V4f[]),       DurablePrimitiveTypes.V4fArray },
            { typeof(Base.V2d[]),       DurablePrimitiveTypes.V2dArray },
            { typeof(Base.V3d[]),       DurablePrimitiveTypes.V3dArray },
            { typeof(Base.V4d[]),       DurablePrimitiveTypes.V4dArray },

            #endregion

            #region Colors

            { typeof(Base.C3b),         DurablePrimitiveTypes.C3b },
            { typeof(Base.C4b),         DurablePrimitiveTypes.C4b },

            { typeof(Base.C3f),         DurablePrimitiveTypes.C3f },
            { typeof(Base.C4f),         DurablePrimitiveTypes.C4f },

            #endregion
        };
    }
}
