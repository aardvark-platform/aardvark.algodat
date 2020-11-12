// Learn more about F# at http://fsharp.org

open System

open Aardvark.Base
open Aardvark.Application.Slim
open Aardvark.Base.Rendering
open Aardvark.SceneGraph
open Aardvark.Application
open FSharp.Data.Adaptive

open Hera


module Shaders = 
    open FShade

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
        let fid = tc * float heatMapColors.Length - 0.5

        let id = int (floor fid)
        if id < 0 then 
            heatMapColors.[0]
        elif id >= heatMapColors.Length - 1 then
            heatMapColors.[heatMapColors.Length - 1]
        else
            let c0 = heatMapColors.[id]
            let c1 = heatMapColors.[id + 1]
            let t = fid - float id
            (c0 * (1.0 - t) + c1 * t)

    type Vertex = 
        {
            [<Position>]
            pos : V4d

            [<PointSize>]
            pointSize : float

            [<PointCoord>]
            pointCoord : V2d

            [<Color>]
            color : V4d

            [<Semantic("Density")>]
            density : float
        }


    let vs (v : Vertex) =
        vertex {
            return 
                { v with
                    pointSize = uniform?PointSize
                }
        }

    let fs (v : Vertex) = 
        fragment {
            let c = v.pointCoord * 2.0 - V2d.II
            let f = Vec.dot c c - 1.0
            if f > 0.0 then discard ()// || v.density > 0.11 then discard()
            let dHeat = heat (v.density * 10.0)
            return V4d(dHeat.XYZ * v.color.XYZ,1.0)
        }

    let fs2 (v : Vertex) = 
        fragment {
            let c = v.pointCoord * 2.0 - V2d.II
            let f = Vec.dot c c - 1.0
            //if f > 0.0 then discard()
            return V4d.IIII
        }


