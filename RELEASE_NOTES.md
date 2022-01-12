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

