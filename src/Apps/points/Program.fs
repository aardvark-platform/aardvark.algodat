open System
open Aardvark.Apps.Points
open Uncodium.SimpleStore
open Aardvark.Geometry.Points
open Aardvark.Data.Points
open Aardvark.Base
open System.IO
open Newtonsoft.Json.Linq
open Newtonsoft.Json

let usage () =
    printfn "usage: points <command>"
    printfn "  import -o <outarg> <pointcloudfile>"
    printfn "  export -i <inarg> -o <outarg> "
    printfn "  -o [store|folder] <path>  ... output storage"
    printfn "  -i [store|folder] <path>  ... input storage"
    printfn ""
    printfn "points import -o store ./mystore foo.e57"

let import (args : Args) =

    let outPath =
      match args.outPath with
      | Some p -> p
      | None -> failwith "missing output (-o)"

    let store = 
      match args.outType with
      | Some Store  -> (new SimpleDiskStore(outPath)).ToPointCloudStore()
      | Some Folder -> (new SimpleFolderStore(outPath)).ToPointCloudStore()
      | _           -> failwith "missing output (-o)"

    let outKey =
      match args.outKey with 
      | Some k -> k 
      | None -> Guid.NewGuid().ToString()

    let config = 
        ImportConfig.Default
          .WithStorage(store)
          .WithKey(outKey)
          .WithOctreeSplitLimit(args.splitLimit)
          .WithMinDist(args.minDist)
          .WithNormalizePointDensityGlobal(true)
          .WithVerbose(true)

    Report.BeginTimed("importing")
    let pc = 
      match args.files with
      | filename :: [] -> PointCloud.Import(filename, config)
      | _ -> failwith "specify exactly 1 filename to import"
    store.Flush()
    Report.EndTimed() |> ignore

    let info = {|
      pointCountTree = pc.Root.Value.PointCountTree;
      boundingBoxExactGlobal = pc.Root.Value.BoundingBoxExactGlobal
      outType = match args.outType with | Some x -> x.ToString() | None -> "null";
      outPath = Path.GetFullPath(outPath);
      pointCloudId = config.Key;
      rootNodeId = pc.Root.Value.Id
      nodeCount = pc.Root.Value.CountNodes(true)
    |}
    
    printfn "%s" (JObject.FromObject(info).ToString(Formatting.Indented))

let export args =
    
    let inPath =
        match args.inPath with
        | Some p -> p
        | None -> failwith "missing input (-i)"

    let inStore = 
        match args.inType with
        | Some Store  -> (new SimpleDiskStore(inPath)).ToPointCloudStore()
        | Some Folder -> (new SimpleFolderStore(inPath)).ToPointCloudStore()
        | _           -> failwith "missing input storage (-i)"

    let outPath =
        match args.outPath with
        | Some p -> p
        | None -> failwith "missing output (-o)"

    let outStore = 
        match args.outType with
        | Some Store  -> (new SimpleDiskStore(outPath)).ToPointCloudStore()
        | Some Folder -> (new SimpleFolderStore(outPath)).ToPointCloudStore()
        | _           -> failwith "missing output storage (-o)"

    let key =
        match args.inKey, args.outKey with 
        | Some k, None -> k 
        | None, _ -> failwith "missing input point cloud key (-ikey)"
        | _, Some _ -> failwith "must not define output key for export (-okey)" 

    Report.BeginTimed("exporting")
    match args.inlining with
    | Some true -> inStore.InlinePointSet(key, outStore)
    | _         -> inStore.ExportPointSet(key, outStore)
    outStore.Flush()
    Report.EndTimed() |> ignore

[<EntryPoint>]
let main argv =

    // parse arguments
    let args = Aardvark.Apps.Points.Args.parse argv
    printfn "%A" args

    // process
    match args.command with
    | None          -> failwith "no command"
    | Some Import   -> import args
    | Some Export   -> export args

    0 // return an integer exit code
