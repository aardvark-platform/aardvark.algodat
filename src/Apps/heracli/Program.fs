open Aardvark.Base
open Aardvark.Data.Points
open Aardvark.Geometry.Points
open Hera
open System
open System.Diagnostics
open System.IO
open Uncodium.SimpleStore

#nowarn "9"

let exampleSingleFileConversion () =

    let inputfile   = @"T:\Hera\impact.0014"
    let outputfile  = @"T:\Hera\impact.0014.durable"
       
    Report.BeginTimed("convert")
    Hera.convertFile inputfile outputfile 
    Report.EndTimed() |> ignore

    let test = Hera.deserialize outputfile
    printfn "deserialized file contains %d points" test.Count

let exampleTgzConversion () =

    let tgzFileName  = @"D:\Hera\Impact_Simulation\r80_p0_m500_v6000_mbasalt_a1.0_1M.tar.gz"
    let targetFolder = @"D:\Hera\Impact_Simulation\r80_p0_m500_v6000_mbasalt_a1.0_1M.tar.gz.betternormals.durable"

    Hera.convertTgz tgzFileName targetFolder

let exampleReadPerformanceOld () =

    let filename  = @"T:\Hera\impact.0000.durable.new"
    
    let sw = Stopwatch()
    for i = 1 to 100 do
        sw.Restart()
        let data = Hera.deserialize filename
        sw.Stop()
        printfn "%A" sw.Elapsed

let exampleMmf () =
    printfn "%A" (Hera.HeraDataRef.FromFile(@"T:\Hera\impact.0000.durable.old"))
    printfn "%A" (Hera.HeraDataRef.FromFile(@"T:\Hera\impact.0000.durable.new"))

let exampleCheckNormals () =
    Directory.GetFiles(@"D:\Hera\Impact_Simulation\r80_p0_m500_v6000_mbasalt_a1.0_1M.tar.gz.durable")
    |> Seq.map Hera.HeraData.Deserialize
    |> Seq.iter Hera.HeraData.CheckNormals

let exampleImportHeraDataFromFileFull () =

    let datafile  = @"T:\Hera\impact.0400"
    let storepath = @"T:\Vgm\Stores\hera\impact.0400"

    let id = Hera.importHeraDataIntoStore datafile storepath false
    printfn "%s" id

    //Report.BeginTimed("importing " + datafile)
    //let particles = Hera.importHeraDataFromFileFull datafile
    //Report.EndTimed() |> ignore

    //let chunk = particles.ToGenericChunk ()
    //printfn "chunk.Count       = %d" chunk.Count
    //printfn "chunk.BoundingBox = %A" chunk.BoundingBox

    //let store = (new SimpleDiskStore(storepath)).ToPointCloudStore()
    //Report.BeginTimed("saving particles octree to store")
    //let pointcloud = particles.SaveToStore store
    //Report.EndTimed() |> ignore
    //printfn "pointcloud bounds (new): %A" pointcloud.BoundingBox
    //let pointcloudId = pointcloud.Id
    //printfn "pointcloud id          : %s" pointcloud.Id
    //let pointcloudRootId = pointcloud.Root.Value.Id
    //printfn "pointcloud root id     : %A" pointcloud.Root.Value.Id
    
    //store.Dispose()
    //printfn "closed store"

    //printfn "reopening store ..."
    //let store = (new SimpleDiskStore(storepath)).ToPointCloudStore()
    //let pointcloud2 = store.GetPointSet(pointcloudId)
    //printfn "pointcloud bounds (new): %A" pointcloud2.BoundingBox
    //printfn "pointcloud id          : %s" pointcloud2.Id
    //printfn "pointcloud root id     : %A" pointcloud2.Root.Value.Id
    
    ()

[<EntryPoint>]
let main argv =

    exampleImportHeraDataFromFileFull ()

    // exampleSingleFileConversion ()

    // exampleTgzConversion ()

    // exampleReadPerformanceOld ()

    // exampleMmf ()

    // exampleCheckNormals ()

    0
