using Aardvark.Base;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Aardvark.Data
{
    /// <summary>
    /// </summary>
    public static class Codec
    {
        private static readonly Dictionary<Guid, object> s_encoders;
        private static readonly Dictionary<Guid, object> s_decoders;

        static Codec()
        {
            // force Durable.Octree initializer
            if (Durable.Octree.NodeId == null) throw new InvalidOperationException("Invariant 98c78cd6-cef2-4f0b-bb8e-907064c305c4.");

            s_encoders = new Dictionary<Guid, object>
            {
                [Durable.Primitives.GuidDef.Id] = EncodeGuid,
                [Durable.Primitives.GuidArray.Id] = EncodeGuidArray,

                [Durable.Primitives.Int16.Id] = EncodeInt16,
                [Durable.Primitives.Int16Array.Id] = EncodeInt16Array,
                [Durable.Primitives.UInt16.Id] = EncodeUInt16,
                [Durable.Primitives.UInt16Array.Id] = EncodeUInt16Array,
                [Durable.Primitives.Int32.Id] = EncodeInt32,
                [Durable.Primitives.Int32Array.Id] = EncodeInt32Array,
                [Durable.Primitives.UInt32.Id] = EncodeUInt32,
                [Durable.Primitives.UInt32Array.Id] = EncodeUInt32Array,
                [Durable.Primitives.Int64.Id] = EncodeInt64,
                [Durable.Primitives.Int64Array.Id] = EncodeInt64Array,
                [Durable.Primitives.UInt64.Id] = EncodeUInt64,
                [Durable.Primitives.UInt64Array.Id] = EncodeUInt64Array,
                [Durable.Primitives.Float32.Id] = EncodeFloat32,
                [Durable.Primitives.Float32Array.Id] = EncodeFloat32Array,
                [Durable.Primitives.Float64.Id] = EncodeFloat64,
                [Durable.Primitives.Float64Array.Id] = EncodeFloat64Array,
                [Durable.Primitives.StringUTF8.Id] = EncodeStringUtf8,
                [Durable.Primitives.DurableMap.Id] = EncodeDurableMapWithoutHeader,

                [Durable.Aardvark.Cell.Id] = EncodeCell,
                [Durable.Aardvark.CellArray.Id] = EncodeCellArray,
                [Durable.Aardvark.V2f.Id] = EncodeV2f,
                [Durable.Aardvark.V2fArray.Id] = EncodeV2fArray,
                [Durable.Aardvark.V3f.Id] = EncodeV3f,
                [Durable.Aardvark.V3fArray.Id] = EncodeV3fArray,
                [Durable.Aardvark.V4f.Id] = EncodeV4f,
                [Durable.Aardvark.V4fArray.Id] = EncodeV4fArray,
                [Durable.Aardvark.V2d.Id] = EncodeV2d,
                [Durable.Aardvark.V2dArray.Id] = EncodeV2dArray,
                [Durable.Aardvark.V3d.Id] = EncodeV3d,
                [Durable.Aardvark.V3dArray.Id] = EncodeV3dArray,
                [Durable.Aardvark.V4d.Id] = EncodeV4d,
                [Durable.Aardvark.V4dArray.Id] = EncodeV4dArray,
                [Durable.Aardvark.Box2f.Id] = EncodeBox2f,
                [Durable.Aardvark.Box2fArray.Id] = EncodeBox2fArray,
                [Durable.Aardvark.Box2d.Id] = EncodeBox2d,
                [Durable.Aardvark.Box2dArray.Id] = EncodeBox2dArray,
                [Durable.Aardvark.Box3f.Id] = EncodeBox3f,
                [Durable.Aardvark.Box3fArray.Id] = EncodeBox3fArray,
                [Durable.Aardvark.Box3d.Id] = EncodeBox3d,
                [Durable.Aardvark.Box3dArray.Id] = EncodeBox3dArray,

                [Durable.Aardvark.C3b.Id] = EncodeC3b,
                [Durable.Aardvark.C3bArray.Id] = EncodeC3bArray,
            };

            s_decoders = new Dictionary<Guid, object>
            {
                [Durable.Primitives.GuidDef.Id] = DecodeGuid,
                [Durable.Primitives.GuidArray.Id] = DecodeGuidArray,
                [Durable.Primitives.Int16.Id] = DecodeInt16,
                [Durable.Primitives.Int16Array.Id] = DecodeInt16Array,
                [Durable.Primitives.UInt16.Id] = DecodeUInt16,
                [Durable.Primitives.UInt16Array.Id] = DecodeUInt16Array,
                [Durable.Primitives.Int32.Id] = DecodeInt32,
                [Durable.Primitives.Int32Array.Id] = DecodeInt32Array,
                [Durable.Primitives.UInt32.Id] = DecodeUInt32,
                [Durable.Primitives.UInt32Array.Id] = DecodeUInt32Array,
                [Durable.Primitives.Int64.Id] = DecodeInt64,
                [Durable.Primitives.Int64Array.Id] = DecodeInt64Array,
                [Durable.Primitives.UInt64.Id] = DecodeUInt64,
                [Durable.Primitives.UInt64Array.Id] = DecodeUInt64Array,
                [Durable.Primitives.Float32.Id] = DecodeFloat32,
                [Durable.Primitives.Float32Array.Id] = DecodeFloat32Array,
                [Durable.Primitives.Float64.Id] = DecodeFloat64,
                [Durable.Primitives.Float64Array.Id] = DecodeFloat64Array,
                [Durable.Primitives.StringUTF8.Id] = DecodeStringUtf8,
                [Durable.Primitives.DurableMap.Id] = DecodeDurableMapWithoutHeader,

                [Durable.Aardvark.Cell.Id] = DecodeCell,
                [Durable.Aardvark.CellArray.Id] = DecodeCellArray,
                [Durable.Aardvark.V2f.Id] = DecodeV2f,
                [Durable.Aardvark.V2fArray.Id] = DecodeV2fArray,
                [Durable.Aardvark.V3f.Id] = DecodeV3f,
                [Durable.Aardvark.V3fArray.Id] = DecodeV3fArray,
                [Durable.Aardvark.V4f.Id] = DecodeV4f,
                [Durable.Aardvark.V4fArray.Id] = DecodeV4fArray,
                [Durable.Aardvark.V2d.Id] = DecodeV2d,
                [Durable.Aardvark.V2dArray.Id] = DecodeV2dArray,
                [Durable.Aardvark.V3d.Id] = DecodeV3d,
                [Durable.Aardvark.V3dArray.Id] = DecodeV3dArray,
                [Durable.Aardvark.V4d.Id] = DecodeV4d,
                [Durable.Aardvark.V4dArray.Id] = DecodeV4dArray,
                [Durable.Aardvark.Box2f.Id] = DecodeBox2f,
                [Durable.Aardvark.Box2fArray.Id] = DecodeBox2fArray,
                [Durable.Aardvark.Box2d.Id] = DecodeBox2d,
                [Durable.Aardvark.Box2dArray.Id] = DecodeBox2dArray,
                [Durable.Aardvark.Box3f.Id] = DecodeBox3f,
                [Durable.Aardvark.Box3fArray.Id] = DecodeBox3fArray,
                [Durable.Aardvark.Box3d.Id] = DecodeBox3d,
                [Durable.Aardvark.Box3dArray.Id] = DecodeBox3dArray,

                [Durable.Aardvark.C3b.Id] = DecodeC3b,
                [Durable.Aardvark.C3bArray.Id] = DecodeC3bArray,
            };
        }

        #region Encode

        private static readonly Action<BinaryWriter, object> EncodeGuid = (s, o) => s.Write(((Guid)o).ToByteArray(), 0, 16);
        private static readonly Action<BinaryWriter, object> EncodeGuidArray = (s, o) => EncodeArray(s, (Guid[])o);
        private static readonly Action<BinaryWriter, object> EncodeInt16 = (s, o) => s.Write((short)o);
        private static readonly Action<BinaryWriter, object> EncodeInt16Array = (s, o) => EncodeArray(s, (short[])o);
        private static readonly Action<BinaryWriter, object> EncodeUInt16 = (s, o) => s.Write((ushort)o);
        private static readonly Action<BinaryWriter, object> EncodeUInt16Array = (s, o) => EncodeArray(s, (ushort[])o);
        private static readonly Action<BinaryWriter, object> EncodeInt32 = (s, o) => s.Write((int)o);
        private static readonly Action<BinaryWriter, object> EncodeInt32Array = (s, o) => EncodeArray(s, (int[])o);
        private static readonly Action<BinaryWriter, object> EncodeUInt32 = (s, o) => s.Write((uint)o);
        private static readonly Action<BinaryWriter, object> EncodeUInt32Array = (s, o) => EncodeArray(s, (uint[])o);
        private static readonly Action<BinaryWriter, object> EncodeInt64 = (s, o) => s.Write((long)o);
        private static readonly Action<BinaryWriter, object> EncodeInt64Array = (s, o) => EncodeArray(s, (long[])o);
        private static readonly Action<BinaryWriter, object> EncodeUInt64 = (s, o) => s.Write((ulong)o);
        private static readonly Action<BinaryWriter, object> EncodeUInt64Array = (s, o) => EncodeArray(s, (ulong[])o);
        private static readonly Action<BinaryWriter, object> EncodeFloat32 = (s, o) => s.Write((float)o);
        private static readonly Action<BinaryWriter, object> EncodeFloat32Array = (s, o) => EncodeArray(s, (float[])o);
        private static readonly Action<BinaryWriter, object> EncodeFloat64 = (s, o) => s.Write((double)o);
        private static readonly Action<BinaryWriter, object> EncodeFloat64Array = (s, o) => EncodeArray(s, (double[])o);
        private static readonly Action<BinaryWriter, object> EncodeStringUtf8 = (s, o) => EncodeArray(s, Encoding.UTF8.GetBytes((string)o));

        private static readonly Action<BinaryWriter, object> EncodeDurableMapWithoutHeader =
            (s, o) =>
            {
                var xs = (IEnumerable<KeyValuePair<Durable.Def, object>>)o;
                var count = xs.Count();
                s.Write(count);
                foreach (var x in xs) Encode(s, x.Key, x.Value);
            };

        private static readonly Action<BinaryWriter, object> EncodeCell =
            (s, o) => { var x = (Cell)o; s.Write(x.X); s.Write(x.Y); s.Write(x.Z); s.Write(x.Exponent); };
        private static readonly Action<BinaryWriter, object> EncodeCellArray =
            (s, o) => EncodeArray(s, (Cell[])o);


        private static readonly Action<BinaryWriter, object> EncodeV2f =
            (s, o) => { var x = (V2f)o; s.Write(x.X); s.Write(x.Y); };
        private static readonly Action<BinaryWriter, object> EncodeV2fArray =
            (s, o) => EncodeArray(s, (V2f[])o);

        private static readonly Action<BinaryWriter, object> EncodeV3f =
            (s, o) => { var x = (V3f)o; s.Write(x.X); s.Write(x.Y); s.Write(x.Z); };
        private static readonly Action<BinaryWriter, object> EncodeV3fArray =
            (s, o) => EncodeArray(s, (V3f[])o);

        private static readonly Action<BinaryWriter, object> EncodeV4f =
            (s, o) => { var x = (V4f)o; s.Write(x.X); s.Write(x.Y); s.Write(x.Z); s.Write(x.W); };
        private static readonly Action<BinaryWriter, object> EncodeV4fArray =
            (s, o) => EncodeArray(s, (V4f[])o);


        private static readonly Action<BinaryWriter, object> EncodeV2d =
            (s, o) => { var x = (V2d)o; s.Write(x.X); s.Write(x.Y); };
        private static readonly Action<BinaryWriter, object> EncodeV2dArray =
            (s, o) => EncodeArray(s, (V2d[])o);

        private static readonly Action<BinaryWriter, object> EncodeV3d =
            (s, o) => { var x = (V3d)o; s.Write(x.X); s.Write(x.Y); s.Write(x.Z); };
        private static readonly Action<BinaryWriter, object> EncodeV3dArray =
            (s, o) => EncodeArray(s, (V3d[])o);

        private static readonly Action<BinaryWriter, object> EncodeV4d =
            (s, o) => { var x = (V4d)o; s.Write(x.X); s.Write(x.Y); s.Write(x.Z); s.Write(x.W); };
        private static readonly Action<BinaryWriter, object> EncodeV4dArray =
            (s, o) => EncodeArray(s, (V4d[])o);


        private static readonly Action<BinaryWriter, object> EncodeBox2f =
            (s, o) => { var x = (Box2f)o; EncodeV2f(s, x.Min); EncodeV2f(s, x.Max); };
        private static readonly Action<BinaryWriter, object> EncodeBox2fArray =
            (s, o) => EncodeArray(s, (Box2f[])o);

        private static readonly Action<BinaryWriter, object> EncodeBox2d =
            (s, o) => { var x = (Box2d)o; EncodeV2d(s, x.Min); EncodeV2d(s, x.Max); };
        private static readonly Action<BinaryWriter, object> EncodeBox2dArray =
            (s, o) => EncodeArray(s, (Box2d[])o);


        private static readonly Action<BinaryWriter, object> EncodeBox3f =
            (s, o) => { var x = (Box3f)o; EncodeV3f(s, x.Min); EncodeV3f(s, x.Max); };
        private static readonly Action<BinaryWriter, object> EncodeBox3fArray =
            (s, o) => EncodeArray(s, (Box3f[])o);

        private static readonly Action<BinaryWriter, object> EncodeBox3d =
            (s, o) => { var x = (Box3d)o; EncodeV3d(s, x.Min); EncodeV3d(s, x.Max); };
        private static readonly Action<BinaryWriter, object> EncodeBox3dArray =
            (s, o) => EncodeArray(s, (Box3d[])o);

        private static readonly Action<BinaryWriter, object> EncodeC3b =
            (s, o) => { var x = (C3b)o; s.Write(x.R); s.Write(x.G); s.Write(x.B); };
        private static readonly Action<BinaryWriter, object> EncodeC3bArray =
            (s, o) => EncodeArray(s, (C3b[])o);

        private static unsafe void EncodeArray<T>(BinaryWriter s, params T[] xs) where T : struct
        {
            var gc = GCHandle.Alloc(xs, GCHandleType.Pinned);
            var size = xs.Length * Marshal.SizeOf<T>();
            var dst = new byte[size];
            try
            {
                Marshal.Copy(gc.AddrOfPinnedObject(), dst, 0, size);
                s.Write(xs.Length);
                s.Write(dst);
            }
            finally
            {
                gc.Free();
            }
        }

        private static void Encode(BinaryWriter stream, Durable.Def def, object x)
        {
            if (def.Type != Durable.Primitives.Unit.Id)
            {
                if (s_encoders.TryGetValue(def.Type, out var encoder))
                {
                    EncodeGuid(stream, def.Id);
                    ((Action<BinaryWriter, object>)encoder)(stream, x);
                }
                else
                {
                    var unknownDef = Durable.Get(def.Type);
                    throw new InvalidOperationException($"Unknown definition {unknownDef}.");
                }
            }
            else
            {
                if (s_encoders.TryGetValue(def.Id, out var encoder))
                {
                    ((Action<BinaryWriter, object>)encoder)(stream, x);
                }
                else
                {
                    var unknownDef = Durable.Get(def.Id);
                    throw new InvalidOperationException($"Unknown definition {unknownDef}.");
                }
            }
        }

        #endregion

        #region Decode

        private static readonly Func<BinaryReader, object> DecodeGuid = s => new Guid(s.ReadBytes(16));
        private static readonly Func<BinaryReader, object> DecodeStringUtf8 = s => Encoding.UTF8.GetString(DecodeArray<byte>(s));

        private static readonly Func<BinaryReader, object> DecodeInt16 = s => s.ReadInt16();
        private static readonly Func<BinaryReader, object> DecodeInt16Array = s => DecodeArray<short>(s);

        private static readonly Func<BinaryReader, object> DecodeUInt16 = s => s.ReadUInt16();
        private static readonly Func<BinaryReader, object> DecodeUInt16Array = s => DecodeArray<ushort>(s);

        private static readonly Func<BinaryReader, object> DecodeInt32 = s => s.ReadInt32();
        private static readonly Func<BinaryReader, object> DecodeInt32Array = s => DecodeArray<int>(s);

        private static readonly Func<BinaryReader, object> DecodeUInt32 = s => s.ReadUInt32();
        private static readonly Func<BinaryReader, object> DecodeUInt32Array = s => DecodeArray<uint>(s);

        private static readonly Func<BinaryReader, object> DecodeInt64 = s => s.ReadInt64();
        private static readonly Func<BinaryReader, object> DecodeInt64Array = s => DecodeArray<long>(s);

        private static readonly Func<BinaryReader, object> DecodeUInt64 = s => s.ReadUInt64();
        private static readonly Func<BinaryReader, object> DecodeUInt64Array = s => DecodeArray<ulong>(s);

        private static readonly Func<BinaryReader, object> DecodeFloat32 = s => s.ReadSingle();
        private static readonly Func<BinaryReader, object> DecodeFloat32Array = s => DecodeArray<float>(s);

        private static readonly Func<BinaryReader, object> DecodeFloat64 = s => s.ReadDouble();
        private static readonly Func<BinaryReader, object> DecodeFloat64Array = s => DecodeArray<double>(s);

        private static readonly Func<BinaryReader, object> DecodeDurableMapWithoutHeader =
            s =>
            {
                var count = s.ReadInt32();
                var entries = new KeyValuePair<Durable.Def, object>[count];
                for (var i = 0; i < count; i++)
                {
                    var e = Decode(s);
                    entries[i] = new KeyValuePair<Durable.Def, object>(e.Item1, e.Item2);
                }
                return ImmutableDictionary.CreateRange(entries);
            };

        private static readonly Func<BinaryReader, object> DecodeCell = s => new Cell(s.ReadInt64(), s.ReadInt64(), s.ReadInt64(), s.ReadInt32());
        private static readonly Func<BinaryReader, object> DecodeCellArray = s => DecodeArray<Cell>(s);

        private static readonly Func<BinaryReader, object> DecodeV2f = s => new V2f(s.ReadSingle(), s.ReadSingle());
        private static readonly Func<BinaryReader, object> DecodeV2fArray = s => DecodeArray<V2f>(s);
        private static readonly Func<BinaryReader, object> DecodeV3f = s => new V3f(s.ReadSingle(), s.ReadSingle(), s.ReadSingle());
        private static readonly Func<BinaryReader, object> DecodeV3fArray = s => DecodeArray<V3f>(s);
        private static readonly Func<BinaryReader, object> DecodeV4f = s => new V4f(s.ReadSingle(), s.ReadSingle(), s.ReadSingle(), s.ReadSingle());
        private static readonly Func<BinaryReader, object> DecodeV4fArray = s => DecodeArray<V4f>(s);

        private static readonly Func<BinaryReader, object> DecodeV2d = s => new V2d(s.ReadDouble(), s.ReadDouble());
        private static readonly Func<BinaryReader, object> DecodeV2dArray = s => DecodeArray<V2d>(s);
        private static readonly Func<BinaryReader, object> DecodeV3d = s => new V3d(s.ReadDouble(), s.ReadDouble(), s.ReadDouble());
        private static readonly Func<BinaryReader, object> DecodeV3dArray = s => DecodeArray<V3d>(s);
        private static readonly Func<BinaryReader, object> DecodeV4d = s => new V4d(s.ReadDouble(), s.ReadDouble(), s.ReadDouble(), s.ReadDouble());
        private static readonly Func<BinaryReader, object> DecodeV4dArray = s => DecodeArray<V4d>(s);

        private static readonly Func<BinaryReader, object> DecodeBox2f = s => new Box2f((V2f)DecodeV2f(s), (V2f)DecodeV2f(s));
        private static readonly Func<BinaryReader, object> DecodeBox2fArray = s => DecodeArray<Box2f>(s);
        private static readonly Func<BinaryReader, object> DecodeBox2d = s => new Box2d((V2d)DecodeV2d(s), (V2d)DecodeV2d(s));
        private static readonly Func<BinaryReader, object> DecodeBox2dArray = s => DecodeArray<Box2d>(s);

        private static readonly Func<BinaryReader, object> DecodeBox3f = s => new Box3f((V3f)DecodeV3f(s), (V3f)DecodeV3f(s));
        private static readonly Func<BinaryReader, object> DecodeBox3fArray = s => DecodeArray<Box3f>(s);
        private static readonly Func<BinaryReader, object> DecodeBox3d = s => new Box3d((V3d)DecodeV3d(s), (V3d)DecodeV3d(s));
        private static readonly Func<BinaryReader, object> DecodeBox3dArray = s => DecodeArray<Box3d>(s);

        private static readonly Func<BinaryReader, object> DecodeC3b = s => new C3b(s.ReadByte(), s.ReadByte(), s.ReadByte());
        private static readonly Func<BinaryReader, object> DecodeC3bArray = s => DecodeArray<C3b>(s);

        private static unsafe T[] DecodeArray<T>(BinaryReader s) where T : struct
        {
            var count = s.ReadInt32();
            var size = count * Marshal.SizeOf<T>();
            var buffer = s.ReadBytes(size);
            var xs = new T[count];
            var gc = GCHandle.Alloc(xs, GCHandleType.Pinned);
            try
            {
                Marshal.Copy(buffer, 0, gc.AddrOfPinnedObject(), size);
                return xs;
            }
            finally
            {
                gc.Free();
            }
        }

        private static readonly Func<BinaryReader, object> DecodeGuidArray = s => DecodeArray<Guid>(s);

        private static (Durable.Def, object) Decode(BinaryReader stream)
        {
            var key = (Guid)DecodeGuid(stream);
            if (!Durable.TryGet(key, out var def))
            {
                stream.BaseStream.Position -= 16;
                def = Durable.Get(Durable.Primitives.DurableMap.Id);
            }

            if (def.Type != Durable.Primitives.Unit.Id)
            {
                if (s_decoders.TryGetValue(def.Type, out var decoder))
                {
                    var o = ((Func<BinaryReader, object>)decoder)(stream);
                    return (def, o);
                }
                else
                {
                    var unknownDef = Durable.Get(def.Type);
                    throw new InvalidOperationException($"Unknown definition {unknownDef}.");
                }
            }
            else
            {
                if (s_decoders.TryGetValue(def.Id, out var decoder))
                {
                    var o = ((Func<BinaryReader, object>)decoder)(stream);
                    return (def, o);
                }
                else
                {
                    var unknownDef = Durable.Get(def.Id);
                    throw new InvalidOperationException($"Unknown definition {unknownDef}.");
                }
            }
        }

        #endregion

        #region Serialization

        /// <summary>
        /// Serializes value x to byte array. 
        /// Can be deserialized with Deserialize.
        /// </summary>
        public static byte[] Serialize<T>(Durable.Def def, T x)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            if (def.Type == Durable.Primitives.Unit.Id)
            {
                // encode type of primitive value, so we can roundtrip with Deserialize
                // (since it is not encoded by the Encode function called below)
                EncodeGuid(bw, def.Id);
            }

            Encode(bw, def, x);
            bw.Flush();
            return ms.ToArray();
        }

        /// <summary>
        /// Serializes value x to stream. 
        /// Can be deserialized with Deserialize.
        /// </summary>
        public static void Serialize<T>(BinaryWriter stream, Durable.Def def, T x)
        {
            if (def.Type == Durable.Primitives.Unit.Id)
            {
                // encode type of primitive value, so we can roundtrip with Deserialize
                // (since it is not encoded by the Encode function called below)
                EncodeGuid(stream, def.Id);
            }

            Encode(stream, def, x);
        }

        /// <summary>
        /// Serializes value x to stream. 
        /// Can be deserialized with Deserialize.
        /// </summary>
        public static void Serialize<T>(Stream stream, Durable.Def def, T x)
        {
            using var bw = new BinaryWriter(stream);
            Serialize(bw, def, x);
        }


        /// <summary>
        /// Deserializes value from stream.
        /// </summary>
        public static (Durable.Def, object) Deserialize(BinaryReader stream)
            => Decode(stream);

        /// <summary>
        /// Deserializes value from stream.
        /// </summary>
        public static (Durable.Def, object) Deserialize(Stream stream)
        {
            using var br = new BinaryReader(stream);
            return Decode(br);
        }

        /// <summary>
        /// Deserializes value from byte array.
        /// </summary>
        public static (Durable.Def, object) Deserialize(byte[] buffer)
        {
            using var ms = new MemoryStream(buffer);
            using var br = new BinaryReader(ms);
            return Decode(br);
        }

        /// <summary>
        /// Deserializes value from file.
        /// </summary>
        public static (Durable.Def, object) Deserialize(string filename)
        {
            using var ms = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(ms);
            return Decode(br);
        }



        /// <summary>
        /// Deserializes value from stream.
        /// </summary>
        public static T DeserializeAs<T>(BinaryReader stream) => (T)Deserialize(stream).Item2;

        /// <summary>
        /// Deserializes value from stream.
        /// </summary>
        public static T DeserializeAs<T>(Stream stream) => (T)Deserialize(stream).Item2;

        /// <summary>
        /// Deserializes value from byte array.
        /// </summary>
        public static T DeserializeAs<T>(byte[] buffer) => (T)Deserialize(buffer).Item2;

        /// <summary>
        /// Deserializes value from file.
        /// </summary>
        public static T DeserializeAs<T>(string filename) => (T)Deserialize(filename).Item2;

        #endregion
    }
}
