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
            s_encoders = new Dictionary<Guid, object>
            {
                [Durable.Primitives.GuidDef.Id] = EncodeGuid,
                [Durable.Primitives.Int16.Id] = EncodeInt16,
                [Durable.Primitives.UInt16.Id] = EncodeUInt16,
                [Durable.Primitives.Int32.Id] = EncodeInt32,
                [Durable.Primitives.UInt32.Id] = EncodeUInt32,
                [Durable.Primitives.Int64.Id] = EncodeInt64,
                [Durable.Primitives.UInt64.Id] = EncodeUInt64,
                [Durable.Primitives.Float32.Id] = EncodeFloat32,
                [Durable.Primitives.Float64.Id] = EncodeFloat64,
                [Durable.Primitives.StringUTF8.Id] = EncodeStringUtf8,
                [Durable.Primitives.DurableMap.Id] = EncodeDurableMap,

                [Durable.Aardvark.Cell.Id] = EncodeCell,
                [Durable.Aardvark.V2f.Id] = EncodeV2f,
                [Durable.Aardvark.V3f.Id] = EncodeV3f,
                [Durable.Aardvark.V4f.Id] = EncodeV4f,
                [Durable.Aardvark.V2d.Id] = EncodeV2d,
                [Durable.Aardvark.V3d.Id] = EncodeV3d,
                [Durable.Aardvark.V4d.Id] = EncodeV4d,
                [Durable.Aardvark.Box2f.Id] = EncodeBox2f,
                [Durable.Aardvark.Box2d.Id] = EncodeBox2d,
                [Durable.Aardvark.Box3f.Id] = EncodeBox3f,
                [Durable.Aardvark.Box3d.Id] = EncodeBox3d,

                [Durable.Primitives.GuidArray.Id] = EncodeGuidArray,
            };

            s_decoders = new Dictionary<Guid, object>
            {
                [Durable.Primitives.GuidDef.Id] = DecodeGuid,
                [Durable.Primitives.Int16.Id] = DecodeInt16,
                [Durable.Primitives.UInt16.Id] = DecodeUInt16,
                [Durable.Primitives.Int32.Id] = DecodeInt32,
                [Durable.Primitives.UInt32.Id] = DecodeUInt32,
                [Durable.Primitives.Int64.Id] = DecodeInt64,
                [Durable.Primitives.UInt64.Id] = DecodeUInt64,
                [Durable.Primitives.Float32.Id] = DecodeFloat32,
                [Durable.Primitives.Float64.Id] = DecodeFloat64,
                [Durable.Primitives.StringUTF8.Id] = DecodeStringUtf8,
                [Durable.Primitives.DurableMap.Id] = DecodeDurableMap,

                [Durable.Aardvark.Cell.Id] = DecodeCell,
                [Durable.Aardvark.V2f.Id] = DecodeV2f,
                [Durable.Aardvark.V3f.Id] = DecodeV3f,
                [Durable.Aardvark.V4f.Id] = DecodeV4f,
                [Durable.Aardvark.V2d.Id] = DecodeV2d,
                [Durable.Aardvark.V3d.Id] = DecodeV3d,
                [Durable.Aardvark.V4d.Id] = DecodeV4d,
                [Durable.Aardvark.Box2f.Id] = DecodeBox2f,
                [Durable.Aardvark.Box2d.Id] = DecodeBox2d,
                [Durable.Aardvark.Box3f.Id] = DecodeBox3f,
                [Durable.Aardvark.Box3d.Id] = DecodeBox3d,

                [Durable.Primitives.GuidArray.Id] = DecodeGuidArray,
            };
        }

        #region Encode

        private static readonly Action<BinaryWriter, object> EncodeGuid = (s, o) => s.Write(((Guid)o).ToByteArray(), 0, 16);
        private static readonly Action<BinaryWriter, object> EncodeInt16 = (s, o) => s.Write((short)o);
        private static readonly Action<BinaryWriter, object> EncodeUInt16 = (s, o) => s.Write((ushort)o);
        private static readonly Action<BinaryWriter, object> EncodeInt32 = (s, o) => s.Write((int)o);
        private static readonly Action<BinaryWriter, object> EncodeUInt32 = (s, o) => s.Write((uint)o);
        private static readonly Action<BinaryWriter, object> EncodeInt64 = (s, o) => s.Write((long)o);
        private static readonly Action<BinaryWriter, object> EncodeUInt64 = (s, o) => s.Write((ulong)o);
        private static readonly Action<BinaryWriter, object> EncodeFloat32 = (s, o) => s.Write((float)o);
        private static readonly Action<BinaryWriter, object> EncodeFloat64 = (s, o) => s.Write((double)o);
        private static readonly Action<BinaryWriter, object> EncodeStringUtf8 = (s, o) => EncodeArray(s, Encoding.UTF8.GetBytes((string)o));

        private static readonly Action<BinaryWriter, object> EncodeDurableMap =
            (s, o) =>
            {
                var xs = (IEnumerable<KeyValuePair<Durable.Def, object>>)o;
                var count = xs.Count();
                s.Write(count);
                foreach (var x in xs) Encode(s, x.Key, x.Value);
            };

        private static readonly Action<BinaryWriter, object> EncodeCell =
            (s, o) => { var x = (Cell)o; s.Write(x.X); s.Write(x.Y); s.Write(x.Z); s.Write(x.Exponent); };


        private static readonly Action<BinaryWriter, object> EncodeV2f =
            (s, o) => { var x = (V2f)o; s.Write(x.X); s.Write(x.Y); };

        private static readonly Action<BinaryWriter, object> EncodeV3f =
            (s, o) => { var x = (V3f)o; s.Write(x.X); s.Write(x.Y); s.Write(x.Z); };

        private static readonly Action<BinaryWriter, object> EncodeV4f =
            (s, o) => { var x = (V4f)o; s.Write(x.X); s.Write(x.Y); s.Write(x.Z); s.Write(x.W); };


        private static readonly Action<BinaryWriter, object> EncodeV2d =
            (s, o) => { var x = (V2d)o; s.Write(x.X); s.Write(x.Y); };

        private static readonly Action<BinaryWriter, object> EncodeV3d =
            (s, o) => { var x = (V3d)o; s.Write(x.X); s.Write(x.Y); s.Write(x.Z); };

        private static readonly Action<BinaryWriter, object> EncodeV4d =
            (s, o) => { var x = (V4d)o; s.Write(x.X); s.Write(x.Y); s.Write(x.Z); s.Write(x.W); };


        private static readonly Action<BinaryWriter, object> EncodeBox2f =
            (s, o) => { var x = (Box2f)o; EncodeV2f(s, x.Min); EncodeV2f(s, x.Max); };

        private static readonly Action<BinaryWriter, object> EncodeBox2d =
            (s, o) => { var x = (Box2d)o; EncodeV2d(s, x.Min); EncodeV2d(s, x.Max); };


        private static readonly Action<BinaryWriter, object> EncodeBox3f =
            (s, o) => { var x = (Box3f)o; EncodeV3f(s, x.Min); EncodeV3f(s, x.Max); };

        private static readonly Action<BinaryWriter, object> EncodeBox3d =
            (s, o) => { var x = (Box3d)o; EncodeV3d(s, x.Min); EncodeV3d(s, x.Max); };


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

        private static readonly Action<BinaryWriter, object> EncodeGuidArray = (s, o) => EncodeArray(s, (Guid[])o);


        /// <summary>
        /// </summary>
        public static void Encode<T>(BinaryWriter stream, Durable.Def def, T x)
        {
            if (def.Type != Durable.Primitives.Unit.Id)
            {
                EncodeGuid(stream, def.Id);
                if (s_encoders.TryGetValue(def.Type, out var encoder))
                {
                    ((Action<BinaryWriter, T>)encoder)(stream, x);
                }
                else
                {
                    var unknownDef = Durable.get(def.Type);
                    throw new InvalidOperationException($"Unknown definition {unknownDef}.");
                }
            }
            else
            {
                if (s_encoders.TryGetValue(def.Id, out var encoder))
                {
                    ((Action<BinaryWriter, T>)encoder)(stream, x);
                }
                else
                {
                    var unknownDef = Durable.get(def.Id);
                    throw new InvalidOperationException($"Unknown definition {unknownDef}.");
                }
            }
        }

        #endregion

        #region Decode

        private static readonly Func<BinaryReader, object> DecodeGuid = s => new Guid(s.ReadBytes(16));
        private static readonly Func<BinaryReader, object> DecodeInt16 = s => s.ReadInt16();
        private static readonly Func<BinaryReader, object> DecodeUInt16 = s => s.ReadUInt16();
        private static readonly Func<BinaryReader, object> DecodeInt32 = s => s.ReadInt32();
        private static readonly Func<BinaryReader, object> DecodeUInt32 = s => s.ReadUInt32();
        private static readonly Func<BinaryReader, object> DecodeInt64 = s => s.ReadInt64();
        private static readonly Func<BinaryReader, object> DecodeUInt64 = s => s.ReadUInt64();
        private static readonly Func<BinaryReader, object> DecodeFloat32 = s => s.ReadSingle();
        private static readonly Func<BinaryReader, object> DecodeFloat64 = s => s.ReadDouble();
        private static readonly Func<BinaryReader, object> DecodeStringUtf8 = s => Encoding.UTF8.GetString(DecodeArray<byte>(s));

        private static readonly Func<BinaryReader, object> DecodeDurableMap =
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

        private static readonly Func<BinaryReader, object> DecodeV2f = s => new V2f(s.ReadSingle(), s.ReadSingle());
        private static readonly Func<BinaryReader, object> DecodeV3f = s => new V3f(s.ReadSingle(), s.ReadSingle(), s.ReadSingle());
        private static readonly Func<BinaryReader, object> DecodeV4f = s => new V4f(s.ReadSingle(), s.ReadSingle(), s.ReadSingle(), s.ReadSingle());

        private static readonly Func<BinaryReader, object> DecodeV2d = s => new V2d(s.ReadDouble(), s.ReadDouble());
        private static readonly Func<BinaryReader, object> DecodeV3d = s => new V3d(s.ReadDouble(), s.ReadDouble(), s.ReadDouble());
        private static readonly Func<BinaryReader, object> DecodeV4d = s => new V4d(s.ReadDouble(), s.ReadDouble(), s.ReadDouble(), s.ReadDouble());

        private static readonly Func<BinaryReader, object> DecodeBox2f = s => new Box2f((V2f)DecodeV2f(s), (V2f)DecodeV2f(s));
        private static readonly Func<BinaryReader, object> DecodeBox2d = s => new Box2d((V2d)DecodeV2d(s), (V2d)DecodeV2d(s));

        private static readonly Func<BinaryReader, object> DecodeBox3f = s => new Box3f((V3f)DecodeV3f(s), (V3f)DecodeV3f(s));
        private static readonly Func<BinaryReader, object> DecodeBox3d = s => new Box3d((V3d)DecodeV3d(s), (V3d)DecodeV3d(s));

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


        /// <summary>
        /// </summary>
        public static (Durable.Def, object) Decode(BinaryReader stream)
        {
            var def = Durable.get((Guid)DecodeGuid(stream));

            if (def.Type != Durable.Primitives.Unit.Id)
            {
                if (s_decoders.TryGetValue(def.Type, out var decoder))
                {
                    var o = ((Func<BinaryReader, object>)decoder)(stream);
                    return (def, o);
                }
                else
                {
                    var unknownDef = Durable.get(def.Type);
                    throw new InvalidOperationException($"Unknown definition {unknownDef}.");
                }
            }
            else
            {
                if (s_encoders.TryGetValue(def.Id, out var decoder))
                {
                    var o = ((Func<BinaryReader, object>)decoder)(stream);
                    return (def, o);
                }
                else
                {
                    var unknownDef = Durable.get(def.Id);
                    throw new InvalidOperationException($"Unknown definition {unknownDef}.");
                }
            }
        }

        #endregion
    }
}
