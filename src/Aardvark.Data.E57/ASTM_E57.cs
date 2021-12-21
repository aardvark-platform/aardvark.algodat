/*
    Copyright (C) 2006-2020. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;
using Aardvark.Base;

namespace Aardvark.Data.E57
{
#pragma warning disable 1591
#pragma warning disable IDE0051 // Remove unused private members

    /// <summary>
    /// E57 (ASTM E2807-11)
    /// Standard Specification for 3D Imaging Data Exchange, Version 1.0.
    /// https://www.astm.org/Standards/E2807.htm
    /// </summary>
    public static class ASTM_E57
    {
        private const int E57_BLOB_SECTION = 0;
        private const int E57_COMPRESSED_VECTOR_SECTION = 1;
        private const int E57_INDEX_PACKET = 0;
        private const int E57_DATA_PACKET = 1;
        private const int E57_IGNORED_PACKET = 2;

        /// <summary>
        /// Physical E57 file offset.
        /// </summary>
        public struct E57PhysicalOffset
        {
            public readonly long Value;
            public E57PhysicalOffset(long value) => Value
                = (value % 1024 < 1020) ? value : throw new ArgumentException($"E57PhysicalOffset must not point to checksum bytes. ({value}).");

            public static E57PhysicalOffset operator +(E57PhysicalOffset a, E57PhysicalOffset b) => new(a.Value + b.Value);
            public static E57LogicalOffset operator +(E57PhysicalOffset a, E57LogicalOffset b) => (E57LogicalOffset)a + b;
            public static explicit operator E57LogicalOffset(E57PhysicalOffset a) => new(a.Value - ((a.Value >> 8) & ~0b11));
            public override string ToString() => $"E57PhysicalOffset({Value})";
        }

        /// <summary>
        /// Logical E57 file offset.
        /// </summary>
        public struct E57LogicalOffset
        {
            public readonly long Value;
            public E57LogicalOffset(long value) { Value = value; }
            public static E57LogicalOffset operator +(E57LogicalOffset a, E57LogicalOffset b) => new(a.Value + b.Value);
            public static E57LogicalOffset operator +(E57LogicalOffset a, int b) => new(a.Value + b);
            public static explicit operator E57PhysicalOffset(E57LogicalOffset a) => new(a.Value + (a.Value / 1020 * 4));
            public override string ToString() => $"E57LogicalOffset({Value})";
        }

        /// <summary>
        /// E57 File Header (7. File Header Section).
        /// TABLE 1 Format of the E57 File Header Section.
        /// </summary>
        public class E57FileHeader
        {
            public string FileSignature { get; private set; }
            public uint VersionMajor { get; private set; }
            public uint VersionMinor { get; private set; }
            public ulong FileLength { get; private set; }
            public E57PhysicalOffset XmlOffset { get; private set; }
            public ulong XmlLength { get; private set; }
            public ulong PageSize { get; private set; }
            public XElement RawXml { get; private set; }
            public E57Root E57Root { get; private set; }

            public static E57FileHeader Parse(Stream stream, long actualFileSizeInBytes, bool verbose)
            {
                stream.Position = 0;
                var buffer = new byte[48];
                if (stream.Read(buffer, 0, 48) != 48) throw new InvalidOperationException();
                
                var h = new E57FileHeader
                {
                    FileSignature = Check("FileSignature", Encoding.ASCII.GetString(buffer, 0, 8), x => x == "ASTM-E57", "ASTM-E57"),
                    VersionMajor = Check("VersionMajor", BitConverter.ToUInt32(buffer, 8), x => x == 1, "1"),
                    VersionMinor = Check("VersionMinor", BitConverter.ToUInt32(buffer, 12), x => x == 0, "0"),
                    FileLength = BitConverter.ToUInt64(buffer, 16),
                    XmlOffset = new E57PhysicalOffset(BitConverter.ToInt64(buffer, 24)),
                    XmlLength = BitConverter.ToUInt64(buffer, 32),
                    PageSize = Check("PageSize", BitConverter.ToUInt64(buffer, 40), x => x == 1024, "1024"),
                };

                if (h.FileLength != (ulong)actualFileSizeInBytes) throw new Exception(
                    $"[E57] According to the E57 file header, file size should be {h.FileLength:N0} bytes, but it is {actualFileSizeInBytes:N0} bytes.");

                var xmlBuffer = ReadLogicalBytes(stream, h.XmlOffset, (int)h.XmlLength);
                var xmlString = Encoding.UTF8.GetString(xmlBuffer);
                h.RawXml = XElement.Parse(xmlString, LoadOptions.SetBaseUri);
                h.E57Root = E57Root.Parse(h.RawXml, stream, verbose);

                //Console.WriteLine(h.RawXml);

                return h;
            }
        }

        public enum E57ElementType
        {
            Integer,                        // Table 2
            ScaledInteger,                  // Table 3
            Float,                          // Table 4
            String,                         // Table 5
            Blob,                           // Table 6
            Structure,                      // Table 7
            Vector,                         // Table 8
            CompressedVector,               // Table 9, Table 10
            E57Codec,                       // Table 11
            E57Root,                        // Table 12
            Data3D,                         // Table 13
            PointRecord,                    // Table 14
            PointGroupingSchemes,           // Table 15
            GroupingByLine,                 // Table 16
            LineGroupRecord,                // Table 17
            RigidBodyTransform,             // Table 18
            Quaternion,                     // Table 19
            Translation,                    // Table 20
            Image2d,                        // Table 21
            VisualReferenceRepresentation,  // Table 22
            PinholeRepresentation,          // Table 23
            SphericalRepresentation,        // Table 24
            CylindricalRepresentation,      // Table 25
            CartesianBounds,                // Table 26
            SphericalBounds,                // Table 27
            IndexBounds,                    // Table 28
            IntensityLimits,                // Table 29
            ColorLimits,                    // Table 30
            E57DateTime,                    // Table 31
        }

        public interface IE57Element
        {
            E57ElementType E57Type { get; }
        }
        
        public interface IBitPack : IE57Element
        {
            int NumberOfBitsForBitPack { get; }
            string Semantic { get; }
        }

        /// <summary>
        /// TABLE 2 Attributes for an Integer Type E57 Element.
        /// </summary>
        public class E57Integer : IE57Element, IBitPack
        {
            public E57ElementType E57Type => E57ElementType.Integer;

#region Properties
            
            public string Name;

            /// <summary>
            /// Optional. Default –2^63.
            /// The smallest value that can be encoded. Shall be in the interval [-2^63, 2^63-1].
            /// </summary>
            public long? Minimum;

            /// <summary>
            /// Optional. Default 2^63-1.
            /// The largest value that can be encoded. Shall be in the interval [minimum, 2^63-1].
            /// </summary>
            public long? Maximum;
            
            public long Value;
            
            public int NumberOfBitsForBitPack => (int)Math.Ceiling(((double)(Maximum - Minimum + 1)).Log2());
            
            public string Semantic => Name;

#endregion

            internal static E57Integer Parse(XElement root)
            {
                if (root == null) return null;
                EnsureElementType(root, "Integer");
                var value = string.IsNullOrWhiteSpace(root.Value) ? 0 : long.Parse(root.Value);
                var min = TryGetLongAttribute(root, "minimum");
                var max = TryGetLongAttribute(root, "maximum");
                if (max < min) Ex("Integer.maximum", $">= minimum ({min})", $"{max}");
                return new E57Integer { Name = root.Name.LocalName, Minimum = min, Maximum = max, Value = value };
            }
        }

        /// <summary>
        /// TABLE 3 Attributes for a ScaledInteger Type E57 Element.
        /// </summary>
        public class E57ScaledInteger : IE57Element, IBitPack
        {
            public E57ElementType E57Type => E57ElementType.ScaledInteger;

            #region Properties
            
            public string Name;

            /// <summary>
            /// Optional. Default –2^63.
            /// The smallest rawValue that can be encoded. Shall be in the interval [-2^63, 2^63-1].
            /// </summary>
            public long? Minimum;

            /// <summary>
            /// Optional. Default 2^63-1.
            /// The largest rawValue that can be encoded. Shall be in the interval [minimum, 2^63-1].
            /// </summary>
            public long? Maximum;

            /// <summary>
            /// Optional. Default 1.0.
            /// The scale value for the ScaledInteger. Shall be non-zero.
            /// </summary>
            public double Scale;

            /// <summary>
            /// Optional. Default 0.0.
            /// The offset value for the ScaledInteger.
            /// </summary>
            public double Offset;

            /// <summary>
            /// Stored integer value.
            /// </summary>
            public long RawValue;

            /// <summary>
            /// Value = RawValue * Scale + Offset.
            /// </summary>
            public double Value;
            
            public int NumberOfBitsForBitPack => (int)Math.Ceiling(((double)(Maximum - Minimum + 1)).Log2());
            
            public string Semantic => Name;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public double Compute(uint rawValue) => (Minimum.HasValue ? (Minimum.Value + rawValue) : rawValue) * Scale + Offset;

            #endregion

            internal static E57ScaledInteger Parse(XElement root)
            {
                if (root == null) return null;
                EnsureElementType(root, "ScaledInteger");
                var raw = string.IsNullOrWhiteSpace(root.Value) ? 0 : long.Parse(root.Value);
                var min = TryGetLongAttribute(root, "minimum");
                var max = TryGetLongAttribute(root, "maximum");
                if (max < min) Ex("ScaledInteger.maximum", $">= minimum ({min})", $"{max}");
                var scale = TryGetFloatAttribute(root, "scale") ?? 1.0;
                var offset = TryGetFloatAttribute(root, "offset") ?? 0.0;
                return new E57ScaledInteger
                {
                    Name = root.Name.LocalName,
                    Minimum = min, Maximum = max, Scale = scale, Offset = offset,
                    RawValue = raw, Value = raw * scale + offset
                };
            }
        }

        /// <summary>
        /// TABLE 4 Attributes for a Float Type E57 Element.
        /// </summary>
        public class E57Float : IE57Element, IBitPack
        {
            public E57ElementType E57Type => E57ElementType.Float;

            #region Properties
            
            public string Name;

            /// <summary>
            /// True for 64 bit floating point values (IEEE 754-1985), false for 32 bit (IEEE 754-1985).
            /// </summary>
            public bool IsDoublePrecision;

            /// <summary>
            /// Optional. Default double.MinValue.
            /// The smallest value that can be encoded.
            /// </summary>
            public double? Minimum;

            /// <summary>
            /// Optional. Default double.MaxValue.
            /// The largest value that can be encoded.
            /// </summary>
            public double? Maximum;
            
            public double Value;
            
            public int NumberOfBitsForBitPack => IsDoublePrecision ? 64 : 32;
            
            public string Semantic => Name;

            #endregion

            internal static E57Float Parse(XElement root)
            {
                if (root == null) return null;
                EnsureElementType(root, "Float");
                var value = string.IsNullOrWhiteSpace(root.Value) ? 0 : double.Parse(root.Value, CultureInfo.InvariantCulture);
                var precision = root.Attribute("precision")?.Value;
                var isDoublePrecision = (precision == null || precision == "double")
                    || (precision != "single" && Ex<bool>("precision", "['double', 'single']", precision))
                    ;
                var min = TryGetFloatAttribute(root, "minimum");
                var max = TryGetFloatAttribute(root, "maximum");
                if (max < min) Ex("Float.maximum", $">= minimum ({min})", $"{max}");
                return new E57Float { Name = root.Name.LocalName, IsDoublePrecision = isDoublePrecision, Minimum = min, Maximum = max, Value = value };
            }
        }

        /// <summary>
        /// TABLE 4 Attributes for a Float Type E57 Element.
        /// </summary>
        public class E57String : IE57Element
        {
            public E57ElementType E57Type => E57ElementType.String;

            #region Properties
            
            public string Name;
            public string Value;

            #endregion

            internal static E57String Parse(XElement root)
            {
                if (root == null) return null;
                EnsureElementType(root, "String");
                return new E57String { Name = root.Name.LocalName, Value = root.Value };
            }
        }

        /// <summary>
        /// TABLE 6 Attributes of a Blob Type E57 Element.
        /// </summary>
        public class E57Blob : IE57Element
        {
            public E57ElementType E57Type => E57ElementType.Blob;

            #region Properties

            /// <summary>
            /// Required.
            /// The physical file offset of the start of the associated binary Blob section in the E57 file. Shall be in the interval [0, 2^63).
            /// </summary>
            public E57PhysicalOffset FileOffset;

            /// <summary>
            /// Required.
            /// The logical length of the associated binary Blob section, in bytes. Shall be in the interval (0, 2^63).
            /// </summary>
            public long Length;

            #endregion

            internal static E57Blob Parse(XElement root)
            {
                if (root == null) return null;
                EnsureElementType(root, "Blob");
                return new E57Blob
                {
                    FileOffset = GetPhysicalOffsetAttribute(root, "fileOffset"),
                    Length = GetLongAttribute(root, "length")
                };
            }
        }

        /// <summary>
        /// TABLE 7 Attributes for a Structure Type E57 Element.
        /// </summary>
        public class E57Structure : IE57Element
        {
            public E57ElementType E57Type => E57ElementType.Structure;

            #region Properties
            
            public IE57Element[] Children;

            #endregion

            internal static E57Structure Parse(XElement root, Stream stream, bool verbose)
            {
                if (root == null) return null;
                EnsureElementType(root, "Structure");
                return new E57Structure
                {
                    Children = root.Elements().Select(x => ParseE57Element(x, stream, verbose)).ToArray()
                };
            }
        }

        /// <summary>
        /// TABLE 8 Attributes for a Vector Type E57 Element.
        /// </summary>
        public class E57Vector : IE57Element
        {
            public E57ElementType E57Type => E57ElementType.Vector;

            #region Properties

            /// <summary>
            /// Optional.
            /// Indicates whether the child elements may have different structure. Set to 1 to enable, set to 0 to disable.Shall be either 0 or 1.
            /// </summary>
            public bool AllowHeterogenousChildren;

            /// <summary>
            /// Vector elements.
            /// </summary>
            public IE57Element[] Children;

            #endregion

            internal static E57Vector Parse(XElement root, Stream stream, bool verbose)
            {
                if (root == null) return null;
                EnsureElementType(root, "Vector");
                return new E57Vector
                {
                    AllowHeterogenousChildren = TryGetLongAttribute(root, "allowHeterogeneousChildren") == 1,
                    Children = root.Elements().Select(x => ParseE57Element(x, stream, verbose)).ToArray()
                };
            }
        }

        /// <summary>
        /// TABLE 9 Attributes for a CompressedVector Type E57 Element.
        /// TABLE 10 Child Elements for a CompressedVector Type E57 Element.
        /// </summary>
        public class E57CompressedVector : IE57Element
        {
            public E57ElementType E57Type => E57ElementType.CompressedVector;

            #region Properties

            /// <summary>
            /// Required.
            /// The physical file offset of the start of the CompressedVector binary section in the E57 file(an integer). Shall be in the interval(0, 2^63).
            /// </summary>
            public E57PhysicalOffset FileOffset;

            /// <summary>
            /// Required.
            /// The number of records in the compressed binary block (an integer). Shall be in the interval[0, 2^63).
            /// </summary>
            public long RecordCount;

            /// <summary>
            /// Required. Structure, Integer, Float, ScaledInteger, String, or Vector.
            /// Specifies the fields of the CompressedVector records and their range limits.
            /// </summary>
            public E57Structure Prototype;

            /// <summary>
            /// Optional. Vector of Codec Structures.
            /// A heterogeneous Vector specifying the compression method to be used for fields within the CompressedVector.
            /// </summary>
            public E57Codec[] Codecs;

            public int ByteStreamsCount => Prototype.Children.Length;

            #endregion

            private Stream m_stream;

            internal static E57CompressedVector Parse(XElement root, Stream stream, bool verbose)
            {
                if (root == null) return null;
                
                var v = new E57CompressedVector
                {
                    FileOffset = GetPhysicalOffsetAttribute(root, "fileOffset"), 
                    RecordCount = GetLongAttribute(root, "recordCount"),
                    Prototype = E57Structure.Parse(GetElement(root, "prototype"), stream, verbose),
                    Codecs = GetElement(root, "codecs").Elements().Select(x => (E57Codec)ParseE57Element(x, stream, verbose)).ToArray(),
                    m_stream = stream
                };

                //v.ReadData(stream);
                return v;
            }

            public IEnumerable<ImmutableDictionary<PointPropertySemantics, Array>> ReadDataFull(
                int maxChunkPointCount,
                ImmutableDictionary<PointPropertySemantics, int> sem2idx,
                E57IntensityLimits intensityLimits,
                bool verbose = false
                )
            {
                var compressedVectorHeader = E57CompressedVectorHeader.Parse(ReadLogicalBytes(m_stream, FileOffset, 32));
                if (verbose)
                {
                    Report.Line($"[E57CompressedVector] FileOffset       = {FileOffset,10}");
                    Report.Line($"[E57CompressedVector] IndexStartOffset = {compressedVectorHeader.IndexStartOffset,10}");
                    Report.Line($"[E57CompressedVector] DataStartOffset  = {compressedVectorHeader.DataStartOffset,10}");
                    Report.Line($"[E57CompressedVector] SectionLength    = {compressedVectorHeader.SectionLength,10}");
                    Report.Line($"[E57CompressedVector] #records         = {RecordCount,10}");
                    Report.Line($"[E57CompressedVector] #bytestreams     = {ByteStreamsCount,10}");
                }

                var recordsLeftToConsumePerByteStream = new long[ByteStreamsCount].Set(RecordCount);
                var bitpackerPerByteStream = Prototype.Children.Map(x => new BitPacker(((IBitPack)x).NumberOfBitsForBitPack));
                var bytesLeftToConsume = compressedVectorHeader.SectionLength - 32;

                bool has(PointPropertySemantics sem) => sem2idx.ContainsKey(sem);

                if (compressedVectorHeader.DataStartOffset.Value == 0) throw new Exception($"Unexpected compressedVectorHeader.DataStartOffset (0).");
                if (compressedVectorHeader.IndexStartOffset.Value != 0) throw new Exception($"Unexpected compressedVectorHeader.IndexStartOffset ({compressedVectorHeader.IndexStartOffset})");

                var data = new ValueBufferSet(sem2idx.Keys);

                var offset = (E57LogicalOffset)compressedVectorHeader.DataStartOffset;
                var verboseDetail = false; // verbose
                while (bytesLeftToConsume > 0)
                {
                    if (verboseDetail) Console.WriteLine($"[E57CompressedVector] bytesLeftToConsume = {bytesLeftToConsume}");
                    var packetStart = offset;
                    var sectionId = ReadLogicalBytes(m_stream, offset, 1)[0];
                    if (verboseDetail) Console.WriteLine($"[E57CompressedVector][OFFSET] {offset}, sectionId = {sectionId}");

                    if (sectionId == E57_COMPRESSED_VECTOR_SECTION)
                    {
                        if (verboseDetail) Console.WriteLine($"[E57CompressedVector] DATA PACKET");
                        var dataPacketHeader = E57DataPacketHeader.Parse(ReadLogicalBytes(m_stream, offset, 6));
                        if (verboseDetail) Console.WriteLine($"[E57CompressedVector]   ByteStreamCount    = {dataPacketHeader.ByteStreamCount}");
                        if (verboseDetail) Console.WriteLine($"[E57CompressedVector]   PacketLengthMinus1 = {dataPacketHeader.PacketLengthMinus1}");

                        // read bytestream buffer lengths
                        offset += 6;
                        if (verboseDetail) Console.WriteLine($"[E57CompressedVector][OFFSET] {offset}");
                        var bytestreamBufferLengths = ReadLogicalUnsignedShorts(m_stream, offset, dataPacketHeader.ByteStreamCount);
                        
                        offset += dataPacketHeader.ByteStreamCount * sizeof(ushort);
                        if (verboseDetail) Console.WriteLine($"[E57CompressedVector][OFFSET] {offset}");

                        // read bytestream buffers
                        var bytestreamBuffers = new byte[dataPacketHeader.ByteStreamCount][];
                        var buffers = new Array[dataPacketHeader.ByteStreamCount];
                        for (var i = 0; i < dataPacketHeader.ByteStreamCount; i++)
                        {
                            var count = bytestreamBufferLengths[i];
                            bytestreamBuffers[i] = ReadLogicalBytes(m_stream, offset, count);
                            offset += count;

                            buffers[i] = UnpackByteStream(bytestreamBuffers[i], bitpackerPerByteStream[i], (IBitPack)Prototype.Children[i]);

                            recordsLeftToConsumePerByteStream[i] -= buffers[i].Length;
                        }

                        // build
                        {
                            var hasCartesian = has(PointPropertySemantics.CartesianX);
                            var hasSpherical = has(PointPropertySemantics.SphericalRange);
                            if (!hasCartesian && !hasSpherical) throw new Exception("Neither cartesian nor spherical coordinates.");
                            if (hasCartesian && hasSpherical) throw new Exception("Both cartesian and spherical coordinates.");

                            foreach (var kv in sem2idx)
                            {
                                var (sem, raw) = (kv.Key, buffers[kv.Value]);
                                switch (sem, raw)
                                {
                                    case (PointPropertySemantics.CartesianX, double[] xs)         : data.Append(sem, xs); break;
                                    case (PointPropertySemantics.CartesianY, double[] xs)         : data.Append(sem, xs); break;
                                    case (PointPropertySemantics.CartesianZ, double[] xs)         : data.Append(sem, xs); break;

                                    case (PointPropertySemantics.CartesianX, float[] xs)          : data.Append(sem, xs.Map(x => (double)x)); break;
                                    case (PointPropertySemantics.CartesianY, float[] xs)          : data.Append(sem, xs.Map(x => (double)x)); break;
                                    case (PointPropertySemantics.CartesianZ, float[] xs)          : data.Append(sem, xs.Map(x => (double)x)); break;

                                    case (PointPropertySemantics.SphericalRange, double[] xs)     : data.Append(sem, xs); break;
                                    case (PointPropertySemantics.SphericalAzimuth, double[] xs)   : data.Append(sem, xs); break;
                                    case (PointPropertySemantics.SphericalElevation, double[] xs) : data.Append(sem, xs); break;

                                    case (PointPropertySemantics.SphericalRange, float[] xs)      : data.Append(sem, xs.Map(x => (double)x)); break;
                                    case (PointPropertySemantics.SphericalAzimuth, float[] xs)    : data.Append(sem, xs.Map(x => (double)x)); break;
                                    case (PointPropertySemantics.SphericalElevation, float[] xs)  : data.Append(sem, xs.Map(x => (double)x)); break;

                                    case (PointPropertySemantics.ColorRed, uint[] xs)             : data.Append(sem, xs.Map(x => (byte)x)); break;
                                    case (PointPropertySemantics.ColorGreen, uint[] xs)           : data.Append(sem, xs.Map(x => (byte)x)); break;
                                    case (PointPropertySemantics.ColorBlue, uint[] xs)            : data.Append(sem, xs.Map(x => (byte)x)); break;

                                    case (PointPropertySemantics.TimeStamp, double[] xs)          : data.Append(sem, xs); break;

                                    case (PointPropertySemantics.RowIndex, uint[] xs)             : data.Append(sem, xs); break;
                                    case (PointPropertySemantics.ColumnIndex, uint[] xs)          : data.Append(sem, xs); break;

                                    case (PointPropertySemantics.CartesianInvalidState, uint[] xs): data.Append(sem, xs.Map(x => (byte)x)); break;
                                    case (PointPropertySemantics.SphericalInvalidState, uint[] xs): data.Append(sem, xs.Map(x => (byte)x)); break;
                                    case (PointPropertySemantics.IsIntensityInvalid, uint[] xs)   : data.Append(sem, xs.Map(x => (byte)x)); break;
                                    case (PointPropertySemantics.IsTimeStampInvalid, uint[] xs)   : data.Append(sem, xs.Map(x => (byte)x)); break;
                                    case (PointPropertySemantics.IsColorInvalid, uint[] xs)       : data.Append(sem, xs.Map(x => (byte)x)); break;

                                    case (PointPropertySemantics.Intensity, _):
                                        {
                                            int[] remapWithLimits(float[] xs)
                                            {
                                                var min = intensityLimits.Intensity.Min;
                                                var max = intensityLimits.Intensity.Max;
                                                var d = max - min;
                                                var result = xs.Map(x => (int)(((x - min) / d) * 65535.5f));
                                                return result;
                                            }

                                            var js = raw switch
                                            {
                                                sbyte[] ys => ys.Map(x => (int)x),
                                                byte[] ys => ys.Map(x => (int)x),
                                                short[] ys => ys.Map(x => (int)x),
                                                ushort[] ys => ys.Map(x => (int)x),
                                                int[] ys => ys,
                                                uint[] ys => ys.Map(x => (int)x),
                                                long[] ys => ys.Map(x => (int)x),
                                                ulong[] ys => ys.Map(x => (int)x),

                                                float[] ys when intensityLimits is null
                                                            => ys.Map(x => (int)(65535 * x)),

                                                float[] ys => remapWithLimits(ys),

                                                double[] ys when intensityLimits is null
                                                            => ys.Map(x => (int)(65535 * x)),

                                                double[] ys => remapWithLimits(ys.Map(x => (float)x)),

                                                _ => throw new Exception($"Unspecified intensity format {raw.GetType()}.")
                                            };

                                            data.Append(sem, js);
                                            break;
                                        }

                                    default: throw new NotImplementedException($"Unexpected data ({raw}) for {sem}");
                                }
                            }

                            if (data.CountMin >= maxChunkPointCount)
                                yield return data.Consume(data.CountMin);
                        }

                        // move to next packet
                        offset = packetStart + dataPacketHeader.PacketLengthMinus1 + 1;
                        bytesLeftToConsume -= dataPacketHeader.PacketLengthMinus1 + 1;
                    }
                    else if (sectionId == E57_INDEX_PACKET)
                    {
                        Console.WriteLine($"[E57CompressedVector] INDEX PACKET");
                        var indexPacketHeader = E57IndexPacketHeader.Parse(ReadLogicalBytes(m_stream, offset, 16));
                        Console.WriteLine($"[E57CompressedVector]   EntryCount         = {indexPacketHeader.EntryCount}");
                        Console.WriteLine($"[E57CompressedVector]   IndexLevel         = {indexPacketHeader.IndexLevel}");
                        Console.WriteLine($"[E57CompressedVector]   PacketLengthMinus1 = {indexPacketHeader.PacketLengthMinus1}");
                        throw new NotImplementedException("E57CompressedVector/IndexPacket");
                    }
                    else
                    {
                        throw new Exception($"Unexpected sectionId ({sectionId}).");
                    }
                }

                if (data.CountMin > 0)
                    yield return data.Consume(data.CountMin);


                #region Helpers
                static Array UnpackByteStream(byte[] buffer, BitPacker packer, IBitPack proto)
                {
                    var bits = proto.NumberOfBitsForBitPack;
                    var semantic = proto.Semantic;
                    switch (proto.E57Type)
                    {
                        case E57ElementType.Float:
                            {
                                var p = (E57Float)proto;
                                return p.IsDoublePrecision ? (Array)UnpackFloat64(buffer, p) : UnpackFloat32(buffer, p);
                            }
                        case E57ElementType.ScaledInteger:
                            {
                                var p = (E57ScaledInteger)proto;
                                return UnpackScaledInteger(buffer, packer, p);
                            }
                        case E57ElementType.Integer:
                            {
                                try
                                {
                                    var p = (E57Integer)proto;
                                    if (p.Minimum < 0)
                                    {
                                        var xs = UnpackIntegers(buffer, packer, p);
                                        return xs;
                                    }
                                    else
                                    {
                                        return UnpackIntegers(buffer, packer, p);
                                    }
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine($"[Semantic={proto.Semantic}; NumberOfBits={proto.NumberOfBitsForBitPack}] {e}");
                                    return new long[0];
                                }
                            }
                        default:
                            Console.WriteLine(
                                $"[E57CompressedVector][UnpackBuffer] UNKNOWN: buffer size {buffer.Length,8},  bits {bits,2},  {proto.E57Type,-16},  {semantic,-24}"
                                );
                            throw new InvalidOperationException();
                    }
                    
                }

                static float[] UnpackFloat32(byte[] buffer, E57Float proto)
                {
                    if (proto.IsDoublePrecision) throw new ArgumentException($"Expected single precision, but is double.");
                    if (proto.NumberOfBitsForBitPack != 32) throw new ArgumentException($"Expected 32 bits, but is {proto.NumberOfBitsForBitPack}.");
                    if (buffer.Length % 4 != 0) throw new ArgumentException($"Expected buffer length multiple of 4, but is {buffer.Length}.");
                    using var br = new BinaryReader(new MemoryStream(buffer));
                    var xs = new float[buffer.Length / 4];
                    for (var i = 0; i < xs.Length; i++) xs[i] = br.ReadSingle();
                    return xs;
                }
                static double[] UnpackFloat64(byte[] buffer, E57Float proto)
                {
                    if (!proto.IsDoublePrecision) throw new ArgumentException($"Expected double precision, but is single.");
                    if (proto.NumberOfBitsForBitPack != 64) throw new ArgumentException($"Expected 64 bits, but is {proto.NumberOfBitsForBitPack}.");
                    if (buffer.Length % 8 != 0) throw new ArgumentException($"Expected buffer length multiple of 8, but is {buffer.Length}.");
                    using var br = new BinaryReader(new MemoryStream(buffer));
                    var xs = new double[buffer.Length / 8];
                    for (var i = 0; i < xs.Length; i++) xs[i] = br.ReadDouble();
                    return xs;
                }
                static double[] UnpackScaledInteger(byte[] buffer, BitPacker packer, E57ScaledInteger proto)
                {
                    checked
                    {
                        var xs = packer.UnpackUInts(buffer);
                        var rs = xs.Map(proto.Compute);
                        return rs;
                    }
                }
                static Array UnpackIntegers(byte[] buffer, BitPacker packer, E57Integer proto)
                    => packer.UnpackUInts(buffer);
                #endregion
            }
        }
        
        /// <summary>
        /// TABLE 11 Child Elements for the Codec Structure.
        /// </summary>
        public class E57Codec : IE57Element
        {
            public E57ElementType E57Type => E57ElementType.E57Codec;

            #region Properties

            /// <summary>
            /// Required.
            /// A Vector listing the relative path names of elements in the prototype that this codec will compress.
            /// </summary>
            public E57Vector Inputs;

            /// <summary>
            /// Optional.
            /// Specifies that the bitPackCodec will be used for compressing the data.
            /// </summary>
            public E57Structure BitPackCodec;

            #endregion

            internal static E57Codec Parse(XElement root, Stream stream, bool verbose)
            {
                if (root == null) return null;
                return new E57Codec
                {
                    Inputs = E57Vector.Parse(GetElement(root, "inputs"), stream, verbose),
                    BitPackCodec = E57Structure.Parse(GetElement(root, "bitPackCodec"), stream, verbose),
                };
            }
        }
        
        /// <summary>
        /// E57 Root (8.4 XML Data Hierarchy).
        /// TABLE 12 Child Elements for the E57Root Structure.
        /// </summary>
        public class E57Root : IE57Element
        {
            public E57ElementType E57Type => E57ElementType.E57Root;

            #region Properties

            /// <summary>
            /// Raw XML.
            /// </summary>
            public XElement XmlRoot;

            /// <summary>
            /// Required.
            /// Shall contain the string “ASTM E57 3D Imaging Data File”.
            /// </summary>
            public string FormatName;
            
            /// <summary>
            /// Required.
            /// A globally unique identification (GUID) String for the current version of the file (see 8.4.22).
            /// </summary>
            public string Guid;

            /// <summary>
            /// Required.
            /// The major version number of the file format. Shall be 1.
            /// </summary>
            public int VersionMajor;

            /// <summary>
            /// Required.
            /// The minor version number of the file format. Shall be 0.
            /// </summary>
            public int VersionMinor;

            /// <summary>
            /// Optional.
            /// The version identifier for the E57 file format library that wrote the file.
            /// </summary>
            public string E57LibraryVersion;

            /// <summary>
            /// Optional.
            /// Date and time that the file was created.
            /// </summary>
            public E57DateTime CreationDateTime;

            /// <summary>
            /// Optional.
            /// A heterogeneous Vector of Data3D Structures for storing 3D imaging data.
            /// </summary>
            public E57Data3D[] Data3D;

            /// <summary>
            /// Optional.
            /// A heterogeneous Vector of Image2D Structures for storing 2D images from a camera or similar device.
            /// </summary>
            public E57Image2D[] Images2D;

            /// <summary>
            /// Optional.
            /// Information describing the Coordinate Reference System to be used for the file.
            /// </summary>
            public string CoordinateMetadata;

            #endregion

            internal static E57Root Parse(XElement root, Stream stream, bool verbose)
            {
                EnsureElementNameAndType(root, "e57Root", "Structure");
                
                return new E57Root
                {
                    XmlRoot = root,
                    FormatName = GetString(root, "formatName", true, "ASTM E57 3D Imaging Data File"),
                    Guid = GetString(root, "guid", true),
                    VersionMajor = GetInteger(root, "versionMajor", true, 1).Value,
                    VersionMinor = GetInteger(root, "versionMinor", true).Value,
                    E57LibraryVersion = GetString(root, "e57LibraryVersion", false),
                    CreationDateTime = E57DateTime.Parse(GetElement(root, "creationDateTime")),
                    Data3D = E57Data3D.ParseVectorChildren(GetElement(root, "data3D"), stream, verbose),
                    Images2D = E57Image2D.ParseVectorChildren(GetElement(root, "images2D"), stream),
                    CoordinateMetadata = GetString(root, "coordinateMetadata", false)
                };
            }
        }

        /// <summary>
        /// E57 Data3D (8.4.3. Data3D).
        /// TABLE 13 Child Elements for the Data3D Structure.
        /// </summary>
        public class E57Data3D : IE57Element
        {
            public E57ElementType E57Type => E57ElementType.Data3D;

            #region Properties

            /// <summary>
            /// Required.
            /// A globally unique identifier for the current version of the Data3D object (see 8.4.22).
            /// </summary>
            public string Guid;

            /// <summary>
            /// Required.
            /// A compressed vector of PointRecord Structures referring to the binary data that actually stores the point data.
            /// </summary>
            public E57CompressedVector Points;

            /// <summary>
            /// Optional.
            /// A rigid body transform that transforms data stored in the local coordinate system of the points to the file-level coordinate system.
            /// </summary>
            public E57RigidBodyTransform Pose;

            /// <summary>
            /// Optional.
            /// A Vector of globally unique identifiers identifying the data set(or sets) from which the points in this Data3D originated.
            /// </summary>
            public E57Vector OriginalGuids;

            /// <summary>
            /// Optional.
            /// The defined schemes that group points in different ways.
            /// </summary>
            public E57PointGroupingSchemes PointGroupingSchemes;

            /// <summary>
            /// Optional.
            /// A user-defined name for the Data3D.
            /// </summary>
            public string Name;

            /// <summary>
            /// Optional.
            /// A user-defined description of the Data3D.
            /// </summary>
            public string Description;

            /// <summary>
            /// Optional.
            /// The bounding region (in Cartesian coordinates) of all the points in this Data3D(in the local coordinate system of the points).
            /// </summary>
            public E57CartesianBounds CartesianBounds;

            /// <summary>
            /// Optional.
            /// The bounding region (in spherical coordinates) of all the points in this Data3D(in the local coordinate system of the points).
            /// </summary>
            public E57SphericalBounds SphericalBounds;

            /// <summary>
            /// Optional.
            /// The bounds of the row, column, and return number of all the points in this Data3D.
            /// </summary>
            public E57IndexBounds IndexBounds;

            /// <summary>
            /// Optional.
            /// The limits for the value of signal intensity that the sensor is capable of producing.
            /// </summary>
            public E57IntensityLimits IntensityLimits;

            /// <summary>
            /// Optional.
            /// The limits for the value of red, green, and blue color that the sensor is capable of producing.
            /// </summary>
            public E57ColorLimits ColorLimits;

            /// <summary>
            /// Optional.
            /// The start date and time that the data was acquired.
            /// </summary>
            public E57DateTime AcquisitonStart;

            /// <summary>
            /// Optional.
            /// The end date and time that the data was acquired.
            /// </summary>
            public E57DateTime AcquisitonEnd;

            /// <summary>
            /// Optional.
            /// The name of the manufacturer for the sensor used to collect the points in this Data3D.
            /// </summary>
            public string SensorVendor;

            /// <summary>
            /// Optional.
            /// The model name or number for the sensor.
            /// </summary>
            public string SensorModel;

            /// <summary>
            /// Optional.
            /// The serial number for the sensor.
            /// </summary>
            public string SensorSerialNumber;

            /// <summary>
            /// Optional.
            /// The version identifier for the sensor hardware at the time of data collection.
            /// </summary>
            public string SensorHardwareVersion;

            /// <summary>
            /// Optional.
            /// The version identifier for the software used for the data collection.
            /// </summary>
            public string SensorSoftwareVersion;

            /// <summary>
            /// Optional.
            /// The version identifier for the firmware installed in the sensor at the time of data collection.
            /// </summary>
            public string SensorFirmwareVersion;

            /// <summary>
            /// Optional.
            /// The ambient temperature, measured at the sensor, at the time of data collection (in degrees Celsius).
            /// Shall be greater or equal to −273.15° (absolute zero).
            /// </summary>
            public double? Temperature;

            /// <summary>
            /// Optional.
            /// The percentage relative humidity, measured at the sensor, at the time of data collection.
            /// Shall be in the interval [0,100].
            /// </summary>
            public double? RelativeHumidity;

            /// <summary>
            /// Optional.
            /// The atmospheric pressure, measured at the sensor, at the time of data collection (in Pascals).
            /// Shall be positive.
            /// </summary>
            public double? AtmosphericPressure;

            #endregion

            public ImmutableDictionary<PointPropertySemantics, int> Sem2Index { get; private set; }
            public bool HasCartesianCoordinates => Has(PointPropertySemantics.CartesianX);
            public bool HasSphericalCoordinates => Has(PointPropertySemantics.SphericalAzimuth);
            public bool HasColors => Has(PointPropertySemantics.ColorRed);
            public bool Has(PointPropertySemantics x) => Sem2Index.ContainsKey(x);
            public bool HasAllOrNoneOf(params PointPropertySemantics[] xs) => !xs.Any(Sem2Index.ContainsKey) || xs.All(Sem2Index.ContainsKey);

            internal static E57Data3D Parse(XElement root, Stream stream, bool verbose)
            {
                EnsureElementNameAndType(root, "vectorChild", "Structure");

                var points = E57CompressedVector.Parse(GetElement(root, "points"), stream, verbose);

                // check semantics
                var sem2Index = points.Prototype.Children
                    .Select((x, i) =>
                    {
                        var s = ((IBitPack)x).Semantic;
                        if (!Enum.TryParse<PointPropertySemantics>(s, ignoreCase: true, out var sem)) 
                            throw new NotImplementedException($"Unknown semantic \"{s}\".");
                        return (idx: i, sem);
                    })
                    .ToImmutableDictionary(x => x.sem, x => x.idx)
                    ;

                var data3d = new E57Data3D
                {
                    Guid = GetString(root, "guid", true),
                    Points = points,
                    Pose = E57RigidBodyTransform.Parse(GetElement(root, "pose")),
                    OriginalGuids = E57Vector.Parse(GetElement(root, "originalGuids"), stream, verbose),
                    PointGroupingSchemes = E57PointGroupingSchemes.Parse(GetElement(root, "pointGroupingSchemes")),
                    Name = GetString(root, "name", false),
                    Description = GetString(root, "description", false),
                    CartesianBounds = E57CartesianBounds.Parse(GetElement(root, "cartesianBounds")),
                    SphericalBounds = E57SphericalBounds.Parse(GetElement(root, "sphericalBounds")),
                    IndexBounds = E57IndexBounds.Parse(GetElement(root, "indexBounds")),
                    IntensityLimits = E57IntensityLimits.Parse(GetElement(root, "intensityLimits")),
                    ColorLimits = E57ColorLimits.Parse(GetElement(root, "colorLimits")),
                    AcquisitonStart = E57DateTime.Parse(GetElement(root, "acquisitonStart")),
                    AcquisitonEnd = E57DateTime.Parse(GetElement(root, "acquisitonEnd")),
                    SensorVendor = GetString(root, "sensorVendor", false),
                    SensorModel = GetString(root, "sensorModel", false),
                    SensorSerialNumber = GetString(root, "sensorSerialNumber", false),
                    SensorHardwareVersion = GetString(root, "sensorHardwareVersion", false),
                    SensorSoftwareVersion = GetString(root, "sensorSoftwareVersion", false),
                    SensorFirmwareVersion = GetString(root, "sensorFirmwareVersion", false),
                    Temperature = Check("temperature", GetFloat(root, "temperature", false), x => !x.HasValue || x >= -273.15, ">= -273.15"),
                    RelativeHumidity = Check("relativeHumidity", GetFloat(root, "relativeHumidity", false), x => !x.HasValue || (x >= 0 && x <= 100), "[0,100]"),
                    AtmosphericPressure = Check("atmosphericPressure", GetFloat(root, "atmosphericPressure", false), x => !x.HasValue || x > 0, "positive"),

                    Sem2Index = sem2Index,
                };


                #region 8.4.4.1 (nop)
                #endregion
                #region 8.4.4.2
                if (!data3d.HasAllOrNoneOf(PointPropertySemantics.CartesianX, PointPropertySemantics.CartesianY, PointPropertySemantics.CartesianZ))
                    throw new ArgumentException("[8.4.4.2] Incomplete cartesian[XYZ].");
                if (!data3d.HasAllOrNoneOf(PointPropertySemantics.SphericalRange, PointPropertySemantics.SphericalAzimuth, PointPropertySemantics.SphericalElevation))
                    throw new ArgumentException("[8.4.4.2] Incomplete spherical[Range|Azimuth|Elevation].");
                #endregion
                #region 8.4.4.3 (nop)
                #endregion
                #region 8.4.4.4
                if (!data3d.HasAllOrNoneOf(PointPropertySemantics.ReturnIndex, PointPropertySemantics.ReturnCount))
                    throw new ArgumentException("[8.4.4.4] Incomplete return[Index|Count].");
                #endregion
                #region 8.4.4.5 (nop)
                #endregion
                #region 8.4.4.6
                if (!data3d.HasAllOrNoneOf(PointPropertySemantics.ColorRed, PointPropertySemantics.ColorGreen, PointPropertySemantics.ColorBlue))
                    throw new ArgumentException("[8.4.4.6] Incomplete color[Red|Green|Blue].");
                #endregion
                #region 8.4.4.7 (nop)
                #endregion
                #region 8.4.4.8 (nop)
                #endregion
                #region 8.4.4.9 (nop)
                #endregion
                #region 8.4.4.10 (nop)
                #endregion
                #region 8.4.4.11 (nop)
                #endregion

                #region 8.4.3.1 (nop)
                #endregion
                #region 8.4.3.2 (nop)
                #endregion
                #region 8.4.3.3 (nop)
                #endregion
                #region 8.4.3.4
                if (data3d.HasCartesianCoordinates && data3d.CartesianBounds == null)
                {
                    if (verbose) Report.Warn("[8.4.3.4] CartesianBounds must be defined (if cartesian coordinates are defined).");
                }
                #endregion
                #region 8.4.3.5
                if (data3d.HasSphericalCoordinates && data3d.SphericalBounds == null)
                {
                    if (verbose) Report.Warn("[8.4.3.5] SphericalBounds must be defined (if spherical coordinates are defined).");
                }
                #endregion
                #region 8.4.3.6
                if (data3d.Has(PointPropertySemantics.RowIndex) && data3d.IndexBounds == null)
                    throw new ArgumentException("[8.4.3.6] IndexBounds must be defined (if rowIndex is defined).");
                if (data3d.Has(PointPropertySemantics.ColumnIndex) && data3d.IndexBounds == null)
                    throw new ArgumentException("[8.4.3.6] IndexBounds must be defined (if columnIndex is defined).");
                if (data3d.Has(PointPropertySemantics.ReturnIndex) && data3d.IndexBounds == null)
                    throw new ArgumentException("[8.4.3.6] IndexBounds must be defined (if returnIndex is defined).");
                #endregion
                #region 8.4.3.7 (nop)
                #endregion
                #region 8.4.3.8 (nop)
                #endregion

                return data3d;
            }
            internal static E57Data3D[] ParseVectorChildren(XElement root, Stream stream, bool verbose)
            {
                if (root == null) return null;
                EnsureElementNameAndType(root, "data3D", "Vector");
                return GetElements(root, "vectorChild").Select(x => Parse(x, stream, verbose)).ToArray();
            }

            public IEnumerable<(V3d[] Positions, ImmutableDictionary<PointPropertySemantics, Array> Properties)> StreamPointsFull(int maxChunkPointCount, bool verbose = false)
            {
                var chunks = Points.ReadDataFull(maxChunkPointCount, Sem2Index, IntensityLimits, verbose);
                foreach (var chunk in chunks)
                {
                    V3d[] ps;
                    if (chunk.ContainsKey(PointPropertySemantics.CartesianX))
                    {
                        var pxs = (double[])chunk[PointPropertySemantics.CartesianX];
                        var pys = (double[])chunk[PointPropertySemantics.CartesianY];
                        var pzs = (double[])chunk[PointPropertySemantics.CartesianZ];

                        var imax = pxs.Length;
                        if (imax != pys.Length || imax != pys.Length) 
                            throw new Exception($"Length mismatch for CartesianX ({pxs.Length}), CartesianY ({pys.Length}), and CartesianZ ({pzs.Length}) ");

                        ps = new V3d[imax];
                        for (var i = 0; i < imax; i++) ps[i].X = pxs[i];
                        for (var i = 0; i < imax; i++) ps[i].Y = pys[i];
                        for (var i = 0; i < imax; i++) ps[i].Z = pzs[i];
                    }
                    else // spherical
                    {
                        var prs = (double[])chunk[PointPropertySemantics.SphericalRange];
                        var pas = (double[])chunk[PointPropertySemantics.SphericalAzimuth];
                        var pes = (double[])chunk[PointPropertySemantics.SphericalElevation];

                        var imax = prs.Length;
                        if (imax != pas.Length || imax != pes.Length)
                            throw new Exception($"Length mismatch for SphericalRange ({prs.Length}), SphericalAzimuth ({pas.Length}), and SphericalElevation ({pes.Length}) ");

                        ps = new V3d[imax];
                        for (var i = 0; i < imax; i++)
                        {
                            var range = prs[i];
                            var azimuth = pas[i];
                            var elevation = pes[i];
                            var cosElevation = Math.Cos(elevation);
                            ps[i] = new(
                                range * cosElevation * Math.Cos(azimuth),
                                range * cosElevation * Math.Sin(azimuth),
                                range * Math.Sin(elevation)
                                );
                        }
                    }

                    if (Pose != null)
                    {
                        ps = ps.Map(p => Pose.Rotation.Transform(p) + Pose.Translation);
                    }

                    yield return (ps, chunk);
                }
            }
        }

        /// <summary>
        /// TABLE 14 Child Elements for the PointRecord Structure.
        /// </summary>
        public class E57PointRecord : IE57Element
        {
            public E57ElementType E57Type => E57ElementType.PointRecord;

            #region Properties

            /// <summary>
            /// Optional.
            /// Grouping information by row or column index.
            /// </summary>
            public GroupingByLine GroupingByLine;

            #endregion

            internal static E57PointRecord Parse(XElement root)
                => root != null ? new E57PointRecord { GroupingByLine = GroupingByLine.Parse(root) } : null;
        }

        /// <summary>
        /// TABLE 15 Child Elements for the PointGroupingSchemes Structure.
        /// </summary>
        public class E57PointGroupingSchemes : IE57Element
        {
            public E57ElementType E57Type => E57ElementType.PointGroupingSchemes;

#region Properties

            /// <summary>
            /// Optional.
            /// Grouping information by row or column index.
            /// </summary>
            public GroupingByLine GroupingByLine;

#endregion

            internal static E57PointGroupingSchemes Parse(XElement root)
            {
                if (root == null) return null;
                return new E57PointGroupingSchemes
                {
                    GroupingByLine = GroupingByLine.Parse(GetElement(root, "groupingByLine"))
                };
            }
        }

        /// <summary>
        /// TABLE 16 Child Elements for the GroupingByLine Structure.
        /// </summary>
        public class GroupingByLine : IE57Element
        {
            public E57ElementType E57Type => E57ElementType.GroupingByLine;

#region Properties

            /// <summary>
            /// Required.
            /// The name of the PointRecord element that identifies which group the point is in.
            /// The value of this string shall be “rowIndex” or “columnIndex” (see 8.4.4.2).
            /// </summary>
            public string IdElementName;

            /// <summary>
            /// Required.
            /// A compressedVector of LineGroupRecord Structures.
            /// </summary>
            public E57LineGroupRecord[] Groups;

#endregion

            internal static GroupingByLine Parse(XElement root)
            {
                if (root == null) return null;
                return new GroupingByLine
                {
                    IdElementName = GetString(root, "idElementName", true),
                    Groups = null // TODO
                };
            }
        }

        /// <summary>
        /// TABLE 17 Child Elements for the LineGroupRecord Structure.
        /// </summary>
        public class E57LineGroupRecord : IE57Element
        {
            public E57ElementType E57Type => E57ElementType.LineGroupRecord;

#region Properties

            /// <summary>
            /// Required.
            /// The value of the identifying element of all members in this group. Shall be in the interval[0, 2^63).
            /// </summary>
            public long IdElementValue;

            /// <summary>
            /// Optional.
            /// The record number of the first point in the continuous interval. Shall be in the interval[0, 2^63).
            /// </summary>
            public long? StartPointIndex;

            /// <summary>
            /// Optional.
            /// The number of PointRecords in the group. Shall be in the interval [1, 2^63).
            /// </summary>
            public long? PointCount;

            /// <summary>
            /// Optional.
            /// The bounding box (in Cartesian coordinates) of all points in the group (in the local coordinate system of the points).
            /// </summary>
            public E57CartesianBounds CartesianBounds;

            /// <summary>
            /// Optional.
            /// The bounding region (in spherical coordinates) of all the points in the group (in the local coordinate system of the points).
            /// </summary>
            public E57SphericalBounds SphericalBounds;

#endregion

            internal static E57LineGroupRecord Parse(XElement root)
            {
                if (root == null) return null;
                return new E57LineGroupRecord
                {
                    IdElementValue = GetLong(root, "idElementValue", true).Value,
                    StartPointIndex = GetLong(root, "startPointIndex", true),
                    PointCount = GetLong(root, "pointCount", true),
                    CartesianBounds = E57CartesianBounds.Parse(GetElement(root, "cartesianBounds")),
                    SphericalBounds = E57SphericalBounds.Parse(GetElement(root, "sphericalBounds")),
                };
            }
        }

        /// <summary>
        /// TABLE 18 Child Elements for the RigidBodyTransform Structure.
        /// </summary>
        public class E57RigidBodyTransform : IE57Element
        {
            public E57ElementType E57Type => E57ElementType.RigidBodyTransform;

#region Properties

            /// <summary>
            /// Required.
            /// A unit quaternion representing the rotation, R, of the transform.
            /// </summary>
            public Rot3d Rotation { get; }

            /// <summary>
            /// Required.
            /// The translation, t, of the transform.
            /// </summary>
            public V3d Translation { get; }

#endregion

            /// <summary>
            /// A 3D point is transformed from the source coordinate system to the
            /// destination coordinate system by first applying the rotation and
            /// then the translation: p' = Rp + t
            /// </summary>
            public Trafo3d RigidBodyTransform { get; }
            
            private E57RigidBodyTransform(Rot3d rotation, V3d translation)
            {
                Rotation = rotation;
                Translation = translation;
                RigidBodyTransform = new Trafo3d(rotation) * Trafo3d.Translation(translation);
            }

            internal static E57RigidBodyTransform Parse(XElement root)
            {
                if (root == null) return null;
                var r = GetElement(root, "rotation") != null ? GetQuaternion(GetElement(root, "rotation")) : Rot3d.Identity;
                var t = GetElement(root, "translation") != null ? GetTranslation(GetElement(root, "translation")) : V3d.Zero;
                return new E57RigidBodyTransform(r, t);
            }
        }
        
        /// <summary>
        /// E57 Image2D (8.4.11. Image2D)
        /// TABLE 21 Child Elements for the Image2D Structure.
        /// </summary>
        public class E57Image2D : IE57Element
        {
            public E57ElementType E57Type => E57ElementType.Image2d;

#region Properties

            /// <summary>
            /// Required.
            /// A globally unique identifier for the current version of the Image2D object (see 8.4.22).
            /// </summary>
            public string Guid;

            /// <summary>
            /// Optional.
            /// Representation for an image that does not define any camera projection model.The image is to be used for visual reference only.
            /// </summary>
            public E57VisualReferenceRepresentation VisualReferenceRepresentation;

            /// <summary>
            /// Optional.
            /// Representation for an image using the pinhole camera projection model.
            /// </summary>
            public E57PinholeRepresentation PinholeRepresentation;

            /// <summary>
            /// Optional.
            /// Representation for an image using the spherical camera projection model.
            /// </summary>
            public E57SphericalRepresentation SphericalRepresentation;

            /// <summary>
            /// Optional.
            /// Representation for an image using the cylindrical camera projection model.
            /// </summary>
            public E57CylindricalRepresentation CylindricalRepresentation;

            /// <summary>
            /// Optional.
            /// A rigid body transform that transforms data stored in the local coordinate system of the image to the file-level coordinate system.
            /// </summary>
            public E57RigidBodyTransform Pose;

            /// <summary>
            /// Optional.
            /// The globally unique identifier for the Data3D object that was being acquired when the picture was taken(see8.4.22).
            /// </summary>
            public string AssociatedData3DGuid;

            /// <summary>
            /// Optional.
            /// A user-defined name for the Image2D.
            /// </summary>
            public string Name;

            /// <summary>
            /// Optional.
            /// A user-defined description of the Image2D.
            /// </summary>
            public string Description;

            /// <summary>
            /// Optional.
            /// The date and time that the image was acquired.
            /// </summary>
            public E57DateTime AcquisitionDateTime;

            /// <summary>
            /// Optional.
            /// The name of the manufacturer for the sensor used to collect the image in this Image2D.
            /// </summary>
            public string SensorVendor;

            /// <summary>
            /// Optional.
            /// The model name or number for the sensor.
            /// </summary>
            public string SensorModel;

            /// <summary>
            /// Optional.
            /// The serial number for the sensor.
            /// </summary>
            public string SensorSerialNumber;

#endregion

            internal static E57Image2D[] ParseVectorChildren(XElement root, Stream stream)
            {
                if (root == null) return null;
                EnsureElementNameAndType(root, "images2D", "Vector");
                return GetElements(root, "vectorChild").Select(x => Parse(x, stream)).ToArray();
            }

            internal static E57Image2D Parse(XElement root, Stream stream)
            {
                EnsureElementNameAndType(root, "vectorChild", "Structure");

                return new E57Image2D
                {
                    Guid = GetString(root, "guid", true),
                    VisualReferenceRepresentation = E57VisualReferenceRepresentation.Parse(GetElement(root, "visualReferenceRepresentation"),stream),
                    PinholeRepresentation = E57PinholeRepresentation.Parse(GetElement(root, "pinholeRepresentation"), stream),
                    SphericalRepresentation = E57SphericalRepresentation.Parse(GetElement(root, "sphericalRepresentation"), stream),
                    CylindricalRepresentation = E57CylindricalRepresentation.Parse(GetElement(root, "cylindricalRepresentation"), stream),
                    Pose = E57RigidBodyTransform.Parse(GetElement(root, "pose")),
                    AssociatedData3DGuid = GetString(root, "associatedData3DGuid", false),
                    Name = GetString(root, "name", false),
                    Description = GetString(root, "description", false),
                    AcquisitionDateTime = E57DateTime.Parse(GetElement(root, "acquisitionDateTime")),
                    SensorVendor = GetString(root, "sensorVendor", false),
                    SensorModel = GetString(root, "sensorModel", false),
                    SensorSerialNumber = GetString(root, "sensorSerialNumber", false),
                };
            }
        }

        /// <summary>
        /// TABLE 22 Child Elements for the VisualReferenceRepresentation Structure.
        /// </summary>
        public class E57VisualReferenceRepresentation : IE57Element
        {
            public E57ElementType E57Type => E57ElementType.VisualReferenceRepresentation;

#region Properties

            /// <summary>
            /// Optional.
            /// JPEG format image data.
            /// </summary>
            public byte[] JpegImage;

            /// <summary>
            /// Optional.
            /// PNG format image data.
            /// </summary>
            public byte[] PngImage;

            /// <summary>
            /// Optional.
            /// PNG format image mask.
            /// </summary>
            public byte[] ImageMask => throw new NotImplementedException();

            /// <summary>
            /// Required.
            /// The image width (in pixels). Shall be positive.
            /// </summary>
            public int ImageWidth;

            /// <summary>
            /// Required.
            /// The image height (in pixels). Shall be positive.
            /// </summary>
            public int ImageHeight;

            /// <summary>
            /// (ImageWidth, ImageHeight).
            /// </summary>
            public V2i ImageSize => new(ImageWidth, ImageHeight);

#endregion

            internal static E57VisualReferenceRepresentation Parse(XElement root, Stream stream)
            {
                if (root == null) return null;
                return new E57VisualReferenceRepresentation
                {
                    JpegImage = GetImageBytes(root, "jpegImage", stream),
                    PngImage = GetImageBytes(root, "pngImage", stream),
                    ImageWidth = GetInteger(root, "imageWidth", true).Value,
                    ImageHeight = GetInteger(root, "imageHeight", true).Value,
                };
            }
        }

        /// <summary>
        /// TABLE 23 Child Elements for the PinholeRepresentation Structure.
        /// </summary>
        public class E57PinholeRepresentation : IE57Element
        {
            public E57ElementType E57Type => E57ElementType.PinholeRepresentation;

            #region Properties

            /// <summary>
            /// Optional.
            /// JPEG format image data.
            /// </summary>
            public byte[] JpegImage;

            /// <summary>
            /// Optional.
            /// PNG format image data.
            /// </summary>
            public byte[] PngImage;

            /// <summary>
            /// Optional.
            /// PNG format image mask.
            /// </summary>
            public byte[] ImageMask => throw new NotImplementedException();

            /// <summary>
            /// Required.
            /// The image width (in pixels). Shall be positive.
            /// </summary>
            public int ImageWidth;

            /// <summary>
            /// Required.
            /// The image height (in pixels). Shall be positive.
            /// </summary>
            public int ImageHeight;

            /// <summary>
            /// (ImageWidth, ImageHeight).
            /// </summary>
            public V2i ImageSize => new(ImageWidth, ImageHeight);

            /// <summary>
            /// Required.
            /// The camera’s focal length (in meters). Shall be positive.
            /// </summary>
            public double FocalLength;

            /// <summary>
            /// Required.
            /// The width of the pixels in the camera (in meters). Shall be positive.
            /// </summary>
            public double PixelWidth;

            /// <summary>
            /// Required.
            /// The height of the pixels in the camera (in meters). Shall be positive.
            /// </summary>
            public double PixelHeight;

            /// <summary>
            /// (PixelWidth, PixelHeight).
            /// </summary>
            public V2i PixelSize => new(PixelWidth, PixelHeight);

            /// <summary>
            /// Required.
            /// The X coordinate in the image of the principal point, (in pixels). The principal point is the
            /// intersection of the z axis of the camera coordinate frame with the image plane.
            /// </summary>
            public double PrincipalPointX;

            /// <summary>
            /// Required.
            /// The Y coordinate in the image of the principal point(in pixels).
            /// </summary>
            public double PrincipalPointY;

            /// <summary>
            /// (PrincipalPointX, PrincipalPointY).
            /// </summary>
            public V2d PrincipalPoint => new(PrincipalPointX, PrincipalPointY);

#endregion

            internal static E57PinholeRepresentation Parse(XElement root, Stream stream)
            {
                if (root == null) return null;
                
                var result = new E57PinholeRepresentation
                {
                    JpegImage = GetImageBytes(root, "jpegImage", stream),
                    PngImage = GetImageBytes(root, "pngImage", stream),                    
                    ImageWidth = GetInteger(root, "imageWidth", true).Value,
                    ImageHeight = GetInteger(root, "imageHeight", true).Value,
                    FocalLength = GetFloat(root, "focalLength", true).Value,
                    PixelWidth = GetFloat(root, "pixelWidth", true).Value,
                    PixelHeight = GetFloat(root, "pixelHeight", true).Value,
                    PrincipalPointX = GetFloat(root, "principalPointX", true).Value,
                    PrincipalPointY = GetFloat(root, "principalPointY", true).Value,
                };
                

                return result;
            }
        }

        /// <summary>
        /// TABLE 24 Child Elements for the SphericalRepresentation Structure. 
        /// </summary>
        public class E57SphericalRepresentation : IE57Element
        {
            public E57ElementType E57Type => E57ElementType.SphericalRepresentation;

            #region Properties

            /// <summary>
            /// Optional.
            /// JPEG format image data.
            /// </summary>
            public byte[] JpegImage;

            /// <summary>
            /// Optional.
            /// PNG format image data.
            /// </summary>
            public byte[] PngImage;

            /// <summary>
            /// Optional.
            /// PNG format image mask.
            /// </summary>
            public byte[] ImageMask => throw new NotImplementedException();

            /// <summary>
            /// Required.
            /// The image width (in pixels). Shall be positive.
            /// </summary>
            public int ImageWidth;

            /// <summary>
            /// Required.
            /// The image height (in pixels). Shall be positive.
            /// </summary>
            public int ImageHeight;

            /// <summary>
            /// (ImageWidth, ImageHeight).
            /// </summary>
            public V2i ImageSize => new(ImageWidth, ImageHeight);
            
            /// <summary>
            /// Required.
            /// The width of the pixels in the camera (in meters). Shall be positive.
            /// </summary>
            public double PixelWidth;

            /// <summary>
            /// Required.
            /// The height of the pixels in the camera (in meters). Shall be positive.
            /// </summary>
            public double PixelHeight;

            /// <summary>
            /// (PixelWidth, PixelHeight).
            /// </summary>
            public V2i PixelSize => new(PixelWidth, PixelHeight);

#endregion

            internal static E57SphericalRepresentation Parse(XElement root, Stream stream)
            {
                if (root == null) return null;
                return new E57SphericalRepresentation
                {
                    JpegImage = GetImageBytes(root, "jpegImage", stream),
                    PngImage = GetImageBytes(root, "pngImage", stream),
                    ImageWidth = GetInteger(root, "imageWidth", true).Value,
                    ImageHeight = GetInteger(root, "imageHeight", true).Value,
                    PixelWidth = GetFloat(root, "pixelWidth", true).Value,
                    PixelHeight = GetFloat(root, "pixelHeight", true).Value,
                };
            }
        }

        /// <summary>
        /// TABLE 25 Child Elements for the CylindricalRepresentation Structure.
        /// </summary>
        public class E57CylindricalRepresentation : IE57Element
        {
            public E57ElementType E57Type => E57ElementType.CylindricalRepresentation;

#region Properties

            /// <summary>
            /// Optional.
            /// JPEG format image data.
            /// </summary>
            public byte[] JpegImage;

            /// <summary>
            /// Optional.
            /// PNG format image data.
            /// </summary>
            public byte[] PngImage;

            /// <summary>
            /// Optional.
            /// PNG format image mask.
            /// </summary>
            public byte[] ImageMask => throw new NotImplementedException();

            /// <summary>
            /// Required.
            /// The image width (in pixels). Shall be positive.
            /// </summary>
            public int ImageWidth;

            /// <summary>
            /// Required.
            /// The image height (in pixels). Shall be positive.
            /// </summary>
            public int ImageHeight;

            /// <summary>
            /// (ImageWidth, ImageHeight).
            /// </summary>
            public V2i ImageSize => new(ImageWidth, ImageHeight);

            /// <summary>
            /// Required.
            /// The closest distance from the cylindrical image surface to the center of projection(that is, the radius of the cylinder) (in meters).
            /// Shall be non-negative.
            /// </summary>
            public double Radius;

            /// <summary>
            /// Required.
            /// TheYcoordinate in the image of the principal point (in pixels). This is the intersection of the z=0 plane with the image.
            /// </summary>
            public double PrincipalPointY;

            /// <summary>
            /// Required.
            /// The width of the pixels in the camera (in meters). Shall be positive.
            /// </summary>
            public double PixelWidth;

            /// <summary>
            /// Required.
            /// The height of the pixels in the camera (in meters). Shall be positive.
            /// </summary>
            public double PixelHeight;

            /// <summary>
            /// (PixelWidth, PixelHeight).
            /// </summary>
            public V2i PixelSize => new(PixelWidth, PixelHeight);

#endregion

            internal static E57CylindricalRepresentation Parse(XElement root, Stream stream)
            {
                if (root == null) return null;
                return new E57CylindricalRepresentation
                {
                    JpegImage = GetImageBytes(root, "jpegImage", stream),
                    PngImage = GetImageBytes(root, "pngImage", stream),
                    ImageWidth = GetInteger(root, "imageWidth", true).Value,
                    ImageHeight = GetInteger(root, "imageHeight", true).Value,
                    Radius = GetFloat(root, "radius", true).Value,
                    PrincipalPointY = GetFloat(root, "principalPointY", true).Value,
                    PixelWidth = GetFloat(root, "pixelWidth", true).Value,
                    PixelHeight = GetFloat(root, "pixelHeight", true).Value,
                };
            }
        }

        /// <summary>
        /// TABLE 26 Child Elements for the CartesianBounds Structure.
        /// </summary>
        public class E57CartesianBounds : IE57Element
        {
            public E57ElementType E57Type => E57ElementType.CartesianBounds;

#region Properties

            /// <summary>
            /// Required.
            /// The extent of the bounding region (in meters).
            /// </summary>
            public Box3d Bounds;

#endregion

            internal static E57CartesianBounds Parse(XElement root) => (root == null) ? null : new E57CartesianBounds
            {
                Bounds = new Box3d(
                    GetFloatRange(root, "xMinimum", "xMaximum"),
                    GetFloatRange(root, "yMinimum", "yMaximum"),
                    GetFloatRange(root, "zMinimum", "zMaximum")
                    ) 
            };
        }

        /// <summary>
        /// TABLE 27 Child Elements for the SphericalBounds Structure.
        /// </summary>
        public class E57SphericalBounds : IE57Element
        {
            public E57ElementType E57Type => E57ElementType.SphericalBounds;

#region Properties

            /// <summary>
            /// Required.
            /// The extent of the bounding region in the r direction (in meters). Shall be non-negative.
            /// </summary>
            public Range1d Range;

            /// <summary>
            /// Required.
            /// The extent of the bounding region in the φ direction (in radians). Shall be in the interval[-π/2, π/2].
            /// </summary>
            public Range1d Elevation;

            /// <summary>
            /// Required.
            /// The azimuth angle defining the extent of the bounding region in the θ direction (in radians). Shall be in the interval(-π, π].
            /// </summary>
            public Range1d Azimuth;

#endregion

            internal static E57SphericalBounds Parse(XElement root) => (root == null) ? null : new E57SphericalBounds
            {
                Range = GetFloatRange(root, "rangeMinimum", "rangeMaximum"),
                Elevation = GetFloatRange(root, "elevationMinimum", "elevationMaximum"),
                Azimuth = GetFloatRange(root, "azimuthStart", "azimuthEnd"),
            };
        }

        /// <summary>
        /// TABLE 28 Child Elements for the IndexBounds Structure. 
        /// </summary>
        public class E57IndexBounds : IE57Element
        {
            public E57ElementType E57Type => E57ElementType.IndexBounds;

#region Properties

            /// <summary>
            /// Required.
            /// The rowIndex range of any point represented by this IndexBounds object.
            /// </summary>
            public Range1i? Row;

            /// <summary>
            /// Required.
            /// The columnIndex range of any point represented by this IndexBounds object.
            /// </summary>
            public Range1i? Column;

            /// <summary>
            /// Required.
            /// The returnIndex range of any point represented by this IndexBounds object.
            /// </summary>
            public Range1i? Return;

#endregion

            internal static E57IndexBounds Parse(XElement root) => (root == null) ? null : new E57IndexBounds
            {
                Row = TryGetIntegerRange(root, "rowMinimum", "rowMaximum"),
                Column = TryGetIntegerRange(root, "columnMinimum", "columnMaximum"),
                Return = TryGetIntegerRange(root, "returnMinimum", "returnMaximum"),
            };
        }

        /// <summary>
        /// TABLE 29 Child Elements for IntensityLimits Structure. 
        /// </summary>
        public class E57IntensityLimits : IE57Element
        {
            public E57ElementType E57Type => E57ElementType.IntensityLimits;

#region Properties

            /// <summary>
            /// Required.
            /// The producible intensity range. Unit is unspecified.
            /// </summary>
            public Range1d Intensity;

#endregion

            internal static E57IntensityLimits Parse(XElement root)
            {
                if (root == null) return null;
                return new E57IntensityLimits
                {
                    Intensity = GetRange(root, "intensityMinimum", "intensityMaximum")
                };
            }
        }

        /// <summary>
        /// TABLE 30 Child Elements for the ColorLimits Structure. 
        /// </summary>
        public class E57ColorLimits : IE57Element
        {
            public E57ElementType E57Type => E57ElementType.ColorLimits;

#region Properties

            /// <summary>
            /// Required.
            /// The producible red color range. Unit is unspecified.
            /// </summary>
            public Range1d Red;

            /// <summary>
            /// Required.
            /// The producible green color range. Unit is unspecified.
            /// </summary>
            public Range1d Green;

            /// <summary>
            /// Required.
            /// The producible blue color range. Unit is unspecified.
            /// </summary>
            public Range1d Blue;

#endregion

            internal static E57ColorLimits Parse(XElement root)
            {
                if (root == null) return null;
                return new E57ColorLimits
                {
                    Red = GetRange(root, "colorRedMinimum", "colorRedMaximum"),
                    Green = GetRange(root, "colorGreenMinimum", "colorGreenMaximum"),
                    Blue = GetRange(root, "colorBlueMinimum", "colorBlueMaximum"),
                };
            }
        }
        
        /// <summary>
        /// TABLE 31 Child Elements for DateTime Structure.
        /// </summary>
        public class E57DateTime : IE57Element
        {
            public E57ElementType E57Type => E57ElementType.E57DateTime;

#region Properties

            /// <summary>
            /// Required.
            /// The time, in seconds, since GPS start epoch. This time specification may include fractions of a second.
            /// The GPS start epoch occured at 0 h UTC (12:00 midnight) on January 6, 1980.
            /// </summary>
            public double DateTimeValue;
            
            /// <summary>
            /// Required.
            /// This element shall be present, and its value set to 1 if, and only if, the time stored in the dateTimeValue element is obtained from an atomic clock time source.
            /// Shall be either 0 or 1.
            /// </summary>
            public bool IsAtomicClockReferenced;
            
            /// <summary>
            /// The time.
            /// </summary>
            public DateTimeOffset DateTime => GpsStartEpoch + TimeSpan.FromSeconds(DateTimeValue);
            internal static readonly DateTimeOffset GpsStartEpoch = new(1980, 01, 06, 0, 0, 0, TimeSpan.Zero);
            internal static readonly DateTimeOffset UnixStartEpoch = new(1970, 01, 01, 0, 0, 0, TimeSpan.Zero);

#endregion

            internal static E57DateTime Parse(XElement root) => (root == null) ? null : new E57DateTime
            {
                DateTimeValue = GetFloat(root, "dateTimeValue", true).Value,
                IsAtomicClockReferenced = GetBool(root, "isAtomicClockReferenced", false).Value
            };
        }

        /// <summary>
        /// E57 CompressedVector Header (9.3. CompressedVector Binary Section).
        /// TABLE 35 Fields for the CompressedVector Header.
        /// </summary>
        public struct E57CompressedVectorHeader
        {
            /// <summary>
            /// E57_COMPRESSED_VECTOR_SECTION (value = 1).
            /// </summary>
            internal byte SectionId;
            /// <summary>
            /// Reserved bytes for future versions of the standard. Shall all be 0.
            /// </summary>
            internal byte[] Reserved;
            /// <summary>
            /// The logical length of the CompressedVector binary section (in bytes).
            /// </summary>
            internal long SectionLength;
            /// <summary>
            /// The file offset of the first data packet in this binary section (in bytes).
            /// </summary>
            internal E57PhysicalOffset DataStartOffset;
            /// <summary>
            /// The file offset to the root level index packet in this binary section (in bytes).
            /// </summary>
            internal E57PhysicalOffset IndexStartOffset;

            internal static E57CompressedVectorHeader Parse(byte[] buffer) => new()
            {
                SectionId = Check("SectionId", buffer[0], x => x == 1, "1"),
                Reserved = Check("Reserved", buffer.Copy(1, 7), xs => xs.All(x => x == 0), "(0,0,0,0,0,0,0)"),
                SectionLength = BitConverter.ToInt64(buffer, 8),
                DataStartOffset = new E57PhysicalOffset(BitConverter.ToInt64(buffer, 16)),
                IndexStartOffset = new E57PhysicalOffset(BitConverter.ToInt64(buffer, 24)),
            };
        }

        /// <summary>
        /// E57 Index Packet Header (9.4. Index Packe).
        /// TABLE 36 Format of an Index Packet Header.
        /// </summary>
        public struct E57IndexPacketHeader
        {
            /// <summary>
            /// E57_INDEX_PACKET (value = 0).
            /// </summary>
            internal byte PacketType;
            /// <summary>
            /// Reserved for future versions of the standard. Shall all be 0.
            /// </summary>
            internal byte Reserved1;
            /// <summary>
            /// One less than the logical length of the packet (in bytes). Shall be in the interval (0, 2^16).
            /// </summary>
            internal ushort PacketLengthMinus1;
            /// <summary>
            /// The number ofu sed address entries in this packet. Shall be in the interval [1, 2048].
            /// </summary>
            internal ushort EntryCount;
            /// <summary>
            /// The level of this index packet in the tree if index packets.
            /// The bottom (leaf) level is zero. Shall be in the interval [0, 5].
            /// </summary>
            internal byte IndexLevel;
            /// <summary>
            /// Reserved bytes for future versions of the standard. Shall all be zero.
            /// </summary>
            internal byte[] Reserved2;

            internal static E57IndexPacketHeader Parse(byte[] buffer) => new()
            {
                PacketType = Check("PacketType", buffer[0], x => x == 0, "0"),
                Reserved1 = Check("Reserved1", buffer[1], x => x == 0, "0"),
                PacketLengthMinus1 = BitConverter.ToUInt16(buffer, 2),
                EntryCount = Check("EntryCount", BitConverter.ToUInt16(buffer, 4), x => x >= 1 || x <= 2048, "[1, 2048]"),
                IndexLevel = Check("IndexLevel", buffer[6], x => x >= 0 || x <= 5, "[0, 5]"),
                Reserved2 = Check("Reserved2", buffer.Copy(7, 8), xs => xs.All(x => x == 0), "(0,0,0,0,0,0,0,0)"),
            };
        }

        /// <summary>
        /// E57 Index Packet Address Entry (9.4. Index Packet).
        /// TABLE 37 Format of Index Packet Address Entries.
        /// </summary>
        public struct E57IndexPacketAddressEntry
        {
            /// <summary>
            /// The index of the first record stored in this chunk (for leaf nodes),
            /// or the index of the first record stored in any chunks within the sub-tree(for non-leaf nodes).
            /// Shall be in the interval[0, 2^63).
            /// </summary>
            internal E57PhysicalOffset ChunkRecordIndex;
            /// <summary>
            /// The file offset to a data packet (for leaf nodes) or to a lower level index packet(for non-leaf nodes).
            /// Shall be in the interval(0, 2^63).
            /// </summary>
            internal E57PhysicalOffset PacketOffset;

            internal static E57IndexPacketAddressEntry Parse(byte[] buffer) => new()
            {
                ChunkRecordIndex = new E57PhysicalOffset(BitConverter.ToInt64(buffer, 0)),
                PacketOffset = new E57PhysicalOffset(BitConverter.ToInt64(buffer, 8))
            };
        }

        /// <summary>
        /// E57 Data Packet Header (9.5. Data Packet).
        /// TABLE 38 Format of a Data Packet Header.
        /// </summary>
        public struct E57DataPacketHeader
        {
            /// <summary>
            /// E57_DATA_PACKET (value = 1).
            /// </summary>
            internal byte PacketType;
            /// <summary>
            /// Packet flag field (described in 9.5.7).
            /// </summary>
            internal byte PacketFlags;
            /// <summary>
            /// One less than the logical length of the packet (in bytes).
            /// To maintain alignment, the packet may be padded with up to three zero-valued bytes after the last used field.
            /// The length includes any zero padding that is present. Shall be in the interval (0,2^16).
            /// </summary>
            internal ushort PacketLengthMinus1;
            /// <summary>
            /// The number of bytestreams in this packet. Shall be in the interval [0, 32763].
            /// </summary>
            internal ushort ByteStreamCount;
            
            internal bool CompressorRestart => (PacketFlags & 0b00000001) != 0;
            
            internal static E57DataPacketHeader Parse(byte[] buffer) => new()
            {
                PacketType = Check("PacketType", buffer[0], x => x == 1, "1"),
                PacketFlags = Check("PacketFlags", buffer[1], x => x <= 1, "[0,1]"),
                PacketLengthMinus1 = BitConverter.ToUInt16(buffer, 2),
                ByteStreamCount = BitConverter.ToUInt16(buffer, 4),
            };
        }

        private static T Check<T>(string name, T x, Func<T, bool> verify, string shouldBe)
        {
            if (!verify(x)) throw new Exception(
                $"[E57] Invalid field <{name}> in E57 CompressedVectorHeader. Should be \"{shouldBe}\" but is \"{x}\"."
                );
            return x;
        }

#region File Helpers

        /// <summary>
        /// Verifies file checksums (CRC).
        /// </summary>
        public static void VerifyChecksums(Stream stream, long streamLengthInBytes)
        {
            stream.Position = 0;

            if (streamLengthInBytes % 1024 != 0) throw new Exception(
                $"[E57] Invalid file size. Must be multiple of 1024, but is {streamLengthInBytes:N0}."
                );

            var imax = streamLengthInBytes / 1024;
            var buffer = new byte[1024];
            for (var i = 0; i < imax; i++)
            {
                if (stream.Read(buffer, 0, 1024) != 1024) throw new InvalidOperationException();

                // checksum is big endian unsigned 32-bit int
                var crcStored = (uint)((buffer[1020] << 24) + (buffer[1021] << 16) + (buffer[1022] << 8) + buffer[1023]);
                
#if false // Crc32C.NET 1.0.5 is not compatible with netstandard2.0
                var crcComputed = Crc32C.Crc32CAlgorithm.Compute(buffer, 0, 1020);
                if (crcStored != crcComputed) throw new Exception(
                    $"[E57] Bad file. Checksum at file offset 0x{stream.Position - 4:X} should be 0x{crcComputed:X} (instead of 0x{crcStored:X})."
                    );
#endif
            }
        }

        /// <summary>
        /// Read given number of logical bytes (excluding 32-bit CRC at the end of each 1024 byte page) from stream,
        /// starting at raw stream position 'start'.
        /// </summary>
        internal static byte[] ReadLogicalBytes(Stream stream, E57PhysicalOffset start, int countLogical)
        {
            checked
            {
                if (countLogical < 0) throw new ArgumentException(nameof(countLogical));

                stream.Position = start.Value;

                var buffer = new byte[countLogical];
                var i = 0;
                while (countLogical > 0)
                {
                    var bytesLeftInPage = 1020 - (int)(stream.Position % 1024);
                    if (bytesLeftInPage > countLogical) bytesLeftInPage = countLogical;
                    var bytesRead = stream.Read(buffer, i, bytesLeftInPage);
                    if (bytesRead != bytesLeftInPage) throw new InvalidOperationException();
                    stream.Position += 4; // skip CRC
                    i += bytesLeftInPage;
                    countLogical -= bytesLeftInPage;
                }

                return buffer;
            }
        }

        /// <summary>
        /// Read given number of logical bytes (excluding 32-bit CRC at the end of each 1024 byte page) from stream,
        /// starting at logical stream position 'start'.
        /// </summary>
        internal static byte[] ReadLogicalBytes(Stream stream, E57LogicalOffset start, int countLogical)
            => ReadLogicalBytes(stream, (E57PhysicalOffset)start, countLogical);

        /// <summary>
        /// Read given number of logical unsigned shorts (excluding 32-bit CRC at the end of each 1024 byte page) from stream,
        /// starting at raw stream position 'startPhysical'.
        /// </summary>
        public static ushort[] ReadLogicalUnsignedShorts(Stream stream, E57PhysicalOffset start, int count)
        {
            var buffer = ReadLogicalBytes(stream, start, count * 2);
            var xs = new ushort[count];
            for (var i = 0; i < count; i++) xs[i] = BitConverter.ToUInt16(buffer, i << 1);
            return xs;
        }

        /// <summary>
        /// Read given number of logical unsigned shorts (excluding 32-bit CRC at the end of each 1024 byte page) from stream,
        /// starting at raw stream position 'startPhysical'.
        /// </summary>
        public static ushort[] ReadLogicalUnsignedShorts(Stream stream, E57LogicalOffset start, int count)
            => ReadLogicalUnsignedShorts(stream, (E57PhysicalOffset)start, count);

#endregion

#region XML Helpers

        private const string DEFAULT_NAMESPACE = "http://www.astm.org/COMMIT/E57/2010-e57-v1.0";
        private static T Ex<T>(string s, string should, string actual) { Ex(s, should, actual); return default; }
        private static void Ex(string s, string should, string actual)
            => throw new Exception($"[E57][XML] Invalid element {s}. Should be \"{should}\", but is \"{actual}\".");
        private static void EnsureElementType(XElement e, string type)
        {
            if (e.Attribute("type").Value != type) Ex("type", type, e.Attribute("type").Value);
        }
        private static string GetElementType(XElement e) => e.Attribute("type").Value;
        private static void EnsureElementName(XElement e, string name)
        {
            if (e.Name.LocalName != name) Ex("name", name, e.Name.LocalName);
        }
        private static void EnsureElementNamespace(XElement e, string name)
        {
            if (e.Name.NamespaceName != name) Ex("namespace", name, e.Name.NamespaceName);
        }
        private static void EnsureElementNameAndType(XElement e, string name, string type)
        {
            EnsureElementName(e, name);
            EnsureElementType(e, type);
            EnsureElementNamespace(e, DEFAULT_NAMESPACE);
        }
        private static XElement GetElement(XElement e, string name)
            => e.Element(XName.Get(name, DEFAULT_NAMESPACE));
        private static IEnumerable<XElement> GetElements(XElement e, string name)
            => e.Elements(XName.Get(name, DEFAULT_NAMESPACE));
        private static string GetString(XElement root, string elementName, bool required, string mustBe = null)
            => GetValue(root, elementName, required, "String", x => mustBe == null || x == mustBe, x => x, mustBe, null);
        private static int? GetInteger(XElement root, string elementName, bool required, int? mustBe = null)
            => GetValue<int?>(root, elementName, required, "Integer", x => !mustBe.HasValue || x == mustBe.Value,
                x => x != null ? int.Parse(x) : 0, mustBe, null);
        private static double? GetScaledInteger(XElement root, string elementName, bool required, int? mustBe = null)
        {
            //return GetValue<int?>(root, elementName, required, "ScaledInteger", x => mustBe.HasValue ? x == mustBe.Value : true,
            //       x => x != null ? int.Parse(x) : 0, mustBe, null);

            var x = GetElement(root, elementName);
            if (x == null)
            {
                if (required) throw new Exception($"[E57] Element <{elementName}> is required! In {root}.");
                return null;
            }
            EnsureElementNameAndType(x, elementName, "ScaledInteger");
            var isEmpty = string.IsNullOrWhiteSpace(x.Value);
            var valueRaw = isEmpty ? null : x.Value;
            var value = valueRaw != null ? int.Parse(valueRaw) : 0;
            if (!(!mustBe.HasValue || value == mustBe.Value)) throw new Exception(
                $"[E57] Element <{elementName}> shall have value \"{mustBe}\", but has \"{value}\". In {root}."
                );

            var scale = GetFloatAttribute(x, "scale");
            var result = value * scale;

            return result;
        }
        private static long? GetLong(XElement root, string elementName, bool required, int? mustBe = null)
            => GetValue<long?>(root, elementName, required, "Integer", x => !mustBe.HasValue || x == mustBe.Value,
                x => x != null ? long.Parse(x) : 0, mustBe, null);
        private static bool? GetBool(XElement root, string elementName, bool required, int? mustBe = null)
            => GetInteger(root, elementName, required, mustBe) == 1;
        private static double? GetFloat(XElement root, string elementName, bool required, double? mustBe = null)
            => GetValue<double?>(root, elementName, required, "Float", x => !mustBe.HasValue || x == mustBe.Value,
                x => x != null ? double.Parse(x, CultureInfo.InvariantCulture) : 0.0, mustBe, null);
        private static byte[] GetImageBytes(XElement root, string elementName, Stream stream)
        {
            var element = GetElement(root, elementName);
            if (element == null) return null;
            var blob = E57Blob.Parse(GetElement(root, elementName));
            // Mysterious!  Why is +16 required? 
            var start = blob.FileOffset.Value + 16;
            // If start falls in checksum region move it forward the the next page
            if(start % 1024 >= 1020)
            {
                start += 1024 - (start % 1024);
            }
            var offest = new E57PhysicalOffset(start);
            return ReadLogicalBytes(stream, offest, (int)blob.Length);            
        }
        private static double? GetFloatOrInteger(XElement root, string elementName, bool required)
        {
            var type = GetElementType(GetElement(root, elementName));
            return type switch
            {
                "Float" => GetFloat(root, elementName, required),
                "Integer" => GetInteger(root, elementName, required),
                "ScaledInteger" => GetScaledInteger(root, elementName, required),
                _ => throw new NotImplementedException($"Unknown type '{type}'. Invariant a55575af-73cc-439f-ba63-a46883b8f6cd."),
            };
        }

        private static Range1i GetIntegerRange(XElement root, string elementNameMin, string elementNameMax) => new(GetInteger(root, elementNameMin, true).Value, GetInteger(root, elementNameMax, true).Value);
        private static Range1i? TryGetIntegerRange(XElement root, string elementNameMin, string elementNameMax)
        {
            var min = GetInteger(root, elementNameMin, false);
            var max = GetInteger(root, elementNameMax, false);

            return min.HasValue
                ? (max.HasValue
                    ? new Range1i(min.Value, max.Value)
                    : throw new Exception($"[E57] Element <{elementNameMax}> is required! In {root}.")
                    )
                : (max.HasValue
                    ? throw new Exception($"[E57] Element <{elementNameMin}> is required! In {root}.")
                    : (Range1i?)null
                    )
                ;
        }
        private static Range1d GetFloatRange(XElement root, string elementNameMin, string elementNameMax)
            => new(GetFloat(root, elementNameMin, true).Value, GetFloat(root, elementNameMax, true).Value);

        private static Range1d? TryGetFloatRange(XElement root, string elementNameMin, string elementNameMax)
        {
            var min = GetFloat(root, elementNameMin, false);
            var max = GetFloat(root, elementNameMax, false);

            return min.HasValue
                ? (max.HasValue
                    ? new Range1d(min.Value, max.Value)
                    : throw new Exception($"[E57] Element <{elementNameMax}> is required! In {root}.")
                    )
                : (max.HasValue
                    ? throw new Exception($"[E57] Element <{elementNameMin}> is required! In {root}.")
                    : (Range1d?)null
                    )
                ;
        }
        private static Range1d GetRange(XElement root, string elementNameMin, string elementNameMax)
            => new(GetFloatOrInteger(root, elementNameMin, true).Value, GetFloatOrInteger(root, elementNameMax, true).Value);
        private static V3d GetTranslation(XElement root)
            => new(GetFloat(root, "x", true).Value, GetFloat(root, "y", true).Value, GetFloat(root, "z", true).Value);
        private static Rot3d GetQuaternion(XElement root)
            => new(GetFloat(root, "w", true).Value, GetFloat(root, "x", true).Value, GetFloat(root, "y", true).Value, GetFloat(root, "z", true).Value);
        private static T GetValue<T>(XElement root, string elementName, bool required, string typename, Func<T, bool> verify, Func<string, T> parse, object mustBe, T defaultValue)
        {
            var x = GetElement(root, elementName);
            if (x == null)
            {
                if (required) throw new Exception($"[E57] Element <{elementName}> is required! In {root}.");
                return defaultValue;
            }
            EnsureElementNameAndType(x, elementName, typename);
            var isEmpty = string.IsNullOrWhiteSpace(x.Value);
            var result = parse(isEmpty ? null : x.Value);
            if (!verify(result)) throw new Exception(
                $"[E57] Element <{elementName}> shall have value \"{mustBe}\", but has \"{result}\". In {root}."
                );
            return result;
        }

        private static E57PhysicalOffset GetPhysicalOffsetAttribute(XElement root, string elementName) => new(long.Parse(root.Attribute(elementName).Value));
        private static long GetLongAttribute(XElement root, string elementName) => long.Parse(root.Attribute(elementName).Value);
        private static long? TryGetLongAttribute(XElement root, string elementName)
        {
            var a = root.Attribute(elementName);
            return (a != null) ? long.Parse(a.Value) : (long?)null;
        }
        private static double GetFloatAttribute(XElement root, string elementName) => double.Parse(root.Attribute(elementName).Value, CultureInfo.InvariantCulture);
        private static double? TryGetFloatAttribute(XElement root, string elementName)
        {
            var a = root.Attribute(elementName);
            return (a != null) ? double.Parse(a.Value, CultureInfo.InvariantCulture) : (double?)null;
        }

        private static IE57Element ParseE57Element(XElement root, Stream stream, bool verbose)
        {
            return (GetElementType(root)) switch
            {
                "Integer" => E57Integer.Parse(root),
                "ScaledInteger" => E57ScaledInteger.Parse(root),
                "Float" => E57Float.Parse(root),
                "String" => E57String.Parse(root),
                "Blob" => E57Blob.Parse(root),
                "Structure" => E57Structure.Parse(root, stream, verbose),
                "Vector" => E57Vector.Parse(root, stream, verbose),
                "CompressedVector" => E57CompressedVector.Parse(root, stream, verbose),
                "Codec" => E57Codec.Parse(root, stream, verbose),
                "E57Root" => E57Root.Parse(root, stream, verbose),
                "Data3D" => E57Data3D.Parse(root, stream, verbose),
                "PointRecord" => E57PointRecord.Parse(root),
                "PointGroupingSchemes" => E57PointGroupingSchemes.Parse(root),
                "GroupingByLine" => GroupingByLine.Parse(root),
                "LineGroupRecord" => E57LineGroupRecord.Parse(root),
                "RigidBodyTransform" => E57RigidBodyTransform.Parse(root),
                //case "Quaternion":
                //case "Translation":
                "Image2d" => E57Image2D.Parse(root, stream),
                "VisualReferenceRepresentation" => E57VisualReferenceRepresentation.Parse(root, stream),
                "PinholeRepresentation" => E57PinholeRepresentation.Parse(root, stream),
                "SphericalRepresentation" => E57SphericalRepresentation.Parse(root, stream),
                "CylindricalRepresentation" => E57CylindricalRepresentation.Parse(root, stream),
                "CartesianBounds" => E57CartesianBounds.Parse(root),
                "SphericalBounds" => E57SphericalBounds.Parse(root),
                "IndexBounds" => E57IndexBounds.Parse(root),
                "IntensityLimits" => E57IntensityLimits.Parse(root),
                "ColorLimits" => E57ColorLimits.Parse(root),
                "E57DateTime" => E57DateTime.Parse(root),
                _ => throw new NotImplementedException($"[E57] Unknown E57 type <{GetElementType(root)}>"),
            };
        }

#endregion
    }

#pragma warning restore IDE0051 // Remove unused private members
#pragma warning restore 1591
}
