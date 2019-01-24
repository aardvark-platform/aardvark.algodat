// Learn more about F# at http://fsharp.org


open Aardvark.Base.Incremental
open Aardvark.Base
open Aardvark.SceneGraph
open Aardvark.Base.Rendering
open Aardvark.Rendering.PointSet
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.Rendering.Text
open Hum


[<EntryPoint>]
let main args =  

    printHeader ()

    let args = Args.parse args

    match args.command with
    | None -> printUsage ()

    | Some (Info filename) -> info filename args

    | Some (Import (filename, store, key)) -> import filename store key args
        
    | Some (View (store, key)) ->
        view store key args
        
    | Some Gui ->
        failwith "not implemented"
        
    | Some (Download (baseurl, targetdir)) -> download baseurl targetdir args
        
    0
