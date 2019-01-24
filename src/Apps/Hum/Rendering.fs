namespace Hum

open Aardvark.Base.Incremental
open Aardvark.Base
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.Rendering.Text
open Aardvark.Rendering.PointSet
open Hum
open Aardvark.Base.Rendering


module Util =
    let coordinateBox (size : float) =
        let s = size

        let ppp = V3d( s, s, s)
        let ppm = V3d( s, s,-s)
        let pmp = V3d( s,-s, s)
        let pmm = V3d( s,-s,-s)
        let mpp = V3d(-s, s, s)
        let mpm = V3d(-s, s,-s)
        let mmp = V3d(-s,-s, s)
        let mmm = V3d(-s,-s,-s)

        let hi = 70
        let lo = 30

        let qf = IndexedGeometryPrimitives.Quad.solidQuadrangle' pmp ppp ppm pmm (C4b(hi,lo,lo,255))
        let qb = IndexedGeometryPrimitives.Quad.solidQuadrangle' mmp mmm mpm mpp (C4b(lo,hi,hi,255))
        let ql = IndexedGeometryPrimitives.Quad.solidQuadrangle' pmp pmm mmm mmp (C4b(lo,hi,lo,255))
        let qr = IndexedGeometryPrimitives.Quad.solidQuadrangle' ppp mpp mpm ppm (C4b(hi,lo,hi,255))
        let qu = IndexedGeometryPrimitives.Quad.solidQuadrangle' pmp ppp mpp mmp (C4b(lo,lo,hi,255))
        let qd = IndexedGeometryPrimitives.Quad.solidQuadrangle' pmm mmm mpm ppm (C4b(hi,hi,lo,255))

        [qf; qb; ql; qr; qu; qd]
    

module Rendering =


    let coordinateCross = 
        let cross =
            IndexedGeometryPrimitives.coordinateCross (V3d.III * 2.0)
                |> Sg.ofIndexedGeometry
                |> Sg.translate -6.0 -6.0 -6.0
                |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! DefaultSurfaces.thickLine
                    do! DefaultSurfaces.vertexColor
                }
                |> Sg.uniform "LineWidth" (Mod.constant 2.0)


        let box =
            Util.coordinateBox 500.0
                |> List.map Sg.ofIndexedGeometry
                |> Sg.ofList
                |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! DefaultSurfaces.vertexColor
                }

        [cross; box] |> Sg.ofList

    let pointClouds (win : IRenderWindow) (camera : IMod<CameraView>) (frustum : IMod<Frustum>) (pcs : list<LodTreeInstance>) =
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

            ) 
            
        let isActive = Mod.init true
        let trafo = Mod.init Trafo3d.Identity
        
        let pcs =
            pcs |> List.map (fun t ->
                { t with uniforms = MapExt.add "Overlay" (config.overlayAlpha :> IMod) t.uniforms }
            )
            
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

        sg

    let show (pcs : list<_>) =
        Ag.initialize()
        Aardvark.Init()

        
        use app = new OpenGlApplication()
        use win = app.CreateGameWindow(8)
        
        let camera =
            CameraView.lookAt (V3d(10,10,10)) V3d.Zero V3d.OOI
            |> DefaultCameraController.control win.Mouse win.Keyboard win.Time

        let frustum =
            win.Sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 1000.0 (float s.X / float s.Y))


        let pcs = pointClouds win camera frustum pcs

        let sg =
            Sg.ofList [
                pcs
                coordinateCross
            ]
    
        win.RenderTask <- Sg.compile app.Runtime win.FramebufferSignature sg
        win.Run()