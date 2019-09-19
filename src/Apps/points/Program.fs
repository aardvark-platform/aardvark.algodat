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
    printfn "  import -o <outarg> <pointcloudfile> [-okey <key>]"
    printfn "  export -i <inarg> -o <outarg>"
    printfn "  info -i <inarg> -ikey <key>"
    printfn "  -o [store|folder] <path>  ... output storage"
    printfn "  -i [store|folder] <path>  ... input storage"
    printfn ""
    printfn "points import -o store ./mystore foo.e57"

let createMetaData (pc : PointSet) (gzipped : bool) =
    let root = pc.Root.Value
    let data = {|
        pointSetId = pc.Id;
        rootId = root.Id.ToString();
        pointCount = root.PointCountTree;
        bounds = root.BoundingBoxExactGlobal;
        centroid = V3d(root.CentroidLocal) + root.Center;
        centroidStdDev = root.CentroidLocalStdDev;
        cell = root.Cell;
        totalNodes = root.CountNodes(true);
        gzipped = gzipped
    |}

    let json = JObject.FromObject(data);

    json

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

    match args.metadataKey with
    | Some k -> let meta = createMetaData pc false
                store.Add(k, meta)
                store.Flush()
    | None   -> ()

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

    let gzipped = match args.gzipped with | Some x -> x | None -> false

    Report.BeginTimed("exporting")
    match args.inlining with
    | Some true -> inStore.InlinePointSet(key, outStore, gzipped)
    | _         -> if gzipped then printfn "[WARNING] -z is only supported with -inline"
                   inStore.ExportPointSet(key, outStore, args.verbose)
    outStore.Flush()
    Report.EndTimed() |> ignore

    match args.metadataKey with
    | Some k -> let pc = inStore.GetPointSet(key)
                let meta = createMetaData pc false
                outStore.Add(k, meta)
                outStore.Flush()
    | None   -> ()

let root args =
    
    let inPath =
        match args.inPath with
        | Some p -> p
        | None -> failwith "missing input (-i)"

    let inStore = 
        match args.inType with
        | Some Store  -> (new SimpleDiskStore(inPath)).ToPointCloudStore()
        | Some Folder -> (new SimpleFolderStore(inPath)).ToPointCloudStore()
        | _           -> failwith "missing input storage (-i)"

    let key =
        match args.inKey with 
        | Some k -> k 
        | None   -> failwith "missing input point cloud key (-ikey)"

    let pointSet = inStore.GetPointSet(key)
    if pointSet <> null then
        printfn "PointSet key = %s" key
        printfn "  root node key = %A" (pointSet.Root.Value.Id)
    else
        printfn "No PointSet with key %s." key


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
    | Some Root     -> root args

    0 // return an integer exit code
