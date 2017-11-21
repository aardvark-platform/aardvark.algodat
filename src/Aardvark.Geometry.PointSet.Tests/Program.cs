using Aardvark.Base;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Uncodium.SimpleStore;
using Aardvark.Geometry.Points;
using Aardvark.Data.E57;

namespace Aardvark.Geometry.Tests
{
    public unsafe class Program
    {
        private const int MIN_CHUNK_SIZE_IN_BYTES = 16 * 1024 * 1024;
        private const double PER_MiB = 1.0 / (1024 * 1024);
        private const double PER_GiB = 1.0 / (1024 * 1024 * 1024);

        internal static Storage CreateStorage()
        {
            var x = new SimpleMemoryStore();
            return new Storage(
                (a, b, c, _) => x.Add(a, b, c), (a, _) => x.Get(a), (a, _) => x.Remove(a),
                (a, _) => x.TryGetFromCache(a), x.Dispose, x.Flush
                );
        }

        internal static void TestE57()
        {
            var filename = ""; 
            var fileSizeInBytes = new FileInfo(filename).Length;
            var stream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read);

            ASTM_E57.VerifyChecksums(stream, fileSizeInBytes);
            var header = ASTM_E57.E57FileHeader.Parse(stream);

            var data = header.E57Root.Data3D.Map(x => x.Points.ReadData().Take(1).ToList());

            //var ps = PointCloud.Parse(filename, ImportConfig.Default)
            //    .SelectMany(x => x.Positions)
            //    .ToArray()
            //    ;
        }

        public static void Main(string[] args)
        {
            TestE57();

            //var ms = new MemoryStream(Encoding.ASCII.GetBytes("2123\r\n1.2 -2.3 3.4 0 255 128\r\ndfg\r\n5.2 -2.3 3.4 0 255 128\r\n13"));

            //var filename = @"T:\Vgm\Data\PointCloud_HiRes.yxh";
            //var filename = @"T:\Vgm\Data\Kindergarten.pts";
            //var filename = @"T:\Vgm\Data\Laserscan-P20_Beiglboeck-2015.pts";
            //var filename = @"T:\Vgm\Data\JBs_Haus.pts";

            //var storage = CreateStorage();
            //Import.File(filename, storage, "test", 0, 16 * 1024, 2 * 1024 * 1024, 8, true, ProgressReporter.None, CancellationToken.None);

            //using (var fs = File.OpenRead(filename))
            //{
            //    var length = new FileInfo(filename).Length;
            //    //length = ms.Length;
            //    foreach (var chunk in Parse(fs, length, MIN_CHUNK_SIZE_IN_BYTES, 0, 0.0, CancellationToken.None))
            //    {
            //        //Console.WriteLine(chunk.SequenceNumber);
            //    }
            //}
        }
        
