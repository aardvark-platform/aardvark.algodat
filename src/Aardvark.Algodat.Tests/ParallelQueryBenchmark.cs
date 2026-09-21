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
using Aardvark.Data.Points;
using Aardvark.Geometry.Points;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Aardvark.Geometry.Tests
{
    /// <summary>
    /// Throughput of a mixed query workload (near-ray, inside-box, cell) vs. number of threads,
    /// on a disk store, with cold (cleared LRU) and warm cache. Prints a table; not a pass/fail test.
    /// Run with: dotnet test --filter FullyQualifiedName~ParallelQueryBenchmark
    /// </summary>
    [TestFixture]
    [Explicit("benchmark")]
    public class ParallelQueryBenchmark
    {
        private const int PointCount = 1_000_000;
        private const int SplitLimit = 8192;
        private const int OpsPerThread = 300;
        private const int Repeats = 3;
        private static readonly int[] ThreadCounts = { 1, 2, 4, 8, 16 };

        private static Chunk CreateTestChunk()
        {
            var r = new Random(0);
            var ps = new V3d[PointCount];
            var cs = new C4b[PointCount];
            for (var i = 0; i < PointCount; i++)
            {
                ps[i] = new V3d(r.NextDouble(), r.NextDouble(), r.NextDouble());
                cs[i] = new C4b(r.Next(256), r.Next(256), r.Next(256));
            }
            var chunk = new Chunk(ps, cs, null, null, null, null, null, null);
            if (ImportConfig.Default.ParseConfig.EnabledProperties.PartIndices) chunk = chunk.WithPartIndices(42u, null);
            return chunk;
        }

        /// <summary>
        /// Counts calls to and time spent inside the underlying store's Get (i.e. under the SimpleDiskStore lock).
        /// </summary>
        private sealed class StoreStats
        {
            public long GetCalls;
            public long GetTicks;
            public long GetBytes;
            public void Reset() { GetCalls = 0; GetTicks = 0; GetBytes = 0; }
        }

        private static (Storage storage, StoreStats stats) Instrument(Storage inner, LruDictionary<string, object> cache)
        {
            var stats = new StoreStats();
            byte[]? get(string key)
            {
                var t0 = Stopwatch.GetTimestamp();
                var r = inner.f_get(key);
                Interlocked.Add(ref stats.GetTicks, Stopwatch.GetTimestamp() - t0);
                Interlocked.Increment(ref stats.GetCalls);
                if (r != null) Interlocked.Add(ref stats.GetBytes, r.Length);
                return r;
            }
            var storage = new Storage(inner.f_add, get, inner.f_getSlice, inner.f_remove, inner.f_dispose, inner.f_flush, cache);
            return (storage, stats);
        }

        private static long RunOps(IPointCloudNode root, int seed, int ops)
        {
            var r = new Random(seed);
            long touched = 0;
            for (var i = 0; i < ops; i++)
            {
                var ray = new Ray3d(new V3d(-1.0, r.NextDouble(), r.NextDouble()), V3d.IOO);
                foreach (var c in root.QueryPointsNearRay(ray, 0.01, 0.0, 10.0)) touched += c.Count;

                var boxMin = new V3d(r.NextDouble() * 0.9, r.NextDouble() * 0.9, r.NextDouble() * 0.9);
                var box = new Box3d(boxMin, boxMin + new V3d(0.1));
                foreach (var c in root.QueryPointsInsideBox(box)) touched += c.Count;

                var cell = new Cell(r.Next(8), r.Next(8), r.Next(8), -3);
                foreach (var c in root.QueryCell(cell).GetPoints(0)) touched += c.Count;
            }
            return touched;
        }

        private static double Seconds(long ticks) => (double)ticks / Stopwatch.Frequency;

        /// <summary>
        /// Runs OpsPerThread ops on each of the given number of threads (started simultaneously); returns wall seconds.
        /// </summary>
        private static double Measure(IPointCloudNode root, int threads)
        {
            using var barrier = new Barrier(threads);
            var tasks = new Task<long>[threads];
            var sw = Stopwatch.StartNew();
            for (var t = 0; t < threads; t++)
            {
                var seed = t;
                tasks[t] = Task.Factory.StartNew(() =>
                {
                    barrier.SignalAndWait();
                    return RunOps(root, seed, OpsPerThread);
                }, TaskCreationOptions.LongRunning);
            }
            Task.WaitAll(tasks);
            sw.Stop();
            return sw.Elapsed.TotalSeconds;
        }

        [Test]
        public void QueriesVsThreads()
        {
            var dir = Path.Combine(Path.GetTempPath(), "algodat-parallel-benchmark", Guid.NewGuid().ToString());
            Directory.CreateDirectory(dir);
            try
            {
                var swImport = Stopwatch.StartNew();
                {
                    var storage = PointCloud.OpenStore(dir, new LruDictionary<string, object>(1L << 30));
                    var config = ImportConfig.Default.WithStorage(storage).WithKey("bench").WithOctreeSplitLimit(SplitLimit);
                    var ps = PointCloud.Chunks(CreateTestChunk(), config);
                    TestContext.Out.WriteLine($"imported {ps.PointCount:N0} points, {ps.Root.Value.CountNodes(outOfCore: true):N0} nodes in {swImport.Elapsed.TotalSeconds:0.0} s");
                    storage.Flush();
                    storage.Dispose();
                }

                var cache = new LruDictionary<string, object>(1L << 30);
                var (storage2, stats) = Instrument(PointCloud.OpenStore(dir, cache: default), cache);
                var root = (storage2.GetPointSet("bench") ?? throw new Exception()).Root.Value;

                TestContext.Out.WriteLine();
                TestContext.Out.WriteLine($"{"threads",7} | {"mode",5} | {"wall s",8} | {"ops/s",8} | {"speedup",7} | {"store gets",10} | {"store MB",8} | {"store s (sum)",13} | {"store share",11}");
                TestContext.Out.WriteLine(new string('-', 110));

                // warmup: JIT and first decode (cold and warm)
                RunOps(root, 12345, OpsPerThread);
                cache.Clear();
                RunOps(root, 12345, OpsPerThread);

                double coldBase = 0, warmBase = 0;
                foreach (var threads in ThreadCounts)
                {
                    foreach (var mode in new[] { "cold", "warm" })
                    {
                        // best of N repeats
                        var wall = double.MaxValue;
                        long getCalls = 0, getTicks = 0, getBytes = 0;
                        for (var rep = 0; rep < Repeats; rep++)
                        {
                            if (mode == "cold") cache.Clear();
                            stats.Reset();
                            var w = Measure(root, threads);
                            if (w < wall) { wall = w; getCalls = stats.GetCalls; getTicks = stats.GetTicks; getBytes = stats.GetBytes; }
                        }

                        var opsPerSec = threads * OpsPerThread / wall;
                        if (threads == 1) { if (mode == "cold") coldBase = opsPerSec; else warmBase = opsPerSec; }
                        var speedup = opsPerSec / (mode == "cold" ? coldBase : warmBase);
                        var storeSeconds = Seconds(getTicks);
                        var share = storeSeconds / (wall * threads);

                        TestContext.Out.WriteLine(
                            $"{threads,7} | {mode,5} | {wall,8:0.000} | {opsPerSec,8:0.0} | {speedup,7:0.00} | {getCalls,10:N0} | {getBytes / (1024.0 * 1024.0),8:0.0} | {storeSeconds,13:0.000} | {share,11:P0}");
                    }
                }

                storage2.Dispose();
            }
            finally
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }
        }
    }
}
