namespace Hum


open Aardvark.Base.Incremental
open Aardvark.Base
open Aardvark.SceneGraph
open Aardvark.Base.Rendering
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
    let replace (str : string) (rep : Box2d -> list<ConcreteShape>) (s : ShapeList) =
                        
        let rec tryReplaceFront (str : string) (i : int) (current : Box2d) (s : list<ConcreteShape>) =
            if i >= str.Length then
                Some (current, s)
            else
                match s with
                | [] -> None
                | h :: rest ->
                    match h.shape with
                    | :? Glyph as g when g.Character = str.[i] ->
                        tryReplaceFront str (i + 1) (Box2d.Union(current, h.bounds)) rest
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
