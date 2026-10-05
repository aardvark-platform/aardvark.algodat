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
using Aardvark.Data;
using Aardvark.Data.Points;
using Aardvark.Geometry.Points;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using static Aardvark.Geometry.Tests.Concurrency.Harness;

#pragma warning disable CS8632

namespace Aardvark.Geometry.Tests.Concurrency
{
    /// <summary>
    /// Correctness of concurrent point queries: reproduces the Vgm.Api consumer access patterns
    /// (one task per cell / per ray, F# Async.Parallel style) and checks that
    ///   (a) no exceptions occur and
    ///   (b) every result equals the sequential result,
    /// on a cold store (all loads happen concurrently) and on a warm store (pure query path),
    /// for plain nodes and for a shared FilteredNode (Vgm.Api filterByHull / filterByPrism).
    ///
    /// Every test runs all of its rounds and reports the accumulated exceptions (grouped) and
    /// result mismatches at the end, so a stress run (env ALGODAT_ROUNDS=10) shows the full picture.
    ///
    /// Runs against the synthetic store and, if present, the real store
    /// (env ALGODAT_REAL_STORE, default D:\bla\stores\lowergetikum_keller; read-only).
    ///
    ///   dotnet test src/Aardvark.Algodat.Tests -c Release --filter FullyQualifiedName~ConcurrentQueryCorrectnessTests
    /// </summary>
    [TestFixture("synthetic")]
    [TestFixture("real")]
    [NonParallelizable]
    public class ConcurrentQueryCorrectnessTests
    {
        private const int SyntheticPointCount = 400_000;
        private const int SyntheticSplitLimit = 1024;
        private const int RayCount = 400;
        private const int Threads = 32;

        // env ALGODAT_ROUNDS scales the number of repetitions (default 1: 3 cold rounds, 5 warm rounds)
        private static readonly int RoundScale = int.TryParse(Environment.GetEnvironmentVariable("ALGODAT_ROUNDS"), out var r) && r > 0 ? r : 1;
        private static readonly int ColdRounds = 3 * RoundScale;
        private static readonly int WarmRounds = 5 * RoundScale;

        private readonly string m_kind;
        private StoreSpec m_spec;
        private Plan m_plan;

        private Summary[] m_refCells;
        private Summary[] m_refRays;
        private Summary[] m_refNear;
        private Summary[] m_refFilteredCells;
        private Summary[] m_refFilteredRays;
        private readonly Dictionary<string, Exception> m_referenceErrors = new Dictionary<string, Exception>();

        public ConcurrentQueryCorrectnessTests(string kind) { m_kind = kind; }

        private IPointCloudNode Filtered(IPointCloudNode root)
            => FilteredNode.CreateTransient(root, new FilterInsideConvexHull3d(m_plan.FilterHull));

        #region setup

        [OneTimeSetUp]
        public void Setup()
        {
            m_spec = m_kind == "real" ? RealStore() : SyntheticStore(SyntheticPointCount, SyntheticSplitLimit);
            if (m_spec == null) Assert.Ignore($"real store not found (set {RealStoreEnvVar}, default {DefaultRealStorePath})");

            var sw = Stopwatch.StartNew();
            using (var s = Open(m_spec))
            {
                m_plan = CreatePlan(s.Root, RayCount, minCells: 300, maxCells: 3000);
                Log($"[{m_kind}] variant={Variant} points={m_plan.PointCount:N0} bbox={m_plan.BoundingBox} root={s.Root.Cell}");
                Log($"[{m_kind}] plan: {m_plan}");

                var root = s.Root;
                m_refCells = Reference("cells", () => { var w = new CellWorkload(root, m_plan); return RunSequential(w.Count, w.Run); });
                m_refRays = Reference("rays", () => { var w = new RayWorkload(root, m_plan); return RunSequential(w.Count, w.Run); });
                m_refNear = Reference("near-point", () => { var w = new NearPointWorkload(root, m_plan); return RunSequential(w.Count, w.Run); });
                var filtered = Filtered(root);
                m_refFilteredCells = Reference("filtered cells", () => { var w = new CellWorkload(filtered, m_plan); return RunSequential(w.Count, w.Run); });
                m_refFilteredRays = Reference("filtered rays", () => { var w = new RayWorkload(filtered, m_plan); return RunSequential(w.Count, w.Run); });

                Log($"[{m_kind}] sequential references computed in {sw.Elapsed.TotalSeconds:0.0} s: " +
                    $"cells {DescribeRef(m_refCells)}, rays {DescribeRef(m_refRays)}, near-point {DescribeRef(m_refNear)}, " +
                    $"filtered cells {DescribeRef(m_refFilteredCells)}, filtered rays {DescribeRef(m_refFilteredRays)}");
            }
        }

