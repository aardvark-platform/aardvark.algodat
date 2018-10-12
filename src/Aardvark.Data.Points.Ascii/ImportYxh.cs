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
using System.Collections.Generic;
using System.IO;

namespace Aardvark.Data.Points.Import
{
    /// <summary>
    /// Importer for YXH format.
    /// </summary>
    [PointCloudFileFormatAttribute]
    public static class Yxh
    {
        /// <summary>
        /// E57 file format.
        /// </summary>
        public static readonly PointCloudFileFormat YxhFormat;

        static Yxh()
        {
            YxhFormat = new PointCloudFileFormat("yxh", new[] { ".yxh" }, YxhInfo, Chunks);
            PointCloudFileFormat.Register(YxhFormat);
        }

        /// <summary>
        /// Parses .yxh file.
        /// </summary>
        public static IEnumerable<Chunk> Chunks(string filename, ImportConfig config)
            => Parsing.AsciiLines(LineParsers.XYZIRGB, filename, config);

        /// <summary>
        /// Parses .yxh stream.
        /// </summary>
        public static IEnumerable<Chunk> Chunks(this Stream stream, long streamLengthInBytes, ImportConfig config)
            => Parsing.AsciiLines(LineParsers.XYZIRGB, stream, streamLengthInBytes, config);

        /// <summary>
        /// Gets general info for .pts file.
        /// </summary>
        public static PointFileInfo YxhInfo(string filename, ImportConfig config)
        {
            var filesize = new FileInfo(filename).Length;
            var pointCount = 0L;
            var pointBounds = Box3d.Invalid;
            foreach (var chunk in Chunks(filename, ImportConfig.Default))
            {
                pointCount += chunk.Count;
                pointBounds.ExtendBy(chunk.BoundingBox);
            }
            return new PointFileInfo(filename, YxhFormat, filesize, pointCount, pointBounds);
        }
    }
}
