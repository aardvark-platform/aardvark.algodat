### 5.3.1
- updated dependencies (System.Collections.Immutable and System.Text.Json lowest_matching: true)

### 5.3.0
- merge "PartIndex" branch into master

### 5.3.0-prerelease015
- changed System.Collections.Immutable and System.Text.Json to '>= 6.0.0' (from '~> 6.0.0')
- updated dotnet-tools

### 5.3.0-prerelease014
- merged interop branch ...
- enable interop with other software
  - moved IPointCloudNode to Aardvark.Data.Points.Base
  - moved PersistentRef to Aardvark.Data.Points.Base (dependency)
  - aligned Aardvark.Geometry.PointTree license to Aardvark.Data.Points.Base license (AGPL -> Apache2.0)

### 5.3.0-prerelease013
- PartIndices ImmutableMerge fix

### 5.3.0-prerelease012
- structured point clouds: filtered nodes
- structured point clouds: chunk TryGetPartIndices

### 5.3.0-prerelease011
- updated base packages to 5.2.28

### 5.3.0-prerelease010
- structured point clouds implementation (prerelease, for testing only)

### 5.3.0-prerelease009
- structured point clouds rendering (prerelease, for testing only)

### 5.3.0-prerelease008
- fix part index handling in JoinNonOverlappingTrees (prerelease, for testing only)

### 5.3.0-prerelease007
- structured point clouds query (prerelease, for testing only)

### 5.3.0-prerelease006
- structured point clouds delete (prerelease, for testing only)

### 5.3.0-prerelease005
- structured point clouds rendering (prerelease, for testing only)

### 5.3.0-prerelease004
- structured point clouds rendering (prerelease, for testing only)

### 5.3.0-prerelease003
- structured point clouds implementation (prerelease, for testing only)

### 5.3.0-prerelease002
- structured point clouds implementation (prerelease, for testing only)

### 5.3.0-prerelease001
- structured point clouds implementation (prerelease, for testing only)

### 5.2.28
- updated base packages to 5.2.28 (LinearRegression missing method hotfix)

### 5.2.27
- [LodTreeInstance] intersect view frustum depth range to -1 +1

### 5.2.26
- [LodTreeInstance] ShouldSplit vfc

### 5.2.25
- [E57 parser] do not transform normals if there are no normals
- 
### 5.2.24
- [E57 parser] transform normals if E57Data3D.Pose is defined

### 5.2.23
- [SimplePickTree] fixed attribute index

### 5.2.22
- add real root node to InlinedNodes to allow tools to access native octree

### 5.2.21
- [IPointCloudNode] DeleteWithClassification

### 5.2.20
- [SimplePickTree] SimplePickPoint records generic attributes

### 5.2.19
- update Aardvark.Base to 5.2.24
- Chunk.ToGenericChunk now forces arrays

### 5.2.18
- removed net70 references
    - System.Collections.Immutable ~> 6.0.0
    - System.Text.Json ~> 6.0.0

### 5.2.17
- update package dependencies and tools
- fix unit tests (mismatched property counts in Chunk no longer throw)

### 5.2.16
- removed unused code
- pointcloud import: gracefully fixing mismatched property counts

### 5.2.15
- fix exception in Queries.QueryCell
- Improve ray - polymesh intersection functions
  - renames TryGetRayIntersection() to Intersects()
  - adds explicit parameters for ray interval
  - adds more detailed comments
  - Fixes Intersects() for negative t values
  - see: https://github.com/aardvark-platform/aardvark.algodat/pull/24
- PolyMesh.Contains
- ray-polymesh intersections
- update to Aardvark.Rendering 5.3
- add missing Aardvark.Build references to fix assembly versions

### 5.2.14
- update packages (Aardvark.Data.Durable 0.3.8)
- fix various warnings

### 5.2.13
- StorageExtensions.UnGZip checks whether buffer is actually gzipped
- E57 checksum verification is now switched off by default;
  `E57.Chunks` and `E57ChunksFull` now have overloads to opt-in
- fix laszip parser for non-seekable streams

### 5.2.12
- update packages (especially Aardvark.Data.Durable 0.3.4)
- fix GenericChunk.NormalsSupportedDefs copy/paste bug

### 5.2.11
- GenericChunk.Subset is now public
- fix Laszip.Parser.ReadInfo (for LAS 1.4)

### 5.2.10
- LASzip importer now supports LAS 1.4
- fix GenericChunk.Empty
- add GenericChunk.Split

### 5.2.9
- fixed ConvertToInline:rescaleIntensities, which failed for empty cells

### 5.2.8
- [PointSet]added QueriesDirectedRay3d.cs

### 5.2.7
- updated dependency (SimpleStore)

### 5.2.6
- fix EnumerateOctreeInlined 
  - undefined behaviour if same enumerator was run multiple times in parallel
  - empty enumeration after first run (when run sequentially)
  - root node has been duplicated in some cases

### 5.2.5
- E57FileHeader: fix leaking file handle
  https://github.com/aardvark-platform/aardvark.algodat/pull/23
  https://github.com/aardvark-platform/aardvark.algodat/issues/22

