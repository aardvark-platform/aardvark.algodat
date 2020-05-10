namespace Aardvark.Geometry.Quadtree

open Aardvark.Base
open Aardvark.Data
open System

[<AutoOpen>]
module Quadtree =

    module Defs =
        let private def id name description (typ : Durable.Def) = Durable.Def(Guid.Parse(id), name, description, typ.Id, false)

        module Quadtree =
            let Node                    = def "e497f9c1-c903-41c4-91de-32bf76e009da" "Quadtree.Node" "A quadtree node. DurableMapAligned16." Durable.Primitives.DurableMapAligned16
            let NodeId                  = def "e46c4163-dd28-43a4-8254-bc21dc3f766b" "Quadtree.NodeId" "Quadtree. Unique id of a node. Guid." Durable.Primitives.GuidDef
            let CellBounds              = def "59258849-5765-4d11-b760-538282063a55" "Quadtree.CellBounds" "Quadtree. Node bounds in cell space. Cell2d." Durable.Aardvark.Cell2d
            let SampleMapping           = def "2f363f2a-2e52-4a86-a620-d8689db511ad" "Quadtree.SampleMapping" "Quadtree. Mapping of sample values to cell space. Box2l." Durable.Aardvark.Box2l
            let SampleSizePotExp        = def "1aa56aca-de4c-4705-9baf-11f8766a0892" "Quadtree.SampleSizePotExp" "Quadtree. Size of a sample is 2^SampleSizePotExp. Int32." Durable.Primitives.Int32
            let SubnodeIds              = def "a2841629-e4e2-4b90-bdd1-7a1a5a41bded" "Quadtree.SubnodeIds" "Quadtree. Subnodes as array of guids. Array length is 4 for inner nodes (where Guid.Empty means no subnode) and no array for leaf nodes. Guid[]." Durable.Primitives.GuidArray
        
            let Heights1f               = def "4cb689c5-b627-4bcd-9db7-5dbd24d7545a" "Quadtree.Heights1f" "Quadtree. Height value per sample. Float32[]." Durable.Primitives.Float32Array
            let Heights1fRef            = def "fcf042b4-fe33-4e28-9aea-f5526600f8a4" "Quadtree.Heights1f.Reference" "Quadtree. Reference to Quadtree.Heights1f. Guid." Durable.Primitives.GuidDef

            let Heights1d               = def "c66a4240-00ef-44f9-b377-0667f279b97e" "Quadtree.Heights1d" "Quadtree. Height value per sample. Float64[]." Durable.Primitives.Float64Array
            let Heights1dRef            = def "baa8ed40-57e3-4f88-8d11-0b547494c8cb" "Quadtree.Heights1d.Reference" "Quadtree. Reference to Quadtree.Heights1d. Guid." Durable.Primitives.GuidDef
        
            let Normals3f               = def "d5166ae4-7bea-4ebe-a3bf-cae8072f8951" "Quadtree.Normals3f" "Quadtree. Normal vector per sample. V3f[]." Durable.Aardvark.V3fArray
            let Normals3fRef            = def "817ecbb6-1e86-41a2-b1ee-53884a27ea97" "Quadtree.Normals3f.Reference" "Quadtree. Reference to Quadtree.Normals3f. Guid." Durable.Primitives.GuidDef
        
            let Heights1dWithOffset     = def "924ae8a2-7b9b-4e4d-a609-7b0381858499" "Quadtree.Heights1dWithOffset" "Quadtree. Height value per sample. Float64 offset + Float32[] values." Durable.Primitives.Float32ArrayWithFloat64Offset
            let Heights1dWithOffsetRef  = def "2815c5a7-48bf-48b6-ba7d-5f4e98f6bc47" "Quadtree.Heights1dWithOffset.Reference" "Quadtree. Reference to Quadtree.Heights1dWithOffset. Guid." Durable.Primitives.GuidDef

            let HeightStdDevs1f         = def "74bfe324-98ad-4f57-8163-120361e1e68e" "Quadtree.HeightStdDevs1f" "Quadtree. Standard deviation per height value. Float32[]." Durable.Primitives.Float32Array
            let HeightStdDevs1fRef      = def "f93e8c5f-7e9e-4e1f-b57a-4475ebf023af" "Quadtree.HeightStdDevs1f.Reference" "Quadtree. Reference to Quadtree.HeightStdDevs1f. Guid." Durable.Primitives.GuidDef

            let Colors3b                = def "378d93ae-45e2-4e6a-9018-f09c88e7d10f" "Quadtree.Colors3b" "Quadtree. Color value per sample. C3b[]." Durable.Aardvark.C4bArray
            let Colors3bRef             = def "d6f8750c-6e94-4c9f-9f83-099268e95cc5" "Quadtree.Colors3b.Reference" "Quadtree. Reference to Quadtree.Colors3b. Guid." Durable.Primitives.GuidDef

            let Colors4b                = def "97b8282c-964a-40e8-a7be-d55ee587b5d4" "Quadtree.Colors4b" "Quadtree. Color value per sample. C4b[]." Durable.Aardvark.C4bArray
            let Colors4bRef             = def "8fe18316-7fa3-4704-869d-c3995d19d03e" "Quadtree.Colors4b.Reference" "Quadtree. Reference to Quadtree.Colors4b. Guid." Durable.Primitives.GuidDef

            let Intensities1i           = def "da564b5d-c5a4-4274-806a-acd04fa206b2" "Quadtree.Intensities1i" "Quadtree. Intensity value per sample. Int32[]." Durable.Primitives.Int32Array
            let Intensities1iRef        = def "b44484ba-e9a6-4e0a-a26a-3641a91ee9cf" "Quadtree.Intensities1i.Reference" "Quadtree. Reference to Quadtree.Intensities1i. Guid." Durable.Primitives.GuidDef


    type ITileData =
        abstract member Def : Durable.Def
        abstract member Data : Array
        abstract member Location : CellRange2
        abstract member Window : Box2l option
        abstract member Merge : ITileData -> ITileData

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
            member this.Merge other : ITileData = failwith "NOT IMPLEMENTED"


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
