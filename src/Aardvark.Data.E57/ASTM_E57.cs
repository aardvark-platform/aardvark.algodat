/*
    Copyright (C) 2017. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Aardvark.Base;

namespace Aardvark.Data.E57
{
    /// <summary>
    /// E57 (ASTM E2807-11)
    /// Standard Specification for 3D Imaging Data Exchange, Version 1.0.
    /// https://www.astm.org/Standards/E2807.htm
    /// </summary>
    public static class ASTM_E57
    {
        /// <summary>
        /// E57 File Header (7. File Header Section).
        /// TABLE 1 Format of the E57 File Header Section.
        /// </summary>
        public class E57FileHeader
        {
            /// <summary>
            /// </summary>
            public string FileSignature;
            /// <summary>
            /// </summary>
            public uint VersionMajor;
            /// <summary>
            /// </summary>
            public uint VersionMinor;
            /// <summary>
            /// </summary>
            public ulong FileLength;
            /// <summary>
            /// </summary>
            public ulong XmlOffset;
            /// <summary>
            /// </summary>
            public ulong XmlLength;
            /// <summary>
            /// </summary>
            public ulong PageSize;
            /// <summary>
            /// </summary>
            public XElement RawXml;
            /// <summary>
            /// </summary>
            public E57Root E57Root;
            
            /// <summary>
            /// </summary>
            public static E57FileHeader Parse(Stream stream)
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
                    XmlOffset = BitConverter.ToUInt64(buffer, 24),
                    XmlLength = BitConverter.ToUInt64(buffer, 32),
                    PageSize = Check("PageSize", BitConverter.ToUInt64(buffer, 40), x => x == 1024, "1024"),
                };

                var xmlBuffer = ReadLogicalBytes(stream, (long)h.XmlOffset, (int)h.XmlLength);
                var xmlString = Encoding.UTF8.GetString(xmlBuffer);
                h.RawXml = XElement.Parse(xmlString, LoadOptions.SetBaseUri);
                h.E57Root = E57Root.Parse(h.RawXml, stream);

                return h;
            }
        }

        /// <summary>
        /// </summary>
        public enum E57ElementType
        {
            /// <summary></summary>
            Integer,                        // Table 2
            /// <summary></summary>
            ScaledInteger,                  // Table 3
            /// <summary></summary>
            Float,                          // Table 4
            /// <summary></summary>
            String,                         // Table 5
            /// <summary></summary>
            Blob,                           // Table 6
            /// <summary></summary>
            Structure,                      // Table 7
            /// <summary></summary>
            Vector,                         // Table 8
            /// <summary></summary>
            CompressedVector,               // Table 9, Table 10
            /// <summary></summary>
            E57Codec,                       // Table 11
            /// <summary></summary>
            E57Root,                        // Table 12
            /// <summary></summary>
            Data3D,                         // Table 13
            /// <summary></summary>
            PointRecord,                    // Table 14
            /// <summary></summary>
            PointGroupingSchemes,           // Table 15
            /// <summary></summary>
            GroupingByLine,                 // Table 16
            /// <summary></summary>
            LineGroupRecord,                // Table 17
            /// <summary></summary>
            RigidBodyTransform,             // Table 18
            /// <summary></summary>
            Quaternion,                     // Table 19
            /// <summary></summary>
            Translation,                    // Table 20
            /// <summary></summary>
            Image2d,                        // Table 21
            /// <summary></summary>
            VisualReferenceRepresentation,  // Table 22
            /// <summary></summary>
            PinholeRepresentation,          // Table 23
            /// <summary></summary>
            SphericalRepresentation,        // Table 24
            /// <summary></summary>
            CylindricalRepresentation,      // Table 25
            /// <summary></summary>
            CartesianBounds,                // Table 26
            /// <summary></summary>
            SphericalBounds,                // Table 27
            /// <summary></summary>
            IndexBounds,                    // Table 28
            /// <summary></summary>
            IntensityLimits,                // Table 29
            /// <summary></summary>
            ColorLimits,                    // Table 30
            /// <summary></summary>
            E57DateTime,                    // Table 31
        }

        /// <summary></summary>
        public interface IE57Element
        {
            /// <summary></summary>
            E57ElementType E57Type { get; }
        }

        /// <summary>
        /// TABLE 2 Attributes for an Integer Type E57 Element.
        /// </summary>
        public class E57Integer : IE57Element
        {
            /// <summary></summary>
            public E57ElementType E57Type => E57ElementType.Integer;

            #region Properties

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

            /// <summary></summary>
            public long Value;

            #endregion
            
            internal static E57Integer Parse(XElement root)
            {
                if (root == null) return null;
                EnsureElementType(root, "Integer");
                var value = string.IsNullOrWhiteSpace(root.Value) ? 0 : long.Parse(root.Value);
                var min = GetOptionalLongAttribute(root, "minimum");
                var max = GetOptionalLongAttribute(root, "maximum");
                if (max < min) Ex("Integer.maximum", $">= minimum ({min})", $"{max}");
                return new E57Integer { Minimum = min, Maximum = max, Value = value };
            }
        }

        /// <summary>
        /// TABLE 3 Attributes for a ScaledInteger Type E57 Element.
        /// </summary>
        public class E57ScaledInteger : IE57Element
        {
            /// <summary></summary>
            public E57ElementType E57Type => E57ElementType.ScaledInteger;

            #region Properties

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

            #endregion
            
            internal static E57ScaledInteger Parse(XElement root)
            {
                if (root == null) return null;
                EnsureElementType(root, "ScaledInteger");
                var raw = string.IsNullOrWhiteSpace(root.Value) ? 0 : long.Parse(root.Value);
                var min = GetOptionalLongAttribute(root, "minimum");
                var max = GetOptionalLongAttribute(root, "maximum");
                if (max < min) Ex("ScaledInteger.maximum", $">= minimum ({min})", $"{max}");
                var scale = GetOptionalFloatAttribute(root, "scale") ?? 1.0;
                var offset = GetOptionalFloatAttribute(root, "offset") ?? 0.0;
                return new E57ScaledInteger
                {
                    Minimum = min, Maximum = max, Scale = scale, Offset = offset,
                    RawValue = raw, Value = raw * scale + offset
                };
            }
        }

        /// <summary>
        /// TABLE 4 Attributes for a Float Type E57 Element.
        /// </summary>
        public class E57Float : IE57Element
        {
            /// <summary></summary>
            public E57ElementType E57Type => E57ElementType.Float;

            #region Properties
            
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

            /// <summary></summary>
            public double Value;

            #endregion
            
            internal static E57Float Parse(XElement root)
            {
                if (root == null) return null;
                EnsureElementType(root, "Float");
                var value = string.IsNullOrWhiteSpace(root.Value) ? 0 : double.Parse(root.Value, CultureInfo.InvariantCulture);
                var min = GetOptionalFloatAttribute(root, "minimum");
                var max = GetOptionalFloatAttribute(root, "maximum");
                if (max < min) Ex("Float.maximum", $">= minimum ({min})", $"{max}");
                return new E57Float { Minimum = min, Maximum = max, Value = value };
            }
        }

        /// <summary>
        /// TABLE 4 Attributes for a Float Type E57 Element.
        /// </summary>
        public class E57String : IE57Element
        {
            /// <summary></summary>
            public E57ElementType E57Type => E57ElementType.String;

            #region Properties

            /// <summary>
            /// String value.
            /// </summary>
            public string Value;

            #endregion

            internal static E57String Parse(XElement root)
            {
                if (root == null) return null;
                EnsureElementType(root, "String");
                return new E57String
                {
                    Value = root.Value
                };
            }
        }

        /// <summary>
        /// TABLE 6 Attributes of a Blob Type E57 Element.
        /// </summary>
        public class E57Blob : IE57Element
        {
            /// <summary></summary>
            public E57ElementType E57Type => E57ElementType.Blob;

            #region Properties

            /// <summary>
            /// Required.
            /// The physical file offset of the start of the associated binary Blob section in the E57 file. Shall be in the interval [0, 2^63).
            /// </summary>
            public long FileOffset;

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
                    FileOffset = GetLongAttribute(root, "fileOffset"),
                    Length = GetLongAttribute(root, "length")
                };
            }
        }

        /// <summary>
        /// TABLE 7 Attributes for a Structure Type E57 Element.
        /// </summary>
        public class E57Structure : IE57Element
        {
            /// <summary></summary>
            public E57ElementType E57Type => E57ElementType.Structure;

            #region Properties

            /// <summary>
            /// 
            /// </summary>
            public IE57Element[] Children;

            #endregion

            internal static E57Structure Parse(XElement root, Stream stream)
            {
                if (root == null) return null;
                EnsureElementType(root, "Structure");
                return new E57Structure
                {
                    Children = root.Elements().Select(x => ParseE57Element(x, stream)).ToArray()
                };
            }
        }

        /// <summary>
        /// TABLE 8 Attributes for a Vector Type E57 Element.
        /// </summary>
        public class E57Vector : IE57Element
        {
            /// <summary></summary>
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

            internal static E57Vector Parse(XElement root, Stream stream)
            {
                if (root == null) return null;
                EnsureElementType(root, "Vector");
                return new E57Vector
                {
                    AllowHeterogenousChildren = GetOptionalLongAttribute(root, "allowHeterogeneousChildren") == 1,
                    Children = root.Elements().Select(x => ParseE57Element(x, stream)).ToArray()
                };
            }
        }

        /// <summary>
        /// TABLE 9 Attributes for a CompressedVector Type E57 Element.
        /// TABLE 10 Child Elements for a CompressedVector Type E57 Element.
        /// </summary>
        public class E57CompressedVector : IE57Element
        {
            /// <summary></summary>
            public E57ElementType E57Type => E57ElementType.CompressedVector;

            #region Properties

            /// <summary>
            /// Required.
            /// The physical file offset of the start of the CompressedVector binary section in the E57 file(an integer). Shall be in the interval(0, 2^63).
            /// </summary>
            public long FileOffset;

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
            public object Codecs;

            #endregion

            internal static E57CompressedVector Parse(XElement root, Stream stream)
            {
                if (root == null) return null;

                var v = new E57CompressedVector
                {
                    FileOffset =  GetLongAttribute(root, "fileOffset"), 
                    RecordCount = GetLongAttribute(root, "recordCount"),
                    Prototype = E57Structure.Parse(GetElement(root, "prototype"), stream),
                    Codecs = null, // TODO
                };
                
                var compressedVectorHeader = E57CompressedVectorHeader.Parse(ReadLogicalBytes(stream, v.FileOffset, 32));

                var dataPacketHeader = E57DataPacketHeader.Parse(ReadLogicalBytes(stream, v.FileOffset + 32, 6));
                var bytestreamBufferLengths = ReadLogicalUnsignedShorts(stream, v.FileOffset + 32 + 6, dataPacketHeader.ByteStreamCount);

                var foo = ReadLogicalBytes(stream, v.FileOffset, 256);
                
                return v;
            }
        }
        
        /// <summary>
        /// TABLE 11 Child Elements for the Codec Structure.
        /// </summary>
        public class E57Codec : IE57Element
        {
            /// <summary></summary>
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

            internal static E57Codec Parse(XElement root, Stream stream)
            {
                if (root == null) return null;
                return new E57Codec
                {
                    Inputs = E57Vector.Parse(GetElement(root, "inputs"), stream),
                    BitPackCodec = E57Structure.Parse(GetElement(root, "bitPackCodec"), stream),
                };
            }
        }
        
        /// <summary>
        /// E57 Root (8.4 XML Data Hierarchy).
        /// TABLE 12 Child Elements for the E57Root Structure.
        /// </summary>
        public class E57Root : IE57Element
        {
            /// <summary></summary>
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

            internal static E57Root Parse(XElement root, Stream stream)
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
                    Data3D = ASTM_E57.E57Data3D.ParseVectorChildren(GetElement(root, "data3D"), stream),
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
            /// <summary></summary>
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

            internal static E57Data3D Parse(XElement root, Stream stream)
            {
                EnsureElementNameAndType(root, "vectorChild", "Structure");
                
                var data3d = new E57Data3D
                {
                    Guid = GetString(root, "guid", true),
                    Points = E57CompressedVector.Parse(GetElement(root, "points"), stream),
                    Pose = E57RigidBodyTransform.Parse(GetElement(root, "pose")),
                    OriginalGuids = E57Vector.Parse(GetElement(root, "originalGuids"), stream),
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
                };
                
                return data3d;
            }

            internal static E57Data3D[] ParseVectorChildren(XElement root, Stream stream)
            {
                if (root == null) return null;
                EnsureElementNameAndType(root, "data3D", "Vector");
                return GetElements(root, "vectorChild").Select(x => Parse(x, stream)).ToArray();
            }
        }

        /// <summary>
        /// TABLE 14 Child Elements for the PointRecord Structure.
        /// </summary>
        public class E57PointRecord : IE57Element
        {
            /// <summary></summary>
            public E57ElementType E57Type => E57ElementType.PointRecord;

            #region Properties

            /// <summary>
            /// Optional.
            /// Grouping information by row or column index.
            /// </summary>
            public GroupingByLine GroupingByLine;

            #endregion

            internal static E57PointRecord Parse(XElement root)
            {
                if (root == null) return null;
                return new E57PointRecord
                {
                    GroupingByLine = GroupingByLine.Parse(root)
                };
            }
        }

        /// <summary>
        /// TABLE 15 Child Elements for the PointGroupingSchemes Structure.
        /// </summary>
        public class E57PointGroupingSchemes : IE57Element
        {
            /// <summary></summary>
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
            /// <summary></summary>
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
            /// <summary></summary>
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
            /// <summary></summary>
            public E57ElementType E57Type => E57ElementType.RigidBodyTransform;

            #region Properties

            /// <summary>
            /// Required.
            /// A unit quaternion representing the rotation, R, of the transform.
            /// </summary>
            public Rot3d Rotation;

            /// <summary>
            /// Required.
            /// The translation, t, of the transform.
            /// </summary>
            public V3d Translation;

            #endregion

            internal static E57RigidBodyTransform Parse(XElement root)
            {
                if (root == null) return null;
                var r = GetElement(root, "rotation");
                var t = GetElement(root, "translation");
                return new E57RigidBodyTransform
                {
                    Rotation = r != null ? GetQuaternion(GetElement(root, "rotation")) : Rot3d.Identity,
                    Translation = t != null ? GetTranslation(GetElement(root, "translation")) : V3d.Zero
                };
            }
        }
        
        /// <summary>
        /// E57 Image2D (8.4.11. Image2D)
        /// TABLE 21 Child Elements for the Image2D Structure.
        /// </summary>
        public class E57Image2D : IE57Element
        {
            /// <summary></summary>
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
                    VisualReferenceRepresentation = E57VisualReferenceRepresentation.Parse(root),
                    PinholeRepresentation = E57PinholeRepresentation.Parse(root),
                    SphericalRepresentation = E57SphericalRepresentation.Parse(root),
                    CylindricalRepresentation = E57CylindricalRepresentation.Parse(root),
                    Pose = E57RigidBodyTransform.Parse(root),
                    AssociatedData3DGuid = GetString(root, "associatedData3DGuid", false),
                    Name = GetString(root, "name", false),
                    Description = GetString(root, "description", false),
                    AcquisitionDateTime = E57DateTime.Parse(root),
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
            /// <summary></summary>
            public E57ElementType E57Type => E57ElementType.VisualReferenceRepresentation;

            #region Properties

            /// <summary>
            /// Optional.
            /// JPEG format image data.
            /// </summary>
            public byte[] JpegImage
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            /// <summary>
            /// Optional.
            /// PNG format image data.
            /// </summary>
            public byte[] PngImage
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            /// <summary>
            /// Optional.
            /// PNG format image mask.
            /// </summary>
            public byte[] ImageMask
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

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
            public V2i ImageSize => new V2i(ImageWidth, ImageHeight);

            #endregion

            internal static E57VisualReferenceRepresentation Parse(XElement root)
            {
                if (root == null) return null;
                return new E57VisualReferenceRepresentation
                {
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
            /// <summary></summary>
            public E57ElementType E57Type => E57ElementType.PinholeRepresentation;

            #region Properties

            /// <summary>
            /// Optional.
            /// JPEG format image data.
            /// </summary>
            public byte[] JpegImage
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            /// <summary>
            /// Optional.
            /// PNG format image data.
            /// </summary>
            public byte[] PngImage
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            /// <summary>
            /// Optional.
            /// PNG format image mask.
            /// </summary>
            public byte[] ImageMask
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

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
            public V2i ImageSize => new V2i(ImageWidth, ImageHeight);

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
            public V2i PixelSize => new V2i(PixelWidth, PixelHeight);

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
            public V2d PrincipalPoint => new V2d(PrincipalPointX, PrincipalPointY);

            #endregion

            internal static E57PinholeRepresentation Parse(XElement root)
            {
                if (root == null) return null;
                return new E57PinholeRepresentation
                {
                    ImageWidth = GetInteger(root, "imageWidth", true).Value,
                    ImageHeight = GetInteger(root, "imageHeight", true).Value,
                    FocalLength = GetFloat(root, "focalLength", true).Value,
                    PixelWidth = GetFloat(root, "pixelWidth", true).Value,
                    PixelHeight = GetFloat(root, "pixelHeight", true).Value,
                    PrincipalPointX = GetFloat(root, "principalPointX", true).Value,
                    PrincipalPointY = GetFloat(root, "principalPointY", true).Value,
                };
            }
        }

        /// <summary>
        /// TABLE 24 Child Elements for the SphericalRepresentation Structure. 
        /// </summary>
        public class E57SphericalRepresentation : IE57Element
        {
            /// <summary></summary>
            public E57ElementType E57Type => E57ElementType.SphericalRepresentation;

            #region Properties

            /// <summary>
            /// Optional.
            /// JPEG format image data.
            /// </summary>
            public byte[] JpegImage
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            /// <summary>
            /// Optional.
            /// PNG format image data.
            /// </summary>
            public byte[] PngImage
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            /// <summary>
            /// Optional.
            /// PNG format image mask.
            /// </summary>
            public byte[] ImageMask
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

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
            public V2i ImageSize => new V2i(ImageWidth, ImageHeight);
            
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
            public V2i PixelSize => new V2i(PixelWidth, PixelHeight);

            #endregion

            internal static E57SphericalRepresentation Parse(XElement root)
            {
                if (root == null) return null;
                return new E57SphericalRepresentation
                {
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
            /// <summary></summary>
            public E57ElementType E57Type => E57ElementType.CylindricalRepresentation;

            #region Properties

            /// <summary>
            /// Optional.
            /// JPEG format image data.
            /// </summary>
            public byte[] JpegImage
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            /// <summary>
            /// Optional.
            /// PNG format image data.
            /// </summary>
            public byte[] PngImage
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            /// <summary>
            /// Optional.
            /// PNG format image mask.
            /// </summary>
            public byte[] ImageMask
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

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
            public V2i ImageSize => new V2i(ImageWidth, ImageHeight);

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
            public V2i PixelSize => new V2i(PixelWidth, PixelHeight);

            #endregion

            internal static E57CylindricalRepresentation Parse(XElement root)
            {
                if (root == null) return null;
                return new E57CylindricalRepresentation
                {
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
            /// <summary></summary>
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
            /// <summary></summary>
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
            /// <summary></summary>
            public E57ElementType E57Type => E57ElementType.IndexBounds;

            #region Properties

            /// <summary>
            /// Required.
            /// The rowIndex range of any point represented by this IndexBounds object.
            /// </summary>
            public Range1i Row;

            /// <summary>
            /// Required.
            /// The columnIndex range of any point represented by this IndexBounds object.
            /// </summary>
            public Range1i Column;

            /// <summary>
            /// Required.
            /// The returnIndex range of any point represented by this IndexBounds object.
            /// </summary>
            public Range1i Return;

            #endregion

            internal static E57IndexBounds Parse(XElement root) => (root == null) ? null : new E57IndexBounds
            {
                Row = GetIntegerRange(root, "rowMinimum", "rowMaximum"),
                Column = GetIntegerRange(root, "columnMinimum", "columnMaximum"),
                Return = GetIntegerRange(root, "returnMinimum", "returnMaximum"),
            };
        }

        /// <summary>
        /// TABLE 29 Child Elements for IntensityLimits Structure. 
        /// </summary>
        public class E57IntensityLimits : IE57Element
        {
            /// <summary></summary>
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
                    Intensity = GetFloatRange(root, "intensityMinimum", "intensityMaximum")
                };
            }
        }

        /// <summary>
        /// TABLE 30 Child Elements for the ColorLimits Structure. 
        /// </summary>
        public class E57ColorLimits : IE57Element
        {
            /// <summary></summary>
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
            /// <summary></summary>
            public E57ElementType E57Type => E57ElementType.E57DateTime;

            #region Properties

            /// <summary>
            /// Required.
            /// The time, in seconds, since GPS start epoch. This time specification may include fractions of a second
            /// </summary>
            public double DateTimeValue;

            /// <summary>
            /// Required.
            /// This element shall be present, and its value set to 1 if, and only if, the time stored in the dateTimeValue element is obtained from an atomic clock time source.
            /// Shall be either 0 or 1.
            /// </summary>
            public bool IsAtomicClockReferenced;

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
            internal byte SectionId;
            internal byte[] Reserved;
            internal ulong SectionLength;
            internal ulong DataStartOffset;
            internal ulong IndexStartOffset;

            internal static E57CompressedVectorHeader Parse(byte[] buffer) => new E57CompressedVectorHeader
            {
                SectionId = Check("SectionId", buffer[0], x => x == 1, "1"),
                Reserved = Check("Reserved", buffer.Copy(1, 7), xs => xs.All(x => x == 0), "(0,0,0,0,0,0,0)"),
                SectionLength = BitConverter.ToUInt64(buffer, 8),
                DataStartOffset = BitConverter.ToUInt64(buffer, 16),
                IndexStartOffset = BitConverter.ToUInt64(buffer, 24),
            };
        }

        /// <summary>
        /// E57 Index Packet Header (9.4. Index Packe).
        /// TABLE 36 Format of an Index Packet Header.
        /// </summary>
        public struct E57IndexPacketHeader
        {
            internal byte PacketType;
            internal byte Reserved1;
            internal ushort PacketLengthMinus1;
            internal ushort EntryCount;
            internal byte IndexLevel;
            internal byte[] Reserved2;

            internal static E57IndexPacketHeader Parse(byte[] buffer) => new E57IndexPacketHeader
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
            internal ulong ChunkRecordIndex;
            internal ulong PacketOffset;

            internal static E57IndexPacketAddressEntry Parse(byte[] buffer) => new E57IndexPacketAddressEntry
            {
                ChunkRecordIndex = BitConverter.ToUInt64(buffer, 0),
                PacketOffset = BitConverter.ToUInt64(buffer, 8)
            };
        }

        /// <summary>
        /// E57 Data Packet Header (9.5. Data Packet).
        /// TABLE 38 Format of a Data Packet Header.
        /// </summary>
        public struct E57DataPacketHeader
        {
            internal byte PacketType;
            internal byte PacketFlags;
            internal ushort PacketLengthMinus1;
            internal ushort ByteStreamCount;

            internal static E57DataPacketHeader Parse(byte[] buffer) => new E57DataPacketHeader
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
                var crcComputed = Crc32C.Crc32CAlgorithm.Compute(buffer, 0, 1020);
                if (crcStored != crcComputed) throw new Exception(
                    $"[E57] Bad file. Checksum at file offset 0x{stream.Position - 4:X} should be 0x{crcComputed:X} (instead of 0x{crcStored:X})."
                    );
            }
        }

        /// <summary>
        /// Read given number of logical bytes (excluding 32-bit CRC at the end of each 1024 byte page) from stream,
        /// starting at raw stream position 'startPhysical'.
        /// </summary>
        public static byte[] ReadLogicalBytes(Stream stream, long startPhysical, int countLogical)
        {
            checked
            {
                if (startPhysical < 0) throw new ArgumentException(nameof(startPhysical));
                if (countLogical < 0) throw new ArgumentException(nameof(countLogical));

                stream.Position = startPhysical;

                var buffer = new byte[countLogical];
                var i = 0;
                while (countLogical > 0)
                {
                    var bytesLeftInPage = 1020 - (int)(stream.Position % 1024);
                    if (bytesLeftInPage > countLogical) bytesLeftInPage = countLogical;
                    if (stream.Read(buffer, i, bytesLeftInPage) != bytesLeftInPage) throw new InvalidOperationException();
                    stream.Position += 4; // skip CRC
                    i += bytesLeftInPage;
                    countLogical -= bytesLeftInPage;
                }

                return buffer;
            }
        }

        /// <summary>
        /// Read given number of logical unsigned shorts (excluding 32-bit CRC at the end of each 1024 byte page) from stream,
        /// starting at raw stream position 'startPhysical'.
        /// </summary>
        public static ushort[] ReadLogicalUnsignedShorts(Stream stream, long startPhysical, int count)
        {
            var buffer = ReadLogicalBytes(stream, startPhysical, count * 2);
            var xs = new ushort[count];
            for (var i = 0; i < count; i++) xs[i] = BitConverter.ToUInt16(buffer, i << 1);
            return xs;
        }

        #endregion

        #region XML Helpers

        private const string DEFAULT_NAMESPACE = "http://www.astm.org/COMMIT/E57/2010-e57-v1.0";
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
            => GetValue(root, elementName, required, "String", x => mustBe != null ? x == mustBe : true, x => x, mustBe, null);
        private static int? GetInteger(XElement root, string elementName, bool required, int? mustBe = null)
            => GetValue<int?>(root, elementName, required, "Integer", x => mustBe.HasValue ? x == mustBe.Value : true,
                x => x != null ? int.Parse(x) : 0, mustBe, null);
        private static long? GetLong(XElement root, string elementName, bool required, int? mustBe = null)
            => GetValue<long?>(root, elementName, required, "Integer", x => mustBe.HasValue ? x == mustBe.Value : true,
                x => x != null ? long.Parse(x) : 0, mustBe, null);
        private static bool? GetBool(XElement root, string elementName, bool required, int? mustBe = null)
            => GetInteger(root, elementName, required, mustBe) == 1;
        private static double? GetFloat(XElement root, string elementName, bool required, double? mustBe = null)
            => GetValue<double?>(root, elementName, required, "Float", x => mustBe.HasValue ? x == mustBe.Value : true,
                x => x != null ? double.Parse(x, CultureInfo.InvariantCulture) : 0.0, mustBe, null);
        private static double? GetFloatOrInteger(XElement root, string elementName, bool required)
        {
            var type = GetElementType(GetElement(root, elementName));
            switch (type)
            {
                case "Integer":
                    return GetInteger(root, elementName, required);
                case "Float":
                    return GetFloat(root, elementName, required);
                default:
                    throw new NotImplementedException();
            }
            
        }
        private static Range1i GetIntegerRange(XElement root, string elementNameMin, string elementNameMax)
            => new Range1i(GetInteger(root, elementNameMin, true).Value, GetInteger(root, elementNameMax, true).Value);
        private static Range1d GetFloatRange(XElement root, string elementNameMin, string elementNameMax)
            => new Range1d(GetFloat(root, elementNameMin, true).Value, GetFloat(root, elementNameMax, true).Value);
        private static Range1d GetRange(XElement root, string elementNameMin, string elementNameMax)
            => new Range1d(GetFloatOrInteger(root, elementNameMin, true).Value, GetFloatOrInteger(root, elementNameMax, true).Value);
        private static V3d GetTranslation(XElement root)
            => new V3d(GetFloat(root, "x", true).Value, GetFloat(root, "y", true).Value, GetFloat(root, "z", true).Value);
        private static Rot3d GetQuaternion(XElement root)
            => new Rot3d(GetFloat(root, "w", true).Value, GetFloat(root, "x", true).Value, GetFloat(root, "y", true).Value, GetFloat(root, "z", true).Value);
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

        private static long GetLongAttribute(XElement root, string elementName) => long.Parse(root.Attribute(elementName).Value);
        private static long? GetOptionalLongAttribute(XElement root, string elementName)
        {
            var a = root.Attribute(elementName);
            return (a != null) ? long.Parse(a.Value) : (long?)null;
        }
        private static double GetFloatAttribute(XElement root, string elementName) => double.Parse(root.Attribute(elementName).Value, CultureInfo.InvariantCulture);
        private static double? GetOptionalFloatAttribute(XElement root, string elementName)
        {
            var a = root.Attribute(elementName);
            return (a != null) ? double.Parse(a.Value, CultureInfo.InvariantCulture) : (double?)null;
        }

        private static IE57Element ParseE57Element(XElement root, Stream stream)
        {
            switch (GetElementType(root))
            {
                case "Integer": return E57Integer.Parse(root);
                case "ScaledInteger": return E57ScaledInteger.Parse(root);
                case "Float": return E57Float.Parse(root);
                case "String": return E57String.Parse(root);
                case "Blob": return E57Blob.Parse(root);
                case "Structure": return E57Structure.Parse(root, stream);
                case "Vector": return E57Vector.Parse(root, stream);
                case "CompressedVector": return E57CompressedVector.Parse(root, stream);
                case "Codec": return E57Codec.Parse(root, stream);
                case "E57Root": return E57Root.Parse(root, stream);
                case "Data3D": return E57Data3D.Parse(root, stream);
                case "PointRecord": return E57PointRecord.Parse(root);
                case "PointGroupingSchemes": return E57PointGroupingSchemes.Parse(root);
                case "GroupingByLine": return GroupingByLine.Parse(root);
                case "LineGroupRecord": return E57LineGroupRecord.Parse(root);
                case "RigidBodyTransform": return E57RigidBodyTransform.Parse(root);
                //case "Quaternion":
                //case "Translation":
                case "Image2d": return E57Image2D.Parse(root, stream);
                case "VisualReferenceRepresentation": return E57VisualReferenceRepresentation.Parse(root);
                case "PinholeRepresentation": return E57PinholeRepresentation.Parse(root);
                case "SphericalRepresentation": return E57SphericalRepresentation.Parse(root);
                case "CylindricalRepresentation": return E57CylindricalRepresentation.Parse(root);
                case "CartesianBounds": return E57CartesianBounds.Parse(root);
                case "SphericalBounds": return E57SphericalBounds.Parse(root);
                case "IndexBounds": return E57IndexBounds.Parse(root);
                case "IntensityLimits": return E57IntensityLimits.Parse(root);
                case "ColorLimits": return E57ColorLimits.Parse(root);
                case "E57DateTime": return E57DateTime.Parse(root);
                default:
                    throw new NotImplementedException($"[E57] Unknown E57 type <{GetElementType(root)}>");
            }
        }

        #endregion
    }
}