### 5.2.4
- added Aardvark.Data.Wavefront
- ply importer: fixed intensity rescale
- ply importer: added support for per-vertex classification property ("scalar_Classification", "classification") and alpha color channel ("alpha")
- InlineNode now also handles classifications and intensities where intensities are converted from int32 to uint8
- fixed paket.template files 

### 5.2.3
- add PLY parser and ply point cloud importer 

### 5.2.2
- E57: add PointPropertySemantics.Classification (not part of E57 specification)
- ported C# code to latest language version

### 5.2.1
- tests: deterministic initialization of random generator
- fix problem with importing uint[] intensities in a .net-framework application,
  see https://github.com/vrvis/Vgm.Api/issues/32
- fix github workflows, dotnet-version 6.0.100 -> 6.0.103
- update net5.0 -> net6.0
- added net472 target to test project
- fix missing entry for FilterInsideConvexHulls3d in Filter.Deserialize
- add serialization tests for FilterHulls3d
- remove unused code from ObsoleteNodeParser.cs

### 5.2.0
- Updated to Aardvark 5.2

### 5.1.34
- [IPointCloudNode] FilterConvexHulls3d: polygon is CCW

### 5.1.33
- [IPointCloudNode] FilterConvexHulls3d: bugfixes, argument checks

### 5.1.32
- [IPointCloudNode] FilterConvexHulls3d

### 5.1.31
- [IPointCloudNode] FilterPrism3d

### 5.1.30
- [paket] Added missing entries in paket.references (System.Collections.Immutable, FShade)

### 5.1.29
- [PointTreeNode] Prevent reads from disposed store

### 5.1.28
- removed Newtonsoft.Json dependency

### 5.1.27
- restrict FSharp.Core to >= 5.0.0 lowest_matching: true
- update Uncodium.SimpleStore to 3.0.10

### 5.1.26
- more package updates

### 5.1.25
- ... and now also completed Martin's *Update to FSharp.Core >= 5.0.0* ;-)

### 5.1.24
- merged martin's branch (Update to FSharp.Core >= 5.0.0)

### 5.1.23
- e57: expose all available point properties (via E57.ChunksFull)
- e57: add support for reading per-point normals, which is an e57 extension
- laszip: synced Aardvark.Data.Points.LasZip project from original code base at https://github.com/stefanmaierhofer/LASzip
  parser now tries to recover colors which have not been normalized according to spec

### 5.1.22
- [ILodTreeNode] shouldSplit only inside ortho frustum 

### 5.1.21
- update dependencies

### 5.1.20
- merged inline fix

### 5.1.19
- enforcing more octree invariants (points on or near border)

### 5.1.18
- update Aardvark.Base 5.1.20
- fixed octree merge

### 5.1.17
- update Aardvark.Base to 5.1.19
  - fixes cell construction from boxes (a little more) introduced by log2int fix

### 5.1.16
- updated build script
- more twilight time calculations
- fixed intensity distribution of LightMeasurementData when importing LDTs with absolute measurements
- cleanup fix + validation
- PointSetNode: add support for Durable.Octree.Colors3b and Durable.Octree.Colors3bReference
  - if no C4b colors exists, then C3b colors will be used if available (on-the-fly conversion)
- paket update (to get rid of missing method exceptions)
- EnumerableExtensions.MapParallel no longer swallows exceptions
- update Aardvark.Base to 5.1.18
  - fixes cell construction from boxes introduced by log2int fix
### 5.1.15
- fixed `IPointCloudNode.ConvertToInline(...)`,
  which failed to include subnode-ids for inner nodes in the non-collapse case

### 5.1.14
- updated aardvark libraries (base 5.1.12, rendering 5.1.14)
- improved clustering performance using ValuesWithKeyEnumerator
- added typed PlaneEpsilonClustering version

### 5.1.12
- fix EnumerateOctreeInlined (subnode guids of inlined inner nodes may be dropped in some cases)

### 5.1.11
- updated packages

### 5.1.10
- Fix for partially written results in octree inlining when using dotnet framework (net48).
  GZipStream will not write to completion (ignores Flush) unless Close() is called explicitely.
  In netcore this works as expected, i.e. Flush() is not ignored.
  
### 5.1.9
- rewrite octree inlining on top of IPointCloudNode to be generally applicable (e.g. also for FilteredNode), see https://github.com/aardvark-platform/aardvark.algodat/issues/16
- fix CountNodes() for FilteredNode, see https://github.com/aardvark-platform/aardvark.algodat/issues/15

### 5.1.8
- improved Chunk.ImmutableMerge null-safety
- update to Uncodium.SimpleStore 3.0.0-preview.14 and adapt PointCloud.OpenStore to different SimpleStore variants
- pointcloudviewer depth workaround

### 5.1.7
- updated dependency Uncodium.Simplestore 3.0

### 5.1.6
- Merge branch 'master' into v51

### 5.1.5
- updated Aardvark.Base
- updated Aardvark.Rendering (breaking changes)
- updated FShade

### 5.1.2
- udpated packages

### 5.1.1
- updated to FSharp.Data.Adaptive 1.1 and base 5.1 track

