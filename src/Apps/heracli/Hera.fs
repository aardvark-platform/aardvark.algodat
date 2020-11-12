namespace Hera
#nowarn "9"

open Aardvark.Base
open Aardvark.Data
open Aardvark.Data.Points
open Aardvark.Data.Points.Import
open Aardvark.Geometry
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

    module Defs =

        let private def (id : string) (name : string) (description : string) (t : Durable.Def) = 
            Durable.Def(new Guid(id), name, description, t.Id, false)

        let Velocity                    = def "71bd64d9-b18b-44a6-b52f-bcbc811005fe" "Hera.Particle.Velocity"
                                              "Velocity."
                                              Durable.Aardvark.V3f
        
        let Mass                        = def "aea3be45-708e-4657-8b89-7c888630c7a9" "Hera.Particle.Mass"
                                              "Mass of SPH particle."
                                              Durable.Primitives.Float32
        
        let Density                     = def "7654dbb4-09d6-4b1b-ba20-c9d12a2895e4" "Hera.Particle.Density"
                                              "Density of SPH particle."
                                              Durable.Primitives.Float32
        
        let InternalEnergy              = def "df469fa5-5ff8-45be-917a-d19bc31655f2" "Hera.Particle.InternalEnergy"
                                              "Internal energy of SPH particle."
                                              Durable.Primitives.Float32
        
        let SmoothingLength             = def "8cec485b-1d82-4ba8-b198-7b3635cd4fe5" "Hera.Particle.SmoothingLength"
                                              "Smoothing length (always the same)."
                                              Durable.Primitives.Float32
        
        let NumberOfInteractionPartners = def "025a2ad0-19cf-4ee2-90a3-2aca8924aa80" "Hera.Particle.NumberOfInteractionPartners"
                                              "Number of interaction partners of SPH particle."
                                              Durable.Primitives.Int32
        
        let MaterialType                = def "d7e8a38f-d406-4970-b51e-934938d1ba98" "Hera.Particle.MaterialType"
                                              "Material type of SPH particle."
                                              Durable.Primitives.Int32
        
        let NumberOfFlaws               = def "97dd2bef-5664-4755-9ab3-88568be093b5" "Hera.Particle.NumberOfFlaws"
                                              "Number of flaws of SPH particle (Grady-Kipp damage model)."
                                              Durable.Primitives.Int32
        
        let NumberOfActivatedFlaws      = def "c7c8e397-3f8c-4df0-b803-518c413fb552" "Hera.Particle.NumberOfActivatedFlaws"
                                              "Number of activated flaws of SPH particle."
                                              Durable.Primitives.Int32
        
        let CubicRootOfDamage           = def "49254ad8-9e3e-4427-ad38-d1d0fcda7512" "Hera.Particle.CubicRootOfDamage"
                                              "Cubic root of damage of SPH particle."
                                              Durable.Primitives.Float32
        
        let LocalStrain                 = def "7c4957d1-42d1-411d-9ccd-6db89acab579" "Hera.Particle.LocalStrain"
                                              "Local_strain of SPH particle."
                                              Durable.Primitives.Float32
        
        let Sigma                       = def "c37db1d5-21b8-4264-97e7-fd17bb051296" "Hera.Particle.Sigma"
                                              "Stress tensor component of SPH particle."
                                              Durable.Aardvark.M33f
        
        let AlphaJutzi                  = def "4934c779-38ac-472c-a0ba-4edb3bebef1e" "Hera.Particle.AlphaJutzi"
                                              "alpha_jutzi = density_compact / density_porous of SPH particle."
                                              Durable.Primitives.Float32
        
        let Pressure                    = def "33258bed-9d43-4d86-89bf-9f38cabd3640" "Hera.Particle.Pressure"
                                              "pressure 28->28+number of flaws:activation thresholds for this particle."
                                              Durable.Primitives.Float32

    let lineDef = [|
        Ascii.Token.PositionX; Ascii.Token.PositionY; Ascii.Token.PositionZ
        Ascii.Token.VelocityX; Ascii.Token.VelocityY; Ascii.Token.VelocityZ
        |]

    type Particle = {
        /// Spatial position.
        Position                    : V3d
        /// Velocity.
        Velocity                    : V3f
        /// Mass of SPH particle.
        Mass                        : float32 
        /// Density of SPH particle.
        Density                     : float32
        /// Internal energy of SPH particle.
        InternalEnergy              : float32
        /// Smoothing length (always the same).
        SmoothingLength             : float32
        /// Number of interaction partners of SPH particle.
        NumberOfInteractionPartners : int
        /// Material type of SPH particle.
        MaterialType                : int
        /// Number of flaws of SPH particle (Grady-Kipp damage model).
        NumberOfFlaws               : int   
        /// Number of activated flaws of SPH particle.
        NumberOfActivatedFlaws      : int
        /// Cubic root of damage of SPH particle.
        CubicRootOfDamage           : float32
        /// Local_strain of SPH particle.
        LocalStrain                 : float32 
        /// Stress tensor component of SPH particle.
        Sigma                       : M33f
        /// alpha_jutzi = density_compact / density_porous of SPH particle
        AlphaJutzi                  : float32 
        /// pressure 28->28+number of flaws:activation thresholds for this particle
        Pressure                    : float32  
        }

    type HeraData(positions : V3f[], normals : V3f[], velocities : V3f[], densities : float32[]) =

        do
            if positions.Length <> normals.Length || positions.Length <> velocities.Length then 
                failwith "All arrays must be of same length."

        member this.Positions with get() = positions
        member this.Normals with get() = normals
        member this.Velocities with get() = velocities
        member this.Densities with get() = densities
        member this.Count with get() = positions.Length

        member this.CheckNormals () =
            for n in this.Normals do
                if not (n.Length.ApproximateEquals(1.0f, 0.001f)) then
                    printfn "[unstable normal] %A" n

        member this.Serialize() =
            ImmutableDictionary<Durable.Def, obj>.Empty
                .Add(Durable.Octree.PositionsLocal3f, positions)
                .Add(Durable.Octree.Normals3f, normals)
                .Add(Durable.Octree.Velocities3f, velocities)
                .Add(Durable.Octree.Densities1f, densities)
                .DurableEncode(Durable.Primitives.DurableMap, false)

        static member Serialize(data : HeraData) = data.Serialize()

        static member Deserialize(buffer : byte[]) =
            let d = Aardvark.Data.Codec.DeserializeAs<ImmutableDictionary<Durable.Def, obj>>(buffer)
            HeraData(
                d.[Durable.Octree.PositionsLocal3f] :?> V3f[],
                d.[Durable.Octree.Normals3f]        :?> V3f[],
                d.[Durable.Octree.Velocities3f]     :?> V3f[],
                d.[Durable.Octree.Densities1f]      :?> float32[]
                )

        static member Deserialize(filename : string) =
            HeraData.Deserialize(File.ReadAllBytes(filename))

        static member CheckNormals(data : HeraData) = data.CheckNormals ()
            
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

        let chunks = Ascii.Chunks(stream, -1L, lineDef, ParseConfig.Default).ToArray()
        let data = Chunk.ImmutableMerge(chunks)

        let ps = data.Positions.Map(fun p -> V3f p)
        let vs = data.Velocities.ToArray()
        let struct (ns, ds) = ps.EstimateNormalsAndLocalDensity(16)

        HeraData(positions = ps, normals = ns, velocities = vs, densities = ds)

    let importHeraDataFromFile filename =
        use fs = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read)
        importHeraDataFromStream(fs)

    let importHeraDataFromFileFull filename =
        File
          .ReadLines(filename)
          //.Take(100)
          .AsParallel()
          .Select(fun line ->
              let ts = line.SplitOnWhitespace()
              {
                  Position                = V3d(float   ts.[ 0], float   ts.[1], float   ts.[2])
                  Velocity                = V3f(float32 ts.[ 3], float32 ts.[4], float32 ts.[5])
                  Mass                        = float32 ts.[ 6]
                  Density                     = float32 ts.[ 7]
                  InternalEnergy              = float32 ts.[ 8]
                  SmoothingLength             = float32 ts.[ 9]
                  NumberOfInteractionPartners = int     ts.[10]
                  MaterialType                = int     ts.[11]
                  NumberOfFlaws               = int     ts.[12]
                  NumberOfActivatedFlaws      = int     ts.[13]
                  CubicRootOfDamage           = float32 ts.[14]
                  LocalStrain                 = float32 ts.[15]
                  Sigma                   = M33f(
                                                float32 ts.[16], float32 ts.[17], float32 ts.[18],
                                                float32 ts.[19], float32 ts.[20], float32 ts.[21],
                                                float32 ts.[22], float32 ts.[23], float32 ts.[24]
                                                  )
                  AlphaJutzi                  = float32 ts.[25]
                  Pressure                    = float32 ts.[26]
              })
          .ToArray()
        
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




