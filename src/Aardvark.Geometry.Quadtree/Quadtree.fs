namespace Aardvark.Geometry.Quadtree

open Aardvark.Base
open Aardvark.Data
open System

(*
    Layer.
*)

type LayerMapping(origin : Cell2d, size : V2i) =

    new (origin : Cell2d, width : int, height : int) =
        LayerMapping(origin, V2i(width, height))

    new (origin : V2l, size : V2i, exponent : int) =
        LayerMapping(Cell2d(origin, exponent), size)

    new (originX : int64, originY : int64, width : int, height : int, exponent : int) =
        LayerMapping(Cell2d(originX, originY, exponent), V2i(width, height))

    member this.Origin with get() = origin
    member this.Size with get() = size
    member this.Width with get() = size.X
    member this.Height with get() = size.Y
    member this.Max with get() = origin.XY + V2l size

    member this.Contains (box : Box2l) =
        let max = this.Max
        box.Min.X >= origin.X && box.Min.Y >= origin.Y && box.Max.X <= max.X && box.Max.Y <= max.Y

    member this.BoundingBox with get() =
        let min = origin.BoundingBox.Min
        let max = Cell2d(this.Max, origin.Exponent).BoundingBox.Min
        Box2d(min, max)

type ILayer =
    abstract member Def : Durable.Def
    abstract member Data : Array
    abstract member Location : LayerMapping
    abstract member Window : Box2l option
    abstract member Merge : ILayer -> ILayer

type Layer<'a> = {
    Def : Durable.Def
    Data : 'a[]
    Location : LayerMapping
    Window : Box2l option
    }
with
    interface ILayer with
        member this.Def with get() = this.Def
        member this.Data with get() = this.Data :> Array
        member this.Location with get() = this.Location
        member this.Window with get() = this.Window
        member this.Merge other : ILayer = failwith "NOT IMPLEMENTED"

module Layer =

    let Create def data (location : LayerMapping) = 
        if location.Width * location.Height <> Array.length data then
            invalidArg "data" "Mismatch of data.Length and location.Size. Invariant b9b09994-2d8e-4e94-9bde-c46b1b6b87ec."
        { Def = def; Data = data; Location = location; Window = None }

    let Window newWindow layerData =
        if not(layerData.Location.Contains(newWindow)) then
            invalidArg "window" "New window is not fully contained in layer. Invariant 032e0790-8e84-4967-b767-e8373abf2348."
        { layerData with Window = Some newWindow }

    
[<AutoOpen>]
module ILayerExtensions =
    type ILayer with
        member this.IsPowerOfTwoSquare with get() = this.Location.Size.X = this.Location.Size.Y && this.Location.Size.X.IsPowerOfTwo()
        member this.SampleExponent with get() = this.Location.Origin.Exponent


(*
    Node.
*)

type INode =
    abstract member Cell : Cell2d
    abstract member Layers : ILayer[] option
    abstract member SubNodes : INode option[] option

[<AutoOpen>]
module INodeExtensions = 
    type INode with
        member this.IsInnerNode with get() = this.SubNodes.IsSome
        member this.IsLeafNode  with get() = this.SubNodes.IsNone

type Node(cell : Cell2d, layers : ILayer[] option, subNodes : INode option[] option) =

    do
        if layers.IsSome then
            let bb = cell.BoundingBox
            for layer in layers.Value do
            if not(bb.Contains(layer.Location.BoundingBox)) then 
                invalidArg "layers" (sprintf "Layer %A is outside node bounds." layer.Def.Id)
            
        if subNodes.IsSome && subNodes.Value.Length <> 4 then 
            invalidArg "subNodes" "Invariant 20baf723-cf32-46a6-9729-3b4e062ceee5."

    new (cell : Cell2d, layers : ILayer[] option) = Node(cell, layers, None)

    interface INode with
        member _.Cell with get() = cell
        member _.Layers with get() = layers
        member _.SubNodes with get() = subNodes

(*
    Merge.
*)

