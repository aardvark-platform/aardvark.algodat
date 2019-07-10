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

    open System.Text.RegularExpressions
    open System.IO

    let dxf (file : string) =
        let rx = Regex @"AcDb3dPolylineVertex\r\n.*\r\n(.*)\r\n.*\r\n(.*)\r\n.*\r\n(.*)"
        let str = File.ReadAllText file
        let ms = rx.Matches(str)

        let res = System.Collections.Generic.List()
        for m in ms do
            let x = m.Groups.[1].Value |> float
            let y = m.Groups.[2].Value |> float
            let z = m.Groups.[3].Value |> float

            let p1 = V3d(x,y,z)
            res.Add(p1)

        Seq.toArray res

    let pointClouds (win : Option<IRenderWindow>) (size : IMod<V2i>) (time : IMod<_>) (msaa : bool) (camera : IMod<CameraView>) (frustum : IMod<Frustum>) (pcs : list<unit -> LodTreeInstance>) =
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
                fun () -> let t = t() in { t with uniforms = MapExt.add "Overlay" (config.overlayAlpha |> Mod.map ((*) V4d.IIII) :> IMod) t.uniforms }
            )
        
        let overallBounds = 
            pcs |> List.map (fun i -> i().root.WorldBoundingBox) |> Box3d

        let trafo = 
            Trafo3d.Translation(-overallBounds.Center) * 
            Trafo3d.Scale(300.0 / overallBounds.Size.NormMax)

        let pcs =
            pcs |> List.map (fun t () -> t() |> LodTreeInstance.transform trafo) // |> ASet.ofList
            
        let cfg =
            match win with
            | Some win ->
                RenderConfig.toSg win config
            | None -> 
                Sg.empty

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

        let reset = Mod.init 0 


        //let pcs = reset |> Mod.map (fun i -> pcs.[i%pcs.Length]()) |> ASet.ofModSingle
        let pcs = ASet.ofList (pcs |> List.map (fun f -> f() ) )

        let sg =
            Sg.LodTreeNode(config.stats, picktrees, true, config.budget, config.splitfactor, config.renderBounds, config.maxSplits, time, pcs) :> ISg
            |> Sg.uniform "PointSize" config.pointSize
            |> Sg.uniform "ViewportSize" size
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
            |> Sg.multisample (Mod.constant true)
            |> Sg.viewTrafo v
            |> Sg.projTrafo p
            |> Sg.andAlso cfg
            |> Sg.blendMode (Mod.constant BlendMode.None)


        match win with 
        | Some win -> 
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
        | None ->
            ()

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

    type InterpolationPath(path : V3d[], speed : float, ?maxError : float, ?minCount : int) =
        
        static let simplify (maxError : float) (minCount : int) (path : V3d[]) =   
            if path.Length > 2 then
                let impact = Array.zeroCreate (path.Length - 2)

                let mutable p0 = path.[0]
                let mutable p1 = path.[1]
                for i in 1 .. path.Length - 2 do
                    let p2 = path.[i+1]
                    let h2 = Vec.cross (p1 - p0) (p2 - p0) |> Vec.lengthSquared
                    impact.[i-1] <- h2
                    p0 <- p1
                    p1 <- p2

                let indices = SortedSetExt<int>(Seq.init impact.Length id)

                let updateImpact (i : int) =
                    let l, s, r = indices.FindNeighbours(i)
                    if s.HasValue then  
                        let s = 1 + s.Value
                        let l = if l.HasValue then l.Value + 1 else 0
                        let r = if r.HasValue then r.Value + 1 else path.Length - 1 
                        let p0 = path.[l]
                        let p1 = path.[s]
                        let p2 = path.[r]
                        let h2 = Vec.cross (p1 - p0) (p2 - p0) |> Vec.lengthSquared
                        impact.[s-1] <- h2
                        

                let smallestImpact () =
                    let mutable smallest = System.Double.PositiveInfinity
                    let mutable index = -1
                    for i in indices do
                        let v = impact.[i]
                        if v < smallest then
                            index <- i
                            smallest <- v

                    if index >= 0 then
                        Some (index, smallest)
                    else
                        None
                      
                let maxError2 = maxError * maxError
                let mutable fin = false 

                while indices.Count > minCount && not fin do
                    match smallestImpact() with
                    | Some (i, v2) ->
                        if v2 < maxError2 then
                            indices.Remove i |> ignore
                            updateImpact (i-1)
                            updateImpact (i+1)
                        else
                            fin <- true
                        
                    | None ->
                        fin <- true

                let res = Array.zeroCreate (2 + indices.Count)
                res.[0] <- path.[0]
                res.[res.Length - 1] <- path.[path.Length - 1]

                let mutable o = 1
                for i in indices do
                    res.[o] <- path.[i]
                    o <- o + 1

                Log.line "simplified to %.2f%% (%d -> %d)" (100.0 * float res.Length / float path.Length) path.Length res.Length 

                res
            else
                path

        static let rec find (t : float) (l : int) (r : int) (times : float[]) =
            if l > r then
                if r < 0 then
                    let i = r + 1
                    i, i, 0.5
                elif r >= times.Length then
                    let i = times.Length - 1
                    i, i, 0.5
                elif r < times.Length - 1 then
                    let t0 = times.[r]
                    let t1 = times.[r+1]
                    r, r+1, (t - t0) / (t1 - t0)
                else
                    r, r, 0.5
            else
                let m = (l + r) / 2
                let tm = times.[m]

                if t = tm then
                    m, m, 0.0
                elif t > tm then
                    find t (m+1) r times
                else
                    find t l (m-1) times

        let maxError = defaultArg maxError 0.001
        let minCount = defaultArg minCount 100

        let path = simplify maxError minCount path

        let derivatives =
            if path.Length = 0 then 
                [||]
            elif path.Length = 1 then
                [| V3d.Zero |]
            else
                let d = Array.zeroCreate path.Length
                d.[0] <- Vec.normalize (path.[1] - path.[0])
                d.[d.Length-1] <- Vec.normalize (path.[path.Length-1] - path.[path.Length-2])
            
                let mutable p0 = path.[0]
                let mutable p1 = path.[1]
                for i in 1 .. path.Length - 2 do
                    let p2 = path.[i+1]
                    d.[i] <- 0.5 * (Vec.normalize (p2 - p1) + Vec.normalize (p1 - p0))
                    p0 <- p1
                    p1 <- p2

                d
        
        let times =
            let duration = Array.zeroCreate (path.Length - 1)
            for i in 0 .. path.Length - 2 do
                duration.[i] <- Vec.length (path.[i+1] - path.[i]) / speed
            Array.scan (+) 0.0 duration

        let total = times.[times.Length - 1]

        member x.TotalTime = total

        member x.Points = path

        member x.Evaluate(time : float) =
            if time <= 0.0 then
                path.[0] + time * derivatives.[0] * speed
            elif time >= total then
                path.[path.Length - 1] + (time - total) * derivatives.[derivatives.Length - 1] * speed
            else
                let time = time % times.[times.Length - 1]
                let (l, h, t) = find time 0 (times.Length - 1) times
                let p0 = path.[l]
                let p1 = path.[h]
                p0 + (p1 - p0) * t


                //let v0 = Vec.normalize derivatives.[l] * speed 
                //let v1 = Vec.normalize derivatives.[h] * speed 

                //let t0 = times.[l]
                //let t1 = times.[h]
                //let d = t1 - t0
                //let t =
                //    if d > 0.0 then (time - t0) / d
                //    else 0.0

                //let f = max 0.0001 d

                //let inline h0 t = 2.0*t*t*t - 3.0*t*t + 1.0
                //let inline h1 t = -2.0*t*t*t + 3.0*t*t
                //let inline h2 t = t*t*t - 2.0*t*t + t
                //let inline h3 t = t*t*t - t*t
                //let inline h0' t = 6.0*t*t - 6.0*t
                //let inline h1' t = -6.0*t*t + 6.0*t
                //let inline h2' t = 3.0*t*t - 4.0*t + 1.0
                //let inline h3' t = 3.0*t*t - 2.0*t
                //let inline pos t = h0(t) * p0 + h1(t) * p1 + h2(t) * v0 * f + h3(t) * v1 * f
                //let inline dir t = h0'(t) * p0 + h1'(t) * p1 + h2'(t) * v0 * f + h3'(t) * v1 * f |> Vec.normalize

                //pos t 
                

    let fly (t : IMod<float>) (paths : IMod<InterpolationPath>) =

        //let mutable ppi = 0
        //let mutable startTime = Mod.force t
        //let mutable endTime = startTime + paths.[ppi].TotalTime

        Mod.custom (fun token ->
            let path = paths.GetValue token
            let time = t.GetValue(token)

            //while time > endTime do
            //    ppi <- (ppi + 1) % paths.Length
            //    startTime <- endTime
            //    endTime <- startTime + paths.[ppi].TotalTime
            //    Log.error "path %d" ppi

            let p0 = path.Evaluate(time)
            let p1 = path.Evaluate(1.0 + time)


            CameraView.lookAt p0 p1 V3d.OOI
            
        )

    
    let renderImages (pcs : list<unit -> LodTreeInstance>) =
        Ag.initialize()
        Aardvark.Init()


        let size = V2i(1920, 1080)
        let fov = 60.0
        let outputFolder = @"C:\Users\Schorsch\Desktop\oebb2"
        let framerate = 30.0
        let time = Mod.init 0.0
        let currentPath = Mod.init 0
        let speed = 30.0 / 3.6

        //let pcs = pcs |> List.map (fun f -> f())

        use app = new OpenGlApplication(true, false)
        
        let overallBounds = 
            pcs |> List.map (fun i -> i().root.WorldBoundingBox) |> Box3d

        let trafo = 
            Trafo3d.Translation(-overallBounds.Center) * 
            Trafo3d.Scale(300.0 / overallBounds.Size.NormMax)
            
        let bb = Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 300.0)


        let paths =       
            let pathBounds = overallBounds.EnlargedBy(100.0)
            let overallBounds = ()
            // let c = File.readAllText @"C:\Users\Schorsch\Desktop\oebb\pathpoints_31256.txt"
            let lines = dxf @"C:\Users\Schorsch\Desktop\oebb\Trajektorie_Verbindungsbahn.dxf"
            
            let res = System.Collections.Generic.List()
            let result = System.Collections.Generic.List()

            let mutable p0i = pathBounds.Contains lines.[0]
            let mutable i = 1
            while i < lines.Length do
                let l = lines.[i]
                let p1i = pathBounds.Contains l

                if p1i then
                    result.Add l
                elif p0i then
                    res.Add(InterpolationPath(CSharpList.toArray result, speed, 0.0))
                    result.Clear()

                p0i <- p1i
                i <- i + 1



            CSharpList.toArray res

        
        let renderTime = Mod.custom (fun _ -> System.DateTime.Now)
    
        let camera =
            let camPath = currentPath |> Mod.map (fun i -> paths.[i])
            fly time camPath
            |> Mod.map (fun c ->
                let fw = trafo.Forward.TransformDir c.Forward |> Vec.normalize
                let r = trafo.Forward.TransformDir c.Right |> Vec.normalize
                let u = trafo.Forward.TransformDir c.Up |> Vec.normalize
                CameraView(V3d.OOI, trafo.Forward.TransformPos c.Location, fw, u, r)
            )

        let frustum =
            Mod.custom (fun t ->
                let c = camera.GetValue t
                let (minPt, maxPt) = bb.GetMinMaxInDirection(c.Forward)
                
                let near = Vec.dot c.Forward (minPt - c.Location)
                let far = Vec.dot c.Forward (maxPt - c.Location)
                let near = max (max 0.05 near) (far / 1000.0)

                Frustum.perspective fov near far (float size.X / float size.Y)
            )

        let config, pcs = pointClouds None (Mod.constant size) renderTime false camera frustum pcs
        transact (fun () -> 
            config.lighting.Value <- false    
            config.pointSize.Value <- config.pointSize.Value / 1.3 / 1.3
        )


        let sky =
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
            |> Sg.viewTrafo (camera |> Mod.map CameraView.viewTrafo)
            |> Sg.projTrafo (frustum |> Mod.map Frustum.projTrafo)


        let sg =
            Sg.ofList [
                pcs

                //Util.coordinateBox
                //|> Sg.onOff (config.background |> Mod.map ((=) Background.CoordinateBox))

                //Sg.ofList (
                //    skyboxes |> Map.toList |> List.map (fun (id, tex) ->
                //        Sg.farPlaneQuad
                //        |> Sg.uniform "EnvMap" tex
                //        |> Sg.onOff (config.background |> Mod.map ((=) (Background.Skybox id)))
                //    )
                //)
                //|> Sg.shader {
                //    do! Util.Shader.reverseTrafo
                //    do! Util.Shader.envMap
                //}

            ]
            |> Sg.viewTrafo (camera |> Mod.map CameraView.viewTrafo)
            |> Sg.projTrafo (frustum |> Mod.map Frustum.projTrafo)

        let signature =
            app.Runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, { format = RenderbufferFormat.Rgba8; samples = 8 }
                DefaultSemantic.Depth, { format = RenderbufferFormat.Depth24Stencil8; samples = 8 }
            ]

        let color = app.Runtime.CreateRenderbuffer(size, RenderbufferFormat.Rgba8, 8)
        let depth = app.Runtime.CreateRenderbuffer(size, RenderbufferFormat.Depth24Stencil8, 8)
        let resolved = app.Runtime.CreateTexture(size, TextureFormat.Rgba8, 1, 1, 1)

        let fbo = 
            app.Runtime.CreateFramebuffer(
                signature,
                [
                    DefaultSemantic.Colors, color :> IFramebufferOutput
                    DefaultSemantic.Depth, depth :> IFramebufferOutput
                ]
            )

        let task = 
            RenderTask.ofList [
                app.Runtime.CompileClear(signature, Mod.constant (C4f(0.0, 0.0, 0.0, 0.0)), Mod.constant 1.0)
                app.Runtime.CompileRender(signature, sg)
            ]
            
        let backgroundTask = 
            RenderTask.ofList [
                app.Runtime.CompileClear(signature, Mod.constant (C4f(0.0, 0.0, 0.0, 0.0)), Mod.constant 1.0)
                app.Runtime.CompileRender(signature, sky)
            ]

        let finished =
            Mod.custom (fun t ->
                let s = config.stats.GetValue(t)
                s.quality >= s.maxQuality
            )

        let renderUntilFinished(min : int) =
            let mutable i = 0
            while i < min || not (Mod.force finished) do
                task.Run(RenderToken.Empty, fbo)
                transact (fun () -> renderTime.MarkOutdated())
                i <- i + 1


            app.Runtime.ResolveMultisamples(color, resolved, ImageTrafo.Rot0)
            let pc = app.Runtime.Download(resolved)
            
            backgroundTask.Run(RenderToken.Empty, fbo)
            app.Runtime.ResolveMultisamples(color, resolved, ImageTrafo.Rot0)
            let background = app.Runtime.Download(resolved)
            background, pc





        for pi in 0 .. paths.Length - 1 do
            transact (fun () -> currentPath.Value <- pi)
            let path = paths.[pi]

            let frameCount = ceil (path.TotalTime * framerate) |> int
            let outputFolder = 
                let p = System.IO.Path.Combine(outputFolder, sprintf "path%03d" pi)
                if not (System.IO.Directory.Exists p) then System.IO.Directory.CreateDirectory p |> ignore
                p

            Log.startTimed "render path with %d frames" frameCount


            let mutable i = 0
            let mutable t = 0.0

            renderUntilFinished 20 |> ignore
            while i < frameCount do
                transact (fun () -> time.Value <- t)
                System.Threading.Thread.Sleep 16
                let background, img = renderUntilFinished 5

                let name = sprintf "a%06d.png" i
                let path = System.IO.Path.Combine(outputFolder, name)
                img.SaveAsImage path

                let name = sprintf "b%06d.png" i
                let path = System.IO.Path.Combine(outputFolder, name)
                background.SaveAsImage path


                t <- t + (1.0 / framerate)
                i <- i + 1
                Report.Progress (float i / float frameCount)

            Log.stop()


    //    ()

    let show (args : Args) (pcs : list<unit -> LodTreeInstance>) =
        Ag.initialize()
        Aardvark.Init()

        use app = new OpenGlApplication(true, false)
        use win = app.CreateGameWindow(8)
        
        let overallBounds = 
            pcs |> List.map (fun i -> i().root.WorldBoundingBox) |> Box3d

        let trafo = 
            Trafo3d.Translation(-overallBounds.Center) * 
            Trafo3d.Scale(300.0 / overallBounds.Size.NormMax)
            
        let bb = Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 300.0)

        let speed = 20.0

        let paths =       
            let pathBounds = overallBounds.EnlargedBy(100.0)
            let overallBounds = ()
            // let c = File.readAllText @"C:\Users\Schorsch\Desktop\oebb\pathpoints_31256.txt"
            let lines = dxf @"C:\Users\Schorsch\Desktop\oebb\Trajektorie_Verbindungsbahn.dxf"
            
            let res = System.Collections.Generic.List()
            let result = System.Collections.Generic.List()

            let mutable p0i = pathBounds.Contains lines.[0]
            let mutable i = 1
            while i < lines.Length do
                let l = lines.[i]
                let p1i = pathBounds.Contains l

                if p1i then
                    result.Add l
                elif p0i then
                    res.Add(InterpolationPath(CSharpList.toArray result, speed, 0.0))
                    result.Clear()

                p0i <- p1i
                i <- i + 1



            CSharpList.toArray res

        let colors = [| V4f.IOOI; V4f.OIOI |]

        let lines = 
            paths |> Array.mapi (fun pi p ->

                let pos = p.Points |> Array.map (trafo.Forward.TransformPos >> V3f)
                Sg.draw IndexedGeometryMode.LineStrip
                |> Sg.vertexAttribute' DefaultSemantic.Positions pos 
                |> Sg.vertexBufferValue DefaultSemantic.Colors (Mod.constant colors.[pi % colors.Length])
                |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! DefaultSurfaces.thickLine
                }
                |> Sg.uniform "LineWidth" (Mod.constant 10.0)
            )
            |> Sg.ofArray
    
        let camera =
            //let sw = System.Diagnostics.Stopwatch.StartNew()
            //let camPath = paths.[0] // |> Array.map (fun l -> Line3d(trafo.Forward.TransformPos l.P0, trafo.Forward.TransformPos l.P1))
            //fly (win.Time |> Mod.map (fun _ -> sw.Elapsed.TotalSeconds)) paths
            //|> Mod.map (fun c ->
            //    let fw = trafo.Forward.TransformDir c.Forward |> Vec.normalize
            //    let r = trafo.Forward.TransformDir c.Right |> Vec.normalize
            //    let u = trafo.Forward.TransformDir c.Up |> Vec.normalize
            //    CameraView(V3d.OOI, trafo.Forward.TransformPos c.Location, fw, u, r)
            //)
            CameraView.lookAt (V3d(10,10,10)) V3d.OOO V3d.OOI
            |> DefaultCameraController.control win.Mouse win.Keyboard win.Time

            
        let frustum =
            Mod.custom (fun t ->
                let s = win.Sizes.GetValue t
                let c = camera.GetValue t

                let (minPt, maxPt) = bb.GetMinMaxInDirection(c.Forward)
                
                let near = 0.01 //Vec.dot c.Forward (minPt - c.Location)
                let far = 1000.0 //Vec.dot c.Forward (maxPt - c.Location)
                let near = max (max 0.05 near) (far / 1000.0)

                Frustum.perspective args.fov near far (float s.X / float s.Y)
            )


        let config, pcs = pointClouds (Some (win :> IRenderWindow)) win.Sizes win.Time args.msaa camera frustum pcs
        
        let sg =
            Sg.ofList [
                pcs

                //lines

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

