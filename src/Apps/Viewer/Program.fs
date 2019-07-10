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
    //import a  @"C:\Users\Schorsch\Development\WorkDirectory\tech" "b" { Args.parse [||] with asciiFormat = Some bgr }

    let a = @"C:\Users\Schorsch\Desktop\oebb\Image_Matching_Meidling_Penzing.e57"
    let b = @"C:\Users\Schorsch\Desktop\oebb\Image_Matching_Penzing_Meidling.e57"
    let store = @"C:\Users\Schorsch\Development\WorkDirectory\oebb"
    //import a store "a" (Args.parse [||])
    //import b store "b" (Args.parse [||])
    movie store ["a"; "b"] //(Args.parse [||])

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
