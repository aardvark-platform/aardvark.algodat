namespace Aardvark.Geometry

open Aardvark.Base
open Aardvark.Data
open System
open System.Collections.Generic

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

type TileData<'a> =
    | ArrayData of 'a[] * Box2l
    | WindowedArrayData of 'a[] * Box2l * Box2l
    | CellAlignedTile of TileData<'a> * Cell2d

module TileData =

    /// Create tile data from array.
    let OfArray mapping data = ArrayData (data, mapping)

    /// Creates windowed tile data.
    let rec Window (window : Box2l) tile =
        match tile with

        | ArrayData (data, mapping) ->
            if not (mapping.Contains(window)) then sprintf "Window must be contained in data mapping %A, but is %A." mapping window |> invalidArg "window"
            WindowedArrayData (data, mapping, window)

        | WindowedArrayData (data, mapping, oldWindow) ->
            if not (mapping.Contains(window)) then sprintf "Window must be contained in data mapping %A, but is %A." mapping window |> invalidArg "window"
            if window = mapping || window = oldWindow then tile
            else WindowedArrayData (data, mapping, window)

        | CellAlignedTile (tile, cell) ->
            CellAlignedTile (tile |> Window window, cell)

    let rec Materialize tile =
        match tile with

        | ArrayData _ -> tile

        | WindowedArrayData (data, mapping, window) ->
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
            ArrayData (xs, window)

        | CellAlignedTile (tile, cell) ->
            CellAlignedTile (tile |> Materialize, cell)

    let rec Bounds tile = match tile with | ArrayData (_, m) -> m | WindowedArrayData (_, _, w) -> w | CellAlignedTile (t, _) -> Bounds t

    let rec Size tile = match tile with | ArrayData (_, m) -> m.Size | WindowedArrayData (_, _, w) -> w.Size | CellAlignedTile (t, _) -> Size t

    let rec IsPowerOfTwoSquare tile = (tile |> Bounds).IsSizePowerOfTwoSquare

    let CellAlign cell tile =
        if IsPowerOfTwoSquare tile then CellAlignedTile (tile, cell)
        else invalidArg "tile" "Tile is not power-of-two square."

    let rec Split tile =
        match tile with
        | ArrayData (data, mapping) -> 
            mapping.SplitAtCenter() |> Array.map (fun box -> WindowedArrayData (data, mapping, box))
        | WindowedArrayData (data, mapping, window) -> 
            window.SplitAtCenter()  |> Array.map (fun box -> WindowedArrayData (data, mapping, box))
        | CellAlignedTile (tile, cell) -> 
            tile |> Split  |> Array.map (fun t   -> CellAlignedTile (t, cell))

    ()

type TileData<'a> with
    member this.Window window = TileData.Window window this
    member this.Window (window : Box2i) = TileData.Window (Box2l window) this
    member this.Materialize () = TileData.Materialize this
    member this.Bounds with get() = TileData.Bounds this
    member this.Size with get() = TileData.Size this
    member this.IsPowerOfTwoSquare with get() = TileData.IsPowerOfTwoSquare this
    member this.CellAlign cell = TileData.CellAlign cell this
    member this.Split () = TileData.Split this

