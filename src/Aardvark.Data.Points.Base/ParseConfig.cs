/*
   Aardvark Platform
   Copyright (C) 2006-2023  Aardvark Platform Team
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
    /// Specifies properties to parse and import.
    /// </summary>
    public class EnabledProperties
    {
        /// <summary>
        /// Parse classifications if available.
        /// </summary>
        public bool Classifications = true;

        /// <summary>
        /// Parse colors if available.
        /// </summary>
        public bool Colors = true;

        /// <summary>
        /// Parse intensities if available.
        /// </summary>
        public bool Intensities = true;

        /// <summary>
        /// Parse normals if available.
        /// </summary>
        public bool Normals = true;

        /// <summary>
        /// Parse/assign part indices if available.
        /// </summary>
        public bool PartIndices = true;
        
        /// <summary>
        /// Parse all available properties.
        /// </summary>
        public static readonly EnabledProperties All = new();
    }

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
        /// Properties to parse.
        /// </summary>
        public EnabledProperties EnabledProperties;

        /// <summary>
        /// Assign per-point part indices starting with this value.
        /// If a point cloud has no internal structure, then all points will be assigned this value.
        /// </summary>
        public int PartIndexOffset = 0;

        /// <summary>
        /// Default configuration.
        /// </summary>
        public static readonly ParseConfig Default =
            new(
                maxChunkPointCount: 5242880,              // 5 MPoints
                verbose: false,                     
                ct: default,
                maxDegreeOfParallelism: 0,                // Environment.ProcessorCount
                minDist: 0.0,
                readBufferSizeInBytes : 268435456,        // 256 MB
                enabledProperties: EnabledProperties.All,
                partIndexOffset: 0
            );

        /// <summary>
        /// Construct parse config.
        /// </summary>
        public ParseConfig(int maxChunkPointCount, bool verbose, CancellationToken ct, int maxDegreeOfParallelism, double minDist, int readBufferSizeInBytes)
            : this(maxChunkPointCount, verbose, ct, maxDegreeOfParallelism, minDist, readBufferSizeInBytes, EnabledProperties.All, partIndexOffset: 0)
        { }

        /// <summary>
        /// Construct parse config.
        /// </summary>
        public ParseConfig(
            int maxChunkPointCount,
            bool verbose,
            CancellationToken ct,
            int maxDegreeOfParallelism,
            double minDist,
            int readBufferSizeInBytes,
            EnabledProperties enabledProperties,
            int partIndexOffset
            )
        {
            MaxChunkPointCount = maxChunkPointCount;
            Verbose = verbose;
            CancellationToken = ct;
            MaxDegreeOfParallelism = maxDegreeOfParallelism;
            MinDist = minDist;
            ReadBufferSizeInBytes = readBufferSizeInBytes;
            EnabledProperties = enabledProperties;
            PartIndexOffset = partIndexOffset;
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
            EnabledProperties = x.EnabledProperties;
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

        /// <summary></summary>
        public ParseConfig WithEnabledProperties(EnabledProperties v) => new(this) { EnabledProperties = v };

        /// <summary></summary>
        public ParseConfig WithPartIndexOffset(int v) => new(this) { PartIndexOffset = v };

    }
}
