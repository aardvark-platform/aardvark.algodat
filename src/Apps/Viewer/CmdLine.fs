(*
    Copied from https://github.com/aardvark-community/hum.
*)
namespace Aardvark.Algodat.App.Viewer

open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Rendering
open Aardvark.Data.Points

open Aardvark.Geometry
open Aardvark.Geometry.Points
open Aardvark.SceneGraph
open Newtonsoft.Json.Linq
open System
open System.Collections.Generic
open System.IO
open System.Linq
open System.Net
open System.Threading
open Uncodium.SimpleStore
open Aardvark.Rendering.PointSet
open Aardvark.Data.Points.Import

[<AutoOpen>]
module CmdLine = 
    open Uncodium
    open System.Diagnostics

    let parseBounds filename =
        let json = JArray.Parse(File.readAllText filename) :> seq<JToken>
        json
            |> Seq.map (fun j -> Box3d.Parse(j.["Bounds"].ToObject<string>()))
            |> Array.ofSeq
            
    let view (store : string) (ids : list<string>) (args : Args) =
        Rendering.show args (ids |> List.choose (fun id ->
            LodTreeInstance.load "asdasdsadasd" id store []
        ))

    let info (filename : string) (args : Args) =
        initPointFileFormats ()
        let info = PointCloud.ParseFileInfo(filename, ParseConfig.Default)
        Console.WriteLine("filename      {0}", info.FileName)
        Console.WriteLine("format        {0}", info.Format.Description)
        Console.WriteLine("file size     {0:N0} bytes", info.FileSizeInBytes)
        Console.WriteLine("point count   {0:N0}", info.PointCount)
        Console.WriteLine("bounds        {0}", info.Bounds)

    let private foo (chunks : seq<Chunk>) =
        chunks
        |> Seq.map (fun chunk ->
        
            let k = 27
            let ps = chunk.Positions.ToArray()
            let cs = chunk.Colors.ToArray()

            let sw = Stopwatch()
            sw.Restart()
            let kd = ps.CreateRkdTree(Metric.Euclidean, 0.0)
            sw.Stop()
            printfn "kd-tree creation: %A" sw.Elapsed

            sw.Restart()
            let median = ps.Sum() / float ps.Length
            let cs = ps
                     |> Array.map2 (fun (c : C4b) p ->
                     
                        if p.Z < median.Z then
                            c
                        else
                            let closest = kd.GetClosest(p, 0.30, k)
                            
                            if closest.Count < 8 then
                                c
                            else
                                let mutable q = M33d.Zero
                                let mutable w = V3d.Zero
                                let mutable cvm = ps.ComputeCovarianceMatrix(closest, p)
                                let mutable o : int[] = null
                                if Eigensystems.Dsyevh3asc(&cvm, &q, &w, &o) then
                                    let flatness = 1.0 - w.[o.[0]] / w.[o.[1]]
                                    let verticality = Vec.Dot(q.Column(o.[0]).Normalized.Abs(), V3d.ZAxis)
                                    if flatness > 0.99 && verticality > 0.7 then C4b(255uy, c.G / 2uy, c.B / 2uy) else c
                                else
                                    c
                        ) cs
            sw.Stop()
            printfn "classification: %A" sw.Elapsed
            chunk.WithColors(cs)
            )

    let private mapReduce cfg (chunks : seq<Chunk>) = PointCloud.Chunks(chunks, cfg)
    let private batchImport dirname (cfg : ImportConfig) (args : Args) =
        getKnownFileExtensions ()
        |> Seq.collect (fun x -> Directory.EnumerateFiles(dirname, "*" + x, SearchOption.AllDirectories))
        |> Seq.skip args.skip
        |> Seq.truncate args.take
        |> Seq.collect (fun f ->
            printfn "importing file %s" f
            Pts.Chunks(f, cfg.ParseConfig)
            )
        |> mapReduce cfg

    let import (filename : string) (store : string) (id : string) (args : Args) =
    
        let isBatchImport = try Directory.Exists(filename) with _ -> false

        let filename =
            if filename.StartsWith "http" then
                if not (Directory.Exists(store)) then
                    Directory.CreateDirectory(store) |> ignore
                let wc = new WebClient()
                let fn = Uri(filename).Segments.Last()
                let targetFilename = Path.Combine(store, fn)
                printfn "downloading %s to %s" filename targetFilename
                wc.DownloadFile(filename, targetFilename)
                targetFilename
            else
                filename
            
        use store = PointCloud.OpenStore(store, LruDictionary(1L <<< 30))
        
        let mutable cfg =
            ImportConfig.Default
                .WithStorage(store)
                .WithKey(id)
                .WithVerbose(true)
                .WithMaxChunkPointCount(10000000)
                //.WithMinDist(0.005)
                //.WithNormalizePointDensityGlobal(true)
                //.WithMinDist(match args.minDist with | None -> 0.0 | Some x -> x)
                
        //match args.k with
        //| Some k -> let generate (ps : IList<V3d>) = Normals.EstimateNormals(ps.ToArray(), k) :> IList<V3f>
        //            cfg <- cfg.WithEstimateNormals(Func<IList<V3d>, IList<V3f>>(generate))
        //| None -> ()
        
        initPointFileFormats ()
        
        let ps =
            match isBatchImport, args.asciiFormat with
            
            // single file, known format
            | false, None   -> let sw = Stopwatch()
                               PointCloud.Import(filename, cfg)
                 
            // single file, custom ascii
            | false, Some f -> let chunks = Import.Ascii.Chunks(filename, f, cfg.ParseConfig)
                               PointCloud.Chunks(chunks, cfg)

            // batch, known formats
            | true, None    -> batchImport filename cfg args

            // batch, custom ascii
            | _             -> failwith "batch import with custom ascii format is not supported"

        Console.WriteLine("point count   {0:N0}", ps.PointCount)
        Console.WriteLine("bounds        {0}", ps.BoundingBox)

    let download (baseurl : string) (targetdir : string) (args : Args) =

        let xs = Download.listHrefsForKnownFormats baseurl
        Console.WriteLine("found {0:N0} point cloud files", xs.Count())
        Download.batchDownload baseurl targetdir xs