        /// <summary>
        /// Computes a sequential reference; a failure here is a finding of its own (see SequentialReferences_Succeed)
        /// and makes the dependent concurrency tests inconclusive instead of failing the whole fixture.
        /// </summary>
        private Summary[] Reference(string name, Func<Summary[]> compute)
        {
            try { return compute(); }
            catch (Exception e)
            {
                Log($"[{m_kind}] sequential reference '{name}' FAILED ({Variant}): {e.GetType().Name}: {e.Message}");
                m_referenceErrors[name] = e;
                return null;
            }
        }

        private static string DescribeRef(Summary[] xs) => xs == null ? "FAILED" : $"{xs.Length} items / {xs.Sum(x => x.Count):N0} pts";

        private Summary[] Require(Summary[] reference, string name)
        {
            if (reference == null) Assert.Inconclusive($"sequential reference '{name}' failed ({Variant}): {m_referenceErrors[name].GetType().Name}: {m_referenceErrors[name].Message}");
            return reference;
        }

        #endregion

        #region rounds (accumulate, report at the end)

        private sealed class Outcome
        {
            public readonly string Name;
            public int Rounds;
            public int RoundsWithExceptions;
            public int RoundsWithMismatches;
            public int Items;
            public int FailedItems;
            public int MismatchedItems;
            public readonly Dictionary<string, (int count, string stack)> Exceptions = new Dictionary<string, (int, string)>();
            public readonly List<string> MismatchSamples = new List<string>();
            public double Seconds;
            public Outcome(string name) { Name = name; }
        }

        /// <summary>
        /// Runs one concurrent round, compares with the sequential reference and records
        /// exceptions and mismatches instead of asserting immediately.
        /// </summary>
        private void Round(Outcome o, int n, Func<Summary[]> run, Summary[] expected)
        {
            o.Rounds++;
            o.Items += n;
            var sw = Stopwatch.StartNew();
            Summary[] actual = null;
            try
            {
                actual = run();
            }
            catch (AggregateException ae)
            {
                var all = ae.Flatten().InnerExceptions;
                o.RoundsWithExceptions++;
                o.FailedItems += all.Count;
                foreach (var e in all)
                {
                    var key = $"{e.GetType().Name}: {Shorten(e.Message)}";
                    if (o.Exceptions.TryGetValue(key, out var x)) o.Exceptions[key] = (x.count + 1, x.stack);
                    else o.Exceptions[key] = (1, e.StackTrace ?? "");
                }
            }
            o.Seconds += sw.Elapsed.TotalSeconds;
            if (actual == null) return;

            var mismatches = 0;
            for (var i = 0; i < expected.Length; i++)
            {
                if (expected[i] == actual[i]) continue;
                mismatches++;
                if (o.MismatchSamples.Count < 5) o.MismatchSamples.Add($"round {o.Rounds - 1} item {i}: expected {expected[i]}, got {actual[i]}");
            }
            if (mismatches > 0) { o.RoundsWithMismatches++; o.MismatchedItems += mismatches; }
        }

        private static string Shorten(string s)
        {
            s = (s ?? "").Replace("\r", " ").Replace("\n", " ");
            return s.Length > 240 ? s.Substring(0, 240) + "…" : s;
        }

