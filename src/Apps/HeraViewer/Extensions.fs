namespace Aardvark.Geometry.Points

open System
open Aardvark.Data
open Aardvark.Base
open Aardvark.Geometry.Points
open System.Collections.Immutable
open Aardvark.Data.Points


module GenericList = 
    open System.Collections.Generic

    let inline chooseiv (f : int -> 'a -> voption<'b>) (xs : array<'a>) : List<'b> = 
        let l = List<'b>()
        for i in 0 .. xs.Length - 1 do
            match f i xs.[i] with
            | ValueSome v -> l.Add v
            | _ -> ()
        l


[<AutoOpen>]
module PcExtension = 
    type IPointCloudNode with

        member node.ToGenericChunk (customAttribs : array<Durable.Def>) =
            let mutable data =
                ImmutableDictionary<Durable.Def, System.Object>.Empty
                 .Add(GenericChunk.Defs.Positions3d, node.PositionsAbsolute)

            for def in customAttribs do    
                data <- data.Add(def, node.Properties.[def])

            GenericChunk data

module Queries = 


    let rec QueryPointsCustom (node : IPointCloudNode) (isNodeFullyInside : IPointCloudNode ->bool) 
                          (isNodeFullyOutside : IPointCloudNode -> bool)
                          (isPositionInside : V3d -> bool) (customAttribs : array<Durable.Def>) (minCellExponent' : Option<int>) = 

        let minCellExponent = defaultArg minCellExponent' System.Int32.MinValue

        if node.Cell.Exponent < minCellExponent  || isNodeFullyOutside node then 
           Seq.empty
        elif node.IsLeaf || node.Cell.Exponent = minCellExponent then
           if false && isNodeFullyInside node then 
                failwith "todo implement. (did not find node.ToGeneri"
           else 
                let subset = 
                    node.PositionsAbsolute 
                    |> GenericList.chooseiv (fun i p -> if isPositionInside p then ValueSome i else ValueNone)

                let ps = node.PositionsAbsolute.Subset subset
                let mutable data =
                    ImmutableDictionary<Durable.Def, System.Object>.Empty
                     .Add(GenericChunk.Defs.Positions3d, ps)

                let attributes = 
                    customAttribs 
                    |> Array.choose (fun def -> 
                        if node.Has def then 
                            Some (def, node.Properties.[def])
                        else None
                    )

                for (def, value) in attributes do
                    let d = value.Subset subset
                    data <- data.Add(def, value.Subset subset)

                GenericChunk data |> Seq.singleton
        else 
            seq {
                for i in 0 .. 7 do
                    let n = node.Subnodes.[i];
                    if isNull n then ()
                    else
                        let xs = QueryPointsCustom n.Value isNodeFullyInside isNodeFullyOutside isPositionInside customAttribs minCellExponent'
                        yield! xs
            }
