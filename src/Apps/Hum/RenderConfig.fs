namespace Hum



open Aardvark.Base.Incremental
open Aardvark.Base
open Aardvark.SceneGraph
open Aardvark.Base.Rendering
open Aardvark.Rendering.PointSet
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.Rendering.Text

type Background =
    | Skybox
    | CoordinateBox
    | Black

type RenderConfig =
    {
        pointSize       : ModRef<float>
        overlayAlpha    : ModRef<float>
        maxSplits       : ModRef<int>
        renderBounds    : ModRef<bool>
        budget          : ModRef<int64>
        lighting        : ModRef<bool>
        colors          : ModRef<bool>
        magicExp        : ModRef<float>
        background      : ModRef<Background>
        stats           : ModRef<LodRendererStats>
        antialias       : ModRef<bool>
        fancy           : ModRef<bool>
    }


module RenderConfig =

    let toSg (win : IRenderWindow) (cfg : RenderConfig) =

    
        let pi = "($qual$)"
        let pm = "($memo$)"
        let pt = "($pts$$)"
        
        let totalMem = cfg.stats |> Mod.map (fun s -> sprintf "%A (%A)" s.usedMemory s.allocatedMemory)
        let counts = cfg.stats |> Mod.map (fun s -> sprintf "%An / %Ap" (Numeric(int64 s.totalNodes)) (Numeric s.totalPrimitives))
        
        let prim =
            Mod.custom (fun t ->
                let v = cfg.stats.GetValue(t).totalPrimitives
                let b = cfg.budget.GetValue t

                let n = sprintf "%Ap" (Numeric v)
                if b < 0L then n
                else sprintf "%s %s" pt n
            )

        let renderTime = cfg.stats |> Mod.map (fun s -> string s.renderTime)

        let lines =
            [
                "Scale",            "O", "P", cfg.pointSize |> Mod.map (sprintf "%.2f")
                "Overlay",          "-", "+", cfg.overlayAlpha |> Mod.map (sprintf "%.1f")
                "Boxes",            "B", "B", cfg.renderBounds |> Mod.map (function true -> "on" | false -> "off")
                "Budget",           "X", "C/Y", cfg.budget |> Mod.map (fun v -> if v < 0L then "off" else string (Numeric v))
                "Light",            "L", "L", cfg.lighting |> Mod.map (function true -> "on" | false -> "off")
                "Color",            "V", "V", cfg.colors |> Mod.map (function true -> "on" | false -> "off")
                "Fancy" ,           "1", "1", cfg.fancy |> Mod.map (function true -> "on" | false -> "off")
                "Antialias",        "2", "2", cfg.antialias |> Mod.map (function true -> "on" | false -> "off")
                "MagicExp",         "U", "I", cfg.magicExp |> Mod.map (sprintf "%.2f")
                "Memory",           " ", " ", totalMem
                "Quality",          " ", " ", Mod.constant pi
                "Points",           " ", " ", prim
            ]

        let maxNameLength = 
            lines |> List.map (fun (n,l,h,_) -> n.Length) |> List.max

        let pad (len : int) (i : string) (str : string) =
            let padding = if str.Length < len then System.String(' ', len - str.Length) else ""
            sprintf "%s%s%s" str i padding

        let text =
            let lines = lines |> List.map (fun (n,l,h,v) -> pad maxNameLength ": " n, l, h, v)

            Mod.custom (fun t ->
                let lines =
                    lines |> List.map (fun (n,l,h,v) -> 
                    let v = v.GetValue t
                    (n,l,h,v)
                )

                let maxValueLength = 
                    lines |> List.map (fun (_,l,h,v) -> if System.String.IsNullOrWhiteSpace l && System.String.IsNullOrWhiteSpace h then 0 else v.Length) |> List.max
                lines 
                |> List.map (fun (n,l,h,v) -> 
                    let v = pad maxValueLength "" v
                    
                    if l <> h then
                        sprintf "%s%s (%s/%s)" n v l h
                    else    
                        if System.String.IsNullOrWhiteSpace l then
                            sprintf "%s%s" n v
                        else
                            sprintf "%s%s (%s)" n v l
                )
                |> String.concat "\r\n"
            )

        let trafo =
            win.Sizes |> Mod.map (fun s ->
                Trafo3d.Translation(1.0, -1.5, 0.0) * 
                Trafo3d.Scale(18.0) *

                Trafo3d.Scale(1.0 / float s.X, 1.0 / float s.Y, 1.0) *
                Trafo3d.Scale(2.0, 2.0, 2.0) *
                Trafo3d.Translation(-1.0, 1.0, -1.0)
            )
            
        let config = { TextConfig.Default with align = TextAlignment.Left }

        let active = Mod.init false

        let layoutWithBackground (alpha : float) (text : string) =
            let fgAlpha = byte (255.0 * (clamp 0.0 1.0 alpha))
            let bgAlpha = byte (255.0 * (clamp 0.0 1.0 alpha) * 0.6)
            let config = { config with color = C4b(255uy, 255uy, 255uy, fgAlpha) }
            let shapes = config.Layout text
            let realBounds = shapes.bounds
            let bounds = realBounds.EnlargedBy(3.0, 0.5, 0.2, 3.0)
            let rect = ConcreteShape.fillRoundedRectangle (C4b(0uy,0uy,0uy,bgAlpha)) 0.8 bounds
            ShapeList.prepend rect shapes

        let progress (v : float) (mv : float) (box : Box2d) =
            let q = clamp 0.03 1.0 v
            let mq = clamp 0.03 1.0 mv
            let w = 0.1
            let offset = box.Min
            let size =  V2d(box.Size.X, min 0.8 box.Size.Y)
            let bgBounds = Box2d.FromMinAndSize(offset, size).ShrunkBy(w / 2.0)
            let maxBounds = Box2d.FromMinAndSize(bgBounds.Min, V2d(bgBounds.SizeX * mq, bgBounds.SizeY)).ShrunkBy(w / 2.0)
            let curBounds = Box2d.FromMinAndSize(bgBounds.Min, V2d(bgBounds.SizeX * q, bgBounds.SizeY)).ShrunkBy(w / 2.0)
            let mutable c = HeatMap.color(1.0 - q)

            let max = if mv > 0.0 then mv else 1.0

            if q < max then c.A <- 200uy
            else c.A <- 150uy

            [
                yield ConcreteShape.roundedRectangle (C4b(255uy,255uy,255uy,255uy)) w 0.8 bgBounds
                if mv > 0.0 then yield ConcreteShape.fillRoundedRectangle (C4b(255uy,255uy,255uy,150uy)) 0.8 maxBounds
                yield ConcreteShape.fillRoundedRectangle c 0.8 curBounds
            ]
            
        let inline progress' (v : ^a) (max : ^a) =
            progress (v / max) -1.0

        let shapes =
            adaptive {
                let! a = active
                if a then
                    let! text = text
                    let! stats = cfg.stats
                    let! b = cfg.budget
                    return 
                        text
                        |> layoutWithBackground 1.0
                        |> ShapeList.replace pi (progress stats.quality stats.maxQuality)
                        |> ShapeList.replace pm (progress' stats.usedMemory stats.allocatedMemory)
                        |> ShapeList.replace pt (progress' (float stats.totalPrimitives) (float b))
                else
                    return layoutWithBackground 0.5 "press 'H' for help"
            }

        win.Keyboard.DownWithRepeats.Values.Add (fun k ->
            if k = Keys.H then
                transact (fun () -> active.Value <- not active.Value)
            else
                ()
        )

        Sg.shape shapes
            |> Sg.trafo trafo
            |> Sg.blendMode (Mod.constant BlendMode.Blend)
