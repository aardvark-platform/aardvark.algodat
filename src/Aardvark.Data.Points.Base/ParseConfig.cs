/*
   Aardvark Platform
   Copyright (C) 2006-2020  Aardvark Platform Team
   https://aardvark.graphics

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
            new(
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
        public ParseConfig WithMaxChunkPointCount(int c) => new(this) { MaxChunkPointCount = c };

        /// <summary></summary>
        public ParseConfig WithCancellationToken(CancellationToken v) => new(this) { CancellationToken = v };

        /// <summary></summary>
        public ParseConfig WithMaxDegreeOfParallelism(int v) => new(this) { MaxDegreeOfParallelism = v };

        /// <summary></summary>
        public ParseConfig WithMinDist(double v) => new(this) { MinDist = v };

        /// <summary></summary>
        public ParseConfig WithReadBufferSizeInBytes(int v) => new(this) { ReadBufferSizeInBytes = v };

        /// <summary></summary>
        public ParseConfig WithVerbose(bool v) => new(this) { Verbose = v };
    }
}
