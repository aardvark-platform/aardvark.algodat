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

    let tgzFileName  = @"D:\Hera\Impact_Simulation\r80_p45_m500_v6000_mbasalt_a4.0_1M.tar.gz"
    let targetFolder = @"D:\Hera\Impact_Simulation\tmp"

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


[<EntryPoint>]
let main argv =

    // exampleSingleFileConversion ()

    // exampleTgzConversion ()

    // exampleReadPerformanceOld ()

    exampleMmf ()

    0
