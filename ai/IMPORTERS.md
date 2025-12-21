# Point Cloud File Format Importers

This document describes the point cloud file format importers provided by the Aardvark platform. All importers follow a unified API pattern and automatically register with the `PointCloud.Import()` system.

## Supported Formats

| Format | Project | Extensions | Features |
|--------|---------|------------|----------|
| E57 | Aardvark.Data.E57 | `.e57` | ASTM E2807-11 standard, multi-scan support, coordinate transforms, multi-return data, grid-based organization, GPS timestamps |
| ASCII | Aardvark.Data.Points.Ascii | Custom | Flexible token-based parsing, PTS format (XYZ+RGB), YXH format (XYZ+IRGB), user-defined layouts |
| LAS/LAZ | Aardvark.Data.Points.LasZip | `.las`, `.laz` | LiDAR data, LAS 1.0-1.4, compressed/uncompressed, return metadata, scan direction, GPS times |
| PLY | Aardvark.Data.Points.Ply | `.ply` | Polygon File Format, ASCII and binary (little/big endian), auto-type conversion, intensity rescaling |

## Import API Patterns

All importers follow a consistent pattern for integration with `PointCloud.Import()`:

### Basic Import

```csharp
using Aardvark.Data;
using Aardvark.Geometry.Points;

var filename = "scan.e57";
var config = ImportConfig.Default
    .WithStorage(PointCloud.CreateInMemoryStore())
    .WithKey("my-pointcloud");

var pointset = PointCloud.Import(filename, config);
```

### Advanced Configuration

```csharp
var config = ImportConfig.Default
    .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
    .WithKey("my-pointcloud")
    .WithMaxChunkPointCount(65536)              // Chunk size control
    .WithPartIndexOffset(42)                     // Multi-file tracking
    .WithEnabledPartIndices(true)                // Enable part indices
    .WithMinDist(0.01)                           // Point density filtering
    .WithVerbose(true);                          // Enable logging

var pointset = PointCloud.Import(filename, config);
```

### Low-Level Chunk Access

Each importer exposes a `Chunks()` method for direct chunk iteration:

```csharp
using Aardvark.Data.Points.Import;

var filename = "scan.las";
var config = ParseConfig.Default;

foreach (var chunk in Laszip.Chunks(filename, config))
{
    V3d[] positions = chunk.Positions;
    C3b[] colors = chunk.Colors;          // May be null
    V3f[] normals = chunk.Normals;        // May be null
    int[] intensities = chunk.Intensities; // May be null
    byte[] classifications = chunk.Classifications; // May be null
}
```

### File Metadata Extraction

```csharp
var e57Info = E57.E57Info("scan.e57", ParseConfig.Default);
var laszipInfo = Laszip.LaszipInfo("scan.las", ParseConfig.Default);
var plyInfo = Ply.PlyInfo("scan.ply", ParseConfig.Default);

// Header-only parsing for PLY
var header = PlyParser.ParseHeader("scan.ply");
Console.WriteLine($"Vertex count: {header.Vertex?.Count}");
```

## Format-Specific Usage

### E57 Format

E57 is an XML-based format with binary data sections (ASTM E2807-11) commonly used for terrestrial laser scanning.

```csharp
using Aardvark.Data.Points.Import;

var chunks = E57.ChunksFull("scan.e57", ParseConfig.Default);

foreach (var chunk in chunks)
{
    // Standard properties
    V3d[] positions = chunk.Positions;
    C3b[] colors = chunk.Colors;
    V3f[] normals = chunk.Normals;
    int[] intensities = chunk.Intensities;

    // E57-specific metadata
    int?[] rowIndices = chunk.RowIndex;
    int?[] columnIndices = chunk.ColumnIndex;
    int[] returnCounts = chunk.ReturnCount;
    int[] returnIndices = chunk.ReturnIndex;
    double[] timestamps = chunk.Timestamps;  // GPS or Unix epoch
    int[] cartesianInvalidState = chunk.CartesianInvalidState;

    // Access all raw properties
    var rawData = chunk.RawData;  // ImmutableDictionary<PointPropertySemantics, Array>
}
```

