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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Uncodium.SimpleStore;

#pragma warning disable CS8632 // nullable annotations in a non-nullable context (test project has nullable disabled)

namespace Aardvark.Geometry.Tests.Concurrency
{
    /// <summary>
    /// Shared infrastructure for the concurrent query correctness and performance suites.
    ///
    /// The suites reproduce the access pattern of Vgm.Api consumers:
    /// (1) enumerate cells sequentially, then process every cell on its own task (F# Async.Parallel), and
    /// (2) run one near-ray query per task and merge the result chunks (Chunk.ImmutableMerge).
    ///
    /// Everything here uses only API that exists on master and on the concurrency-hazards branch,
    /// so the same files compile and run against both.
    ///
    /// Stores:
    /// - synthetic: generated into %TEMP%\algodat-concurrent-query-tests (reused across runs),
    /// - real: directory from env ALGODAT_REAL_STORE (default D:\bla\stores\lowergetikum_keller),
    ///   opened as read-only snapshot. The real store is never written to.
    ///
    /// Env ALGODAT_VARIANT labels the code under test in reports (e.g. "master", "concurrency-hazards").
    /// </summary>
    internal static class Harness
    {
        public const string RealStoreEnvVar = "ALGODAT_REAL_STORE";
        public const string DefaultRealStorePath = @"D:\bla\stores\lowergetikum_keller";

        static Harness()
        {
            // Stored kd-trees are coded with Aardvark's BinaryCoder, which resolves the type by name
            // via the TypeInfo registry. Applications fill it with Aardvark.Init(); that crashes under
            // the test host (see PolyMesh/PointClustering.cs), so register the types by hand.
            // Without this, kd-tree blobs decode to null and every kd-tree query throws NullReferenceException.
            Aardvark.Base.Coder.TypeInfo.Add(typeof(PointRkdTreeFData));
            Aardvark.Base.Coder.TypeInfo.Add(typeof(PointRkdTreeDData));
        }

        public static string Variant
            => Environment.GetEnvironmentVariable("ALGODAT_VARIANT") is string s && s.Length > 0 ? s : "unspecified";

        public static void Log(string s) => TestContext.Progress.WriteLine(s);

        #region Store specs

        public sealed class StoreSpec
        {
            public string Name;
            public string Path;
            public string RootKey;
            public bool ReadOnly;
            public bool IsReal;
            public override string ToString() => $"{Name} ({Path})";
        }

        public static StoreSpec RealStore()
        {
            var p = Environment.GetEnvironmentVariable(RealStoreEnvVar);
            if (string.IsNullOrWhiteSpace(p)) p = DefaultRealStorePath;
            if (!Directory.Exists(p)) return null;
            var keyFile = Path.Combine(p, "key.txt");
            if (!File.Exists(keyFile)) return null;
            return new StoreSpec
            {
                Name = "real",
                Path = p,
                RootKey = File.ReadAllText(keyFile).Trim(),
                ReadOnly = true,
                IsReal = true,
            };
        }

        /// <summary>
        /// Creates (or reuses) a synthetic scan-like store: walls, floor and ceiling of a room
        /// with colors, normals, intensities and classifications.
        /// </summary>
        public static StoreSpec SyntheticStore(int pointCount, int splitLimit, int seed = 0)
        {
            var dir = Path.Combine(Path.GetTempPath(), "algodat-concurrent-query-tests", $"synthetic-{pointCount}-{splitLimit}-{seed}");
            var keyFile = Path.Combine(dir, "key.txt");
            if (!File.Exists(keyFile))
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
                if (File.Exists(dir)) File.Delete(dir);
                Directory.CreateDirectory(dir);
                var key = Guid.NewGuid().ToString();
                // same layout as a Vgm.Api store: a directory containing data.bin (SimpleDiskStore v3)
                var storage = new SimpleDiskStore(Path.Combine(dir, "data.bin"), s_noLog).ToPointCloudStore(new LruDictionary<string, object>(1L << 30));
                try
                {
                    var config = ImportConfig.Default
                        .WithStorage(storage)
                        .WithKey(key)
                        .WithOctreeSplitLimit(splitLimit)
                        .WithVerbose(false)
                        ;
                    var sw = Stopwatch.StartNew();
                    var ps = PointCloud.Chunks(CreateRoomChunk(pointCount, seed), config);
                    storage.Flush();
                    Log($"[harness] built synthetic store {dir}: {ps.PointCount:N0} points, {sw.Elapsed.TotalSeconds:0.0} s");
                }
                finally
                {
                    storage.Dispose();
                }
                File.WriteAllText(keyFile, key);
            }
            return new StoreSpec
            {
                Name = "synthetic",
                Path = dir,
                RootKey = File.ReadAllText(keyFile).Trim(),
                ReadOnly = false,
                IsReal = false,
            };
        }