        private void Finish(Outcome o)
        {
            Log($"[{m_kind}] {o.Name} ({Variant}): {o.Rounds} rounds, {o.Items} items, {o.Seconds:0.00} s; " +
                $"exceptions: {o.FailedItems} items in {o.RoundsWithExceptions} rounds; mismatches: {o.MismatchedItems} items in {o.RoundsWithMismatches} rounds");
            if (o.FailedItems == 0 && o.MismatchedItems == 0) return;

            var sb = new StringBuilder();
            sb.AppendLine($"[{Variant}] {o.Name} on the {m_kind} store: {o.Rounds} rounds, {o.Items} items.");
            if (o.FailedItems > 0)
            {
                sb.AppendLine($"EXCEPTIONS: {o.FailedItems} items failed in {o.RoundsWithExceptions} of {o.Rounds} rounds, {o.Exceptions.Count} distinct:");
                foreach (var kv in o.Exceptions.OrderByDescending(kv => kv.Value.count).Take(8))
                {
                    sb.AppendLine($"  [{kv.Value.count}x] {kv.Key}");
                    foreach (var line in kv.Value.stack.Split('\n').Take(6)) sb.AppendLine("        " + line.Trim());
                }
            }
            if (o.MismatchedItems > 0)
            {
                sb.AppendLine($"WRONG RESULTS: {o.MismatchedItems} items differ from the sequential run in {o.RoundsWithMismatches} of {o.Rounds} rounds, e.g.");
                foreach (var m in o.MismatchSamples) sb.AppendLine("  " + m);
            }
            Assert.Fail(sb.ToString());
        }

        #endregion

        #region plain nodes

        [Test]
        public void Cells_TaskPerItem_ColdStore()
        {
            var expected = Require(m_refCells, "cells");
            var o = new Outcome("cells, task per cell, cold store");
            for (var round = 0; round < ColdRounds; round++)
            {
                using var s = Open(m_spec);
                var w = new CellWorkload(s.Root, m_plan);
                Round(o, w.Count, () => RunTaskPerItem(w.Count, w.Run), expected);
            }
            Finish(o);
        }

        [Test]
        public void Cells_Threads_WarmStore()
        {
            var expected = Require(m_refCells, "cells");
            var o = new Outcome($"cells, {Threads} threads, warm store");
            using var s = Open(m_spec);
            var w = new CellWorkload(s.Root, m_plan);
            Round(o, w.Count, () => RunSequential(w.Count, w.Run), expected); // warm-up
            for (var round = 0; round < WarmRounds; round++) Round(o, w.Count, () => RunThreads(Threads, w.Count, w.Run), expected);
            Finish(o);
        }

        [Test]
        public void Rays_TaskPerItem_ColdStore()
        {
            var expected = Require(m_refRays, "rays");
            var o = new Outcome("rays, task per ray, cold store");
            for (var round = 0; round < ColdRounds; round++)
            {
                using var s = Open(m_spec);
                var w = new RayWorkload(s.Root, m_plan);
                Round(o, w.Count, () => RunTaskPerItem(w.Count, w.Run), expected);
            }
            Finish(o);
        }

        [Test]
        public void Rays_Threads_ColdStore()
        {
            // all threads start at the root of an empty cache: maximum contention on the same loads
            var expected = Require(m_refRays, "rays");
            var o = new Outcome($"rays, {Threads} threads, cold store");
            for (var round = 0; round < ColdRounds; round++)
            {
                using var s = Open(m_spec);
                var w = new RayWorkload(s.Root, m_plan);
                Round(o, w.Count, () => RunThreads(Threads, w.Count, w.Run), expected);
            }
            Finish(o);
        }

        [Test]
        public void Rays_Threads_WarmStore()
        {
            var expected = Require(m_refRays, "rays");
            var o = new Outcome($"rays, {Threads} threads, warm store");
            using var s = Open(m_spec);
            var w = new RayWorkload(s.Root, m_plan);
            Round(o, w.Count, () => RunSequential(w.Count, w.Run), expected); // warm-up
            for (var round = 0; round < WarmRounds; round++) Round(o, w.Count, () => RunThreads(Threads, w.Count, w.Run), expected);
            Finish(o);
        }

