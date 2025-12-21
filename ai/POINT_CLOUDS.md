# Point Cloud Data Structures and APIs

## Purpose

Point cloud storage and spatial query infrastructure for large-scale 3D point datasets. Provides octree-based hierarchical storage (`PointSet`), spatial acceleration structures (`PointTree`), and generic chunk-based data flow (`Chunk`, `GenericChunk`).

## Major Types

| Type | Purpose | Key Properties |
|------|---------|----------------|
| `PointSet` | Immutable octree-based point cloud container | `Root`, `Storage`, `PointCount`, `Bounds`, `SplitLimit` |
| `PointSetNode` | Octree node with per-point attributes | `Id`, `Cell`, `Positions`, `Colors`, `Normals`, `Intensities`, `Classifications`, `Subnodes`, `KdTree` |
| `IPointCloudNode` | Interface for octree nodes | `Cell`, `PointCountTree`, `BoundingBoxExactGlobal`, `HasPositions`, `IsLeaf` |
| `Chunk` | Streaming point data with attributes | `Positions`, `Colors`, `Normals`, `Intensities`, `Classifications`, `PartIndices`, `BoundingBox` |
| `GenericChunk` | Extensible chunk with custom attributes | `Data` (ImmutableDictionary), `BoundingBox` |
| `Storage` | Persistence layer for point cloud data | `Add()`, `Get()`, `Remove()`, `Flush()`, `Cache` |
| `PointRkdTreeF<TArray, TPoint>` | KD-tree for fast spatial queries | `GetClosest()`, `GetClosestToLine()` |
| `Cell` | Octree cell index/bounds representation | `BoundingBox`, `Exponent`, `GetOctant()` |
| `PersistentRef<T>` | Lazy-loaded reference to stored data | `Id`, `Value` (lazy loaded) |

## Usage Patterns

### Creating a Point Cloud

```csharp
using Aardvark.Geometry.Points;
using Aardvark.Data.Points;
using Aardvark.Base;

// Create storage backend
var storage = /* SimpleDiskStorage, SimpleMemoryStorage, etc. */;

// Prepare point data
var positions = new List<V3d> { /* ... */ };
var colors = new List<C4b> { /* ... */ };
var normals = new List<V3f> { /* ... */ };

// Create point cloud with octree structure
var pointSet = PointSet.Create(
    storage: storage,
    pointSetId: "myPointCloud.json",
    positions: positions,
    colors: colors,
    normals: normals,
    intensities: null,
    classifications: null,
    partIndices: null,
    octreeSplitLimit: 8192,
    generateLod: true,
    isTemporaryImportNode: false
);

Console.WriteLine($"Point count: {pointSet.PointCount}");
Console.WriteLine($"Bounds: {pointSet.BoundingBox}");
```

### Loading an Existing Point Cloud

```csharp
var storage = /* load storage */;
var json = /* load JSON from storage.Get("myPointCloud.json") */;
var pointSet = PointSet.Parse(JsonNode.Parse(json), storage);
```

### Querying Points by Bounding Box

```csharp
using Aardvark.Geometry.Points;

var queryBox = new Box3d(new V3d(-10, -10, -10), new V3d(10, 10, 10));

// Stream chunks of points inside the box
foreach (var chunk in pointSet.QueryPointsInsideBox(queryBox))
{
    foreach (var pos in chunk.Positions)
    {
        // Process point
    }

    if (chunk.HasColors)
    {
        foreach (var color in chunk.Colors)
        {
            // Process color
        }
    }
}

// Count points without materializing
long count = pointSet.CountPointsInsideBox(queryBox);
```

### Spatial Queries with KD-Tree

```csharp
// Access node's KD-tree (automatically computed for leaf nodes)
var node = pointSet.Root.Value;
if (node.HasKdTree)
{
    var kdTree = node.KdTree.Value;

    // Find K nearest neighbors
    var queryPoint = new V3f(0, 0, 0);
    var nearest = kdTree.GetClosest(
        point: queryPoint,
        maxDistance: double.MaxValue,
        maxCount: 10
    );

    foreach (var result in nearest)
    {
        var point = node.Positions.Value[result.Index];
        var distance = result.Dist;
        // Process neighbor
    }
}
```

### Working with Chunks (Streaming Import)

```csharp
using Aardvark.Data.Points;

// Create chunk from raw data
var chunk = new Chunk(
    positions: new V3d[] { /* ... */ },
    colors: new C4b[] { /* ... */ },
    normals: null,
    intensities: null,
    classifications: null,
    partIndices: null,
    partIndexRange: null,
    bbox: null  // Auto-computed
);

// Filter chunk
var filteredChunk = chunk.ImmutableFilterByBox3d(
    new Box3d(new V3d(-5, -5, -5), new V3d(5, 5, 5))
);

// Merge chunks
var mergedChunk = Chunk.ImmutableMerge(chunk1, chunk2, chunk3);

// Split large chunk
foreach (var subChunk in chunk.Split(chunksize: 4096))
{
    // Process sub-chunk
}
```

