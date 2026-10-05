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
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Uncodium.SimpleStore;
using static Aardvark.Geometry.Tests.Concurrency.Harness;

#pragma warning disable CS8632

namespace Aardvark.Geometry.Tests.Concurrency
{
    /// <summary>
    /// Throughput of concurrent point queries (same workloads as the correctness suite) for
    /// 1..16 parallel workers, on a cold store (LRU empty, file in OS cache) and on a warm store,
    /// plus raw SimpleDiskStore read throughput to isolate the store lock.
    ///
    /// Results are printed as markdown and appended to the file given by env ALGODAT_PERF_RESULTS
    /// (default %TEMP%\algodat-perf\results.md). Label the code under test with env ALGODAT_VARIANT.
    ///
    ///   dotnet test src/Aardvark.Algodat.Tests -c Release --filter FullyQualifiedName~ConcurrentQueryPerformanceTests --logger "console;verbosity=normal"
    /// </summary>
    [TestFixture("synthetic")]
    [TestFixture("real")]
    [Explicit("performance")]
    [Category("Performance")]
    [NonParallelizable]
    public class ConcurrentQueryPerformanceTests
    {
        private const int SyntheticPointCount = 2_000_000;
        private const int SyntheticSplitLimit = 8192;
        private const int RayCount = 2000;
        private const int Repeats = 2;
        private static readonly int[] Degrees = { 1, 2, 4, 8, 16 };

        private readonly string m_kind;
        private StoreSpec m_spec;
        private Plan m_plan;
        private readonly StringBuilder m_report = new StringBuilder();

        public ConcurrentQueryPerformanceTests(string kind) { m_kind = kind; }

        [OneTimeSetUp]
        public void Setup()
        {
            m_spec = m_kind == "real" ? RealStore() : SyntheticStore(SyntheticPointCount, SyntheticSplitLimit);
            if (m_spec == null) Assert.Ignore($"real store not found (set {RealStoreEnvVar}, default {DefaultRealStorePath})");
            using var s = Open(m_spec);
            m_plan = CreatePlan(s.Root, RayCount, minCells: 1000, maxCells: 8000);
            Report($"## {m_kind} store, variant `{Variant}`, {DateTime.Now:yyyy-MM-dd HH:mm}");
            Report("");
            Report($"- store: `{m_spec.Path}`, {m_plan.PointCount:N0} points, root {s.Root.Cell}");
            Report($"- plan: {m_plan}");
            Report($"- machine: {Environment.ProcessorCount} logical cores, server GC: {System.Runtime.GCSettings.IsServerGC}, {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
            Report("");
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            if (m_report.Length == 0) return;
            var file = Environment.GetEnvironmentVariable("ALGODAT_PERF_RESULTS");
            if (string.IsNullOrWhiteSpace(file)) file = Path.Combine(Path.GetTempPath(), "algodat-perf", "results.md");
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.AppendAllText(file, m_report.ToString() + Environment.NewLine);
            Log($"[perf] results appended to {file}");
        }

        private void Report(string line)
        {
            m_report.AppendLine(line);
            Log(line);
        }

        private double Measure(Action a)
        {
            var sw = Stopwatch.StartNew();
            a();
            return sw.Elapsed.TotalSeconds;
        }

        private delegate Summary[] Runner(int n, Func<int, Summary> f);

        private IEnumerable<(string name, Runner run)> Runners()
        {
            foreach (var d in Degrees)
            {
                var degree = d;
                yield return ($"{degree}", (n, f) => RunDegree(degree, n, f));
            }
            yield return ("task/item", (n, f) => RunTaskPerItem(n, f));
        }

        [Test]
        public void QueryThroughput()
        {
            Report("### query throughput (items per second, best of " + Repeats + ")");
            Report("");
            Report("| workload | state | " + string.Join(" | ", Runners().Select(r => r.name)) + " |");
            Report("|---|---|" + string.Concat(Runners().Select(_ => "---:|")));

            foreach (var workload in new[] { "cells", "rays" })
            {
                foreach (var state in new[] { "cold", "warm" })
                {
                    var cells = new List<string>();
                    foreach (var (name, run) in Runners())
                    {
                        var best = double.MaxValue;
                        var count = 0;
                        for (var rep = 0; rep < Repeats; rep++)
                        {
                            using var s = Open(m_spec);
                            if (state == "warm") Warm(s.Root);
                            Func<int, Summary> f;
                            if (workload == "cells") { var w = new CellWorkload(s.Root, m_plan); count = w.Count; f = w.Run; }
                            else { var w = new RayWorkload(s.Root, m_plan); count = w.Count; f = w.Run; }
                            var t = Measure(() => run(count, f));
                            best = Math.Min(best, t);
                        }
                        cells.Add($"{count / best:N0}");
                        Log($"[perf] {m_kind} {workload} {state} {name}: {count} items, {best:0.000} s, {count / best:N0} items/s");
                    }
                    Report($"| {workload} | {state} | " + string.Join(" | ", cells) + " |");
                }
            }
            Report("");
        }

        [Test]
        public void StoreReadThroughput()
        {
            // raw SimpleDiskStore.Get of every key, independent of algodat: isolates the store's lock
            using var store = SimpleDiskStore.OpenReadOnlySnapshot(m_spec.Path, _ => { });
            var keys = store.List().Select(x => x.key).ToArray();
            long totalBytes = 0;
            foreach (var k in keys) totalBytes += store.Get(k)?.Length ?? 0; // warm OS cache

            Report($"### raw store reads ({keys.Length:N0} keys, {totalBytes / (1024.0 * 1024.0):N0} MB)");
            Report("");
            Report("| threads | seconds | MB/s | speedup |");
            Report("|---:|---:|---:|---:|");
            double t1 = 0;
            foreach (var threads in Degrees)
            {
                var best = double.MaxValue;
                for (var rep = 0; rep < Repeats; rep++)
                {
                    var t = Measure(() =>
                    {
                        using var barrier = new Barrier(threads);
                        var ts = Enumerable.Range(0, threads).Select(tid => Task.Factory.StartNew(() =>
                        {
                            barrier.SignalAndWait();
                            long sum = 0;
                            for (var i = tid; i < keys.Length; i += threads) sum += store.Get(keys[i])?.Length ?? 0;
                            return sum;
                        }, TaskCreationOptions.LongRunning)).ToArray();
                        Task.WaitAll(ts);
                    });
                    best = Math.Min(best, t);
                }
                if (threads == 1) t1 = best;
                Report($"| {threads} | {best:0.000} | {totalBytes / best / (1024.0 * 1024.0):N0} | {t1 / best:0.00}x |");
            }
            Report("");
        }
    }
}
