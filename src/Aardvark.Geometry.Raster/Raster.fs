namespace Aardvark.Geometry

open Aardvark.Base
open Aardvark.Data
open System
open System.Collections.Generic

[<AutoOpen>]
module PowersOfTwo =

    /// power-of-two exponent
    [<Measure>] type potexp

    /// power-of-two
    [<Measure>] type pot

    let potexp2pot (e : int<potexp>) =
        if e < 0<potexp> then invalidArg "e" "Exponent must not be negative."
        (1L <<< int e) * 1L<pot>

[<AutoOpen>]
module RasterExtensions =

    type Box2l with
        member this.IsSizePowerOfTwoSquare with get() = this.SizeX = this.SizeY && this.SizeX.IsPowerOfTwo()
        member this.SplitAtCenter () =
            let c = this.Center
            [|
                Box2l(this.Min.X, this.Min.Y,        c.X,        c.Y)
                Box2l(       c.X, this.Min.Y, this.Max.X,        c.Y)
                Box2l(this.Min.X,        c.Y,        c.X, this.Max.Y)
                Box2l(       c.X,        c.Y, this.Max.X, this.Max.Y)
            |]

    type Cell2d with
        static member Create(xy : V2l, e : int<potexp>) = Cell2d(xy, int e)

type ITileData =
    //abstract member Materialize : unit -> ITileData
    abstract member Data : Array with get

/// A two-dim raster of values.
type TileData<'a> =
    /// (data, mapping), where mapping places the samples globally.
    | ArrayTile of 'a[] * Box2l
    /// (data, mapping, window), where window is absolute (not relative to mapping).
    | WindowedTile of 'a[] * Box2l * Box2l
    /// (tile, cell, e), where e specifies the sample size as 2^e.
    /// E.g. for cell.Exponent=8 and e=2 the cell contains a raster of 64x64 samples (which may only be partially covered, as specified by raster bounds).
    /// Coords (x,y) of 'cell.Bounds' now live in space Cell2d(x, y, cell.Exponent - e), and must be contained in 'cell'.
    | CellTile of TileData<'a> * Cell2d * int<potexp>
with
    override this.ToString() =
        match this with
        | ArrayTile (data, mapping) -> sprintf "ArrayTile(%d, %A)" data.Length mapping
        | WindowedTile (data, mapping, window) -> sprintf "WindowedTile(%d, %A, %A)" data.Length mapping window
        | CellTile (tile, cell, e) -> sprintf "CellTile(%A, %A, %d)" (tile.ToString()) cell e

    interface ITileData with
        //member this.Materialize() = TileData.materialize this
        member this.Data with get() = match this with | ArrayTile (data, _) -> data :> Array | WindowedTile (data, _, _) -> data :> Array | CellTile (t, _, _) -> (t :> ITileData).Data