**Key Features:**
- Multiple Data3D objects (different scans) in one file
- Coordinate transformations via pose (rotation + translation)
- Multi-return sensor data support
- Optional grid-based point organization (row/column indices)
- Checksum verification

### ASCII Formats

Highly flexible custom ASCII parsing via token definitions.

```csharp
using Aardvark.Data.Points.Import;
using static Aardvark.Data.Points.Import.Ascii;

// Built-in PTS format: X Y Z R G B
var ptsChunks = Pts.Chunks("scan.pts", ParseConfig.Default);

// Built-in YXH format: X Y Z Intensity R G B
var yxhChunks = Yxh.Chunks("scan.yxh", ParseConfig.Default);

// Custom format definition
var customFormat = Ascii.CreateFormat("MyFormat", new[] {
    Token.PositionX, Token.PositionY, Token.PositionZ,
    Token.ColorR, Token.ColorG, Token.ColorB,
    Token.Intensity,
    Token.NormalX, Token.NormalY, Token.NormalZ
});

var customChunks = Ascii.Chunks("scan.txt", customFormat.LineDefinition, ParseConfig.Default);
```

**Available Tokens:**
- Position: `PositionX`, `PositionY`, `PositionZ`
- Normal: `NormalX`, `NormalY`, `NormalZ`
- Color (byte 0-255): `ColorR`, `ColorG`, `ColorB`, `ColorA`
- Color (float 0.0-1.0): `ColorRf`, `ColorGf`, `ColorBf`, `ColorAf`
- `Intensity`
- Custom data: `CustomByte`, `CustomInt32`, `CustomFloat32`, `CustomFloat64`
- `Skip` (ignore field)

**Key Features:**
- Stream-based line-by-line parsing
- Empty lines skipped automatically
- Progress reporting and verbose logging
- Null handling for missing colors/normals/intensities

### LAS/LAZ Format

LiDAR data format (LAS 1.0-1.4) with optional compression (.laz).

```csharp
using Aardvark.Data.Points.Import;

var chunks = Laszip.Chunks("scan.laz", ParseConfig.Default);

foreach (var chunk in chunks)
{
    V3d[] positions = chunk.Positions;
    ushort[] intensities = chunk.Intensities;
    byte[] classifications = chunk.Classifications;
    C3b[] colors = chunk.Colors;

    // LiDAR-specific metadata
    byte[] returnNumbers = chunk.ReturnNumbers;
    byte[] numberOfReturnsOfPulses = chunk.NumberOfReturnsOfPulses;
    bool[] scanDirectionFlags = chunk.ScanDirectionFlags;
    bool[] edgeOfFlightLines = chunk.EdgeOfFlightLines;
    double[] gpsTimes = chunk.GpsTimes;
}
```

**Key Features:**
- Wraps laszip.net native library
- Supports both compressed (.laz) and uncompressed (.las)
- Handles LiDAR-specific metadata (return counts, scan direction)
- Efficient chunked reading
- GPS time support

**Dependencies:**
- `Unofficial.laszip.netstandard` package

### PLY Format

Polygon File Format supporting ASCII and binary encodings.

```csharp
using Aardvark.Data.Points.Import;

// High-level import
var chunks = Ply.Chunks("scan.ply", ParseConfig.Default);

// Low-level parsing with Ply.Net
var dataset = PlyParser.Parse("scan.ply", maxChunkSize: 10000);
var plyChunks = Ply.Chunks(dataset, ParseConfig.Default);

foreach (var chunk in chunks)
{
    V3d[] positions = chunk.Positions;  // Required: x, y, z
    C3b[] colors = chunk.Colors;        // Optional: red, green, blue, alpha
    V3f[] normals = chunk.Normals;      // Optional: nx, ny, nz
    int[] intensities = chunk.Intensities;  // Optional: scalar_intensity or intensity
    byte[] classifications = chunk.Classifications;  // Optional: scalar_classification or classification
}
```

**Property Detection:**
- Positions: `x`, `y`, `z` (required, auto-converts from any numeric type)
- Colors: `red`, `green`, `blue`, `alpha` (byte values, auto-scales from float 0.0-1.0)
- Normals: `nx`, `ny`, `nz` (float values)
- Intensities: `scalar_intensity` or `intensity` (auto-scales to 0-255 range)
- Classifications: `scalar_classification` or `classification` (byte values)

