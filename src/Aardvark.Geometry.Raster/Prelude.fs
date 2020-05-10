namespace Aardvark.Geometry

open Aardvark.Base
open System

[<AutoOpen>]
module PowersOfTwo =

    /// power-of-two exponent
    [<Measure>] type potexp
    type PowerOfTwoExp = int<potexp>

    /// power-of-two
    [<Measure>] type pot

    /// length
    [<Measure>] type length

    let potexp2pot (e : PowerOfTwoExp) =
        if e < 0<potexp> then invalidArg "e" "Exponent must not be negative."
        (1L <<< int e) * 1L<pot>

[<AutoOpen>]
module RasterExtensions =

    type Box2l with
        member this.IsSizePowerOfTwoSquared with get() = this.SizeX = this.SizeY && this.SizeX.IsPowerOfTwo()
        member this.SplitAtCenter () =
            let c = this.Center
            [|
                Box2l(this.Min.X, this.Min.Y,        c.X,        c.Y)
                Box2l(       c.X, this.Min.Y, this.Max.X,        c.Y)
                Box2l(this.Min.X,        c.Y,        c.X, this.Max.Y)
                Box2l(       c.X,        c.Y, this.Max.X, this.Max.Y)
            |]

    type Cell2d with
        static member Create(xy : V2l, e : PowerOfTwoExp) = Cell2d(xy, int e)
        member this.SideLength with get() = Math.Pow(2.0, float this.Exponent)

    type CellRange2 = {
        Origin : Cell2d
        Size : V2l
    }
    with
        member this.BoundingBox with get() =
            let min = this.Origin.BoundingBox.Min
            let max = Cell2d(this.Origin.XY + this.Size, this.Origin.Exponent).BoundingBox.Min
            Box2d(min, max)



(* ---------------------------------  V2 Sketch ----------------------------------------------- *)

namespace Aardvark.Geometry.Quadtree

open Aardvark.Base
open Aardvark.Data
open Aardvark.Geometry
open System

type ITileData =
    abstract member Def : Durable.Def
    abstract member Data : Array
    abstract member Location : CellRange2
    abstract member Window : Box2l option

[<AutoOpen>]
module ITileDataExtensions =
    type ITileData with
        member this.IsPowerOfTwoSquare with get() = this.Location.Size.X = this.Location.Size.Y && this.Location.Size.X.IsPowerOfTwo()
        member this.SampleExponent with get() = this.Location.Origin.Exponent

type TileData<'a>(def : Durable.Def, data : 'a[], location : CellRange2, window : Box2l option) =

    do
        if location.Size.X * location.Size.Y <> data.LongLength then 
            invalidArg "data" "Mismatch of data.Length and location.Size. Invariant b9b09994-2d8e-4e94-9bde-c46b1b6b87ec."

    interface ITileData with
        member _.Def with get() = def
        member _.Data with get() = data :> Array
        member _.Location with get() = location
        member _.Window with get() = window


type IQuadtreeNode =
    abstract member Cell : Cell2d
    abstract member Layers : ITileData[] option
    abstract member SubNodes : IQuadtreeNode option[] option

[<AutoOpen>]
module IQuadtreeNodeExtensions =
    type IQuadtreeNode with
        member this.IsInnerNode with get() = this.SubNodes.IsSome
        member this.IsLeafNode  with get() = this.SubNodes.IsNone

type QuadtreeNode(cell : Cell2d, layers : ITileData[] option, subNodes : IQuadtreeNode option[] option) =

    do
        if layers.IsSome then
            let bb = cell.BoundingBox
            for layer in layers.Value do
            if not(bb.Contains(layer.Location.BoundingBox)) then 
                invalidArg "layers" (sprintf "Layer %A is outside node bounds." layer.Def.Id)
            
        if subNodes.IsSome && subNodes.Value.Length <> 4 then 
            invalidArg "subNodes" "Invariant 20baf723-cf32-46a6-9729-3b4e062ceee5."

    /// Create leaf node.
    new (cell : Cell2d, layers : ITileData[] option) = QuadtreeNode(cell, layers, None)

    interface IQuadtreeNode with
        member _.Cell with get() = cell
        member _.Layers with get() = layers
        member _.SubNodes with get() = subNodes

