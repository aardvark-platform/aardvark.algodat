### 5.1.14
- updated aardvark libraries (base 5.1.12, rendering 5.1.14)
- improved clustering performance using ValuesWithKeyEnumerator
- added typed PlaneEpsilonClustering version

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

