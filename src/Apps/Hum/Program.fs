// Learn more about F# at http://fsharp.org


open Aardvark.Base.Incremental
open Aardvark.Base
open Aardvark.SceneGraph
open Aardvark.Base.Rendering
open Aardvark.Rendering.PointSet
open Aardvark.Application
open Aardvark.Application.Utilities

[<EntryPoint>]
let main argv =  
    Ag.initialize()
    Aardvark.Init()

    let win =
        window {
            backend Backend.GL
            device DeviceKind.Dedicated
            display Display.Mono
            samples 8
            debug false
        }

    let pointSize = Mod.init 1.0
    let overlayAlpha = Mod.init 0.0
    let isActive = Mod.init true
    let maxSplits = Mod.init 8
    let renderBounds = Mod.init false
    let trafo = Mod.init Trafo3d.Identity
    let budget = Mod.init -1L //(1L <<< 30)
    let planeness = Mod.init 0.00001



    let quality = Mod.init 0.0
    let maxQuality = Mod.init 1.0
    
    let tree =
        LodTreeInstance.importAscii
            "ssd"
            "xyzrgb"
            @"C:\Users\Schorsch\Development\WorkDirectory\Kaunertal.txt"
            @"C:\Users\Schorsch\Development\WorkDirectory\kauny2"
            [
                "Overlay", overlayAlpha :> IMod
                "TreeActive", isActive :> IMod
            ]
            

    let overlay =
        let p1 = RenderPass.after "p1" RenderPassOrder.Arbitrary RenderPass.main
        let p2 = RenderPass.after "p2" RenderPassOrder.Arbitrary p1
        let p3 = RenderPass.after "p3" RenderPassOrder.Arbitrary p2
        let scale = Trafo3d.Scale(0.3, 0.05, 0.05)

        Sg.ofList [
            Sg.box (Mod.constant C4b.Blue) (Mod.constant Box3d.Unit)
                |> Sg.trafo (quality |> Mod.map (fun q -> scale * Trafo3d.Scale(q, 1.0, 1.0))) 
                |> Sg.translate -1.0 -1.0 0.0
                |> Sg.viewTrafo (Mod.constant Trafo3d.Identity)
                |> Sg.projTrafo (Mod.constant Trafo3d.Identity)
                |> Sg.pass p3
                |> Sg.depthTest (Mod.constant DepthTestMode.None)
                
            Sg.box' (C4b(25,25,25,255)) Box3d.Unit
                |> Sg.trafo (maxQuality |> Mod.map (fun q -> scale * Trafo3d.Scale(q, 1.0, 1.0))) 
                |> Sg.translate -1.0 -1.0 0.0
                |> Sg.viewTrafo (Mod.constant Trafo3d.Identity)
                |> Sg.projTrafo (Mod.constant Trafo3d.Identity)
                |> Sg.pass p2
                |> Sg.depthTest (Mod.constant DepthTestMode.None)

            Sg.box' C4b.Gray Box3d.Unit
                |> Sg.transform scale
                |> Sg.translate -1.0 -1.0 0.0
                |> Sg.viewTrafo (Mod.constant Trafo3d.Identity)
                |> Sg.projTrafo (Mod.constant Trafo3d.Identity)
                |> Sg.pass p1
                |> Sg.depthTest (Mod.constant DepthTestMode.None)
        ]
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.vertexColor
        }
    
    let vis = Mod.init (PointVisualization.Lighting ||| PointVisualization.Color ||| PointVisualization.OverlayLod)

    let pcs =
        ASet.ofList [
            yield tree |> LodTreeInstance.normalize 300.0 |> LodTreeInstance.trafo trafo
        ]

    let sg =
        Sg.LodTreeNode(quality, maxQuality, budget, renderBounds, maxSplits, win.Time, pcs) :> ISg
        |> Sg.uniform "PointSize" pointSize
        |> Sg.uniform "ViewportSize" win.Sizes
        |> Sg.uniform "Planeness" planeness
        |> Sg.uniform "PointVisualization" vis
        |> Sg.shader {
            do! PointSetShaders.lodPointSize
            do! PointSetShaders.cameraLight
            do! PointSetShaders.lodPointCircular
        }
        |> Sg.andAlso overlay

    win.Keyboard.DownWithRepeats.Values.Add(fun k ->
        match k with
        | Keys.V ->
            let n = 
                match unbox<PointVisualization> (int vis.Value &&& 0xF) with
                    | PointVisualization.Color -> PointVisualization.White
                    | _ -> PointVisualization.Color
            transact (fun () ->
                vis.Value <- unbox (int vis.Value &&& 0xFFFFFFF0) ||| n
            )
        | Keys.L ->
            transact (fun () ->
                vis.Value <- vis.Value ^^^ PointVisualization.Lighting
            )

        | Keys.O -> transact (fun () -> pointSize.Value <- pointSize.Value / 1.3)
        | Keys.P -> transact (fun () -> pointSize.Value <- pointSize.Value * 1.3)
        | Keys.Subtract | Keys.OemMinus -> transact (fun () -> overlayAlpha.Value <- max 0.0 (overlayAlpha.Value - 0.1))
        | Keys.Add | Keys.OemPlus -> transact (fun () -> overlayAlpha.Value <- min 1.0 (overlayAlpha.Value + 0.1))

        | Keys.Left -> transact (fun () -> trafo.Value <- trafo.Value * Trafo3d.Translation(-20.0, 0.0, 0.0))
        | Keys.Right -> transact (fun () -> trafo.Value <- trafo.Value * Trafo3d.Translation(20.0, 0.0, 0.0))

        | Keys.D1 -> transact (fun () -> isActive.Value <- not isActive.Value); printfn "active: %A" isActive.Value

        | Keys.Up -> transact (fun () -> maxSplits.Value <- maxSplits.Value + 1); printfn "splits: %A" maxSplits.Value
        | Keys.Down -> transact (fun () -> maxSplits.Value <- max 1 (maxSplits.Value - 1)); printfn "splits: %A" maxSplits.Value

        | Keys.C -> transact (fun () -> budget.Value <- 2L * budget.Value); Log.line "budget: %A" budget.Value
        | Keys.X -> transact (fun () -> budget.Value <- max (budget.Value / 2L) (256L <<< 10)); Log.line "budget: %A" budget.Value
        
        | Keys.G -> transact (fun () -> planeness.Value <- planeness.Value * 1.15); Log.line "planeness: %f" (planeness.Value)
        | Keys.H -> transact (fun () -> planeness.Value <- planeness.Value / 1.15); Log.line "planeness: %f" (planeness.Value)
        
        | Keys.B -> transact (fun () -> renderBounds.Value <- not renderBounds.Value); Log.line "bounds: %A" renderBounds.Value

        | _ -> 
            ()
    )
    

    win.Scene <- sg
    win.Run()


    0