module Merge =

    let inline private invariant condition id =
        if not condition then failwith <| sprintf "Invariant %s" id

    let inline private intersecting (a : INode) (b : INode) = a.Cell.Intersects(b.Cell)

    let private extendUpTo (root : Cell2d) (node : INode option) : INode option =
        match node with
        | None -> None
        | Some node ->
            invariant (not(root.Contains(node.Cell)))          "a48ca4ab-3f20-45ff-bd3c-c08f2a8fcc15."
            invariant (root.Exponent < node.Cell.Exponent)     "cda4b28d-4449-4db2-80b8-40c0617ecf22."

            if root.Exponent = node.Cell.Exponent then
                Some node
            else
                invariant (root.Exponent > node.Cell.Exponent) "56251fd0-5344-4d0a-b76b-815cdd5a7607."
                let parentCell = node.Cell.Parent
                let qi = root.GetQuadrant(parentCell)
                invariant qi.HasValue                          "09575aa7-38b3-4afa-bb63-389af3301fc0."
                let subnodes = Array.create 4 None
                subnodes.[qi.Value] <- Some node
                Node(parentCell, None, Some subnodes) :> INode |> Some

    let private mergeLayers (a : ILayer[] option) (b : ILayer[] option) : ILayer[] option =
        match a, b with
        | Some a', Some b' ->
            let mutable merged = Map.empty
            let merge (x : ILayer) : unit =
                match Map.tryFind x.Def.Id merged with

                | Some (y : ILayer) ->

                    let handleCollision () =
                        let z = x.Merge y
                        merged <- merged |> Map.add z.Def.Id z

                    if x.Location = y.Location then
                        if   x.SampleExponent < y.SampleExponent then merged <- merged |> Map.add x.Def.Id x
                        elif y.SampleExponent < x.SampleExponent then merged <- merged |> Map.add y.Def.Id y
                        else handleCollision()
                    else
                        handleCollision()

                | None   -> merged <- merged |> Map.add x.Def.Id x

            for x in a' do merge x
            for y in b' do merge y
            merged |> Map.toArray |> Array.map (fun (_, v) -> v) |> Some
        | Some _,  None    -> a
        | None,    Some _  -> b
        | None,    None    -> None

    let rec private mergeSameRoot (a : INode option) (b : INode option) : INode option =
        match a, b with
        | Some a, Some b ->
            invariant (a.Cell = b.Cell) "641da2e5-a7ea-4692-a96b-94440453ff1e."
            let cell = a.Cell
            match a.SubNodes, b.SubNodes with
            | Some xs, Some ys -> // inner/inner
                let zs = Array.map2 mergeSameRoot xs ys
                Node(cell, None, Some zs) :> INode |> Some
            | Some xs, None    -> // inner/leaf
                Node(cell, None, Some xs) :> INode |> Some
            | None,    Some ys -> // leaf/inner
                Node(cell, None, Some ys) :> INode |> Some
            | None,    None    -> // leaf/leaf
                let layers = mergeLayers a.Layers b.Layers
                Node(cell, layers, None) :> INode |> Some
        | Some a, None   -> Some a
        | None,   Some b -> Some b
        | None,   None   -> None
    
    let private setOrMergeIthSubnode (i : int) (node : INode) (newSubnode : INode option) : INode =
        invariant node.SubNodes.IsSome "f74ba958-cf53-4336-944f-46ef2c2b8893"
        if newSubnode.IsSome then invariant (node.Cell.GetQuadrant(i) = newSubnode.Value.Cell) "f5b92710-39de-4054-a67d-e2fbb1c9212c"
        let nss = node.SubNodes.Value |> Array.copy
        nss.[i] <- mergeSameRoot nss.[i] newSubnode
        Node(node.Cell, node.Layers, Some nss) :> INode

    let rec private mergeIntersecting (a : INode option) (b : INode option) : INode option =
        match a, b with
        | Some a', Some b' ->
            if   a'.Cell.Exponent = b'.Cell.Exponent then mergeSameRoot     a b
            elif a'.Cell.Exponent < b'.Cell.Exponent then mergeIntersecting a b
            else
                invariant (a'.Cell.Exponent > b'.Cell.Exponent) "4b40bc08-b19d-4f49-b6e5-f321bf1e7dd0."
                invariant (not(a'.Cell.Contains(b'.Cell)))      "9a44a9ea-2996-46ff-9cc6-c9de1992465d."
                invariant (b'.Cell.Contains(a'.Cell))           "7d3465b9-90c7-4e7d-99aa-67e5383fb124."

                let qi = a'.Cell.GetQuadrant(b'.Cell).Value
                let qcell = a'.Cell.GetQuadrant(qi)

                let a'' = if a'.IsLeafNode then Node(a'.Cell, a'.Layers, Some <| Array.create 4 None) :> INode else a'
                b |> extendUpTo qcell |> setOrMergeIthSubnode qi a'' |> Some

        | Some _, None   -> a
        | None,   Some _ -> b
        | None,   None   -> None

    let private mergeNonIntersecting (a : INode option) (b : INode option) : INode option =
        match a, b with
        | Some a', Some b' ->
            let withCommonRoot = extendUpTo <| Cell2d(Box2d(a'.Cell.BoundingBox, b'.Cell.BoundingBox)) 
            mergeSameRoot (a |> withCommonRoot) (b |> withCommonRoot)
        | Some _,  None    -> a
        | None,    Some _  -> b
        | None,    None    -> None

    let Merge (a : INode option) ( b : INode option) : INode option =
        match a, b with
        | Some a', Some b' -> (if intersecting a' b' then mergeIntersecting else mergeNonIntersecting) a b
        | Some _,  None    -> a
        | None,    Some _  -> b
        | None,    None    -> None
