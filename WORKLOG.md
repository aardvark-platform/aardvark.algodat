# WORKLOG: concurrency hazards in the point query path

Branch: `concurrency-hazards` (aardvark.algodat). Started 2026-09-21.

## Context

Vgm.Api (`C:\repo\Vgm.Api-master`, F#) exposes point queries (`CellQuery.enumerateCells`,
`queryPointsNearRay`, box/plane/hull queries) that forward directly into
`Aardvark.Geometry.Points.Queries` in this repo. The goal is to allow concurrent calls on
those endpoints. This log tracks the fixes that make the query path safe to share, so
parallelization can be evaluated afterwards.

### Findings (analysis, 2026-09-21)

The query functions themselves are pure over immutable `Chunk` objects. Shared state lives
below them:

- `LruDictionary` (Aardvark.Data.Points.Base) locks every lookup/insert. Safe.
- `Uncodium.SimpleStore` 3.0.30 `SimpleDiskStore`: one global `lock (m_lock)` around every
  operation, including the byte copy out of the memory-mapped file. Safe for concurrent
  reads; serializes on cache misses (throughput concern for later, not a correctness issue).
- `PointRkdTreeF`: per-call query object, no mutable instance state after construction. Safe.

Real hazards:

1. **Store write during decode.** `PointSetNode` constructor (`Octrees/PointSetNode.cs`
   ~line 325) computes a kd-tree and calls `storage.Add` when a node has positions but no
   kd-tree. `READONLY` is not defined anywhere, and `Decode` constructs with
   `writeToStore: false`, so the reference is never persisted: every load of such a node
   writes a new orphan blob. Under concurrency, two decodes of the same node both write.
2. **FilteredNode lazy caches are unsynchronized.** `Views/FilteredNode.cs`: subnode array
   (`m_subnodes_cache`), untyped `Dictionary m_cache`, `m_ensuredPositionsAndDerived`,
   `_subsetIndexArray`. Filtered nodes are placed in the shared LRU by `GetPointCloudNode`
   and handed to callers by Vgm.Api's prism/hull filters, so two threads can race on one
   node and corrupt the dictionary.
3. **Chunks hand out cached arrays by reference.** `ToChunk` (`Octrees/IPointCloudNodeExtensions.cs`
   lines ~900/913) and `QueriesOctreeLevels.cs` ~line 223 pass the node's colors / normals /
   intensities / classifications arrays straight into `Chunk`. `Chunk` exposes them as
   `IList<T>` over a raw array, so an indexer write lands in the LRU cache and corrupts every
   later reader. Positions are already copied.
4. Minor: `PointSetNode.m_centroid` / `m_centroidStdDev` are nullable structs memoized
   without synchronization (torn read possible); `LruDictionary.Add` on an existing key
   updates size but not value.

## Plan

| Step | What | Status |
|------|------|--------|
| 0 | Concurrency characterization test (parallel cell / ray / near-point queries on plain and filtered nodes; assert same results as sequential) | done |
| 1 | Never write to the store during decode; build missing kd-trees lazily in memory instead | done |
| 2 | Make `FilteredNode` lazy state thread-safe (`Lazy<T>` with ExecutionAndPublication) | done |
| 3 | Copy attribute arrays in `ToChunk` / octree-level query so chunks never alias cached arrays | done |
| 4 | Small cleanups: centroid memo, `LruDictionary.Add` value update | done |

After these, parallelization of the Vgm.Api endpoints is re-evaluated; the remaining
serialization point is the SimpleDiskStore lock on cache misses.

## Log

- 2026-09-21: branch created, plan committed.
- 2026-09-21: **Step 0 done.** Added `src/Aardvark.Algodat.Tests/ConcurrencyTests.cs` with three
  tests: parallel queries (cell enumeration at exponent -2, near-ray, near-point, inside-box)
  from 16 threads over 8 rounds, with the LRU cache cleared each round so node/attribute loads
  happen concurrently. Variants: plain node on disk store, plain node on in-memory store,
  shared `FilteredNode` on disk store. Result before fixes: both plain-node tests pass;
  the filtered-node test fails (6 of 128 runs returned 0 points instead of 10053), which is
  hazard 2: `FilteredNode.Subnodes` publishes `m_subnodes_cache` before filling it, so a
  second thread sees an all-null subnode array.
