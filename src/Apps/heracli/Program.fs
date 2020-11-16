open System.IO
open Aardvark.Data.Points
open Aardvark.Geometry.Points

#nowarn "9"

open System
open System.Diagnostics
open Aardvark.Base

open Hera

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
    let sw = Stopwatch()
    sw.Start()
    let particles = Hera.importHeraDataFromFileFull "T:\Hera\impact.0400"
    sw.Stop()
    printfn "%A" sw.Elapsed

    let chunk = 
        [
            (GenericChunk.Defs.Positions3f,         particles.Positions :> obj)
            (GenericChunk.Defs.Normals3f,           particles.EstimatedNormals :> obj)
            (Hera.Defs.AlphaJutzi,                  particles.AlphaJutzi :> obj)
            (Hera.Defs.AverageSquaredDistances,     particles.AverageSquaredDistances :> obj)
            (Hera.Defs.CubicRootsOfDamage,          particles.CubicRootOfDamage :> obj)
            (Hera.Defs.Densities,                   particles.Densities :> obj)
            (Hera.Defs.InternalEnergies,            particles.InternalEnergies :> obj)
            (Hera.Defs.LocalStrains,                particles.LocalStrains :> obj)
            (Hera.Defs.Masses,                      particles.Masses :> obj)
            (Hera.Defs.MaterialTypes,               particles.MaterialTypes :> obj)
            (Hera.Defs.NumbersOfActivatedFlaws,     particles.NumberOfActivatedFlaws :> obj)
            (Hera.Defs.NumbersOfFlaws,              particles.NumberOfFlaws :> obj)
            (Hera.Defs.NumberOfInteractionPartners, particles.NumberOfInteractionPartners :> obj)
            (Hera.Defs.Pressures,                   particles.Pressures :> obj)
            (Hera.Defs.Sigmas,                      particles.Sigmas :> obj)
            (Hera.Defs.SmoothingLengths,            particles.SmoothingLengths :> obj)
            (Hera.Defs.Velocities,                  particles.Velocities :> obj)
        ]
        |> Map.ofList
        |> GenericChunk

    printfn "chunk.Count       = %d" chunk.Count
    printfn "chunk.BoundingBox = %A" chunk.BoundingBox

    let config = 
        ImportConfig.Default
          .WithInMemoryStore()
          .WithRandomKey()
          .WithVerbose(true)
          .WithMaxDegreeOfParallelism(0)
          .WithMinDist(0.0)
          .WithOctreeSplitLimit(8192)
          .WithNormalizePointDensityGlobal(false)
               
    let oldChunk = Chunk(particles.Positions |> Array.map V3d, null, particles.EstimatedNormals, null, null);
    let oldPointcloud = PointCloud.Import(oldChunk, config)      
    printfn "pointcloud bounds (old): %A" oldPointcloud.BoundingBox

    let pointcloud = PointCloud.Import(chunk, config)      
    printfn "pointcloud bounds (new): %A" pointcloud.BoundingBox

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
