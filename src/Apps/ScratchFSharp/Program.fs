namespace ScratchFSharp

open System
open System.IO
open Aardvark.Base
open Aardvark.Data.Points
open Aardvark.Data.Points.Import
open Aardvark.Geometry.Points
open Aardvark.Data
open Aardvark.Algodat.App.Viewer
open Aardvark.Rendering
open Aardvark.Rendering.PointSet
open FSharp.Data.Adaptive
open Aardvark.Application.Slim
open SamSharp
open Aardvark.Application
open Aardvark.SceneGraph
open Microsoft.FSharp.NativeInterop

#nowarn "9"
module Bla =
    let heatMapColors =
        let fromInt (i : int) =
            C4b(
                byte ((i >>> 16) &&& 0xFF),
                byte ((i >>> 8) &&& 0xFF),
                byte (i &&& 0xFF),
                255uy
            ).ToC4f()

        Array.map fromInt [|
            0x1639fa
            0x2050fa
            0x3275fb
            0x459afa
            0x55bdfb
            0x67e1fc
            0x72f9f4
            0x72f8d3
            0x72f7ad
            0x71f787
            0x71f55f
            0x70f538
            0x74f530
            0x86f631
            0x9ff633
            0xbbf735
            0xd9f938
            0xf7fa3b
            0xfae238
            0xf4be31
            0xf29c2d
            0xee7627
            0xec5223
            0xeb3b22
        |]

    let heat (c : float) =
        let c = clamp 0.0 1.0 c
        let fid = c * float heatMapColors.Length - 0.5

        let id = int (floor fid)
        if id < 0 then
            heatMapColors.[0]
        elif id >= heatMapColors.Length - 1 then
            heatMapColors.[heatMapColors.Length - 1]
        else
            let c0 = heatMapColors.[id]
            let c1 = heatMapColors.[id + 1]
            let t = fid - float id
            (c0 * (1.0f - float32 t) + c1 * float32 t)

    
    module Shader =
        open FShade
        
        type UniformScope with
            member x.SegmentationCenter : V3d = uniform?SegmentationCenter
            member x.SegmentationPartIndex : int = uniform?SegmentationPartIndex
            
        let masky =
            sampler2d {
                texture uniform?SegmentationMask
                addressU WrapMode.Clamp
                addressV WrapMode.Clamp
                filter Filter.MinMagMipPoint
            }
        
        type Vertex =
            {
                [<Position>] pos : V4d
                [<Semantic("PartIndices")>] idx : int
                [<Color>] c : V4d
            }
        let segmented (v : Vertex) =
            vertex {
                let mutable c = v.c
                if v.idx = uniform.SegmentationPartIndex then
                    let pt = v.pos.XYZ - uniform.SegmentationCenter
                    
                    let d = Vec.length pt
                    let phi = Constant.Pi - atan2 pt.Y pt.X
                    let theta = asin (pt.Z / d)
                    
                    let rx = phi / Constant.PiTimesTwo
                    let ry = (theta + Constant.PiHalf) / Constant.Pi
                    
                    if masky.SampleLevel(V2d(rx, ry), 0.0).X > 0.5 then c <- V4d.IOOI
                    
                return { v with c = c }
                
            }
    
    [<EntryPoint>]
    let main a =
        
        let ensure storepath =
            if not (Directory.Exists storepath) then Directory.CreateDirectory storepath |> ignore
            storepath
                
                
        let inputs =
                [
                    @"D:\Clouds\unbereinigt\Bf Tapfheim- 001.e57"
                    @"D:\Clouds\unbereinigt\Bf Tapfheim- 002.e57"
                    @"D:\Clouds\unbereinigt\Bf Tapfheim- 003.e57"
                    @"D:\Clouds\unbereinigt\Bf Tapfheim- 004.e57"
                    @"D:\Clouds\unbereinigt\Bf Tapfheim- 005.e57"
                    @"D:\Clouds\unbereinigt\Bf Tapfheim- 006.e57"
                    @"D:\Clouds\unbereinigt\Bf Tapfheim- 007.e57"
                    @"D:\Clouds\unbereinigt\Bf Tapfheim- 008.e57"
                    @"D:\Clouds\unbereinigt\Bf Tapfheim- 009.e57"
                ]
        let outdir = @"D:\stores\bahn" |> ensure
        let storepath = Path.combine [outdir; "store"] |> ensure
        let panospath = Path.combine [outdir; "panos"] |> ensure
        let centerspath = Path.combine [outdir;"centers.txt"]
        
        let store = PointCloud.OpenStore(Path.combine [storepath; "data.uds"])
        let cloud =
            if File.Exists centerspath then
                PointCloud.Load("a",store)
            else 
                let queue = new System.Collections.Concurrent.BlockingCollection<Chunk>()
                let key = "a"
                let config =
                    ImportConfig.Default
                        .WithStorage(store)
                        .WithKey(key)
                        .WithVerbose(true)
                try File.writeAllText (Path.combine [storepath; "key.txt"]) key with e -> Log.error "%A" e
                let mutable result = None
                let thread =
                    startThread (fun _ ->
                        result <- Some (PointCloud.Chunks(queue.GetConsumingEnumerable(),config))    
                    )
                
                let mutable partIndex = 0
                let centers = ResizeArray()
                for file in inputs do
                    Log.line $"file {file}"
                    let actualSize = FileInfo(file).Length
                    use s = File.OpenRead file
                    let info = Aardvark.Data.E57.ASTM_E57.E57FileHeader.Parse(s, actualSize, false)
                        
                    let mutable i = 0
                    for data in info.E57Root.Data3D do
                        
                        if not (isNull data.Pose) then
                            let name = $"{data.Guid}_{i}"
                            let center = data.Pose.Translation
                            centers.Add(center)
                            // let w = data.IndexBounds.Column.Value.Max - data.IndexBounds.Column.Value.Min
                            // let h = data.IndexBounds.Row.Value.Max - data.IndexBounds.Row.Value.Min
                            let size = V2i(5000, 2500)
                            //assert(size.X > 0 && size.Y > 0)
                            let dimg = PixImage<float32>(Col.Format.Gray, size)
                            let mutable dmat = dimg.GetChannel(0L)
                            dmat.Set(System.Single.PositiveInfinity) |> ignore
                            
                            let chunks =
                                data.StreamPointsFull(1 <<< 28, false, System.Collections.Immutable.ImmutableHashSet.Empty)
                                |> Seq.map (fun struct(a,b) -> E57.E57Chunk(b, data, a))
                                
                            let mutable maxDepth = 0.0
                            for c in chunks do
                                
                                let ac = 
                                    Chunk(
                                        c.Positions,
                                        c.Colors |> Array.map C4b,
                                        null,
                                        null,
                                        null,
                                        partIndex,
                                        Range1i(partIndex,partIndex),
                                        Unchecked.defaultof<_>
                                    )
                                queue.Add(ac)
                                   
                                for i in 0 .. c.Count - 1 do
                                    let pt = c.Positions.[i] - center
                                    
                                    let d = Vec.length pt
                                    let phi = Constant.Pi - atan2 pt.Y pt.X
                                    let theta = asin (pt.Z / d)
                                    
                                    let rx = phi / Constant.PiTimesTwo
                                    let ry = (theta + Constant.PiHalf) / Constant.Pi
                                    
                                    let pos = V2i(int32 (rx * float dimg.Size.X), int32 ((1.0 - ry) * float dimg.Size.Y))
                                    dmat.[pos] <- float32 d
                                    maxDepth <- max maxDepth d
                                    
                            let dimg2 = PixImage<byte>(Col.Format.RGBA, dimg.Size)
                            dimg2.GetMatrix<C4b>().SetMap(dmat, fun d ->
                                if float d > maxDepth then C4b.Black
                                else heat(sqrt (float d / maxDepth)).ToC4b()
                            ) |> ignore
                            dimg2.SaveImageSharp (Path.Combine(panospath, $"%03d{partIndex}.png"))
                            
                            use srcPtr = fixed dimg.Volume.Data
                            let sizeInBytes = size.X * size.Y * sizeof<float32>
                            let src = System.Span<byte>(NativePtr.toVoidPtr srcPtr, sizeInBytes)
                            let dst = Array.zeroCreate<byte> sizeInBytes
                            src.CopyTo(dst)
                            File.WriteAllBytes(Path.Combine(panospath, $"%03d{partIndex}.bin"), dst)
                            
                            partIndex <- partIndex + 1
                        i <- i + 1

                try File.writeAllLines centerspath (centers |> Seq.toArray |> Array.map (fun v -> v.ToString("0.00000"))) with e -> Log.error "%A" e
                queue.CompleteAdding()
                thread.Join()
                result.Value
        
        
        let root = cloud.Root.Value
        
        let bounds = root.Cell.BoundingBox

        let trafo = Similarity3d(1.0, Euclidean3d(Rot3d.Identity, -bounds.Center))
        let source = Symbol.Create "sourceName"
        let root = Aardvark.Rendering.PointSet.LodTreeInstance.PointTreeNode.Create(System.Guid.NewGuid(), null, store.Cache, source, trafo, None, None, 0, 0, root) 
        Aardvark.Init()
        
        PixImageSharp.Init()
        use app = new OpenGlApplication(true, false)
        use win = app.CreateGameWindow(8)
        let pick = ref (fun _ _ _ -> [||])
        
        let parts = cloud.PartIndexRange.Value
        let panos =
            Map.ofArray [|
                for part in parts.Min .. parts.Max do
                    let file = Path.Combine(panospath, sprintf "%03d.png" part)
                    let img = PixImage.Load file
                    yield part, img.ToPixImage<byte>()
            |]
            
        let depthImages =
            panos |> Map.map (fun part p ->
                let img = PixImage<float32>(Col.Format.Gray, p.Size)
                let file = Path.Combine(panospath, sprintf "%03d.bin" part)
                use ptr = fixed (File.ReadAllBytes file)
                let dst = img.Volume.Data
                let srcSpan = System.Span<float32>(NativePtr.toVoidPtr ptr, dst.Length)
                srcSpan.CopyTo(dst)
                img 
            )
            
            
        let sam = new Sam() 
        // let sams =
        //     panos |> Map.map (fun _ p ->
        //         sam.BuildIndex p
        //     )
        //     
        let centers =
            Map.ofArray [|
                let centers =
                    File.ReadAllLines(centerspath)
                    |> Array.map (fun l -> V3d.Parse l)
                for part in parts.Min .. parts.Max do
                    yield part, centers.[part]
                
            |]
            
        let trySamplePano (cloudCenter : V3d) (viewProj : Trafo3d) (pick : V3d) (part : int) =
            let cloudCenter = ()
            match Map.tryFind part centers, Map.tryFind part depthImages with
            | Some center, Some pano ->
                
                
                let pp = viewProj.Forward.TransformPosProj pick
                let ppx =
                    if pp.X < 0.0 then V3d(1.0, pp.Y, pp.Z)
                    else V3d(-1.0, pp.Y, pp.Z)
                    
                let ppy =
                    if pp.Y < 0.0 then V3d(pp.X, 1.0, pp.Z)
                    else V3d(pp.X, -1.0, pp.Z)
                
                let rx = Vec.distance pick (viewProj.Backward.TransformPosProj(ppx))
                let ry = Vec.distance pick (viewProj.Backward.TransformPosProj(ppy))
                let radius = max rx ry * 1.5
                
                printfn "radius: %A" radius
                
                let diff = center - pick
                let distance = Vec.length diff
                let z = diff / distance
                let sky = V3d.OOI
                let x = Vec.cross sky z |> Vec.normalize
                let y = -Vec.cross z x |> Vec.normalize
                let t = Trafo3d.FromBasis(x, y, z, pick)
                
                
                
                
                let getTC (pos : V3d) =
                    let pt = pos - center
                    let d = Vec.length pt
                    let phi = Constant.Pi - atan2 pt.Y pt.X
                    let theta = asin (pt.Z / d)
                    let rx = phi / Constant.PiTimesTwo
                    let ry = (theta + Constant.PiHalf) / Constant.Pi
                    V2d(rx, 1.0 - ry)
                    
                let getCoord (c : V2d) =
                    let ndc = 2.0 * c - V2d.II
                    let tc = t.TransformPos((ndc*radius).XYO) |> getTC
                    V2i (tc * V2d pano.Size)
                    
                let img = PixImage<float32>(Col.Format.Gray, V2i(1024, 1024))
                let m = img.GetChannel 0L
                let rcpSize = 1.0 / V2d img.Size
                let panoMat = pano.GetChannel(0L)
                let mutable range = Range1f.Invalid
                m.SetByCoord (fun (c : V2l) ->
                    let px = (V2d c + V2d.Half) * rcpSize |> getCoord
                    // let cx = c + rcpSize.XO |> getCoord
                    // let cy = c + rcpSize.OY |> getCoord
                    //
                    let v = panoMat.[px]
                    if v < System.Single.PositiveInfinity then
                        range <- range.ExtendedBy v
                    v
                ) |> ignore
                    
                let colorImg = PixImage<byte>(Col.Format.RGBA, img.Size)
                colorImg.GetMatrix<C4b>().SetMap(m, fun v ->
                    if v < System.Single.PositiveInfinity then
                        let r = (v - range.Min) / range.Size |> float
                        heat(r).ToC4b()
                    else
                        C4b.Black
                ) |> ignore
                
                let img = colorImg
                //     
                // img.SaveAsPng @"D:\crop.png"
                    
                let idx = sam.BuildIndex img
                let res = idx.Query [Query.Point(img.Size / 2, 1)]
                // let maskImg = PixImage<byte>(Col.Format.RGBA, res.Size)
                // let maskMat = maskImg.GetMatrix<C4b>()
                // maskMat.SetMap(res, fun res ->
                //     C4f(C3f.Red * res, 1.0f).ToC4b()    
                // ) |> ignore
                // maskImg.SaveAsPng @"D:\cropMask.png"
                
                
                let pano = PixImage<byte>(Col.Format.Gray, pano.Size)
                let mutable panoMat = pano.GetChannel(0L)
                let pCenter = t.Backward.TransformPos center
                
                panoMat.SetByCoord(fun (c : V2l) ->
                    let tc = (V2d c + V2d.Half) / V2d panoMat.Size
                    
                    // let d = Vec.length pt
                    // let phi = Constant.Pi - atan2 pt.Y pt.X
                    // let theta = asin (pt.Z / d)
                    // let rx = phi / Constant.PiTimesTwo
                    // let ry = (theta + Constant.PiHalf) / Constant.Pi
                    //
                    
                    let phi = -(tc.X * Constant.PiTimesTwo - Constant.Pi)
                    let theta = (1.0 - tc.Y) * Constant.Pi - Constant.PiHalf
                    
                    let ct  = cos theta
                    let dir = V3d(cos phi * ct, sin phi * ct, sin theta)
                    
                    let pDir = t.Backward.TransformDir dir
                    
                    // pCenter.Z + t*pDir.Z = 0
                
                    let t = -pCenter.Z / pDir.Z
                    let pPos = pCenter + t * pDir
                    if t >= 0.0 && pPos.X >= -radius && pPos.X <= radius && pPos.Y >= -radius && pPos.Y <= radius then
                        let ndc = pPos.XY / radius
                        let px = 0.5 * (ndc + V2d.II) * V2d res.Size |> V2i
                        let m = res.[px]
                        byte (255.0f * m)
                    else
                        0uy
                    
                ) |> ignore
                
                
                // res.ForeachXYIndex(fun (x : int64) (y : int64) (i : int64) ->
                //     let m = res.[i]
                //     let c = (V2d(x,y) + V2d.Half) / V2d maskMat.Size
                //     let ndc = 2.0 * c - V2d.II
                //     let pos = t.TransformPos (radius * ndc).XYO
                //     
                //     let tc = getTC pos
                //     let px = V2i(tc * V2d pano.Size)
                //     panoMat.[px] <- byte (255.0f * m)
                // )
                
                
                
                    
               
                    
                    
                
                Some pano
                
            | _ ->
                None
            
            
            
            
        let segmentationMask = cval (NullTexture.Instance)
        let segmentationCenter = cval V3d.Zero
        let segmentationPartIndex = cval -1
        
        let pcs = cset []
        let bb = 
            pcs |> ASet.toAVal |> AVal.map (fun pcs ->
                pcs |> Seq.map (fun i -> 
                    match i.root with
                    | :? LodTreeInstance.PointTreeNode as n -> n.Original.BoundingBoxApproximate
                    | _ -> i.root.WorldBoundingBox
                ) |> Box3d
            )

        let locAndCenter =
            pcs |> ASet.toAVal |> AVal.map (fun pcs ->
                let pc = pcs |> Seq.tryHead
        
                match pc with
                | Some pc ->
                    let rand = RandomSystem()
                    match pc.root with
                    | :? LodTreeInstance.PointTreeNode as n -> 
                        let c = n.Original.Center + V3d n.Original.CentroidLocal
                        let clstddev = max 1.0 (float n.Original.CentroidLocalStdDev)
                        let pos = c + rand.UniformV3dDirection() * 2.0 * clstddev
                        pos, c
                    | _ -> 
                        let bb = pc.root.WorldBoundingBox
                        bb.Max, bb.Center
                | None  ->
                    V3d.III * 6.0, V3d.Zero
            )
        let speed = AVal.init 2.0
        
        let initial = CameraView.ofTrafo <| Trafo3d.Parse "[[[-0.707106781186548, 0.707106781186548, 0, 0], [-0.408248290463863, -0.408248290463863, 0.816496580927726, 0], [0.577350269189626, 0.577350269189626, 0.577350269189626, -342.288592930008], [0, 0, 0, 1]], [[-0.707106781186548, -0.408248290463863, 0.577350269189626, 197.620411268678], [0.707106781186548, -0.408248290463863, 0.577350269189626, 197.620411268678], [0, 0.816496580927726, 0.577350269189626, 197.620411268678], [0, 0, 0, 1]]]"
        let target = CameraView.ofTrafo <| Trafo3d.Parse "[[[-0.567502843343406, 0.823371436714408, 0, -0.0700798647066693], [-0.362138228540449, -0.249601170524128, 0.898084160367263, 0.212820843806073], [0.739456845412046, 0.509665314570097, 0.439823647496845, -1.68800745703926], [0, 0, 0, 1]], [[-0.567502843343406, -0.362138228540449, 0.739456845412046, 1.28550871010452], [0.823371436714408, -0.249601170524128, 0.509665314570097, 0.971140942202795], [0, 0.898084160367263, 0.439823647496845, 0.551294567938653], [0, 0, 0, 1]]]"
        
        let custom = AVal.init None
        let camera =
            custom |> AVal.bind (fun (custom : Option<CameraView>) -> 
                printfn "%A" (locAndCenter |> AVal.force)
                printfn "%A" (initial.Location)
                match custom with 
                | None -> 
                    locAndCenter |> AVal.bind (fun (loc, center) ->
                        CameraView.lookAt loc center V3d.OOI
                        |> DefaultCameraController.controlWithSpeed speed win.Mouse win.Keyboard win.Time
                    )
                | Some cv -> 
                    locAndCenter |> AVal.map (fun (_,center) -> 
                        cv.WithLocation (cv.Location + center)
                    )
                    //AVal.constant cv
            )
        
        let frustum =
            AVal.custom (fun t ->
                let s = win.Sizes.GetValue t
                let c = camera.GetValue t
                let bb = bb.GetValue t

                let (minPt, maxPt) = bb.GetMinMaxInDirection(c.Forward)
                
                let near = Vec.dot c.Forward (minPt - c.Location)
                let far = Vec.dot c.Forward (maxPt - c.Location)
                let near = max (max 0.05 near) (far / 100000.0)

                Frustum.perspective 90.0 near far (float s.X / float s.Y)
            )
        

        win.Mouse.Click.Values.Add (fun b ->
            if b = Aardvark.Application.MouseButtons.Right then
                let pts = pick.Value (AVal.force win.Mouse.Position).Position 5 100
                if pts.Length > 0 then
                    let partIndices = 
                        pts |> Array.collect (fun (pt : PickPoint) ->
                            let chunk = cloud.QueryPointsInsideBox(Box3d.FromCenterAndSize(pt.World, V3d.III * 0.2))
                            chunk |> Seq.collect (fun c ->
                                let parts = c.TryGetPartIndices()
                                if isNull parts then Seq.empty
                                else parts
                            )
                            |> Seq.toArray
                        )
                        |> Array.countBy id
                        |> Array.sortByDescending snd
                        |> Array.tryHead
                        
                    match partIndices with
                    | Some (id, _) ->
                        match Map.tryFind id panos, Map.tryFind id centers with
                        | Some pano, Some center ->
                            Log.startTimed "segmenting %d: %A" id center
                            // let seeds =
                            //     pts |> Array.map (fun pt ->
                            //         let pt = pt.World - center
                            //     
                            //         let d = Vec.length pt
                            //         let phi = Constant.Pi - atan2 pt.Y pt.X
                            //         let theta = asin (pt.Z / d)
                            //     
                            //         let rx = phi / Constant.PiTimesTwo
                            //         let ry = (theta + Constant.PiHalf) / Constant.Pi
                            //         
                            //         let pos = V2i(int32 (rx * float pano.Size.X), int32 ((1.0 - ry) * float pano.Size.Y))
                            //         pos
                            //     )
                            //     
                            //     
                            // let seeds = seeds |> Array.truncate 1
                            // printfn "%A" seeds
                            // let queries =
                            //     seeds |> Array.toList |> List.map (fun pt -> Query.Point(pt, 1))
                            //     
                            // let mask = samIndex.Query queries
                            //   
                            // let img = PixImage<byte>(Col.Format.RGBA, mask.Size)
                            // let m = img.GetMatrix<C4b>()
                            // m.SetMap(mask, fun v -> if v > 0.4f then C4b.Red else C4b.Black) |> ignore
                            //
                            //
                            //
                            // let maskSeeds = seeds |> Array.map (fun s -> V2d m.Size * (V2d s + V2d.Half) / V2d pano.Size |> V2i)
                            // for s in maskSeeds do
                            //     m.SetCross(s, 10, C4b.Green)
                            //
                            let model = Trafo3d.Identity //Trafo3d.Translation(cloud.Bounds.Center)
                            let view = AVal.force camera |> CameraView.viewTrafo
                            let proj = AVal.force frustum |> Frustum.projTrafo
                            let mvp = model * view * proj
                            
                            match trySamplePano cloud.Bounds.Center mvp pts.[0].World id with
                            | Some img ->
                                // img.SaveAsPng @"D:\bla.png"
                                // pano.SaveAsPng @"D:\pano.png"
                                
                                
                                transact (fun () ->
                                    segmentationMask.Value <- PixTexture2d(PixImageMipMap [|img :> PixImage|], TextureParams.empty)
                                    segmentationCenter.Value <- center - cloud.Bounds.Center
                                    segmentationPartIndex.Value <- id
                                )
                               
                                    
                            | None ->
                                ()
                            
                            Log.stop()
                                
                            ()
                        | _ ->
                            ()
                    | None ->
                        ()
        )
        
        let sets = 
            match root with
            | Some root ->
                let uniforms = MapExt.ofList []
                let uniforms = MapExt.add "Scales" (AVal.constant V4d.IIII :> IAdaptiveValue) uniforms
                List.singleton { 
                    root = root
                    uniforms = uniforms
                }
            | None ->
                List.empty
        

        
        transact (fun () ->
            pcs.Value <- HashSet.ofList sets    
        )

        win.VSync <- false
        win.DropFiles.Add(fun e ->
            match e with
            | [| e |] when Directory.Exists e -> 
                let key = Path.combine [e; "key.txt"]
                if File.Exists key then 
                    let kk = File.ReadAllText(key).Trim()
                    match LodTreeInstance.load "asdasdasd" kk e [] with
                    | Some inst ->
                        transact (fun () ->
                            pcs.Value <- HashSet.single inst
                        )
                    | None ->
                        ()
            
                Log.warn "dropped: %A" e
            | _ ->
                ()
        )
            
        //let bb = Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 300.0)
        
        win.Keyboard.DownWithRepeats.Values.Add(function
            | Keys.PageUp | Keys.Up -> transact(fun () -> speed.Value <- speed.Value * 1.5)
            | Keys.PageDown | Keys.Down -> transact(fun () -> speed.Value <- speed.Value / 1.5)
            | Keys.D5 -> transact(fun () -> custom.Value <- Some initial)
            | Keys.D6 -> transact(fun () -> custom.Value <- Some target)
            | Keys.D7 -> transact(fun () -> custom.Value <- None)
            | _ -> ()
        )

        let sh = FShade.Effect.ofFunction Shader.segmented
        
        let uf =
            HashMap.ofList [
                "SegmentationMask", segmentationMask :> IAdaptiveValue
                "SegmentationCenter", segmentationCenter :> IAdaptiveValue
                "SegmentationPartIndex", segmentationPartIndex :> IAdaptiveValue
            ]
        
        
        let config, pcs = Rendering.pointClouds (Some sh) uf pick win false camera frustum pcs
        
        let sg =
            Sg.ofList [
                pcs
                Util.coordinateBox
                |> Sg.onOff (config.background |> AVal.map ((=) Background.CoordinateBox))

                Sg.ofList (
                    Rendering.skyboxes |> Map.toList |> List.map (fun (id, tex) ->
                        Sg.farPlaneQuad
                        |> Sg.uniform "EnvMap" tex
                        |> Sg.onOff (config.background |> AVal.map ((=) (Background.Skybox id)))
                    )
                )
                |> Sg.shader {
                    do! Util.Shader.reverseTrafo
                    do! Util.Shader.envMap
                }

            ]
            |> Sg.viewTrafo (camera |> AVal.map CameraView.viewTrafo)
            |> Sg.projTrafo (frustum |> AVal.map Frustum.projTrafo)
            //|> Sg.uniform "EnvMap" skyboxes.[Skybox.ViolentDays]
    
        win.RenderTask <- Sg.compile win.Runtime win.FramebufferSignature sg
        win.Run()
        
        
        
        0
