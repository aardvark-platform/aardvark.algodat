# PolyMesh

`Aardvark.Geometry.PolyMesh` stores polygon faces, attributes, and optional half-edge topology. [PolyMesh source](../src/Aardvark.Geometry.PolyMesh/PolyMesh.cs).

## Construction

```csharp
using System;
using Aardvark.Base;
using Aardvark.Geometry;

var mesh = new PolyMesh
{
    PositionArray = new[] { V3d.Zero, V3d.IOO, new V3d(1, 1, 0), V3d.OIO },
    FirstIndexArray = new[] { 0, 4 },
    VertexIndexArray = new[] { 0, 1, 2, 3 }
};
```

`FirstIndexArray` has `FaceCount + 1` entries. Each adjacent pair bounds a face's slice of `VertexIndexArray`; the final entry is the total face-vertex count. Incremental construction is also available through `AddVertex` and `AddFace`.

Primitives live in [PolyMeshPrimitives](../src/Aardvark.Geometry.PolyMesh/PolyMeshPrimitives.cs), for example `PolyMeshPrimitives.Box(Box3d.Unit, C4b.White)`. For epsilon-based vertex deduplication, see [Clustering](GEOMETRY.md#clustering).

## Attributes

| Dictionary | Direct attribute length |
|------------|-------------------------|
| `VertexAttributes` | `VertexCount` |
| `FaceAttributes` | `FaceCount` |
| `FaceVertexAttributes` | Total face-vertex count |

`NormalArray` (`V3d[]`) and `ColorArray` (`C4f[]`) are convenience properties for per-vertex attributes. Custom attributes use `Symbol` keys and arrays aligned with their dictionary's domain.

Indexed attributes use **indices under the positive symbol** and **values under the negative symbol**. For the quad above:

```csharp
var uv = PolyMesh.Property.DiffuseColorCoordinates;
mesh.FaceVertexAttributes[uv] = new[] { 0, 1, 2, 3 };
mesh.FaceVertexAttributes[-uv] = new[]
{
    new V2f(0, 0), new V2f(1, 0), new V2f(1, 1), new V2f(0, 1)
};
```

Without a negative-key entry, the positive-key array holds direct values instead. The same convention applies to indexed normals and custom attributes.

## Operations

| Operation | Contract |
|-----------|----------|
| `TriangulatedCopy()` | Returns a triangulated mesh; `FaceVertexCountRange.Max > 3` identifies faces needing subdivision |
| `SubSetOfFaces(indices, compactVertices)` | `true` removes unused vertices and reindexes them; `false` preserves vertex indices. Neither compacts indexed attribute value arrays |
| `Transformed(trafo)` | Transforms positions and normals; do not assume custom or cached derived attributes are recomputed |
| `WithoutDegeneratedEdges()` | Removes consecutive duplicate vertex indices; may leave degenerate faces |
| `WithoutDegeneratedFaces()` | Filters degenerate faces; options control position and normal checks |
| `FaceReversedCopy()` | Reverses face winding; topology must be rebuilt for the result |
| `GetIndexedGeometry()` | Converts to rendering geometry; see [conversion overloads](../src/Aardvark.Geometry.PolyMesh/PolyMeshIndexedGeometry.cs) for attribute selection |

`new PolyMesh(other)` shares arrays. Do not treat it as a deep copy before mutation.

For grouping, use the `meshes.Group()` extension, which returns `PolyMesh.Grouping`, not a mesh. Inspect its matching-attribute information before extracting `Mesh`; see [PolyMeshGrouping](../src/Aardvark.Geometry.PolyMesh/PolyMeshGrouping.cs).

## Topology

Call `BuildTopology()` before topology-dependent edge or vertex-neighborhood traversal:

```csharp
mesh.BuildTopology();
foreach (var edge in mesh.GetFace(0).Edges)
{
    Console.WriteLine($"{edge.FromVertex.Position} -> {edge.ToVertex.Position}");
    if (edge.OppositeIsBorder)
        Console.WriteLine("Boundary edge");
}
```

`Vertices`, `Faces`, and `Edges` are enumerables; use `GetVertex(index)` and `GetFace(index)` for indexed access. Edge facades are structs: `Opposite` is not a nullable boundary marker. Use `OppositeIsBorder` or check `OppositeFace.IsValid` before crossing a boundary.

## Related

- [Geometry](GEOMETRY.md): clustering and intersection queries
