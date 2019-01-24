// Learn more about F# at http://fsharp.org


open Aardvark.Base.Incremental
open Aardvark.Base
open Aardvark.SceneGraph
open Aardvark.Base.Rendering
open Aardvark.Rendering.PointSet
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.Rendering.Text
open Hum

let parseArgs (args : string[]) : list<LodTreeInstance> =
    [
        LodTreeInstance.importAscii
            "ssd"
            "xyzrgb"
            @"C:\Users\Schorsch\Development\WorkDirectory\Kaunertal.txt"
            @"C:\Users\Schorsch\Development\WorkDirectory\kauny2"
            []
    ]


[<EntryPoint>]
let main args =  

    let pcs = parseArgs args
    
    Ag.initialize()
    Aardvark.Init()




    use app = new OpenGlApplication()
    use win = app.CreateGameWindow(8)


    let camera =
        CameraView.lookAt (V3d(10,10,10)) V3d.Zero V3d.OOI
        |> DefaultCameraController.control win.Mouse win.Keyboard win.Time

    let frustum =
        win.Sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 1000.0 (float s.X / float s.Y))

    let config =
        {
            pointSize = Mod.init 1.0
            overlayAlpha = Mod.init 0.0
            maxSplits = Mod.init 8
            renderBounds = Mod.init false
            budget = Mod.init -1L
            lighting = Mod.init true
            colors = Mod.init true
            quality = Mod.init 0.0
            maxQuality = Mod.init 1.0
        }

    let vis = 
        Mod.custom (fun t ->
            let l = config.lighting.GetValue t
            let c = config.colors.GetValue t

            let vis = PointVisualization.OverlayLod
            let vis = 
                if l then PointVisualization.Lighting ||| vis
                else vis

            let vis =
                if c then PointVisualization.Color ||| vis
                else PointVisualization.White ||| vis

            vis

        ) //Mod.init (PointVisualization.Lighting ||| PointVisualization.Color ||| PointVisualization.OverlayLod)


    let isActive = Mod.init true
    let trafo = Mod.init Trafo3d.Identity


    //let quality = Mod.init 0.0
    //let maxQuality = Mod.init 1.0
    
    let pcs =
        pcs |> List.map (fun t ->
            { t with uniforms = MapExt.add "Overlay" (config.overlayAlpha :> IMod) t.uniforms }
        )

            

    //let overlay =
    //    let p1 = RenderPass.after "p1" RenderPassOrder.Arbitrary RenderPass.main
    //    let p2 = RenderPass.after "p2" RenderPassOrder.Arbitrary p1
    //    let p3 = RenderPass.after "p3" RenderPassOrder.Arbitrary p2
    //    let scale = Trafo3d.Scale(0.3, 0.05, 0.05)

    //    Sg.ofList [
    //        Sg.box (Mod.constant C4b.Blue) (Mod.constant Box3d.Unit)
    //            |> Sg.trafo (config.quality |> Mod.map (fun q -> scale * Trafo3d.Scale(q, 1.0, 1.0))) 
    //            |> Sg.translate -1.0 -1.0 0.0
    //            |> Sg.viewTrafo (Mod.constant Trafo3d.Identity)
    //            |> Sg.projTrafo (Mod.constant Trafo3d.Identity)
    //            |> Sg.pass p3
    //            |> Sg.depthTest (Mod.constant DepthTestMode.None)
                
    //        Sg.box' (C4b(25,25,25,255)) Box3d.Unit
    //            |> Sg.trafo (maxQuality |> Mod.map (fun q -> scale * Trafo3d.Scale(q, 1.0, 1.0))) 
    //            |> Sg.translate -1.0 -1.0 0.0
    //            |> Sg.viewTrafo (Mod.constant Trafo3d.Identity)
    //            |> Sg.projTrafo (Mod.constant Trafo3d.Identity)
    //            |> Sg.pass p2
    //            |> Sg.depthTest (Mod.constant DepthTestMode.None)

    //        Sg.box' C4b.Gray Box3d.Unit
    //            |> Sg.transform scale
    //            |> Sg.translate -1.0 -1.0 0.0
    //            |> Sg.viewTrafo (Mod.constant Trafo3d.Identity)
    //            |> Sg.projTrafo (Mod.constant Trafo3d.Identity)
    //            |> Sg.pass p1
    //            |> Sg.depthTest (Mod.constant DepthTestMode.None)
    //    ]
    //    |> Sg.shader {
    //        do! DefaultSurfaces.trafo
    //        do! DefaultSurfaces.vertexColor
    //    }
    

    let overallBounds = 
        pcs |> List.map (fun i -> i.root.BoundingBox) |> Box3d
    
    let trafo = 
        Trafo3d.Translation(-overallBounds.Center) *
        Trafo3d.Scale(300.0 / overallBounds.Size.NormMax)

    let pcs =
        pcs |> List.map (LodTreeInstance.transform trafo) |> ASet.ofList


        

    let sg =
        Sg.LodTreeNode(config.quality, config.maxQuality, config.budget, config.renderBounds, config.maxSplits, win.Time, pcs) :> ISg
        |> Sg.uniform "PointSize" config.pointSize
        |> Sg.uniform "ViewportSize" win.Sizes
        |> Sg.uniform "PointVisualization" vis
        |> Sg.shader {
            do! PointSetShaders.lodPointSize
            do! PointSetShaders.cameraLight
            do! PointSetShaders.lodPointCircular
        }
        //|> Sg.andAlso overlay
        |> Sg.viewTrafo (camera |> Mod.map CameraView.viewTrafo)
        |> Sg.projTrafo (frustum |> Mod.map Frustum.projTrafo)
        |> Sg.andAlso (RenderConfig.toSg win config)

    win.Keyboard.DownWithRepeats.Values.Add(fun k ->
        match k with
        | Keys.V ->
            transact (fun () ->
                config.colors.Value <- not config.colors.Value
            )
        | Keys.L ->
            transact (fun () ->
                config.lighting.Value <- not config.lighting.Value
            )

        | Keys.O -> transact (fun () -> config.pointSize.Value <- config.pointSize.Value / 1.3)
        | Keys.P -> transact (fun () -> config.pointSize.Value <- config.pointSize.Value * 1.3)
        | Keys.Subtract | Keys.OemMinus -> transact (fun () -> config.overlayAlpha.Value <- max 0.0 (config.overlayAlpha.Value - 0.1))
        | Keys.Add | Keys.OemPlus -> transact (fun () -> config.overlayAlpha.Value <- min 1.0 (config.overlayAlpha.Value + 0.1))
        
        | Keys.D1 -> transact (fun () -> isActive.Value <- not isActive.Value); printfn "active: %A" isActive.Value

        | Keys.Up -> transact (fun () -> config.maxSplits.Value <- config.maxSplits.Value + 1); printfn "splits: %A" config.maxSplits.Value
        | Keys.Down -> transact (fun () -> config.maxSplits.Value <- max 1 (config.maxSplits.Value - 1)); printfn "splits: %A" config.maxSplits.Value

        | Keys.C -> transact (fun () -> config.budget.Value <- 2L * config.budget.Value); Log.line "budget: %A" config.budget.Value
        | Keys.X -> transact (fun () -> config.budget.Value <- max (config.budget.Value / 2L) (256L <<< 10)); Log.line "budget: %A" config.budget.Value
        
        | Keys.B -> transact (fun () -> config.renderBounds.Value <- not config.renderBounds.Value); Log.line "bounds: %A" config.renderBounds.Value

        | _ -> 
            ()
    )
    
    win.RenderTask <- Sg.compile app.Runtime win.FramebufferSignature sg
    win.Run()


    0
