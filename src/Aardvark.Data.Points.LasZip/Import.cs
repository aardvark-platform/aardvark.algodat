/*
    Copyright (C) 2017-2020. Stefan Maierhofer.
    
    This code has been COPIED from https://github.com/stefanmaierhofer/LASzip.

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

        private static R[] Map<T, R>(T[] xs, Func<T, R> f)
        {
            var rs = new R[xs.Length];
            for (var i = 0; i < xs.Length; i++) rs[i] = f(xs[i]);
            return rs;
        }

        /// <summary>
        /// Parses LASzip (.las, .laz) file.
        /// </summary>
        public static IEnumerable<Chunk> Chunks(string filename, ParseConfig config)
            => Chunks(LASZip.Parser.ReadPoints(filename, config.MaxChunkPointCount));

        /// <summary>
        /// Parses LASzip (.las, .laz) stream.
        /// </summary>
        public static IEnumerable<Chunk> Chunks(this Stream stream, long streamLengthInBytes, ParseConfig config)
            => Chunks(LASZip.Parser.ReadPoints(stream, config.MaxChunkPointCount));

        private static IEnumerable<Chunk> Chunks(this IEnumerable<LASZip.Points> xs)
            => xs.Select(x => new Chunk(
                positions: x.Positions,
                colors: x.Colors != null ? Map(x.Colors, c => new C4b(c)) : null,
                normals: null,
                intensities: Map(x.Intensities, i => (int)i),
                classifications: x.Classifications,
                bbox: null
                )
            );

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
