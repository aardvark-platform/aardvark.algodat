# Point Cloud Rendering System

## Purpose

This document describes the point cloud rendering architecture in `Aardvark.Rendering.PointSet`. The system implements a deferred rendering pipeline with advanced features including sphere-based point splats, SSAO, plane fitting, and LOD-aware rendering.

## Core Rendering Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `LodTreeSceneGraph` | `LodTreeSceneGraph.fs` | Deferred rendering pipeline, point splatting, depth readback |
| `LodTreeInstance` | `LodTreeInstance.fs` | Scene graph node wrapper, data loading, attribute management |
| `SSAO` | `SSAO.fs` | Screen-space ambient occlusion (HBAO implementation) |
| `FXAA` | `FXAA.fs` | Fast approximate anti-aliasing |
| `SimplePick` | `SimplePick.fs` | Point picking via BVH traversal and region queries |
| `PointSetShaders` | `LodTreeInstance.fs:1046-1507` | Vertex/fragment shaders for point rendering |

## Integration with Aardvark Rendering Pipeline

### Scene Graph Integration

```fsharp
// Create render configuration
let renderConfig : PointSetRenderConfig = {
    runtime = win.Runtime
    viewTrafo = viewTrafo
    projTrafo = projTrafo
    size = win.Sizes
    colors = AVal.init true
    pointSize = AVal.init 2.25
    planeFit = AVal.init true
    planeFitTol = AVal.constant 0.009
    planeFitRadius = AVal.constant 7.0
    ssao = AVal.init true
    diffuse = AVal.init true
    gamma = AVal.init 1.4
    lodConfig = { /* ... */ }
    ssaoConfig = { /* ... */ }
    pickCallback = None
}

// Render point clouds
let sg = Sg.pointSets renderConfig instances
```

### Rendering Stages

**Stage 1: Point Rasterization** (`LodTreeSceneGraph.fs:822-903`)
- Renders points to offscreen buffers (color + depth)
- Uses `lodPointSizeSimple` + `lodPointSphereSimple` shaders
- Outputs: `TextureFormat.Rgba8` color, `TextureFormat.Depth24Stencil8` depth

**Stage 2: Normal Estimation** (`LodTreeSceneGraph.fs:924-959`)
- Applies plane fitting for normal computation
- Computes lighting from estimated normals
- Uses `blitPlaneFit` shader with PCA-based plane fitting

**Stage 3: SSAO** (`SSAO.fs:507-685`)
- Horizon-based ambient occlusion (HBAO)
- Bilateral blur with depth awareness
- Outputs occlusion term multiplied by lit colors

**Stage 4: Composition** (`SSAO.fs:483-505`)
- FXAA anti-aliasing
- Gamma correction
- Final output to framebuffer

## Usage Patterns

### Basic Point Cloud Rendering

```fsharp
// Load point cloud
let instance =
    LodTreeInstance.load "mycloud" "key" "store.uds" []
    |> Option.get

// Create instances set
let instances = ASet.single instance

// Render
let renderConfig = { /* see configuration above */ }
let sg = Sg.pointSets renderConfig instances
```

### LOD Configuration

```fsharp
let lodConfig : LodTreeRenderConfig = {
    time = win.Time                      // Adaptive updates
    renderBounds = AVal.init false       // Debug bounding boxes
    stats = AVal.init Unchecked.defaultof<_>
    pickTrees = Some pickTreeCache       // Enable picking
    alphaToCoverage = false
    maxSplits = AVal.init 8              // Max LOD splits per frame
    splitfactor = AVal.init 0.1          // Angular threshold (degrees)
    budget = AVal.init -(256L <<< 10)    // Negative = point count limit
}
```

### SSAO Configuration

```fsharp
let ssaoConfig : SSAOConfig = {
    radius = AVal.init 0.04              // Sample radius in view space
    threshold = AVal.init 0.1            // Depth difference threshold
    sigma = AVal.init 5.0                // Blur sigma (pixels)
    sharpness = AVal.init 4.0            // Depth-aware blur sharpness
    sampleDirections = AVal.init 2       // Directional samples (1-32)
    samples = AVal.init 4                // Samples per direction (1-32)
}
```

### Point Picking