- 2026-09-21: **Step 1 done.** `Octrees/PointSetNode.cs`: the constructor's kd-tree block now
  branches on `writeToStore`. Write path (import, merge, `WriteToStore`): unchanged, computes
  and persists the kd-tree. Read path (`Decode`, i.e. `writeToStore: false`): no store write;
  a `Lazy<PointRkdTreeF>` (ExecutionAndPublication) is registered as an in-memory
  `PersistentRef` under the kd-tree key, so `HasKdTree`/`KdTree` keep working and the tree is
  built at most once per node instance. `HasKdTree` now also reports in-memory trees;
  `KdTreeId` stays null for them. The `#if !READONLY` guards were removed (the symbol was
  never defined). The DEBUG leaf invariant `KdTreeId == null` became `!HasKdTree`.
  Extracted `ComputeKdTree` from `ComputeAndStoreKdTree`.
  New tests in `ReadPathStoreWriteTests.cs`: decoding a node stored without kd-tree does
  not add anything to the store (counting `Storage` wrapper), near-point and line-segment
  queries stay correct, concurrent queries build exactly one tree, and the write path still
  persists a kd-tree. Full suite: 447 passed, 2 skipped, 1 failed (the expected filtered-node
  concurrency test).
- 2026-09-21: **Step 2 done.** `Views/FilteredNode.cs` rewritten without the untyped
  `Dictionary m_cache`, the `m_ensuredPositionsAndDerived` flag, `m_subnodes_cache` and
  `_subsetIndexArray`. Every derived value is now its own `Lazy<T>` initialized in the
  constructor with `LazyThreadSafetyMode.ExecutionAndPublication`: subnodes, subset index
  array, positions, absolute positions, exact local bbox, kd-tree, colors, normals,
  intensities, classifications, per-point part indices, part index range. Semantics are
  unchanged with one deliberate improvement: the kd-tree is no longer built as a side effect
  of touching positions, only when `KdTree` is accessed. `GetSubArray` became the pure
  `SubsetOf`. The filtered-node concurrency test now passes (32 tests in
  ConcurrencyTests, Views*, ReadPathStoreWriteTests green). Full suite afterwards: 448 passed,
  2 skipped, 0 failed.
- 2026-09-21: **Step 3 done.** New internal helper `OwnedAttributes` (in
  `Octrees/IPointCloudNodeExtensions.cs`) copies attribute arrays and per-point part indices.
  `ToChunk()` now copies colors / normals / intensities / classifications / part indices;
  `ToChunk(fromRelativeDepth)` and the bounded `QueryPointsInOctreeLevel` delegate to it; the
  unbounded `QueryPointsInOctreeLevel` copies after its length check. Positions were already
  fresh for `PointSetNode`; `FilteredNode.PositionsAbsolute` no longer memoizes and returns a
  new array per call, matching `PointSetNode`. Sites that already copied (near-ray, near-point,
  line-segment, custom-attribute queries via `Subset`) are unchanged.
  New `ChunkOwnershipTests.cs`: scribbling into chunks from ToChunk, ToChunk(0), both
  octree-level queries, inside-box, Collect and cell enumeration must not change node data
  or later queries, for plain and partially filtered leaves. Verified the tests fail against
  the pre-fix library (3/3 fail) and pass with it.
- 2026-09-21: **Step 4 done.** `PointSetNode`: the two nullable-struct memos `m_centroid` /
  `m_centroidStdDev` became one immutable `CentroidInfo` reference published atomically
  (`GetCentroidInfo`); per-field preference for stored values is preserved.
  `LruDictionary.Add` on an existing key now updates value and onRemove (not only size);
  `Count` reads under the lock. Two new LruDictionary tests. Full suite: 453 passed,
  2 skipped, 0 failed.

## State after the fixes

Query path shared state, as of the end of step 4:

| Component | Status |
|-----------|--------|
| Query functions (`Queries*.cs`) | pure, allocate per call |
| `Chunk` handed to callers | owns all arrays (step 3) |
| `PointSetNode` | immutable `Data`; `PersistentRefs` written only in ctor; centroid memo atomic (step 4); no store writes on read (step 1) |
| `FilteredNode` | all derived state `Lazy<T>` ExecutionAndPublication (step 2) |
| `LruDictionary` | locked per operation |
| `SimpleDiskStore` | one global lock per operation, including the copy out of the mmap |

Remaining serialization point for concurrent endpoint calls: the SimpleDiskStore lock on LRU
cache misses. Candidates for the parallelization pass: copy outside the store lock (or a
reader/writer lock in SimpleDiskStore), and reducing decode fan-out in the `PointSetNode`
constructor (it eagerly loads positions for the length consistency check and recursively
loads children when a stored inner node lacks `BoundingBoxExactGlobal`).

