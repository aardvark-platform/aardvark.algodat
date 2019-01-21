/*
    Copyright (C) 2006-2018. Aardvark Platform Team. http://github.com/aardvark-platform.
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
                    WriteLine($"[E57] ========================================================================");
                    WriteLine($"[E57] Header (binary)");
                    WriteLine($"[E57] ------------------------------------------------------------------------");
                    WriteLine($"[E57]   fileSignature ........ {header.FileSignature}");
                    WriteLine($"[E57]   versionMajor ......... {header.VersionMajor}");
                    WriteLine($"[E57]   versionMinor ......... {header.VersionMinor}");
                    WriteLine($"[E57]   headerFileLength ..... {header.FileLength:N0}");
                    WriteLine($"[E57]   headerXmlOffset ...... {header.XmlOffset:N0}");
                    WriteLine($"[E57]   headerXmlLength ...... {header.XmlLength:N0}");
                    WriteLine($"[E57]   headerPageSize ....... {header.PageSize:N0}");
                    WriteLine($"[E57] ========================================================================");
                    WriteLine($"[E57] E57 Root (XML)");
                    WriteLine($"[E57] ------------------------------------------------------------------------");
                    WriteLine($"[E57]   formatName ........... {header.E57Root.FormatName}");
                    WriteLine($"[E57]   guid ................. {header.E57Root.Guid}");
                    WriteLine($"[E57]   versionMajor ......... {header.E57Root.VersionMajor}");
                    WriteLine($"[E57]   cersionMajor ......... {header.E57Root.VersionMinor}");
                    WriteLine($"[E57]   e57LibraryVersion .... {header.E57Root.E57LibraryVersion}");
                    WriteLine($"[E57]   creationDateTime");
                    WriteLine($"[E57]     .dateTimeValue ............. {header.E57Root.CreationDateTime?.DateTimeValue}");
                    WriteLine($"[E57]     .isAtomicClockReferenced ... {header.E57Root.CreationDateTime?.IsAtomicClockReferenced}");
                    WriteLine($"[E57]   coordinateMetadata ... {header.E57Root.CoordinateMetadata}");
                    WriteLine($"[E57] ------------------------------------------------------------------------");
                    WriteLine($"[E57] Data3D");
                    WriteLine($"[E57] ========================================================================");
                    for (var i = 0; i < header.E57Root.Data3D.Length; i++)
                    {
                        var data3d = header.E57Root.Data3D[i];
                        WriteLine($"[E57] [{i}]");
                        WriteLine($"[E57]   guid .................. {data3d.Guid}");
                        WriteLine($"[E57]   points ................ {data3d.Points.RecordCount}");
                        if (data3d.Pose != null)
                            WriteLine($"[E57]   pose .................. R = {data3d.Pose.Rotation}, t = {data3d.Pose.Translation}");
                        if (data3d.OriginalGuids != null)
                            WriteLine($"[E57]   original guids ........ {data3d.OriginalGuids.Children}");
                        if (data3d.PointGroupingSchemes != null)
                        {
                            var x = data3d.PointGroupingSchemes.GroupingByLine;
                            WriteLine($"[E57]   point grouping ........ GroupingByLine");
                            WriteLine($"[E57]      idElementName ...... {x.IdElementName}");
                            if (x.Groups != null)
                                WriteLine($"[E57]      idElementName ...... {x.Groups}");
                        }
                        if (data3d.Name != null)
                            WriteLine($"[E57]   name .................. {data3d.Name}");
                        if (data3d.Description != null)
                            WriteLine($"[E57]   description ........... {data3d.Description}");
                        if (data3d.CartesianBounds != null)
                            WriteLine($"[E57]   cartesian bounds ...... {data3d.CartesianBounds.Bounds:0.000}");
                        if (data3d.SphericalBounds != null)
                            WriteLine($"[E57]   spherical bounds ...... {data3d.SphericalBounds:0.000}");
                        if (data3d.IndexBounds != null)
                        {
                            var x = data3d.IndexBounds;
                            WriteLine($"[E57]   index bounds .......... row {x.Row}, col {x.Column}, return {x.Return}");
                        }
                        if (data3d.IntensityLimits != null)
                            WriteLine($"[E57]   intensity limits ...... {data3d.IntensityLimits.Intensity}");
                        if (data3d.ColorLimits != null)
                        {
                            var x = data3d.ColorLimits;
                            WriteLine($"[E57]   color limits .......... R{x.Red}, G{x.Green}, B{x.Blue}");
                        }
                        if (data3d.AcquisitonStart != null)
                            WriteLine($"[E57]   acquisition start ..... {data3d.AcquisitonStart}");
                        if (data3d.AcquisitonEnd != null)
                            WriteLine($"[E57]   acquisition end ....... {data3d.AcquisitonEnd}");
                        if (data3d.SensorVendor != null)
                            WriteLine($"[E57]   sensor vendor ......... {data3d.SensorVendor}");
                        if (data3d.SensorModel != null)
                            WriteLine($"[E57]   sensor model .......... {data3d.SensorModel}");
                        if (data3d.SensorSerialNumber != null)
                            WriteLine($"[E57]   sensor serial number .. {data3d.SensorSerialNumber}");
                        if (data3d.SensorHardwareVersion != null)
                            WriteLine($"[E57]   sensor hardware ....... {data3d.SensorHardwareVersion}");
                        if (data3d.SensorSoftwareVersion != null)
                            WriteLine($"[E57]   sensor software ....... {data3d.SensorSoftwareVersion}");
                        if (data3d.SensorFirmwareVersion != null)
                            WriteLine($"[E57]   sensor firmware ....... {data3d.SensorFirmwareVersion}");
                        if (data3d.Temperature != null)
                            WriteLine($"[E57]   temperature ........... {data3d.Temperature}");
                        if (data3d.RelativeHumidity != null)
                            WriteLine($"[E57]   relative humidity ..... {data3d.RelativeHumidity}");
                        if (data3d.AtmosphericPressure != null)
                            WriteLine($"[E57]   atmospheric pressure .. {data3d.AtmosphericPressure}");
                    }

                    WriteLine(header.RawXml.ToString(SaveOptions.OmitDuplicateNamespaces));

                    WriteLine();
                    WriteLine();
                }

                var totalRecordCount = header.E57Root.Data3D.Sum(x => x.Points.RecordCount);
                var yieldedRecordCount = 0L;
                var ps = new List<V3d>(); var cs = new List<C4b>();
                Chunk PrepareChunk()
                {
                    var chunk = new Chunk(ps, cs);
                    yieldedRecordCount += ps.Count;
                    ps = new List<V3d>(); cs = new List<C4b>();
                    if (config.Verbose)
                    {
                        var progress = yieldedRecordCount / (double)totalRecordCount;
                        Write($"\r[E57] yielded {yieldedRecordCount,13:N0}/{totalRecordCount:N0} points [{progress * 100,6:0.00}%]");
                    }
                    return chunk;
                }

                foreach (var data3d in header.E57Root.Data3D)
                {
                    foreach (var x in data3d.StreamPoints())
                    {
                        ps.Add(x.Item1); cs.Add(x.Item2);
                        if (ps.Count == config.MaxChunkPointCount) yield return PrepareChunk();
                    }
                    if (ps.Count > 0) yield return PrepareChunk();
                }

                if (config.Verbose) WriteLine();
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
