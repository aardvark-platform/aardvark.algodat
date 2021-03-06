namespace Aardvark.Algodat.App.Viewer


open FSharp.Data.Adaptive
open Aardvark.Base
open Aardvark.SceneGraph
open Aardvark.Rendering
open Aardvark.Rendering.PointSet
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.Rendering.Text

[<StructuredFormatDisplay("{AsString}"); Struct>]
type private Numeric(value : int64) =
    member x.Value = value
    member private x.AsString = x.ToString()

    override x.ToString() =
        let a = abs value
        if a >= 1000000000000L then sprintf "%.3fT" (float value / 1000000000000.0)
        elif a >= 1000000000L then sprintf "%.3fG" (float value / 1000000000.0)
        elif a >= 1000000L then sprintf "%.3fM" (float value / 1000000.0)
        elif a >= 1000L then sprintf "%.1fk" (float value / 1000.0)
        else sprintf "%d" value
        

module ShapeList =

    let empty =
        {
            bounds = Box2d.Invalid
            textBounds = Box2d.Invalid
            concreteShapes = []
            zRange = Range1i.Invalid
            renderTrafo = Trafo3d.Identity
            flipViewDependent = true
            renderStyle = RenderStyle.Normal
        }

    let translated (shift : V2d) (l : ShapeList) =
        let bounds = l.bounds.Translated shift
        let renderTrafo = l.renderTrafo * Trafo3d.Translation(V3d(shift, 0.0))
        
        { 
            bounds = bounds
            textBounds = l.textBounds.Translated shift
            renderTrafo = renderTrafo
            concreteShapes = l.concreteShapes
            zRange = l.zRange
            flipViewDependent = l.flipViewDependent
            renderStyle = RenderStyle.Normal
        }
        
    let union (l : ShapeList) (r : ShapeList) =
        let bounds = Box.Union(l.bounds, r.bounds)
        let renderTrafo = Trafo3d.Translation(V3d(bounds.Center.X, 0.0, 0.0))
        
        let lShapes =
            let l2g = l.renderTrafo * renderTrafo.Inverse

            let m = l2g.Forward
            let mat = M33d.FromRows(m.R0.XYW,m.R1.XYW,m.R3.XYW)

            l.concreteShapes |> List.map (fun (s : ConcreteShape) ->
                { trafo = mat * s.trafo; color = s.color; shape = s.shape; z = s.z }
            )

        let rShapes =
            let r2g = r.renderTrafo * renderTrafo.Inverse
            let m = r2g.Forward
            let mat = M33d.FromRows(m.R0.XYW,m.R1.XYW,m.R3.XYW)
            r.concreteShapes |> List.map (fun (s : ConcreteShape) ->
                { trafo = mat * s.trafo; color = s.color; shape = s.shape; z = s.z }
            )
        
        { 
            bounds = bounds
            textBounds = bounds
            renderTrafo = renderTrafo
            concreteShapes = lShapes @ rShapes
            zRange = Range.Union(l.zRange, r.zRange)
            flipViewDependent = l.flipViewDependent && r.flipViewDependent
            renderStyle = RenderStyle.Normal
        }

    let appendHorizontal (spacing : float) (l : ShapeList) (r : ShapeList) =
        let shift = l.bounds.Max.XO - r.bounds.Min.XO + V2d(spacing, 0.0)
        union l (translated shift r)
        


    let replace (str : string) (rep : Box2d -> list<ConcreteShape>) (s : ShapeList) =
                        
        let rec tryReplaceFront (str : string) (i : int) (current : Box2d) (s : list<ConcreteShape>) =
            if i >= str.Length then
                Some (current, s)
            else
                match s with
                | [] -> None
                | h :: rest ->
                    match h.shape with
                    | :? Glyph as g when g.CodePoint.String.[0] = str.[i] ->
                        tryReplaceFront str (i + 1) (Box.Union(current, h.bounds)) rest
                    | _ ->
                        None
                     
        let rec replace (str : string) (rep : Box2d -> list<ConcreteShape>) (s : list<ConcreteShape>) =
            match tryReplaceFront str 0 Box2d.Invalid s with
            | Some (box, rest) ->
                rep box @ replace str rep rest
            | None ->
                match s with
                | [] -> []
                | h :: rest -> h :: replace str rep rest

        { s with concreteShapes = replace str rep s.concreteShapes }