Not touched: Vgm.Api (no in-place writes found there; its wrappers are thin).

## Parallelization evaluation (2026-09-21, after the fixes)

### Harness

`src/Aardvark.Algodat.Tests/ParallelQueryBenchmark.cs` (`[Explicit]`, run with
`dotnet test -c Release --filter FullyQualifiedName~ParallelQueryBenchmark --logger "console;verbosity=normal"`).
Imports 1M random points with colors (585 nodes, split limit 8192) into a SimpleDiskStore in
the temp folder, reopens it with a fresh 1 GB LRU, and runs a mixed workload per thread
(300 ops; each op = one near-ray query, one 0.1³ inside-box query, one cell query at
exponent -3), from 1, 2, 4, 8, 16 threads, cold (LRU cleared before the run) and warm.
Best of 3. The underlying `Storage.f_get` is wrapped to count calls, bytes and time spent in
the store (i.e. under the SimpleDiskStore lock, waiting included). Machine: Ryzen 7 7700,
8 cores / 16 threads. Note: "cold" means LRU-cold; the 43 MB store file stays in the OS file
cache, so this does not measure disk I/O.

### Baseline (after steps 0-4, before the changes below)

| threads | mode | ops/s | speedup | store gets | store MB | store share of thread time |
|--------:|------|------:|--------:|-----------:|---------:|---------------------------:|
| 1 | cold | 1 419 | 1.00 | 1 824 | 42.7 | 29 % |
| 1 | warm | 3 244 | 1.00 | 0 | 0 | 0 % |
| 4 | cold | 6 748 | 4.76 | 2 102 | 50.6 | 16 % |
| 4 | warm | 9 307 | 2.87 | 0 | 0 | 0 % |
| 8 | cold | 11 533 | 8.13 | 2 439 | 65.3 | 15 % |
| 8 | warm | 15 099 | 4.66 | 0 | 0 | 0 % |
| 16 | cold | 16 716 | 11.78 | 3 024 | 88.0 | 11 % |
| 16 | warm | 19 693 | 6.07 | 0 | 0 | 0 % |

Reading: (a) warm scaling reaches ~6x on 8 physical cores; (b) cold "speedup" > thread
count is an artifact (the decode work is shared by all threads, while ops scale with
threads); (c) at 16 threads the store is read twice as much as at 1 thread (3 024 vs 1 824
gets, 88 vs 43 MB): concurrent misses on the same key each load and decode it; (d) the
store lock accounts for 11-29 % of thread time, falling with thread count.

### Experiment 1: lock-free reads in `LruDictionary`

Hypothesis: every `PersistentRef.Value` takes the LRU lock, limiting warm scaling.
Change: `m_k2e` is a `ConcurrentDictionary`; `TryGetValue`/`ContainsKey`/`Count` no longer
lock; mutations still lock (`m_lock`). Result: no measurable change (16 threads warm
19.7k -> 20.4k ops/s, within noise). Hypothesis rejected; kept the change because it is
strictly less contention for free.

### Experiment 2: server GC

`DOTNET_gcServer=1`: 16 threads warm 19.7k -> 24.1k ops/s (+22 %), 8 threads warm
15.1k -> 16.9k. The workload allocates heavily (chunk copies, filtered lists), so the
API host process should run with server GC (`<ServerGarbageCollection>true</ServerGarbageCollection>`
or `DOTNET_gcServer=1`). Together with 8 physical cores, warm scaling (9.1x at 16 threads,
6.4x at 8) is close to the hardware limit: the warm path is CPU-bound, not lock-bound.

### Experiment 3: in-flight deduplication of cache misses

Change: `LruDictionary.TryGetOrAdd(key, create, out value)` runs `create` once per key even
under concurrent misses (in-flight `ConcurrentDictionary<K, Lazy<...>>`, re-check inside
the factory, callers unregister only their own entry). `StorageExtensions` load functions
(`GetV3fArray`, `GetIntArray`, `GetInt16Array`, `GetC4bArray`, both kd-tree data loaders,
`GetPointSet`, `GetPointCloudNode`) go through a common `GetOrLoad` that uses it.
Side effects: nodes parsed by `ObsoleteNodeParser` are now cached like all other nodes;
classifications (`Classifications1bReference`) are now cached (`GetByteArrayCached`),
previously every access re-read them from the store.
Result: store gets are constant at 1 824 for every thread count (was 3 024 at 16), bytes
constant at 42.7 MB (was 88), store share of thread time at 16 threads 11 % -> 4 %.
Test: `ConcurrencyTests.ParallelColdLoads_LoadEachKeyOnce` asserts no key is loaded twice
under 16 concurrent cold threads.

