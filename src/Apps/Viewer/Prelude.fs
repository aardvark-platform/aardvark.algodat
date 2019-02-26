namespace Aardvark.Algodat.App.Viewer

open Aardvark.Data.Points.Import
open System

[<AutoOpen>]
module Prelude =

    let private formats = [
        Pts.PtsFormat
        E57.E57Format
        Yxh.YxhFormat
        ]

    /// Init point cloud file formats.
    let initPointFileFormats () = for x in formats do x |> ignore

    /// Known file extensions.
    let getKnownFileExtensions () =
        formats
        |> List.collect (fun x -> x.FileExtensions |> List.ofArray)
        |> List.map (fun x -> x.ToLowerInvariant())
        
  