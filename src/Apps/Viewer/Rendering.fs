(*
    Copied from https://github.com/aardvark-community/hum.
*)
namespace Aardvark.Algodat.App.Viewer

open Aardvark.Base.Incremental
open Aardvark.Base.Incremental.Operators
open Aardvark.Base
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.Rendering.Text
open Aardvark.Rendering.PointSet
open Aardvark.Base.Rendering
open Aardvark.Base.Geometry


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
                
                let vp = uniform.ProjTrafoInv * V4d(v.pos.X, v.pos.Y, -1.0, 1.0)
                let vp = vp.XYZ / vp.W

                let dir = (uniform.ViewTrafoInv * V4d(vp, 0.0)).XYZ |> Vec.normalize


                //let wp = uniform.ViewProjTrafoInv * V4d(v.pos.X, v.pos.Y, -1.0, 1.0)

                //let f = 1.0 / (uniform.ViewProjTrafoInv.M33 - uniform.ViewProjTrafoInv.M32)
                
                //let dir = 
                //    f * uniform.ViewProjTrafoInv.C0.XYZ * v.pos.X + 
                //    f * uniform.ViewProjTrafoInv.C1.XYZ * v.pos.Y +
                //    f * uniform.ViewProjTrafoInv.C2.XYZ * -1.0 +

                //    f * uniform.ViewProjTrafoInv.C3.XYZ +
                //    (uniform.ViewProjTrafoInv.C2.XYZ) / (-uniform.ViewProjTrafoInv.M32)

                //let dir = Vec.normalize dir

                //let c = uniform.CameraLocation
                //let f = v.wp.XYZ
                //let dir = Vec.normalize (f - c)
                return env.Sample(dir)
            }

    let coordinateBox =
        Sg.farPlaneQuad
            |> Sg.shader  {
                do! Shader.reverseTrafo
                do! Shader.box
            }
    