### Experiment 4: reader/writer lock in SimpleDiskStore (prototype, separate repo)

Repo `C:\repo\Uncodium.SimpleStore`, branch `concurrent-reads` (from main at 3.0.30+2).
`SimpleDiskStore.cs`: the single `lock (m_lock)` became a `ReaderWriterLockSlim`
(recursive, because Add -> EnsureSpaceFor -> ReOpenMemoryMappedFile nest). `Contains`,
`GetSize`, `Get`, `GetSlice`, `GetStream` take the shared lock via `ReadLockWithOpenMmf()`,
which first re-opens the mapping under the exclusive lock if a previous resize failed (a
shared lock cannot be upgraded); `List` takes the plain shared lock (index only, must work
while the file is closed on disk-full). `Add`, `AddStream`, `Remove`, `Flush`, `Dispose`,
`EnsureSpaceFor`, `ReOpenMemoryMappedFile` take the exclusive lock. The in-memory index is
plain dictionaries, safe for concurrent readers when writers are excluded;
`MemoryMappedViewAccessor.ReadArray` is safe for concurrent reads.
Tests: existing suite 74 passed / 10 skipped (Azure) / 0 failed on net10.0. New
`ConcurrentReadTests`: 8 readers verify values while a writer appends 96 MB and forces
resizes; and `ReadersOverlap`: 8 readers of a 64 MB blob take 0.245 s vs 0.171 s for one
reader (serialized would be ~1.37 s).
Effect on the algodat benchmark (DLL swapped into bin/Release for the run): none beyond
noise, because after experiment 3 the store is only 4-6 % of thread time and the file is in
the OS cache. The lock matters when reads are disk-bound (page faults inside the lock
serialize I/O) or blobs are large; the micro-test shows that case. Not consumed by algodat
yet (paket pins 3.0.30); adopting it needs a SimpleStore release.

### Table after experiments 1, 3, 4 (same harness, same machine, workstation GC)

| threads | mode | ops/s | speedup | store gets | store MB | store share |
|--------:|------|------:|--------:|-----------:|---------:|------------:|
| 1 | cold | 1 493 | 1.00 | 1 824 | 42.7 | 37 % |
| 1 | warm | 3 217 | 1.00 | 0 | 0 | 0 % |
| 4 | cold | 7 818 | 5.24 | 1 824 | 42.7 | 19 % |
| 4 | warm | 10 384 | 3.23 | 0 | 0 | 0 % |
| 8 | cold | 8 987 | 6.02 | 1 824 | 42.7 | 12 % |
| 8 | warm | 14 796 | 4.60 | 0 | 0 | 0 % |
| 16 | cold | 17 376 | 11.64 | 1 824 | 42.7 | 6 % |
| 16 | warm | 20 341 | 6.32 | 0 | 0 | 0 % |

### Conclusions for Vgm.Api

1. Concurrent calls to the query endpoints are safe now (steps 1-4) and need no lock in
   Vgm.Api; its wrappers are stateless forwards.
2. Throughput scales with physical cores on the warm path; run the host with server GC
   (+20 % at 16 threads here) and let the thread pool size the parallelism.
3. Cold loads no longer multiply with the number of concurrent callers (experiment 3);
   each blob is read and decoded once per LRU lifetime.
4. The SimpleDiskStore lock is the last serialization point, relevant for disk-bound or
   large-blob reads; the prototype on `concurrent-reads` removes it for readers.
5. Not measured here and left as candidates: the eager `Positions.Value` load in the
   `PointSetNode` constructor (one extra blob per decoded inner node), and the recursive
   child load when a stored inner node lacks `BoundingBoxExactGlobal` (old stores only).
   The benchmark's cold path is decode-bound (~70 % of thread time outside the store at
   1 thread), so decode cost, not locking, is where further cold-path gains would come from.

Files (this repo): `Aardvark.Data.Points.Base/LruDictionary.cs` (lock-free reads,
`TryGetOrAdd`), `Aardvark.Geometry.PointSet/Utils/StorageExtensions.cs` (`GetOrLoad`,
`GetByteArrayCached`, `LoadPointCloudNode`), `Octrees/PointSetNode.cs` (cached
classifications), tests `ConcurrencyTests.cs`, `ParallelQueryBenchmark.cs`.
