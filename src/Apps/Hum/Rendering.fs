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

    module Shader =
        open FShade

        let reverseTrafo (v : Effects.Vertex) =
            vertex {
                let wp = uniform.ViewProjTrafoInv * v.pos
                return { v with wp = wp / wp.W }
            }
    
        let hi = 70.0 / 255.0
        let lo = 30.0 / 255.0
        let qf = (V4d(hi,lo,lo,1.0))
        let qb = (V4d(lo,hi,hi,1.0))
        let ql = (V4d(lo,hi,lo,1.0))
        let qr = (V4d(hi,lo,hi,1.0))
        let qu = (V4d(lo,lo,hi,1.0))
        let qd = (V4d(hi,hi,lo,1.0))

        let box (v : Effects.Vertex) =
            fragment {
                let c = uniform.CameraLocation
                let f = v.wp.XYZ
                let dir = Vec.normalize (f - c)
                
                let absDir = V3d(abs dir.X, abs dir.Y, abs dir.Z)

                if absDir.X > absDir.Y && absDir.X > absDir.Z then 
                    if dir.X > 0.0 then return qf
                    else return qb
                elif absDir.Y > absDir.X && absDir.Y > absDir.Z then
                    if dir.Y > 0.0 then return ql
                    else return qr
                else
                    if dir.Z > 0.0 then return qu
                    else return qd

            }

        let env =
            samplerCube {
                texture uniform?EnvMap
                addressU WrapMode.Wrap
                addressV WrapMode.Wrap
                addressW WrapMode.Wrap
                filter Filter.MinMagMipLinear
            }

        let envMap (v : Effects.Vertex) =
            fragment {
                let c = uniform.CameraLocation
                let f = v.wp.XYZ
                let dir = Vec.normalize (f - c)
                return env.Sample(dir)
            }

    let coordinateBox =
        Sg.farPlaneQuad
            |> Sg.shader  {
                do! Shader.reverseTrafo
                do! Shader.box
            }
    