[<EntryPoint>]
let main argv =
    Aardvark.Init()
    //let inputfile   = @"D:\volumes\univie\r80_p0_m500_v6000_mbasalt_a1.0_1M\impact.0400"
    //let outputfile  = @"D:\volumes\univie\r80_p0_m500_v6000_mbasalt_a1.0_1M\impact.0400.durable"
    //let outputfile  = @"D:\Hera\Impact_Simulation\r80_p0_m500_v6000_mbasalt_a1.0_1M.tar.gz.betternormals.durable\impact.0039.durable"
    let outputfile = @"\\ripperl\T$\Hera\Impact_Simulation\r80_p0_m500_v6000_mbasalt_a1.0_1M.tar.gz.betternormals.densities.durable\impact.0400.durable"

    //Report.BeginTimed("convert")
    //Hera.convertFile inputfile outputfile 
    //Report.EndTimed() |> ignore

    let data = Hera.deserialize outputfile


    let bb = Box3f data.Positions |> Box3d

    let vertices = data.Positions
    let velocities = data.Velocities
    let normals = data.EstimatedNormals
    let averageSquaredDistances = data.AverageSquaredDistances

    let min = Array.min averageSquaredDistances
    let max = Array.max averageSquaredDistances
    let histo = Histogram(float min,float max,100)
    histo.AddRange(averageSquaredDistances |> Seq.map float)
    printfn "%A" histo.Slots
    printfn "deserialized file contains %d points" data.Count


    let app = new OpenGlApplication()

    // creates a new game window with samples = 8 (not showing it). neeeds to be disposed.

    let win = app.CreateGameWindow(4)

    let t = Trafo3d.Translation -bb.Center * Trafo3d.Scale (20.0 / bb.Size.NormMax)
    let initialCam = CameraView.lookAt (V3d.III * 30.0) V3d.Zero V3d.OOI
    let c = DefaultCameraController.control win.Mouse win.Keyboard win.Time initialCam

    let f = win.Sizes |> AVal.map (fun s -> Frustum.perspective 60.0 0.01 100.0 (float s.X / float s.Y))


    let sw = System.Diagnostics.Stopwatch.StartNew()
    let vertices = 
        win.Time |> AVal.map (fun _ -> 
            let t = (sw.Elapsed.TotalSeconds % 115.0 ) * 1.5
            (vertices, velocities) ||> Array.map2 (fun p v -> 
                p //+ float32 t * v |> V3f
            )
    )

    let eyeSeparation = V3d(-0.04, 0.0, 0.0)

    let stereoViews =
        let half = eyeSeparation * 0.5
        c  |> AVal.map (fun v -> 
            let t = CameraView.viewTrafo v
            [|
                t * Trafo3d.Translation(-half)
                t * Trafo3d.Translation(half)
            |]
        )

    let stereoProjs =
        win.Sizes 
        // construct a standard perspective frustum (60 degrees horizontal field of view,
        // near plane 0.1, far plane 50.0 and aspect ratio x/y.
        |> AVal.map (fun s -> 
            let ac = 30.0
            let ao = 30.0
            let near = 0.01
            let far = 10.0
            let aspect = float s.X / float s.Y
            let sc = tan (Conversion.RadiansFromDegrees ac) * near
            let so = tan (Conversion.RadiansFromDegrees ao) * near
            let sv = tan (0.5 * Conversion.RadiansFromDegrees (ac + ao)) * near

            let leftEye = { left = -sc; right = +so; bottom = -sv / aspect; top = +sv / aspect; near = near; far = far; isOrtho = false }
            let rightEye = { left = -so; right = +sc; bottom = -sv / aspect; top = +sv / aspect; near = near; far = far; isOrtho = false }
            [|
                Frustum.perspective 90.0 0.01 100.0 aspect   |> Frustum.projTrafo
                Frustum.perspective 90.0 0.01 100.0 aspect   |> Frustum.projTrafo
                //Frustum.projTrafo leftEye
                //Frustum.projTrafo rightEye
            |]
        )

    let mutable blend = 
        BlendMode(true)

    blend.AlphaOperation <- BlendOperation.Add
    blend.Operation <- BlendOperation.Add
    blend.SourceAlphaFactor <- BlendFactor.One
    blend.DestinationAlphaFactor <- BlendFactor.One
    blend.SourceFactor <- BlendFactor.One
    blend.DestinationFactor <- BlendFactor.One

    win.RenderTask <-
        Sg.draw IndexedGeometryMode.PointList
        |> Sg.vertexAttribute DefaultSemantic.Positions vertices
        |> Sg.vertexAttribute DefaultSemantic.Normals (AVal.constant normals)
        |> Sg.vertexAttribute (Sym.ofString "AverageSquaredDistance") (AVal.constant averageSquaredDistances)
        |> Sg.vertexAttribute' DefaultSemantic.Colors (velocities |> Array.map ( fun v -> ((v.Normalized + V3f.III) * 0.5f) |> V3f ))
        |> Sg.shader {  
             do! DefaultSurfaces.trafo
             do! Shaders.vs
             do! DefaultSurfaces.constantColor C4f.White
             do! DefaultSurfaces.simpleLighting
             do! Shaders.fs
             //do! DefaultSurfaces.pointSprite
             //do! DefaultSurfaces.pointSpriteFragment
           }
        |> Sg.uniform "PointSize" (AVal.constant 8.0)

        |> Sg.transform t
        |> Sg.viewTrafo (c |> AVal.map CameraView.viewTrafo)
        |> Sg.projTrafo (f |> AVal.map Frustum.projTrafo)
        |> Sg.uniform "ViewTrafo" stereoViews
        |> Sg.uniform "ProjTrafo" stereoProjs
        //|> Sg.blendMode (Mod.constant blend)
        //|> Sg.depthTest (Mod.constant DepthTestMode.None)
        |> Sg.compile app.Runtime win.FramebufferSignature

    win.Run()
    0 
