# Geometry and Geodetics

Geometry APIs below use `Aardvark.Base` and `Aardvark.Geometry`; clustering additionally uses `Aardvark.Geometry.Clustering`. Examples with named input arrays are fragments to embed in an application.

## BSP Triangle Sorting

[BspTreeBuilder](../src/Aardvark.Geometry.BspTree/BspTree.cs) builds a tree for view-dependent triangle sorting. Given nonempty triangle indices `int[] indices`, positions `V3d[] positions`, and `V3d eyePosition`:

```csharp
var builder = new BspTreeBuilder(
    (int[])indices.Clone(), (V3d[])positions.Clone(), absoluteEpsilon: 1e-6);
var tree = builder.BspTree;
var splitPositions = builder.PositionArray;
var sortedIndices = new int[builder.TriangleCountMul3];
using var done = tree.SortVertexIndexArray(
    BspTree.Order.BackToFront, eyePosition, sortedIndices, parallel: true);
done.Wait();
```

- Pass copies when the originals must remain unchanged.
- Splitting may add vertices and triangles. Sorted indices refer to `builder.PositionArray`, whose used length is `builder.VertexCount`; size the output from `TriangleCountMul3`, not the input count.
- Read `builder.BspTree` once to finalize it. Both sorting modes return a `CountdownEvent`; wait before consuming the output.
- `absoluteEpsilon` is the coplanarity tolerance in position units.

## Clustering

For `V3d[] vertices`:

```csharp
var clustering = new PointEpsilonClustering(vertices, epsilon: 1e-6);
var centroids = vertices.ClusterCentroidArray(clustering.CountArray, clustering.IndexArray);
```

`IndexArray` maps each input to a cluster; `CountArray` gives cluster sizes. Epsilon clustering joins nearby points transitively, so a cluster's diameter can exceed epsilon. Use `PointEqualClustering` for exact equality.

[Clustering algorithms](../src/Aardvark.Geometry.Clustering/Algorithms.cs) also include `PointClustering`, `PlaneEpsilonClustering`, and `NormalsClustering`. When using low-level merge operations, consolidate cluster roots with `ClusterConsolidate` before `CompactAndComputeCountArray`; the high-level constructors handle their own consolidation.

## Ray and Closest-Point Queries

[KdIntersectionTree](../src/Aardvark.Geometry.Intersection/KdIntersectionTree.cs) accelerates an `IIntersectableObjectSet`. `IntersectableTriangleSet` accepts `int[] indices` and **`V3f[] positions`**:

```csharp
var triangles = new IntersectableTriangleSet(indices, positions);
var tree = new KdIntersectionTree(triangles, KdIntersectionTree.BuildFlags.Raytracing);
var hit = ObjectRayHit.MaxRange;
if (tree.Intersect(new FastRay3d(origin, direction), 0, double.MaxValue, ref hit))
{
    V3d hitPoint = hit.RayHit.Point;
    int triangleIndex = hit.SetObject.Index;
}
```

Here `origin` and `direction` are `V3d`. The hit argument is an in/out bound: the query updates it only when a closer hit is found. Ray `t` is a distance only for a unit direction. `BuildFlags` controls construction tradeoffs; `NoMultithreading` disables parallel construction. `IntersectsBox` tests only the tree's bounds, not its individual objects.

For a `V3d queryPoint`, use the same triangle tree to find the nearest surface point:

```csharp
var closest = ObjectClosestPoint.MaxRange;
if (tree.ClosestPoint(queryPoint, ref closest))
{
    V3d nearestPoint = closest.Point;
    double distance = closest.Distance;
    int triangleIndex = closest.SetObject.Index;
}
```

`MaxRange` initializes an unbounded query. For a finite bound, set both `Distance` and `DistanceSquared` consistently. Only a strictly closer result replaces the supplied value; otherwise the call returns `false` and leaves it unchanged.

The [triangle-set implementation](../src/Aardvark.Geometry.Intersection/IntersectableTriangleSet.cs) accepts a null object filter for all triangles, or a predicate to select candidates. Its separate point-result filter is ignored. Filter support for other object sets depends on their implementation.

## Normal Estimation

For a nonempty `V3f[]` or `V3d[] points`:

```csharp
var kdTree = points.BuildKdTree();
V3f[] normals = points.EstimateNormals(k: 16, kdTree);
var result = points.EstimateNormalsAndLocalDensity(k: 16, kdTree);
```

[Normals](../src/Aardvark.Geometry.Normals/Normals.cs) requires `k >= 3` and always returns `V3f[]` normals. PCA does not resolve their sign; orient them separately if needed. Local density is the average **squared** distance of neighbors to their centroid. Overloads without a KD-tree construct one; reuse a tree for repeated calls. Async variants are also available.

## Geodetics

[CoordinateSystem](../src/Aardvark.Geodetics/CoordinateSystem.fs) wraps DotSpatial projections. EPSG:4326 points use **longitude, latitude** in degrees; projected coordinates use the target system's units (meters for EPSG:32633).

```fsharp
open Aardvark.Base
open Aardvark.Geodetics

let wgs84 = CoordinateSystem.epsg 4326
let utm33n = CoordinateSystem.epsg 32633
let points = [| V3d(16.37, 48.21, 100.0); V3d(16.38, 48.22, 105.0) |]
let projected = CoordinateSystem.transform wgs84 utm33n points
```

C# uses the static members:

```csharp
using Aardvark.Base;
using CoordinateSystem = Aardvark.Geodetics.CoordinateSystem;

var wgs84 = CoordinateSystem.FromEPSGCode(4326);
var utm33n = CoordinateSystem.FromEPSGCode(32633);
var projected = CoordinateSystem.Transform(wgs84, utm33n, new V3d(16.37, 48.21, 100.0));
```

`FromProj4` and `FromEsri` support custom definitions. Array, list, and sequence overloads are eager: sequence input is materialized, not streamed. Transform bounded batches for large point clouds.

## Related

- [PolyMesh](POLYMESH.md): polygon construction, attributes, topology
- [Point clouds](POINT_CLOUDS.md): stored octrees and queries
- [Sky](SKY.md): astronomical coordinate and time conventions
- [EPSG registry](https://epsg.io/)
