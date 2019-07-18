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
open Aardvark.Geometry.Points


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
                splitfactor = Mod.init 0.4
                budget = Mod.init -(256L <<< 10)
                lighting = Mod.init true
                colors = Mod.init true
                magicExp = Mod.init 0.0
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
                    else PointVisualization.Normals ||| vis
                    
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
                { t with uniforms = MapExt.add "Overlay" (config.overlayAlpha |> Mod.map ((*) V4d.IIII) :> IMod) t.uniforms }
            )
        
        let overallBounds = 
            pcs |> List.map (fun i -> i.root.WorldBoundingBox) |> Box3d

        let trafo = 
            Trafo3d.Translation(-overallBounds.Center) * 
            Trafo3d.Scale(300.0 / overallBounds.Size.NormMax)

        let pcs =
            pcs |> List.toArray |> Array.map (LodTreeInstance.transform trafo >> Mod.init)
            
        let cfg =
            RenderConfig.toSg win config


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
            )

        let afterMain = RenderPass.after "aftermain" RenderPassOrder.Arbitrary RenderPass.main

        let thing =
            Sg.draw IndexedGeometryMode.PointList
            |> Sg.vertexAttribute DefaultSemantic.Positions (Mod.map snd picked)
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.pointSprite
                do! DefaultSurfaces.constantColor C4f.Red
                do! DefaultSurfaces.pointSpriteFragment
            }
            |> Sg.uniform "PointSize" (Mod.constant 10.0)
            |> Sg.uniform "ViewportSize" win.Sizes
            |> Sg.depthTest (Mod.constant DepthTestMode.None)
            |> Sg.pass afterMain 

        let reset = Mod.init 0 
        //let filter : ModRef<Option<Hull3d>> = Mod.init None


        let stupidFilter =
            let right (center : V3d) (p : V3d) = p.X >= center.X
            { new ISpatialFilter with
                member x.Serialize() = failwith ""
                member x.Contains (pt : V3d) = true
                member x.IsFullyInside (n : Box3d) = false
                member x.IsFullyOutside (n : Box3d) = false
                member x.IsFullyInside (n : IPointCloudNode) = x.IsFullyInside n.BoundingBoxExactGlobal
                member x.IsFullyOutside (n : IPointCloudNode) = x.IsFullyOutside n.BoundingBoxExactGlobal
                member x.Clip(bb) = bb
                member x.FilterPoints(node, _set) =
                    let ps = node.Positions.Value 
                    let c = node.Center
                    let set = System.Collections.Generic.HashSet<int>()
                    for i in 0 .. ps.Length - 1 do
                        if right c (c + V3d ps.[i]) then
                            set.Add i |> ignore
                    set
            }

        let instances = 
            aset {
                if pcs.Length > 0 then
                    let! i = reset
                    let! pc = pcs.[i%pcs.Length]
                    yield pc

            }

        let sg =
            Sg.LodTreeNode(config.stats, picktrees, true, config.budget, config.splitfactor, config.renderBounds, config.maxSplits, win.Time, instances) :> ISg
            |> Sg.uniform "PointSize" config.pointSize
            |> Sg.uniform "ViewportSize" win.Sizes
            |> Sg.uniform "PointVisualization" vis
            |> Sg.uniform "MagicExp" config.magicExp
            |> Sg.shader {
                do! PointSetShaders.lodPointSize
                //do! PointSetShaders.cameraLight
                //if msaa then
                //    do! PointSetShaders.lodPointCircularMSAA
                //else
                do! PointSetShaders.lodPointCircular
                //do! PointSetShaders.envMap
            }
            //|> Sg.andAlso thing
            |> Sg.multisample (Mod.constant true)
            |> Sg.viewTrafo v
            |> Sg.projTrafo p
            |> Sg.andAlso cfg
            //|> Sg.andAlso bla
            |> Sg.blendMode (Mod.constant BlendMode.None)


        let switchActive = win.Keyboard.IsDown Keys.M
        let switchThread =
            for i in 1 .. 1 do
                startThread (fun () ->
                    let rand = RandomSystem()
                    while true do
                        System.Threading.Thread.Sleep(rand.UniformInt(100))
                        if Mod.force switchActive then
                            transact (fun () -> reset.Value <- reset.Value + 1)
                    
                ) |> ignore


        win.Keyboard.DownWithRepeats.Values.Add(fun k ->
            match k with

            | Keys.Delete ->
                let i = pcs.[0].Value
                match i.root with
                | :? LodTreeInstance.PointTreeNode as n -> 
                    match n.Delete (Box3d.FromCenterAndSize(i.root.WorldBoundingBox.Center, V3d(10.0, 10.0, 1000.0))) with
                    | Some r ->
                        transact (fun () -> 
                            pcs.[0].Value <- { i with root = r }
                        )
                    | None ->
                        ()
                | _ ->
                    ()


            | Keys.Escape ->
                transact (fun () ->
                    let pc = pcs.[0].Value
                    let bb = pc.root.WorldBoundingBox
                    let f1 = Hull3d.Create (Box3d.FromMinAndSize(bb.Min, bb.Size * V3d(1.0, 1.0, 0.15)))

                    let root = pc.root |> unbox<LodTreeInstance.PointTreeNode>
                    match root.Original with
                    | :? FilteredNode as fn ->
                        let inner = fn.Node
                        Log.warn "%A" inner
                        match root.WithPointCloudNode(inner) with
                        | Some pp ->
                            pcs.[0].Value <- { pc with root = pp }
                        | None ->
                            Log.warn "hinig"
                    | _ -> 
                        match LodTreeInstance.filter (FilterInsideConvexHull3d f1) pc with
                        | Some pc -> 
                            pcs.[0].Value <- pc
                        | None -> 
                            Log.warn "hinig"
                  
                )
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
            //| Keys.M -> 
            //    transact ( fun () -> reset.Value <- reset.Value + 1 )

            | Keys.O -> transact (fun () -> config.pointSize.Value <- config.pointSize.Value / 1.3)
            | Keys.P -> transact (fun () -> config.pointSize.Value <- config.pointSize.Value * 1.3)
            | Keys.Subtract | Keys.OemMinus -> transact (fun () -> config.overlayAlpha.Value <- max 0.0 (config.overlayAlpha.Value - 0.1))
            | Keys.Add | Keys.OemPlus -> transact (fun () -> config.overlayAlpha.Value <- min 1.0 (config.overlayAlpha.Value + 0.1))
        
            | Keys.Up -> transact (fun () -> config.maxSplits.Value <- config.maxSplits.Value + 1); printfn "splits: %A" config.maxSplits.Value
            | Keys.Down -> transact (fun () -> config.maxSplits.Value <- max 1 (config.maxSplits.Value - 1)); printfn "splits: %A" config.maxSplits.Value
            | Keys.F -> transact (fun () -> if config.maxSplits.Value = 0 then printfn "unfreeze"; config.maxSplits.Value <- 12 else printfn "freeze"; config.maxSplits.Value <- 0)
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

            | Keys.N -> transact (fun () -> reset.Value <- reset.Value + 1)
            | Keys.Return -> Log.line "%A" config.stats.Value

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


    let show (args : Args) (pcs : list<LodTreeInstance>) =
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