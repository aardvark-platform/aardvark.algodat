open Aardvark.Base.Incremental
open Aardvark.Base
open Aardvark.SceneGraph
open Aardvark.Base.Rendering
open Aardvark.Rendering.PointSet
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.Rendering.Text
open Aardvark.Algodat.App.Viewer
open Aardvark.Data.Points.Import


[<EntryPoint>]
let main args =  

    //let a = @"C:\Users\Schorsch\Development\WorkDirectory\Technologiezentrum_Teil1.pts"
    //let bgr =
    //    [|
    //        Ascii.Token.PositionX
    //        Ascii.Token.PositionY
    //        Ascii.Token.PositionZ
    //        Ascii.Token.Intensity
    //        Ascii.Token.ColorB
    //        Ascii.Token.ColorG
    //        Ascii.Token.ColorR
    //    |]
    //import a  @"C:\Users\Schorsch\Development\WorkDirectory\tech" "a" (Args.parse [||])
    //import a  @"C:\Users\Schorsch\Development\WorkDirectory\tech" "b" { Args.parse [||] with asciiFormat = Some bgr }

    view @"C:\Users\Schorsch\Development\WorkDirectory\tech" ["a"; "b"; "a"; "b"] (Args.parse [||])

    //let args = Args.parse args
    
    //match args.command with

    //| Some (Info filename) -> info filename args

    //| Some (Import (filename, store, key)) -> import filename store key args
      
    //| Some (View (store, key)) ->
    //    view store key args
      
    //| Some Gui ->
    //    failwith "not implemented"
      
    //| Some (Download (baseurl, targetdir)) -> download baseurl targetdir args
      
    //| None ->
    //    printUsage()

    0
