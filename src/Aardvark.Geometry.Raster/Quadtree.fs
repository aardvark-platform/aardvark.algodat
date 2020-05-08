namespace Aardvark.Geometry

open Aardvark.Base
open Aardvark.Data
open System
open System.Collections.Generic

module Raster =

    type Map<'k,'v when 'k : comparison> with
        static member ofDictionary(dict : IReadOnlyDictionary<'k,'v>) = dict |> Seq.map (fun e -> (e.Key, e.Value)) |> Map.ofSeq

    type private Def = Durable.Def

    module Defs =
        let private def id name description (typ : Def) = Def(Guid.Parse(id), name, description, typ.Id, false)

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

    open Defs

    let inline private sqr x = x * x

    type NodeData = Map<Guid,obj>

    [<AutoOpen>]
    module private NodeData =

        let contains (def : Def) (data : NodeData) = data.ContainsKey(def.Id)

        let add (def : Durable.Def) (value : 'a) (data : NodeData) : NodeData = 
            data |> Map.add def.Id (value :> obj)

        let tryAdd (def : Durable.Def) (value : 'a option) (data : NodeData) : NodeData =
            if value.IsSome then data |> Map.add def.Id (value.Value :> obj) else data

        let inline get (def : Def) (data : NodeData) =
            data.[def.Id] :?> 'a

        let tryGet (def : Def) (data : NodeData) =
            match data.TryGetValue(def.Id) with | false, _ -> None | true, x -> Some (x :?> 'a)

        let layers (data : NodeData) = seq {
            for kv in data do
                match kv.Value with
                | :? Array as a -> yield (kv.Key, a)
                | _ -> ()
            }

    type Box2c(min : V2l, max : V2l, exponent : PowerOfTwoExp) =
        
        new (box : Box2l, exponent : PowerOfTwoExp) = Box2c(box.Min, box.Max, exponent)
        member ____.Min with get() = min
        member ____.Max with get() = max
        member ____.Exponent with get() = exponent
        member ____.BoundingBox with get() =
            let d = Math.Pow(2.0, float exponent)
            Box2d(d * V2d min, d * V2d max)
        member ____.Width with get() = max.X - min.X
        member ____.Height with get() = max.Y - min.Y
        member this.Area with get() = this.Width * this.Height

    type NodeDataLayers(mapping : Box2c, data : NodeData) =
    
        do
            for (id, array) in data |> layers do
                if int64 array.Length <> mapping.Area then 
                    invalidArg "data" (sprintf "Layer %A has invalid size." id)

        new (mapping : Box2c, [<ParamArray>] layers : ValueTuple<Guid,obj>[]) =
            NodeDataLayers(mapping, Map(layers |> Array.map (fun struct (k, v) -> (k, v))))

        member this.Mapping with get() = mapping

        member this.Layers with get() = seq {
            for kv in data do
                match kv.Value with
                | :? Array as a -> yield (kv.Key, a)
                | _ -> ()
            }


    /// Create data map for RasterNode2d.
    let CreateNodeData
        (
            id : Guid, 
            cellBounds       : Cell2d,
            sampleMapping    : Box2l,
            sampleSizePotExp : PowerOfTwoExp,
            heights1d        : TileData<float> option,
            heightStdDevs1f  : TileData<float32> option,
            colors4b         : TileData<C4b> option,
            intensities1i    : TileData<int> option 
        ) : NodeData =
        
        let tryAddLayer (def : Durable.Def) (value : TileData<'a> option) (data : Map<Guid, obj>) =
            match value with
            | Some tile ->
                /// create a tile aligned with a Cell2d with sample size = 2^sampleExp ...
                let bb = tile |> TileData.bounds
                let maxRes = potexp2pot (cellBounds.Exponent * 1<potexp> - sampleSizePotExp)
                if bb.Size.AnyGreater(int64 maxRes) then invalidArg "tile" "Tile size too large."
                let foo = V2l(cellBounds.X * int64 maxRes, cellBounds.Y * int64 maxRes)
                if bb.Min.AnySmaller(foo) then invalidArg "tile" "Tile not aligned"
                if bb.Max.AnyGreater(foo + int64 maxRes) then invalidArg "tile" "Tile not aligned"
                data |> Map.add def.Id (tile |> TileData.materialize |> TileData.data :> obj)
            | None -> data
            
        Map.empty
        |> add Quadtree.NodeId                   id
        |> add Quadtree.CellBounds               cellBounds
        |> add Quadtree.SampleMapping         sampleMapping
        |> add Quadtree.SampleSizePotExp      sampleSizePotExp
        |> tryAddLayer Quadtree.Heights1d        heights1d
        |> tryAddLayer Quadtree.HeightStdDevs1f  heightStdDevs1f
        |> tryAddLayer Quadtree.Colors4b         colors4b
        |> tryAddLayer Quadtree.Intensities1i    intensities1i

    

    /// Quadtree raster tile.
    type RasterNode2d(data : NodeData, getData : Guid -> obj) =

        let check (def : Def) = if not (data |> contains def) then invalidArg "data" (sprintf "Data does not contain %s." def.Name)
        let checkMany (defs : Def list) = defs |> List.iter check
        
        let loadNode (id : Guid) : RasterNode2d option = 
            if id = Guid.Empty then None
            else match getData id with
                 | :? NodeData as n -> RasterNode2d(n, getData) |> Some
                 | _ -> None
      
        do
            checkMany [Quadtree.NodeId; Quadtree.CellBounds; Quadtree.SampleSizePotExp]

            let e : PowerOfTwoExp = data |> get Quadtree.SampleSizePotExp
            let l = sqr (potexp2pot e)

            let checkArray (def : Def) =
                let xs : Array option = data |> tryGet def
                if xs.IsSome && xs.Value.Length <> int l then invalidArg "data" (sprintf "%s[] must have length %d, but has length %d." def.Name l xs.Value.Length)

            checkArray Quadtree.Heights1d
            checkArray Quadtree.HeightStdDevs1f
            checkArray Quadtree.Colors4b
            checkArray Quadtree.Intensities1i

            ()

    with

        member ____.Id                      with get() : Guid               = data |> get Quadtree.NodeId
        member ____.CellBounds              with get() : Cell2d             = data |> get Quadtree.CellBounds
        member ____.SampleMapping           with get() : Box2l              = data |> get Quadtree.SampleMapping
        member ____.SampleSizePotExp        with get() : PowerOfTwoExp      = data |> get Quadtree.SampleSizePotExp
        member this.Mapping                 with get() : Box2c              = Box2c(this.SampleMapping, this.SampleSizePotExp)
        member ____.SubnodeIds              with get() : Guid[] option      = data |> tryGet Quadtree.SubnodeIds
        
        member ____.TryGetLayerData<'a> (def : Def)    : 'a[] option        = data |> tryGet def
        
        member ____.Heights1d               with get() : float[] option     = data |> tryGet Quadtree.Heights1d
        member ____.HeightStdDevs1f         with get() : float32[] option   = data |> tryGet Quadtree.HeightStdDevs1f
        member ____.Colors4b                with get() : C4b[] option       = data |> tryGet Quadtree.Colors4b
        member ____.Intensities1i           with get() : int[] option       = data |> tryGet Quadtree.Intensities1i

        member this.Layers                  with get() = NodeDataLayers(this.Mapping, data)
        member this.Resolution              with get() = potexp2pot this.SampleSizePotExp
        member this.IsLeafNode              with get() = this.SubnodeIds.IsNone
        member this.IsInnerNode             with get() = this.SubnodeIds.IsSome

        member this.Subnodes with get() : RasterNode2d option[] option  = 
            match this.SubnodeIds with
            | None -> None
            | Some xs -> xs |> Array.map loadNode |> Some

        member this.Split () : RasterNode2d =
            if this.SubnodeIds.IsSome then failwith "Cannot split inner node. Invariant 85500a67-2df6-4549-8632-384f89bed051."

            failwith ""

        override this.ToString() = sprintf "RasterNode2d(%A, %A, %d x %d)" this.Id this.CellBounds this.Resolution this.Resolution


    let buildQuadtree (layers : NodeDataLayers) =

        printfn "mapping bb: %A" layers.Mapping.BoundingBox
        let rootCell = Cell2d(layers.Mapping.BoundingBox)
        printfn "root cell : %A" rootCell

        ()