        [Test]
        public void NearPoint_TaskPerItem_ColdStore()
        {
            var expected = Require(m_refNear, "near-point");
            var o = new Outcome("near-point, task per query, cold store");
            for (var round = 0; round < ColdRounds; round++)
            {
                using var s = Open(m_spec);
                var w = new NearPointWorkload(s.Root, m_plan);
                Round(o, w.Count, () => RunTaskPerItem(w.Count, w.Run), expected);
            }
            Finish(o);
        }

        [Test]
        public void Mixed_TaskPerItem_ColdStore()
        {
            // cells, rays and near-point queries interleaved on one cold store
            var refCells = Require(m_refCells, "cells");
            var refRays = Require(m_refRays, "rays");
            var refNear = Require(m_refNear, "near-point");
            var o = new Outcome("mixed cells + rays + near-point, task per query, cold store");
            for (var round = 0; round < ColdRounds; round++)
            {
                using var s = Open(m_spec);
                var cells = new CellWorkload(s.Root, m_plan);
                var rays = new RayWorkload(s.Root, m_plan);
                var near = new NearPointWorkload(s.Root, m_plan);
                var items = new List<(Func<int, Summary> f, int i, Summary expected)>();
                var max = Math.Max(cells.Count, rays.Count);
                for (var i = 0; i < max; i++)
                {
                    if (i < cells.Count) items.Add((cells.Run, i, refCells[i]));
                    if (i < rays.Count) items.Add((rays.Run, i, refRays[i]));
                    if (i < near.Count) items.Add((near.Run, i, refNear[i]));
                }
                var expected = items.Select(x => x.expected).ToArray();
                Round(o, items.Count, () => RunTaskPerItem(items.Count, k => items[k].f(items[k].i)), expected);
            }
            Finish(o);
        }

        #endregion

        #region shared FilteredNode (Vgm.Api filterByHull / filterByPrism)

        [Test]
        public void FilteredCells_TaskPerItem_ColdStore()
        {
            var expected = Require(m_refFilteredCells, "filtered cells");
            var o = new Outcome("filtered cells, task per cell, cold store");
            for (var round = 0; round < ColdRounds; round++)
            {
                using var s = Open(m_spec);
                var w = new CellWorkload(Filtered(s.Root), m_plan);
                Round(o, w.Count, () => RunTaskPerItem(w.Count, w.Run), expected);
            }
            Finish(o);
        }

        [Test]
        public void FilteredCells_Threads_WarmStore()
        {
            var expected = Require(m_refFilteredCells, "filtered cells");
            var o = new Outcome($"filtered cells, {Threads} threads, warm store");
            using var s = Open(m_spec);
            Warm(s.Root);
            for (var round = 0; round < WarmRounds; round++)
            {
                // a fresh FilteredNode per round, shared by all threads (its lazy state is what is under test)
                var w = new CellWorkload(Filtered(s.Root), m_plan);
                Round(o, w.Count, () => RunThreads(Threads, w.Count, w.Run), expected);
            }
            Finish(o);
        }

        [Test]
        public void FilteredRays_TaskPerItem_ColdStore()
        {
            var expected = Require(m_refFilteredRays, "filtered rays");
            var o = new Outcome("filtered rays, task per ray, cold store");
            for (var round = 0; round < ColdRounds; round++)
            {
                using var s = Open(m_spec);
                var w = new RayWorkload(Filtered(s.Root), m_plan);
                Round(o, w.Count, () => RunTaskPerItem(w.Count, w.Run), expected);
            }
            Finish(o);
        }

        [Test]
        public void FilteredRays_Threads_WarmStore()
        {
            var expected = Require(m_refFilteredRays, "filtered rays");
            var o = new Outcome($"filtered rays, {Threads} threads, warm store");
            using var s = Open(m_spec);
            Warm(s.Root);
            for (var round = 0; round < WarmRounds; round++)
            {
                var w = new RayWorkload(Filtered(s.Root), m_plan);
                Round(o, w.Count, () => RunThreads(Threads, w.Count, w.Run), expected);
            }
            Finish(o);
        }

        #endregion

        #region side effects

