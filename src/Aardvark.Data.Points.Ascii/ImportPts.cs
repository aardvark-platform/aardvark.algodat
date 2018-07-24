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
using Aardvark.Geometry.Points;
using System.Collections.Generic;
using System.IO;

namespace Aardvark.Data.Points.Import
{
    /// <summary>
    /// Importers for various formats.
    /// </summary>
    public static partial class Pts
    {
        /// <summary>
        /// Parses .pts file.
        /// </summary>
        public static IEnumerable<Chunk> Chunks(string filename, ImportConfig config)
            => Ascii.AsciiLines(HighPerformanceParsing.ParseLinesXYZIRGB, filename, config);

        /// <summary>
        /// Parses .pts stream.
        /// </summary>
        public static IEnumerable<Chunk> Chunks(this Stream stream, long streamLengthInBytes, ImportConfig config)
            => Ascii.AsciiLines(HighPerformanceParsing.ParseLinesXYZIRGB, stream, streamLengthInBytes, config);
    }
}