module Quadtree =

    let inline private invariant condition id =
        if not condition then failwith <| sprintf "Invariant %s" id

    let inline private intersecting (a : IQuadtreeNode) (b : IQuadtreeNode) = a.Cell.Intersects(b.Cell)

    let private extendUpTo (root : Cell2d) (node : IQuadtreeNode option) : IQuadtreeNode option =
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
                QuadtreeNode(parentCell, None, Some subnodes) :> IQuadtreeNode |> Some

    let private mergeLayers (a : ITileData[] option) (b : ITileData[] option) : ITileData[] option =
        match a, b with
        | Some a', Some b' ->
            let mutable merged = Map.empty
            let merge (x : ITileData) : unit =
                match Map.tryFind x.Def.Id merged with
                | Some (y : ITileData) ->
                    if   x.SampleExponent < y.SampleExponent then merged <- merged |> Map.add x.Def.Id x
                    elif y.SampleExponent < x.SampleExponent then merged <- merged |> Map.add y.Def.Id y
                    else failwith "[NOT IMPLEMENTED] collision of two layers with same resolution in same place"
                | None   -> merged <- merged |> Map.add x.Def.Id x
            for x in a' do merge x
            for y in b' do merge y
            merged |> Map.toArray |> Array.map (fun (_, v) -> v) |> Some
        | Some _,  None    -> a
        | None,    Some _  -> b
        | None,    None    -> None

    let rec private mergeSameRoot (a : IQuadtreeNode option) (b : IQuadtreeNode option) : IQuadtreeNode option =
        match a, b with
        | Some a, Some b ->
            invariant (a.Cell = b.Cell) "641da2e5-a7ea-4692-a96b-94440453ff1e."
            let cell = a.Cell
            match a.SubNodes, b.SubNodes with
            | Some xs, Some ys -> // inner/inner
                let zs = Array.map2 mergeSameRoot xs ys
                QuadtreeNode(cell, None, Some zs) :> IQuadtreeNode |> Some
            | Some xs, None    -> // inner/leaf
                QuadtreeNode(cell, None, Some xs) :> IQuadtreeNode |> Some
            | None,    Some ys -> // leaf/inner
                QuadtreeNode(cell, None, Some ys) :> IQuadtreeNode |> Some
            | None,    None    -> // leaf/leaf
                let layers = mergeLayers a.Layers b.Layers
                QuadtreeNode(cell, layers, None) :> IQuadtreeNode |> Some
        | Some a, None   -> Some a
        | None,   Some b -> Some b
        | None,   None   -> None
    
    /// Sets/merges i-th subnode of an inner node.
    let private setOrMergeIthSubnode (i : int) (node : IQuadtreeNode) (newSubnode : IQuadtreeNode option) : IQuadtreeNode =
        invariant node.SubNodes.IsSome "f74ba958-cf53-4336-944f-46ef2c2b8893"
        if newSubnode.IsSome then invariant (node.Cell.GetQuadrant(i) = newSubnode.Value.Cell) "f5b92710-39de-4054-a67d-e2fbb1c9212c"
        let nss = node.SubNodes.Value |> Array.copy
        nss.[i] <- mergeSameRoot nss.[i] newSubnode
        QuadtreeNode(node.Cell, node.Layers, Some nss) :> IQuadtreeNode

    let rec private mergeIntersecting (a : IQuadtreeNode option) (b : IQuadtreeNode option) : IQuadtreeNode option =
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

                let a'' = if a'.IsLeafNode then QuadtreeNode(a'.Cell, a'.Layers, Some <| Array.create 4 None) :> IQuadtreeNode else a'
                b |> extendUpTo qcell |> setOrMergeIthSubnode qi a'' |> Some

        | Some _, None   -> a
        | None,   Some _ -> b
        | None,   None   -> None

    let private mergeNonIntersecting (a : IQuadtreeNode option) (b : IQuadtreeNode option) : IQuadtreeNode option =
        match a, b with
        | Some a', Some b' ->
            let withCommonRoot = extendUpTo <| Cell2d(Box2d(a'.Cell.BoundingBox, b'.Cell.BoundingBox)) 
            mergeSameRoot (a |> withCommonRoot) (b |> withCommonRoot)
        | Some _,  None    -> a
        | None,    Some _  -> b
        | None,    None    -> None

    let Merge (a : IQuadtreeNode option) ( b : IQuadtreeNode option) : IQuadtreeNode option =
        match a, b with
        | Some a', Some b' -> (if intersecting a' b' then mergeIntersecting else mergeNonIntersecting) a b
        | Some _,  None    -> a
        | None,    Some _  -> b
        | None,    None    -> None
