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
| 1 | Never write to the store during decode; build missing kd-trees lazily in memory instead | todo |
| 2 | Make `FilteredNode` lazy state thread-safe (`Lazy<T>` with ExecutionAndPublication) | todo |
| 3 | Copy attribute arrays in `ToChunk` / octree-level query so chunks never alias cached arrays | todo |
| 4 | Small cleanups: centroid memo, `LruDictionary.Add` value update | todo |

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
