# Geometry Algorithms and Data Structures

Computational geometry algorithms for BSP trees, clustering, intersection tests, and normal estimation.

## Aardvark.Geometry.BspTree

Binary Space Partitioning trees for view-dependent triangle sorting.

| Type | Purpose |
|------|---------|
| `BspTree` | Holds triangle vertex indices and a tree of `BspNode`s for sorting based on eye point |
| `BspTreeBuilder` | Builds a BSP tree from triangle mesh data; discarded after construction |
| `BspNode` | Internal node storing splitting plane (point + normal) and child references |
| `BspSplitPoint` | Represents split points on triangle edges during tree construction |

### Usage

```csharp
// Copy index and position arrays (BSP builder modifies them)
var indices = originalIndices.ToArray();
var positions = originalPositions.ToArray();

// Build BSP tree with absolute epsilon for coplanarity
var builder = new BspTreeBuilder(indices, positions, absoluteEpsilon: 1e-6);
var bspTree = builder.BspTree;

// Sort indices back-to-front for transparency rendering
var sortedIndices = new int[indices.Length];
var countdown = bspTree.SortVertexIndexArray(
    BspTree.Order.BackToFront,
    eyePosition,
    sortedIndices,
    parallel: true
);
countdown.Wait(); // Block until sorting completes
```

### Gotchas

- **BspTreeBuilder modifies input arrays** – Always pass copies of index/position arrays.
- **Epsilon controls splitting** – Too small creates deep trees; too large loses precision.
- **Parallel sorting returns async event** – `SortVertexIndexArray` returns `CountdownEvent`; call `.Wait()` to block.
- **Tree construction uses shuffle** – Triangles are inserted in shuffled order to reduce tree depth.
- **Split triangles are duplicated** – Triangles straddling planes are cloned into fragments; final triangle count may exceed original.

## Aardvark.Geometry.Clustering

Spatial clustering via union-find with hash grids and kd-trees.

| Type | Purpose |
|------|---------|
| `Clustering` | Base class managing cluster index and count arrays |
| `DynamicClustering` | Supports incremental item addition with `AddItem()` |
| `PointClustering` | Clusters V3d points within delta distance using kd-tree |
| `PointEpsilonClustering` | Fast hash-grid clustering for epsilon-close vertices |
| `PointEqualClustering` | Merges exactly equal points using hash grid |
| `PlaneEpsilonClustering` | Clusters planes by normal and distance epsilon |
| `NormalsClustering` | Clusters normals by dot product threshold |
| `ClusteringExtensions` | Methods for `GetClusterIndex`, `ClusterMergeLeft`, `ClusterConsolidate`, `CompactAndComputeCountArray` |

### Usage

```csharp
// Epsilon-based vertex deduplication (ideal for mesh cleanup)
var clustering = new PointEpsilonClustering(vertices, epsilon: 1e-6);
int[] clusterIndices = clustering.IndexArray;  // maps vertex -> cluster
int[] clusterCounts = clustering.CountArray;   // size of each cluster

// Centroid computation
var centroids = vertices.ClusterCentroidArray(clusterCounts, clusterIndices);

// Custom clustering with generic accessors
var planeClustering = new PlaneEpsilonClustering<Plane3d[]>(
    count: planes.Length,
    pa: planes,
    getNormal: (arr, i) => arr[i].Normal,
    getDist: (arr, i) => arr[i].Distance,
    epsNormal: 1e-4,
    epsDist: 1e-3
);
```

### Gotchas

- **Call `ClusterConsolidate` before `CompactAndComputeCountArray`** – Cluster indices must point to root before compaction.
- **Epsilon clustering uses 8-cell hash** – Checks neighboring grid cells; performance degrades if too many points per cell.
- **PointEqualClustering requires exact equality** – Use `PointEpsilonClustering` for numerical tolerance.
- **Random merge ties** – Merging uses random bits to prevent pathological tree depth; results may vary slightly.
- **Hash grid epsilon is for acceleration** – In `PointEpsilonClustering`, epsilon defines grid size; actual distance checks use squared epsilon.

