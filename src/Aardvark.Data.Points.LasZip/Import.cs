/*
    Copyright (C) 2017. Stefan Maierhofer.
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
using System.Linq;

namespace Aardvark.Data.Points.Import
{
    /// <summary>
    /// Importer for PTS format.
    /// </summary>
    [PointCloudFileFormatAttribute]
    public static class Laszip
    {
        /// <summary>
        /// Pts file format.
        /// </summary>
        public static readonly PointCloudFileFormat LaszipFormat;

        static Laszip()
        {
            LaszipFormat = new PointCloudFileFormat("LASzip", new[] { ".las", ".laz" }, LaszipInfo, Chunks);
            PointCloudFileFormat.Register(LaszipFormat);
        }

        /// <summary>
        /// Parses LASzip (.las, .laz) file.
        /// </summary>
        public static IEnumerable<Chunk> Chunks(string filename, ParseConfig config)
            => LASZip.Parser.ReadPoints(filename, config.MaxChunkPointCount)
            .Select(x => new Chunk(x.Positions, x.Colors, null, null, x.Classifications));

        /// <summary>
        /// Parses LASzip (.las, .laz) stream.
        /// </summary>
        public static IEnumerable<Chunk> Chunks(this Stream stream, long streamLengthInBytes, ParseConfig config)
            => LASZip.Parser.ReadPoints(stream, config.MaxChunkPointCount)
            .Select(x => new Chunk(x.Positions, x.Colors, null, null, x.Classifications));

        /// <summary>
        /// Gets general info for LASzip (.las, .laz) file.
        /// </summary>
        public static PointFileInfo LaszipInfo(string filename, ParseConfig config)
        {
            var fileSizeInBytes = new FileInfo(filename).Length;
            var info = LASZip.Parser.ReadInfo(filename);
            return new PointFileInfo(filename, LaszipFormat, fileSizeInBytes, info.Count, info.Bounds);
        }
    }
}
