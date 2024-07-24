/*
    Copyright (C) 2006-2023. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using Aardvark.Data.E57;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using static Aardvark.Data.E57.ASTM_E57;

#pragma warning disable IDE0130 // Namespace does not match folder structure

namespace Aardvark.Data.Points.Import
{
    /// <summary>
    /// Importer for E57 format.
    /// </summary>
    [PointCloudFileFormatAttribute]
    public static partial class E57
    {
        /// <summary>
        /// E57 file format.
        /// </summary>
        public static readonly PointCloudFileFormat E57Format;

        static E57()
        {
            E57Format = new PointCloudFileFormat("e57", [".e57"], E57Info, Chunks);
            PointCloudFileFormat.Register(E57Format);
        }
        
        /// <summary>
        /// Parses .e57 file.
        /// </summary>
        public static IEnumerable<Chunk> Chunks(string filename, ParseConfig config)
        {
            var fileSizeInBytes = new FileInfo(filename).Length;
            var stream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Chunks(stream, fileSizeInBytes, config);
        }

        /// <summary>
        /// Parses .e57 file (including all properties).
        /// </summary>
        public static IEnumerable<E57Chunk> ChunksFull(string filename, ParseConfig config)
        {
            var fileSizeInBytes = new FileInfo(filename).Length;
            var stream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
            return ChunksFull(stream, fileSizeInBytes, config);
        }

        private static void PrintHeader(E57FileHeader header)
        {
            Report.Line($"[E57] ========================================================================");
            Report.Line($"[E57] Header (binary)");
            Report.Line($"[E57] ------------------------------------------------------------------------");
            Report.Line($"[E57]   fileSignature ........ {header.FileSignature}");
            Report.Line($"[E57]   versionMajor ......... {header.VersionMajor}");
            Report.Line($"[E57]   versionMinor ......... {header.VersionMinor}");
            Report.Line($"[E57]   headerFileLength ..... {header.FileLength:N0}");
            Report.Line($"[E57]   headerXmlOffset ...... {header.XmlOffset:N0}");
            Report.Line($"[E57]   headerXmlLength ...... {header.XmlLength:N0}");
            Report.Line($"[E57]   headerPageSize ....... {header.PageSize:N0}");
            Report.Line($"[E57] ========================================================================");
            Report.Line($"[E57] E57 Root (XML)");
            Report.Line($"[E57] ------------------------------------------------------------------------");
            Report.Line($"[E57]   formatName ........... {header.E57Root.FormatName}");
            Report.Line($"[E57]   guid ................. {header.E57Root.Guid}");
            Report.Line($"[E57]   versionMajor ......... {header.E57Root.VersionMajor}");
            Report.Line($"[E57]   cersionMajor ......... {header.E57Root.VersionMinor}");
            Report.Line($"[E57]   e57LibraryVersion .... {header.E57Root.E57LibraryVersion}");
            Report.Line($"[E57]   creationDateTime");
            Report.Line($"[E57]     .dateTimeValue ............. {header.E57Root.CreationDateTime?.DateTimeValue}");
            Report.Line($"[E57]     .isAtomicClockReferenced ... {header.E57Root.CreationDateTime?.IsAtomicClockReferenced}");
            Report.Line($"[E57]   coordinateMetadata ... {header.E57Root.CoordinateMetadata}");
            Report.Line($"[E57] ------------------------------------------------------------------------");
            Report.Line($"[E57] Data3D");
            Report.Line($"[E57] ========================================================================");
            for (var i = 0; i < header.E57Root.Data3D.Length; i++)
            {
                var data3d = header.E57Root.Data3D[i];
                Report.Line($"[E57] [{i}]");
                Report.Line($"[E57]   guid .................. {data3d.Guid}");
                Report.Line($"[E57]   points ................ {data3d.Points.RecordCount}");
                if (data3d.Pose != null)
                    Report.Line($"[E57]   pose .................. R = {data3d.Pose.Rotation}, t = {data3d.Pose.Translation}");
                if (data3d.OriginalGuids != null)
                    Report.Line($"[E57]   original guids ........ {data3d.OriginalGuids.Children}");
                if (data3d.PointGroupingSchemes != null)
                {
                    var x = data3d.PointGroupingSchemes.GroupingByLine;
                    Report.Line($"[E57]   point grouping ........ GroupingByLine");
                    Report.Line($"[E57]      idElementName ...... {x.IdElementName}");
                    if (x.Groups != null)
                        Report.Line($"[E57]      idElementName ...... {x.Groups}");
                }
                if (data3d.Name != null)
                    Report.Line($"[E57]   name .................. {data3d.Name}");
                if (data3d.Description != null)
                    Report.Line($"[E57]   description ........... {data3d.Description}");
                if (data3d.CartesianBounds != null)
                    Report.Line($"[E57]   cartesian bounds ...... {data3d.CartesianBounds.Bounds:0.000}");
                if (data3d.SphericalBounds != null)
                    Report.Line($"[E57]   spherical bounds ...... {data3d.SphericalBounds:0.000}");
                if (data3d.IndexBounds != null)
                {
                    var x = data3d.IndexBounds;
                    Report.Line($"[E57]   index bounds .......... row {x.Row}, col {x.Column}, return {x.Return}");
                }
                if (data3d.IntensityLimits != null)
                    Report.Line($"[E57]   intensity limits ...... {data3d.IntensityLimits.Intensity}");
                if (data3d.ColorLimits != null)
                {
                    var x = data3d.ColorLimits;
                    Report.Line($"[E57]   color limits .......... R{x.Red}, G{x.Green}, B{x.Blue}");
                }
                if (data3d.AcquisitonStart != null)
                    Report.Line($"[E57]   acquisition start ..... {data3d.AcquisitonStart}");
                if (data3d.AcquisitonEnd != null)
                    Report.Line($"[E57]   acquisition end ....... {data3d.AcquisitonEnd}");
                if (data3d.SensorVendor != null)
                    Report.Line($"[E57]   sensor vendor ......... {data3d.SensorVendor}");
                if (data3d.SensorModel != null)
                    Report.Line($"[E57]   sensor model .......... {data3d.SensorModel}");
                if (data3d.SensorSerialNumber != null)
                    Report.Line($"[E57]   sensor serial number .. {data3d.SensorSerialNumber}");
                if (data3d.SensorHardwareVersion != null)
                    Report.Line($"[E57]   sensor hardware ....... {data3d.SensorHardwareVersion}");
                if (data3d.SensorSoftwareVersion != null)
                    Report.Line($"[E57]   sensor software ....... {data3d.SensorSoftwareVersion}");
                if (data3d.SensorFirmwareVersion != null)
                    Report.Line($"[E57]   sensor firmware ....... {data3d.SensorFirmwareVersion}");
                if (data3d.Temperature != null)
                    Report.Line($"[E57]   temperature ........... {data3d.Temperature}");
                if (data3d.RelativeHumidity != null)
                    Report.Line($"[E57]   relative humidity ..... {data3d.RelativeHumidity}");
                if (data3d.AtmosphericPressure != null)
                    Report.Line($"[E57]   atmospheric pressure .. {data3d.AtmosphericPressure}");
            }

            Report.Line(header.RawXml.ToString(SaveOptions.OmitDuplicateNamespaces));

            Report.Line();
            Report.Line();
        }

        /// <summary>
        /// Parses .e57 stream.
        /// </summary>
        public static IEnumerable<Chunk> Chunks(this Stream stream, long streamLengthInBytes, ParseConfig config)
            => Chunks(stream, streamLengthInBytes, config, verifyChecksums: false);

        /// <summary>
        /// Parses .e57 stream.
        /// </summary>
        public static IEnumerable<Chunk> Chunks(this Stream stream, long streamLengthInBytes, ParseConfig config, bool verifyChecksums)
        {
            var exclude = ImmutableHashSet<PointPropertySemantics>.Empty
                //.Add(PointPropertySemantics.CartesianInvalidState)
                .Add(PointPropertySemantics.ColumnIndex)
                .Add(PointPropertySemantics.IsColorInvalid)
                .Add(PointPropertySemantics.IsIntensityInvalid)
                .Add(PointPropertySemantics.IsTimeStampInvalid)
                .Add(PointPropertySemantics.ReturnCount)
                .Add(PointPropertySemantics.ReturnIndex)
                .Add(PointPropertySemantics.RowIndex)
                .Add(PointPropertySemantics.SphericalInvalidState)
                .Add(PointPropertySemantics.TimeStamp)
                ;

            checked
            {
                // file integrity check
                if (verifyChecksums) VerifyChecksums(stream, streamLengthInBytes);

                var header = E57FileHeader.Parse(stream, streamLengthInBytes, config.Verbose);
                if (config.Verbose) PrintHeader(header);

                if (header.E57Root.Data3D.Length == 0) yield break;

                var totalRecordCount = header.E57Root.Data3D.Sum(x => x.Points.RecordCount);
                var yieldedRecordCount = 0L;

                var partIndex = config.PartIndexOffset;

                // collect all semantics (over all data3d objects)
                // -> we do this, so we can fill "missing" properties with default values
                // -> this is a workaround for e57 files that contain data3d objects with differing semantics (which can't be merged later on) 
                var semanticsAll = new HashSet<PointPropertySemantics>();
                foreach (var data3d in header.E57Root.Data3D)
                {
                    semanticsAll.AddRange(data3d.Sem2Index.Keys);
                }

                // print overview
                foreach (var data3d in header.E57Root.Data3D)
                {
                    Console.WriteLine($"[Data3D] {data3d.Name} ({data3d.Points.ByteStreamsCount} byte streams)");
                    foreach (var k in semanticsAll)
                    {
                        if (!data3d.Sem2Index.ContainsKey(k))
                        {
                            Console.WriteLine($"[Data3D]   {k} missing");
                        }
                    }
                }

                foreach (var data3d in header.E57Root.Data3D)
                {
                    foreach (var (Positions, Properties) in data3d.StreamPointsFull(config.MaxChunkPointCount, config.Verbose, exclude))
                    {
                        var e57chunk = new E57Chunk(Properties, data3d, Positions);

                        // ensure that there are colors (if e57 chunk has no colors then add all black colors
                        var cs = e57chunk.Colors?.Map(c => new C4b(c)) ?? Positions.Map(_ => C4b.Black);

                        // ensure that there are normals, if any e57 chunk has normals (according to 'semanticsAll') 
                        var ns = e57chunk.Normals;
                        if (ns == null && semanticsAll.Contains(PointPropertySemantics.NormalX))
                        {
                            ns = Positions.Map(_ => V3f.Zero); // set all normals to (0,0,0)
                        }

                        // ensure that there are intensities, if any e57 chunk has intensities (according to 'semanticsAll') 
                        var js = e57chunk.Intensities;
                        if (js == null && semanticsAll.Contains(PointPropertySemantics.Intensity))
                        {
                            js = new int[Positions.Length]; // set all intensities to 0
                        }

                        // ensure that there are classifications, if any e57 chunk has classifications (according to 'semanticsAll') 
                        var ks = e57chunk.Classification?.Map(x => (byte)x);
                        if (ks == null && semanticsAll.Contains(PointPropertySemantics.Intensity))
                        {
                            ks = new byte[Positions.Length]; // set all classifications to 0
                        }

                        var chunk = new Chunk(
                            positions: Positions,
                            colors: cs,
                            normals: ns,
                            intensities: js,
                            classifications: ks,
                            partIndices: config.EnabledProperties.PartIndices ? partIndex : null,
                            partIndexRange: null,
                            bbox: null
                            );

                        if (Properties.ContainsKey(PointPropertySemantics.CartesianInvalidState))
                        {
                            var cis = (byte[])Properties[PointPropertySemantics.CartesianInvalidState];
                            chunk = chunk.ImmutableFilter((c, i) => cis[i] == 0);
                        }

                        if (chunk.HasNormals && data3d.Pose != null)
                        {
                            chunk = chunk.WithNormalsTransformed(data3d.Pose.Rotation);
                        }

                        Interlocked.Add(ref yieldedRecordCount, Positions.Length);

                        if (config.Verbose)
                        {
                            var progress = yieldedRecordCount / (double)totalRecordCount;
                            Report.Line($"\r[E57] yielded {yieldedRecordCount,13:N0}/{totalRecordCount:N0} points [{progress * 100,6:0.00}%]");
                        }

                        yield return chunk;
                    }

                    partIndex++;
                }

                if (config.Verbose) Report.Line();
            }
        }

        /// <summary>
        /// Parses .e57 stream.
        /// </summary>
        public static IEnumerable<E57Chunk> ChunksFull(this Stream stream, long streamLengthInBytes, ParseConfig config)
            => ChunksFull(stream, streamLengthInBytes, config, verifyChecksums: false);

        /// <summary>
        /// Parses .e57 stream.
        /// </summary>
        public static IEnumerable<E57Chunk> ChunksFull(this Stream stream, long streamLengthInBytes, ParseConfig config, bool verifyChecksums)
        {
            checked
            {
                // file integrity check
                if (verifyChecksums) VerifyChecksums(stream, streamLengthInBytes);

                var header = E57FileHeader.Parse(stream, streamLengthInBytes, config.Verbose);
                if (config.Verbose) PrintHeader(header);

                if (header.E57Root.Data3D.Length == 0) yield break;

                var totalRecordCount = header.E57Root.Data3D.Sum(x => x.Points.RecordCount);
                var yieldedRecordCount = 0L;

                var head = header.E57Root.Data3D[0];

                foreach (var data3d in header.E57Root.Data3D)
                {
                    foreach (var (Positions, Properties) in data3d.StreamPointsFull(config.MaxChunkPointCount, config.Verbose, ImmutableHashSet<PointPropertySemantics>.Empty))
                    {
                        for (var i = 0; i < Positions.Length; i++) if (Positions[i].Length > 1000000000) Debugger.Break();

                        var chunk = new E57Chunk(Properties, data3d, Positions);

                        if (chunk.HasNormals && data3d.Pose != null)
                        {
                            chunk.NormalsTransformInPlace(data3d.Pose.Rotation);
                        }

                        Interlocked.Add(ref yieldedRecordCount, Positions.Length);

                        if (config.Verbose)
                        {
                            var progress = yieldedRecordCount / (double)totalRecordCount;
                            Report.Line($"\r[E57] yielded {yieldedRecordCount,13:N0}/{totalRecordCount:N0} points [{progress * 100,6:0.00}%]");
                        }

                        yield return chunk;
                    }
                }

                if (config.Verbose) Report.Line();
            }
        }

        /// <summary>
        /// Gets general info for .e57 file.
        /// </summary>
        public static PointFileInfo<E57FileHeader> E57Info(string filename, ParseConfig config)
        {
            var filesize = new FileInfo(filename).Length;
            E57FileHeader header;
            using (var stream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                header = E57FileHeader.Parse(stream, filesize, config.Verbose);
            };
            var pointCount = 0L;
            var pointBounds = Box3d.Invalid;
            foreach (var data3d in header.E57Root.Data3D)
            {
                pointCount += data3d.Points.RecordCount;
                if (data3d.CartesianBounds != null)
                {
                    pointBounds.ExtendBy(data3d.CartesianBounds.Bounds);
                }
            }
            return new PointFileInfo<E57FileHeader>(filename, E57Format, filesize, pointCount, pointBounds, header);
        }

        /// <summary>
        /// </summary>
        public enum CartesianInvalidState : byte
        {
            /// <summary>
            /// The cartesian coordinates are meaningful.
            /// </summary>
            Valid = 0,

            /// <summary>
            /// Only the direction component of the vector is meaningful.
            /// </summary>
            OnlyDirectionIsValid = 1,

            /// <summary>
            /// The cartesian coordinates are not meaningful.
            /// </summary>
            Invalid = 2,
        }

        /// <summary>
        /// </summary>
        public enum SphericalInvalidState : byte
        {
            /// <summary>
            /// </summary>
            Valid = 0,

            /// <summary>
            /// </summary>
            InvalidSphericalRange = 1,

            /// <summary>
            /// </summary>
            Invalid = 2,
        }

        /// <summary>
        /// </summary>
        public class E57Chunk
        {
            /// <summary></summary>
            public E57Chunk(ImmutableDictionary<PointPropertySemantics, Array> rawData, E57Data3D data3d, V3d[] positions)
            {
                if (rawData.Values.Any(x => x.Length != positions.Length))
                    throw new Exception("All properties must have same number of entries.");

                RawData = rawData;
                Positions = positions;
                Data3D = data3d;
            }

            /// <summary>
            /// Raw data as decoded from e57 file.
            /// </summary>
            public ImmutableDictionary<PointPropertySemantics, Array> RawData { get; }

            /// <summary>
            /// </summary>
            public E57Data3D Data3D { get; }

            /// <summary>
            /// Cartesian positions.
            /// </summary>
            public V3d[] Positions { get; }

            /// <summary>
            /// Number of points in this chunk.
            /// </summary>
            public int Count => Positions.Length;


            /// <summary>
            /// The row number of point (zero-based). This is useful for data that is stored in a regular grid. Optional.
            /// </summary>
            public uint[] RowIndex => GetOrNull<uint>(PointPropertySemantics.RowIndex);

            /// <summary>
            /// The column number of point (zero-based). This is useful for data that is stored in a regular grid. Optional.
            /// </summary>
            public uint[] ColumnIndex => GetOrNull<uint>(PointPropertySemantics.ColumnIndex);

            /// <summary>
            /// Only for multi-return sensors. The total number of returns for the pulse that this corresponds to. Optional.
            /// </summary>
            public uint[] ReturnCount => GetOrNull<uint>(PointPropertySemantics.ReturnCount);

            /// <summary>
            /// Only for multi-return sensors. The number of this return (zero based). 
            /// That is, 0 is the first return, 1 is the second, and so on. 
            /// Shall be in the interval [0, returnCount). Optional.
            /// </summary>
            public uint[] ReturnIndex => GetOrNull<uint>(PointPropertySemantics.ReturnIndex);

            /// <summary>
            /// Timestamps. Optional.
            /// </summary>
            public DateTimeOffset[] Timestamps
            {
                get
                {
                    var xs = GetOrNull<double>(PointPropertySemantics.TimeStamp);
                    if (xs == null) return null;

                    DateTimeOffset guessAcquisitionStart()
                    {
                        if (E57DateTime.GpsStartEpoch.AddSeconds(xs[0]) < DateTimeOffset.Now) return E57DateTime.GpsStartEpoch;
                        return E57DateTime.UnixStartEpoch;
                    }
                    var t0 = Data3D.AcquisitonStart?.DateTime ?? guessAcquisitionStart();

                    var ts = new DateTimeOffset[xs.Length];
                    for (var i = 0; i < xs.Length; i++) ts[i] = t0.AddSeconds(xs[i]);
                    return ts;
                }
            }

            /// <summary>
            /// Intensities. Optional.
            /// </summary>
            public int[] Intensities => GetOrNull<int>(PointPropertySemantics.Intensity);

            /// <summary>
            /// Colors. Optional.
            /// </summary>
            public C3b[] Colors
            {
                get
                {
                    C3b[] cs = null;
                    if (RawData.ContainsKey(PointPropertySemantics.ColorRed))
                    {
                        if (!RawData.ContainsKey(PointPropertySemantics.ColorGreen)) throw new Exception($"Missing green color channel. Error 4d802e06-bc48-4b18-81f5-4cae997081f3.");
                        if (!RawData.ContainsKey(PointPropertySemantics.ColorBlue)) throw new Exception($"Missing blue color channel. Error ea80cee4-be10-4ef4-ae0b-8a6cb3685b13.");

                        var crs = (byte[])RawData[PointPropertySemantics.ColorRed];
                        var cgs = (byte[])RawData[PointPropertySemantics.ColorGreen];
                        var cbs = (byte[])RawData[PointPropertySemantics.ColorBlue];

                        var imax = Count;
                        cs = new C3b[imax];
                        for (var i = 0; i < imax; i++) cs[i] = new(crs[i], cgs[i], cbs[i]);
                    }
                    return cs;
                }
            }

            /// <summary>
            /// [Spec] If cartesianInvalidState is defined, its value shall have the following interpretation.
            /// If the value is 0, the values of cartesianX, cartesianY, and cartesianZ shall all be 
            /// meaningful. If the value is 1, only the direction component of the vector (cartesianX,
            /// cartesianY, cartesianZ) shall be meaningful, and the magnitude of the vector shall be
            /// considered non-meaningful. If the value is 2, the values of cartesianX, cartesianY,
            /// and cartesianZ shall all be considered non-meaningful.
            /// </summary>
            public CartesianInvalidState[] CartesianInvalidState 
                => GetOrNull<byte>(PointPropertySemantics.CartesianInvalidState)?.Map(x => (CartesianInvalidState)x);

            /// <summary>
            /// If sphericalInvalidState is defined, its value shall have the following interpretation.
            /// If the value is 0, the values of sphericalRange, sphericalAzimuth, and sphericalElevation
            /// shall all be meaningful. If the value is 1, the value of sphericalRange shall be considered 
            /// non-meaningful, and the value of sphericalAzimuth, and sphericalElevation shall be meaningful.
            /// If the value is 2, the values of sphericalRange, sphericalAzimuth, and sphericalElevation 
            /// shall all be considered non-meaningful.
            /// </summary>
            public SphericalInvalidState[] SphericalInvalidState
                => GetOrNull<byte>(PointPropertySemantics.SphericalInvalidState)?.Map(x => (SphericalInvalidState)x);

            /// <summary>
            /// If isTimeStampInvalid is defined and its value is 1, the value of timeStamp shall be
            /// considered non-meaningful. Otherwise, the value of timeStamp shall be meaningful.
            /// </summary>
            public bool[] IsTimeStampInvalid
                => GetOrNull<byte>(PointPropertySemantics.IsTimeStampInvalid)?.Map(x => x == 1);

            /// <summary>
            /// If isIntensityInvalid is defined and its value is 1, the value of intensity shall be 
            /// considered non-meaningful. Otherwise, the value of intensity shall be meaningful.
            /// </summary>
            public bool[] IsIntensityInvalid
                => GetOrNull<byte>(PointPropertySemantics.IsIntensityInvalid)?.Map(x => x == 1);

            /// <summary>
            /// If isColorInvalid is defined and its value is 1, the values of colorRed, colorGreen,
            /// and colorBlue shall be considered non-meaningful. Otherwise, the values of colorRed,
            /// colorGreen, and colorBlue shall all be meaningful.
            /// </summary>
            public bool[] IsColorInvalid
                => GetOrNull<byte>(PointPropertySemantics.IsColorInvalid)?.Map(x => x == 1);

            /// <summary>
            /// </summary>
            public bool HasNormals => RawData.ContainsKey(PointPropertySemantics.NormalX);

            /// <summary>
            /// Normals. Optional.
            /// </summary>
            public V3f[] Normals
            {
                get
                {
                    V3f[] ns = null;
                    if (HasNormals)
                    {
                        if (!RawData.ContainsKey(PointPropertySemantics.NormalY)) throw new Exception($"Missing NormalY channel. Error 0e39ebe8-0cf9-44b9-9e22-6ccbab945b4e.");
                        if (!RawData.ContainsKey(PointPropertySemantics.NormalZ)) throw new Exception($"Missing NormalZ channel. Error 277e8cbc-9cbb-4142-8852-8bd45f851a94.");

                        var nxs = (float[])RawData[PointPropertySemantics.NormalX];
                        var nys = (float[])RawData[PointPropertySemantics.NormalY];
                        var nzs = (float[])RawData[PointPropertySemantics.NormalZ];

                        var imax = Count;
                        ns = new V3f[imax];
                        for (var i = 0; i < imax; i++) ns[i] = new(nxs[i], nys[i], nzs[i]);
                    }
                    return ns;
                }
            }

            /// <summary>
            /// </summary>
            public void NormalsTransformInPlace(Rot3d r)
            {
                if (!HasNormals) return;

                var nxs = (float[])RawData[PointPropertySemantics.NormalX];
                var nys = (float[])RawData[PointPropertySemantics.NormalY];
                var nzs = (float[])RawData[PointPropertySemantics.NormalZ];

                var imax = Count;
                for (var i = 0; i < imax; i++)
                {
                    var n = r.Transform(new V3d(nxs[i], nys[i], nzs[i]));
                    nxs[i] = (float)n.X;
                    nys[i] = (float)n.Y;
                    nzs[i] = (float)n.Z;
                }
            }

            /// <summary>
            /// Classification. Optional.
            /// </summary>
            public int[] Classification => GetOrNull<int>(PointPropertySemantics.Classification);

            private T[] GetOrNull<T>(PointPropertySemantics sem)
                => RawData.TryGetValue(sem, out var raw) ? (T[])raw : null;
        }
    }
}
