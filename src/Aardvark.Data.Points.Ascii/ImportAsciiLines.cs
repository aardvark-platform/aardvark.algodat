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
using System;
using System.Collections.Generic;
using System.IO;

namespace Aardvark.Data.Points.Import
{
    /// <summary>
    /// Importers for various formats.
    /// </summary>
    public static class Ascii
    {
        /// <summary>
        /// Parses ASCII lines file.
        /// </summary>
        internal static IEnumerable<Chunk> AsciiLines(Func<byte[], int, double, Chunk?> lineParser,
            string filename, ImportConfig config
            )
        {
            var fileSizeInBytes = new FileInfo(filename).Length;
            var stream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
            return AsciiLines(lineParser, stream, fileSizeInBytes, config);
        }

        /// <summary>
        /// Parses ASCII lines stream.
        /// </summary>
        internal static IEnumerable<Chunk> AsciiLines(Func<byte[], int, double, Chunk?> lineParser,
            Stream stream, long streamLengthInBytes, ImportConfig config
            )
        {
            // importing file
            return stream
                .ChunkStreamAtNewlines(streamLengthInBytes, config.ReadBufferSizeInBytes, config.CancellationToken)
                .ParseBuffers(streamLengthInBytes, lineParser, config.MinDist, config.MaxDegreeOfParallelism, config.Verbose, config.CancellationToken)
                ;
        }
    }
}
