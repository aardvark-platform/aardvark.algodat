# PolyLineSet

`PolyLineSet` in `Aardvark.Geometry.PolyMesh` stores several polylines over one shared vertex array with the index layout of [PolyMesh](POLYMESH.md) faces. [PolyLineSet source](../src/Aardvark.Geometry.PolyMesh/PolyLineSet.cs).

```csharp
// fresh vertices per polyline; polylines with fewer than two points are dropped
var lines = PolyLineSet.FromPolyLines(new[]
{
    new[] { V3d.Zero, V3d.IOO, new V3d(2, 0, 0) },
    new[] { V3d.OIO, V3d.IIO }
});

// shared vertices: a line and a second line starting at the same vertex, stored as two polylines (a gap)
var indexed = PolyLineSet.FromIndexed(vertices, new[] { new[] { 0, 1, 2 }, new[] { 2, 3 } });
```

`FirstIndexArray` has `PolyLineCount + 1` entries like the mesh's face array and `VertexIndexArray` holds all polylines packed without end markers. `GetPolyLine(i)`, `GetSegments(i)`, `Segments` and `BoundingBox3d` walk the referenced vertices only.

| Dictionary | Direct attribute length |
|------------|-------------------------|
| `VertexAttributes` | `VertexCount` |
| `LineAttributes` | `PolyLineCount` |
| `LineVertexAttributes` | `VertexIndexCount` |
| `InstanceAttributes` | one value per set |

`Transformed(trafo)` transforms positions and the `Normals` entry of each dictionary; index arrays and other attributes are shared with the source. `GetIndexedGeometry()` returns an indexed `IndexedGeometryMode.LineList` built from `BuildLineListIndexArray()` with vertex attributes passed through (V3d arrays become V3f); line and line-vertex attributes are not part of that geometry. A set without polylines converts to `null`.

## Line attributes in the geometry

`WithResolvedLineAttributes()` moves the line and line-vertex attributes into `VertexAttributes`. Only a vertex that is referenced with more than one attribute value is duplicated (once per further value, appended after the original vertices, existing vertex attributes copied); the polyline indices are redirected to the copies and everything else stays shared. A set whose polylines own their vertices gets no duplicates, a set with nothing to resolve returns itself. `GetIndexedGeometry(resolveLineAttributes: true)` does this first, so per-polyline or per-polyline-vertex colours reach the renderer without expanding every segment.

```csharp
lines.LineAttributes[PolyLineSet.Property.Colors] = new[] { C4b.Red, C4b.Blue }; // one colour per polyline
var geometry = lines.GetIndexedGeometry(resolveLineAttributes: true);           // Colors is a vertex attribute now
```

## Serialisation

`PolyLineSet` is `IFieldCodeable` and registered with `[RegisterTypeInfo]`: the three arrays and the four attribute dictionaries are coded, so it can be a field of other codeable types (`c.CodeT(ref lineSet)`). A test host that cannot run `Aardvark.Init()` registers it with `TypeInfo.Add(typeof(PolyLineSet))`.

## Related

- [PolyMesh](POLYMESH.md): polygon faces with the same index layout
