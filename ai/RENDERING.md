# Point Cloud Rendering

`Aardvark.Rendering.PointSet` integrates stored point clouds with Aardvark scene graphs. It provides LOD point splats, optional plane fitting and lighting, SSAO, antialiasing, and picking. This repository consumes the core rendering and scene-graph packages; it does not define them.

## Integration

The host supplies a `PointSetRenderConfig` containing its runtime, adaptive viewport size, view/projection transforms, display settings, and LOD/SSAO configuration. See the [record definition](../src/Aardvark.Rendering.PointSet/LodTreeSceneGraph.fs) and the viewer's [complete configuration](../src/Apps/Viewer/Rendering.fs).

Given that configuration and an existing store/key:

```fsharp
open FSharp.Data.Adaptive
open Aardvark.Rendering.PointSet

let tryScene (config : PointSetRenderConfig) (key : string) (path : string) =
    match LodTreeInstance.load "cloud" key path [] with
    | Some instance -> Some (Sg.pointSets config (ASet.single instance))
    | None -> None
```

`LodTreeInstance.load` takes a source name, key, store path, and uniform list. Handle `None`; loading failures may also throw. If you already own a `PointSet`, use `LodTreeInstance.ofPointSet uniforms partIndexOffset pointSet` rather than reopen its store.

## Configuration

| Settings | Purpose |
|----------|---------|
| `colors`, `pointSize` | Stored colors and screen-space point size |
| `planeFit`, `planeFitRadius`, `planeFitTol` | Surface fitting and its neighborhood/tolerance |
| `diffuse`, `ssao`, `gamma` | Lighting, ambient occlusion, output correction |
| `ssaoConfig` | `radius`, `threshold`, `sigma`, `sharpness`, `sampleDirections`, `samples` |
| `lodConfig` | Adaptive time, bounds display, statistics, picking trees, `budget`, `splitfactor`, `maxSplits`, and alpha-to-coverage |

These settings are adaptive except where the record specifies otherwise. Update changeable values inside `transact`. Smaller `splitfactor` requests finer LOD; point size and SSAO sampling also affect rendering cost. Measure with the target data, viewport, and GPU rather than treating the viewer's values as universal recommendations.

The renderer forwards `LodTreeRenderConfig` scheduling and budget settings to Aardvark.Rendering. Check the resolved package's semantics when changing those controls; they are not defined by this repository's point-storage API.

## Picking

`pickCallback` is an optional reference to a function of type `V2i -> int -> int -> PickPoint[]`. Install it before constructing the scene graph:

```fsharp
open Aardvark.Base

let withPicking (config : PointSetRenderConfig) =
    let pick = ref (fun (_ : V2i) (_ : int) (_ : int) -> Array.empty<PickPoint>)
    { config with pickCallback = Some pick }, pick
```

Render using the returned configuration. After rendering, call `pick.Value pixel radius maxPoints`, where the radius is in pixels. Results contain `World`, `View`, `Ndc`, and `Pixel` coordinates. This callback reads rendered depth; it is distinct from the LOD picking trees used by [SimplePick](../src/Aardvark.Rendering.PointSet/SimplePick.fs).

## Resource Ownership

Keep the underlying store alive while the scene graph may request point data. A custom cache is supplied when opening the store; see [point-cloud storage](POINT_CLOUDS.md#create-store-and-load). `PointTreeNode.Release()` is part of the LOD lifecycle, not a promise to dispose the store or evict every cached point array. Tear down rendering users before disposing storage you own.

## Source Navigation

- [LodTreeInstance](../src/Aardvark.Rendering.PointSet/LodTreeInstance.fs): loading, LOD nodes, point attributes and shaders
- [LodTreeSceneGraph](../src/Aardvark.Rendering.PointSet/LodTreeSceneGraph.fs): scene integration and depth picking
- [SSAO](../src/Aardvark.Rendering.PointSet/SSAO.fs), [FXAA](../src/Aardvark.Rendering.PointSet/FXAA.fs): post-processing
- [Importers](IMPORTERS.md): preparing stored datasets
