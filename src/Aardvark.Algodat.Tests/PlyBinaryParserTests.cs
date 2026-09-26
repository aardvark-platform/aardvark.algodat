/*
    Copyright (C) 2006-2026. Aardvark Platform Team. http://github.com/aardvark-platform.
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.
    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/
using Aardvark.Base;
using Aardvark.Data.Points;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using Ply.Net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class PlyBinaryParserTests
    {
        [TestCase(false, "\n")]
        [TestCase(false, "\r\n")]
        [TestCase(true, "\n")]
        [TestCase(true, "\r\n")]
        public void ScalarsDecodeAllNumericTypes(bool bigEndian, string newline)
        {
            var file = CreateFile(bigEndian, newline, new[]
            {
                "element sample 2",
                "property char i8",
                "property uchar u8",
                "property short i16",
                "property ushort u16",
                "property int i32",
                "property uint u32",
                "property long i64",
                "property ulong u64",
                "property float f32",
                "property double f64"
            }, writer =>
            {
                WriteScalarRecord(writer, 0);
                WriteScalarRecord(writer, 1);
            });

            using (var stream = new MemoryStream(file.Bytes, writable: false))
            {
                var header = PlyParser.ParseHeader(stream, log: null);
                ClassicAssert.AreEqual(file.DataOffset, header.DataOffset);
                ClassicAssert.AreEqual(file.DataOffset, stream.Position);
            }

            var chunks = Parse(file.Bytes, maxChunkSize: 1);
            ClassicAssert.AreEqual(2, chunks.Length);
            ClassicAssert.IsTrue(chunks.All(x => x.Element.Name == "sample"));
            CollectionAssert.AreEqual(new sbyte[] { -5, -6 }, ScalarValues<sbyte>(chunks, "i8"));
            CollectionAssert.AreEqual(new byte[] { 250, 249 }, ScalarValues<byte>(chunks, "u8"));
            CollectionAssert.AreEqual(new short[] { -12345, -12346 }, ScalarValues<short>(chunks, "i16"));
            CollectionAssert.AreEqual(new ushort[] { 54321, 54320 }, ScalarValues<ushort>(chunks, "u16"));
            CollectionAssert.AreEqual(new[] { -123456789, -123456790 }, ScalarValues<int>(chunks, "i32"));
            CollectionAssert.AreEqual(new uint[] { 3456789012, 3456789011 }, ScalarValues<uint>(chunks, "u32"));
            CollectionAssert.AreEqual(new long[] { -1234567890123, -1234567890124 }, ScalarValues<long>(chunks, "i64"));
            CollectionAssert.AreEqual(new ulong[] { 12345678901234567890, 12345678901234567889 }, ScalarValues<ulong>(chunks, "u64"));
            CollectionAssert.AreEqual(new[] { -123.25f, -124.25f }, ScalarValues<float>(chunks, "f32"));
            CollectionAssert.AreEqual(new[] { Math.PI, Math.PI + 1.0 }, ScalarValues<double>(chunks, "f64"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ListsDecodeEveryIntegerCountAndNumericValueType(bool bigEndian)
        {
            var file = CreateFile(bigEndian, bigEndian ? "\r\n" : "\n", new[]
            {
                "element mixed 1",
                "property list char char li8",
                "property list uchar uchar lu8",
                "property list short short li16",
                "property list ushort ushort lu16",
                "property list int int li32",
                "property uint marker",
                "property list uint uint lu32",
                "property list long long li64",
                "property list ulong ulong lu64",
                "property list char float lf32",
                "property list uchar double lf64"
            }, writer =>
            {
                writer.Int8(2); writer.Int8(-2); writer.Int8(100);
                writer.UInt8(2); writer.UInt8(1); writer.UInt8(250);
                writer.Int16(2); writer.Int16(-30000); writer.Int16(1234);
                writer.UInt16(2); writer.UInt16(50000); writer.UInt16(3);
                writer.Int32(2); writer.Int32(-2000000000); writer.Int32(1000000000);
                writer.UInt32(0xdeadbeef);
                writer.UInt32(2); writer.UInt32(4000000000); writer.UInt32(3000000000);
                writer.Int64(2); writer.Int64(-7000000000000000000); writer.Int64(6000000000000000000);
                writer.UInt64(2); writer.UInt64(17000000000000000000); writer.UInt64(9000000000000000000);
                writer.Int8(2); writer.Float32(1.5f); writer.Float32(-2.25f);
                writer.UInt8(2); writer.Float64(Math.PI); writer.Float64(-0.125);
            });

            var chunk = Parse(file.Bytes, maxChunkSize: 16).Single();
            CollectionAssert.AreEqual(new sbyte[] { -2, 100 }, ListValue<sbyte>(chunk, "li8"));
            CollectionAssert.AreEqual(new byte[] { 1, 250 }, ListValue<byte>(chunk, "lu8"));
            CollectionAssert.AreEqual(new short[] { -30000, 1234 }, ListValue<short>(chunk, "li16"));
            CollectionAssert.AreEqual(new ushort[] { 50000, 3 }, ListValue<ushort>(chunk, "lu16"));
            CollectionAssert.AreEqual(new[] { -2000000000, 1000000000 }, ListValue<int>(chunk, "li32"));
            CollectionAssert.AreEqual(new uint[] { 4000000000, 3000000000 }, ListValue<uint>(chunk, "lu32"));
            CollectionAssert.AreEqual(new long[] { -7000000000000000000, 6000000000000000000 }, ListValue<long>(chunk, "li64"));
            CollectionAssert.AreEqual(new ulong[] { 17000000000000000000, 9000000000000000000 }, ListValue<ulong>(chunk, "lu64"));
            CollectionAssert.AreEqual(new[] { 1.5f, -2.25f }, ListValue<float>(chunk, "lf32"));
            CollectionAssert.AreEqual(new[] { Math.PI, -0.125 }, ListValue<double>(chunk, "lf64"));
            CollectionAssert.AreEqual(new uint[] { 0xdeadbeef }, (uint[])chunk["marker"].Data);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MultipleElementsAndChunksRemainInFileOrder(bool bigEndian)
        {
            var file = CreateFile(bigEndian, "\n", new[]
            {
                "element vertex 3",
                "property int id",
                "property short marker",
                "element face 2",
                "property uchar material",
                "property list ushort int vertex_indices"
            }, writer =>
            {
                for (var i = 0; i < 3; i++)
                {
                    writer.Int32(100 + i);
                    writer.Int16((short)(-10 - i));
                }
                writer.UInt8(7); writer.UInt16(3); writer.Int32(0); writer.Int32(1); writer.Int32(2);
                writer.UInt8(8); writer.UInt16(4); writer.Int32(2); writer.Int32(3); writer.Int32(4); writer.Int32(5);
            });

            var chunks = Parse(file.Bytes, maxChunkSize: 2);
            CollectionAssert.AreEqual(new[] { "vertex", "vertex", "face" }, chunks.Select(x => x.Element.Name).ToArray());
            CollectionAssert.AreEqual(new[] { 100, 101, 102 }, ScalarValues<int>(chunks.Take(2), "id"));
            CollectionAssert.AreEqual(new short[] { -10, -11, -12 }, ScalarValues<short>(chunks.Take(2), "marker"));
            CollectionAssert.AreEqual(new byte[] { 7, 8 }, ScalarValues<byte>(chunks.Skip(2), "material"));
            var faces = chunks[2]["vertex_indices"].Data as int[][];
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, faces[0]);
            CollectionAssert.AreEqual(new[] { 2, 3, 4, 5 }, faces[1]);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void HighLevelChunksPreserveVertexAttributes(bool bigEndian)
        {
            var file = CreateFile(bigEndian, "\r\n", new[]
            {
                "element vertex 5",
                "property double x",
                "property float y",
                "property short z",
                "property float nx",
                "property float ny",
                "property float nz",
                "property uchar red",
                "property uchar green",
                "property uchar blue",
                "property uchar alpha",
                "property ushort intensity",
                "property ushort classification"
            }, writer =>
            {
                for (var i = 0; i < 5; i++)
                {
                    writer.Float64(i + 0.25);
                    writer.Float32(i * -0.5f);
                    writer.Int16((short)(i * 10));
                    writer.Float32(i + 0.1f);
                    writer.Float32(i + 0.2f);
                    writer.Float32(i + 0.3f);
                    writer.UInt8((byte)(10 + i));
                    writer.UInt8((byte)(20 + i));
                    writer.UInt8((byte)(30 + i));
                    writer.UInt8((byte)(40 + i));
                    writer.UInt16((ushort)(1000 + i));
                    writer.UInt16((ushort)(50 + i));
                }
            });

            using var stream = new MemoryStream(file.Bytes, writable: false);
            var chunks = Aardvark.Data.Points.Import.Ply.Chunks(
                stream,
                file.Bytes.LongLength,
                ParseConfig.Default.WithMaxChunkPointCount(2)
                ).ToArray();
            CollectionAssert.AreEqual(new[] { 2, 2, 1 }, chunks.Select(x => x.Count).ToArray());

            var positions = chunks.SelectMany(x => x.Positions).ToArray();
            var colors = chunks.SelectMany(x => x.Colors).ToArray();
            var normals = chunks.SelectMany(x => x.Normals).ToArray();
            var intensities = chunks.SelectMany(x => x.Intensities).ToArray();
            var classifications = chunks.SelectMany(x => x.Classifications).ToArray();
            for (var i = 0; i < 5; i++)
            {
                ClassicAssert.AreEqual(new V3d(i + 0.25, i * -0.5, i * 10), positions[i]);
                ClassicAssert.AreEqual(new C4b((byte)(10 + i), (byte)(20 + i), (byte)(30 + i), (byte)(40 + i)), colors[i]);
                ClassicAssert.AreEqual(new V3f(i + 0.1f, i + 0.2f, i + 0.3f), normals[i]);
                ClassicAssert.AreEqual(1000 + i, intensities[i]);
                ClassicAssert.AreEqual(50 + i, classifications[i]);
            }
        }

        [TestCase("char", -1L)]
        [TestCase("short", -1L)]
        [TestCase("int", -1L)]
        [TestCase("long", -1L)]
        [TestCase("uint", 2147483648L)]
        [TestCase("long", 2147483648L)]
        public void InvalidSignedAndOversizedListCountsAreRejected(string countType, long count)
        {
            var file = CreateCountFile(countType, bigEndian: true, writer => WriteCount(writer, countType, count));
            ClassicAssert.Throws<InvalidDataException>(() => Parse(file, maxChunkSize: 1));
        }

        [Test]
        public void OversizedUnsignedLongListCountIsRejected()
        {
            var file = CreateCountFile("ulong", bigEndian: false, writer => writer.UInt64(ulong.MaxValue));
            ClassicAssert.Throws<InvalidDataException>(() => Parse(file, maxChunkSize: 1));
        }

        [Test]
        public void ListPayloadByteCountOverflowIsRejectedBeforeAllocation()
        {
            var file = CreateCountFile("uint", bigEndian: false, writer => writer.UInt32(int.MaxValue), valueType: "double");
            ClassicAssert.Throws<InvalidDataException>(() => Parse(file, maxChunkSize: 1));
        }

        [TestCase("float")]
        [TestCase("double")]
        public void NonIntegerListCountDeclarationsAreRejected(string countType)
        {
            var file = CreateFile(false, "\n", new[]
            {
                "element face 1",
                $"property list {countType} int indices"
            }, _ => { });
            using var stream = new MemoryStream(file.Bytes, writable: false);
            ClassicAssert.Throws<InvalidDataException>(() => PlyParser.Parse(stream, 1));
        }

        [Test]
        public void ZeroLengthListsAreDecoded()
        {
            var file = CreateCountFile("ushort", bigEndian: true, writer => writer.UInt16(0));
            var chunk = Parse(file, 1).Single();
            ClassicAssert.AreEqual(0, ListValue<int>(chunk, "items").Length);
        }

        [TestCaseSource(nameof(TruncatedFiles))]
        public void TruncatedBinaryDataIsRejected(byte[] file)
        {
            ClassicAssert.Throws<EndOfStreamException>(() => Parse(file, maxChunkSize: 1));
        }

        private static IEnumerable<byte[]> TruncatedFiles()
        {
            yield return CreateFile(false, "\n", new[]
            {
                "element vertex 1",
                "property double x"
            }, writer =>
            {
                for (var i = 0; i < 7; i++) writer.UInt8((byte)i);
            }).Bytes;

            yield return CreateFile(true, "\r\n", new[]
            {
                "element face 1",
                "property list ushort int items"
            }, writer => writer.UInt8(0)).Bytes;

            yield return CreateFile(false, "\n", new[]
            {
                "element face 1",
                "property list uchar int items"
            }, writer =>
            {
                writer.UInt8(2);
                writer.Int32(10);
                writer.Int16(20);
            }).Bytes;
        }

        private sealed class FileData
        {
            public byte[] Bytes { get; }
            public int DataOffset { get; }

            public FileData(byte[] bytes, int dataOffset)
            {
                Bytes = bytes;
                DataOffset = dataOffset;
            }
        }

        private sealed class EndianWriter
        {
            private readonly Stream _stream;
            private readonly bool _bigEndian;

            public EndianWriter(Stream stream, bool bigEndian)
            {
                _stream = stream;
                _bigEndian = bigEndian;
            }

            public void Int8(sbyte value) => _stream.WriteByte(unchecked((byte)value));
            public void UInt8(byte value) => _stream.WriteByte(value);
            public void Int16(short value) => Write(unchecked((ushort)value), 2);
            public void UInt16(ushort value) => Write(value, 2);
            public void Int32(int value) => Write(unchecked((uint)value), 4);
            public void UInt32(uint value) => Write(value, 4);
            public void Int64(long value) => Write(unchecked((ulong)value), 8);
            public void UInt64(ulong value) => Write(value, 8);
            public void Float32(float value) => Int32(BitConverter.SingleToInt32Bits(value));
            public void Float64(double value) => Int64(BitConverter.DoubleToInt64Bits(value));

            private void Write(ulong value, int byteCount)
            {
                if (_bigEndian)
                {
                    for (var i = byteCount - 1; i >= 0; i--) _stream.WriteByte((byte)(value >> (i * 8)));
                }
                else
                {
                    for (var i = 0; i < byteCount; i++) _stream.WriteByte((byte)(value >> (i * 8)));
                }
            }
        }

        private static FileData CreateFile(
            bool bigEndian,
            string newline,
            string[] declarations,
            Action<EndianWriter> writePayload
            )
        {
            using var stream = new MemoryStream();
            var lines = new List<string>
            {
                "ply",
                $"format binary_{(bigEndian ? "big" : "little")}_endian 1.0"
            };
            lines.AddRange(declarations);
            lines.Add("end_header");
            var header = Encoding.ASCII.GetBytes(string.Join(newline, lines) + newline);
            stream.Write(header, 0, header.Length);
            writePayload(new EndianWriter(stream, bigEndian));
            return new FileData(stream.ToArray(), header.Length);
        }

        private static byte[] CreateCountFile(
            string countType,
            bool bigEndian,
            Action<EndianWriter> writePayload,
            string valueType = "int"
            )
            => CreateFile(bigEndian, "\n", new[]
            {
                "element face 1",
                $"property list {countType} {valueType} items"
            }, writePayload).Bytes;

        private static void WriteCount(EndianWriter writer, string countType, long value)
        {
            switch (countType)
            {
                case "char": writer.Int8((sbyte)value); break;
                case "short": writer.Int16((short)value); break;
                case "int": writer.Int32((int)value); break;
                case "uint": writer.UInt32((uint)value); break;
                case "long": writer.Int64(value); break;
                default: throw new ArgumentOutOfRangeException(nameof(countType));
            }
        }

        private static void WriteScalarRecord(EndianWriter writer, int row)
        {
            writer.Int8((sbyte)(-5 - row));
            writer.UInt8((byte)(250 - row));
            writer.Int16((short)(-12345 - row));
            writer.UInt16((ushort)(54321 - row));
            writer.Int32(-123456789 - row);
            writer.UInt32(3456789012 - (uint)row);
            writer.Int64(-1234567890123 - row);
            writer.UInt64(12345678901234567890 - (ulong)row);
            writer.Float32(-123.25f - row);
            writer.Float64(Math.PI + row);
        }

        private static PlyParser.ElementData[] Parse(byte[] bytes, int maxChunkSize)
        {
            using var stream = new MemoryStream(bytes, writable: false);
            return PlyParser.Parse(stream, maxChunkSize).Data.ToArray();
        }

        private static T[] ScalarValues<T>(IEnumerable<PlyParser.ElementData> chunks, string propertyName)
            => chunks.SelectMany(x => (T[])x[propertyName].Data).ToArray();

        private static T[] ListValue<T>(PlyParser.ElementData chunk, string propertyName)
            => ((T[][])chunk[propertyName].Data).Single();
    }
}
