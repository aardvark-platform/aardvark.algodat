/*
    Copyright (C) 2006-2024. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System.Threading;

namespace Aardvark.Data.Points
{
    /// <summary>
    /// </summary>
    public static class Parsing
    {
        private const double PER_MiB = 1.0 / (1024 * 1024);
        private const double PER_GiB = 1.0 / (1024 * 1024 * 1024);

        /// <summary>
        /// Parses ASCII lines file.
        /// </summary>
        internal static IEnumerable<Chunk> AsciiLines(Func<byte[], int, double, int?, Chunk> lineParser,
            string filename, ParseConfig config
            )
        {
            var fileSizeInBytes = new FileInfo(filename).Length;
            var stream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
            return AsciiLines(lineParser, stream, fileSizeInBytes, config);
        }

        /// <summary>
        /// Parses ASCII lines stream.
        /// </summary>
        internal static IEnumerable<Chunk> AsciiLines(Func<byte[], int, double, int?, Chunk> lineParser,
            Stream stream, long streamLengthInBytes, ParseConfig config
            )
        {
            // importing file
            var result = stream
                .ChunkStreamAtNewlines(streamLengthInBytes, config.ReadBufferSizeInBytes, config.CancellationToken)
                .ParseBuffers(streamLengthInBytes, lineParser, config)
                ;
            return result;
        }

        /// <summary>
        /// Splits a stream into buffers of approximately the given size.
        /// Splits will only occur at newlines, therefore each buffer
        /// will be sized less or equal than maxChunkSizeInBytes.
        /// </summary>
        public static IEnumerable<Buffer> ChunkStreamAtNewlines(
            this Stream stream, long streamLengthInBytes, int maxChunkSizeInBytes,
            CancellationToken ct
            )
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            if (maxChunkSizeInBytes < 1) throw new ArgumentException(
                $"Argument 'maxChunkSizeInBytes' must be greater than 0, but is {maxChunkSizeInBytes}."
                );

            //var estimatedNumberOfChunks = streamLengthInBytes / maxChunkSizeInBytes + 1;
            var stats = new ParsingStats(streamLengthInBytes);

            var totalBytesRead = 0L;
            //var bounds = Box3d.Invalid;
            var buffer = new byte[maxChunkSizeInBytes];

            var bufferStart = 0;
            while (true)
            {
                ct.ThrowIfCancellationRequested();

                var bufferBytesRead = stream.ReadAsync(buffer, bufferStart, maxChunkSizeInBytes - bufferStart).Result;
                if (bufferStart == 0 && bufferBytesRead == 0) break;

                var validBufferSize = bufferStart + bufferBytesRead;

                var data = buffer;

                // search from end of buffer for last newline
                var indexOfLastNewline = validBufferSize;
                while (--indexOfLastNewline >= 0 && data[indexOfLastNewline] != 10) ;
                var needToCopyRest = indexOfLastNewline != -1 && data[indexOfLastNewline] == 10;

                // acquire next buffer
                buffer = new byte[maxChunkSizeInBytes];

                // copy rest of previous buffer to new buffer (if necessary)
                var _data = data;
                var _count = validBufferSize;

                if (needToCopyRest)
                {
                    var countRest = validBufferSize - indexOfLastNewline - 1;
                    Array.Copy(data, indexOfLastNewline + 1, buffer, 0, countRest);
                    bufferStart = countRest;
                    _count -= countRest;
                }
                else
                {
                    bufferStart = 0;
                }

                totalBytesRead += bufferBytesRead;
                stats.ReportProgress(totalBytesRead);
                yield return Buffer.Create(_data, 0, _count);
            }
        }

        /// <summary>
        /// Parses a sequence of buffers into a sequence of point chunks.
        /// </summary>
        /// <param name="buffers"></param>
        /// <param name="sumOfAllBufferSizesInBytes"></param>
        /// <param name="parser">(buffer, count, minDist) => Chunk</param>
        /// <param name="config"></param>
        /// <returns></returns>
        public static IEnumerable<Chunk> ParseBuffers(
            this IEnumerable<Buffer> buffers, long sumOfAllBufferSizesInBytes,
            Func<byte[], int, double, int?, Chunk> parser, 
            ParseConfig config
            )
        {
            var stats = new ParsingStats(sumOfAllBufferSizesInBytes);
            var sampleCount = 0L;
            var sampleCountYielded = 0L;
            var totalBytesRead = 0L;
            var bounds = Box3d.Invalid;

            var result = buffers

                .MapParallel((buffer, ct2) =>
                    {
                        int? partIndices = config.EnabledProperties.PartIndices ? config.PartIndexOffset : null;
                        var optionalSamples = parser(buffer.Data, buffer.Count, config.MinDist, partIndices);
                        if (optionalSamples == null) return Chunk.Empty;
                        var samples = optionalSamples;
                        if (config.EnabledProperties.PartIndices) samples = samples.WithPartIndices(config.PartIndexOffset, new Range1i(config.PartIndexOffset), [ config.PartIndexOffset ]);
                        bounds.ExtendBy(new Box3d(samples.Positions));
                        Interlocked.Add(ref sampleCount, samples.Count);
                        var r = new Chunk(
                            samples.Positions, samples.Colors, samples.Normals, samples.Intensities, samples.Classifications,
                            samples.PartIndices, samples.PartIndexRange, samples.PartIndexSet,
                            samples.BoundingBox
                            );

                        Interlocked.Add(ref sampleCountYielded, r.Count);
                        Interlocked.Add(ref totalBytesRead, buffer.Count);
                        stats.ReportProgress(totalBytesRead);
                
                        if (config.Verbose) Console.WriteLine(
                            $"[Parsing] processed {totalBytesRead * PER_GiB:0.000} GiB at {stats.MiBsPerSecond:0.00} MiB/s"
                            );

                        return r;
                    },
                    config.MaxChunkPointCount,
                    elapsed =>
                    {
                        if (config.Verbose)
                        {
                            Console.WriteLine($"[Parsing] summary: processed {sumOfAllBufferSizesInBytes} bytes in {elapsed.TotalSeconds:0.00} secs");
                            Console.WriteLine($"[Parsing] summary: with average throughput of {sumOfAllBufferSizesInBytes * PER_MiB / elapsed.TotalSeconds:0.00} MiB/s");
                            Console.WriteLine($"[Parsing] summary: parsed  {sampleCount} point samples");
                            Console.WriteLine($"[Parsing] summary: yielded {sampleCountYielded} point samples");
                            Console.WriteLine($"[Parsing] summary: bounding box is {bounds}");
                        }
                    }, config.CancellationToken)

                .Where(c => c != null && c.Count > 0)

                ;

            return result;
        }
    }
}
