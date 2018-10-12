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
using System.Linq;

namespace Aardvark.Data.Points.Import
{
    /// <summary>
    /// Importer for custom ASCII format.
    /// </summary>
    public static class Ascii
    {
        /// <summary>
        /// Custom ASCII parser tokens.
        /// </summary>
        public enum Token
        {
            /// <summary>
            /// Parses Position.X from double value.
            /// </summary>
            PositionX,

            /// <summary>
            /// Parses Position.Y from double value.
            /// </summary>
            PositionY,

            /// <summary>
            /// Parses Position.Z from double value.
            /// </summary>
            PositionZ,



            /// <summary>
            /// Parses Normal.X from float value.
            /// </summary>
            NormalX,

            /// <summary>
            /// Parses Normal.Y from float value.
            /// </summary>
            NormalY,

            /// <summary>
            /// Parses Normal.Z from float value.
            /// </summary>
            NormalZ,



            /// <summary>
            /// Parses Color.R from byte value [0, 255].
            /// </summary>
            ColorR,

            /// <summary>
            /// Parses Color.G from byte value [0, 255].
            /// </summary>
            ColorG,

            /// <summary>
            /// Parses Color.B from byte value [0, 255].
            /// </summary>
            ColorB,

            /// <summary>
            /// Parses Color.A from byte value [0, 255].
            /// </summary>
            ColorA,



            /// <summary>
            /// Parses Color.R from float value [0.0, 1.0].
            /// </summary>
            ColorRf,

            /// <summary>
            /// Parses Color.G from float value [0.0, 1.0].
            /// </summary>
            ColorGf,

            /// <summary>
            /// Parses Color.B from float value [0.0, 1.0].
            /// </summary>
            ColorBf,

            /// <summary>
            /// Parses Color.A from float value [0.0, 1.0].
            /// </summary>
            ColorAf,



            /// <summary>
            /// Parses Intensity from int value.
            /// </summary>
            Intensity,




            /// <summary>
            /// Skips value. 
            /// </summary>
            Skip,
        }


        internal static bool HasColorTokens(this Token[] layout)
            => layout.Contains(Token.ColorR) ||
               layout.Contains(Token.ColorRf) ||
               layout.Contains(Token.ColorG) ||
               layout.Contains(Token.ColorGf) ||
               layout.Contains(Token.ColorB) ||
               layout.Contains(Token.ColorBf) ||
               layout.Contains(Token.ColorA) ||
               layout.Contains(Token.ColorAf)
               ;

        internal static bool HasNormalTokens(this Token[] layout)
            => layout.Contains(Token.NormalX) ||
               layout.Contains(Token.NormalY) ||
               layout.Contains(Token.NormalZ)
               ;

        internal static bool HasIntensityTokens(this Token[] layout)
            => layout.Contains(Token.Intensity)
               ;

        /// <summary>
        /// </summary>
        public static PointCloudFileFormat CreateFormat(string description, Token[] lineDefinition)
            => new PointCloudFileFormat(description, new string[0],
                (filename, config) => CustomAsciiInfo(filename, lineDefinition, config),
                (filename, config) => Chunks(filename, lineDefinition, config)
                );

        /// <summary>
        /// Parses ASCII file.
        /// </summary>
        public static IEnumerable<Chunk> Chunks(string filename, Token[] lineDefinition, ImportConfig config)
        {
            Chunk? lineParser(byte[] buffer, int count, double filterDist)
                => LineParsers.Custom(buffer, count, filterDist, lineDefinition);
            return Parsing.AsciiLines(lineParser, filename, config);
        }

        /// <summary>
        /// Parses ASCII stream.
        /// </summary>
        public static IEnumerable<Chunk> Chunks(this Stream stream, long streamLengthInBytes, Token[] lineDefinition, ImportConfig config)
        {
            Chunk? lineParser(byte[] buffer, int count, double filterDist)
                => LineParsers.Custom(buffer, count, filterDist, lineDefinition);
            return Parsing.AsciiLines(lineParser, stream, streamLengthInBytes, config);
        }

        /// <summary>
        /// Gets general info for custom ASCII file.
        /// </summary>
        public static PointFileInfo CustomAsciiInfo(string filename, Token[] lineDefinition, ImportConfig config)
        {
            var filesize = new FileInfo(filename).Length;
            var pointCount = 0L;
            var pointBounds = Box3d.Invalid;
            foreach (var chunk in Chunks(filename, lineDefinition, ImportConfig.Default))
            {
                pointCount += chunk.Count;
                pointBounds.ExtendBy(chunk.BoundingBox);
            }
            var format = CreateFormat("Custom ASCII", lineDefinition);
            return new PointFileInfo(filename, format, filesize, pointCount, pointBounds);
        }
    }
}