module Rendering =


    let pointClouds (win : IRenderWindow) (msaa : bool) (camera : IMod<CameraView>) (frustum : IMod<Frustum>) (pcs : list<LodTreeInstance>) =
        let picktrees : mmap<ILodTreeNode,SimplePickTree> = MMap.empty
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
                background = Mod.init (Background.Skybox Skybox.Miramar)
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

        let pcs =
            pcs |> List.map (fun t ->
                { t with uniforms = MapExt.add "Overlay" (config.overlayAlpha :> IMod) t.uniforms }
            )
        
        let overallBounds = 
            pcs |> List.map (fun i -> i.root.WorldBoundingBox) |> Box3d

        let trafo = 
            Trafo3d.Translation(-overallBounds.Center) * 
            Trafo3d.Scale(300.0 / overallBounds.Size.NormMax)

        let pcs =
            pcs |> List.map (LodTreeInstance.transform trafo) |> ASet.ofList
            
        let cfg =
            RenderConfig.toSg win config

        //let bla =
        //    let pi = Mod.init 0
        //    let pos = 
        //        [|
        //            OverlayPosition.None
        //            OverlayPosition.Top
        //            OverlayPosition.Top ||| OverlayPosition.Right
        //            OverlayPosition.Right
        //            OverlayPosition.Right ||| OverlayPosition.Bottom
        //            OverlayPosition.Bottom
        //            OverlayPosition.Bottom ||| OverlayPosition.Left
        //            OverlayPosition.Left
        //            OverlayPosition.Left ||| OverlayPosition.Top
        //        |]
        //    win.Keyboard.DownWithRepeats.Values.Add (fun k ->
        //        match k with
        //        | Keys.Divide -> transact (fun () -> pi.Value <- (pi.Value + 1) % pos.Length)
        //        | _ -> ()
        //    )

        //    let cfg = pi |> Mod.map (fun pi -> { pos = pos.[pi]  })
        //    let content = 
        //        Mod.constant {
        //            prefix = ""
        //            suffix = ""
        //            separator = " "
        //            entries = [
        //                [ 
        //                    Text ~~"Hig"
        //                    Text ~~""
        //                    Number((config.stats |> Mod.map (fun s -> float s.totalPrimitives)), "pt", 1)
        //                ]
        //                [ 
        //                    Text ~~"Hugo"
        //                    Text ~~"Sepp"
        //                    Number((config.stats |> Mod.map (fun s -> float s.totalPrimitives)), "pt", 1)
        //                ]
        //                [ 
        //                    Text ~~"Hugo"
        //                    ColSpan(2, 
        //                        Text (~~(string (Mem (1L <<< 48))))
        //                    )
        //                ]
        //                //[ 
        //                //    Text ~~"Seppy"
        //                //    Concat [
        //                //        //Progress (config.stats |> Mod.map (fun s -> s.quality))
        //                //        Text (config.stats |> Mod.map (fun s -> sprintf " %.0f%%" (100.0 * s.quality)))
        //                //    ]
        //                //]
        //                //[
        //                //    ColSpan(2, Text ~~"kjasndjasnlkdnsadlknsadnaldnasd")
        //                //]
        //            ]
        //        }
        //    Overlay.table cfg win.Sizes content

        let v = (camera |> Mod.map CameraView.viewTrafo)
        let p = (frustum |> Mod.map Frustum.projTrafo)

        let picked = 
            Mod.custom ( fun a ->
                let ndc = win.Mouse.Position.GetValue a |> (fun pp -> pp.NormalizedPosition * V2d(2,-2) + V2d(-1,1))
                
                match (picktrees |> MMap.toMod).GetValue a |> Seq.tryHead with
                | None -> 
                    false, [||]
                | Some (_,tree) -> 
                    let s = win.Sizes.GetValue a
                    let vp = v.GetValue a * p.GetValue a
                    let loc = vp.Backward.TransformPosProj(V3d(0.0,0.0,-100000000.0))
                    let npp = vp.Backward.TransformPosProj(V3d(ndc, -1.0))
                    let l = loc
                    let n = npp

                    //let cone = Ray3d(l, (n-l).Normalized)
                    //let k = tan (2.0 * Constant.RadiansPerDegree)
                    //let pts = 
                    //    tree.FindPointsLocal(cone, 0.0, System.Double.PositiveInfinity, 0.0, k)
                    //    |> Seq.map (fun p -> V3f p.Value.WorldPosition)
                    //    |> Seq.truncate 5000
                    //    |> Seq.toArray

                    
                    let pixelRadius = 10.0
                    let e = Ellipse2d(ndc, 2.0 * V2d.IO * pixelRadius / float s.X, 2.0 * V2d.OI * pixelRadius / float s.Y)

                    let d2 (pt : V3d) =
                        vp.Forward.TransformPosProj(pt).XY - ndc |> Vec.lengthSquared

                    let pts = 
                        tree.FindPoints(vp, e)
                        |> Seq.truncate 30
                        |> Seq.sortBy (fun p -> d2 p.Value.WorldPosition)
                        |> Seq.truncate 1
                        |> Seq.map (fun p -> V3f p.Value.WorldPosition)
                        |> Seq.toArray

                    pts.Length > 0, pts
                    //match pt with
                    //| Some pt -> 
                    //    let t = pt.Value.WorldPosition
                    //    Log.warn "%A" pt.Value.DataPosition
                    //    true, Trafo3d.Translation t
                    //| None -> false, Trafo3d.Identity
            )

        let afterMain = RenderPass.after "aftermain" RenderPassOrder.Arbitrary RenderPass.main

        let thing =
            Sg.draw IndexedGeometryMode.PointList
            |> Sg.vertexAttribute DefaultSemantic.Positions (Mod.map snd picked)
            //|> Sg.onOff (Mod.map fst picked)
            //|> Sg.fillMode (Mod.constant FillMode.Line)
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.pointSprite
                do! DefaultSurfaces.constantColor C4f.Red
                do! DefaultSurfaces.pointSpriteFragment
            }
            |> Sg.uniform "PointSize" (Mod.constant 10.0)
            |> Sg.uniform "ViewportSize" win.Sizes
            |> Sg.depthTest (Mod.constant DepthTestMode.None)
            //|> Sg.viewTrafo (Mod.constant Trafo3d.Identity)
            //|> Sg.projTrafo (Mod.constant Trafo3d.Identity)
            |> Sg.pass afterMain

        let sg =
            Sg.LodTreeNode(config.stats, picktrees, true, config.budget, config.renderBounds, config.maxSplits, win.Time, pcs) :> ISg
            |> Sg.uniform "PointSize" config.pointSize
            |> Sg.uniform "ViewportSize" win.Sizes
            |> Sg.uniform "PointVisualization" vis
            |> Sg.uniform "MagicExp" config.magicExp
            |> Sg.shader {
                do! PointSetShaders.lodPointSize
                //do! PointSetShaders.cameraLight
                if msaa then
                    do! PointSetShaders.lodPointCircularMSAA
                else
                    do! PointSetShaders.lodPointCircular
                //do! PointSetShaders.envMap
            }
            |> Sg.andAlso thing
            |> Sg.multisample (Mod.constant true)
            |> Sg.viewTrafo v
            |> Sg.projTrafo p
            |> Sg.andAlso cfg
            //|> Sg.andAlso bla
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
                    | Background.Skybox s -> 
                        match s with
                        | Skybox.Miramar -> config.background.Value <- Background.Skybox Skybox.ViolentDays
                        | Skybox.ViolentDays -> config.background.Value <- Background.Skybox Skybox.Wasserleonburg
                        | Skybox.Wasserleonburg -> config.background.Value <- Background.CoordinateBox
                    | Background.CoordinateBox ->  config.background.Value <- Background.Black
                    | Background.Black -> config.background.Value <- Background.Skybox Skybox.Miramar
                )

            | Keys.D1 -> transact (fun () -> config.fancy.Value <- not config.fancy.Value)
            | Keys.D2 -> transact (fun () -> config.antialias.Value <- not config.antialias.Value)

            | k -> 
                ()
        )

        config, sg

    let skybox (name : string) =
        
        Mod.custom (fun _ ->
            let env =
                let trafo t (img : PixImage) = img.Transformed t
                let load (name : string) =
                    use s = typeof<Args>.Assembly.GetManifestResourceStream("Viewer.CubeMap." + name)
                    PixImage.Create(s, PixLoadOptions.Default)
                
                PixImageCube [|
                    PixImageMipMap(
                        load (name.Replace("$", "rt"))
                        |> trafo ImageTrafo.Rot90
                    )
                    PixImageMipMap(
                        load (name.Replace("$", "lf"))
                        |> trafo ImageTrafo.Rot270
                    )
                
                    PixImageMipMap(
                        load (name.Replace("$", "bk"))
                    )
                    PixImageMipMap(
                        load (name.Replace("$", "ft"))
                        |> trafo ImageTrafo.Rot180
                    )
                
                    PixImageMipMap(
                        load (name.Replace("$", "up"))
                        |> trafo ImageTrafo.Rot90
                    )
                    PixImageMipMap(
                        load (name.Replace("$", "dn"))
                        |> trafo ImageTrafo.Rot90
                    )
                |]

            PixTextureCube(env, TextureParams.mipmapped) :> ITexture
        )

    let rftSky =
        let name = "2010.04.29-16.59.11-$.jpg"
        Mod.custom (fun _ ->
            let env =
                let trafo t (img : PixImage) = img.Transformed t
                let load (name : string) =
                    use s = typeof<Args>.Assembly.GetManifestResourceStream("Viewer.CubeMap." + name)
                    PixImage.Create(s, PixLoadOptions.Default)
                
                PixImageCube [|
                    PixImageMipMap(
                        load (name.Replace("$", "l"))
                        |> trafo ImageTrafo.Rot90
                    )
                    PixImageMipMap(
                        load (name.Replace("$", "r"))
                        |> trafo ImageTrafo.Rot270
                    )
                
                    PixImageMipMap(
                        load (name.Replace("$", "b"))
                    )
                    PixImageMipMap(
                        load (name.Replace("$", "f"))
                        |> trafo ImageTrafo.Rot180
                    )
                
                    PixImageMipMap(
                        load (name.Replace("$", "u"))
                        |> trafo ImageTrafo.Rot180
                    )
                    PixImageMipMap(
                        load (name.Replace("$", "d"))
                        |> trafo ImageTrafo.Rot90
                    )
                |]

            PixTextureCube(env, TextureParams.mipmapped) :> ITexture
        )

    let skyboxes =
        Map.ofList [
            Skybox.Miramar, skybox "miramar_$.png"
            Skybox.ViolentDays, skybox "violentdays_$.jpg"
            Skybox.Wasserleonburg, rftSky
        ]


    let show (args : Args) (pcs : list<_>) =
        Ag.initialize()
        Aardvark.Init()

        use app = new OpenGlApplication(true, false)
        use win = app.CreateGameWindow(8)
        
    
        let camera =
            CameraView.lookAt (V3d(10,10,10)) V3d.OOO V3d.OOI
            |> DefaultCameraController.control win.Mouse win.Keyboard win.Time

            
        let bb = Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 300.0)

        let frustum =
            Mod.custom (fun t ->
                let s = win.Sizes.GetValue t
                let c = camera.GetValue t

                let (minPt, maxPt) = bb.GetMinMaxInDirection(c.Forward)
                
                let near = Vec.dot c.Forward (minPt - c.Location)
                let far = Vec.dot c.Forward (maxPt - c.Location)
                let near = max (max 0.05 near) (far / 1000.0)

                Frustum.perspective args.fov near far (float s.X / float s.Y)
            )


        let config, pcs = pointClouds win args.msaa camera frustum pcs
        
        let sg =
            Sg.ofList [
                pcs

                Util.coordinateBox
                |> Sg.onOff (config.background |> Mod.map ((=) Background.CoordinateBox))

                Sg.ofList (
                    skyboxes |> Map.toList |> List.map (fun (id, tex) ->
                        Sg.farPlaneQuad
                        |> Sg.uniform "EnvMap" tex
                        |> Sg.onOff (config.background |> Mod.map ((=) (Background.Skybox id)))
                    )
                )
                |> Sg.shader {
                    do! Util.Shader.reverseTrafo
                    do! Util.Shader.envMap
                }

            ]
            |> Sg.viewTrafo (camera |> Mod.map CameraView.viewTrafo)
            |> Sg.projTrafo (frustum |> Mod.map Frustum.projTrafo)
            //|> Sg.uniform "EnvMap" skyboxes.[Skybox.ViolentDays]
    
        win.RenderTask <- Sg.compile app.Runtime win.FramebufferSignature sg
        win.Run()