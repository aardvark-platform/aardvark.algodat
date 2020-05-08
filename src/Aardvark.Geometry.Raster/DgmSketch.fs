namespace Aardvark.Geometry

open Aardvark.Base
open Aardvark.Data
open Raster
open System
open Uncodium.SimpleStore

module ApiSketch =

    type DtmChunk = {

        /// Durable.Def id -> 'a[]
        Layers : Map<Guid, obj>

        /// layer.Length = Mapping.SizeX * Mapping.SizeY
        SampleMapping : Box2l

        /// Area occupied by one sample value as power-of-two-exponent
        SampleSizePotExp : int<potexp>
    }

    type DtmImportConfig = {
        HeightMergeThreshold : float option
        Verbose : bool
    }

    type IRasterNode2d = 
        abstract member Id : Guid 
        abstract member CellBounds : Cell2d
        abstract member SampleMapping : Box2l 
        abstract member SampleSizePotExp : int<potexp>
        abstract member SubnodeIds : Guid[] option
        abstract member Data : Map<Guid, obj>
        abstract member TryGetLayerData<'a> : Durable.Def -> 'a[] option

    let import (store : ISimpleStore) (config : DtmImportConfig) (data : DtmChunk seq) : IRasterNode2d =
        failwith "not implemented"

   

    type PositionSampler =
        | Center
        | BottomLeft
        | BottomRight
        | TopRight
        | TopLeft
        | CustomRelativePosition of V2d
    with
        /// Value (0,0) corresponds to bottom left, and (1,1) to top right corner of sample.
        member this.RelativePosition with get () =
            match this with
            | Center                   -> V2d(0.5, 0.5)
            | BottomLeft               -> V2d(0.0, 0.0)
            | BottomRight              -> V2d(1.0, 0.0)
            | TopRight                 -> V2d(1.0, 1.0)
            | TopLeft                  -> V2d(0.0, 1.0)
            | CustomRelativePosition p -> p



    type QueryConfig = {
        Verbose : bool
        PositionSampler : PositionSampler
    }


    type NodeSelection =
        | FullySelected
        | NotSelected
        | PartiallySelectedByIndex of int[]



    type QueryResult = {
        Node : IRasterNode2d
        Selection : NodeSelection
    }


    let queryNearLine2d (filter : Line2d) (config : QueryConfig) (withinDistance : float) (root : IRasterNode2d) : (IRasterNode2d * int[]) seq =
        failwith "not implemented"

    let queryInsidePolygon2d (filter : Polygon2d) (config : QueryConfig) (root : IRasterNode2d) : QueryResult seq =
        failwith "not implemented"

    let queryNode (filter : Cell2d) (config : QueryConfig) (root : IRasterNode2d) : QueryResult =
        failwith "not implemented"



    type HeightDeltas = {
        Deltas : float[]
        SampleMapping : Box2l
        SampleSizePotExp : int<potexp>
    }
    with
        member this.SampleArea with get() : float<length^2> = 
            let x = float (potexp2pot (this.SampleSizePotExp)) * 1.0<length>
            x * x

    /// Computes difference volume (a-b) using the given integrator which computes difference volume of two tiles.
    let computeDifferenceVolume (integrator : HeightDeltas -> float<length^3>) (a : IRasterNode2d) (b : IRasterNode2d) : float<length^3> =
        failwith "not implemented"

    ()

