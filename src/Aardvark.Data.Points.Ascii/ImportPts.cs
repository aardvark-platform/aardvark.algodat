/*
    Copyright (C) 2006-2025. Aardvark Platform Team. http://github.com/aardvark-platform.
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
    /// Importer for PTS format.
    /// </summary>
    [PointCloudFileFormatAttribute]
    public static class Pts
    {
        /// <summary>
        /// Pts file format.
        /// </summary>
        public static readonly PointCloudFileFormat PtsFormat;

        private static readonly Ascii.Token[] s_lineDefinition =
        [
            Ascii.Token.PositionX, Ascii.Token.PositionY, Ascii.Token.PositionZ,
            Ascii.Token.Skip,
            Ascii.Token.ColorR, Ascii.Token.ColorG, Ascii.Token.ColorB,
        ];

        static Pts()
        {
            PtsFormat = new PointCloudFileFormat("pts", [".pts"], PtsInfo, Chunks);
            PointCloudFileFormat.Register(PtsFormat);
        }

        /// <summary>
        /// Parses a PTS file while omitting disabled color output.
        /// </summary>
        public static IEnumerable<Chunk> Chunks(string filename, ParseConfig config)
            => Ascii.Chunks(filename, s_lineDefinition, config);

        /// <summary>
        /// Parses a PTS stream while omitting disabled color output.
        /// </summary>
        public static IEnumerable<Chunk> Chunks(this Stream stream, long streamLengthInBytes, ParseConfig config)
            => Ascii.Chunks(stream, streamLengthInBytes, s_lineDefinition, config);

        /// <summary>
        /// Gets general info for .pts file.
        /// </summary>
        public static PointFileInfo PtsInfo(string filename, ParseConfig config)
        {
            var filesize = new FileInfo(filename).Length;
            var pointCount = 0L;
            var pointBounds = Box3d.Invalid;
            foreach (var chunk in Chunks(filename, ParseConfig.Default))
            {
                pointCount += chunk.Count;
                pointBounds.ExtendBy(chunk.BoundingBox);
            }
            return new PointFileInfo(filename, PtsFormat, filesize, pointCount, pointBounds);
        }
    }
}
