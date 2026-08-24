/*
   Copyright (C) 2018-2025. Stefan Maierhofer.

   This code is based on https://github.com/stefanmaierhofer/Ply.Net (copied and extended).

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
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Ply.Net;

public static class PlyParser
{
    #region Types

    public enum Format
    {
        Undefined,
        Ascii,
        BinaryLittleEndian,
        BinaryBigEndian
    }

    public enum DataType
    {
        Undefined,
        Int8, UInt8,
        Int16, UInt16,
        Int32, UInt32,
        Int64, UInt64,
        Float32, Float64
    }

    public enum ElementType
    {
        Vertex,
        Face,
        Edge,
        Material,
        Cell,
        UserDefined,
    }

    public record Property(DataType DataType, string Name, DataType ListCountType)
    {
        public bool IsListProperty => ListCountType != DataType.Undefined;
    }

    public record Element(ElementType Type, string Name, int Count, ImmutableList<Property> Properties)
    {
        public Element Add(Property p) => this with { Properties = Properties.Add(p) };

        public bool ContainsListProperty => Properties.Any(p => p.IsListProperty);
    }

    public record Header(Format Format, ImmutableList<Element> Elements, ImmutableList<string> HeaderLines, long DataOffset)
    {
        public Element? Cell     => Elements.SingleOrDefault(x => x.Type == ElementType.Cell);
        public Element? Edge     => Elements.SingleOrDefault(x => x.Type == ElementType.Edge);
        public Element? Face     => Elements.SingleOrDefault(x => x.Type == ElementType.Face);
        public Element? Material => Elements.SingleOrDefault(x => x.Type == ElementType.Material);
        public Element? Vertex   => Elements.SingleOrDefault(x => x.Type == ElementType.Vertex);
    }

    public record PropertyData(Property Property, Array Data);

    public record ElementData(Element Element, ImmutableList<PropertyData> Data)
    {
        public PropertyData? this[string propertyName] => Data.SingleOrDefault(x => x.Property.Name == propertyName);
    }

    public record Dataset(Header Header, IEnumerable<ElementData> Data);

    #endregion

    #region Header

    private static string? ReadLine(Stream stream, int maxLength)
    {
        var sb = new StringBuilder();
        while (true)
        {
            var c = stream.ReadByte();
            if (c < 0) return sb.Length == 0 ? null : sb.ToString();
            if (c == '\n') return sb.ToString();
            if (c == '\r')
            {
                var next = stream.ReadByte();
                if (next >= 0 && next != '\n') stream.Position--;
                return sb.ToString();
            }
            if (sb.Length == maxLength)
                throw new InvalidDataException($"PLY header line exceeds {maxLength} bytes.");
            sb.Append((char)c);
        }
    }

    private static Format ParseFormatLine(string line)
    {
        void Fail() => throw new Exception($"Expected \"format [ascii|binary_little_endian|binary_big_endian] 1.0\", but found:\n{line}.");

        if (!line.StartsWith("format")) Fail();

        var ts = line.SplitOnWhitespace();
        if (ts.Length != 3 || ts[2] != "1.0") Fail();

        var x = ts[1] switch
        {
            "ascii" => Format.Ascii,
            "binary_little_endian" => Format.BinaryLittleEndian,
            "binary_big_endian" => Format.BinaryBigEndian,
            _ => Format.Undefined
        };

        if (x == Format.Undefined) Fail();

        return x;
    }

    private static Element ParseElementLine(string line)
    {
        void Fail() => throw new Exception($"Expected \"element <element-name> <number-in-file>\", but found:\n{line}.");

        if (!line.StartsWith("element")) Fail();

        var ts = line.SplitOnWhitespace();
        if (ts.Length != 3) Fail();

        if (!int.TryParse(ts[2], out var count) || count < 0)
        {
            throw new Exception($"Expected <number_in_file> to be in range [0..2147483647], but found \"{ts[2]}\".");
        }

        var type = ts[1].ToLower() switch
        {
            "cell"     => ElementType.Cell,
            "edge"     => ElementType.Edge,
            "face"     => ElementType.Face,
            "material" => ElementType.Material,
            "vertex"   => ElementType.Vertex,
            _          => ElementType.UserDefined,
        };

        return new(type, ts[1], count, []);
    }

    private static Property ParsePropertyLine(string line)
    {
        void Fail() => throw new Exception($"Expected \"property <data-type> <property-name>\", but found:\n{line}.");

        if (!line.StartsWith("property")) Fail();

        var ts = line.SplitOnWhitespace();

        if (ts.Length == 3)
        {
            var datatype = ParseDataType(ts[1]);
            return new(datatype, Name: ts[2].ToLower(), ListCountType: DataType.Undefined);
        }
        else if (ts.Length == 5 && ts[1] == "list")
        {
            var listCountType = ParseDataType(ts[2]);
            if (!IsIntegerDataType(listCountType))
                throw new InvalidDataException($"PLY list count type must be an integer, but found \"{ts[2]}\".");
            var datatype = ParseDataType(ts[3]);
            return new(datatype, Name: ts[4].ToLower(), listCountType);
        }
        else
        {
            Fail();
            throw new Exception(); // make compiler happy
        }
    }

    private static bool IsIntegerDataType(DataType type) => type is
        DataType.Int8 or DataType.UInt8 or
        DataType.Int16 or DataType.UInt16 or
        DataType.Int32 or DataType.UInt32 or
        DataType.Int64 or DataType.UInt64;

    private static DataType ParseDataType(string s) => s switch
    {
        "char"   => DataType.Int8,    "int8"    => DataType.Int8,    "sbyte" => DataType.Int8,
        "uchar"  => DataType.UInt8,   "uint8"   => DataType.UInt8,   "ubyte" => DataType.UInt8, "byte" => DataType.UInt8,
        "short"  => DataType.Int16,   "int16"   => DataType.Int16,
        "ushort" => DataType.UInt16,  "uint16"  => DataType.UInt16,
        "int"    => DataType.Int32,   "int32"   => DataType.Int32,
        "uint"   => DataType.UInt32,  "uint32"  => DataType.UInt32,
        "long"   => DataType.Int64,   "int64"   => DataType.Int64,
        "ulong"  => DataType.UInt64,  "uint64"  => DataType.UInt64,
        "float"  => DataType.Float32, "float32" => DataType.Float32,
        "double" => DataType.Float64, "float64" => DataType.Float64,
        _ => throw new Exception($"Unknown data type \"{s}\".")
    };

    public static Header ParseHeader(Stream f, Action<string>? log)
    {
        #region check for magic bytes

        var magic = new byte[3];
        if (f.Read(magic, 0, 3) != 3 || (char)magic[0] != 'p' || (char)magic[1] != 'l' || (char)magic[2] != 'y')
        {
            throw new Exception("Not a ply file.");
        }
        f.Position = 0;
        var firstLine = ReadLine(f, 1024);
        if (firstLine != "ply")
        {
            throw new Exception("Not a ply file.");
        }

        #endregion

        var format = Format.Undefined;
        var elements = ImmutableList<Element>.Empty;
        var headerlines = ImmutableList<string>.Empty;

        Element? currentElement = default;
        while (true)
        {
            var line = ReadLine(f, 1024);

            if (line == null)
            {
                throw new Exception("Could not read next header line.");
            }
            else
            {
                headerlines = headerlines.Add(line);
            }

            // comment
            if (line.StartsWith("comment") || line.StartsWith("obj_info"))
            {
                continue;
            }

            // format
            if (format == Format.Undefined)
            {
                format = ParseFormatLine(line);
                continue;
            }

            // end of header
            if (line == "end_header")
            {
                break;
            }

            // element
            if (line.StartsWith("element"))
            {
                if (currentElement != null) elements = elements.Add(currentElement);
                currentElement = ParseElementLine(line);
                continue;
            }

            // property
            if (line.StartsWith("property"))
            {
                if (currentElement == null) throw new Exception($"Expected \"element\" definition before \"property\" definition.");
                var p = ParsePropertyLine(line);
                currentElement = currentElement.Add(p);
                continue;
            }

            log?.Invoke($"[PlyParser][WARNING] Unknown header entry: {line}");
        }

        if (currentElement != null) elements = elements.Add(currentElement);

        var header = new Header(format, elements, headerlines, DataOffset: f.Position);
        if (log != null)
        {
            foreach (var s in header.HeaderLines) log($"[PlyParser] {s}");
        }

        return header;
    }

    public static Header ParseHeader(string filename, Action<string>? log = null)
    {
        using var f = File.OpenRead(filename);
        return ParseHeader(f, log);
    }

    #endregion

    private static class AsciiParser
    {
        private delegate void PropertyParser(int rowIndex, Row rowData);

        private class Row(string line)
        {
            private readonly string[] _ts = line.SplitOnWhitespace();
            private int _next = 0;

            public string NextToken() => _ts[_next++];
        }

        private static ElementData ParseElement(StreamReader f, Element element, Action<string>? log)
        {
            (PropertyParser Parse, Array Data) CreateListPropertyParser(Property p)
            {
                (Action<int, Row, int>, Array) ParseListValues<T>(Func<string, T> parseValue)
                {
                    var rs = new T[element.Count][];
                    return ((row, line, count) =>
                    {
                        var xs = new T[count]; rs[row] = xs;
                        for (var i = 0; i < count; i++) xs[i] = parseValue(line.NextToken());
                    }, rs);
                }

                var (parseListEntries, data) = p.DataType switch
                {
                    DataType.Int8    => ParseListValues(sbyte.Parse),
                    DataType.UInt8   => ParseListValues(byte.Parse),
                    DataType.Int16   => ParseListValues(short.Parse),
                    DataType.UInt16  => ParseListValues(ushort.Parse),
                    DataType.Int32   => ParseListValues(int.Parse),
                    DataType.UInt32  => ParseListValues(uint.Parse),
                    DataType.Int64   => ParseListValues(long.Parse),
                    DataType.UInt64  => ParseListValues(ulong.Parse),
                    DataType.Float32 => ParseListValues(s => float.Parse(s, CultureInfo.InvariantCulture)),
                    DataType.Float64 => ParseListValues(s => double.Parse(s, CultureInfo.InvariantCulture)),
                    _ => throw new Exception($"List data type {p.DataType} is not supported."),
                };

                return (
                    Parse: (row, line) => parseListEntries(row, line, int.Parse(line.NextToken())),
                    Data : data
                    );
            }

            (PropertyParser Parse, Array Data) CreateScalarPropertyParser(Property p)
            {
                (PropertyParser Parse, Array Data) ParseScalarValue<T>(Func<string, T> parse)
                { var xs = new T[element.Count]; return ((row, line) => xs[row] = parse(line.NextToken()), xs); }

                return p.DataType switch
                {
                    DataType.Int8    => ParseScalarValue(sbyte.Parse),
                    DataType.UInt8   => ParseScalarValue(byte.Parse),
                    DataType.Int16   => ParseScalarValue(short.Parse),
                    DataType.UInt16  => ParseScalarValue(ushort.Parse),
                    DataType.Int32   => ParseScalarValue(int.Parse),
                    DataType.UInt32  => ParseScalarValue(uint.Parse),
                    DataType.Int64   => ParseScalarValue(long.Parse),
                    DataType.UInt64  => ParseScalarValue(ulong.Parse),
                    DataType.Float32 => ParseScalarValue(s => float.Parse(s, CultureInfo.InvariantCulture)),
                    DataType.Float64 => ParseScalarValue(s => double.Parse(s, CultureInfo.InvariantCulture)),
                    _ => throw new Exception($"List data type {p.DataType} is not supported."),
                };
            };

            var parse = new PropertyParser[element.Properties.Count];
            var data  = new PropertyData[element.Properties.Count];

            for (var pi = 0; pi < element.Properties.Count; pi++)
            {
                var p = element.Properties[pi];
                (parse[pi], var d) = p.IsListProperty ? CreateListPropertyParser(p) : CreateScalarPropertyParser(p);
                data[pi] = new(p, d);
            }

            for (var i = 0; i < element.Count; i++)
            {
                var l = f.ReadLine();
                if (l == string.Empty) { i--; continue; }
                if (l == null)
                {
                    log?.Invoke($"[PlyParser] Failed to read next line. Premature end of element {element} after {i}/{element.Count} lines.");
                    break;
                }

                var line = new Row(l);
                for (var j = 0; j < element.Properties.Count; j++) parse[j](i, line);
            }

            return new(element, [.. data]);
        }

        public static Dataset Parse(Header header, Stream f, Action<string>? log)
        {
            var sr = new StreamReader(f);
            var data = header.Elements.Select(e => ParseElement(sr, e, log)).ToImmutableList();
            return new(header, data);
        }
    }

    private static class BinaryParser
    {
        private static readonly Dictionary<DataType, int> DataTypeSizes = new()
        {
            { DataType.Int8,    1 },
            { DataType.UInt8,   1 },
            { DataType.Int16,   2 },
            { DataType.UInt16,  2 },
            { DataType.Int32,   4 },
            { DataType.UInt32,  4 },
            { DataType.Int64,   8 },
            { DataType.UInt64,  8 },
            { DataType.Float32, 4 },
            { DataType.Float64, 8 }
        };

        /// <summary>
        /// Size of element in bytes, or null if element contains one or more list properties (there is no fixed size in this case).
        /// </summary>
        private static int? GetSizeInBytes(Element element)
        {
            if (element.ContainsListProperty) return null;
            try
            {
                var size = 0;
                foreach (var property in element.Properties)
                    size = checked(size + DataTypeSizes[property.DataType]);
                return size;
            }
            catch (OverflowException e)
            {
                throw new InvalidDataException($"PLY element '{element.Name}' has an invalid fixed record size.", e);
            }
        }

        private static Array AllocArray(Property p, int count) => (p.IsListProperty, p.DataType) switch
        {
            (false, DataType.Int8   ) => new sbyte [count], (true , DataType.Int8   ) => new sbyte [count][],
            (false, DataType.UInt8  ) => new byte  [count], (true , DataType.UInt8  ) => new byte  [count][],
            (false, DataType.Int16  ) => new short [count], (true , DataType.Int16  ) => new short [count][],
            (false, DataType.UInt16 ) => new ushort[count], (true , DataType.UInt16 ) => new ushort[count][],
            (false, DataType.Int32  ) => new int   [count], (true , DataType.Int32  ) => new int   [count][],
            (false, DataType.UInt32 ) => new uint  [count], (true , DataType.UInt32 ) => new uint  [count][],
            (false, DataType.Int64  ) => new long  [count], (true , DataType.Int64  ) => new long  [count][],
            (false, DataType.UInt64 ) => new ulong [count], (true , DataType.UInt64 ) => new ulong [count][],
            (false, DataType.Float32) => new float [count], (true , DataType.Float32) => new float [count][],
            (false, DataType.Float64) => new double[count], (true , DataType.Float64) => new double[count][],
            _ => throw new InvalidDataException($"PLY property '{p.Name}' has unsupported type {p.DataType}.")
        };

        private sealed class BinaryInput
        {
            private const int BufferSize = 4096;

            private readonly Stream _stream;
            private long _remaining;
            private byte[]? _buffer;
            private int _bufferOffset;
            private int _bufferCount;

            public BinaryInput(Stream stream)
            {
                _stream = stream;
                _remaining = stream.Length - stream.Position;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void EnsureAvailable(int byteCount)
            {
                if (_remaining < byteCount)
                    throw new EndOfStreamException("Unexpected end of binary PLY data.");
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public byte ReadByte()
            {
                EnsureBuffered(1);
                _remaining--;
                _bufferCount--;
                return _buffer![_bufferOffset++];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public unsafe T ReadNative<T>() where T : unmanaged
            {
                var byteCount = sizeof(T);
                EnsureBuffered(byteCount);
                _remaining -= byteCount;
                T value;
                fixed (byte* p = &_buffer![_bufferOffset]) value = *(T*)p;
                _bufferOffset += byteCount;
                _bufferCount -= byteCount;
                return value;
            }

            public void ReadNativeArray(Array target, int byteCount)
            {
                EnsureAvailable(byteCount);
                var totalByteCount = byteCount;
                var targetOffset = 0;
                while (byteCount > 0)
                {
                    if (_bufferCount == 0) EnsureBuffered(1);
                    var count = Math.Min(byteCount, _bufferCount);
                    Buffer.BlockCopy(_buffer!, _bufferOffset, target, targetOffset, count);
                    _bufferOffset += count;
                    _bufferCount -= count;
                    targetOffset += count;
                    byteCount -= count;
                }
                _remaining -= totalByteCount;
            }

            public void ReadExactly(byte[] target, int offset, int count)
            {
                EnsureAvailable(count);
                var totalByteCount = count;

                if (_bufferCount > 0)
                {
                    var bufferedCount = Math.Min(count, _bufferCount);
                    Buffer.BlockCopy(_buffer!, _bufferOffset, target, offset, bufferedCount);
                    _bufferOffset += bufferedCount;
                    _bufferCount -= bufferedCount;
                    offset += bufferedCount;
                    count -= bufferedCount;
                }

                if (count >= BufferSize)
                {
                    var read = _stream.Read(target, offset, count);
                    if (read <= 0) throw new EndOfStreamException("Unexpected end of binary PLY data.");
                    if (read < count) ReadFromStream(target, offset + read, count - read);
                    _remaining -= totalByteCount;
                    return;
                }

                while (count > 0)
                {
                    EnsureBuffered(1);
                    var bufferedCount = Math.Min(count, _bufferCount);
                    Buffer.BlockCopy(_buffer!, _bufferOffset, target, offset, bufferedCount);
                    _bufferOffset += bufferedCount;
                    _bufferCount -= bufferedCount;
                    offset += bufferedCount;
                    count -= bufferedCount;
                }
                _remaining -= totalByteCount;
            }

            private void EnsureBuffered(int requiredCount)
            {
                EnsureAvailable(requiredCount);
                if (_bufferCount >= requiredCount) return;

                _buffer ??= new byte[BufferSize];
                if (_bufferCount > 0)
                    Buffer.BlockCopy(_buffer, _bufferOffset, _buffer, 0, _bufferCount);
                _bufferOffset = 0;

                while (_bufferCount < requiredCount)
                {
                    var availableInStream = _remaining - _bufferCount;
                    var readCount = (int)Math.Min(_buffer.Length - _bufferCount, availableInStream);
                    var read = _stream.Read(_buffer, _bufferCount, readCount);
                    if (read <= 0) throw new EndOfStreamException("Unexpected end of binary PLY data.");
                    _bufferCount += read;
                }
            }

            private void ReadFromStream(byte[] target, int offset, int count)
            {
                while (count > 0)
                {
                    var read = _stream.Read(target, offset, count);
                    if (read <= 0) throw new EndOfStreamException("Unexpected end of binary PLY data.");
                    offset += read;
                    count -= read;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort DecodeUInt16(byte[] data, int offset, bool littleEndian)
            => littleEndian
                ? (ushort)(data[offset] | data[offset + 1] << 8)
                : (ushort)(data[offset] << 8 | data[offset + 1]);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint DecodeUInt32(byte[] data, int offset, bool littleEndian)
            => littleEndian
                ? (uint)(data[offset] | data[offset + 1] << 8 | data[offset + 2] << 16 | data[offset + 3] << 24)
                : (uint)(data[offset] << 24 | data[offset + 1] << 16 | data[offset + 2] << 8 | data[offset + 3]);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong DecodeUInt64(byte[] data, int offset, bool littleEndian)
        {
            if (littleEndian)
            {
                return data[offset]
                    | (ulong)data[offset + 1] << 8
                    | (ulong)data[offset + 2] << 16
                    | (ulong)data[offset + 3] << 24
                    | (ulong)data[offset + 4] << 32
                    | (ulong)data[offset + 5] << 40
                    | (ulong)data[offset + 6] << 48
                    | (ulong)data[offset + 7] << 56;
            }

            return (ulong)data[offset] << 56
                | (ulong)data[offset + 1] << 48
                | (ulong)data[offset + 2] << 40
                | (ulong)data[offset + 3] << 32
                | (ulong)data[offset + 4] << 24
                | (ulong)data[offset + 5] << 16
                | (ulong)data[offset + 6] << 8
                | data[offset + 7];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static short DecodeInt16(byte[] data, int offset, bool littleEndian)
            => unchecked((short)DecodeUInt16(data, offset, littleEndian));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int DecodeInt32(byte[] data, int offset, bool littleEndian)
            => unchecked((int)DecodeUInt32(data, offset, littleEndian));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long DecodeInt64(byte[] data, int offset, bool littleEndian)
            => unchecked((long)DecodeUInt64(data, offset, littleEndian));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe float DecodeFloat32(byte[] data, int offset, bool littleEndian)
        {
            var bits = DecodeUInt32(data, offset, littleEndian);
            return *(float*)&bits;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe double DecodeFloat64(byte[] data, int offset, bool littleEndian)
        {
            var bits = DecodeUInt64(data, offset, littleEndian);
            return *(double*)&bits;
        }

        private static int CheckedListCount(long count, DataType type)
        {
            if (count < 0 || count > int.MaxValue)
                throw new InvalidDataException($"PLY list count {count} encoded as {type} is outside [0, {int.MaxValue}].");
            return (int)count;
        }

        private static int CheckedListCount(ulong count, DataType type)
        {
            if (count > int.MaxValue)
                throw new InvalidDataException($"PLY list count {count} encoded as {type} is outside [0, {int.MaxValue}].");
            return (int)count;
        }

        private static int GetListByteCount(int listCount, int valueSize, Property property)
        {
            try
            {
                return checked(listCount * valueSize);
            }
            catch (OverflowException e)
            {
                throw new InvalidDataException($"PLY list property '{property.Name}' is too large.", e);
            }
        }

        private static void DecodeFixedProperty(
            PropertyData propertyData,
            byte[] source,
            int sourceOffset,
            int sourceStride,
            int count,
            bool littleEndian
            )
        {
            switch (propertyData.Property.DataType)
            {
                case DataType.Int8:
                {
                    var target = (sbyte[])propertyData.Data;
                    for (var i = 0; i < count; i++, sourceOffset += sourceStride) target[i] = unchecked((sbyte)source[sourceOffset]);
                    break;
                }
                case DataType.UInt8:
                {
                    var target = (byte[])propertyData.Data;
                    for (var i = 0; i < count; i++, sourceOffset += sourceStride) target[i] = source[sourceOffset];
                    break;
                }
                case DataType.Int16:
                {
                    var target = (short[])propertyData.Data;
                    for (var i = 0; i < count; i++, sourceOffset += sourceStride) target[i] = DecodeInt16(source, sourceOffset, littleEndian);
                    break;
                }
                case DataType.UInt16:
                {
                    var target = (ushort[])propertyData.Data;
                    for (var i = 0; i < count; i++, sourceOffset += sourceStride) target[i] = DecodeUInt16(source, sourceOffset, littleEndian);
                    break;
                }
                case DataType.Int32:
                {
                    var target = (int[])propertyData.Data;
                    for (var i = 0; i < count; i++, sourceOffset += sourceStride) target[i] = DecodeInt32(source, sourceOffset, littleEndian);
                    break;
                }
                case DataType.UInt32:
                {
                    var target = (uint[])propertyData.Data;
                    for (var i = 0; i < count; i++, sourceOffset += sourceStride) target[i] = DecodeUInt32(source, sourceOffset, littleEndian);
                    break;
                }
                case DataType.Int64:
                {
                    var target = (long[])propertyData.Data;
                    for (var i = 0; i < count; i++, sourceOffset += sourceStride) target[i] = DecodeInt64(source, sourceOffset, littleEndian);
                    break;
                }
                case DataType.UInt64:
                {
                    var target = (ulong[])propertyData.Data;
                    for (var i = 0; i < count; i++, sourceOffset += sourceStride) target[i] = DecodeUInt64(source, sourceOffset, littleEndian);
                    break;
                }
                case DataType.Float32:
                {
                    var target = (float[])propertyData.Data;
                    for (var i = 0; i < count; i++, sourceOffset += sourceStride) target[i] = DecodeFloat32(source, sourceOffset, littleEndian);
                    break;
                }
                case DataType.Float64:
                {
                    var target = (double[])propertyData.Data;
                    for (var i = 0; i < count; i++, sourceOffset += sourceStride) target[i] = DecodeFloat64(source, sourceOffset, littleEndian);
                    break;
                }
                default:
                    throw new InvalidDataException($"Unsupported PLY property type {propertyData.Property.DataType}.");
            }
        }

        private static unsafe void CopyNativeFixedProperty(
            PropertyData propertyData,
            byte[] source,
            int sourceOffset,
            int sourceStride,
            int count
            )
        {
            fixed (byte* sourceStart = &source[sourceOffset])
            {
                static void Copy<T>(byte* source, int stride, T[] target, int count) where T : unmanaged
                {
                    fixed (T* targetStart = &target[0])
                    {
                        var t = targetStart;
                        for (var i = 0; i < count; i++, source += stride, t++) *t = *(T*)source;
                    }
                }

                switch (propertyData.Property.DataType)
                {
                    case DataType.Int8   : Copy(sourceStart, sourceStride, (sbyte [])propertyData.Data, count); break;
                    case DataType.UInt8  : Copy(sourceStart, sourceStride, (byte  [])propertyData.Data, count); break;
                    case DataType.Int16  : Copy(sourceStart, sourceStride, (short [])propertyData.Data, count); break;
                    case DataType.UInt16 : Copy(sourceStart, sourceStride, (ushort[])propertyData.Data, count); break;
                    case DataType.Int32  : Copy(sourceStart, sourceStride, (int   [])propertyData.Data, count); break;
                    case DataType.UInt32 : Copy(sourceStart, sourceStride, (uint  [])propertyData.Data, count); break;
                    case DataType.Int64  : Copy(sourceStart, sourceStride, (long  [])propertyData.Data, count); break;
                    case DataType.UInt64 : Copy(sourceStart, sourceStride, (ulong [])propertyData.Data, count); break;
                    case DataType.Float32: Copy(sourceStart, sourceStride, (float [])propertyData.Data, count); break;
                    case DataType.Float64: Copy(sourceStart, sourceStride, (double[])propertyData.Data, count); break;
                    default: throw new InvalidDataException($"Unsupported PLY property type {propertyData.Property.DataType}.");
                }
            }
        }

        private static IEnumerable<ElementData> ParseElement(
            BinaryInput input,
            Element element,
            int maxChunkSize,
            bool littleEndian,
            Action<string>? log
            )
        {
            var totalStopwatch = Stopwatch.StartNew();
            var chunkStopwatch = new Stopwatch();
            var nativeEndian = littleEndian == BitConverter.IsLittleEndian;

            var propertiesCount = element.Properties.Count;
            var offsets = new int[propertiesCount];
            var offset = 0;
            for (var pi = 0; pi < propertiesCount; pi++)
            {
                offsets[pi] = offset;
                offset += DataTypeSizes[element.Properties[pi].DataType];
            }

            var rowCount = element.Count;
            for (var rowIndex = 0L; rowIndex < rowCount; rowIndex += maxChunkSize)
            {
                chunkStopwatch.Restart();

                var chunkRowCount = (int)Math.Min(rowCount - rowIndex, maxChunkSize);
                var perPropertyData = element.Properties.Select(p => new PropertyData(p, AllocArray(p, chunkRowCount))).ToArray();
                var chunkSizeInBytes = 0;

                if (element.ContainsListProperty)
                {
                    var parse = new Action<int>[propertiesCount];
                    for (var pi = 0; pi < propertiesCount; pi++)
                    {
                        var property = element.Properties[pi];
                        var target = perPropertyData[pi].Data;

                        if (property.IsListProperty)
                        {
                            var rawCount = new byte[8];

                            T ReadSwappedCount<T>(int byteCount, Func<byte[], int, bool, T> decode)
                            {
                                input.ReadExactly(rawCount, 0, byteCount);
                                return decode(rawCount, 0, littleEndian);
                            }

                            Func<int> readListCount = nativeEndian
                                ? property.ListCountType switch
                                {
                                    DataType.Int8   => () => CheckedListCount(unchecked((sbyte)input.ReadByte()), DataType.Int8),
                                    DataType.UInt8  => () => input.ReadByte(),
                                    DataType.Int16  => () => CheckedListCount(input.ReadNative<short>(), DataType.Int16),
                                    DataType.UInt16 => () => input.ReadNative<ushort>(),
                                    DataType.Int32  => () => CheckedListCount(input.ReadNative<int>(), DataType.Int32),
                                    DataType.UInt32 => () => CheckedListCount(input.ReadNative<uint>(), DataType.UInt32),
                                    DataType.Int64  => () => CheckedListCount(input.ReadNative<long>(), DataType.Int64),
                                    DataType.UInt64 => () => CheckedListCount(input.ReadNative<ulong>(), DataType.UInt64),
                                    _ => throw new InvalidDataException($"PLY list count type must be an integer, but found {property.ListCountType}.")
                                }
                                : property.ListCountType switch
                                {
                                    DataType.Int8   => () => CheckedListCount(unchecked((sbyte)input.ReadByte()), DataType.Int8),
                                    DataType.UInt8  => () => input.ReadByte(),
                                    DataType.Int16  => () => CheckedListCount(ReadSwappedCount(2, DecodeInt16), DataType.Int16),
                                    DataType.UInt16 => () => ReadSwappedCount(2, DecodeUInt16),
                                    DataType.Int32  => () => CheckedListCount(ReadSwappedCount(4, DecodeInt32), DataType.Int32),
                                    DataType.UInt32 => () => CheckedListCount(ReadSwappedCount(4, DecodeUInt32), DataType.UInt32),
                                    DataType.Int64  => () => CheckedListCount(ReadSwappedCount(8, DecodeInt64), DataType.Int64),
                                    DataType.UInt64 => () => CheckedListCount(ReadSwappedCount(8, DecodeUInt64), DataType.UInt64),
                                    _ => throw new InvalidDataException($"PLY list count type must be an integer, but found {property.ListCountType}.")
                                };

                            void ParseNativeList<T>(int row, int valueSize) where T : unmanaged
                            {
                                var listCount = readListCount();
                                var byteCount = GetListByteCount(listCount, valueSize, property);
                                input.EnsureAvailable(byteCount);

                                var values = new T[listCount];
                                input.ReadNativeArray(values, byteCount);
                                ((T[][])target)[row] = values;
                            }

                            void ParseSwappedList<T>(
                                int row,
                                int valueSize,
                                Func<byte[], int, bool, T> decode
                                ) where T : unmanaged
                            {
                                var listCount = readListCount();
                                var byteCount = GetListByteCount(listCount, valueSize, property);
                                input.EnsureAvailable(byteCount);

                                var raw = byteCount == 0 ? [] : new byte[byteCount];
                                input.ReadExactly(raw, 0, byteCount);
                                var values = new T[listCount];
                                for (var i = 0; i < listCount; i++) values[i] = decode(raw, i * valueSize, littleEndian);
                                ((T[][])target)[row] = values;
                            }

                            parse[pi] = nativeEndian
                                ? property.DataType switch
                                {
                                    DataType.Int8    => row => ParseNativeList<sbyte >(row, 1),
                                    DataType.UInt8   => row => ParseNativeList<byte  >(row, 1),
                                    DataType.Int16   => row => ParseNativeList<short >(row, 2),
                                    DataType.UInt16  => row => ParseNativeList<ushort>(row, 2),
                                    DataType.Int32   => row => ParseNativeList<int   >(row, 4),
                                    DataType.UInt32  => row => ParseNativeList<uint  >(row, 4),
                                    DataType.Int64   => row => ParseNativeList<long  >(row, 8),
                                    DataType.UInt64  => row => ParseNativeList<ulong >(row, 8),
                                    DataType.Float32 => row => ParseNativeList<float >(row, 4),
                                    DataType.Float64 => row => ParseNativeList<double>(row, 8),
                                    _ => throw new InvalidDataException($"Unsupported PLY list value type {property.DataType}.")
                                }
                                : property.DataType switch
                                {
                                    DataType.Int8    => row => ParseSwappedList(row, 1, (raw, i, _) => unchecked((sbyte)raw[i])),
                                    DataType.UInt8   => row => ParseSwappedList(row, 1, (raw, i, _) => raw[i]),
                                    DataType.Int16   => row => ParseSwappedList(row, 2, DecodeInt16),
                                    DataType.UInt16  => row => ParseSwappedList(row, 2, DecodeUInt16),
                                    DataType.Int32   => row => ParseSwappedList(row, 4, DecodeInt32),
                                    DataType.UInt32  => row => ParseSwappedList(row, 4, DecodeUInt32),
                                    DataType.Int64   => row => ParseSwappedList(row, 8, DecodeInt64),
                                    DataType.UInt64  => row => ParseSwappedList(row, 8, DecodeUInt64),
                                    DataType.Float32 => row => ParseSwappedList(row, 4, DecodeFloat32),
                                    DataType.Float64 => row => ParseSwappedList(row, 8, DecodeFloat64),
                                    _ => throw new InvalidDataException($"Unsupported PLY list value type {property.DataType}.")
                                };
                        }
                        else
                        {
                            var raw = new byte[8];

                            void ParseScalar<T>(int row, int byteCount, Func<byte[], int, bool, T> decode)
                            {
                                input.ReadExactly(raw, 0, byteCount);
                                ((T[])target)[row] = decode(raw, 0, littleEndian);
                            }

                            parse[pi] = nativeEndian
                                ? property.DataType switch
                                {
                                    DataType.Int8    => row => ((sbyte[])target)[row] = unchecked((sbyte)input.ReadByte()),
                                    DataType.UInt8   => row => ((byte[])target)[row] = input.ReadByte(),
                                    DataType.Int16   => row => ((short [])target)[row] = input.ReadNative<short>(),
                                    DataType.UInt16  => row => ((ushort[])target)[row] = input.ReadNative<ushort>(),
                                    DataType.Int32   => row => ((int   [])target)[row] = input.ReadNative<int>(),
                                    DataType.UInt32  => row => ((uint  [])target)[row] = input.ReadNative<uint>(),
                                    DataType.Int64   => row => ((long  [])target)[row] = input.ReadNative<long>(),
                                    DataType.UInt64  => row => ((ulong [])target)[row] = input.ReadNative<ulong>(),
                                    DataType.Float32 => row => ((float [])target)[row] = input.ReadNative<float>(),
                                    DataType.Float64 => row => ((double[])target)[row] = input.ReadNative<double>(),
                                    _ => throw new InvalidDataException($"Unsupported PLY scalar type {property.DataType}.")
                                }
                                : property.DataType switch
                                {
                                    DataType.Int8    => row => ((sbyte[])target)[row] = unchecked((sbyte)input.ReadByte()),
                                    DataType.UInt8   => row => ((byte[])target)[row] = input.ReadByte(),
                                    DataType.Int16   => row => ParseScalar(row, 2, DecodeInt16),
                                    DataType.UInt16  => row => ParseScalar(row, 2, DecodeUInt16),
                                    DataType.Int32   => row => ParseScalar(row, 4, DecodeInt32),
                                    DataType.UInt32  => row => ParseScalar(row, 4, DecodeUInt32),
                                    DataType.Int64   => row => ParseScalar(row, 8, DecodeInt64),
                                    DataType.UInt64  => row => ParseScalar(row, 8, DecodeUInt64),
                                    DataType.Float32 => row => ParseScalar(row, 4, DecodeFloat32),
                                    DataType.Float64 => row => ParseScalar(row, 8, DecodeFloat64),
                                    _ => throw new InvalidDataException($"Unsupported PLY scalar type {property.DataType}.")
                                };
                        }
                    }

                    for (var chunkRowIndex = 0; chunkRowIndex < chunkRowCount; chunkRowIndex++)
                    {
                        for (var pi = 0; pi < propertiesCount; pi++) parse[pi](chunkRowIndex);
                    }
                }
                else
                {
                    var rowSizeInBytes = GetSizeInBytes(element) ?? throw new InvalidDataException($"PLY element '{element.Name}' has no fixed record size.");
                    try
                    {
                        chunkSizeInBytes = checked(chunkRowCount * rowSizeInBytes);
                    }
                    catch (OverflowException e)
                    {
                        throw new InvalidDataException($"PLY chunk for element '{element.Name}' is too large.", e);
                    }

                    input.EnsureAvailable(chunkSizeInBytes);
                    var chunk = new byte[chunkSizeInBytes];
                    input.ReadExactly(chunk, 0, chunkSizeInBytes);

                    var tasks = new List<Task>(propertiesCount);
                    for (var pi = 0; pi < propertiesCount; pi++)
                    {
                        var propertyIndex = pi;
                        tasks.Add(Task.Run(() =>
                        {
                            if (nativeEndian)
                                CopyNativeFixedProperty(perPropertyData[propertyIndex], chunk, offsets[propertyIndex], rowSizeInBytes, chunkRowCount);
                            else
                                DecodeFixedProperty(perPropertyData[propertyIndex], chunk, offsets[propertyIndex], rowSizeInBytes, chunkRowCount, littleEndian);
                        }));
                    }
                    Task.WhenAll(tasks).Wait();
                }

                chunkStopwatch.Stop();
                if (log != null)
                {
                    var bps = chunkSizeInBytes / chunkStopwatch.Elapsed.TotalSeconds;
                    var vps = chunkRowCount / chunkStopwatch.Elapsed.TotalSeconds;
                    log($"[PlyParser] parsed {chunkSizeInBytes,16:N0} bytes in {chunkStopwatch.Elapsed.TotalSeconds,6:N3} s | {bps,16:N0} MiB/s | {chunkRowCount,10:N0} points | {vps,16:N0} points/s");
                }

                yield return new(element, [.. perPropertyData]);
            }

            totalStopwatch.Stop();
            log?.Invoke($"[PlyParser] total {totalStopwatch.Elapsed}");
        }

        public static Dataset Parse(Header header, Stream stream, int maxChunkSize, Action<string>? log)
        {
            if (maxChunkSize <= 0) throw new ArgumentOutOfRangeException(nameof(maxChunkSize));
            var littleEndian = header.Format switch
            {
                Format.BinaryLittleEndian => true,
                Format.BinaryBigEndian => false,
                _ => throw new InvalidDataException($"Expected binary PLY format, but found {header.Format}.")
            };

            var input = new BinaryInput(stream);
            var data = header.Elements.SelectMany(element => ParseElement(input, element, maxChunkSize, littleEndian, log));
            return new(header, data);
        }
    }

    public static Dataset Parse(Stream f, int maxChunkSize, Action<string>? log = null)
    {
        var header = ParseHeader(f, log);

        return header.Format switch
        {
            Format.BinaryLittleEndian => BinaryParser.Parse(header, f, maxChunkSize, log),
            Format.BinaryBigEndian    => BinaryParser.Parse(header, f, maxChunkSize, log),
            Format.Ascii              => AsciiParser.Parse(header, f, log),
            _ => throw new Exception($"Format {header.Format} is not supported. Error f7bf1121-fb1d-4fcd-844d-e43105d8fe79."),
        };
    }

    public static Dataset Parse(string filename, int maxChunkSize, Action<string>? log = null)
    {
        log?.Invoke($"[PlyParser] parsing file {filename} ({new FileInfo(filename).Length:N0} bytes)");
        var f = File.OpenRead(filename);
        return Parse(f, maxChunkSize, log);
    }
}