        [Test]
        public void Queries_DoNotWriteToStore()
        {
            using var s = Open(m_spec, countAccesses: true);
            var cells = new CellWorkload(s.Root, m_plan);
            var rays = new RayWorkload(s.Root, m_plan);
            var o = new Outcome("no-write check (cells + rays, task per item, cold store)");
            Round(o, cells.Count, () => RunTaskPerItem(cells.Count, cells.Run), Require(m_refCells, "cells"));
            Round(o, rays.Count, () => RunTaskPerItem(rays.Count, rays.Run), Require(m_refRays, "rays"));
            Warm(s.Root);
            Log($"[{m_kind}] store accesses during read-only use: gets={s.Counters.Gets:N0} adds={s.Counters.Adds:N0} removes={s.Counters.Removes:N0}");
            Assert.That(s.Counters.Adds, Is.EqualTo(0),
                $"[{Variant}] {s.Counters.Adds} store write(s) were attempted while only reading (first keys: {string.Join(", ", s.Counters.AddedKeys.Take(3))})");
        }

        [Test]
        public void ChunkMutation_DoesNotLeakIntoCache()
        {
            // Chunks returned by queries must not alias the arrays held in the node cache,
            // otherwise a consumer mutating its result corrupts every other reader.
            using var s = Open(m_spec);
            var cells = new CellWorkload(s.Root, m_plan);
            var tested = 0;
            var leaking = new List<int>();
            for (var i = 0; i < cells.Count; i++)
            {
                var chunk = cells.Cells[i].GetPoints(m_plan.FromRelativeDepth).FirstOrDefault(c => c.Count > 0 && (c.HasColors || c.HasIntensities));
                if (chunk == null) continue;
                var before = Summarize(cells.Cells[i].GetPoints(m_plan.FromRelativeDepth));

                try
                {
                    if (chunk.HasColors) chunk.Colors[0] = new C4b(chunk.Colors[0].R == 0 ? 255 : 0, 7, 7);
                    else chunk.Intensities[0] = chunk.Intensities[0] + 123456;
                }
                catch (NotSupportedException)
                {
                    continue; // read-only result: fine
                }
                tested++;

                var after = Summarize(cells.Cells[i].GetPoints(m_plan.FromRelativeDepth));
                if (after != before) leaking.Add(i);
            }
            if (tested == 0) Assert.Inconclusive("no cell with colors or intensities found");
            Log($"[{m_kind}] chunk aliasing ({Variant}): {leaking.Count} of {tested} mutated result chunks changed the data returned by the next query of the same cell");
            Assert.That(leaking, Is.Empty,
                $"[{Variant}] mutating a result chunk changed the data returned by the next query of the same cell for {leaking.Count} of {tested} cells (e.g. cells {string.Join(", ", leaking.Take(5))})");
        }

        [Test]
        public void SequentialReferences_Succeed()
        {
            if (m_referenceErrors.Count == 0) return;
            var sb = new StringBuilder();
            sb.AppendLine($"[{Variant}] {m_referenceErrors.Count} sequential (single-threaded) reference run(s) failed on the {m_kind} store:");
            foreach (var kv in m_referenceErrors) sb.AppendLine($"--- {kv.Key} ---\n{kv.Value}");
            Assert.Fail(sb.ToString());
        }

        #endregion