module TileData =

    /// Create tile data from array.
    let ofArray mapping data = ArrayTile (data, mapping)

    /// Create tile data from array.
    let OfArray(mapping, data) = ofArray mapping data

    /// Creates windowed tile data.
    let rec withWindow (window : Box2l) tile =
        match tile with

        | ArrayTile (data, mapping) ->
            if not (mapping.Contains(window)) then sprintf "Window must be contained in data mapping %A, but is %A." mapping window |> invalidArg "window"
            WindowedTile (data, mapping, window)

        | WindowedTile (data, mapping, oldWindow) ->
            if not (mapping.Contains(window)) then sprintf "Window must be contained in data mapping %A, but is %A." mapping window |> invalidArg "window"
            if window = mapping || window = oldWindow then tile
            else WindowedTile (data, mapping, window)

        | CellTile (tile, cell, e) ->
            CellTile (tile |> withWindow window, cell, e)

    /// Ensure that tile is not windowed, i.e. copy out windowed data.
    let rec materialize tile =
        match tile with

        | ArrayTile _ -> tile

        | WindowedTile (data, mapping, window) ->
            let xs : 'a[] = Array.zeroCreate (int window.Area)
            let widthMapping = int (mapping.Max.X - mapping.Min.X)
            let widthWindow = int (window.Max.X - window.Min.X)
            let heightWindow = int (window.Max.Y - window.Min.Y)
            let originWindowLocal = V2i(window.Min - mapping.Min)
            let mutable i = 0
            let mutable j0 = originWindowLocal.Y * widthMapping + originWindowLocal.X
            for _ = 1 to heightWindow do
                let jmax = j0 + widthWindow - 1
                let mutable j = j0
                for j = j0 to jmax do
                    xs.[i] <- data.[j]
                    i <- i + 1
                j0 <- j0 + widthMapping
            ArrayTile (xs, window)

        | CellTile (tile, cell, e) ->
            CellTile (tile |> materialize, cell, e)

    /// Get bounds of tile.
    let rec bounds tile = match tile with | ArrayTile (_, m) -> m | WindowedTile (_, _, w) -> w | CellTile (t, _, _) -> bounds t

    /// Get size (columns x rows) of tile. 
    let rec size tile = match tile with | ArrayTile (_, m) -> m.Size | WindowedTile (_, _, w) -> w.Size | CellTile (t, _, _) -> size t

    ///// Get raw data of tile.
    //let rec data tile = match tile with | ArrayTile (data, _) -> data | WindowedTile (data, _, _) -> data | CellTile (t, _, _) -> data t

    /// Create a tile aligned with a Cell2d with sample size = 2^sampleExp.
    let withCell (cell : Cell2d) (sampleExp : int<potexp>) tile  =
        let bb = tile |> bounds
        let maxRes = potexp2pot (cell.Exponent * 1<potexp> - sampleExp)
        if bb.Size.AnyGreater(int64 maxRes) then invalidArg "tile" "Tile size too large."
        let foo = V2l(cell.X * int64 maxRes, cell.Y * int64 maxRes)
        if bb.Min.AnySmaller(foo) then invalidArg "tile" "Tile not aligned"
        if bb.Max.AnyGreater(foo + int64 maxRes) then invalidArg "tile" "Tile not aligned"
        CellTile (tile, cell, sampleExp)

    /// Split tile into quadrants (at center).
    let rec splitIntoQuadrants tile =
        match tile with
        | ArrayTile (data, mapping) -> 
            mapping.SplitAtCenter() |> Array.map (fun box -> WindowedTile (data, mapping, box))
        | WindowedTile (data, mapping, window) -> 
            window.SplitAtCenter()  |> Array.map (fun box -> WindowedTile (data, mapping, box))
        | CellTile (tile, cell, e) -> 
            tile |> splitIntoQuadrants |> Array.mapi (fun i t -> CellTile (t, cell.GetQuadrant(i), e))

    // (tilecoords, tilewindow) seq
    let splitIntoTiles (tilesize : V2l) tile =
        let bb = bounds tile
        let f x s = if x < 0L then (x+1L) / s - 1L else x / s
        let minIncl = V2l(f bb.Min.X tilesize.X, f bb.Min.Y tilesize.Y)
        let maxIncl = V2l(f bb.Max.X tilesize.X, f bb.Max.Y tilesize.Y) // bb.Max is exclusive, therefore result is inclusive
        let mutable y = minIncl.Y
        seq {
            while y <= maxIncl.Y do
                let mutable x = minIncl.X
                while x <= maxIncl.X do
                    let t = V2l(x,y)
                    let tbb = Box2l(t * tilesize, (t + 1L) * tilesize)
                    //printfn "[%d, %d] -> %A" x y tbb
                    let intsctn = bb.Intersection(tbb) 
                    //printfn "            %A    %d" intsctn intsctn.Area
                    yield (t, tile |> withWindow intsctn)

                    x <- x + 1L
                y <- y + 1L
        }

    /// Splits a 'source' tile into cells with given size ('targetCellExponent'), where
    /// each sample of the source tile is defined to have size 'sourceSampleExponent'.
    /// E.g. for sourceSampleExponent = -2 (size 0.25 x 0.25) and targetCellExponent = 7 (size 128.0 x 128.0)
    /// the function will return a sequence of cell tiles with 512x512 samples, aligned at multiples of 512 with respect to the source raster.
    /// Border cells may have fewer samples.
    let splitIntoCells (targetCellExponent : int<potexp>) (sourceSampleExponent : int<potexp>) (source : TileData<'a>) =
        let targetResolution = potexp2pot (targetCellExponent - sourceSampleExponent)
        source
        |> splitIntoTiles (V2l(int64 targetResolution)) 
        |> Seq.map (fun (xy, t) -> t |> withCell (Cell2d.Create(xy, targetCellExponent)) sourceSampleExponent) 

    ()

type TileData<'a> with
    member this.WithWindow (window : Box2l) = TileData.withWindow window this
    member this.WithWindow (window : Box2i) = TileData.withWindow (Box2l window) this
    member this.Materialize () = TileData.materialize this
    member this.Bounds with get() = TileData.bounds this
    member this.Size with get() = TileData.size this
    member this.WithCell cell sampleExp = TileData.withCell cell sampleExp this
    member this.SplitIntoQuadrants () = TileData.splitIntoQuadrants this
    member this.SplitIntoTiles (tileSize : V2l) = TileData.splitIntoTiles tileSize this
    member this.SplitIntoCells (targetCellExponent : int<potexp>) (sourceSampleExponent : int<potexp>) = TileData.splitIntoCells targetCellExponent sourceSampleExponent this

    

module Raster =

    type Map<'k,'v when 'k : comparison> with
        static member ofDictionary(dict : IReadOnlyDictionary<'k,'v>) = dict |> Seq.map (fun e -> (e.Key, e.Value)) |> Map.ofSeq

    type private Def = Durable.Def

    module Defs =
        let private def id name description (typ : Def) = Def(Guid.Parse(id), name, description, typ.Id, false)

        module Primitives =

            let Cell2d                          = def "9d580e5d-a559-4c5e-9413-7675f1dfe93c" "Cell2d" "A 2^Exponent sized cube positioned at (X,Y,Z) * 2^Exponent." Durable.Primitives.Unit

            let Float32ArrayWithFloat64Offset   = def "aab1d3e2-80f9-4f12-895c-81cd5fc5d096" "Float32ArrayWithFloat64Offset" "Float64[] data stored as Int32[] plus Float64 offset." Durable.Primitives.Unit

            let Int32ArrayWithInt64Offset       = def "8061bb56-3076-4afd-865d-c9a7701d225a" "Int32ArrayWithInt64Offset" "Int64[] data stored as Int32[] plus Int64 offset." Durable.Primitives.Unit
            let Int16ArrayWithInt64Offset       = def "f200bcb7-e462-4d42-88e8-d8bfcb10c265" "Int16ArrayWithInt64Offset" "Int64[] data stored as Int16[] plus Int64 offset." Durable.Primitives.Unit
            let Int8ArrayWithInt64Offset        = def "46cc0b8e-c4e4-4626-940f-d2adc28c0c00" "Int8ArrayWithInt64Offset" "Int64[] data stored as Int8[] plus Int64 offset." Durable.Primitives.Unit
            let Int16ArrayWithInt32Offset       = def "8daf811d-3d1c-4219-8f2e-22c6c49de6cd" "Int16ArrayWithInt32Offset" "Int32[] data stored as Int16[] plus Int32 offset." Durable.Primitives.Unit
            let Int8ArrayWithInt32Offset        = def "fd4db85a-5b2c-4390-aeb3-9f2162034ebb" "Int8ArrayWithInt32Offset" "Int32[] data stored as Int8[] plus Int32 offset." Durable.Primitives.Unit
            let Int8ArrayWithInt16Offset        = def "2a9f5350-02d3-45e7-84db-5ec55d105787" "Int8ArrayWithInt16Offset" "Int16[] data stored as Int8[] plus Int16 offset." Durable.Primitives.Unit

            let UInt32ArrayWithUInt64Offset     = def "3f3d719a-5a3b-4a97-b9f6-a821c063374f" "UInt32ArrayWithUInt64Offset" "UInt64[] data stored as UInt32[] plus UInt64 offset." Durable.Primitives.Unit
            let UInt16ArrayWithUInt64Offset     = def "f4387c8f-92de-4af7-96fe-5b1e0e3ff935" "UInt16ArrayWithUInt64Offset" "UInt64[] data stored as UInt16[] plus UInt64 offset." Durable.Primitives.Unit
            let UInt8ArrayWithUInt64Offset      = def "166f9886-61a9-4d81-b072-b86bab4e3ba3" "UInt8ArrayWithUInt64Offset" "UInt64[] data stored as UInt8[] plus UInt64 offset." Durable.Primitives.Unit
            let UInt16ArrayWithUInt32Offset     = def "2477f185-8c5b-4f5c-b3d0-21e63f361304" "UInt16ArrayWithUInt32Offset" "UInt32[] data stored as UInt16[] plus UInt32 offset." Durable.Primitives.Unit
            let UInt8ArrayWithUInt32Offset      = def "92355e69-b783-45cd-bf6b-cd5fb978ea33" "UInt8ArrayWithUInt32Offset" "UInt32[] data stored as UInt8[] plus UInt32 offset." Durable.Primitives.Unit
            let UInt8ArrayWithUInt16Offset      = def "625a813d-f2bd-4034-a69c-d967fef3da50" "UInt8ArrayWithUInt16Offset" "UInt16[] data stored as UInt8[] plus UInt16 offset." Durable.Primitives.Unit

        module Quadtree =
            let Node                    = def "e497f9c1-c903-41c4-91de-32bf76e009da" "Quadtree.Node" "A quadtree node. DurableMapAligned16." Durable.Primitives.DurableMapAligned16
            let NodeId                  = def "e46c4163-dd28-43a4-8254-bc21dc3f766b" "Quadtree.NodeId" "Quadtree. Unique id of a node. Guid." Durable.Primitives.GuidDef
            let CellBounds              = def "59258849-5765-4d11-b760-538282063a55" "Quadtree.CellBounds" "Quadtree. Node bounds in cell space. Cell2d." Primitives.Cell2d
            let SampleMapping           = def "2f363f2a-2e52-4a86-a620-d8689db511ad" "Quadtree.SampleMapping" "Quadtree. Mapping of sample values to cell space. Box2l." Durable.Aardvark.Box2l
            let SampleSizePotExp        = def "1aa56aca-de4c-4705-9baf-11f8766a0892" "Quadtree.SampleSizePotExp" "Quadtree. Size of a sample is 2^SampleSizePotExp. Int32." Durable.Primitives.Int32
            let SubnodeIds              = def "a2841629-e4e2-4b90-bdd1-7a1a5a41bded" "Quadtree.SubnodeIds" "Quadtree. Subnodes as array of guids. Array length is 4 for inner nodes (where Guid.Empty means no subnode) and no array for leaf nodes. Guid[]." Durable.Primitives.GuidArray
            
            let Heights1f               = def "4cb689c5-b627-4bcd-9db7-5dbd24d7545a" "Quadtree.Heights1f" "Quadtree. Height values. Float32[]." Durable.Primitives.Float32Array
            let Heights1fRef            = def "fcf042b4-fe33-4e28-9aea-f5526600f8a4" "Quadtree.Heights1f.Reference" "Quadtree. Reference to Quadtree.Heights1f. Guid." Durable.Primitives.GuidDef
            
            let Heights1d               = def "c66a4240-00ef-44f9-b377-0667f279b97e" "Quadtree.Heights1d" "Quadtree. Height values. Float64[]." Durable.Primitives.Float64Array
            let Heights1dRef            = def "baa8ed40-57e3-4f88-8d11-0b547494c8cb" "Quadtree.Heights1d.Reference" "Quadtree. Reference to Quadtree.Heights1d. Guid." Durable.Primitives.GuidDef

            let Heights1dWithOffset     = def "924ae8a2-7b9b-4e4d-a609-7b0381858499" "Quadtree.Heights1dWithOffset" "Quadtree. Height values. Float64 offset + Float32[] values." Primitives.Float32ArrayWithFloat64Offset
            let Heights1dWithOffsetRef  = def "2815c5a7-48bf-48b6-ba7d-5f4e98f6bc47" "Quadtree.Heights1dWithOffset.Reference" "Quadtree. Reference to Quadtree.Heights1dWithOffset. Guid." Durable.Primitives.GuidDef

            let HeightStdDevs1f         = def "74bfe324-98ad-4f57-8163-120361e1e68e" "Quadtree.HeightStdDevs1f" "Quadtree. Standard deviation per height value. Float32[]." Durable.Primitives.Float32Array
            let HeightStdDevs1fRef      = def "f93e8c5f-7e9e-4e1f-b57a-4475ebf023af" "Quadtree.HeightStdDevs1f.Reference" "Quadtree. Reference to Quadtree.HeightStdDevs1f. Guid." Durable.Primitives.GuidDef

            let Colors4b                = def "97b8282c-964a-40e8-a7be-d55ee587b5d4" "Quadtree.Colors4b" "Quadtree. Color per height value. C4b[]." Durable.Aardvark.C4bArray
            let Colors4bRef             = def "8fe18316-7fa3-4704-869d-c3995d19d03e" "Quadtree.Colors4b.Reference" "Quadtree. Reference to Quadtree.Colors4b. Guid." Durable.Primitives.GuidDef

            let Intensities1i           = def "da564b5d-c5a4-4274-806a-acd04fa206b2" "Quadtree.Intensities1i" "Quadtree. Intensity per height value. Int32[]." Durable.Primitives.Int32Array
            let Intensities1iRef        = def "b44484ba-e9a6-4e0a-a26a-3641a91ee9cf" "Quadtree.Intensities1i.Reference" "Quadtree. Reference to Quadtree.Intensities1i. Guid." Durable.Primitives.GuidDef

    open Defs

    let inline private sqr x = x * x

    /// Create data map for RasterNode2d.
    let CreateData(id : Guid, 
                   cellBounds : Cell2d,
                   sampleMapping : Box2l option,
                   sampleSizePotExp : int<potexp> option,
                   globalHeights1d  : TileData<float> option,
                   heightStdDevs1f  : TileData<float32> option,
                   colors4b         : TileData<C4b> option,
                   intensities1i    : TileData<int> option 
                   ) =
        let add (def : Durable.Def) (value : 'a) (data : Map<Guid, obj>) = data |> Map.add def.Id (value :> obj)
        let tryAdd (def : Durable.Def) (value : 'a option) (data : Map<Guid, obj>) = if value.IsSome then data |> Map.add def.Id (value.Value :> obj) else data
        let tryAddTile (def : Durable.Def) (value : TileData<'a> option) (data : Map<Guid, obj>) = 
            if value.IsSome then
                match value.Value with
                | CellTile (tile, _, _) -> data |> Map.add def.Id ((tile.Materialize() :> ITileData).Data :> obj)
                | _ -> failwith "Must be CellTile."
            else 
                data
        Map.empty
        |> add Quadtree.NodeId                  id
        |> add Quadtree.CellBounds              cellBounds
        |> tryAdd Quadtree.SampleMapping        sampleMapping
        |> tryAdd Quadtree.SampleSizePotExp     sampleSizePotExp
        |> tryAddTile Quadtree.Heights1d        globalHeights1d
        |> tryAddTile Quadtree.HeightStdDevs1f  heightStdDevs1f
        |> tryAddTile Quadtree.Colors4b         colors4b
        |> tryAddTile Quadtree.Intensities1i    intensities1i

    /// Quadtree raster tile.
    type RasterNode2d(data : IReadOnlyDictionary<Guid, obj>, getData : Func<Guid, IReadOnlyDictionary<Guid, obj>>) =

        let contains (def : Def) = data.ContainsKey(def.Id)
        let check' (def : Def) = if not (contains def) then invalidArg "data" (sprintf "Data does not contain %s." def.Name)
        let check (defs : Def list) = defs |> List.iter check'
        let get (def : Def) = data.[def.Id] :?> 'a
        let tryGet (def : Def) = match data.TryGetValue(def.Id) with | false, _ -> None | true, x -> Some (x :?> 'a)
        let loadNode (id : Guid) : RasterNode2d option = if id = Guid.Empty then None else RasterNode2d(getData.Invoke id, getData) |> Some
   
        do
            check [Quadtree.NodeId; Quadtree.CellBounds; Quadtree.SampleSizePotExp]

            let e : int<potexp> = get Quadtree.SampleSizePotExp
            let l = sqr (potexp2pot e)

            let checkArray (def : Def) =
                let xs : Array option = tryGet def
                if xs.IsSome && xs.Value.Length <> int l then invalidArg "data" (sprintf "%s[] must have length %d, but has length %d." def.Name l xs.Value.Length)

            checkArray Quadtree.Heights1d
            checkArray Quadtree.HeightStdDevs1f
            checkArray Quadtree.Colors4b
            checkArray Quadtree.Intensities1i

            ()

    with

        member ____.Id                      with get() : Guid               = Quadtree.NodeId               |> get
        member ____.CellBounds              with get() : Cell2d             = Quadtree.CellBounds           |> get
        member ____.SampleSizePotExp        with get() : int<potexp>        = Quadtree.SampleSizePotExp     |> get
        member ____.SubnodeIds              with get() : Guid[] option      = Quadtree.SubnodeIds           |> tryGet
        member ____.Heights1d               with get() : float[]            = Quadtree.Heights1d            |> get
        member ____.HeightStdDevs1f         with get() : float32[] option   = Quadtree.HeightStdDevs1f      |> tryGet
        member ____.Colors4b                with get() : C4b[] option       = Quadtree.Colors4b             |> tryGet
        member ____.Intensities1i           with get() : int[] option       = Quadtree.Intensities1i        |> tryGet

        member this.Resolution              with get() = potexp2pot this.SampleSizePotExp
        member this.IsLeafNode              with get() = this.SubnodeIds.IsNone
        member this.IsInnerNode             with get() = this.SubnodeIds.IsSome

        member this.Subnodes with get() : RasterNode2d option[] option  = 
            match this.SubnodeIds with
            | None -> None
            | Some xs -> xs |> Array.map loadNode |> Some

        override this.ToString() = sprintf "RasterNode2d(%A, %A, %d x %d)" this.Id this.CellBounds this.Resolution this.Resolution
