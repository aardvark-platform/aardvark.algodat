open Aardvark.Base
open Aardvark.Data
open Aardvark.Data.Points
open Aardvark.Data.Points.Import
open Aardvark.Geometry.Points
open SharpCompress.Common
open System
open System.Collections.Immutable
open System.IO
open System.Linq
open System.Text.RegularExpressions

module Hera =

    type HeraData(positions : V3f[], normals : V3f[], velocities : V3f[]) =

        do
            if positions.Length <> normals.Length || positions.Length <> velocities.Length then 
                failwith "All arrays must be of same length."

        member this.Positions with get() = positions
        member this.Normals with get() = normals
        member this.Velocities with get() = velocities
        member this.Count with get() = positions.Length

        member this.Serialize() =
            ImmutableDictionary<Durable.Def, obj>.Empty
                .Add(Durable.Octree.PositionsLocal3f, positions)
                .Add(Durable.Octree.Normals3f, normals)
                .Add(Durable.Octree.Velocities3f, velocities)
                .DurableEncode(Durable.Primitives.DurableMap, false)

        static member Serialize(data : HeraData) = data.Serialize()

        static member Deserialize(buffer : byte[]) =
            let d = buffer.DurableDecode<ImmutableDictionary<Durable.Def, obj>>()
            HeraData(
                d.[Durable.Octree.PositionsLocal3f] :?> V3f[],
                d.[Durable.Octree.Normals3f]        :?> V3f[],
                d.[Durable.Octree.Velocities3f]     :?> V3f[]
                )

    let importHeraDataFromStream stream =

        let lineDef = [|
            Ascii.Token.PositionX; Ascii.Token.PositionY; Ascii.Token.PositionZ
            Ascii.Token.VelocityX; Ascii.Token.VelocityY; Ascii.Token.VelocityZ
            |]

        let chunks = Ascii.Chunks(stream, -1L, lineDef, ParseConfig.Default)

        let pointset = 
            PointCloud.Import(chunks, ImportConfig.Default
                .WithInMemoryStore()
                .WithKey("data")
                .WithVerbose(false)
                .WithMaxDegreeOfParallelism(0)
                .WithMinDist(0.0)
                .WithNormalizePointDensityGlobal(false)
                )

        let allPoints = Chunk.ImmutableMerge(pointset.Root.Value.Collect(Int32.MaxValue))

        HeraData(
            allPoints.Positions.Map(fun p -> V3f p),
            allPoints.Normals.ToArray(),
            allPoints.Velocities.ToArray()
            )

    let importHeraDataFromFile filename =
        use fs = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read)
        importHeraDataFromStream(fs)

    let writeBufferToFile path buffer = File.WriteAllBytes(path, buffer)

    let convertFile inputfile outputfile =
        inputfile 
        |> importHeraDataFromFile 
        |> HeraData.Serialize
        |> writeBufferToFile outputfile

    let convertStream inputstream outputfile =
        inputstream 
        |> importHeraDataFromStream 
        |> HeraData.Serialize
        |> writeBufferToFile outputfile

    let deserialize filename = filename |> File.ReadAllBytes |> HeraData.Deserialize


open Hera
open System.IO.Compression
open SharpCompress.Readers

[<EntryPoint>]
let main argv =

    let tgzfilename = @"D:\Hera\r80_p0_m500_v6000_mbasalt_a1.0_1M.tar.gz"
    let pattern = Regex("impact\.([0-9]*)$")
    let lineDef = [|
        Ascii.Token.PositionX; Ascii.Token.PositionY; Ascii.Token.PositionZ
        Ascii.Token.VelocityX; Ascii.Token.VelocityY; Ascii.Token.VelocityZ
        |]

    let targetFolder = @"T:\Hera\Output"

    if not (Directory.Exists(targetFolder)) then Directory.CreateDirectory(targetFolder) |> ignore
    
    let tgz (pattern : Regex) tgzfilename = seq {
        
        use fs = File.Open(tgzfilename, FileMode.Open, FileAccess.Read, FileShare.Read)
        use zs = new GZipStream(fs, CompressionMode.Decompress)
        let reader = ReaderFactory.Open(zs)

        while reader.MoveToNextEntry() do
        
            let filename =  Path.GetFileName(reader.Entry.Key)

            printfn "[%s]" filename
        
            let m = pattern.Match filename
            if m.Success then
        
                let outputFile = Path.Combine(targetFolder, filename + ".durable")
            
                if not (File.Exists(outputFile)) then
                    use ms = new MemoryStream()
                    reader.WriteEntryTo(ms)
                    let buffer = ms.ToArray()
                    yield {| Key = reader.Entry.Key; Buffer = buffer; OutputFileName = outputFile |}
        }

    let buffers = tgz pattern tgzfilename

    buffers
        .AsParallel()
        .WithDegreeOfParallelism(8)
        .Select(fun fileData ->

            use stream = new MemoryStream(fileData.Buffer)
            let chunks = Ascii.Chunks(stream, -1L, lineDef, ParseConfig.Default).ToArray()

            try
                let pointset = 
                    PointCloud.Import(chunks, ImportConfig.Default
                        .WithInMemoryStore()
                        .WithKey("data")
                        .WithVerbose(false)
                        .WithMaxDegreeOfParallelism(0)
                        .WithMinDist(0.0)
                        .WithNormalizePointDensityGlobal(false)
                        )

                let allPoints = Chunk.ImmutableMerge(pointset.Root.Value.Collect(Int32.MaxValue))

                let data = 
                    HeraData(
                        allPoints.Positions.Map(fun p -> V3f p),
                        allPoints.Normals.ToArray(),
                        allPoints.Velocities.ToArray()
                        )

                data
                    |> HeraData.Serialize
                    |> writeBufferToFile fileData.OutputFileName

                Console.WriteLine("-> {0} ... {1,16:N0}", fileData.OutputFileName, data.Count)

                Ok {| OutputFileName = fileData.OutputFileName; PointCount = data.Count |}

            with
            | e -> 
                printfn "%s" (e.ToString())
                Error e

            )
        .ForEach(fun _ -> ())
        

    //let inputfile   = @"T:\Hera\impact.0014"
    //let outputfile  = @"T:\Hera\impact.0014.durable"
    
    //Report.BeginTimed("convert")
    //convert inputfile outputfile 
    //Report.EndTimed() |> ignore

    //let test = deserialize outputfile
    //printfn "deserialized file contains %d points" test.Count

    0