```fsharp
// Setup picking callback
let pick = ref (fun _ _ _ -> [||])
let renderConfig = { renderConfig with pickCallback = Some pick }

// Use after rendering
let pickPoint (pixel : V2i) =
    let radius = 20              // Pixel radius
    let maxPoints = 800          // Max points to return
    let points = pick.Value pixel radius maxPoints

    // Process picked points
    points |> Array.iter (fun pt ->
        printfn "World: %A, View: %A, Ndc: %A"
            pt.World pt.View pt.Ndc
    )
```

### Shader Customization

```fsharp
// Example: Custom point visualization
let customVis =
    Sg.pointSets renderConfig instances
    |> Sg.uniform "PointVisualization"
        (AVal.constant (PointVisualization.Color |||
                       PointVisualization.Lighting |||
                       PointVisualization.Antialias))
    |> Sg.uniform "MagicExp" (AVal.init 1.0)  // Point size scaling exponent
```

## Advanced Features

### Deferred Point Splatting

The system renders point sprites as oriented disks in screen space:

1. **Vertex Shader** (`lodPointSizeSimple`, line 197): Projects point, calculates screen-space radius
2. **Fragment Shader** (`lodPointSphereSimple`, line 229): Computes sphere depth offset via `sqrt(1.0 - r²)`
3. Depth modification enables proper occlusion between overlapping points

### Plane Fitting

Optional surface reconstruction via local PCA (`blitPlaneFit`, line 748):

- Samples 24 neighboring points within `planeFitRadius`
- Fits plane using covariance matrix eigendecomposition
- Adjusts depth to plane intersection for smoother surfaces
- Tolerance `planeFitTol` filters outliers

### Adaptive LOD

LOD decisions based on angular size (`equivalentAngle60`, line 384):

```
angle = 60° * (avgPointDistance / minDistance) / fov
split if angle > splitfactor / quality
```

## Gotchas

| Issue | Symptom | Solution |
|-------|---------|----------|
| **Large point size artifacts** | Points clipped at viewport edges | System expands viewport by `max(32, pointSize)` pixels. Increase if clipping persists. |
| **Plane fit introduces holes** | Black pixels where plane fit fails | Reduce `planeFitRadius` or increase `planeFitTol`. Disable with `planeFit = false`. |
| **SSAO over-darkens** | Excessive darkening in cavities | Reduce `ssaoConfig.radius` or `samples`. Typical values: radius 0.02-0.06. |
| **LOD pop-in** | Visible transitions during LOD changes | Increase `budget` (more points), decrease `splitfactor` (finer LOD). |
| **Slow performance** | Low FPS with large datasets | Set `budget` to negative value (point count limit). Start with `-262144` (256K points). |
| **Picking returns no points** | Empty array from pick callback | Ensure `pickTrees = Some cmap()` in `lodConfig`. Check radius and maxPoints values. |
| **Depth readback fails** | Picking crashes on older GPUs | Compute shader readback requires OpenGL 4.3+. Fallback path exists but may be slower. |

## Performance Tuning

### Point Size vs Performance
- Larger point sizes increase fill rate
- Threshold: Performance degrades beyond ~20 pixels/point
- Use `lodConfig.budget` to maintain frame rate

### SSAO Cost
- Dominant factor: `samples * sampleDirections`
- Conservative values: 2 directions × 4 samples = 8 total
- Aggressive values: 8 directions × 8 samples = 64 total (expensive)

### LOD Overhead
- `maxSplits` controls LOD updates per frame
- Higher values = smoother LOD but more CPU overhead
- Recommended: 6-12 for interactive applications

## Memory Management

Point data loaded on-demand via `LruDictionary` cache (default 1 GB):

```fsharp
let cache = LruDictionary(1L <<< 30)  // 1 GB cache
let store = PointCloud.OpenStore(path, cache)
```

Release memory explicitly when done:

```fsharp
node.Release()  // Releases node and children from cache
```

## See Also

- [POINT_CLOUDS.md](POINT_CLOUDS.md) - Point cloud data structures, LOD trees
- [IMPORTERS.md](IMPORTERS.md) - Import/export formats
- Aardvark.Rendering - Core scene graph and rendering abstractions
- Aardvark.SceneGraph - Scene graph combinators and uniform management
- FShade - Shader composition framework used for effect definitions