### Accessing Node Attributes

```csharp
var node = pointSet.Root.Value;

// Check attribute availability
if (node.HasPositions)
{
    var positions = node.Positions.Value;  // V3f[] (local coords)
    var absolute = node.PositionsAbsolute; // V3d[] (global coords)
}

if (node.HasColors)
{
    var colors = node.Colors.Value;  // C4b[]
}

if (node.HasNormals)
{
    var normals = node.Normals.Value;  // V3f[]
}

if (node.HasIntensities)
{
    var intensities = node.Intensities.Value;  // int[]
}

if (node.HasClassifications)
{
    var classifications = node.Classifications.Value;  // byte[]
}

// Tree structure
if (!node.IsLeaf)
{
    for (int i = 0; i < 8; i++)
    {
        var subnode = node.Subnodes?[i];
        if (subnode != null)
        {
            var childNode = subnode.Value;  // Lazy-loaded
            // Process child
        }
    }
}
```

### Merging Point Clouds

```csharp
var config = ImportConfig.Default;
var merged = pointSet1.Merge(
    other: pointSet2,
    pointsMergedCallback: count => Console.WriteLine($"Merged {count} points"),
    config: config
);
```

### Custom Storage Backend

```csharp
var storage = new Storage(
    add: (key, value, createBuffer) =>
    {
        var buffer = createBuffer();
        // Write buffer to custom backend
    },
    get: key =>
    {
        // Read from custom backend, return byte[] or null
        return /* byte[] */;
    },
    getSlice: (key, offset, count) =>
    {
        // Read slice from custom backend
        return /* byte[] */;
    },
    remove: key =>
    {
        // Remove from custom backend
    },
    dispose: () =>
    {
        // Clean up resources
    },
    flush: () =>
    {
        // Flush pending writes
    },
    cache: new LruDictionary<string, object>(capacity: 1024)
);
```

## Gotchas

### 1. Local vs. Global Coordinates

**Problem:** `PointSetNode.Positions` returns positions in **local cell coordinates** (relative to `node.Center`), not global coordinates.

```csharp
var node = pointSet.Root.Value;
var localPos = node.Positions.Value[0];    // Wrong: relative to cell center
var globalPos = node.PositionsAbsolute[0];  // Correct: absolute coordinates
// Or manually: globalPos = new V3d(node.Center + localPos)
```

### 2. Lazy-Loaded Data Access

**Problem:** Accessing `PersistentRef<T>.Value` triggers storage I/O. Repeated access loads from storage each time unless cached.

```csharp
// Inefficient: loads from storage twice
for (int i = 0; i < node.Positions.Value.Length; i++)
    Process(node.Positions.Value[i]);

// Efficient: cache the array reference
var positions = node.Positions.Value;
for (int i = 0; i < positions.Length; i++)
    Process(positions[i]);
```

### 3. KD-Tree Availability

**Problem:** KD-trees are only computed for **leaf nodes** and only if `isTemporaryImportNode = false`. Querying inner nodes or temporary import nodes will return `HasKdTree = false`.

```csharp
if (!node.IsLeaf || node.IsTemporaryImportNode)
{
    // No KD-tree available - use octree traversal instead
}
```

### 4. Point Count Semantics

**Problem:** `PointCountCell` vs. `PointCountTree` have different meanings.

```csharp
var node = pointSet.Root.Value;
var cellCount = node.PointCountCell;   // Points in THIS cell only
var treeCount = node.PointCountTree;   // Points in entire subtree (sum of leaves)

// For leaf nodes: cellCount == treeCount
// For inner nodes: cellCount may be 0, treeCount > 0
```

### 5. Immutable Updates and Storage

**Problem:** Calling `With()` creates a new node with a new ID but does **not** write to storage automatically. You must call `WriteToStore()` explicitly.

```csharp
var updatedNode = node.With(new Dictionary<Durable.Def, object>
{
    [Durable.Octree.Colors4b] = newColors
});
// updatedNode exists only in memory!

updatedNode = updatedNode.WriteToStore();  // Now persisted
```

## See Also

- [IMPORTERS.md](IMPORTERS.md) - Importing point clouds from various file formats
- [GEOMETRY.md](GEOMETRY.md) - Geometric primitives (Box3d, Cell, V3d, etc.)
- `Queries*.cs` - Specialized spatial queries (Box3d, Polygon3d, ViewFrustum, etc.)
- `Filter*.cs` - View filtering (boolean operations, classification, intensity ranges)
- `LodExtensions` - Level-of-detail generation and management
