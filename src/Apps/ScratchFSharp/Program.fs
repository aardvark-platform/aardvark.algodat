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
open FSharp.Data.Adaptive

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

    
    
    
    [<EntryPoint>]
    let main a =
        
        let ensure storepath =
            if not (Directory.Exists storepath) then Directory.CreateDirectory storepath |> ignore
            storepath
                
                
        let inputs =
                [
                    @"\\heap\aszabo\geos3d\Beispiel Bahnhof GEOS3D\unbereinigt\Bf Tapfheim- 001.e57"
                    @"\\heap\aszabo\geos3d\Beispiel Bahnhof GEOS3D\unbereinigt\Bf Tapfheim- 002.e57"
                    @"\\heap\aszabo\geos3d\Beispiel Bahnhof GEOS3D\unbereinigt\Bf Tapfheim- 003.e57"
                    @"\\heap\aszabo\geos3d\Beispiel Bahnhof GEOS3D\unbereinigt\Bf Tapfheim- 004.e57"
                    @"\\heap\aszabo\geos3d\Beispiel Bahnhof GEOS3D\unbereinigt\Bf Tapfheim- 005.e57"
                    @"\\heap\aszabo\geos3d\Beispiel Bahnhof GEOS3D\unbereinigt\Bf Tapfheim- 006.e57"
                    @"\\heap\aszabo\geos3d\Beispiel Bahnhof GEOS3D\unbereinigt\Bf Tapfheim- 007.e57"
                    @"\\heap\aszabo\geos3d\Beispiel Bahnhof GEOS3D\unbereinigt\Bf Tapfheim- 008.e57"
                    @"\\heap\aszabo\geos3d\Beispiel Bahnhof GEOS3D\unbereinigt\Bf Tapfheim- 009.e57"
                ]
        let outdir = @"C:\bla\bahn" |> ensure
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
                                    
                                    let rx = (phi + Constant.Pi) / Constant.PiTimesTwo
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
        
        Rendering.show (Args.parse [||]) sets
        0
