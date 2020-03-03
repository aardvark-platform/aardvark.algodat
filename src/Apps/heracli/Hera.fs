namespace Hera
#nowarn "9"

open Aardvark.Base
open Aardvark.Data
open Aardvark.Data.Points
open Aardvark.Data.Points.Import
open Aardvark.Geometry.Points
open SharpCompress.Readers
open System
open System.Collections.Immutable
open System.IO
open System.IO.Compression
open System.IO.MemoryMappedFiles
open System.Linq
open System.Text.RegularExpressions
open System.Collections.Generic
open System.Threading.Tasks
open System.Diagnostics

module Hera =

    let lineDef = [|
        Ascii.Token.PositionX; Ascii.Token.PositionY; Ascii.Token.PositionZ
        Ascii.Token.VelocityX; Ascii.Token.VelocityY; Ascii.Token.VelocityZ
        |]

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
            let d = Aardvark.Data.Codec.DeserializeAs<ImmutableDictionary<Durable.Def, obj>>(buffer)
            HeraData(
                d.[Durable.Octree.PositionsLocal3f] :?> V3f[],
                d.[Durable.Octree.Normals3f]        :?> V3f[],
                d.[Durable.Octree.Velocities3f]     :?> V3f[]
                )

    type HeraDataRef = {
        Count : int
        PtrPositions : nativeptr<byte>
        PtrNormals : nativeptr<byte>
        PtrVelocities : nativeptr<byte>
        Mmf : MemoryMappedFile
        }
    with
        static member FromFile(filename : string) =

            let mmf = MemoryMappedFile.CreateFromFile(filename, FileMode.Open, null, 0L, MemoryMappedFileAccess.Read)
            
            let accessor = mmf.CreateViewAccessor(0L, 0L, MemoryMappedFileAccess.Read)
            let mutable g = Guid.Empty
            accessor.Read(0L, &g)
            printfn "%A" g

            
            let mutable p : nativeptr<byte> = Unchecked.defaultof<_>
            accessor.SafeMemoryMappedViewHandle.AcquirePointer(&p)

            printfn "p = %A" p

            let mutable offset = if g = Durable.Primitives.DurableMap.Id then 16L else 0L
            printfn "offset: %d" offset

            let mutable countEntries = 0
            accessor.Read(offset, &countEntries)
            offset <- offset + 4L
            printfn "count entries %A" countEntries

            let mutable pPs : nativeptr<byte> = Unchecked.defaultof<_>
            let mutable pNs : nativeptr<byte> = Unchecked.defaultof<_>
            let mutable pVs : nativeptr<byte> = Unchecked.defaultof<_>
            let mutable count = 0

            for i = 1 to countEntries do
                accessor.Read(offset, &g)
                offset <- offset + 16L

                accessor.Read(offset, &count)
                offset <- offset + 4L

                if g = Durable.Octree.PositionsLocal3f.Id then
                    pPs <- NativeInterop.NativePtr.add p (int offset)
                    offset <- offset + 12L * int64 count
                elif g = Durable.Octree.Normals3f.Id then
                    pNs <- NativeInterop.NativePtr.add p (int offset)
                    offset <- offset + 12L * int64 count
                elif g = Durable.Octree.Velocities3f.Id then
                    pVs <- NativeInterop.NativePtr.add p (int offset)
                    offset <- offset + 12L * int64 count
                else
                    failwith (sprintf "what is %A ?" g)

            { 
                Count = count
                PtrPositions = pPs
                PtrNormals = pNs
                PtrVelocities = pVs
                Mmf = mmf
            }

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

    let importHeraDataFromBuffer (buffer : byte[]) =
        use stream = new MemoryStream(buffer)
        importHeraDataFromStream stream

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

    type TgzEntry = { Key : string; Buffer : byte[] }

    /// Enumerates all tgz entries (key, buffer) matching the predicate.
    let enumerateTgzEntries predicate tgzFileName : TgzEntry seq = seq {
        
        use fs = File.Open(tgzFileName, FileMode.Open, FileAccess.Read, FileShare.Read)
        use zs = new GZipStream(fs, CompressionMode.Decompress)
        let reader = ReaderFactory.Open(zs)

        while reader.MoveToNextEntry() do
            let filename =  Path.GetFileName(reader.Entry.Key)
            if predicate filename then
                use ms = new MemoryStream()
                reader.WriteEntryTo(ms)
                let buffer = ms.ToArray()
                yield { Key = reader.Entry.Key; Buffer = buffer }
            else
                Report.Line("skipped {0}", reader.Entry.Key)
        }

    let convertTgz tgzFileName targetFolder =

        if (not (File.Exists(tgzFileName))) then failwith (sprintf "File does not exist: %s" tgzFileName)
        if not (Directory.Exists(targetFolder)) then Directory.CreateDirectory(targetFolder) |> ignore

        let pattern = Regex("impact\.([0-9]*)$")

        let getTargetFileNameFromKey (key : string) = 
            Path.Combine(targetFolder, Path.GetFileName(key) + ".durable")

        let predicate key =
            pattern.Match(key).Success && (not (File.Exists(getTargetFileNameFromKey key)))

        let processTgzEntry e =
            try
                let sw = Stopwatch()
                let targetFileName = getTargetFileNameFromKey e.Key
                Report.Line("processing {0}", targetFileName)
                sw.Start()
                importHeraDataFromBuffer e.Buffer
                |> HeraData.Serialize
                |> writeBufferToFile targetFileName
                sw.Stop()
                Report.Line("processing {0} [DONE] {1}", targetFileName, sw.Elapsed)
            with
            | e -> Report.Error("{0}", e.ToString())

        let queue = Queue<TgzEntry>()
        let mutable producerIsFinished = false
        let workers = List<Task>()

        // produce entries
        let producer = Task.Run(fun () ->
            try
                let xs = enumerateTgzEntries predicate tgzFileName
                let mutable i = 0
                for x in xs do
                    lock queue (fun () -> queue.Enqueue(x))
                    i <- i + 1
                    Report.Line("enqueued {0}", x.Key)

                producerIsFinished <- true
                    
            with
            | e -> Report.Error("{0}", e.ToString())
            )

        // consume entries
        let consumer = Task.Run(fun () ->
            try
                while not producerIsFinished || queue.Count > 0 do

                    match lock queue (fun () -> if queue.Count > 0 then Some (queue.Dequeue()) else None) with
                    | Some item -> Task.Run(fun () -> processTgzEntry item) |> workers.Add
                    | None -> Task.Delay(1000).Wait()
                    
            with
            | e -> Report.Error("{0}", e.ToString())
            )

        producer.Wait()
        consumer.Wait()
        Task.WhenAll(workers).Wait()

        ()