        internal static IEnumerable<Chunk> Parse(
            Stream stream, long streamLengthInBytes,
            int chunkSizeInBytes,
            int maxLevelOfParallelism,
            double minDist,
            CancellationToken ct
            )
        {
            if (chunkSizeInBytes < MIN_CHUNK_SIZE_IN_BYTES) chunkSizeInBytes = MIN_CHUNK_SIZE_IN_BYTES;
            if (maxLevelOfParallelism <= 0) maxLevelOfParallelism = Environment.ProcessorCount;

            var estimatedNumberOfChunks = streamLengthInBytes / chunkSizeInBytes + 1;
            var stats = new ParsingStats(streamLengthInBytes);

            var sampleCount = 0L;
            var totalBytesRead = 0L;
            var bounds = Box3d.Invalid;
            var buffer = new byte[chunkSizeInBytes];
            var sw = new Stopwatch(); sw.Start();
            var chunkNumber = 0;

            var semaphore = new SemaphoreSlim(maxLevelOfParallelism);
            var results = new Queue<Chunk>();

            var bufferStart = 0;
            while (true)
            {
                ct.ThrowIfCancellationRequested();

                var bufferBytesRead = stream.ReadAsync(buffer, bufferStart, chunkSizeInBytes - bufferStart).Result;
                if (bufferStart == 0 && bufferBytesRead == 0) break;

                var validBufferSize = bufferStart + bufferBytesRead;

                var data = buffer;

                // search from end of buffer for last newline
                var indexOfLastNewline = validBufferSize;
                while (--indexOfLastNewline >= 0)
                {
                    if (data[indexOfLastNewline] == 10) break;
                }
                if (indexOfLastNewline == -1)
                {
                    var content = new string(Encoding.ASCII.GetChars(data, 0, validBufferSize));
                    Console.WriteLine($"[YxhParser  ][WARNING] invalid data: {content}");
                }
                var needToCopyRest = indexOfLastNewline != -1 && data[indexOfLastNewline] == 10;

                // acquire next buffer
                buffer = new byte[chunkSizeInBytes];

                // copy rest of previous buffer to new buffer (if necessary)
                var _data = data;
                var _count = validBufferSize; // bufferSizeInBytes;

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

                var chunkSequenceNumber = chunkNumber++;
                semaphore.Wait(ct);

                var _ = Task.Run(() =>
                {
                    try
                    {
                        var samples = _ParseBuffer(_data, _count, minDist);
                        bounds.ExtendBy(new Box3d(samples.Positions));
                        Interlocked.Add(ref sampleCount, samples.Positions.Count);
                        var r = new Chunk(samples.Positions, samples.Colors, samples.BoundingBox);
                        lock (results) results.Enqueue(r);
                    }
                    catch (Exception e)
                    {
                        Report.Error($"[YxhParser  ] {e}");
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, ct);
                
                totalBytesRead += bufferBytesRead;
                stats.ReportProgress(totalBytesRead);

                Chunk? chunk = null;
                lock (results) { if (results.Count > 0) chunk = results.Dequeue(); }
                if (chunk.HasValue) yield return chunk.Value;


                Console.WriteLine($"[YxhParser  ] processed {totalBytesRead * PER_GiB:0.000} GiB at {stats.MiBsPerSecond:0.00} MiB/s");

                if (bufferBytesRead == 0) break;
            }

            while (semaphore.CurrentCount < maxLevelOfParallelism)
            {
                Task.Delay(TimeSpan.FromSeconds(0.1)).Wait();

                while (results.Count > 0)
                {
                    Chunk? chunk = null;
                    lock (results) { if (results.Count > 0) chunk = results.Dequeue(); }
                    if (chunk.HasValue) yield return chunk.Value;
                }
            }
            sw.Stop();

            Console.WriteLine($"[YxhParser  ] summary: processed {streamLengthInBytes} bytes in {sw.Elapsed.TotalSeconds:0.00} secs");
            Console.WriteLine($"[YxhParser  ] summary: with average throughput of {streamLengthInBytes * PER_MiB / sw.Elapsed.TotalSeconds:0.00} MiB/s");
            Console.WriteLine($"[YxhParser  ] summary: parsed {sampleCount} point samples");
            Console.WriteLine($"[YxhParser  ] summary: bounding box is {bounds}");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool SkipToNextLine(ref byte* p, byte* end)
        {
            while (p < end && *p != '\n') p++;
            p++;
            return p < end;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double ParseDouble(ref byte* p, byte* end)
        {
            if (p >= end) return double.NaN;

            while (*p == ' ' || p >= end) p++;
            if (p >= end) return double.NaN;

            var minus = *p == ((byte)'-');
            if (minus) p++;

            var x = 0.0;
            var parse = true;
            while (parse && p < end)
            {
                switch ((char)*p)
                {
                    case '0': x = x * 10.0; break;
                    case '1': x = x * 10.0 + 1.0; break;
                    case '2': x = x * 10.0 + 2.0; break;
                    case '3': x = x * 10.0 + 3.0; break;
                    case '4': x = x * 10.0 + 4.0; break;
                    case '5': x = x * 10.0 + 5.0; break;
                    case '6': x = x * 10.0 + 6.0; break;
                    case '7': x = x * 10.0 + 7.0; break;
                    case '8': x = x * 10.0 + 8.0; break;
                    case '9': x = x * 10.0 + 9.0; break;
                    case '.': parse = false; break;
                    case ' ': return minus ? -x : x;
                    default: return double.NaN;
                }
                p++;
            }
            if (p >= end) return minus ? -x : x;

            var y = 0.0;
            var r = 0.1;
            while (p < end)
            {
                switch ((char)*p)
                {
                    case '0': break;
                    case '1': y = y + r; break;
                    case '2': y = y + r * 2; break;
                    case '3': y = y + r * 3; break;
                    case '4': y = y + r * 4; break;
                    case '5': y = y + r * 5; break;
                    case '6': y = y + r * 6; break;
                    case '7': y = y + r * 7; break;
                    case '8': y = y + r * 8; break;
                    case '9': y = y + r * 9; break;
                    case ' ': return minus ? -x - y : x + y;
                    default: return double.NaN;
                }
                r *= 0.1;
                p++;
            }
            return minus ? -x - y : x + y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int? ParseInt(ref byte* p, byte* end)
        {
            if (p >= end) return null;

            while (*p == ' ' || p >= end) p++;
            if (p >= end) return null;

            var minus = *p == ((byte)'-');
            if (minus) p++;

            var x = 0;
            while (p < end)
            {
                switch ((char)*p)
                {
                    case '0': x = x * 10; break;
                    case '1': x = x * 10 + 1; break;
                    case '2': x = x * 10 + 2; break;
                    case '3': x = x * 10 + 3; break;
                    case '4': x = x * 10 + 4; break;
                    case '5': x = x * 10 + 5; break;
                    case '6': x = x * 10 + 6; break;
                    case '7': x = x * 10 + 7; break;
                    case '8': x = x * 10 + 8; break;
                    case '9': x = x * 10 + 9; break;
                    case '\r':
                    case ' ': return minus ? -x : x;
                    default: return null;
                }
                p++;
            }
            return minus ? -x : x;
        }

        private static Chunk _ParseBuffer(
            byte[] buffer, int count, double filterDist
            )
        {
            var ps = new List<V3d>();
            var cs = new List<C4b>();
            var prevX = double.PositiveInfinity;
            var prevY = double.PositiveInfinity;
            var prevZ = double.PositiveInfinity;

            var mps = new MemoryStream();
            var mcs = new MemoryStream();

            using (var bps = new BinaryWriter(new GZipStream(mps, CompressionMode.Compress)))
            using (var bcs = new BinaryWriter(new GZipStream(mcs, CompressionMode.Compress)))
            {
                unsafe
                {
                    fixed (byte* begin = buffer)
                    {
                        var p = begin;
                        var end = p + count;
                        while (p < end)
                        {
                            var x = ParseDouble(ref p, end);
                            var y = ParseDouble(ref p, end);
                            var z = ParseDouble(ref p, end);

                            var r = ParseInt(ref p, end);
                            var g = ParseInt(ref p, end);
                            var b = ParseInt(ref p, end);

                            SkipToNextLine(ref p, end);

                            if (!r.HasValue) continue;

                            var dx = x - prevX; if (dx < 0) dx = -dx;
                            var dy = y - prevY; if (dy < 0) dy = -dy;
                            var dz = z - prevZ; if (dz < 0) dz = -dz;
                            if (dx > filterDist || dy > filterDist || dz > filterDist)
                            {
                                ps.Add(new V3d(x, y, z));
                                cs.Add(new C4b(r.Value, g.Value, b.Value));
                                prevX = x;
                                prevY = y;
                                prevZ = z;

                                bps.Write(x); bps.Write(y); bps.Write(z);
                                bcs.Write((byte)r.Value); bcs.Write((byte)g.Value); bcs.Write((byte)b.Value);
                            }

                            //{
                            //    Console.WriteLine($"[YxhParser  ][WARNING] Invalid format: number of tokens is {ts.Length}, but expected 6. Index {i}/{count}.");
                            //    Console.WriteLine($"[YxhParser  ][WARNING] line (length={line.Length})");
                            //}
                        }
                    }
                }
            }

            var zps = mps.ToArray();
            var zcs = mcs.ToArray();

            Console.WriteLine($"ps: {zps.Length / (ps.Count * 24.0):0.00}, cs: {zcs.Length / (cs.Count * 3.0):0.00}");

            return new Chunk(ps, cs, new Box3d(ps));
        }
    }
}