**Key Features:**
- Supports ASCII and binary (little/big endian) PLY formats
- Auto-type conversion for numeric properties
- Intensity value rescaling (preserves 0-255 for small values, scales larger ranges)
- Color alpha channel defaults to 255 if missing
- Chunked processing for large files
- Property names are case-insensitive

## Gotchas

### 1. Coordinate System Assumptions

**Problem:** Importers do not perform coordinate system transformations. E57 applies pose transformations, but other formats assume the coordinate system is as-stored.

**Solution:** Apply coordinate transformations manually after import if needed:

```csharp
var pointset = PointCloud.Import("scan.las", config);
var transformed = pointset.Transform(Matrix4x4.FromRotationZ(Math.PI / 2));
```

### 2. Memory Usage with Large Files

**Problem:** Loading entire point clouds into memory can exhaust resources.

**Solution:** Use chunked processing and configure `MaxChunkPointCount`:

```csharp
var config = ImportConfig.Default
    .WithMaxChunkPointCount(32768)  // Smaller chunks
    .WithStorage(PointCloud.CreateInMemoryStore(cache: default));

foreach (var chunk in Laszip.Chunks("huge.laz", ParseConfig.Default))
{
    ProcessChunk(chunk);  // Process one chunk at a time
}
```

### 3. Missing Properties Are Null

**Problem:** Not all formats support all properties (colors, normals, intensities, classifications). Missing properties are `null`, not empty arrays.

**Solution:** Always null-check before accessing:

```csharp
var chunk = chunks.First();
if (chunk.Colors != null)
{
    var avgColor = chunk.Colors.Average(c => c.R);
}
```

### 4. ASCII Format Token Order Matters

**Problem:** Custom ASCII format definitions must match the exact column order in the file.

**Solution:** Verify token sequence matches file layout:

```csharp
// File: "X Y Z R G B I"
var correct = new[] {
    Token.PositionX, Token.PositionY, Token.PositionZ,
    Token.ColorR, Token.ColorG, Token.ColorB,
    Token.Intensity
};

// Wrong order will produce garbage data
var wrong = new[] {
    Token.PositionX, Token.PositionZ, Token.PositionY,  // Y and Z swapped!
    Token.ColorR, Token.ColorG, Token.ColorB,
    Token.Intensity
};
```

### 5. E57 Invalid State Filtering

**Problem:** E57 files can contain invalid points (direction-only or invalid coordinates) marked by `CartesianInvalidState`.

**Solution:** Filter invalid points explicitly:

```csharp
foreach (var chunk in E57.ChunksFull("scan.e57", ParseConfig.Default))
{
    if (chunk.CartesianInvalidState != null)
    {
        var validMask = chunk.CartesianInvalidState
            .Select(state => state == 0)  // 0 = valid
            .ToArray();

        var validPositions = chunk.Positions
            .Where((pos, i) => validMask[i])
            .ToArray();
    }
}
```

### 6. PLY Intensity Rescaling

**Problem:** PLY intensity values are auto-rescaled to 0-255 range, which may lose precision for scientific applications.

**Solution:** Use Ply.Net directly to access raw intensity values:

```csharp
var dataset = PlyParser.Parse("scan.ply", maxChunkSize: 10000);
foreach (var elementData in dataset.Data)
{
    if (elementData.Element.Type == ElementType.Vertex)
    {
        var rawIntensity = elementData["intensity"].Data as double[];
        // Work with raw values instead of rescaled 0-255
    }
}
```

## See Also

- [POINT_CLOUDS.md](POINT_CLOUDS.md) - Point cloud processing and storage
- [ASTM E2807-11](https://www.astm.org/e2807-11.html) - E57 format specification
- [LAS Specification](https://www.asprs.org/divisions-committees/lidar-division/laser-las-file-format-exchange-activities) - LAS file format
- [PLY Format](http://paulbourke.net/dataformats/ply/) - Polygon File Format
- [ply.net](https://github.com/aardvark-platform/ply.net) - PLY parser library
- [laszip.net](https://github.com/aardvark-community/laszip.net) - LAS/LAZ parser wrapper
