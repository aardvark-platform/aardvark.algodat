# Point Cloud Importers

File import produces a stored `PointSet`; direct parsers yield chunks. These are point-cloud importers, not general mesh import/export APIs.

| Format | Entry points | Source |
|--------|--------------|--------|
| E57 | `E57.Chunks`, `E57.ChunksFull`, `E57.E57Info` | [Aardvark.Data.E57](../src/Aardvark.Data.E57/ImportE57.cs) |
| LAS/LAZ | `Laszip.Chunks`, `Laszip.LaszipInfo` | [Aardvark.Data.Points.LasZip](../src/Aardvark.Data.Points.LasZip/Import.cs) |
| PLY | `Ply.Chunks`, `Ply.PlyInfo`, `PlyParser` | [Aardvark.Data.Points.Ply](../src/Aardvark.Data.Points.Ply/PlyImport.cs), [Ply.Net](../src/Ply.Net) |
| ASCII | `Pts.Chunks`, `Yxh.Chunks`, `Ascii.Chunks` | [Aardvark.Data.Points.Ascii](../src/Aardvark.Data.Points.Ascii) |

Include the format's project/package in the application. `PointCloud.Import` selects registered formats by extension; [PointCloudFileFormat](../src/Aardvark.Data.Points.Base/PointCloudFileFormat.cs) discovers importer classes through introspection. A custom ASCII layout is supplied explicitly rather than inferred from an arbitrary `.txt` extension.

## Import to a Store

This example requires an existing `scan.e57` file:

```csharp
using Aardvark.Data.Points;
using Aardvark.Geometry.Points;

using var storage = PointCloud.OpenStore("scan.uds");
var config = ImportConfig.Default
    .WithStorage(storage)
    .WithKey("scan")
    .WithMaxChunkPointCount(65536);
var pointSet = PointCloud.Import("scan.e57", config);
storage.Flush();
```

[ImportConfig](../src/Aardvark.Geometry.PointSet/Octrees/ImportConfig.cs) uses `With*` methods that return new configurations. `WithEnabledPartIndices` and `WithPartIndexOffset` control source-part tracking; `WithProgressCallback` reports import progress. `WithMinDist` filters point density and `WithReproject` maps positions during import. Reprojection of positions does not itself transform normals.

See [Point clouds](POINT_CLOUDS.md) for storage lifetime, chunk types, and queries. Chunked parsing bounds working batches, not the size of an in-memory store.

## Direct Chunk Processing

Direct parsers take **ParseConfig**, not `ImportConfig`:

```csharp
using System;
using Aardvark.Data.Points;
using Aardvark.Data.Points.Import;

var parseConfig = ParseConfig.Default.WithMaxChunkPointCount(32768);
foreach (var chunk in Laszip.Chunks("scan.laz", parseConfig))
{
    Console.WriteLine(chunk.Count);
    if (chunk.Colors != null && chunk.Count > 0)
        Console.WriteLine(chunk.Colors[0]);
}
```

`Laszip.Chunks`, `Ply.Chunks`, and `E57.Chunks` return the common [Chunk representation](POINT_CLOUDS.md#chunks). Raw format properties are not necessarily retained there.

## Format-Specific Behavior

### E57

- Positions have the scan pose applied; normals are rotated when a pose is present.
- `Chunks` filters nonzero `CartesianInvalidState` entries. It supplies default colors and may fill attributes missing from individual scans to make their data compatible.
- `ChunksFull` returns `E57.E57Chunk`, preserving `RawData` and `Data3D`. It does **not** perform the same invalid-state filtering. If filtering it yourself, apply the mask to every aligned attribute, not only positions.
- Full chunks expose optional `uint[]` row/column and return indices, `C3b[]` colors, and `DateTimeOffset[]` timestamps. Timestamp conversion uses acquisition metadata or an epoch heuristic; consult `RawData` for the decoded timestamp values.
- Checksum verification is opt-in through the stream overloads with `verifyChecksums`.

### LAS/LAZ

`Laszip.Chunks` normalizes colors to `C4b` and intensities to `int`; it does not expose return numbers, flight-line flags, or GPS times. For those fields use [LASZip.Parser.ReadPoints](../src/Aardvark.Data.Points.LasZip/Parser.cs) and its raw `LASZip.Points` batches.

### ASCII

Token order must match the file's columns. For an `X Y Z R G B` text file:

```csharp
using Aardvark.Data.Points;
using Aardvark.Data.Points.Import;
using static Aardvark.Data.Points.Import.Ascii;

var layout = new[]
{
    Token.PositionX, Token.PositionY, Token.PositionZ,
    Token.ColorR, Token.ColorG, Token.ColorB
};
var chunks = Ascii.Chunks("scan.txt", layout, ParseConfig.Default);
```

The token enum also provides normals, float colors, intensity, custom scalar fields, and `Skip`; check [Ascii.Token](../src/Aardvark.Data.Points.Ascii/ImportAscii.cs) when defining a layout. `Pts` and `Yxh` have built-in layouts.

### PLY

The parser supports ASCII and both binary byte orders. The point importer reads vertex properties, not polygon faces:

- Position channels: `x`, `y`, `z`; normal channels: `nx`, `ny`, `nz`.
- Colors: `red`, `green`, `blue`, `alpha`; float colors are scaled from [0,1], and missing alpha defaults to 255 when colors exist.
- Intensity: `scalar_intensity` or `intensity`; classification: `scalar_classification` or `classification`.
- Intensity conversion depends on source type. Up to signed 32-bit integer values are retained; unsigned 32-bit, 64-bit, and floating-point values use per-batch rescaling to [0,255] when outside that range. This may lose precision and comparability between batches. Use `PlyParser.Parse` directly when raw values matter.

## Related

- [Geodetics](GEOMETRY.md#geodetics): explicit coordinate-system transformations
- [E57 specification](https://www.astm.org/e2807-11.html)
- [PLY format](http://paulbourke.net/dataformats/ply/)
