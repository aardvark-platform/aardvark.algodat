open FSharp.Data.Adaptive
open Aardvark.Base
open Aardvark.SceneGraph
open Aardvark.Rendering
open Aardvark.Rendering.PointSet
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.Rendering.Text
open Aardvark.Algodat.App.Viewer
open Aardvark.Data.Points.Import


[<EntryPoint>]
let main args =  

    //import @"C:\Users\Schorsch\Development\WorkDirectory\jbhaus-innen_erdgeschoss_blk.e57"  @"C:\Users\Schorsch\Development\WorkDirectory\jb_innen" "jb_innen" (Args.parse [||])
    view @"C:\stores\innen_store" ["a2b7e0c1-e672-48d3-8958-9ff8678f2dc4"] (Args.parse [||])

    let args = Args.parse args
    
    match args.command with

    | Some (Info filename) -> info filename args

    | Some (Import (filename, store, key)) -> import filename store key args
      
    | Some (View (store, key)) ->
        view store [key] args
      
    | Some Gui ->
        failwith "not implemented"
       
    | Some (Download (baseurl, targetdir)) -> download baseurl targetdir args
      
    | None ->
        printUsage()

    0