        [Test]
        public void Inventory()
        {
            using var s = Open(m_spec, countAccesses: true);
            var root = s.Root;
            long nodes = 0, leaves = 0, maxDepth = 0, withKdTreeRef = 0, leavesWithoutKdTreeRef = 0, kdTreeNull = 0, kdTreeBlobMissing = 0, kdTreeLoadFailed = 0;
            var attributes = new SortedSet<string>();
            var kdErrors = new Dictionary<string, int>();
            void Visit(IPointCloudNode n, int depth)
            {
                nodes++;
                maxDepth = Math.Max(maxDepth, depth);
                var hasKdRef = n.Has(Durable.Octree.PointRkdTreeFDataReference) || n.Has(Durable.Octree.PointRkdTreeDDataReference);
                if (hasKdRef) withKdTreeRef++;
                if (n.HasPositions)
                {
                    if (n.KdTree == null) kdTreeNull++;
                    else
                    {
                        try
                        {
                            var kd = n.KdTree.Value;
                            if (kd == null) kdTreeNull++;
                            else
                            {
                                // probe: a nearest-point query at the node's own first point must succeed
                                var p = n.Positions.Value.Length > 0 ? n.Positions.Value[0] : V3f.Zero;
                                kd.GetClosest(p, 0.01f, 1);
                            }
                        }
                        catch (Exception e)
                        {
                            kdTreeLoadFailed++;
                            var k = $"{e.GetType().Name}: {e.Message} @ {(e.StackTrace ?? "").Split('\n').FirstOrDefault()?.Trim()}";
                            kdErrors[k] = kdErrors.TryGetValue(k, out var c) ? c + 1 : 1;
                        }
                    }
                    if (n.Properties.TryGetValue(Durable.Octree.PointRkdTreeFDataReference, out var kdId) && s.Storage.GetByteArray((Guid)kdId) == null) kdTreeBlobMissing++;
                }
                if (n.HasColors) attributes.Add("colors");
                if (n.HasNormals) attributes.Add("normals");
                if (n.HasIntensities) attributes.Add("intensities");
                if (n.HasClassifications) attributes.Add("classifications");
                if (n.HasPartIndices) attributes.Add("partIndices");
                if (n.IsLeaf) { leaves++; if (!hasKdRef) leavesWithoutKdTreeRef++; }
                else foreach (var c in n.Subnodes) if (c != null) Visit(c.Value, depth + 1);
            }
            var sw = Stopwatch.StartNew();
            Visit(root, 0);
            Log($"[{m_kind}] inventory ({Variant}): {m_spec}");
            Log($"[{m_kind}]   points {root.PointCountTree:N0}, nodes {nodes:N0}, leaves {leaves:N0}, depth {maxDepth}, root cell {root.Cell}, bbox {root.BoundingBoxExactGlobal}");
            Log($"[{m_kind}]   attributes: {string.Join(", ", attributes)}; nodes with kd-tree ref: {withKdTreeRef:N0}; leaves without kd-tree ref: {leavesWithoutKdTreeRef:N0}");
            Log($"[{m_kind}]   kd-tree: null {kdTreeNull:N0}, blob missing in store {kdTreeBlobMissing:N0}, load/query failed {kdTreeLoadFailed:N0}" +
                (kdErrors.Count > 0 ? " (" + string.Join("; ", kdErrors.Select(kv => $"{kv.Value}x {kv.Key}")) + ")" : ""));
            Log($"[{m_kind}]   store gets while loading all nodes: {s.Counters.Gets:N0}, store writes attempted: {s.Counters.Adds:N0} ({sw.Elapsed.TotalSeconds:0.0} s)");

            // kd-tree blob diagnostics for the first leaf
            IPointCloudNode FirstLeaf(IPointCloudNode n) => n.IsLeaf ? n : FirstLeaf(n.Subnodes.First(c => c != null).Value);
            var leaf = FirstLeaf(root);
            if (leaf.Properties.TryGetValue(Durable.Octree.PointRkdTreeFDataReference, out var kdRef))
            {
                var blob = s.Storage.GetByteArray((Guid)kdRef);
                string decoded;
                try
                {
                    var d = s.Storage.GetPointRkdTreeFData(((Guid)kdRef).ToString());
                    decoded = d == null ? "null" : $"PermArray {(d.PermArray == null ? "null" : d.PermArray.Length.ToString())}, AxisArray {(d.AxisArray == null ? "null" : d.AxisArray.Length.ToString())}, RadiusArray {(d.RadiusArray == null ? "null" : d.RadiusArray.Length.ToString())}";
                }
                catch (Exception e) { decoded = $"decode failed: {e.GetType().Name}: {e.Message}"; }
                var ascii = blob == null ? "" : new string(blob.Select(b => b >= 32 && b < 127 ? (char)b : '.').ToArray());
                Log($"[{m_kind}]   first leaf {leaf.Id}: {leaf.PointCountCell} points, kd-tree blob {kdRef}: {blob?.Length ?? -1} bytes, decoded: {decoded}");
                Log($"[{m_kind}]   kd-tree blob as text: {ascii}");
            }
            Log($"[{m_kind}]   plan: {m_plan}");
        }
    }
}
