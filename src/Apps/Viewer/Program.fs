open Aardvark.Base
open Aardvark.Rendering.Text
open Aardvark.Algodat.App.Viewer


[<EntryPoint>]
let main args =  
    //let pts = @"C:\bla\pts\lowergetikum 20230321.e57"
    //import pts  @"C:\bla\store\lowergetikum\data.bin" "a" (Args.parse [||])
    //exit 0

    //view @"C:\stores\innen_store" ["a2b7e0c1-e672-48d3-8958-9ff8678f2dc4"] (Args.parse [||])
    //view @"C:\Users\sm\Downloads\C_31EN2.LAZ.store" [File.readAllText @"C:\Users\sm\Downloads\C_31EN2.LAZ.key"] (Args.parse [||])
    //view @"C:\Users\sm\Downloads\C_30DN2.LAZ.store" [File.readAllText @"C:\Users\sm\Downloads\C_30DN2.LAZ.key"] (Args.parse [||])
    //view @"C:\Users\sm\Downloads\test.store" ["128330b1-8761-4a07-b160-76bcd7e2f70a"; "ab2f6f76-7eae-47c9-82d1-ad28b816abb9"] (Args.parse [||])
    
    let store = @"C:\bla\store\lowergetikum\data.bin"
    let key = Path.combine [System.IO.Path.GetDirectoryName store;"key.txt"] |> File.readAllText
    view store [key] (Args.parse [||])

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
