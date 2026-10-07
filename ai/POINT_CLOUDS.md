# Point Clouds

`PointSet` is a stored octree; `IPointCloudNode` exposes its nodes. [PointSet source](../src/Aardvark.Geometry.PointSet) contains octree operations, spatial queries, LOD generation, and filtered views.

## Create, Store, and Load

This in-memory example creates a small cloud. For file input, see [Importers](IMPORTERS.md).

```csharp
using System;
using Aardvark.Base;
using Aardvark.Data.Points;
using Aardvark.Geometry.Points;

using var storage = PointCloud.CreateInMemoryStore();
var chunk = new Chunk(new[] { V3d.Zero, V3d.IOO, V3d.OIO });
var config = ImportConfig.Default.WithStorage(storage).WithKey("example")
    .WithEnabledPartIndices(false); // This hand-built chunk has no part indices.
var pointSet = PointCloud.Import(chunk, config);

Console.WriteLine($"{pointSet.PointCount} points in {pointSet.Bounds}");
var loaded = PointCloud.Load("example", storage);
```

For persistent storage, use `PointCloud.OpenStore(path)` instead. Keep the store alive while using its clouds and lazy references; flush pending writes with `storage.Flush()` and dispose it when finished. An in-memory store still retains all stored data: a smaller cache does not make it out-of-core.

`PointCloud.Load(key, storage)` throws if the key is absent. [Storage](../src/Aardvark.Data.Points.Base/Storage.cs) also supports custom backends through read, write, slice, remove, flush, and dispose delegates.

## Chunks

[Chunk](../src/Aardvark.Data.Points.Base/Chunk.cs) is the common streaming representation:

| Member | Type |
|--------|------|
| `Positions` | `IList<V3d>`; global coordinates |
| `Colors` | `IList<C4b>` or null |
| `Normals` | `IList<V3f>` or null |
| `Intensities` | `IList<int>` or null |
| `Classifications` | `IList<byte>` or null |

Optional per-point lists must align with `Positions`. Check null or the corresponding `Has*` property before access. Use `TryGetPartIndices()` to expand compact part indices to a per-point list.

`ImmutableFilterByBox3d`, `ImmutableMapPositions`, `Chunk.ImmutableMerge`, and `Split(chunksize)` return new chunks. [GenericChunk](../src/Aardvark.Data.Points.Base/GenericChunk.cs) uses a `Durable.Def`-keyed dictionary for additional attributes; `chunk.ToGenericChunk()` converts the standard representation.

## Spatial Queries

Continuing the example above:

```csharp
var box = new Box3d(new V3d(-1), new V3d(1));
foreach (var result in pointSet.QueryPointsInsideBox(box))
    foreach (var position in result.Positions)
        Console.WriteLine(position);

long count = pointSet.CountPointsInsideBox(box);
```

Box queries include the boundary. The default visits full-resolution data; `minCellExponent` can stop traversal at a coarser level. Count queries avoid returning point lists but still load node data. See [Queries](../src/Aardvark.Geometry.PointSet/Queries) for nearest-point, polygon, and frustum queries.

### Octree-Level Counts

`CountPointsInOctreeLevel(level)` counts `PointCountCell` on a relative-depth front:
level 0 is the starting node, earlier leaves remain on deeper fronts, and negative
levels return zero. Inner nodes contribute their stored LoD samples, not
`PointCountTree`. The bounds overload rejects nodes whose exact global bounds do
not intersect the query; partially overlapping terminal nodes contribute all
their stored points, without per-point clipping.

Counting uses metadata rather than fetching terminal position arrays. Resident
ordinary nodes can therefore be counted even when their external position
payloads have been evicted from the cache. This is not a guarantee of payload-free
cold traversal: node decoding validates arrays, legacy metadata initialization
may load positions, and filtered views may load positions to select points or
derive bounds. Point enumeration is unchanged.

## Node Contracts

- `Positions.Value` is a `V3f[]` relative to `node.Center`; `PositionsAbsolute` returns global `V3d[]` coordinates.
- `PointCountCell` counts this node's stored points, including LOD samples on inner nodes. `PointCountTree` counts full-resolution points in the subtree, not the sum of LOD samples at every level.
- `Subnodes` contains up to eight child references with null entries for empty octants. Use `IsLeaf` to distinguish leaves.
- Attributes are accessed through `PersistentRef<T>.Value`. This may load from storage or return cached/in-memory data; retain the returned array when processing it repeatedly.
- Filtered views and in-memory KD-trees use `Lazy<T>`, which caches initialization exceptions. After resolving a transient failure, recreate the view, or evict the failed stored node's ID from `storage.Cache` and reload it. Existing references to the failed instance remain failed; no automatic retry or eviction is performed.

### Node-Local KD Queries

```csharp
var node = pointSet.Root.Value;
if (node.HasKdTree)
{
    var positions = node.Positions.Value;
    var nearest = node.KdTree.Value.GetClosest(V3f.Zero, float.MaxValue, 10);
    foreach (var hit in nearest)
        Console.WriteLine(positions[hit.Index]);
}
```

The query and results above are in **local coordinates**, and search only this node's points, not the entire cloud. Results with a count limit are in heap order, not nearest-first order.

KD-trees are not restricted to leaves. Non-temporary nodes with positions offer a KD-tree even without a stored KD reference: read-only construction builds it lazily in memory, without writing to storage. Explicit writes persist a missing tree. Temporary import nodes can receive one through `WithComputedKdTree()`; test `HasKdTree` rather than inferring availability from node type.

### Immutable Updates

`node.With(replacements)` creates a node with a new ID but does not persist that node. Call `WriteToStore()` and use its returned node: writing a missing KD-tree can return a new instance with the same ID and a persisted KD reference. This does not update a parent or a point set to reference the new ID; update those references separately. See [PointSetNode](../src/Aardvark.Geometry.PointSet/Octrees/PointSetNode.cs).

`pointSet.Merge(other, pointsMergedCallback, config)` returns a merged cloud. Supply the import configuration and keep its storage available throughout the operation.

## Related

- [Geometry](GEOMETRY.md): normal estimation and standalone spatial structures
- [Rendering](RENDERING.md): LOD scene-graph integration