module Rendering =


    let pointClouds (win : IRenderWindow) (camera : IMod<CameraView>) (frustum : IMod<Frustum>) (pcs : list<LodTreeInstance>) =
        let config =
            {
                pointSize = Mod.init 1.0
                overlayAlpha = Mod.init 0.0
                maxSplits = Mod.init 8
                renderBounds = Mod.init false
                budget = Mod.init -(256L <<< 10)
                lighting = Mod.init true
                colors = Mod.init true
                magicExp = Mod.init 1.0
                stats = Mod.init Unchecked.defaultof<_>
                background = Mod.init Background.Skybox
                antialias = Mod.init true
                fancy = Mod.init false
            }

        let vis = 
            Mod.custom (fun t ->
                let l = config.lighting.GetValue t
                let c = config.colors.GetValue t
                let aa = config.antialias.GetValue t
                let fancy = config.fancy.GetValue t

                let vis = PointVisualization.OverlayLod
                let vis = 
                    if l then PointVisualization.Lighting ||| vis
                    else vis

                let vis =
                    if c then PointVisualization.Color ||| vis
                    else PointVisualization.White ||| vis
                    
                let vis =
                    if aa then PointVisualization.Antialias ||| vis
                    else vis

                let vis =
                    if fancy then PointVisualization.FancyPoints ||| vis
                    else vis
                //let vis =
                //    if s then PointVisualization.MagicSqrt ||| vis
                //    else vis

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
            Sg.LodTreeNode(config.stats, true, config.budget, config.renderBounds, config.maxSplits, win.Time, pcs) :> ISg
            |> Sg.uniform "PointSize" config.pointSize
            |> Sg.uniform "ViewportSize" win.Sizes
            |> Sg.uniform "PointVisualization" vis
            |> Sg.uniform "MagicExp" config.magicExp
            |> Sg.shader {
                do! PointSetShaders.lodPointSize
                do! PointSetShaders.cameraLight
                do! PointSetShaders.lodPointCircular
            }
            |> Sg.multisample (Mod.constant true)
            |> Sg.viewTrafo (camera |> Mod.map CameraView.viewTrafo)
            |> Sg.projTrafo (frustum |> Mod.map Frustum.projTrafo)
            |> Sg.andAlso (RenderConfig.toSg win config)
            |> Sg.blendMode (Mod.constant BlendMode.None)

        win.Keyboard.DownWithRepeats.Values.Add(fun k ->
            match k with
            | Keys.I ->
                transact (fun () -> config.magicExp.Value <- min 4.0 (config.magicExp.Value + 0.01))
            | Keys.U ->
                transact (fun () -> config.magicExp.Value <- max 0.0 (config.magicExp.Value - 0.01))
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
        
            | Keys.Up -> transact (fun () -> config.maxSplits.Value <- config.maxSplits.Value + 1); printfn "splits: %A" config.maxSplits.Value
            | Keys.Down -> transact (fun () -> config.maxSplits.Value <- max 1 (config.maxSplits.Value - 1)); printfn "splits: %A" config.maxSplits.Value

            | Keys.C -> transact (fun () -> if config.budget.Value > 0L && config.budget.Value < (1L <<< 30) then config.budget.Value <- 2L * config.budget.Value); Log.line "budget: %A" config.budget.Value
            | Keys.X -> transact (fun () -> if config.budget.Value > (256L <<< 10) then config.budget.Value <- max (config.budget.Value / 2L) (256L <<< 10)); Log.line "budget: %A" config.budget.Value
        
            | Keys.B -> transact (fun () -> config.renderBounds.Value <- not config.renderBounds.Value); Log.line "bounds: %A" config.renderBounds.Value

            | Keys.Y -> transact (fun () -> config.budget.Value <- -config.budget.Value)
            
            | Keys.Space -> 
                transact (fun () -> 
                    match config.background.Value with
                    | Background.Skybox -> config.background.Value <- Background.CoordinateBox
                    | Background.CoordinateBox ->  config.background.Value <- Background.Black
                    | Background.Black -> config.background.Value <- Background.Skybox
                )

            | Keys.D1 -> transact (fun () -> config.fancy.Value <- not config.fancy.Value)
            | Keys.D2 -> transact (fun () -> config.antialias.Value <- not config.antialias.Value)

            | k -> 
                ()
        )

        config, sg

    let skybox =
        Mod.custom (fun _ ->
            let env =
                let trafo t (img : PixImage) = img.Transformed t
                let load (name : string) =
                    use s = typeof<Args>.Assembly.GetManifestResourceStream("Hum.CubeMap." + name)
                    PixImage.Create(s, PixLoadOptions.Default)
                
                PixImageCube [|
                    PixImageMipMap(
                        load "miramar_rt.png"
                        |> trafo ImageTrafo.Rot90
                    )
                    PixImageMipMap(
                        load "miramar_lf.png"
                        |> trafo ImageTrafo.Rot270
                    )
                
                    PixImageMipMap(
                        load "miramar_bk.png"
                    )
                    PixImageMipMap(
                        load "miramar_ft.png"
                        |> trafo ImageTrafo.Rot180
                    )
                
                    PixImageMipMap(
                        load "miramar_up.png"
                        |> trafo ImageTrafo.Rot90
                    )
                    PixImageMipMap(
                        load "miramar_dn.png"
                    )
                |]

            PixTextureCube(env, TextureParams.mipmapped) :> ITexture
        )


    let show (args : Args) (pcs : list<_>) =
        Ag.initialize()
        Aardvark.Init()

        
        use app = new OpenGlApplication(true, false)
        use win = app.CreateGameWindow(8)
        
        let camera =
            CameraView.lookAt (V3d(10,10,10)) V3d.Zero V3d.OOI
            |> DefaultCameraController.control win.Mouse win.Keyboard win.Time

        let frustum =
            win.Sizes |> Mod.map (fun s -> Frustum.perspective args.fov 0.05 300.0 (float s.X / float s.Y))


        let config, pcs = pointClouds win camera frustum pcs

        let sg =
            Sg.ofList [
                pcs

                Util.coordinateBox
                |> Sg.viewTrafo (camera |> Mod.map CameraView.viewTrafo)
                |> Sg.projTrafo (frustum |> Mod.map Frustum.projTrafo)
                |> Sg.onOff (config.background |> Mod.map ((=) Background.CoordinateBox))

                Sg.farPlaneQuad
                |> Sg.shader {
                    do! Util.Shader.reverseTrafo
                    do! Util.Shader.envMap
                }
                |> Sg.uniform "EnvMap" skybox
                |> Sg.viewTrafo (camera |> Mod.map CameraView.viewTrafo)
                |> Sg.projTrafo (frustum |> Mod.map Frustum.projTrafo)
                |> Sg.onOff (config.background |> Mod.map ((=) Background.Skybox))

            ]
    
        win.RenderTask <- Sg.compile app.Runtime win.FramebufferSignature sg
        win.Run()