## Aardvark.Geometry.Intersection

Kd-tree–based ray-object intersection with custom object sets.

| Type | Purpose |
|------|---------|
| `IIntersectableObjectSet` | Interface for ray-intersectable object collections |
| `KdIntersectionTree` | Kd-tree accelerating ray intersections and closest-point queries |
| `IntersectableTriangleSet` | Triangle soup implementation of `IIntersectableObjectSet` |
| `ObjectRayHit` | Ray intersection result with t-parameter, point, and object reference |
| `ObjectClosestPoint` | Closest-point query result with distance and coordinates |
| `FastRay3d` | Precomputed ray data for efficient kd-tree traversal |

### Usage

```csharp
// Build kd-tree for triangle set
var triangles = new IntersectableTriangleSet(indices, positions);
var kdTree = new KdIntersectionTree(
    triangles,
    KdIntersectionTree.BuildFlags.Raytracing
);

// Ray intersection
var ray = new FastRay3d(origin, direction);
var hit = ObjectRayHit.MaxRange;
if (kdTree.Intersect(ray, tmin: 0, tmax: double.MaxValue, ref hit))
{
    V3d hitPoint = hit.RayHit.Point;
    double t = hit.RayHit.T;
    int triangleIndex = hit.SetObject.Index;
}

// Closest point query
var closest = new ObjectClosestPoint { DistanceSquared = double.MaxValue };
if (kdTree.ClosestPoint(queryPoint, ref closest))
{
    V3d nearestPoint = closest.Point;
    double distance = closest.Distance;
}
```

### Gotchas

- **BuildFlags control quality/speed tradeoff** – `FastIntersection` splits at 7 objects, `Raytracing` uses slower build with better quality.
- **FastRay3d precomputes reciprocals** – Construct once per ray; do not modify direction.
- **Hit parameter is in/out** – Pass existing hit with `t` limit; updated only if closer intersection found.
- **Object filters can skip tests** – Supply `null` for no filtering; filters allow skipping objects or hits.
- **Parallel build enabled by default** – Use `BuildFlags.NoMultithreading` to force single-threaded construction.

## Aardvark.Geometry.Normals

Normal estimation from k-nearest neighbors via PCA.

| Type | Purpose |
|------|---------|
| `Normals` (static) | Extension methods for estimating normals from point clouds |

### Usage

```csharp
// Estimate normals (builds temporary kd-tree)
V3f[] normals = points.EstimateNormals(k: 16);

// Reuse existing kd-tree
var kdTree = points.BuildKdTree();
V3f[] normals = points.EstimateNormals(k: 16, kdTree);

// Async estimation
V3f[] normals = await points.EstimateNormalsAsync(k: 16);

// Estimate normals + local density
var (normals, densities) = points.EstimateNormalsAndLocalDensity(k: 16);
// densities[i] = average squared distance of k-nearest points to centroid
```

### Gotchas

- **k must be ≥ 3** – At least 3 points needed for PCA; throws `ArgumentOutOfRangeException` otherwise.
- **Normal orientation is arbitrary** – Eigenvector for smallest eigenvalue has undefined sign; post-process to orient consistently.
- **Temporary kd-tree cost** – Overloads without kd-tree parameter build one internally; reuse kd-tree for multiple calls.
- **Local density is squared distance** – In `EstimateNormalsAndLocalDensity`, density values are not distances but squared distances.
- **V3d arrays return V3f normals** – Normals are always `V3f[]` regardless of input precision.

## See Also

- [POLYMESH.md](POLYMESH.md) - Polygon mesh data structures
- [POINT_CLOUDS.md](POINT_CLOUDS.md) - Point cloud processing, kd-trees, and spatial queries