        private static Chunk CreateRoomChunk(int n, int seed)
        {
            // room 20 x 12 x 3 m, points on 6 faces plus a few boxes inside, small noise
            var r = new Random(seed);
            var room = new Box3d(new V3d(0, 0, 0), new V3d(20, 12, 3));
            var boxes = new[]
            {
                new Box3d(new V3d(2, 2, 0), new V3d(5, 4, 1.2)),
                new Box3d(new V3d(12, 7, 0), new V3d(16, 9, 2.0)),
                new Box3d(new V3d(8, 1, 0), new V3d(9, 2, 2.8)),
            };
            var ps = new V3d[n];
            var ns = new V3f[n];
            var cs = new C4b[n];
            var js = new int[n];
            var ks = new byte[n];
            for (var i = 0; i < n; i++)
            {
                var box = (i % 10 < 7) ? room : boxes[i % boxes.Length];
                var inward = box == room;
                var face = r.Next(6);
                var axis = face / 2;
                var side = face % 2;
                var p = new V3d(
                    box.Min.X + r.NextDouble() * box.SizeX,
                    box.Min.Y + r.NextDouble() * box.SizeY,
                    box.Min.Z + r.NextDouble() * box.SizeZ
                    );
                var normal = V3d.Zero;
                normal[axis] = side == 0 ? -1 : +1;
                p[axis] = side == 0 ? box.Min[axis] : box.Max[axis];
                if (inward) normal = -normal;
                p += new V3d(r.NextDouble() - 0.5, r.NextDouble() - 0.5, r.NextDouble() - 0.5) * 0.004;

                ps[i] = p;
                ns[i] = (V3f)normal;
                cs[i] = new C4b((byte)(p.X * 12), (byte)(p.Y * 20), (byte)(p.Z * 80), (byte)255);
                js[i] = (int)(1000 + 500 * Math.Abs(normal.Z) + r.Next(50));
                ks[i] = (byte)(axis == 2 ? (side == 0 ? 2 : 3) : 6);
            }
            var chunk = new Chunk(ps, cs, ns, js, ks, null, null, null);
            if (ImportConfig.Default.ParseConfig.EnabledProperties.PartIndices) chunk = chunk.WithPartIndices(42u, null);
            return chunk;
        }

        #endregion

        #region Opening stores

        public sealed class StoreCounters
        {
            public long Gets;
            public long GetSlices;
            public long Adds;
            public long Removes;
            public readonly List<string> AddedKeys = new List<string>();
        }

        public sealed class OpenedStore : IDisposable
        {
            public StoreSpec Spec;
            public Storage Storage;
            public IPointCloudNode Root;
            public StoreCounters Counters;
            public void Dispose() => Storage.Dispose();
        }

        private static readonly Action<string[]> s_noLog = _ => { };

        /// <summary>
        /// Opens the store with an empty LRU cache (cold). If <paramref name="countAccesses"/> is set,
        /// the returned Storage counts gets and adds; adds are counted but NOT forwarded to the
        /// underlying store (so the real store is never modified, and read-only snapshots do not throw).
        /// </summary>
        public static OpenedStore Open(StoreSpec spec, bool countAccesses = false, long cacheSize = 2L << 30)
        {
            ISimpleStore simple = spec.ReadOnly
                ? SimpleDiskStore.OpenReadOnlySnapshot(spec.Path, s_noLog)
                : new SimpleDiskStore(spec.Path, s_noLog);
            var cache = new LruDictionary<string, object>(cacheSize);
            var inner = simple.ToPointCloudStore(cache);

            Storage storage = inner;
            StoreCounters counters = null;
            if (countAccesses)
            {
                var c = new StoreCounters();
                storage = new Storage(
                    add: (key, value, encode) =>
                    {
                        Interlocked.Increment(ref c.Adds);
                        lock (c.AddedKeys) c.AddedKeys.Add(key);
                        // dropped on purpose: queries must not write; if they do, we only record it
                    },
                    get: key => { Interlocked.Increment(ref c.Gets); return inner.f_get(key); },
                    getSlice: (key, offset, length) => { Interlocked.Increment(ref c.GetSlices); return inner.f_getSlice(key, offset, length); },
                    remove: key => { Interlocked.Increment(ref c.Removes); },
                    dispose: inner.Dispose,
                    flush: inner.Flush,
                    cache: cache
                    );
                counters = c;
            }

            var root = ResolveRoot(storage, spec.RootKey);
            return new OpenedStore { Spec = spec, Storage = storage, Root = root, Counters = counters };
        }

