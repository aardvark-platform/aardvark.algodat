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
using System.Threading;

namespace Aardvark.Data.Points
{
    /// <summary>
    /// Config for parsing.
    /// </summary>
    public struct ParseConfig
    {
        /// <summary>
        /// Parses in chunks of this number of points.
        /// </summary>
        public int MaxChunkPointCount;

        /// <summary>
        /// Report what happens during parsing. 
        /// </summary>
        public bool Verbose;

        /// <summary>
        /// Parsing cancellation.
        /// </summary>
        public CancellationToken CancellationToken;

        /// <summary>
        /// Use at max given number of parallel threads.
        /// </summary>
        public int MaxDegreeOfParallelism;

        /// <summary>
        /// Skip points which are less than given distance from previous point.
        /// </summary>
        public double MinDist;

        /// <summary>
        /// Read raw data in chunks of given number of bytes.
        /// </summary>
        public int ReadBufferSizeInBytes;

        /// <summary>
        /// Default configuration.
        /// </summary>
        public static readonly ParseConfig Default =
            new ParseConfig(
                maxChunkPointCount: 5242880,        // 5 MPoints
                verbose: false,                     
                ct: default,
                maxDegreeOfParallelism: 0,          // Environment.ProcessorCount
                minDist: 0.0,
                readBufferSizeInBytes : 268435456   // 256 MB
            );

        /// <summary>
        /// Construct parse config.
        /// </summary>
        public ParseConfig(int maxChunkPointCount, bool verbose, CancellationToken ct, int maxDegreeOfParallelism, double minDist, int readBufferSizeInBytes)
        {
            MaxChunkPointCount = maxChunkPointCount;
            Verbose = verbose;
            CancellationToken = ct;
            MaxDegreeOfParallelism = maxDegreeOfParallelism;
            MinDist = minDist;
            ReadBufferSizeInBytes = readBufferSizeInBytes;
        }

        /// <summary>
        /// Copy.
        /// </summary>
        public ParseConfig(ParseConfig x)
        {
            MaxChunkPointCount = x.MaxChunkPointCount;
            Verbose = x.Verbose;
            CancellationToken = x.CancellationToken;
            MaxDegreeOfParallelism = x.MaxDegreeOfParallelism;
            MinDist = x.MinDist;
            ReadBufferSizeInBytes = x.ReadBufferSizeInBytes;
        }

        /// <summary></summary>
        public ParseConfig WithMaxChunkPointCount(int c) => new ParseConfig(this) { MaxChunkPointCount = c };

        /// <summary></summary>
        public ParseConfig WithCancellationToken(CancellationToken v) => new ParseConfig(this) { CancellationToken = v };

        /// <summary></summary>
        public ParseConfig WithMaxDegreeOfParallelism(int v) => new ParseConfig(this) { MaxDegreeOfParallelism = v };

        /// <summary></summary>
        public ParseConfig WithMinDist(double v) => new ParseConfig(this) { MinDist = v };

        /// <summary></summary>
        public ParseConfig WithReadBufferSizeInBytes(int v) => new ParseConfig(this) { ReadBufferSizeInBytes = v };

        /// <summary></summary>
        public ParseConfig WithVerbose(bool v) => new ParseConfig(this) { Verbose = v };
    }
}
