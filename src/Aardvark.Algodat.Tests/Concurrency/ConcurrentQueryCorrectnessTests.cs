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

        public ConcurrentQueryCorrectnessTests(string kind) { m_kind = kind; }

        private IPointCloudNode Filtered(IPointCloudNode root)
            => FilteredNode.CreateTransient(root, new FilterInsideConvexHull3d(m_plan.FilterHull));

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
                    $"cells {Describe(m_refCells)}, rays {Describe(m_refRays)}, near-point {Describe(m_refNear)}, " +
                    $"filtered cells {Describe(m_refFilteredCells)}, filtered rays {Describe(m_refFilteredRays)}");
            }
        }

        private readonly Dictionary<string, Exception> m_referenceErrors = new Dictionary<string, Exception>();

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

        private static string Describe(Summary[] xs) => xs == null ? "FAILED" : $"{xs.Length} items / {xs.Sum(x => x.Count):N0} pts";

        private Summary[] Require(Summary[] reference, string name)
        {
            if (reference == null) Assert.Inconclusive($"sequential reference '{name}' failed ({Variant}): {m_referenceErrors[name].GetType().Name}: {m_referenceErrors[name].Message}");
            return reference;
        }

        [Test]
        public void SequentialReferences_Succeed()
        {
            if (m_referenceErrors.Count == 0) return;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[{Variant}] {m_referenceErrors.Count} sequential (single-threaded) reference run(s) failed on the {m_kind} store:");
            foreach (var kv in m_referenceErrors) sb.AppendLine($"--- {kv.Key} ---\n{kv.Value}");
            Assert.Fail(sb.ToString());
        }

        #region plain nodes

        [Test]
        public void Cells_TaskPerItem_ColdStore()
        {
            for (var round = 0; round < ColdRounds; round++)
            {
                using var s = Open(m_spec);
                var sw = Stopwatch.StartNew();
                var w = new CellWorkload(s.Root, m_plan);
                var actual = RunOrFail($"cells cold round {round}", w.Count, () => RunTaskPerItem(w.Count, w.Run));
                Log($"[{m_kind}] cells cold round {round}: {w.Count} cells in {sw.Elapsed.TotalSeconds:0.00} s");
                AssertSameResults($"cells cold round {round}", Require(m_refCells, "cells"), actual);
            }
        }

        [Test]
        public void Cells_Threads_WarmStore()
        {
            using var s = Open(m_spec);
            var w = new CellWorkload(s.Root, m_plan);
            AssertSameResults("cells warm-up", Require(m_refCells, "cells"), RunSequential(w.Count, w.Run));
            for (var round = 0; round < WarmRounds; round++)
            {
                var sw = Stopwatch.StartNew();
                var actual = RunOrFail($"cells warm round {round}", w.Count, () => RunThreads(Threads, w.Count, w.Run));
                Log($"[{m_kind}] cells warm round {round}: {w.Count} cells on {Threads} threads in {sw.Elapsed.TotalSeconds:0.00} s");
                AssertSameResults($"cells warm round {round}", Require(m_refCells, "cells"), actual);
            }
        }

        [Test]
        public void Rays_TaskPerItem_ColdStore()
        {
            for (var round = 0; round < ColdRounds; round++)
            {
                using var s = Open(m_spec);
                var sw = Stopwatch.StartNew();
                var w = new RayWorkload(s.Root, m_plan);
                var actual = RunOrFail($"rays cold round {round}", w.Count, () => RunTaskPerItem(w.Count, w.Run));
                Log($"[{m_kind}] rays cold round {round}: {w.Count} rays in {sw.Elapsed.TotalSeconds:0.00} s");
                AssertSameResults($"rays cold round {round}", Require(m_refRays, "rays"), actual);
            }
        }

        [Test]
        public void Rays_Threads_WarmStore()
        {
            using var s = Open(m_spec);
            var w = new RayWorkload(s.Root, m_plan);
            AssertSameResults("rays warm-up", Require(m_refRays, "rays"), RunSequential(w.Count, w.Run));
            for (var round = 0; round < WarmRounds; round++)
            {
                var sw = Stopwatch.StartNew();
                var actual = RunOrFail($"rays warm round {round}", w.Count, () => RunThreads(Threads, w.Count, w.Run));
                Log($"[{m_kind}] rays warm round {round}: {w.Count} rays on {Threads} threads in {sw.Elapsed.TotalSeconds:0.00} s");
                AssertSameResults($"rays warm round {round}", Require(m_refRays, "rays"), actual);
            }
        }

        [Test]
        public void NearPoint_TaskPerItem_ColdStore()
        {
            for (var round = 0; round < ColdRounds; round++)
            {
                using var s = Open(m_spec);
                var sw = Stopwatch.StartNew();
                var w = new NearPointWorkload(s.Root, m_plan);
                var actual = RunOrFail($"near-point cold round {round}", w.Count, () => RunTaskPerItem(w.Count, w.Run));
                Log($"[{m_kind}] near-point cold round {round}: {w.Count} queries in {sw.Elapsed.TotalSeconds:0.00} s");
                AssertSameResults($"near-point cold round {round}", Require(m_refNear, "near-point"), actual);
            }
        }

        [Test]
        public void Mixed_TaskPerItem_ColdStore()
        {
            // cells, rays and near-point queries interleaved on one cold store
            Require(m_refCells, "cells"); Require(m_refRays, "rays"); Require(m_refNear, "near-point");
            for (var round = 0; round < ColdRounds; round++)
            {
                using var s = Open(m_spec);
                var sw = Stopwatch.StartNew();
                var cells = new CellWorkload(s.Root, m_plan);
                var rays = new RayWorkload(s.Root, m_plan);
                var near = new NearPointWorkload(s.Root, m_plan);
                var items = new List<(Func<int, Summary> f, int i, Summary expected)>();
                var max = Math.Max(cells.Count, rays.Count);
                for (var i = 0; i < max; i++)
                {
                    if (i < cells.Count) items.Add((cells.Run, i, m_refCells[i]));
                    if (i < rays.Count) items.Add((rays.Run, i, m_refRays[i]));
                    if (i < near.Count) items.Add((near.Run, i, m_refNear[i]));
                }
                var expected = items.Select(x => x.expected).ToArray();
                var actual = RunOrFail($"mixed cold round {round}", items.Count, () => RunTaskPerItem(items.Count, k => items[k].f(items[k].i)));
                Log($"[{m_kind}] mixed cold round {round}: {items.Count} queries in {sw.Elapsed.TotalSeconds:0.00} s");
                AssertSameResults($"mixed cold round {round}", expected, actual);
            }
        }

        #endregion

        #region shared FilteredNode (Vgm.Api filterByHull / filterByPrism)

        [Test]
        public void FilteredCells_TaskPerItem_ColdStore()
        {
            for (var round = 0; round < ColdRounds; round++)
            {
                using var s = Open(m_spec);
                var sw = Stopwatch.StartNew();
                var w = new CellWorkload(Filtered(s.Root), m_plan);
                var actual = RunOrFail($"filtered cells cold round {round}", w.Count, () => RunTaskPerItem(w.Count, w.Run));
                Log($"[{m_kind}] filtered cells cold round {round}: {w.Count} cells in {sw.Elapsed.TotalSeconds:0.00} s");
                AssertSameResults($"filtered cells cold round {round}", Require(m_refFilteredCells, "filtered cells"), actual);
            }
        }

        [Test]
        public void FilteredCells_Threads_WarmStore()
        {
            using var s = Open(m_spec);
            Warm(s.Root);
            for (var round = 0; round < WarmRounds; round++)
            {
                // a fresh FilteredNode per round, shared by all threads (its lazy state is what is under test)
                var sw = Stopwatch.StartNew();
                var w = new CellWorkload(Filtered(s.Root), m_plan);
                var actual = RunOrFail($"filtered cells warm round {round}", w.Count, () => RunThreads(Threads, w.Count, w.Run));
                Log($"[{m_kind}] filtered cells warm round {round}: {w.Count} cells on {Threads} threads in {sw.Elapsed.TotalSeconds:0.00} s");
                AssertSameResults($"filtered cells warm round {round}", Require(m_refFilteredCells, "filtered cells"), actual);
            }
        }

        [Test]
        public void FilteredRays_TaskPerItem_ColdStore()
        {
            for (var round = 0; round < ColdRounds; round++)
            {
                using var s = Open(m_spec);
                var sw = Stopwatch.StartNew();
                var w = new RayWorkload(Filtered(s.Root), m_plan);
                var actual = RunOrFail($"filtered rays cold round {round}", w.Count, () => RunTaskPerItem(w.Count, w.Run));
                Log($"[{m_kind}] filtered rays cold round {round}: {w.Count} rays in {sw.Elapsed.TotalSeconds:0.00} s");
                AssertSameResults($"filtered rays cold round {round}", Require(m_refFilteredRays, "filtered rays"), actual);
            }
        }

        [Test]
        public void FilteredRays_Threads_WarmStore()
        {
            using var s = Open(m_spec);
            Warm(s.Root);
            for (var round = 0; round < WarmRounds; round++)
            {
                var sw = Stopwatch.StartNew();
                var w = new RayWorkload(Filtered(s.Root), m_plan);
                var actual = RunOrFail($"filtered rays warm round {round}", w.Count, () => RunThreads(Threads, w.Count, w.Run));
                Log($"[{m_kind}] filtered rays warm round {round}: {w.Count} rays on {Threads} threads in {sw.Elapsed.TotalSeconds:0.00} s");
                AssertSameResults($"filtered rays warm round {round}", Require(m_refFilteredRays, "filtered rays"), actual);
            }
        }

        #endregion

        #region side effects

        [Test]
        public void Queries_DoNotWriteToStore()
        {
            using var s = Open(m_spec, countAccesses: true);
            var cells = new CellWorkload(s.Root, m_plan);
            var rays = new RayWorkload(s.Root, m_plan);
            RunOrFail("no-write cells", cells.Count, () => RunTaskPerItem(cells.Count, cells.Run));
            RunOrFail("no-write rays", rays.Count, () => RunTaskPerItem(rays.Count, rays.Run));
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
            Log($"[{m_kind}] chunk aliasing: {leaking.Count} of {tested} mutated result chunks changed the data returned by the next query of the same cell");
            Assert.That(leaking, Is.Empty,
                $"[{Variant}] mutating a result chunk changed the data returned by the next query of the same cell for {leaking.Count} of {tested} cells (e.g. cells {string.Join(", ", leaking.Take(5))})");
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
            Log($"[{m_kind}]   kd-tree: null {kdTreeNull:N0}, blob missing in store {kdTreeBlobMissing:N0}, load failed {kdTreeLoadFailed:N0}" +
                (kdErrors.Count > 0 ? " (" + string.Join("; ", kdErrors.Select(kv => $"{kv.Value}x {kv.Key}")) + ")" : ""));
            Log($"[{m_kind}]   store gets while loading all nodes: {s.Counters.Gets:N0}, store writes attempted: {s.Counters.Adds:N0} ({sw.Elapsed.TotalSeconds:0.0} s)");

            // kd-tree blob diagnostics for the first leaf
            IPointCloudNode FirstLeaf(IPointCloudNode n) => n.IsLeaf ? n : FirstLeaf(n.Subnodes.First(c => c != null).Value);
            var leaf = FirstLeaf(root);
            if (leaf.Properties.TryGetValue(Durable.Octree.PointRkdTreeFDataReference, out var kdRef))
            {
                var blob = s.Storage.GetByteArray((Guid)kdRef);
                var head = blob != null && blob.Length >= 16 ? new Guid(blob.Take(16).ToArray()).ToString() : "n/a";
                string decoded;
                try
                {
                    var d = s.Storage.GetPointRkdTreeFData(((Guid)kdRef).ToString());
                    decoded = d == null ? "null" : $"PermArray {(d.PermArray == null ? "null" : d.PermArray.Length.ToString())}, AxisArray {(d.AxisArray == null ? "null" : d.AxisArray.Length.ToString())}, RadiusArray {(d.RadiusArray == null ? "null" : d.RadiusArray.Length.ToString())}";
                }
                catch (Exception e) { decoded = $"decode failed: {e.GetType().Name}: {e.Message}"; }
                var ascii = blob == null ? "" : new string(blob.Select(b => b >= 32 && b < 127 ? (char)b : '.').ToArray());
                Log($"[{m_kind}]   first leaf {leaf.Id}: {leaf.PointCountCell} points, kd-tree blob {kdRef}: {blob?.Length ?? -1} bytes, first 16 bytes as guid {head}, decoded: {decoded}");
                Log($"[{m_kind}]   kd-tree blob as text: {ascii}");
            }
            Log($"[{m_kind}]   plan: {m_plan}");
        }
    }
}
