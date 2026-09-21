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
using NUnit.Framework.Legacy;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Aardvark.Geometry.Tests
{
    /// <summary>
    /// Characterization tests for running point queries concurrently on a shared octree.
    /// These exercise the paths used by Vgm.Api's query endpoints (cell enumeration,
    /// near-ray, near-point) from many threads at once and compare against a sequential run.
    /// </summary>
    [TestFixture]
    public class ConcurrencyTests
    {
        private const int PointCount = 50_000;
        private const int SplitLimit = 512;
        private const int Threads = 16;
        private const int Rounds = 8;

        private static Chunk CreateTestChunk(int seed)
        {
            var r = new Random(seed);
            var ps = new V3d[PointCount];
            var cs = new C4b[PointCount];
            var js = new int[PointCount];
            for (var i = 0; i < PointCount; i++)
            {
                ps[i] = new V3d(r.NextDouble(), r.NextDouble(), r.NextDouble());
                cs[i] = new C4b(r.Next(256), r.Next(256), r.Next(256));
                js[i] = r.Next(1000);
            }
            var chunk = new Chunk(ps, cs, null, js, null, null, null, null);
            if (ImportConfig.Default.ParseConfig.EnabledProperties.PartIndices) chunk = chunk.WithPartIndices(42u, null);
            return chunk;
        }

        /// <summary>
        /// Imports a test cloud into a disk store, closes it, and reopens it with an empty cache,
        /// so that all node and attribute loads happen lazily (and concurrently) during the test.
        /// </summary>
        private static (Storage storage, IPointCloudNode root, string dir) CreateDiskStoreCloud(string key)
        {
            var dir = Path.Combine(Path.GetTempPath(), "algodat-concurrency-tests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(dir);

            {
                var storage = PointCloud.OpenStore(dir, new LruDictionary<string, object>(1L << 30));
                var config = ImportConfig.Default
                    .WithStorage(storage)
                    .WithKey(key)
                    .WithOctreeSplitLimit(SplitLimit)
                    ;
                var _ = PointCloud.Chunks(CreateTestChunk(0), config);
                storage.Flush();
                storage.Dispose();
            }

            var reopened = PointCloud.OpenStore(dir, new LruDictionary<string, object>(1L << 30));
            var pointset = reopened.GetPointSet(key) ?? throw new Exception("Point set not found.");
            return (reopened, pointset.Root.Value, dir);
        }

        private static void Cleanup(Storage storage, string dir)
        {
            try { storage.Dispose(); } catch { }
            try { Directory.Delete(dir, recursive: true); } catch { }
        }

        /// <summary>
        /// Deterministic summary of the query results used by Vgm.Api endpoints.
        /// </summary>
        private sealed record QuerySummary(
            (Cell cell, long count, long colorSum, long intensitySum)[] Cells,
            long RayPoints,
            long NearPoints,
            long TotalPoints
            );

        private static QuerySummary RunQueries(IPointCloudNode root)
        {
            var cells = new List<(Cell, long, long, long)>();
            foreach (var c in Queries.EnumerateCells(root, -2, V3i.III))
            {
                long count = 0, colorSum = 0, intensitySum = 0;
                foreach (var chunk in c.GetPoints(0))
                {
                    count += chunk.Count;
                    if (chunk.HasColors) for (var i = 0; i < chunk.Count; i++) colorSum += chunk.Colors[i].R;
                    if (chunk.HasIntensities) for (var i = 0; i < chunk.Count; i++) intensitySum += chunk.Intensities[i];
                }
                cells.Add((c.Cell, count, colorSum, intensitySum));
            }
            cells.Sort((a, b) => a.Item1.ToString().CompareTo(b.Item1.ToString()));

            var ray = new Ray3d(new V3d(-1.0, 0.5, 0.5), V3d.IOO);
            var rayPoints = root.QueryPointsNearRay(ray, 0.05, 0.0, 10.0).Sum(x => (long)x.Count);

            var near = root.QueryPointsNearPoint(new V3d(0.5, 0.5, 0.5), 0.1, 1000);
            var nearPoints = (long)near.Count;

            var total = root.QueryPointsInsideBox(Box3d.Unit).Sum(x => (long)x.Count);

            return new QuerySummary(cells.ToArray(), rayPoints, nearPoints, total);
        }

        private static void AssertSameSummary(QuerySummary expected, QuerySummary actual)
        {
            ClassicAssert.AreEqual(expected.TotalPoints, actual.TotalPoints, "total points");
            ClassicAssert.AreEqual(expected.RayPoints, actual.RayPoints, "ray points");
            ClassicAssert.AreEqual(expected.NearPoints, actual.NearPoints, "near points");
            ClassicAssert.AreEqual(expected.Cells.Length, actual.Cells.Length, "cell count");
            for (var i = 0; i < expected.Cells.Length; i++)
            {
                ClassicAssert.AreEqual(expected.Cells[i], actual.Cells[i], $"cell {i}");
            }
        }

        /// <summary>
        /// Runs the given action from many threads that start at the same time.
        /// Collects all exceptions instead of stopping at the first one.
        /// </summary>
        private static void RunConcurrently(int threads, Action<int> action)
        {
            using var barrier = new Barrier(threads);
            var exceptions = new List<Exception>();
            var tasks = new Task[threads];
            for (var t = 0; t < threads; t++)
            {
                var id = t;
                tasks[t] = Task.Factory.StartNew(() =>
                {
                    barrier.SignalAndWait();
                    try { action(id); }
                    catch (Exception e) { lock (exceptions) exceptions.Add(e); }
                }, TaskCreationOptions.LongRunning);
            }
            Task.WaitAll(tasks);
            if (exceptions.Count > 0) throw new AggregateException(exceptions);
        }

        [Test]
        public void ParallelQueries_PlainNode_DiskStore()
        {
            var (storage, root, dir) = CreateDiskStoreCloud("test");
            try
            {
                var expected = RunQueries(root);
                ClassicAssert.AreEqual(PointCount, expected.TotalPoints);
                ClassicAssert.IsTrue(expected.Cells.Length > 1);
                ClassicAssert.IsTrue(expected.RayPoints > 0);
                ClassicAssert.IsTrue(expected.NearPoints > 0);

                for (var round = 0; round < Rounds; round++)
                {
                    // fresh cache each round, so that concurrent loads from disk are exercised every time
                    storage.Cache!.Clear();
                    RunConcurrently(Threads, _ => AssertSameSummary(expected, RunQueries(root)));
                }
            }
            finally
            {
                Cleanup(storage, dir);
            }
        }

        [Test]
        public void ParallelQueries_FilteredNode_DiskStore()
        {
            var (storage, root, dir) = CreateDiskStoreCloud("test");
            try
            {
                var box = new Box3d(new V3d(0.1, 0.2, 0.3), new V3d(0.9, 0.7, 0.8));

                // baseline from a private filtered node
                var expected = RunQueries(FilteredNode.CreateTransient(root, new FilterInsideBox3d(box)));
                ClassicAssert.IsTrue(expected.TotalPoints > 0 && expected.TotalPoints < PointCount);

                for (var round = 0; round < Rounds; round++)
                {
                    storage.Cache!.Clear();

                    // one shared filtered node instance, queried from all threads at once
                    var shared = FilteredNode.CreateTransient(root, new FilterInsideBox3d(box));
                    RunConcurrently(Threads, _ =>
                    {
                        var actual = RunQueries(shared);
                        AssertSameSummary(expected, actual);

                        // also exercise the derived-attribute paths on the shared node itself
                        shared.CheckDerivedAttributes();
                        if (shared.TryGetPartIndices(out var qs)) ClassicAssert.AreEqual(shared.PointCountCell, qs.Length);
                    });
                }
            }
            finally
            {
                Cleanup(storage, dir);
            }
        }

        [Test]
        public void ParallelQueries_PlainNode_InMemoryStore()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: new LruDictionary<string, object>(1L << 30));
            var config = ImportConfig.Default
                .WithStorage(storage)
                .WithKey("test")
                .WithOctreeSplitLimit(SplitLimit)
                ;
            var pointset = PointCloud.Chunks(CreateTestChunk(0), config);
            var root = pointset.Root.Value;

            var expected = RunQueries(root);
            for (var round = 0; round < Rounds; round++)
            {
                storage.Cache!.Clear();
                RunConcurrently(Threads, _ => AssertSameSummary(expected, RunQueries(root)));
            }
        }
    }
}
