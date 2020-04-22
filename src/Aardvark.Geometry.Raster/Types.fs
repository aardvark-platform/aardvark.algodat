namespace Aardvark.Geometry

open Aardvark.Base
open Aardvark.Data
open System
open System.Collections.Generic

module Raster =

    module Defs =
        let private def id name description (typ : Durable.Def) = Durable.Def(Guid.Parse(id), name, description, typ.Id, false)

        let Cell2d               = def "9d580e5d-a559-4c5e-9413-7675f1dfe93c" "Durable.Aardvark.Cell2d" "A 2^Exponent sized cube positioned at (X,Y,Z) * 2^Exponent." Durable.Primitives.Unit
        let NodeId               = def "e46c4163-dd28-43a4-8254-bc21dc3f766b" "RasterNode2d.Id" "Unique id of a RasterNode2d. Guid." Durable.Primitives.GuidDef
        let Bounds               = def "59258849-5765-4d11-b760-538282063a55" "RasterNode2d.Bounds" "Node bounds. Cell2d." Cell2d
        let ResolutionPowerOfTwo = def "1aa56aca-de4c-4705-9baf-11f8766a0892" "RasterNode2d.ResolutionPowerOfTwo" "There are 2^res x 2^res values. Int32." Durable.Primitives.Int32
        let SubnodeIds           = def "a2841629-e4e2-4b90-bdd1-7a1a5a41bded" "RasterNode2d.SubnodeIds" "Subnodes as array of guids. Array length is 4 for inner nodes (where Guid.Empty means no subnode) and no array for leaf nodes. Guid[]." Durable.Primitives.GuidArray
        let GlobalHeightOffset   = def "98cb3948-80ba-4148-86d6-d8bafbdda571" "RasterNode2d.GlobalHeightOffset" "Global offset for local heights. Float64." Durable.Primitives.Float64
        let LocalHeights         = def "81fbaa7a-0b11-40a7-b62e-63d81781eb20" "RasterNode2d.LocalHeights" "Local height values. Float32[]." Durable.Primitives.Float32Array
        let HeightStdDevs        = def "74bfe324-98ad-4f57-8163-120361e1e68e" "RasterNode2d.HeightStdDevs" "Standard deviation for each height value. Float32[]." Durable.Primitives.Float32Array
        let Colors4b             = def "97b8282c-964a-40e8-a7be-d55ee587b5d4" "RasterNode2d.Colors4b" "Color for each height value. C4b[]." Durable.Aardvark.C4bArray
        let Intensities1i        = def "da564b5d-c5a4-4274-806a-acd04fa206b2" "RasterNode2d.Intensities1i" "Intensity for each height value. Int32[]." Durable.Primitives.Int32Array

    open Defs

    let inline private sqr x = x * x
   
    type RasterNode2d(data : IReadOnlyDictionary<Guid, obj>, getData : Func<Guid, IReadOnlyDictionary<Guid, obj>>) =

        let contains (def : Durable.Def) = data.ContainsKey(def.Id)
        let check' (def : Durable.Def) = if not (contains def) then invalidArg "data" (sprintf "Data does not contain %s." def.Name)
        let check (defs : Durable.Def list) = defs |> List.iter check'
        let get (def : Durable.Def) = data.[def.Id] :?> 'a
        let tryGet (def : Durable.Def) = match data.TryGetValue(def.Id) with | false, _ -> None | true, x -> Some (x :?> 'a)
        let loadNode (id : Guid) : RasterNode2d option = if id = Guid.Empty then None else RasterNode2d(getData.Invoke id, getData) |> Some
   
        do
            check [NodeId; Bounds; ResolutionPowerOfTwo; GlobalHeightOffset; LocalHeights]

            let e : int = get ResolutionPowerOfTwo
            let l = sqr (1 <<< e)
            let hs : float32[] = get LocalHeights
            if hs.Length <> l then invalidArg "data" (sprintf "LocalHeights[] must have length %d, but has length %d." l hs.Length)

            ()

    with

        member ____.Id                      with get() : Guid               = NodeId               |> get
        member ____.Bounds                  with get() : Cell2d             = Bounds               |> get
        member ____.ResolutionPowerOfTwo    with get() : int                = ResolutionPowerOfTwo |> get
        member ____.SubnodeIds              with get() : Guid[] option      = SubnodeIds           |> tryGet
        member ____.GlobalHeightOffset      with get() : float              = GlobalHeightOffset   |> get
        member ____.LocalHeights            with get() : float32[]          = LocalHeights         |> get
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

        member this.GlobalHeights with get() = this.LocalHeights |> Array.map (fun z -> this.GlobalHeightOffset + float z)

