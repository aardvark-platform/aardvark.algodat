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
//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Globalization;
//using System.IO;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;
//using Aardvark.Base;

//namespace Aardvark.Geometry.Points
//{
//    /// <summary>
//    /// Parser for .pts files.
//    /// </summary>
//    internal static class LegacyPtsParser
//    {
//        private const int MIN_BUFFER_SIZE_IN_BYTES = 16 * 1024 * 1024;
//        private const double PER_MiB = 1.0 / (1024 * 1024);
//        private const double PER_GiB = 1.0 / (1024 * 1024 * 1024);

//        /// <summary>
//        /// Parser points in .pts format from given stream.
//        /// </summary>
//        /// <param name="stream">Stream to parse from.</param>
//        /// <param name="streamLengthInBytes">Length of stream in bytes.</param>
//        /// <param name="bufferSizeInBytes">e.g. 16*1024*1024</param>
//        /// <param name="maxLevelOfParallelism">e.g. Environment.ProcessorCount</param>
//        /// <param name="filterDistSquared"></param>
//        /// <param name="onNextChunk">Called for each parsed chunk.</param>
//        /// <param name="ct"></param>
//        [Obsolete]
//        public static async Task Parse(
//            Stream stream, long streamLengthInBytes,
//            int bufferSizeInBytes,
//            int maxLevelOfParallelism,
//            double filterDistSquared,
//            Action<Chunk> onNextChunk,
//            CancellationToken ct
//            )
//        {
//            if (bufferSizeInBytes < MIN_BUFFER_SIZE_IN_BYTES) bufferSizeInBytes = MIN_BUFFER_SIZE_IN_BYTES;
//            if (maxLevelOfParallelism <= 0) maxLevelOfParallelism = Environment.ProcessorCount;

//            var estimatedNumberOfChunks = streamLengthInBytes / bufferSizeInBytes + 1;
//            var stats = new ParseFastStats(streamLengthInBytes);

//            var sampleCount = 0L;
//            var totalBytesRead = 0L;
//            var bounds = Box3d.Invalid;
//            var buffer = new byte[bufferSizeInBytes];
//            var sw = new Stopwatch(); sw.Start();
//            var chunkNumber = 0;
//            var taskCount = new SemaphoreSlim(maxLevelOfParallelism);

//            var bufferStart = 0;
//            while (true)
//            {
//                ct.ThrowIfCancellationRequested();
                
//                var bufferBytesRead = await stream.ReadAsync(buffer, bufferStart, bufferSizeInBytes - bufferStart);
//                if (bufferStart == 0 && bufferBytesRead == 0) break;

//                totalBytesRead += bufferBytesRead;
//                stats.ReportProgress(totalBytesRead);
//                var validBufferSize = bufferStart + bufferBytesRead;
                
//                var data = buffer;

//                // search from end of buffer for last newline
//                var indexOfLastNewline = validBufferSize;
//                while (--indexOfLastNewline >= 0)
//                {
//                    if (data[indexOfLastNewline] == 10) break;
//                }
//                if (indexOfLastNewline == -1)
//                {
//                    var content = new string(Encoding.ASCII.GetChars(data, 0, validBufferSize));
//                    Console.WriteLine($"[PtsParser  ][WARNING] invalid data: {content}");
//                }
//                var needToCopyRest = indexOfLastNewline != -1 && data[indexOfLastNewline] == 10;

//                // acquire next buffer
//                buffer = new byte[bufferSizeInBytes];

//                // copy rest of previous buffer to new buffer (if necessary)
//                var _data = data;
//                var _count = bufferSizeInBytes;

//                if (needToCopyRest)
//                {
//                    var countRest = validBufferSize - indexOfLastNewline - 1;
//                    Array.Copy(data, indexOfLastNewline + 1, buffer, 0, countRest);
//                    bufferStart = countRest;
//                    _count -= countRest;
//                }
//                else
//                {
//                    bufferStart = 0;
//                }

//                var chunkSequenceNumber = chunkNumber++;
//                taskCount.Wait(ct);
//                var _ = Task.Run(() =>
//                {
//                    try
//                    {
//                        var samples = _ParseFastProcessBuffer(_data, _count, filterDistSquared);
//                        bounds.ExtendBy(new Box3d(samples.Positions));
//                        Interlocked.Add(ref sampleCount, samples.Positions.Count);
//                        onNextChunk(new Chunk(samples.Positions, samples.Colors, samples.BoundingBox));
//                    }
//                    catch (Exception e)
//                    {
//                        Report.Error($"[PtsParser  ] {e}");
//                    }
//                    finally
//                    {
//                        taskCount.Release();
//                    }
//                }, ct);

//                Console.WriteLine($"[PtsParser  ] processed {totalBytesRead * PER_GiB:0.000} GiB at {stats.MiBsPerSecond:0.00} MiB/s");
                
//                if (bufferBytesRead == 0) break;
//            }

//            while (taskCount.CurrentCount < maxLevelOfParallelism) await Task.Delay(TimeSpan.FromSeconds(1.0));
//            sw.Stop();

//            Console.WriteLine($"[PtsParser  ] summary: processed {streamLengthInBytes} bytes in {sw.Elapsed.TotalSeconds:0.00} secs");
//            Console.WriteLine($"[PtsParser  ] summary: with average throughput of {streamLengthInBytes * PER_MiB / sw.Elapsed.TotalSeconds:0.00} MiB/s");
//            Console.WriteLine($"[PtsParser  ] summary: parsed {sampleCount} point samples");
//            Console.WriteLine($"[PtsParser  ] summary: bounding box is {bounds}");
//        }
        
