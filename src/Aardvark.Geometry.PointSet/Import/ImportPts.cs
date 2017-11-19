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
using System.Collections.Generic;
using System.IO;
using Aardvark.Base;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// Importers for various formats.
    /// </summary>
    public static partial class PointCloud
    {
        /// <summary>
        /// Gets general info for .pts file.
        /// </summary>
        public static PointFileInfo PtsInfo(string filename, ImportConfig config)
        {
            var filesize = new FileInfo(filename).Length;
            var pointCount = 0L;
            var pointBounds = Box3d.Invalid;
            foreach (var chunk in Pts(filename, new ImportConfig()))
            {
                pointCount += chunk.Count;
                pointBounds.ExtendBy(chunk.BoundingBox);
            }
            return new PointFileInfo(filename, PointCloudFormat.E57Format, filesize, pointCount, pointBounds);
        }

        /// <summary>
        /// Parses .pts file.
        /// </summary>
        public static IEnumerable<Chunk> Pts(string filename, ImportConfig config)
            => AsciiLines(HighPerformanceParsing.ParseLinesXYZIRGB, filename, config);

        /// <summary>
        /// Parses .pts stream.
        /// </summary>
        public static IEnumerable<Chunk> Pts(this Stream stream, long streamLengthInBytes, ImportConfig config)
            => AsciiLines(HighPerformanceParsing.ParseLinesXYZIRGB, stream, streamLengthInBytes, config);
    }
}
