
[Aardvark](https://github.com/aardvark-platform/aardvark.docs/wiki) is an open-source platform for visual computing, real-time graphics and visualization.

This repository contains high-performance, production-quality data structures and algorithms. 

* **Aardvark.Geometry.PointSet** - out-of-core point cloud data management
* **Aardvark.Geometry.PointTree** - fast n-closest points queries for in-memory point clouds

## Point Clouds

Point clouds can be loaded like this:
```csharp
var a = PointCloud.Import("scan.pts"); // in-memory, limited size
WriteLine(a.PointCount);
WriteLine(a.Bounds);
```

By specifying an additional location for out-of-core data, all size limits will be removed.
```csharp
var a = PointCloud.Import("scan.pts", @"C:\Data\mystore"); // out-of-core, unlimited size
var key = a.Id;
```

When using a store, the imported dataset will also be stored permanently and can be loaded again directly from the store, which is very fast:
```csharp
var a = PointCloud.Load(key, @"C:\Data\mystore");
```

### Operations
Point clouds can be merged into larger ones.
```csharp
var a = PointCloud.Import("scan1.pts");
var b = PointCloud.Import("scan2.pts");
var m = a.Merge(b);
```
By the way, *a* and *b* are not touched by the merge operation and are still valid.
Internally, *m* will of course efficiently reuse the data already stored for *a* and *b*.

### Queries

#### Planes
```csharp
// All points within maxDistance of given plane.
QueryPointsNearPlane(Plane3d plane, double maxDistance)

// All points within maxDistance of ANY of the given planes.
QueryPointsNearPlanes(Plane3d[] planes, double maxDistance)

// All points NOT within maxDistance of given plane.
QueryPointsNotNearPlane(Plane3d plane, double maxDistance)

// All points NOT within maxDistance of ALL the given planes.
QueryPointsNotNearPlanes(Plane3d[] planes, double maxDistance)
```

#### Polygons
```csharp
// All points within maxDistance of given polygon.
QueryPointsNearPolygon(Polygon3d plane, double maxDistance)

// All points within maxDistance of ANY of the given polygons.
QueryPointsNearPolygons(Polygon3d[] planes, double maxDistance)

// All points NOT within maxDistance of given polygon.
QueryPointsNotNearPolygon(Polygon3d plane, double maxDistance)

// All points NOT within maxDistance of ALL the given polygons.
QueryPointsNotNearPolygons(Polygon3d[] planes, double maxDistance)
```

#### Box
```csharp
// All points inside axis-aligned box (including boundary).
QueryPointsInsideBox(Box3d box)

// All points outside axis-aligned box (excluding boundary).
QueryPointsOutsideBox(Box3d box)
```

#### Convex Hull
```csharp
// All points inside convex hull (including boundary).
QueryPointsInsideConvexHull(Hull3d convexHull)

// All points outside convex hull (excluding boundary).
QueryPointsOutsideConvexHull(Hull3d convexHull)
```

## License and Support

This software repository is made available under the terms of the [GNU Affero General Public License (AGPL)](LICENSE).

[Aardvark Platform Documentation](https://github.com/aardvark-platform/aardvark.docs/wiki)

[Other licensing options and maintenance plans](https://aardvark.graphics) are available for industrial development or research.