//        private class ParseFastStats
//        {
//            public DateTimeOffset T0 { get; }
//            public long TotalBytesCount { get; }
//            public long TotalBytesRead { get; private set; }
//            public double BytesPerSecond { get; private set; }
//            public double MiBsPerSecond => BytesPerSecond / (1024 * 1024);
            
//            public ParseFastStats(long totalBytesCount)
//            {
//                T0 = DateTimeOffset.UtcNow;
//                TotalBytesCount = totalBytesCount;
//            }

//            public void ReportProgress(long totalBytesRead)
//            {
//                var t = DateTimeOffset.UtcNow;
//                var dt = (t - T0).TotalSeconds;

//                BytesPerSecond = totalBytesRead / dt;
//                TotalBytesRead = totalBytesRead;
//            }
//        }

//        private static Chunk _ParseFastProcessBuffer(
//            byte[] buffer, int count, double filterDistSquared
//            )
//        {
//            var ps = new List<V3d>();
//            var cs = new List<C4b>();
//            var separators = new char[] { ' ', (char)13 };
//            var lineStartIndex = 0;
//            var pPrev = V3d.Zero;
//            for (var i = 0; i < count; i++)
//            {
//                if (buffer[i] == 10 || i == count - 1)
//                {
//                    try
//                    {
//                        var line = new string(Encoding.ASCII.GetChars(buffer, lineStartIndex, i - lineStartIndex));
//                        var ts = line.Split(separators, StringSplitOptions.RemoveEmptyEntries);
//                        lineStartIndex = ++i;
//                        if (ts.Length == 1) continue; // skip
//                        if (ts.Length == 7)
//                        {
//                            var p = new V3d(
//                                double.Parse(ts[0], CultureInfo.InvariantCulture),
//                                double.Parse(ts[1], CultureInfo.InvariantCulture),
//                                double.Parse(ts[2], CultureInfo.InvariantCulture)
//                                );
//                            if ((p - pPrev).LengthSquared > filterDistSquared)
//                            {
//                                ps.Add(p);
//                                cs.Add(new C4b(byte.Parse(ts[4]), byte.Parse(ts[5]), byte.Parse(ts[6])));
//                                pPrev = p;
//                            }
//                        }
//                        else
//                        {
//                            Console.WriteLine($"[PtsParser  ][WARNING] Invalid format: number of tokens is {ts.Length}, but expected 7. Index {i}/{count}.");
//                            Console.WriteLine($"[PtsParser  ][WARNING] line (length={line.Length})");
//                        }
//                    }
//                    catch (OutOfMemoryException)
//                    {
//                        Console.WriteLine($"[PtsParser  ][ERROR] out-of-memory exception (line length is {i - lineStartIndex})");
//                        throw;
//                    }
//                }
//            }
//            return new Chunk(ps, cs, new Box3d(ps));
//        }
        
//        /// <summary>
//        /// Buffer is expected to contain ASCII. Lines separated by '\n'.
//        /// Expected line format: [double X] [double Y] [double Z] [int INTENSITY] [byte R] [byte G] [byte B] \n
//        /// </summary>
//        public static Chunk? ParsePtsBuffer(
//            byte[] buffer, int count, double filterDist
//            )
//        {
//            var filterDistSquared = filterDist * filterDist;
//            var ps = new List<V3d>();
//            var cs = new List<C4b>();
//            var separators = new char[] { ' ', (char)13 };
//            var lineStartIndex = 0;
//            var pPrev = V3d.Zero;
//            for (var i = 0; i < count; i++)
//            {
//                if (buffer[i] == 10 || i == count - 1)
//                {
//                    try
//                    {
//                        var line = new string(Encoding.ASCII.GetChars(buffer, lineStartIndex, i - lineStartIndex));
//                        var ts = line.Split(separators, StringSplitOptions.RemoveEmptyEntries);
//                        lineStartIndex = ++i;
//                        if (ts.Length == 1) continue; // skip
//                        if (ts.Length == 7)
//                        {
//                            var p = new V3d(
//                                double.Parse(ts[0], CultureInfo.InvariantCulture),
//                                double.Parse(ts[1], CultureInfo.InvariantCulture),
//                                double.Parse(ts[2], CultureInfo.InvariantCulture)
//                                );
//                            if ((p - pPrev).LengthSquared > filterDistSquared)
//                            {
//                                ps.Add(p);
//                                cs.Add(new C4b(byte.Parse(ts[4]), byte.Parse(ts[5]), byte.Parse(ts[6])));
//                                pPrev = p;
//                            }
//                        }
//                        else
//                        {
//                            Console.WriteLine($"[PtsParser  ][WARNING] Invalid format: number of tokens is {ts.Length}, but expected 7. Index {i}/{count}.");
//                            Console.WriteLine($"[PtsParser  ][WARNING] line (length={line.Length})");
//                        }
//                    }
//                    catch (OutOfMemoryException)
//                    {
//                        Console.WriteLine($"[PtsParser  ][ERROR] out-of-memory exception (line length is {i - lineStartIndex})");
//                        throw;
//                    }
//                }
//            }
//            return new Chunk(ps, cs, new Box3d(ps));
//        }
//    }
//}
