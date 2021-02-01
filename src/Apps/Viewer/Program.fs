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
    //view @"C:\stores\innen_store" ["a2b7e0c1-e672-48d3-8958-9ff8678f2dc4"] (Args.parse [||])
    //view @"C:\Users\sm\Downloads\C_31EN2.LAZ.store" [File.readAllText @"C:\Users\sm\Downloads\C_31EN2.LAZ.key"] (Args.parse [||])
    //view @"C:\Users\sm\Downloads\C_30DN2.LAZ.store" [File.readAllText @"C:\Users\sm\Downloads\C_30DN2.LAZ.key"] (Args.parse [||])
    //view @"C:\Users\sm\Downloads\test.store" ["128330b1-8761-4a07-b160-76bcd7e2f70a"; "ab2f6f76-7eae-47c9-82d1-ad28b816abb9"] (Args.parse [||])
    //view @"T:\Vgm\Stores\C_30DN2.LAZ" ["C_30DN2.LAZ"] (Args.parse [||])
    view @"E:\AHN3\laz\orig\C_24HN2.LAZ.store" [File.readAllText "E:\AHN3\laz\orig\C_24HN2.LAZ.key"] (Args.parse [||])

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