        private static IPointCloudNode ResolveRoot(Storage storage, string key)
        {
            PointSet ps = null;
            try { ps = storage.GetPointSet(key); } catch { }
            if (ps != null && ps.Root != null) return ps.Root.Value;
            return storage.GetPointCloudNode(key);
        }

        #endregion

        #region Summaries (order independent, exact)

        public readonly record struct Summary(long Count, long X, long Y, long Z, long Color, long Intensity)
        {
            public static Summary operator +(Summary a, Summary b)
                => new Summary(a.Count + b.Count, a.X + b.X, a.Y + b.Y, a.Z + b.Z, a.Color + b.Color, a.Intensity + b.Intensity);
            public static readonly Summary Zero = new Summary(0, 0, 0, 0, 0, 0);
            public override string ToString() => $"n={Count} x={X} y={Y} z={Z} c={Color} i={Intensity}";
        }

        private static long Q(double v) => (long)Math.Round(v * 1000.0);

        public static Summary Summarize(Chunk chunk)
        {
            if (chunk == null || chunk.IsEmpty) return Summary.Zero;
            long x = 0, y = 0, z = 0, c = 0, j = 0;
            var ps = chunk.Positions;
            for (var i = 0; i < ps.Count; i++) { var p = ps[i]; x += Q(p.X); y += Q(p.Y); z += Q(p.Z); }
            if (chunk.HasColors) { var cs = chunk.Colors; for (var i = 0; i < cs.Count; i++) { var k = cs[i]; c += k.R + k.G + k.B; } }
            if (chunk.HasIntensities) { var js = chunk.Intensities; for (var i = 0; i < js.Count; i++) j += js[i]; }
            return new Summary(chunk.Count, x, y, z, c, j);
        }

        public static Summary Summarize(IEnumerable<Chunk> chunks)
        {
            var s = Summary.Zero;
            foreach (var c in chunks) s += Summarize(c);
            return s;
        }

        public static Summary Summarize(PointsNearObject<V3d> r)
        {
            if (r == null || r.IsEmpty) return Summary.Zero;
            long x = 0, y = 0, z = 0, c = 0, j = 0;
            for (var i = 0; i < r.Positions.Length; i++) { var p = r.Positions[i]; x += Q(p.X); y += Q(p.Y); z += Q(p.Z); }
            if (r.Colors != null) foreach (var k in r.Colors) c += k.R + k.G + k.B;
            if (r.Intensities != null) foreach (var v in r.Intensities) j += v;
            return new Summary(r.Count, x, y, z, c, j);
        }

        #endregion

        #region Workload plan

        /// <summary>
        /// Deterministic query parameters derived from the cloud (identical for every code variant).
        /// </summary>
        public sealed class Plan
        {
            public int CellExponent;
            public int CellCount;
            public int FromRelativeDepth;
            public Ray3d[] Rays;
            public double RayMaxDistance;
            public double RayTMin;
            public double RayTMax;
            public double NearPointMaxDistance;
            public int NearPointMaxCount;
            public Hull3d FilterHull;
            public Box3d BoundingBox;
            public long PointCount;
            public override string ToString()
                => $"cells: exponent {CellExponent} ({CellCount} cells, fromRelativeDepth {FromRelativeDepth}); " +
                   $"rays: {Rays.Length} (maxDist {RayMaxDistance:0.####}, t in [{RayTMin:0.##},{RayTMax:0.##}]); " +
                   $"near-point: maxDist {NearPointMaxDistance:0.####}, maxCount {NearPointMaxCount}";
        }

