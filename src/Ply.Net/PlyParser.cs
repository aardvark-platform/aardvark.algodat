﻿/*
   Copyright (C) 2018-2022. Stefan Maierhofer.

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
using System.IO.MemoryMappedFiles;
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

    private static string ReadLine(Stream stream, int maxLength)
    {
        var sb = new StringBuilder();
        var c = stream.ReadByte();

        while (c == '\n' || c == '\r') c = stream.ReadByte();

        while (c != -1 && c != '\n' && c != '\r' && sb.Length < maxLength)
        {
            sb.Append((char)c);
            c = stream.ReadByte();
        }
        return sb.ToString();
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

        return new(type, ts[1], count, ImmutableList<Property>.Empty);
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
            var datatype = ParseDataType(ts[3]);
            return new(datatype, Name: ts[4].ToLower(), listCountType);
        }
        else
        {
            Fail();
            throw new Exception(); // make compiler happy
        }
    }

    private static DataType ParseDataType(string s) => s switch
    {
        "char" => DataType.Int8,
        "int8" => DataType.Int8,
        "sbyte" => DataType.Int8,

        "uchar" => DataType.UInt8,
        "uint8" => DataType.UInt8,
        "ubyte" => DataType.UInt8,
        "byte" => DataType.UInt8,

        "short" => DataType.Int16,
        "int16" => DataType.Int16,

        "ushort" => DataType.UInt16,
        "uint16" => DataType.UInt16,

        "int" => DataType.Int32,
        "int32" => DataType.Int32,

        "uint" => DataType.UInt32,
        "uint32" => DataType.UInt32,

        "float" => DataType.Float32,
        "float32" => DataType.Float32,

        "double" => DataType.Float64,
        "float64" => DataType.Float64,

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

            log?.Invoke($"[WARNING] Unknown header entry: {line}");
        }

        if (currentElement != null) elements = elements.Add(currentElement);

        var header = new Header(format, elements, headerlines, DataOffset: f.Position);
        if (log != null)
        {
            foreach (var s in header.HeaderLines) log(s);
        }

        return header;
    }

    public static Header ParseHeader(string filename, Action<string>? log = null)
    {
        var f = File.OpenRead(filename);
        return ParseHeader(f, log);
    }

    #endregion

    private static class AsciiParser
    {
        private delegate void PropertyParser(int rowIndex, Row rowData);

        private class Row
        {
            private readonly string[] _ts;
            private int _next;
            public Row(string line) { _ts = line.SplitOnWhitespace(); _next = 0; }
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
                    log?.Invoke($"Failed to read next line. Premature end of element {element} after {i}/{element.Count} lines.");
                    break;
                }

                var line = new Row(l);
                for (var j = 0; j < element.Properties.Count; j++) parse[j](i, line);
            }

            return new(element, data.ToImmutableList());
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
            { DataType.Float32, 4 },
            { DataType.Float64, 8 }
        };

        /// <summary>
        /// Size of element in bytes, or null if element contains one or more list properties (there is no fixed size in this case).
        /// </summary>
        private static int? GetSizeInBytes(Element e) => e.ContainsListProperty ? null : e.Properties.Sum(p => DataTypeSizes[p.DataType]);

        private static Array AllocArray(Property p, int count) => (p.IsListProperty, p.DataType) switch
        {
            (false, DataType.Int8   ) => new sbyte [count], (true , DataType.Int8   ) => new sbyte [count][],
            (false, DataType.UInt8  ) => new byte  [count], (true , DataType.UInt8  ) => new byte  [count][],
            (false, DataType.Int16  ) => new short [count], (true , DataType.Int16  ) => new short [count][],
            (false, DataType.UInt16 ) => new ushort[count], (true , DataType.UInt16 ) => new ushort[count][],
            (false, DataType.Int32  ) => new int   [count], (true , DataType.Int32  ) => new int   [count][],
            (false, DataType.UInt32 ) => new uint  [count], (true , DataType.UInt32 ) => new uint  [count][],
            (false, DataType.Float32) => new float [count], (true , DataType.Float32) => new float [count][],
            (false, DataType.Float64) => new double[count], (true , DataType.Float64) => new double[count][],
            _ => throw new Exception($"Error 8ba1684a-21e9-4e6c-9767-74bca998df53. {p} not supported.")
        };

        private static IEnumerable<ElementData> ParseElement(Stream f, Element element, Action<string>? log)
        {
            const int MAX_CHUNK_ROWS = 16 * 1024 * 1024;

            var rows = element.Properties.Select(p => new PropertyData(p, AllocArray(p, element.Count))).ToArray();
            var rowsOffset = 0;

            var offsets = new int[element.Properties.Count];
            var offset = 0;
            for (var pi = 0; pi < element.Properties.Count; pi++)
            {
                offsets[pi] = offset; 
                offset += DataTypeSizes[element.Properties[pi].DataType];
            }

            if (element.ContainsListProperty)
            {
                throw new NotImplementedException();
            }
            else
            {
                var rowSizeInBytes = GetSizeInBytes(element) ?? throw new Exception($"Error 4f56dae5-94e8-4bfb-a290-0e20d1396ca6.");
                var maxChunkSizeInBytes = MAX_CHUNK_ROWS * rowSizeInBytes;
                var remainingBytes = (long)element.Count * rowSizeInBytes;

                var swTotal = new Stopwatch(); swTotal.Restart();
                var sw = new Stopwatch();
                while (remainingBytes > 0)
                {
                    var chunkRowCount = (int)Math.Min(remainingBytes / rowSizeInBytes, MAX_CHUNK_ROWS);
                    var chunkSizeInBytes = chunkRowCount * rowSizeInBytes;
                    remainingBytes -= chunkSizeInBytes;
                    if (remainingBytes < 0) throw new Exception("Error c9aef005-bd20-4320-abde-c03116b77c2e.");

                    sw.Restart();

                    var chunk = new byte[chunkSizeInBytes];
                    var c = f.Read(chunk, 0, chunkSizeInBytes);
                    if (c != chunkSizeInBytes) throw new Exception("Error a6d0f241-be6a-408b-baa5-5872b311b6a0.");

                    var tasks = new List<Task>();
                    for (var _pi = 0; _pi < element.Properties.Count; _pi++)
                    {
                        var pi = _pi;
                        void Process()
                        {
                            unsafe
                            {
                                fixed (byte* p0 = &chunk[0])
                                {
                                    var p = p0 + offsets[pi];
                                    var a = rows[pi].Data;

                                    static bool Copy<T>(byte* source, int sourceStride, Array target, int targetOffset, int count) where T : unmanaged
                                    {
                                        fixed (T* _t = &((T[])target)[0])
                                        {
                                            var t = _t + targetOffset;
                                            for (int i = 0; i < count; i++, source += sourceStride, t++) *t = *(T*)source;
                                        }
                                        return true;
                                    }

                                    _ = element.Properties[pi].DataType switch
                                    {
                                        DataType.Int8    => Copy<sbyte >(p, rowSizeInBytes, a, rowsOffset, chunkRowCount),
                                        DataType.UInt8   => Copy<byte  >(p, rowSizeInBytes, a, rowsOffset, chunkRowCount),
                                        DataType.Int16   => Copy<short >(p, rowSizeInBytes, a, rowsOffset, chunkRowCount),
                                        DataType.UInt16  => Copy<ushort>(p, rowSizeInBytes, a, rowsOffset, chunkRowCount),
                                        DataType.Int32   => Copy<int   >(p, rowSizeInBytes, a, rowsOffset, chunkRowCount),
                                        DataType.UInt32  => Copy<uint  >(p, rowSizeInBytes, a, rowsOffset, chunkRowCount),
                                        DataType.Float32 => Copy<float >(p, rowSizeInBytes, a, rowsOffset, chunkRowCount),
                                        DataType.Float64 => Copy<double>(p, rowSizeInBytes, a, rowsOffset, chunkRowCount),
                                        _ => throw new Exception()
                                    };
                                }
                            }
                        }

                        tasks.Add(Task.Run(Process));
                    }
                    Task.WhenAll(tasks).Wait();


                    rowsOffset += chunkRowCount;

                    sw.Stop();
                    if (log != null)
                    {
                        var bps = chunkSizeInBytes / sw.Elapsed.TotalSeconds;
                        var vps = chunkRowCount / sw.Elapsed.TotalSeconds;
                        log?.Invoke($"[PlyParser] {chunkSizeInBytes,16:N0} bytes | in {sw.Elapsed.TotalSeconds,6:N3} s | {bps,16:N0} MiB/s | {vps,16:N0} vertices/s");
                    }
                }
                swTotal.Stop();
                log?.Invoke($"[PlyParser] total {swTotal.Elapsed}");
                yield return new(element, rows.ToImmutableList());
            }
        }

        public static Dataset Parse(Header header, Stream f, Action<string>? log)
        {
            if (BitConverter.IsLittleEndian && header.Format == Format.BinaryBigEndian)
                throw new Exception("Parsing binary big endian on little endian machine is not supported.");

            if (!BitConverter.IsLittleEndian && header.Format == Format.BinaryLittleEndian)
                throw new Exception("Parsing binary little endian on big endian machine is not supported.");

            var data = header.Elements.SelectMany(e => ParseElement(f, e, log));
            return new(header, data);
        }
    }

    public static Dataset Parse(Stream f, Action<string>? log = null)
    {
        var header = ParseHeader(f, log);

        return header.Format switch
        {
            Format.BinaryLittleEndian => BinaryParser.Parse(header, f, log),
            Format.BinaryBigEndian    => BinaryParser.Parse(header, f, log),
            Format.Ascii              => AsciiParser.Parse(header, f, log),
            _ => throw new NotImplementedException($"Format {header.Format} not supported."),
        };
    }

    public static Dataset Parse(string filename, Action<string>? log = null)
    {
        log?.Invoke($"parsing file {filename} ({new FileInfo(filename).Length:N0} bytes)");
        var f = File.OpenRead(filename);
        return Parse(f, log);
    }
}