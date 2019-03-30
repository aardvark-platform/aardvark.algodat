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
    /// Basic durable data types (language independent).
    /// Numeric values are little endian, unless otherwise specified.
    /// </summary>
    public static class DurableTypes
    {
        private static readonly Dictionary<Type, string> m_map = new Dictionary<Type, string>
        {
            #region Simple

            { typeof(DurableData.Unit), DurableTypes.Unit },
            { typeof(sbyte),            DurableTypes.SByte },
            { typeof(byte),             DurableTypes.UByte },
            { typeof(short),            DurableTypes.Int16 },
            { typeof(ushort),           DurableTypes.UInt16 },
            { typeof(int),              DurableTypes.Int32 },
            { typeof(uint),             DurableTypes.UInt32 },
            { typeof(long),             DurableTypes.Int64 },
            { typeof(ulong),            DurableTypes.UInt64 },
            { typeof(float),            DurableTypes.Float32 },
            { typeof(double),           DurableTypes.Float64 },

            #endregion

            #region Ranges

            { typeof(Base.Cell),        DurableTypes.Cell },

            { typeof(Base.Range1sb),    DurableTypes.Range1sb },
            { typeof(Base.Range1b),     DurableTypes.Range1b },
            { typeof(Base.Range1s),     DurableTypes.Range1s },
            { typeof(Base.Range1us),    DurableTypes.Range1us },
            { typeof(Base.Range1i),     DurableTypes.Range1i },
            { typeof(Base.Range1ui),    DurableTypes.Range1ui },
            { typeof(Base.Range1l),     DurableTypes.Range1l },
            { typeof(Base.Range1ul),    DurableTypes.Range1ul },

            { typeof(Base.Box2f),       DurableTypes.Box2f },
            { typeof(Base.Box3f),       DurableTypes.Box3f },

            { typeof(Base.Box2d),       DurableTypes.Box2d },
            { typeof(Base.Box3d),       DurableTypes.Box2d },

            #endregion

            #region Vectors

            { typeof(Base.V2i),         DurableTypes.V2i },
            { typeof(Base.V3i),         DurableTypes.V3i },
            { typeof(Base.V4i),         DurableTypes.V4i },

            { typeof(Base.V2l),         DurableTypes.V2l },
            { typeof(Base.V3l),         DurableTypes.V3l },
            { typeof(Base.V4l),         DurableTypes.V4l },

            { typeof(Base.V2f),         DurableTypes.V2f },
            { typeof(Base.V3f),         DurableTypes.V3f },
            { typeof(Base.V4f),         DurableTypes.V4f },

            { typeof(Base.V2d),         DurableTypes.V2d },
            { typeof(Base.V3d),         DurableTypes.V3d },
            { typeof(Base.V4d),         DurableTypes.V4d },

            #endregion

            #region Colors

            { typeof(Base.C3b),         DurableTypes.C3b },
            { typeof(Base.C4b),         DurableTypes.C4b },

            { typeof(Base.C3f),         DurableTypes.C3f },
            { typeof(Base.C4f),         DurableTypes.C4f },

            #endregion
        };

        /// <summary></summary>
        public static string OfType(Type t) => m_map[t];

        #region Simple

        /// <summary>
        /// Unit.
        /// </summary>
        public const string Unit = "unit";


        /// <summary>
        /// Signed 8-bit integer. 2-complement.
        /// </summary>
        public const string SByte = "sbyte";

        /// <summary>
        /// Unsigned 8-bit integer.
        /// </summary>
        public const string UByte = "ubyte";


        /// <summary>
        /// Signed 16-bit integer. 2-complement.
        /// </summary>
        public const string Int16 = "int16";

        /// <summary>
        /// Unsigned 16-bit integer.
        /// </summary>
        public const string UInt16 = "uint16";


        /// <summary>
        /// Signed 32-bit integer. 2-complement.
        /// </summary>
        public const string Int32 = "int32";

        /// <summary>
        /// Unsigned 32-bit integer.
        /// </summary>
        public const string UInt32 = "uint32";


        /// <summary>
        /// Signed 64-bit integer. 2-complement.
        /// </summary>
        public const string Int64 = "int64";

        /// <summary>
        /// Unsigned 64-bit integer.
        /// </summary>
        public const string UInt64 = "uint64";


        /// <summary>
        /// Floating point value (32-bit).
        /// </summary>
        public const string Float32 = "float32";

        /// <summary>
        /// Floating point value (64-bit).
        /// </summary>
        public const string Float64 = "float64";

        #endregion

        #region Ranges

        /// <summary>Aardvark.Base.Cell [X:int32,Y:int32,Z:int32,E:int64]</summary>
        public const string Cell = "Cell";

        /// <summary>Aardvark.Base.Range1sb [MIN:sbyte,MAX:sbyte]</summary>
        public const string Range1sb = "Range1sb";

        /// <summary>Aardvark.Base.Range1b [MIN:ubyte,MAX:ubyte]</summary>
        public const string Range1b = "Range1b";

        /// <summary>Aardvark.Base.Range1sb [MIN:int16,MAX:int16]</summary>
        public const string Range1s = "Range1s";

        /// <summary>Aardvark.Base.Range1s [MIN:uint16,MAX:uint16]</summary>
        public const string Range1us = "Range1us";

        /// <summary>Aardvark.Base.Range1sb [MIN:int32,MAX:int32]</summary>
        public const string Range1i = "Range1i";

        /// <summary>Aardvark.Base.Range1s [MIN:uint32,MAX:uint32]</summary>
        public const string Range1ui = "Range1ui";

        /// <summary>Aardvark.Base.Range1sb [MIN:int64,MAX:int64]</summary>
        public const string Range1l = "Range1l";

        /// <summary>Aardvark.Base.Range1s [MIN:uint64,MAX:uint64]</summary>
        public const string Range1ul = "Range1ul";


        /// <summary>Aardvark.Base.Box2f [MIN:V2f,MAX:V2f]</summary>
        public const string Box2f = "Box2f";

        /// <summary>Aardvark.Base.Box3f [MIN:V3f,MAX:V3f]</summary>
        public const string Box3f = "Box3f";


        /// <summary>Aardvark.Base.Box2f [MIN:V2d,MAX:V2d]</summary>
        public const string Box2d = "Box2d";

        /// <summary>Aardvark.Base.Box3f [MIN:V3d,MAX:V3d]</summary>
        public const string Box3d = "Box3d";

        #endregion

        #region Vectors

        /// <summary>Aardvark.Base.V2i [X:int32,Y:int32]</summary>
        public const string V2i = "V2i";

        /// <summary>Aardvark.Base.V3i [X:int32,Y:int32,Z:int32]</summary>
        public const string V3i = "V3i";

        /// <summary>Aardvark.Base.V4i [X:int32,Y:int32,Z:int32,W:int32]</summary>
        public const string V4i = "V4i";


        /// <summary>Aardvark.Base.V2l [X:int64,Y:int64]</summary>
        public const string V2l = "V2l";

        /// <summary>Aardvark.Base.V3l [X:int64,Y:int64,Z:int64]</summary>
        public const string V3l = "V3l";

        /// <summary>Aardvark.Base.V4l [X:int64,Y:int64,Z:int64,W:int64]</summary>
        public const string V4l = "V4l";


        /// <summary>Aardvark.Base.V2f [X:float32,Y:float32]</summary>
        public const string V2f = "V2f";

        /// <summary>Aardvark.Base.V3f [X:float32,Y:float32,Z:float32]</summary>
        public const string V3f = "V3f";

        /// <summary>Aardvark.Base.V4f [X:float32,Y:float32,Z:float32,W:float32]</summary>
        public const string V4f = "V4f";


        /// <summary>Aardvark.Base.V2d [X:float64,Y:float64]</summary>
        public const string V2d = "V2d";

        /// <summary>Aardvark.Base.V3d [X:float64,Y:float64,Z:float64]</summary>
        public const string V3d = "V3d";

        /// <summary>Aardvark.Base.V4d [X:float64,Y:float64,Z:float64,W:float64]</summary>
        public const string V4d = "V4d";

        #endregion

        #region Colors

        /// <summary>Aardvark.Base.C3b [R:ubyte,G:ubyte,B:ubyte]</summary>
        public const string C3b = "C3b";

        /// <summary>Aardvark.Base.C4b [R:ubyte,G:ubyte,B:ubyte,A:ubyte]</summary>
        public const string C4b = "C4b";


        /// <summary>Aardvark.Base.C3f [R:float32,G:float32,B:float32]</summary>
        public const string C3f = "C3f";

        /// <summary>Aardvark.Base.C4f [R:float32,G:float32,B:float32,A:float32]</summary>
        public const string C4f = "C4f";

        #endregion
    }
}