        public static Plan CreatePlan(IPointCloudNode root, int rayCount, int minCells, int maxCells, int seed = 1)
        {
            var bb = root.BoundingBoxExactGlobal;
            var diag = bb.Size.Length;

            // largest exponent that yields at least minCells non-empty cells (capped by maxCells)
            var e = root.Cell.Exponent;
            var count = 0;
            for (var i = 0; i < 16; i++)
            {
                count = root.EnumerateCells(e).Count();
                if (count >= minCells || count > maxCells) break;
                e--;
            }
            while (count > maxCells) { e++; count = root.EnumerateCells(e).Count(); }

            // sample points (and normals, if present) from a coarse octree level for rays
            var r = new Random(seed);
            var samples = new List<(V3d p, V3d n)>();
            var level = 0;
            while (level < 12)
            {
                samples.Clear();
                foreach (var chunk in root.QueryPointsInOctreeLevel(level))
                {
                    for (var i = 0; i < chunk.Count; i++)
                    {
                        var n = chunk.HasNormals ? (V3d)chunk.Normals[i] : V3d.Zero;
                        samples.Add((chunk.Positions[i], n));
                    }
                }
                if (samples.Count >= rayCount * 4) break;
                level++;
            }
            if (samples.Count == 0) throw new InvalidOperationException("Cloud has no points.");

            var rays = new Ray3d[rayCount];
            for (var i = 0; i < rayCount; i++)
            {
                var (p, n) = samples[r.Next(samples.Count)];
                if (n == V3d.Zero || !n.Length.ApproximateEquals(1.0, 1e-3))
                {
                    n = new V3d(r.NextDouble() - 0.5, r.NextDouble() - 0.5, r.NextDouble() - 0.5).Normalized;
                }
                rays[i] = new Ray3d(p, n);
            }

            var inner = new Box3d(bb.Min + bb.Size * 0.2, bb.Max - bb.Size * 0.2);

            return new Plan
            {
                CellExponent = e,
                CellCount = count,
                FromRelativeDepth = 0,
                Rays = rays,
                RayMaxDistance = diag * 0.002,
                RayTMin = -diag * 0.05,
                RayTMax = diag * 0.05,
                NearPointMaxDistance = diag * 0.01,
                NearPointMaxCount = 2000,
                FilterHull = new Hull3d(inner),
                BoundingBox = bb,
                PointCount = root.PointCountTree,
            };
        }

        #endregion

        #region Workloads (mirror the Vgm.Api consumer snippets)

        /// <summary>
        /// Snippet 1: enumerate cells (sequential), then GetPoints per cell (parallel).
        /// </summary>
        public sealed class CellWorkload
        {
            public readonly Queries.CellQueryResult[] Cells;
            public readonly int FromRelativeDepth;
            public CellWorkload(IPointCloudNode root, Plan plan)
            {
                Cells = root.EnumerateCells(plan.CellExponent).ToArray();
                FromRelativeDepth = plan.FromRelativeDepth;
            }
            public int Count => Cells.Length;
            public Summary Run(int i) => Summarize(Cells[i].GetPoints(FromRelativeDepth));
        }

        /// <summary>
        /// Snippet 2: QueryPointsNearRay with range per ray (parallel), merged via Chunk.ImmutableMerge.
        /// </summary>
        public sealed class RayWorkload
        {
            private readonly IPointCloudNode m_root;
            private readonly Plan m_plan;
            public RayWorkload(IPointCloudNode root, Plan plan) { m_root = root; m_plan = plan; }
            public int Count => m_plan.Rays.Length;
            public Summary Run(int i)
            {
                var chunks = m_root.QueryPointsNearRay(m_plan.Rays[i], m_plan.RayMaxDistance, m_plan.RayTMin, m_plan.RayTMax);
                var merged = Chunk.ImmutableMerge(chunks);
                return Summarize(merged);
            }
        }

        public sealed class NearPointWorkload
        {
            private readonly IPointCloudNode m_root;
            private readonly Plan m_plan;
            public NearPointWorkload(IPointCloudNode root, Plan plan) { m_root = root; m_plan = plan; }
            public int Count => m_plan.Rays.Length;
            public Summary Run(int i)
                => Summarize(m_root.QueryPointsNearPoint(m_plan.Rays[i].Origin, m_plan.NearPointMaxDistance, m_plan.NearPointMaxCount));
        }

        #endregion

        #region Runners

        public static Summary[] RunSequential(int n, Func<int, Summary> f)
        {
            var result = new Summary[n];
            for (var i = 0; i < n; i++) result[i] = f(i);
            return result;
        }