module Raster =

    type Map<'k,'v when 'k : comparison> with
        static member ofDictionary(dict : IReadOnlyDictionary<'k,'v>) = dict |> Seq.map (fun e -> (e.Key, e.Value)) |> Map.ofSeq

    type private Def = Durable.Def

    module Defs =
        let private def id name description (typ : Def) = Def(Guid.Parse(id), name, description, typ.Id, false)

        let Cell2d               = def "9d580e5d-a559-4c5e-9413-7675f1dfe93c" "Durable.Aardvark.Cell2d" "A 2^Exponent sized cube positioned at (X,Y,Z) * 2^Exponent." Durable.Primitives.Unit
        let NodeId               = def "e46c4163-dd28-43a4-8254-bc21dc3f766b" "RasterNode2d.Id" "Unique id of a RasterNode2d. Guid." Durable.Primitives.GuidDef
        let Bounds               = def "59258849-5765-4d11-b760-538282063a55" "RasterNode2d.Bounds" "Node bounds. Cell2d." Cell2d
        let ResolutionPowerOfTwo = def "1aa56aca-de4c-4705-9baf-11f8766a0892" "RasterNode2d.ResolutionPowerOfTwo" "There are 2^res x 2^res values. Int32." Durable.Primitives.Int32
        let SubnodeIds           = def "a2841629-e4e2-4b90-bdd1-7a1a5a41bded" "RasterNode2d.SubnodeIds" "Subnodes as array of guids. Array length is 4 for inner nodes (where Guid.Empty means no subnode) and no array for leaf nodes. Guid[]." Durable.Primitives.GuidArray
        let GlobalHeights        = def "924ae8a2-7b9b-4e4d-a609-7b0381858499" "RasterNode2d.GlobalHeights" "Global height values. Float64[]." Durable.Primitives.Float64Array
        let HeightStdDevs        = def "74bfe324-98ad-4f57-8163-120361e1e68e" "RasterNode2d.HeightStdDevs" "Standard deviation for each height value. Float32[]." Durable.Primitives.Float32Array
        let Colors4b             = def "97b8282c-964a-40e8-a7be-d55ee587b5d4" "RasterNode2d.Colors4b" "Color for each height value. C4b[]." Durable.Aardvark.C4bArray
        let Intensities1i        = def "da564b5d-c5a4-4274-806a-acd04fa206b2" "RasterNode2d.Intensities1i" "Intensity for each height value. Int32[]." Durable.Primitives.Int32Array

    open Defs

    let inline private sqr x = x * x

    /// Create data map for RasterNode2d.
    let CreateData(id : Guid, bounds : Cell2d, resolutionPowerOfTwo : int,
                   globalHeights : float[]   option,
                   heightStdDevs : float32[] option,
                   colors4b      : C4b[]     option,
                   intensities1i : int[]     option 
                   ) =
        let add (def : Durable.Def) (value : 'a) (data : Map<Guid, obj>) = data |> Map.add def.Id (value :> obj)
        let tryAdd (def : Durable.Def) (value : 'a option) (data : Map<Guid, obj>) = if value.IsSome then data |> Map.add def.Id (value.Value :> obj) else data
        Map.empty
        |> add Defs.NodeId                  id
        |> add Defs.Bounds                  bounds
        |> add Defs.ResolutionPowerOfTwo    resolutionPowerOfTwo
        |> tryAdd Defs.GlobalHeights        globalHeights
        |> tryAdd Defs.HeightStdDevs        heightStdDevs
        |> tryAdd Defs.Colors4b             colors4b
        |> tryAdd Defs.Intensities1i        intensities1i

    /// Quadtree raster tile.
    type RasterNode2d(data : IReadOnlyDictionary<Guid, obj>, getData : Func<Guid, IReadOnlyDictionary<Guid, obj>>) =

        let contains (def : Def) = data.ContainsKey(def.Id)
        let check' (def : Def) = if not (contains def) then invalidArg "data" (sprintf "Data does not contain %s." def.Name)
        let check (defs : Def list) = defs |> List.iter check'
        let get (def : Def) = data.[def.Id] :?> 'a
        let tryGet (def : Def) = match data.TryGetValue(def.Id) with | false, _ -> None | true, x -> Some (x :?> 'a)
        let loadNode (id : Guid) : RasterNode2d option = if id = Guid.Empty then None else RasterNode2d(getData.Invoke id, getData) |> Some
   
        do
            check [NodeId; Bounds; ResolutionPowerOfTwo]

            let e : int = get ResolutionPowerOfTwo
            let l = sqr (1 <<< e)

            let checkArray (def : Def) =
                let xs : Array option = tryGet def
                if xs.IsSome && xs.Value.Length <> l then invalidArg "data" (sprintf "%s[] must have length %d, but has length %d." def.Name l xs.Value.Length)

            checkArray GlobalHeights
            checkArray HeightStdDevs
            checkArray Colors4b
            checkArray Intensities1i

            ()

    with

        member ____.Id                      with get() : Guid               = NodeId               |> get
        member ____.Bounds                  with get() : Cell2d             = Bounds               |> get
        member ____.ResolutionPowerOfTwo    with get() : int                = ResolutionPowerOfTwo |> get
        member ____.SubnodeIds              with get() : Guid[] option      = SubnodeIds           |> tryGet
        member ____.GlobalHeights           with get() : float[]            = GlobalHeights        |> get
        member ____.HeightStdDevs           with get() : float32[] option   = HeightStdDevs        |> tryGet
        member ____.Colors4b                with get() : C4b[] option       = Colors4b             |> tryGet
        member ____.Intensities1i           with get() : int[] option       = Intensities1i        |> tryGet

        member this.Resolution              with get() = 1 <<< this.ResolutionPowerOfTwo
        member this.IsLeafNode              with get() = this.SubnodeIds.IsNone
        member this.IsInnerNode             with get() = this.SubnodeIds.IsSome

        member this.Subnodes with get() : RasterNode2d option[] option  = 
            match this.SubnodeIds with
            | None -> None
            | Some xs -> xs |> Array.map loadNode |> Some

        override this.ToString() = sprintf "RasterNode2d(%A, %A, %d x %d)" this.Id this.Bounds this.Resolution this.Resolution
