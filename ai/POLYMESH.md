# Aardvark PolyMesh Reference

Polygon mesh representation with half-edge topology and flexible attribute storage.

## Core Types

| Type | Purpose |
|------|---------|
| PolyMesh | Main polygon mesh class, extends SymMap |
| PolyMesh.Vertex | Lightweight facade for vertex access |
| PolyMesh.Face | Lightweight facade for face traversal |
| PolyMesh.Edge | Half-edge facade for topology |
| PolyMesh.Polygon | Non-oriented polygon facade |

## Properties

| Property | Type | Description |
|----------|------|-------------|
| PositionArray | V3d[] | Vertex positions |
| FirstIndexArray | int[] | Face start indices (length = FaceCount + 1) |
| VertexIndexArray | int[] | Face-vertex indices |
| NormalArray | V3d[] | Vertex normals (optional) |
| ColorArray | C4f[] | Vertex colors (optional) |
| VertexAttributes | SymbolDict<Array> | Per-vertex data |
| FaceAttributes | SymbolDict<Array> | Per-face data |
| FaceVertexAttributes | SymbolDict<Array> | Per-face-vertex data |

## Construction

### Manual Construction with Vertex Deduplication

```csharp
var builder = new PolyMesh();
var eps = 1e-8;
var vertexMap = new Dictionary<V3d, int>();

int AddUniqueVertex(V3d pos)
{
    foreach (var kvp in vertexMap)
    {
        if (V3d.Distance(kvp.Key, pos) < eps)
            return kvp.Value;
    }
    var idx = builder.AddVertex(pos);
    vertexMap[pos] = idx;
    return idx;
}

var v0 = AddUniqueVertex(new V3d(0, 0, 0));
var v1 = AddUniqueVertex(new V3d(1, 0, 0));
var v2 = AddUniqueVertex(new V3d(1, 1, 0));
var v3 = AddUniqueVertex(new V3d(0, 1, 0));

builder.AddFace(v0, v1, v2, v3);
var mesh = builder;
```

### Array-Based Construction

```csharp
var positions = new V3d[]
{
    new V3d(0, 0, 0),
    new V3d(1, 0, 0),
    new V3d(1, 1, 0),
    new V3d(0, 1, 0)
};

var firstIndices = new int[] { 0, 4 }; // One quad: starts at 0, ends at 4
var vertexIndices = new int[] { 0, 1, 2, 3 };

var mesh = new PolyMesh
{
    PositionArray = positions,
    FirstIndexArray = firstIndices,
    VertexIndexArray = vertexIndices
};
```

### Primitive Creation

```csharp
// Box
var box = PolyMesh.CreateBox(Box3d.Unit);

// Sphere (tessellated icosahedron)
var sphere = PolyMesh.CreateSphere(V3d.Zero, 1.0, 2); // 2 subdivisions

// Cylinder
var cylinder = PolyMesh.CreateCylinder(V3d.Zero, V3d.OOI, 0.5, 16);
```

### With Attributes

```csharp
var mesh = new PolyMesh
{
    PositionArray = positions,
    FirstIndexArray = firstIndices,
    VertexIndexArray = vertexIndices,
    NormalArray = normals, // per-vertex
    ColorArray = colors,   // per-vertex
    FaceAttributes = new SymbolDict<Array>
    {
        [PolyMesh.Property.MaterialIndices] = new int[] { 0, 1, 2 }
    }
};
```

### Indexed Attributes (Per-Face-Vertex)

```csharp
// UV coordinates with indexing
var uvData = new V2f[] { new V2f(0, 0), new V2f(1, 0), new V2f(1, 1), new V2f(0, 1) };
var uvIndices = new int[] { 0, 1, 2, 3, 1, 2, 3, 0 }; // 2 quads sharing UVs

mesh.FaceVertexAttributes = new SymbolDict<Array>
{
    [PolyMesh.Property.DiffuseColorCoordinates] = uvData,
    [-PolyMesh.Property.DiffuseColorCoordinates] = uvIndices // negative key for indices
};
```

## Operations

| Method | Description |
|--------|-------------|
| TriangulatedCopy() | Triangulate all faces |
| SubSetOfFaces(indices, compact) | Extract face subset |
| Transformed(Trafo3d) | Apply transformation |
| Group(meshes) | Merge multiple meshes |
| WithoutDegeneratedEdges() | Remove zero-length edges |
| WithoutDegeneratedFaces() | Remove zero-area faces |
| GetIndexedGeometry() | Convert to IndexedGeometry for rendering |

### Triangulation

```csharp
// Check if triangulation needed
if (mesh.FaceVertexCountRange.Max > 3)
{
    mesh = mesh.TriangulatedCopy();
}
```

### Plane Splitting

```csharp
var plane = new Plane3d(V3d.IOO, 0.0); // x = 0
var (negative, positive) = mesh.SplitOnPlane(
    plane, 1e-9, SplitterOptions.NegativeAndPositive);
var positiveOnly = mesh.SplitOnPlane(plane, 1e-9, SplitterOptions.Positive).Item2;
```

`SplitOnPlane` partitions convex faces without generating caps or building topology. Tuple element 0 is negative and element 1 is positive. Unrequested or absent sides are null; `SplitterOptions.None` returns `(null, null)` without scanning geometry. One-sided calls only construct the requested side. The plane normal need not be normalized: `epsilon` is a tolerance on `plane.Height(position)`, so scale it along with the plane equation.

