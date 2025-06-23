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
open Aardvark.Rendering.GL
open Aardvark.Rendering.PointSet
open FSharp.Data.Adaptive
open Aardvark.Application.Slim
open SamSharp
open Aardvark.Application
open Aardvark.SceneGraph
open Microsoft.FSharp.NativeInterop

#nowarn "9"
module Heat = 
     let heatMapColors =
         let fromInt (i : int) =
             C4b(
                 byte ((i >>> 16) &&& 0xFF),
                 byte ((i >>> 8) &&& 0xFF),
                 byte (i &&& 0xFF),
                 255uy
             ).ToC4f().ToV4d()

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

     [<ReflectedDefinition>]
     let heat (tc : float) =
         let tc = clamp 0.0 1.0 tc
         let fid = tc * float 24

         let id = int (floor fid)
         if id < 0 then 
             heatMapColors.[0]
         elif id >= 24 - 1 then
             heatMapColors.[24 - 1]
         else
             let c0 = heatMapColors.[id]
             let c1 = heatMapColors.[id + 1]
             let t = fid - float id
             (c0 * (1.0 - t) + c1 * t)


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

        
    [<ReflectedDefinition>]
    module Shader =
        open FShade
        
        type UniformScope with
            member x.SegmentationCenter : V3d = uniform?SegmentationCenter
            member x.SegmentationPartIndex : int = uniform?SegmentationPartIndex
            member x.SegmentationViewProj : M44d = uniform?SegmentationViewProj
            
            member x.Centers : V4d[] = uniform?StorageBuffer?Centers
            member x.Slice : int = uniform?Slice
            member x.SliceCount : int = uniform?SliceCount
            
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
                // let sp = uniform.SegmentationViewProj * v.pos
                // if sp.X >= -sp.W && sp.X <= sp.W && sp.Y >= -sp.W && sp.Y <= sp.W then
                //     let sp = sp.XYZ / sp.W
                //     c <- V4d.IOOI
                
                if v.idx = uniform.SegmentationPartIndex then
                    
                    let sp = uniform.SegmentationViewProj * v.pos
                    if sp.X >= -sp.W && sp.X <= sp.W && sp.Y >= -sp.W && sp.Y <= sp.W then
                        let sp = sp.XYZ / sp.W
                        if masky.SampleLevel(V2d(0.5 + 0.5 * sp.X, 0.5 - 0.5*sp.Y), 0.0).X > 0.5 then c <- V4d.IOOI
                
                return { v with c = c }
                
            }
    
    
        let texy =
            sampler2dArray {
                texture uniform?DepthTextures
                addressU WrapMode.Clamp
                addressV WrapMode.Clamp
                filter Filter.MinMagMipPoint
            }
            
        let tryGetPosition (mySlice : int) (tc : V2d) (pos : ref<V3d>)=
            let distance = texy.SampleLevel(tc, mySlice, 0.0).X
            if distance < 1000.0 then
                let center = uniform.Centers.[mySlice].XYZ
                let phi = Constant.Pi - tc.X * Constant.PiTimesTwo
                let theta = tc.Y * Constant.Pi - Constant.PiHalf
                let ct = cos theta
                let dir = V3d(cos phi * ct, sin phi * ct, sin theta)
                pos := center + dir * distance
                true
            else
                false

        let getRayDirection (tc : V2d) =
            let phi = Constant.Pi - tc.X * Constant.PiTimesTwo
            let theta = tc.Y * Constant.Pi - Constant.PiHalf
            let ct = cos theta
            V3d(cos phi * ct, sin phi * ct, sin theta)
            
        let tryGetNormal (mySlice : int) (tc : V2d) (normal : ref<V3d>)=
            let mutable position = V3d.Zero
            if tryGetPosition mySlice tc &&position then
                let dx = V2d(1.0 / float texy.Size.X, 0.0)
                let dy = V2d(0.0, 1.0 / float texy.Size.Y)
                let mutable px = V3d.Zero
                let mutable py = V3d.Zero
                if tryGetPosition mySlice (tc + dx) &&px then
                    if tryGetPosition mySlice (tc + dy) &&py then
                        normal := Vec.cross (px - position) (py - position) |> Vec.normalize
                        true
                    elif tryGetPosition mySlice (tc - dy) &&py then
                        normal := Vec.cross (px - position) (position - py) |> Vec.normalize
                        true
                    else
                        false
                        
                elif tryGetPosition mySlice (tc - dx) &&px then
                    if tryGetPosition mySlice (tc + dy) &&py then
                        normal := Vec.cross (position - px) (py - position) |> Vec.normalize
                        true
                    elif tryGetPosition mySlice (tc - dy) &&py then
                        normal := Vec.cross (position - px) (position - py) |> Vec.normalize
                        true
                    else
                        false
                else
                    false
            else
                false
            

        
        let samples24 =
            [|
                V2d( 0.0, 0.0 )
                V2d( -0.4612850228120782, -0.8824263018037591 )
                V2d( 0.2033539719528926, 0.9766070232577696 )
                V2d( 0.8622755945065503, -0.4990552917715807 )
                V2d( -0.8458406529500018, 0.4340626564690164 )
                V2d( 0.9145341241356336, 0.40187426079092753 )
                V2d( -0.8095919285224212, -0.2476471278659192 )
                V2d( 0.2443597793708885, -0.8210571365841042 )
                V2d( -0.29522102954593127, 0.6411496844366571 )
                V2d( 0.4013698454531175, 0.47134750051312063 )
                V2d( -0.1573158341083741, -0.48548502348882533 )
                V2d( 0.5674301785250454, -0.1052346781436156 )
                V2d( -0.4929375319230899, 0.09422383038685558 )
                V2d( 0.967785465127825, -0.06868225365333279 )
                V2d( 0.2267967507441493, -0.40237871966279687 )
                V2d( -0.7200979001122771, -0.6248240905561527 )
                V2d( -0.015195608523765971, 0.35623701723070667 )
                V2d( -0.11428925675805125, -0.963723441683084 )
                V2d( 0.5482105069441386, 0.781847612911249 )
                V2d( -0.6515264455787967, 0.7473765703131305 )
                V2d( 0.5826875031269089, -0.6956573112908789 )
                V2d( -0.8496230198638387, 0.09209564840857346 )
                V2d( 0.38289808661249414, 0.15269522898022844 )
                V2d( -0.4951171173546325, -0.2654758742352245 )
            |]
        
        let realRootsOfNormed (c2 : float) (c1 : float) (c0 : float) =
            let mutable d = c2 * c2
            let p3 = 1.0/3.0 * (-1.0/3.0 * d + c1)
            let q2 = 1.0/2.0 * ((2.0/27.0 * d - 1.0/3.0 * c1) * c2 + c0)
            let p3c = p3 * p3 * p3
            let shift = 1.0/3.0 * c2
            d <- q2 * q2 + p3c
            if d < 0.0 then
                if p3c > 0.0 || p3 > 0.0 then
                    -1.0
                else
                    let v = -q2 / sqrt(-p3c)
                    if v < -1.0 || v > 1.0 then
                        -1.0
                    else
                        let phi = 1.0 / 3.0 * acos v
                        let t = 2.0 * sqrt(-p3)
                        let r0 = t * cos phi - shift
                        let r1 = -t * cos (phi + Constant.Pi / 3.0) - shift
                        let r2 = -t * cos (phi - Constant.Pi / 3.0) - shift
                        min r0 (min r1 r2)
            
            else
                d <- sqrt d
                let uav = cbrt (d - q2) - cbrt (d + q2)
                let s0 = uav - shift
                let s1 = -0.5 * uav - shift
                min s0 s1

        let tryGetSmoothPlane (mySlice : int) (tc : V2d) (result : ref<V4d>) =
            let planeFitRadius = 5.0
            let planeFitTolerance = 0.1
            
            // let mutable vp = V3d.Zero
            // let mutable vn = V3d.Zero
            // if tryGetPosition mySlice tc &&vp && tryGetNormal mySlice tc &&vn then
            if true then
                let size = V2d texy.Size.XY
                //let plane = V4d(vn, -Vec.dot vn vp)

                let mutable sum = V3d.Zero
                let mutable sumSq = V3d.Zero
                let mutable off = V3d.Zero
                let mutable cnt = 1

                let x = V2d.IO //randomSam.SampleLevel((floor (tc * viewPosSize()) + V2d.Half) / V2d randomSam.Size, 0.0).XY |> Vec.normalize
                let y = V2d(-x.Y, x.X)

                for o in samples24 do
                    let tc = tc + planeFitRadius * (x*o.X + y*o.Y) / size
                    let mutable p = V3d.Zero
                    if tryGetPosition mySlice tc &&p then //&& abs (Vec.dot plane (V4d(p, 1.0))) <= planeFitTolerance then
                        let pt = p // p - vp
                        sum <- sum + pt
                        sumSq <- sumSq + sqr pt
                        off <- off + V3d(pt.Y*pt.Z, pt.X*pt.Z, pt.X*pt.Y)
                        cnt <- cnt + 1

                if cnt >= 4 then
                    let n = float cnt
                    let avg = sum / n
                    let xx = (sumSq.X - avg.X * sum.X) / (n - 1.0)
                    let yy = (sumSq.Y - avg.Y * sum.Y) / (n - 1.0)
                    let zz = (sumSq.Z - avg.Z * sum.Z) / (n - 1.0)

                    let xy = (off.Z - avg.X * sum.Y) / (n - 1.0)
                    let xz = (off.Y - avg.X * sum.Z) / (n - 1.0)
                    let yz = (off.X - avg.Y * sum.Z) / (n - 1.0)
            
            
                    let _a = 1.0
                    let b = -xx - yy - zz
                    let c = -sqr xy - sqr xz - sqr yz + xx*yy + xx*zz + yy*zz
                    let d = -xx*yy*zz - 2.0*xy*xz*yz + sqr xz*yy + sqr xy*zz + sqr yz*xx


                    let l = realRootsOfNormed b c d
                    if l < 0.0 then
                        let mutable vp = V3d.Zero
                        let mutable vn = V3d.Zero
                        if tryGetPosition mySlice tc &&vp && tryGetNormal mySlice tc &&vn then
                            result := V4d(vn, -Vec.dot vn vp)
                            true
                        else
                            false
                    else
                        let c0 = V3d(xx - l, xy, xz)
                        let c1 = V3d(xy, yy - l, yz)
                        let c2 = V3d(xz, yz, zz - l)
                        let len0 = Vec.lengthSquared c0
                        let len1 = Vec.lengthSquared c1
                        let len2 = Vec.lengthSquared c2

                        let normal =
                            if len0 > len1 then
                                if len2 > len1 then Vec.cross c0 c2
                                else Vec.cross c0 c1
                            else
                                if len2 > len0 then Vec.cross c1 c2
                                else Vec.cross c0 c1

                        let len = Vec.length normal

                        if len > 0.0 then
                            let normal = normal / len

                            //result := V4d(normal, -Vec.dot normal (vp + avg))
                            result := V4d(normal, -Vec.dot normal (avg))
                            true
                        else
                            // result := V4d(vn, -Vec.dot vn vp)
                            // true
                            false
                else
                    // result := V4d(vn, -Vec.dot vn vp)
                    // true
                    false
            else
                false

        let tryGetSmoothNormal (mySlice : int) (tc : V2d) (result : ref<V3d>) =
            let mutable plane = V4d.Zero
            if tryGetSmoothPlane mySlice tc &&plane then
                result := plane.XYZ
                true
            else
                false

            
            
        [<ReflectedDefinition>]
        let getCoord (slice : int) (position : V3d) =
            let pt = position - uniform.Centers.[slice].XYZ
            let myDepth = Vec.length pt
            let phi = Constant.Pi - atan2 pt.Y pt.X
            let theta = asin (pt.Z / myDepth)
            let rx = phi / Constant.PiTimesTwo
            let ry = (theta + Constant.PiHalf) / Constant.Pi
            V2d(rx, ry)
            
        let occlusionTest (v : Effects.Vertex) =
            fragment {
                let s = V2d texy.Size.XY
                let tc = V2d(v.tc.X, 1.0 - v.tc.Y)
                let mySlice = uniform.Slice
                let mutable position = V3d.Zero
                let mutable res = V4d.OOOI
           
                let mutable minError = 100000.0
                let mutable bad = 0
                let mutable total = 0
                let mutable maxTolerance = 0.0
                let mutable found = false
                for i in 0 .. samples24.Length - 1 do
                    let off = samples24.[i]
                    let tc = tc + 0.5 * off / s
                    if tryGetPosition mySlice tc &&position then
                        found <- true
                        res <- V4d.IIII
                        let mutable n = V3d.Zero
                        let fw = position - uniform.Centers.[mySlice].XYZ |> Vec.normalize
                        if not (tryGetNormal mySlice tc &&n) then
                            n <- fw
                            
                        let pd = Vec.dot n position
                          
                                
                        for slice in 0 .. uniform.SliceCount - 1 do
                            if slice <> mySlice then
                                let rtc = getCoord slice position
                                
                                let dir = getRayDirection rtc
                                let d = acos (Vec.dot dir n |> abs) * 57.295779513
                                if d < 80.0 then
                                    let o = uniform.Centers.[slice].XYZ
                                    let dxp = getRayDirection (rtc + V2d(0.5 / float texy.Size.X, 0.0))
                                    let dxn = getRayDirection (rtc - V2d(0.5 / float texy.Size.X, 0.0))
                                    let dyp = getRayDirection (rtc + V2d(0.0, 0.5 / float texy.Size.Y))
                                    let dyn = getRayDirection (rtc - V2d(0.0, 0.5 / float texy.Size.Y))
                                    
                                    let num = pd - Vec.dot n o
                                    let sx = Vec.distance (dxp * num / Vec.dot n dxp) (dxn * num / Vec.dot n dxn)
                                    let sy = Vec.distance (dyp * num / Vec.dot n dyp) (dyn * num / Vec.dot n dyn)
                                    let footprintSize = max sx sy
                                    
                                    
                                    // <n | o + t * d> = dist
                                    
                                    // t  = (dist - <n|o>) / <n|d>
                                    
                                    
                                    let cmpDepth = texy.SampleLevel(rtc, slice, 0.0).X
                                    let myDepth = Vec.distance position uniform.Centers.[slice].XYZ
                                    
                                   
                                    if cmpDepth < 10000.0 then
                                        let tolerance = 0.1 + 25.0*footprintSize
                                        maxTolerance <- max maxTolerance tolerance
                                        if cmpDepth > myDepth + tolerance then
                                            bad <- bad + 1
                                        total <- total + 1
                                        
                                        let err = abs (cmpDepth - myDepth) / tolerance
                                        minError <- min minError err
                             
                if found then
                    res <- Heat.heat (float bad / float (total + 1))
                return res
            }
    
    
    let renderOcclusionMasks (outPath : string) (runtime : IRuntime) (depthImages : Map<int, PixImage<float32>>) (centers : Map<int, V3d>) =
        let count = 1 + (depthImages |> Map.toSeq |> Seq.map fst |> Seq.max)
        let size = (Seq.head depthImages).Value.Size
        let offset = centers |> Seq.averageBy (fun (KeyValue(_, v)) -> v)
        
        
        
        let arr = runtime.CreateTexture2DArray(V2i size, TextureFormat.R32f, 1, 1, count)
        for KeyValue(k, img) in depthImages do
            runtime.Upload(arr.[TextureAspect.Color, 0, k], img.TransformedPixImage(ImageTrafo.MirrorY))
        //     
        // let depthImageArray =
        //     let vol = PixVolume<float32>(Col.Format.Gray, V3i(size, count))
        //     
        //     for KeyValue(k, img) in depthImages do
        //         let dst = vol.Tensor4.SubXYWVolume(int64 k)
        //         let src = img.Volume
        //         
        //         runtime.Upload(arr.[TextureAspect.Color, 0, k], img)
        //         
        //         
        //         NativeVolume.using src (fun src ->
        //             NativeVolume.using dst (fun dst ->
        //                 NativeVolume.copy src dst    
        //             )    
        //         )
        //     vol
        //
        let centers =
            let arr = Array.zeroCreate count
            for KeyValue(k, c) in centers do
                arr.[k] <- V4f(V3f (c - offset), 0.0f)
            arr
        let slice = cval 0
        
        
        
        
        let scene = 
            Sg.fullScreenQuad
            |> Sg.uniform' "DepthTextures" (AVal.constant (arr :> ITexture))
            |> Sg.uniform' "Centers" centers
            |> Sg.uniform "Slice" slice
            |> Sg.uniform' "SliceCount" count
            |> Sg.shader {
                do! Shader.occlusionTest
            }
            |> Sg.depthTest' DepthTest.Always
            
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
            ]
            
        use tex = runtime.CreateTexture2D(size, TextureFormat.Rgba8)
        use fbo = runtime.CreateFramebuffer(signature, [DefaultSemantic.Colors, tex.[TextureAspect.Color, 0, 0] :> IFramebufferOutput])
            
        use task = scene |> Sg.compile runtime signature
        
        for KeyValue(k, _) in depthImages do
            transact (fun () -> slice.Value <- k)
            printfn "%A: %A" k task.OutOfDate
            task.Run(AdaptiveToken.Top, RenderToken.Empty, fbo)
            let img = runtime.Download tex
            img.Save (Path.Combine(outPath, $"mask_%03d{k}.png"))
        
        
            
            
    let panoCompare() =
        
        let panospath = "/Users/schorsch/Desktop"
        let inputs =
            [|
                "/Users/schorsch/Desktop/Bf Tapfheim- 137 org.e57", "/Users/schorsch/Desktop/Bf Tapfheim- 137.e57"
                "/Users/schorsch/Desktop/Bf Tapfheim- 140 org.e57", "/Users/schorsch/Desktop/Bf Tapfheim- 140.e57"
            |]
        
        let mutable partIndex = 0
        let centers = ResizeArray()
        for original, cleaned in inputs do
            Log.line $"file {original}"
            
            
            
            let size = V2i(5000, 2500)
            let dimg = PixImage<float32>(Col.Format.Gray, size)
            let mutable dmat = dimg.GetChannel(0L)
            dmat.Set(System.Single.PositiveInfinity) |> ignore
            let mutable maxDepth = 0.0
            
            
            let dimgCleaned = PixImage<float32>(Col.Format.Gray, size)
            let mutable dmatCleaned = dimgCleaned.GetChannel(0L)
            dmatCleaned.Set(System.Single.PositiveInfinity) |> ignore
            
            
            do 
                let actualSize = FileInfo(original).Length
                use s = File.OpenRead original
                let info = Aardvark.Data.E57.ASTM_E57.E57FileHeader.Parse(s, actualSize, false)
                    
                let mutable i = 0
                for data in info.E57Root.Data3D do
                    
                    if not (isNull data.Pose) then
                        let name = $"{data.Guid}_{i}"
                        let center = data.Pose.Translation
                        centers.Add(center)
                        
                        let chunks =
                            data.StreamPointsFull(1 <<< 28, false, System.Collections.Immutable.ImmutableHashSet.Empty)
                            |> Seq.map (fun struct(a,b) -> E57.E57Chunk(b, data, a))
                            
                        for c in chunks do
                            
                            for i in 0 .. c.Count - 1 do
                                let pt = c.Positions.[i] - center
                                
                                let d = Vec.length pt
                                let phi = Constant.Pi - atan2 pt.Y pt.X
                                let theta = asin (pt.Z / d)
                                
                                let rx = phi / Constant.PiTimesTwo
                                let ry = (theta + Constant.PiHalf) / Constant.Pi
                                
                                let pos = V2i(int32 (rx * float dimg.Size.X), int32 ((1.0 - ry) * float dimg.Size.Y))
                                dmat.[pos] <- float32 d
                                
                
            do
                let original = ()
                let dmat = ()
                let dimg = ()
                let actualSize = FileInfo(cleaned).Length
                use s = File.OpenRead cleaned
                let info = Aardvark.Data.E57.ASTM_E57.E57FileHeader.Parse(s, actualSize, false)
                    
                let mutable i = 0
                for data in info.E57Root.Data3D do
                    
                    if not (isNull data.Pose) then
                        let name = $"{data.Guid}_{i}"
                        let center = data.Pose.Translation
                        centers.Add(center)
                        
                        let chunks =
                            data.StreamPointsFull(1 <<< 28, false, System.Collections.Immutable.ImmutableHashSet.Empty)
                            |> Seq.map (fun struct(a,b) -> E57.E57Chunk(b, data, a))
                            
                        for c in chunks do
                            
                            for i in 0 .. c.Count - 1 do
                                let pt = c.Positions.[i] - center
                                
                                let d = Vec.length pt
                                let phi = Constant.Pi - atan2 pt.Y pt.X
                                let theta = asin (pt.Z / d)
                                
                                let rx = phi / Constant.PiTimesTwo
                                let ry = (theta + Constant.PiHalf) / Constant.Pi
                                
                                let pos = V2i(int32 (rx * float dimgCleaned.Size.X), int32 ((1.0 - ry) * float dimgCleaned.Size.Y))
                                dmatCleaned.[pos] <- float32 d
                                maxDepth <- max maxDepth d
                               
                                        
            
                            
            let dimg2 = PixImage<byte>(Col.Format.RGBA, dimg.Size)
            dimg2.GetMatrix<C4b>().SetMap2(dmat, dmatCleaned, fun oDepth cleanedDepth ->
                if cleanedDepth >= 1000.0f then
                    if oDepth < 1000.0f then C4b.White
                    else C4b.Black
                else
                    heat(sqrt(float oDepth / maxDepth)).ToC4b()
            ) |> ignore
            dimg2.SaveImageSharp (Path.Combine(panospath, $"%03d{partIndex}.png"))
            partIndex <- partIndex + 1
            
    [<EntryPoint>]
    let main a =
        
        // panoCompare()
        // exit 0
        
        let ensure storepath =
            if not (Directory.Exists storepath) then Directory.CreateDirectory storepath |> ignore
            storepath
                
                
        let inputs =
                [
                    @"D:\Clouds\Kindergarten\KG1__010.e57"
                    @"D:\Clouds\Kindergarten\KG1__011.e57"
                    @"D:\Clouds\Kindergarten\KG1__012.e57"
                    @"D:\Clouds\Kindergarten\KG1__013.e57"
                    @"D:\Clouds\Kindergarten\KG1__014.e57"
                    @"D:\Clouds\Kindergarten\KG1__015.e57"
                    @"D:\Clouds\Kindergarten\KG1__016.e57"
                    @"D:\Clouds\Kindergarten\KG1__017.e57"
                    @"D:\Clouds\Kindergarten\KG1__018.e57"
                ]
        let outdir = @"D:\stores\kindergarten" |> ensure
        let storepath = Path.combine [outdir; "store"] |> ensure
        let panospath = Path.combine [outdir; "panos"] |> ensure
        let maskspath = Path.combine [outdir; "masks"] |> ensure
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
            
        renderOcclusionMasks maskspath app.Runtime depthImages centers
            
            
        let sam = new Sam() 
        let trySamplePano (cloudCenter : V3d) (viewProj : Trafo3d) (pick : V3d) (part : int) =
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
                
                Log.line "radius: %A" radius
                
                Log.startTimed "render img"
                let diff = center - pick
                let distance = Vec.length diff
                let z = diff / distance
                let sky = V3d.OOI
                let x = Vec.cross sky z |> Vec.normalize
                let y = Vec.cross z x |> Vec.normalize
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
                Log.stop()
                    
                img.SaveAsPng @"D:\crop.png"
                    
                Log.startTimed "sam index"
                let idx = sam.BuildIndex img
                Log.stop()
                Log.startTimed "sam query"
                let res = idx.Query [Query.Point(img.Size / 2, 1)]
                let maskImg = PixImage<byte>(Col.Format.RGBA, res.Size)
                let maskMat = maskImg.GetMatrix<C4b>()
                maskMat.SetMap(res, fun res ->
                    C4f(C3f.Red * res, 1.0f).ToC4b()    
                ) |> ignore
                maskImg.SaveAsPng @"D:\cropMask.png"
                Log.stop()
                
                let distance = Vec.distance center pick
                let fov = 2.0 * Constant.DegreesPerRadian * atan (radius / distance)
                let view  = Trafo3d.FromBasis(x, y, z, center).Inverse //CameraView(sky, center, -z, y, x)//CameraView.lookAt center pick y
                let proj = Frustum.perspective fov 0.01 (3.0 * distance) 1.0
                let viewProj = Trafo3d.Translation(cloudCenter) * view * Frustum.projTrafo proj
                
                Some (viewProj, res)
                
                
                
                // let pano = PixImage<byte>(Col.Format.Gray, pano.Size)
                // let mutable panoMat = pano.GetChannel(0L)
                // let pCenter = t.Backward.TransformPos center
                //
                // panoMat.SetByCoord(fun (c : V2l) ->
                //     let tc = (V2d c + V2d.Half) / V2d panoMat.Size
                //     
                //     // let d = Vec.length pt
                //     // let phi = Constant.Pi - atan2 pt.Y pt.X
                //     // let theta = asin (pt.Z / d)
                //     // let rx = phi / Constant.PiTimesTwo
                //     // let ry = (theta + Constant.PiHalf) / Constant.Pi
                //     //
                //     
                //     let phi = -(tc.X * Constant.PiTimesTwo - Constant.Pi)
                //     let theta = (1.0 - tc.Y) * Constant.Pi - Constant.PiHalf
                //     
                //     let ct  = cos theta
                //     let dir = V3d(cos phi * ct, sin phi * ct, sin theta)
                //     
                //     let pDir = t.Backward.TransformDir dir
                //     
                //     // pCenter.Z + t*pDir.Z = 0
                //
                //     let t = -pCenter.Z / pDir.Z
                //     let pPos = pCenter + t * pDir
                //     if t >= 0.0 && pPos.X >= -radius && pPos.X <= radius && pPos.Y >= -radius && pPos.Y <= radius then
                //         let ndc = pPos.XY / radius
                //         let px = 0.5 * (ndc + V2d.II) * V2d res.Size |> V2i
                //         let m = res.[px]
                //         byte (255.0f * m)
                //     else
                //         0uy
                //     
                // ) |> ignore
                //
                //
                // // res.ForeachXYIndex(fun (x : int64) (y : int64) (i : int64) ->
                // //     let m = res.[i]
                // //     let c = (V2d(x,y) + V2d.Half) / V2d maskMat.Size
                // //     let ndc = 2.0 * c - V2d.II
                // //     let pos = t.TransformPos (radius * ndc).XYO
                // //     
                // //     let tc = getTC pos
                // //     let px = V2i(tc * V2d pano.Size)
                // //     panoMat.[px] <- byte (255.0f * m)
                // // )
                //
                //
                //
                //     
                //
                //     
                //     
                //
                // Some pano
                //
            | _ ->
                None
            
            
            
            
        let segmentationViewProj = cval Trafo3d.Identity
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
                            | Some(viewProj, mat) ->
                                // img.SaveAsPng @"D:\bla.png"
                                // pano.SaveAsPng @"D:\pano.png"
                                let img = PixImage<float32>(Col.Format.Gray, mat)
                                
                                transact (fun () ->
                                    segmentationViewProj.Value <- viewProj
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
                "SegmentationViewProj", segmentationViewProj :> IAdaptiveValue
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
