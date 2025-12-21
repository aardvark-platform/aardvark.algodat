# AI Agent Entry Point

Entry point for AI agents working on Aardvark.Algodat.

## Quick Reference

### Essential Commands

| Command | Purpose |
|---------|---------|
| `dotnet tool restore` | Restore local .NET tools |
| `dotnet paket restore` | Restore Paket dependencies |
| `./build.sh` or `.\build.cmd` | Build entire solution (includes tool + paket restore) |
| `.\build.cmd restore` | Restore only (skip build) |
| `dotnet test src/Aardvark.Algodat.Tests` | Run tests |
| `dotnet paket add <package>` | Add Paket dependency |
| `dotnet paket update` | Update Paket dependencies |

### .NET SDK Requirements

- **Required version**: .NET 8.0.0 (see `global.json`)
- **Rollforward policy**: `latestFeature`
- **Prerelease**: Disabled
- **Build targets**: `net8.0`

### Paket Dependency Rules

| Rule | Pattern | Example |
|------|---------|---------|
| Compatible versions | `~> X.Y.Z` | `~> 5.266.0` |
| Exact lowest | `>= X.Y.Z lowest_matching: true` | `>= 8.0.0` |
| Framework | `framework: auto-detect` | auto-detect |
| Storage | `storage: none` | no local storage |

**Critical rules:**
1. NEVER manually edit `.csproj` or `.fsproj` package references
2. ALWAYS use `dotnet paket add <package>` to add dependencies
3. ALWAYS run `dotnet paket restore` after modifying `paket.dependencies`
4. Test dependencies go in `group Test` section with `framework: net8.0`

### Solution Structure

**Main solution**: `src/Aardvark.Algodat.sln` (25 projects)

| Category | Projects |
|----------|----------|
| Point Cloud | Aardvark.Geometry.PointSet, PointTree, Data.Points.Base, Data.Points.Ascii, Data.Points.LasZip, Data.Points.Ply |
| Geometry | Aardvark.Geometry.BspTree, Clustering, Intersection, Normals, PolyMesh |
| Importers | Aardvark.Data.E57, Ply.Net, Unofficial.laszip.netstandard |
| Physics/Geodetics | Aardvark.Physics.Sky, Aardvark.Geodetics (F#) |
| Rendering | Aardvark.Rendering.PointSet (F#) |
| Applications | Apps/Viewer (F#), HeraViewer (F#), heracli (F#), Scratch (C#), ScratchFSharp (F#), ImportTest, Geodetics (F#) |
| Tests | Aardvark.Algodat.Tests |
| Build/Import | import |

### File Ownership

| Permission | Files |
|------------|-------|
| **OWN** (may modify) | `AGENTS.md`, task-specific files |
| **READ** (context only) | All repository files |
| **NO MODIFY** (without permission) | `global.json`, `paket.dependencies`, `*.sln`, `build.sh`, `build.cmd`, `src/` (except task-specific), `.github/workflows/` |

### Common Build Failures

| Error | Symptom | Fix |
|-------|---------|-----|
| Paket not restored | "Paket is not restored" | `dotnet paket restore` |
| SDK version | "SDK version not found" | Install .NET 8.0 SDK or verify `global.json` |
| Tool restore | ".NET tool restore failed" | `dotnet tool restore` before paket restore |
| Project reference | "Project reference not found" | Verify with `dotnet sln src/Aardvark.Algodat.sln list` |
| Version conflicts | Paket dependency conflicts | `dotnet paket why <package>` to trace chains |

### Project Facts

- **Languages**: 18 C# projects + 7 F# projects
- **Design**: Out-of-core (supports datasets larger than RAM)
- **Platforms**: Windows, Linux, macOS
- **Test runner**: `dotnet test` (no `test.sh`/`test.cmd`)
- **Default config**: Release builds
- **Import**: `import.cmd` for data workflows (separate from build)

### Repository Context

Production-quality data structures and algorithms for:
- Out-of-core point cloud management
- N-closest point queries
- Mesh intersection tests
- Point cloud import (E57, LasZip, PLY)
- Sky models and astronomical calculations

Part of [Aardvark Platform](https://github.com/aardvark-platform) for visual computing and real-time graphics.

---

**For detailed AI agent documentation**, see `ai/README.md`.

**Last updated**: 2025-12-21
