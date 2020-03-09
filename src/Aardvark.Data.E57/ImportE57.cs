/*
    Copyright (C) 2006-2019. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using static System.Console;

namespace Aardvark.Data.Points.Import
{
    /// <summary>
    /// Importer for E57 format.
    /// </summary>
    [PointCloudFileFormatAttribute]
    public static class E57
    {
        /// <summary>
        /// E57 file format.
        /// </summary>
        public static readonly PointCloudFileFormat E57Format;

        static E57()
        {
            E57Format = new PointCloudFileFormat("e57", new[] { ".e57" }, E57Info, Chunks);
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
        /// Parses .e57 stream.
        /// </summary>
        public static IEnumerable<Chunk> Chunks(this Stream stream, long streamLengthInBytes, ParseConfig config)
        {
            checked
            {
                // file integrity check
                ASTM_E57.VerifyChecksums(stream, streamLengthInBytes);

                var header = ASTM_E57.E57FileHeader.Parse(stream, streamLengthInBytes);
                if (config.Verbose)
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

                if (header.E57Root.Data3D.Length == 0) yield break;

                var totalRecordCount = header.E57Root.Data3D.Sum(x => x.Points.RecordCount);
                var yieldedRecordCount = 0L;

                var head = header.E57Root.Data3D[0];
                var hasPositions = head.HasCartesianCoordinates || head.HasSphericalCoordinates;
                var hasColors = head.HasColors;
                var hasIntensities = head.HasIntensities;

                var ps = hasPositions ? new List<V3d>() : null;
                var cs = hasColors ? new List<C4b>() : null;
                var js = hasIntensities ? new List<double>() : null;

                Chunk PrepareChunk()
                {
                    var chunk = new Chunk(
                        positions   : ps, 
                        colors      : hasColors ? cs : ps.Map(_ => C4b.White),
                        normals     : null, 
                        intensities : js
                        );
                    Interlocked.Add(ref yieldedRecordCount, ps.Count);

                    if (hasPositions) ps = new List<V3d>(); 
                    if (hasColors) cs = new List<C4b>();
                    if (hasIntensities) js = new List<double>();

                    if (config.Verbose)
                    {
                        var progress = yieldedRecordCount / (double)totalRecordCount;
                        Write($"\r[E57] yielded {yieldedRecordCount,13:N0}/{totalRecordCount:N0} points [{progress * 100,6:0.00}%]");
                    }

                    return chunk;
                }

                foreach (var data3d in header.E57Root.Data3D)
                {
                    foreach (var (pos, color, intensity) in data3d.StreamPoints())
                    {
                        if (hasPositions) ps.Add(pos);
                        if (hasColors) cs.Add(color);
                        if (hasIntensities) js.Add(intensity);
                        if (ps.Count == config.MaxChunkPointCount) yield return PrepareChunk();
                    }
                    if (ps.Count > 0) yield return PrepareChunk();
                }

                if (config.Verbose) Report.Line();
            }
        }

        /// <summary>
        /// Gets general info for .e57 file.
        /// </summary>
        public static PointFileInfo<ASTM_E57.E57FileHeader> E57Info(string filename, ParseConfig config)
        {
            var filesize = new FileInfo(filename).Length;
            var stream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
            var header = ASTM_E57.E57FileHeader.Parse(stream, filesize);
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
            return new PointFileInfo<ASTM_E57.E57FileHeader>(filename, E57Format, filesize, pointCount, pointBounds, header);
        }
    }
}
