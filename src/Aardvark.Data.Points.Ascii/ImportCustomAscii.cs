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
using Aardvark.Base;
using System;
using System.Collections.Generic;
using System.IO;

namespace Aardvark.Data.Points.Import
{
    /// <summary></summary>
    public enum Token
    {
        /// <summary></summary>
        RedByte,
        /// <summary></summary>
        GreenByte,
        /// <summary></summary>
        BlueByte,
        /// <summary></summary>
        RedFloat,
        /// <summary></summary>
        GreenFloat,
        /// <summary></summary>
        BlueFloat,
        /// <summary></summary>
        Intensity,
        /// <summary></summary>
        PosX,
        /// <summary></summary>
        PosY,
        /// <summary></summary>
        PosZ,
        /// <summary></summary>
        NormalX,
        /// <summary></summary>
        NormalY,
        /// <summary></summary>
        NormalZ,
    }

    /// <summary>
    /// Importer for custom ASCII format.
    /// </summary>
    public static class CustomAscii
    {
        /// <summary>
        /// </summary>
        public static PointCloudFileFormat CreateFormat(string description, Func<byte[], int, double, Chunk?> lineParser)
            => new PointCloudFileFormat(description, new[] { ".pts" },
                (filename, config) => CustomAsciiInfo(filename, lineParser, config),
                (filename, config) => Chunks(filename, lineParser, config)
                );

        /// <summary>
        /// Parses .pts file.
        /// </summary>
        public static IEnumerable<Chunk> Chunks(string filename,
            Func<byte[], int, double, Chunk?> lineParser, ImportConfig config)
            => Parsing.AsciiLines(lineParser, filename, config);

        /// <summary>
        /// Parses .pts stream.
        /// </summary>
        public static IEnumerable<Chunk> Chunks(this Stream stream, long streamLengthInBytes,
            Func<byte[], int, double, Chunk?> lineParser, ImportConfig config)
            => Parsing.AsciiLines(HighPerformanceParsing.ParseLinesXYZIRGB, stream, streamLengthInBytes, config);

        /// <summary>
        /// Gets general info for custom ASCII file.
        /// </summary>
        public static PointFileInfo CustomAsciiInfo(string filename, Func<byte[], int, double, Chunk?> lineParse, ImportConfig config)
        {
            var filesize = new FileInfo(filename).Length;
            var pointCount = 0L;
            var pointBounds = Box3d.Invalid;
            foreach (var chunk in Chunks(filename, lineParse, ImportConfig.Default))
            {
                pointCount += chunk.Count;
                pointBounds.ExtendBy(chunk.BoundingBox);
            }
            var format = CreateFormat("Custom ASCII", lineParse);
            return new PointFileInfo(filename, format, filesize, pointCount, pointBounds);
        }
    }
}