- Whole-side results return the original mesh, including its arrays. Splitting does not modify source arrays; new fragment meshes also share the source instance-attribute dictionary. Copy before mutating shared results or instance data.
- Shared indexed edges reuse a single cut vertex per side. Retained faces come first, followed by fragments in source-face order; face winding is preserved.
- Face attributes follow each fragment's original face. Indexed face attributes are compacted from the source attribute indices used by retained faces and fragments, preserving the value-array type. Indexed storage uses `int[]` indices under the positive semantic key and values under the negative key; for example, source face index `[1]` into values `[11,22]` becomes `[0]` into `[22]`.
- Vertex attributes interpolate at cut points using their registered interpolators. Corner attributes interpolate along each original face edge, independently on adjacent faces, so shared geometric vertices do not weld attribute seams. For a crossing quad with corner values `[10,20,30,40]`, midpoint cuts interpolate to 15 and 35.
- Existing coplanar ownership is preserved: a coplanar face in mixed input belongs to the positive side (and is omitted by a negative-only request). Wholly coplanar input is returned on the positive side when requested, otherwise on the negative side. Empty input follows the same whole-side preference. Points within the height tolerance are not projected onto the plane.

This operation does not change grouping or topology rules, and does not repair `ClipByPlane` cap generation.

### Face Subset Extraction

```csharp
// Extract specific faces
var faceIndices = new int[] { 0, 2, 5 };
var subset = mesh.SubSetOfFaces(faceIndices, compactVertices: true); // removes unused vertices
var subsetKeepAll = mesh.SubSetOfFaces(faceIndices, compactVertices: false); // keeps all vertices
```

### Transformation

```csharp
var trafo = Trafo3d.RotationX(Constant.PiHalf) * Trafo3d.Translation(1, 2, 3);
var transformed = mesh.Transformed(trafo);
```

### Mesh Grouping

```csharp
var meshes = new[] { mesh1, mesh2, mesh3 };
var merged = PolyMesh.Group(meshes); // requires matching attribute sets
```

### Cleanup

```csharp
var cleaned = mesh
    .WithoutDegeneratedEdges()
    .WithoutDegeneratedFaces();
```

### Rendering Conversion

```csharp
var indexedGeometry = mesh.GetIndexedGeometry();
// Use with Aardvark.Rendering
```

## Topology

Half-edge topology must be built explicitly before accessing edges.

```csharp
mesh.BuildTopology();

// Traverse edges around a face
var face = mesh.Faces[0];
foreach (var edge in face.Edges)
{
    var start = edge.FromVertex.Position;
    var end = edge.ToVertex.Position;
    var opposite = edge.Opposite; // may be null on boundary
}

// Traverse edges around a vertex
var vertex = mesh.Vertices[0];
foreach (var edge in vertex.OutgoingEdges)
{
    var neighbor = edge.ToVertex;
}
```

## Attributes

### Direct Per-Vertex Attributes

```csharp
mesh.NormalArray = normals; // V3d[]
mesh.ColorArray = colors;   // C4f[]
mesh.VertexAttributes[MySymbol] = customData; // any Array type
```

### Direct Per-Face Attributes

```csharp
mesh.FaceAttributes[PolyMesh.Property.MaterialIndices] = new int[] { 0, 1, 2 };
```

### Indexed Per-Face-Vertex Attributes

```csharp
// Normal indices (per-face-vertex)
var normalData = new V3f[] { V3f.OOI, V3f.OIO, V3f.IOO };
var normalIndices = new int[] { 0, 0, 0, 1, 1, 1, 2, 2, 2 }; // 3 triangles

mesh.FaceVertexAttributes = new SymbolDict<Array>
{
    [PolyMesh.Property.Normals] = normalData,
    [-PolyMesh.Property.Normals] = normalIndices // negative key
};
```

### Custom Attributes

```csharp
var MyTemperature = new Symbol("Temperature");

// Per-vertex
mesh.VertexAttributes[MyTemperature] = new float[] { 20.5f, 21.3f, 19.8f };

// Per-face
mesh.FaceAttributes[MyTemperature] = new float[] { 20.0f, 21.0f };

// Per-face-vertex (indexed)
var temps = new float[] { 15f, 20f, 25f };
var tempIndices = new int[] { 0, 1, 2, 1, 2, 0 };
mesh.FaceVertexAttributes[MyTemperature] = temps;
mesh.FaceVertexAttributes[-MyTemperature] = tempIndices;
```

## Gotchas

1. **Shallow copy by default** - `new PolyMesh(other)` shares arrays. Mutate carefully.
2. **Cached attributes don't update** - `Transformed()` updates positions/normals but cached derived data (areas, volumes) may be stale.
3. **Topology not automatic** - Call `BuildTopology()` before accessing `Edges`, `Vertices.OutgoingEdges`, etc.
4. **Indexed attributes require both arrays** - Set data array with positive key, index array with negative key.
5. **Triangulation is expensive** - Check `FaceVertexCountRange.Max` before calling `TriangulatedCopy()`.
6. **Imported meshes have degenerate faces** - Call `WithoutDegeneratedFaces()` before processing.
7. **Vertex clustering can throw** - Use try-catch when clustering with small delta values.
8. **SubSetOfFaces compactVertices flag** - `true` reindexes vertices, `false` preserves original indices.
9. **Group() requires matching attributes** - All meshes must have same attribute keys or operation fails.
10. **FaceReversedCopy() blindly flips** - Analyze orientation first (check normals vs face winding), don't flip unnecessarily.

## See Also

- [GEOMETRY.md](GEOMETRY.md) - Core geometric types
- [IMPORTERS.md](IMPORTERS.md) - Mesh import/export
- [RENDERING.md](RENDERING.md) - Rendering pipeline integration
