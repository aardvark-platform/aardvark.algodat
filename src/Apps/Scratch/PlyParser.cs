/*
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

using Aardvark.Base;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

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

    public enum ElementType
    {
        Vertex,
        Face,
        Edge,
        Material,
        Cell,
        UserDefined,
    }

    public enum DataType
    {
        Undefined,
        Int8, UInt8,
        Int16, UInt16,
        Int32, UInt32,
        Float32, Float64
    }

    public record Property(DataType DataType, string Name, DataType ListCountType)
    {
        public bool IsListProperty => ListCountType != DataType.Undefined;
    }

    public record Element(ElementType Type, string Name, int Count, ImmutableList<Property> Properties)
    {
        public Element Add(Property p) => this with { Properties = Properties.Add(p) };

        public int? GetPropertyIndex(string name)
        {
            for (var i = 0; i < Properties.Count; i++)
            {
                if (Properties[i].Name == name) return i;
            }
            return null;
        }

        public bool HasColor => GetPropertyIndex("red") != null && GetPropertyIndex("green") != null && GetPropertyIndex("blue") != null;
        public bool HasNormal => GetPropertyIndex("nx") != null && GetPropertyIndex("ny") != null && GetPropertyIndex("nz") != null;
        public bool HasIntensity => GetPropertyIndex("scalar_intensity") != null;
    }

    public record Header(Format Format, ImmutableList<Element> Elements, ImmutableList<string> HeaderLines, long DataOffset);

    public record Vertices(ImmutableDictionary<Property, object> Properties, IReadOnlyList<V3d> Positions, IReadOnlyList<C3b>? Colors, IReadOnlyList<V3f>? Normals, float[]? Intensities);

    public record Faces(int[][] PerFaceVertexIndices);

    public record Edges();

    public record Cells();

    public record Materials();

    public record UserDefined();

    public record Dataset(Vertices Vertices, Faces? Faces, Edges? Edges, Cells? Cells, Materials? Materials, ImmutableList<UserDefined> UserDefined);

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

        var type = ts[1] switch
        {
            "vertex" => ElementType.Vertex,
            "face" => ElementType.Face,
            "edge" => ElementType.Edge,
            "material" => ElementType.Material,
            "cell" => ElementType.Cell,
            _ => ElementType.UserDefined,
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

    #endregion

    private static class AsciiParser
    {
        private static Func<string[], V3d> CreateVertexPositionParser(Element e)
        {
            var ipx = e.GetPropertyIndex("x") ?? throw new Exception("Missing vertex property \"x\".");
            var ipy = e.GetPropertyIndex("y") ?? throw new Exception("Missing vertex property \"y\".");
            var ipz = e.GetPropertyIndex("z") ?? throw new Exception("Missing vertex property \"z\".");

            return ts =>
            {
                return new V3d(
                    double.Parse(ts[ipx], CultureInfo.InvariantCulture),
                    double.Parse(ts[ipy], CultureInfo.InvariantCulture),
                    double.Parse(ts[ipz], CultureInfo.InvariantCulture)
                    );
            };
        }

        private static Func<string[], V3f> CreateVertexNormalParser(Element e)
        {
            var ipnx = e.GetPropertyIndex("nx") ?? throw new Exception("Missing vertex property \"nx\".");
            var ipny = e.GetPropertyIndex("ny") ?? throw new Exception("Missing vertex property \"ny\".");
            var ipnz = e.GetPropertyIndex("nz") ?? throw new Exception("Missing vertex property \"nz\".");

            return ts =>
            {
                return new V3f(
                    float.Parse(ts[ipnx], CultureInfo.InvariantCulture),
                    float.Parse(ts[ipny], CultureInfo.InvariantCulture),
                    float.Parse(ts[ipnz], CultureInfo.InvariantCulture)
                    );
            };
        }

        private static Func<string[], C3b> CreateVertexColorParser(Element e)
        {
            var ir = e.GetPropertyIndex("red") ?? throw new Exception("Missing vertex property \"red\".");
            var ig = e.GetPropertyIndex("green") ?? throw new Exception("Missing vertex property \"green\".");
            var ib = e.GetPropertyIndex("blue") ?? throw new Exception("Missing vertex property \"blue\".");
            var pr = e.Properties[ir];
            var pg = e.Properties[ig];
            var pb = e.Properties[ib];

            return ts => 
            {
                var r = pr.DataType switch
                {
                    DataType.Int8 => byte.Parse(ts[ir]),
                    DataType.UInt8 => byte.Parse(ts[ir]),
                    _ => throw new NotImplementedException($"Color data type not supported: ({pr.DataType}, {pg.DataType}, {pb.DataType})"),
                };
                var g = pg.DataType switch
                {
                    DataType.Int8 => byte.Parse(ts[ig]),
                    DataType.UInt8 => byte.Parse(ts[ig]),
                    _ => throw new NotImplementedException($"Color data type not supported: ({pr.DataType}, {pg.DataType}, {pb.DataType})"),
                }; 
                var b = pb.DataType switch
                {
                    DataType.Int8 => byte.Parse(ts[ib]),
                    DataType.UInt8 => byte.Parse(ts[ib]),
                    _ => throw new NotImplementedException($"Color data type not supported: ({pr.DataType}, {pg.DataType}, {pb.DataType})"),
                };

                return new C3b(r, g, b);
            };
        }

        private static Func<string[], float> CreateVertexIntensityParser(Element e)
        {
            var isi = e.GetPropertyIndex("scalar_intensity") ?? throw new Exception("Missing vertex property \"scalar_intensity\".");
            return ts => float.Parse(ts[isi], CultureInfo.InvariantCulture);
        }

        private static Vertices ParseVertices(StreamReader f, Element element)
        {
            if (element.Type != ElementType.Vertex) throw new Exception($"Expected \"vertex\" element, but found \"{element.Name}\".");

            var hasColors = element.HasColor;
            var hasNormals = element.HasNormal;
            var hasIntensities = element.HasIntensity;

            var data = new object[element.Properties.Count];
            var parse = new Action<string[], int>[element.Properties.Count];
            var props = ImmutableDictionary<Property, object>.Empty;

            for (var i = 0; i < element.Properties.Count; i++)
            {
                var p = element.Properties[i];
                if (p.IsListProperty) throw new NotImplementedException();

                var ti = i;
                switch (p.DataType)
                {
                    case DataType.Int8:
                        {
                            var xs = new sbyte[element.Count];
                            data[i] = xs;
                            parse[i] = (ts, j) => xs[j] = sbyte.Parse(ts[ti]);
                            break;
                        }
                    case DataType.UInt8:
                        {
                            var xs = new byte[element.Count];
                            data[i] = xs;
                            parse[i] = (ts, j) => xs[j] = byte.Parse(ts[ti]);
                            break;
                        }
                    case DataType.Int16:
                        {
                            var xs = new short[element.Count];
                            data[i] = xs;
                            parse[i] = (ts, j) => xs[j] = short.Parse(ts[ti]);
                            break;
                        }
                    case DataType.UInt16:
                        {
                            var xs = new ushort[element.Count];
                            data[i] = xs;
                            parse[i] = (ts, j) => xs[j] = ushort.Parse(ts[ti]);
                            break;
                        }
                    case DataType.Int32:
                        {
                            var xs = new int[element.Count];
                            data[i] = xs;
                            parse[i] = (ts, j) => xs[j] = int.Parse(ts[ti]);
                            break;
                        }
                    case DataType.UInt32:
                        {
                            var xs = new uint[element.Count];
                            data[i] = xs;
                            parse[i] = (ts, j) => xs[j] = uint.Parse(ts[ti]);
                            break;
                        }
                    case DataType.Float32:
                        {
                            var xs = new float[element.Count];
                            data[i] = xs;
                            parse[i] = (ts, j) => xs[j] = float.Parse(ts[ti], CultureInfo.InvariantCulture);
                            break;
                        }
                    case DataType.Float64:
                        {
                            var xs = new double[element.Count];
                            data[i] = xs;
                            parse[i] = (ts, j) => xs[j] = double.Parse(ts[ti], CultureInfo.InvariantCulture);
                            break;
                        }
                    default:
                        {
                            throw new Exception($"Property data type {p.DataType} not supported.");
                        }
                }

                props = props.Add(p, data[i]);
            }

            var ps = new V3d[element.Count];
            var parsePosition = CreateVertexPositionParser(element);
            var cs = hasColors ? new C3b[element.Count] : null;
            var parseColor = hasColors ? CreateVertexColorParser(element) : null;
            var ns = hasNormals ? new V3f[element.Count] : null;
            var parseNormal = hasNormals ? CreateVertexNormalParser(element) : null;
            var js = hasIntensities ? new float[element.Count] : null;
            var parseIntensity = hasIntensities ? CreateVertexIntensityParser(element) : null;

            for (var i = 0; i < element.Count; i++)
            {
                var line = f.ReadLine();
                if (line == string.Empty) { i--; continue; }
                if (line == null) throw new Exception("Failed to read next line.");

                var ts = line.SplitOnWhitespace();

                for (var j = 0; j < element.Properties.Count; j++) parse[j](ts, i);

                ps[i] = parsePosition(ts);
                if (hasColors) cs![i] = parseColor!(ts);
                if (hasNormals) ns![i] = parseNormal!(ts);
                if (hasIntensities) js![i] = parseIntensity!(ts);
            }

            return new(props, ps, cs, ns, js);
        }

        private static Faces ParseFaces(StreamReader f, Element element, Action<string>? log)
        {
            if (element.Type != ElementType.Face) throw new Exception($"Expected \"face\" element, but found \"{element.Name}\".");

            var pfvia = new int[element.Count][];

            for (var i = 0; i < element.Count; i++)
            {
                var line = f.ReadLine();
                if (line == string.Empty) { i--; continue; }
                if (line == null)
                {
                    var msg = "Premature end of file.";
                    log?.Invoke(msg);
                    Report.Warn(msg);
                    break;
                }

                var ts = line.SplitOnWhitespace();

                var count = int.Parse(ts[0]);
                var via = new int[count];
                for (var j = 0; j < count;) via[j] = int.Parse(ts[++j]);
                pfvia[i] = via;
            }

            return new(pfvia);
        }

        private static Faces ParseEdges(StreamReader f, Element element)
        {
            if (element.Type != ElementType.Edge) throw new Exception($"Expected \"edge\" element, but found \"{element.Name}\".");
            throw new Exception($"Element type \"{element.Name}\" not supported.");
        }

        private static Cells ParseCells(StreamReader f, Element element)
        {
            if (element.Type != ElementType.Cell) throw new Exception($"Expected \"cell\" element, but found \"{element.Name}\".");
            throw new Exception($"Element type \"{element.Name}\" not supported.");
        }

        private static Materials ParseMaterials(StreamReader f, Element element)
        {
            if (element.Type != ElementType.Material) throw new Exception($"Expected \"material\" element, but found \"{element.Name}\".");
            throw new Exception($"Element type \"{element.Name}\" not supported.");
        }

        private static UserDefined ParseUserDefined(StreamReader f, Element element)
        {
            throw new Exception($"User defined element type \"{element.Name}\" not supported.");
        }

        public static Dataset Parse(Header header, Stream f, Action<string>? log)
        {
            Vertices? vertices = null;
            Faces? faces = null;
            Edges? edges = null;
            Cells? cells = null;
            Materials? materials = null;
            var userDefined = ImmutableList<UserDefined>.Empty;

            var sr = new StreamReader(f);

            foreach (var element in header.Elements)
            {
                switch (element.Type)
                {
                    case ElementType.Vertex: vertices = ParseVertices(sr, element); break;
                    case ElementType.Face: faces = ParseFaces(sr, element, log); break;
                    case ElementType.Edge: faces = ParseEdges(sr, element); break;
                    case ElementType.Cell: cells = ParseCells(sr, element); break;
                    case ElementType.Material: materials = ParseMaterials(sr, element); break;
                    case ElementType.UserDefined: userDefined = userDefined.Add(ParseUserDefined(sr, element)); break;
                    default: throw new Exception($"Element type {element.Type} not supported.");
                }
            }

            if (vertices == null) throw new Exception($"Missing vertex data.");

            return new(vertices, faces, edges, cells, materials, userDefined);
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

        private static Func<byte[], V3d> CreateVertexPositionParser(Element e, int[] offsets)
        {
            var ipx = e.GetPropertyIndex("x") ?? throw new Exception("Missing vertex property \"x\".");
            var ipy = e.GetPropertyIndex("y") ?? throw new Exception("Missing vertex property \"y\".");
            var ipz = e.GetPropertyIndex("z") ?? throw new Exception("Missing vertex property \"z\".");
            var ox = offsets[ipx]; var oy = offsets[ipy]; var oz = offsets[ipz];
            var px = e.Properties[ipx];
            var py = e.Properties[ipy];
            var pz = e.Properties[ipz];
            
            return (px.DataType, py.DataType, pz.DataType) switch
            {
                (DataType.Float32, DataType.Float32, DataType.Float32) => buffer => new V3d(BitConverter.ToSingle(buffer, ox), BitConverter.ToSingle(buffer, oy), BitConverter.ToSingle(buffer, oz)),
                (DataType.Float32, DataType.Float32, DataType.Float64) => buffer => new V3d(BitConverter.ToSingle(buffer, ox), BitConverter.ToSingle(buffer, oy), BitConverter.ToDouble(buffer, oz)),
                (DataType.Float32, DataType.Float64, DataType.Float32) => buffer => new V3d(BitConverter.ToSingle(buffer, ox), BitConverter.ToDouble(buffer, oy), BitConverter.ToSingle(buffer, oz)),
                (DataType.Float32, DataType.Float64, DataType.Float64) => buffer => new V3d(BitConverter.ToSingle(buffer, ox), BitConverter.ToDouble(buffer, oy), BitConverter.ToDouble(buffer, oz)),
                (DataType.Float64, DataType.Float32, DataType.Float32) => buffer => new V3d(BitConverter.ToDouble(buffer, ox), BitConverter.ToSingle(buffer, oy), BitConverter.ToSingle(buffer, oz)),
                (DataType.Float64, DataType.Float32, DataType.Float64) => buffer => new V3d(BitConverter.ToDouble(buffer, ox), BitConverter.ToSingle(buffer, oy), BitConverter.ToDouble(buffer, oz)),
                (DataType.Float64, DataType.Float64, DataType.Float32) => buffer => new V3d(BitConverter.ToDouble(buffer, ox), BitConverter.ToDouble(buffer, oy), BitConverter.ToSingle(buffer, oz)),
                (DataType.Float64, DataType.Float64, DataType.Float64) => buffer => new V3d(BitConverter.ToDouble(buffer, ox), BitConverter.ToDouble(buffer, oy), BitConverter.ToDouble(buffer, oz)),
                _ => throw new NotImplementedException($"Position data type not supported: ({px.DataType}, {py.DataType}, {pz.DataType})")
            };
        }

        private static Func<byte[], V3f> CreateVertexNormalParser(Element e, int[] offsets)
        {
            var ipnx = e.GetPropertyIndex("nx") ?? throw new Exception("Missing vertex property \"nx\".");
            var ipny = e.GetPropertyIndex("ny") ?? throw new Exception("Missing vertex property \"ny\".");
            var ipnz = e.GetPropertyIndex("nz") ?? throw new Exception("Missing vertex property \"nz\".");
            var onx = offsets[ipnx]; var ony = offsets[ipny]; var onz = offsets[ipnz];
            var pnx = e.Properties[ipnx];
            var pny = e.Properties[ipny];
            var pnz = e.Properties[ipnz];
            return (pnx.DataType, pny.DataType, pnz.DataType) switch
            {
                (DataType.Float32, DataType.Float32, DataType.Float32) => buffer => new V3f(BitConverter.ToSingle(buffer, onx), BitConverter.ToSingle(buffer, ony), BitConverter.ToSingle(buffer, onz)),
                (DataType.Float32, DataType.Float32, DataType.Float64) => buffer => new V3f(BitConverter.ToSingle(buffer, onx), BitConverter.ToSingle(buffer, ony), BitConverter.ToDouble(buffer, onz)),
                (DataType.Float32, DataType.Float64, DataType.Float32) => buffer => new V3f(BitConverter.ToSingle(buffer, onx), BitConverter.ToDouble(buffer, ony), BitConverter.ToSingle(buffer, onz)),
                (DataType.Float32, DataType.Float64, DataType.Float64) => buffer => new V3f(BitConverter.ToSingle(buffer, onx), BitConverter.ToDouble(buffer, ony), BitConverter.ToDouble(buffer, onz)),
                (DataType.Float64, DataType.Float32, DataType.Float32) => buffer => new V3f(BitConverter.ToDouble(buffer, onx), BitConverter.ToSingle(buffer, ony), BitConverter.ToSingle(buffer, onz)),
                (DataType.Float64, DataType.Float32, DataType.Float64) => buffer => new V3f(BitConverter.ToDouble(buffer, onx), BitConverter.ToSingle(buffer, ony), BitConverter.ToDouble(buffer, onz)),
                (DataType.Float64, DataType.Float64, DataType.Float32) => buffer => new V3f(BitConverter.ToDouble(buffer, onx), BitConverter.ToDouble(buffer, ony), BitConverter.ToSingle(buffer, onz)),
                (DataType.Float64, DataType.Float64, DataType.Float64) => buffer => new V3f(BitConverter.ToDouble(buffer, onx), BitConverter.ToDouble(buffer, ony), BitConverter.ToDouble(buffer, onz)),
                _ => throw new NotImplementedException($"Normal data type not supported: ({pnx.DataType}, {pny.DataType}, {pnz.DataType})")
            };
        }

        private static Func<byte[], C3b> CreateVertexColorParser(Element e, int[] offsets)
        {
            var ir = e.GetPropertyIndex("red") ?? throw new Exception("Missing vertex property \"red\".");
            var ig = e.GetPropertyIndex("green") ?? throw new Exception("Missing vertex property \"green\".");
            var ib = e.GetPropertyIndex("blue") ?? throw new Exception("Missing vertex property \"blue\".");
            var or = offsets[ir]; var og = offsets[ig]; var ob = offsets[ib];
            var pr = e.Properties[ir];
            var pg = e.Properties[ig];
            var pb = e.Properties[ib];
            return (pr.DataType, pg.DataType, pb.DataType) switch
            {
                (DataType.UInt8, DataType.UInt8, DataType.UInt8)    => buffer => new C3b(buffer[or], buffer[og], buffer[ob]),
                (DataType.UInt8, DataType.UInt8, DataType.Int8)     => buffer => new C3b(buffer[or], buffer[og], buffer[ob]),
                (DataType.UInt8, DataType.Int8, DataType.UInt8)     => buffer => new C3b(buffer[or], buffer[og], buffer[ob]),
                (DataType.UInt8, DataType.Int8, DataType.Int8)      => buffer => new C3b(buffer[or], buffer[og], buffer[ob]),
                (DataType.Int8, DataType.UInt8, DataType.UInt8)     => buffer => new C3b(buffer[or], buffer[og], buffer[ob]),
                (DataType.Int8, DataType.UInt8, DataType.Int8)      => buffer => new C3b(buffer[or], buffer[og], buffer[ob]),
                (DataType.Int8, DataType.Int8, DataType.UInt8)      => buffer => new C3b(buffer[or], buffer[og], buffer[ob]),
                (DataType.Int8, DataType.Int8, DataType.Int8)       => buffer => new C3b(buffer[or], buffer[og], buffer[ob]),
                _ => throw new NotImplementedException($"Color data type not supported: ({pr.DataType}, {pg.DataType}, {pb.DataType})")
            };
        }

        private static Func<byte[], float> CreateVertexIntensityParser(Element e, int[] offsets)
        {
            var isi = e.GetPropertyIndex("scalar_intensity") ?? throw new Exception("Missing vertex property \"scalar_intensity\".");
            var osi = offsets[isi];
            var psi = e.Properties[osi];
            return (psi.DataType) switch
            {
                DataType.Int8 => buffer => buffer[osi],
                DataType.UInt8 => buffer => buffer[osi],
                DataType.Int16 => buffer => BitConverter.ToInt16(buffer, osi),
                DataType.UInt16 => buffer => BitConverter.ToUInt16(buffer, osi),
                DataType.Int32 => buffer => BitConverter.ToInt32(buffer, osi),
                DataType.UInt32 => buffer => BitConverter.ToUInt32(buffer, osi),
                DataType.Float32 => buffer => BitConverter.ToSingle(buffer, osi),
                DataType.Float64 => buffer => (float)BitConverter.ToDouble(buffer, osi),
                _ => throw new NotImplementedException($"Scalar intensity data type not supported: {psi.DataType}")
            };
        }

        private static unsafe Func<Stream, int[]> CreateFaceParser(Element e)
        {
            if (e.Properties.Count != 1 || !e.Properties[0].IsListProperty) throw new Exception("Face properties not supported. Expected exactly 1 list property.");
            
            Func<Stream, int> parseCount = e.Properties[0].ListCountType switch
            {
                DataType.Int8   => f => f.ReadByte(),
                DataType.UInt8  => f => f.ReadByte(),
                DataType.Int16  => f => { Span<byte> buffer = stackalloc byte[2]; f.Read(buffer); return BitConverter.ToInt16(buffer); },
                DataType.UInt16 => f => { Span<byte> buffer = stackalloc byte[2]; f.Read(buffer); return BitConverter.ToUInt16(buffer); },
                DataType.Int32  => f => { Span<byte> buffer = stackalloc byte[4]; f.Read(buffer); return BitConverter.ToInt32(buffer); },
                DataType.UInt32 => f => { Span<byte> buffer = stackalloc byte[4]; f.Read(buffer); return (int)BitConverter.ToUInt32(buffer); },
                _ => throw new NotImplementedException($"List count type not supported: {e.Properties[0].ListCountType}")
            };

            Func<Stream, int, int[]> parseData = e.Properties[0].DataType switch
            {
                DataType.Int8   => (f, c) => { var buffer = new byte[c * 1]; f.Read(buffer); return buffer.Map(x => (int)x); },
                DataType.UInt8  => (f, c) => { var buffer = new byte[c * 1]; f.Read(buffer); return buffer.Map(x => (int)x); },
                DataType.Int16  => (f, c) => { Span<byte> buffer = stackalloc byte[c * 2]; f.Read(buffer); return MemoryMarshal.Cast<byte, short>(buffer).Map(x => (int)x); },
                DataType.UInt16 => (f, c) => { Span<byte> buffer = stackalloc byte[c * 2]; f.Read(buffer); return MemoryMarshal.Cast<byte, ushort>(buffer).Map(x => (int)x); },
                DataType.Int32  => (f, c) => { Span<byte> buffer = stackalloc byte[c * 4]; f.Read(buffer); return MemoryMarshal.Cast<byte, int>(buffer).Map(x => (int)x); },
                DataType.UInt32 => (f, c) => { Span<byte> buffer = stackalloc byte[c * 4]; f.Read(buffer); return MemoryMarshal.Cast<byte, uint>(buffer).Map(x => (int)x); },
                _ => throw new NotImplementedException($"List data type not supported: {e.Properties[0].ListCountType}")
            };

            return f =>
            {
                var count = parseCount(f);
                var ia = parseData(f, count);
                return ia;
            };
        }

        private static Vertices ParseVertices(Stream f, Element element)
        {
            if (element.Type != ElementType.Vertex) throw new Exception($"Expected \"vertex\" element, but found \"{element.Name}\".");

            var hasColors = element.HasColor;
            var hasNormals = element.HasNormal;
            var hasIntensities = element.HasIntensity;

            var data = new object[element.Properties.Count];
            var parse = new Action<byte[], int>[element.Properties.Count];
            var props = ImmutableDictionary<Property, object>.Empty;

            var offsets = new int[element.Properties.Count];
            var offset = 0;
            for (var i = 0; i < element.Properties.Count; i++)
            {
                var p = element.Properties[i];
                if (p.IsListProperty) throw new NotImplementedException();
                
                offsets[i] = offset;
                var o = offset;
                offset += DataTypeSizes[p.DataType];

                var ti = i;
                switch (p.DataType)
                {
                    case DataType.Int8:
                        {
                            var xs = new sbyte[element.Count];
                            data[i] = xs;
                            parse[i] = (buffer, j) => xs[j] = (sbyte)buffer[o];
                            break;
                        }
                    case DataType.UInt8:
                        {
                            var xs = new byte[element.Count];
                            data[i] = xs;
                            parse[i] = (buffer, j) => xs[j] = buffer[o];
                            break;
                        }
                    case DataType.Int16:
                        {
                            var xs = new short[element.Count];
                            data[i] = xs;
                            parse[i] = (buffer, j) => xs[j] = BitConverter.ToInt16(buffer, o);
                            break;
                        }
                    case DataType.UInt16:
                        {
                            var xs = new ushort[element.Count];
                            data[i] = xs;
                            parse[i] = (buffer, j) => xs[j] = BitConverter.ToUInt16(buffer, o);
                            break;
                        }
                    case DataType.Int32:
                        {
                            var xs = new int[element.Count];
                            data[i] = xs;
                            parse[i] = (buffer, j) => xs[j] = BitConverter.ToInt32(buffer, o);
                            break;
                        }
                    case DataType.UInt32:
                        {
                            var xs = new uint[element.Count];
                            data[i] = xs;
                            parse[i] = (buffer, j) => xs[j] = BitConverter.ToUInt32(buffer, o);
                            break;
                        }
                    case DataType.Float32:
                        {
                            var xs = new float[element.Count];
                            data[i] = xs;
                            parse[i] = (buffer, j) => xs[j] = BitConverter.ToSingle(buffer, o);
                            break;
                        }
                    case DataType.Float64:
                        {
                            var xs = new double[element.Count];
                            data[i] = xs;
                            parse[i] = (buffer, j) => xs[j] = BitConverter.ToDouble(buffer, o);
                            break;
                        }
                    default:
                        {
                            throw new Exception($"Property data type {p.DataType} not supported.");
                        }
                }

                props = props.Add(p, data[i]);
            }

            var ps = new List<V3d>();
            var parsePosition = CreateVertexPositionParser(element, offsets);
            var cs = hasColors ? new List<C3b>() : null;
            var parseColor = hasColors ? CreateVertexColorParser(element, offsets) : null;
            var ns = hasNormals ? new List<V3f>() : null;
            var parseNormal = hasNormals ? CreateVertexNormalParser(element, offsets) : null;
            var js = hasIntensities ? new float[element.Count] : null;
            var parseIntensity = hasIntensities ? CreateVertexIntensityParser(element, offsets) : null;

            var bufferSize = element.Properties.Sum(p => DataTypeSizes[p.DataType]);
            var buffer = new byte[bufferSize];
            for (var i = 0; i < element.Count; i++)
            {
                var c = f.Read(buffer, 0, bufferSize);
                if (c != bufferSize) throw new Exception("");

                for (var j = 0; j < element.Properties.Count; j++) parse[j](buffer, i);

                ps.Add(parsePosition(buffer));
                if (hasColors) cs!.Add(parseColor!(buffer));
                if (hasNormals) ns!.Add(parseNormal!(buffer));
                if (hasIntensities) js![i] = parseIntensity!(buffer);
            }

            return new(props, ps, cs, ns, js);
        }

        private static Faces ParseFaces(Stream f, Element element)
        {
            if (element.Type != ElementType.Face) throw new Exception($"Expected \"face\" element, but found \"{element.Name}\".");

            var fvia = new int[element.Count][];
            var parseFace = CreateFaceParser(element);
            for (var i = 0; i < element.Count; i++)
            {
                fvia[i] = parseFace(f);
            }
            return new(fvia);
        }

        private static Faces ParseEdges(Stream f, Element element)
        {
            if (element.Type != ElementType.Edge) throw new Exception($"Expected \"edge\" element, but found \"{element.Name}\".");
            throw new Exception($"Element type \"{element.Name}\" not supported.");
        }

        private static Cells ParseCells(Stream f, Element element)
        {
            if (element.Type != ElementType.Cell) throw new Exception($"Expected \"cell\" element, but found \"{element.Name}\".");
            throw new Exception($"Element type \"{element.Name}\" not supported.");
        }

        private static Materials ParseMaterials(Stream f, Element element)
        {
            if (element.Type != ElementType.Material) throw new Exception($"Expected \"material\" element, but found \"{element.Name}\".");
            throw new Exception($"Element type \"{element.Name}\" not supported.");
        }

        private static UserDefined ParseUserDefined(Stream f, Element element)
        {
            throw new Exception($"User defined element type \"{element.Name}\" not supported.");
        }

        public static Dataset Parse(Header header, Stream f, Action<string>? log)
        {
            if (BitConverter.IsLittleEndian && header.Format == Format.BinaryBigEndian)
                throw new Exception("Parsing binary big endian on little endian machine is not supported.");

            if (!BitConverter.IsLittleEndian && header.Format == Format.BinaryLittleEndian)
                throw new Exception("Parsing binary little endian on big endian machine is not supported.");

            Vertices?   vertices    = null;
            Faces?      faces       = null;
            Edges?      edges       = null;
            Cells?      cells       = null;
            Materials?  materials   = null;
            var         userDefined = ImmutableList<UserDefined>.Empty;

            foreach (var element in header.Elements)
            {
                switch (element.Type)
                {
                    case ElementType.Vertex     : vertices      = ParseVertices(f, element); break;
                    case ElementType.Face       : faces         = ParseFaces(f, element); break;
                    case ElementType.Edge       : faces         = ParseEdges(f, element); break;
                    case ElementType.Cell       : cells         = ParseCells(f, element); break;
                    case ElementType.Material   : materials     = ParseMaterials(f, element); break;
                    case ElementType.UserDefined: userDefined   = userDefined.Add(ParseUserDefined(f, element)); break;
                    default                     : throw new Exception($"Element type {element.Type} not supported.");
                }
            }

            if (vertices == null) throw new Exception($"Missing vertex data.");

            return new(vertices, faces, edges, cells, materials, userDefined);
        }
    }

    public static Header ParseHeader(Stream f, Action<string>? log)
    {
        #region check for magic bytes

        var magic = new byte[3];
        if (f.Read(magic) != 3 || (char)magic[0] != 'p' || (char)magic[1] != 'l' || (char)magic[2] != 'y')
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

            Report.Warn($"Unknown header entry:\n{line}");
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
        log?.Invoke($"parsing file {filename}");
        var f = File.OpenRead(filename);
        return Parse(f, log);
    }

    private static R[] Map<T, R>(this Span<T> xs, Func<T, R> map)
    {
        var rs = new R[xs.Length];
        for (var i = 0; i < xs.Length; i++) rs[i] = map(xs[i]);
        return rs;
    }
}
