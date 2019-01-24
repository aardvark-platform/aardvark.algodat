namespace Hum



open Aardvark.Base.Incremental
open Aardvark.Base
open Aardvark.SceneGraph
open Aardvark.Base.Rendering
open Aardvark.Rendering.PointSet
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.Rendering.Text

type RenderConfig =
    {
        pointSize       : ModRef<float>
        overlayAlpha    : ModRef<float>
        maxSplits       : ModRef<int>
        renderBounds    : ModRef<bool>
        budget          : ModRef<int64>
        lighting        : ModRef<bool>
        colors          : ModRef<bool>
        quality         : ModRef<float>
        maxQuality       : ModRef<float>
    }


module RenderConfig =

    let toSg (win : IRenderWindow) (cfg : RenderConfig) =
        let pi = "$$$$$$"
        let lines =
            [
                "PointScale",       "O", "P", cfg.pointSize |> Mod.map (sprintf "%.2f")
                "Overlay",          "-", "+", cfg.overlayAlpha |> Mod.map (sprintf "%.1f")
                "BoundingBoxes",    "B", "B", cfg.renderBounds |> Mod.map (function true -> "on" | false -> "off")
                "PointBudget",      "X", "C", cfg.budget |> Mod.map (fun v -> if v < 0L then "off" else string (Numeric v))
                "Lighting",         "L", "L", cfg.lighting |> Mod.map (function true -> "on" | false -> "off")
                "Coloring",         "V", "V", cfg.colors |> Mod.map (function true -> "on" | false -> "off")
                "Quality",          pi,  pi,  cfg.quality |> Mod.map (fun q -> sprintf "%.0f%%" (100.0 * q))
            ]

        let maxNameLength = 
            lines |> List.map (fun (n,_,_,_) -> n.Length) |> List.max

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
                    lines |> List.map (fun (_,_,_,v) -> v.Length) |> List.max
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
                Trafo3d.Scale(20.0) *

                Trafo3d.Scale(1.0 / float s.X, 1.0 / float s.Y, 1.0) *
                Trafo3d.Scale(2.0, 2.0, 2.0) *
                Trafo3d.Translation(-1.0, 1.0, 0.0)
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

        let shapes =
            adaptive {
                let! a = active
                if a then
                    let! text = text
                    let! q = cfg.quality
                    let! mq = cfg.maxQuality
                    return 
                        text
                        |> layoutWithBackground 1.0
                        |> ShapeList.replace (sprintf "(%s)" pi) (fun (box : Box2d) ->
                            let w = 0.1
                            let offset = box.Min
                            let size =  V2d(box.Size.X, min 0.8 box.Size.Y)
                            let bgBounds = Box2d.FromMinAndSize(offset, size).ShrunkBy(w / 2.0)
                            let maxBounds = Box2d.FromMinAndSize(bgBounds.Min, V2d(bgBounds.SizeX * mq, bgBounds.SizeY)).ShrunkBy(w / 2.0)
                            let curBounds = Box2d.FromMinAndSize(bgBounds.Min, V2d(bgBounds.SizeX * q, bgBounds.SizeY)).ShrunkBy(w / 2.0)
                            let c = HeatMap.color(1.0 - q)

                            [
                                ConcreteShape.roundedRectangle (C4b(255uy,255uy,255uy,255uy)) w 0.8 bgBounds
                                ConcreteShape.fillRoundedRectangle (C4b(255uy,255uy,255uy,150uy)) 0.8 maxBounds
                                ConcreteShape.fillRoundedRectangle (C4b(c.R,c.G,c.B,200uy)) 0.8 curBounds
                            ]
                        )
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
