/*
   Copyright (C) 2017-2020. Stefan Maierhofer.
    
   This code has been COPIED from https://github.com/stefanmaierhofer/LASzip.

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/

#pragma warning disable IDE0060 // Remove unused parameter

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
            => Chunks(LASZip.Parser.ReadPoints(filename, config.MaxChunkPointCount, config.Verbose), partIndices: config.EnabledProperties.PartIndices ? config.PartIndexOffset : null);

        /// <summary>
        /// Parses LASzip (.las, .laz) stream.
        /// </summary>
        public static IEnumerable<Chunk> Chunks(this Stream stream, long streamLengthInBytes, ParseConfig config)
            => Chunks(LASZip.Parser.ReadPoints(stream, config.MaxChunkPointCount, config.Verbose), partIndices: config.EnabledProperties.PartIndices ? config.PartIndexOffset : null);

        private static IEnumerable<Chunk> Chunks(this IEnumerable<LASZip.Points> xs, object? partIndices)
            => xs.Select(x => new Chunk(
                positions: x.Positions,
                colors: x.Colors != null ? Map(x.Colors, c => new C4b(c)) : null,
                normals: null,
                intensities: x.Intensities != null ? Map(x.Intensities, i => (int)i) : null,
                classifications: x.Classifications,
                partIndices: partIndices,
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