        /// <summary>
        /// One Task per item, all started at once (what F# Async.Parallel does).
        /// Throws AggregateException with every failure.
        /// </summary>
        public static Summary[] RunTaskPerItem(int n, Func<int, Summary> f)
        {
            var tasks = new Task<Summary>[n];
            for (var i = 0; i < n; i++) { var j = i; tasks[i] = Task.Run(() => f(j)); }
            Task.WaitAll(tasks);
            return tasks.Map(t => t.Result);
        }

        /// <summary>
        /// Fixed number of dedicated threads, started simultaneously (barrier), items striped over threads.
        /// Throws AggregateException with every failure.
        /// </summary>
        public static Summary[] RunThreads(int threads, int n, Func<int, Summary> f)
        {
            var result = new Summary[n];
            var errors = new List<Exception>();
            using (var barrier = new Barrier(threads))
            {
                var ts = new Thread[threads];
                for (var t = 0; t < threads; t++)
                {
                    var tid = t;
                    ts[t] = new Thread(() =>
                    {
                        barrier.SignalAndWait();
                        for (var i = tid; i < n; i += threads)
                        {
                            try { result[i] = f(i); }
                            catch (Exception e) { lock (errors) errors.Add(e); }
                        }
                    });
                    ts[t].IsBackground = true;
                }
                foreach (var t in ts) t.Start();
                foreach (var t in ts) t.Join();
            }
            if (errors.Count > 0) throw new AggregateException(errors);
            return result;
        }

        /// <summary>
        /// Parallel.For with bounded degree of parallelism (for scaling measurements).
        /// </summary>
        public static Summary[] RunDegree(int degree, int n, Func<int, Summary> f)
        {
            if (degree == 1) return RunSequential(n, f);
            var result = new Summary[n];
            Parallel.For(0, n, new ParallelOptions { MaxDegreeOfParallelism = degree }, i => result[i] = f(i));
            return result;
        }

        #endregion

        #region Assertions / reporting

        public static string Describe(AggregateException ae, int n)
        {
            var all = ae.Flatten().InnerExceptions;
            var groups = all
                .GroupBy(e => $"{e.GetType().Name}: {Shorten(e.Message)}")
                .OrderByDescending(g => g.Count())
                .ToArray();
            var sb = new StringBuilder();
            sb.AppendLine($"{all.Count} of {n} items failed with {groups.Length} distinct error(s):");
            foreach (var g in groups.Take(8))
            {
                sb.AppendLine($"  [{g.Count()}x] {g.Key}");
                var st = g.First().StackTrace ?? "";
                foreach (var line in st.Split('\n').Take(6)) sb.AppendLine("        " + line.Trim());
            }
            return sb.ToString();
        }

        private static string Shorten(string s)
        {
            s = (s ?? "").Replace("\r", " ").Replace("\n", " ");
            return s.Length > 240 ? s.Substring(0, 240) + "…" : s;
        }

        /// <summary>
        /// Runs <paramref name="run"/>; converts an AggregateException into a readable Assert.Fail.
        /// </summary>
        public static Summary[] RunOrFail(string what, int n, Func<Summary[]> run)
        {
            try
            {
                return run();
            }
            catch (AggregateException ae)
            {
                Assert.Fail($"[{what}] exceptions during concurrent queries ({Variant}):\n{Describe(ae, n)}");
                return null;
            }
        }

        public static void AssertSameResults(string what, Summary[] expected, Summary[] actual)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length), what);
            var mismatches = new List<string>();
            for (var i = 0; i < expected.Length; i++)
            {
                if (expected[i] != actual[i]) mismatches.Add($"  item {i}: expected {expected[i]}, got {actual[i]}");
            }
            if (mismatches.Count > 0)
            {
                Assert.Fail($"[{what}] {mismatches.Count} of {expected.Length} results differ from the sequential run ({Variant}):\n" +
                    string.Join("\n", mismatches.Take(10)));
            }
        }

        public static void Warm(IPointCloudNode root)
        {
            // touch every node and attribute once
            void Visit(IPointCloudNode n)
            {
                if (n.HasPositions) _ = n.Positions.Value;
                if (n.HasColors) _ = n.Colors.Value;
                if (n.HasNormals) _ = n.Normals.Value;
                if (n.HasIntensities) _ = n.Intensities.Value;
                if (n.HasClassifications) _ = n.Classifications.Value;
                if (n.HasKdTree) _ = n.KdTree.Value;
                if (n.Subnodes == null) return;
                foreach (var s in n.Subnodes) if (s != null) Visit(s.Value);
            }
            Visit(root);
        }

        #endregion
    }
}
