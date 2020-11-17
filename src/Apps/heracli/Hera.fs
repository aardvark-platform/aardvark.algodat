namespace Hera

open System.Runtime.CompilerServices

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
open Uncodium.SimpleStore

module Hera =

    module Defs =

        let private def (id : string) (name : string) (description : string) (t : Durable.Def) (isArray : bool) = 
            Durable.Def(new Guid(id), name, description, t.Id, isArray)

        let ParticleSet                 = def "3c65f19c-50fa-4183-9f82-411a60ed8039" "Hera.ParticleSet"
                                              "Set of Hera particles."
                                              Durable.Primitives.DurableMap false

        let Positions                   = def "3fcec3fa-282d-4255-b541-94c6d4f8a14c" "Hera.Particle.Positions"
                                              "Spatial position of SPH particle."
                                              Durable.Aardvark.V3fArray true

        let EstimatedNormals            = def "b2bb7961-0422-4f1c-8d64-0d4bf049083d" "Hera.Particle.EstimatedNormals"
                                              "Estimated normal vector (from k-nearest neighbours positions) of SPH particle."
                                              Durable.Aardvark.V3fArray true

        let Velocities                  = def "2b2c08af-d81a-4d2e-a174-74b49ea231f3" "Hera.Particle.Velocities"
                                              "Velocity of SPH particle."
                                              Durable.Aardvark.V3fArray true
        
        let Masses                      = def "8b47ae79-aacc-4f72-9652-cf60d6ffe948" "Hera.Particle.Masses"
                                              "Mass of SPH particle."
                                              Durable.Primitives.Float32Array true
        
        let Densities                   = def "f9da3db2-3e3e-4d62-97e7-c46e0d979be9" "Hera.Particle.Densities"
                                              "Density of SPH particle."
                                              Durable.Primitives.Float32Array true
        
        let InternalEnergies            = def "47a77854-8397-410f-aa41-58a5ce7695e7" "Hera.Particle.InternalEnergies"
                                              "Internal energy of SPH particle."
                                              Durable.Primitives.Float32Array true
        
        let SmoothingLengths            = def "b5e0c915-34c4-4bab-ae48-f43726d72726" "Hera.Particle.SmoothingLengths"
                                              "Smoothing length (always the same)."
                                              Durable.Primitives.Float32Array true
        
        let NumberOfInteractionPartners = def "6cb36a49-b3ac-4c94-9869-7f0eb09950ac" "Hera.Particle.NumberOfInteractionPartners"
                                              "Number of interaction partners of SPH particle."
                                              Durable.Primitives.Int32Array true
        
        let MaterialTypes               = def "1da0927e-6b72-4a47-8d89-c58d8876e954" "Hera.Particle.MaterialTypes"
                                              "Material type of SPH particle."
                                              Durable.Primitives.Int32Array true
        
        let NumbersOfFlaws              = def "467008f9-2ad8-4aa3-a6ff-cb412b59e70d" "Hera.Particle.NumbersOfFlaws"
                                              "Number of flaws of SPH particle (Grady-Kipp damage model)."
                                              Durable.Primitives.Int32Array true
        
        let NumbersOfActivatedFlaws     = def "4833966e-a2ec-4f6a-94f8-76fbebb9d97f" "Hera.Particle.NumbersOfActivatedFlaws"
                                              "Number of activated flaws of SPH particle."
                                              Durable.Primitives.Int32Array true
        
        let CubicRootsOfDamage          = def "0fb53221-5bd7-4caa-8c72-b9f0cc7b8d9c" "Hera.Particle.CubicRootsOfDamage"
                                              "Cubic root of damage of SPH particle."
                                              Durable.Primitives.Float32Array true
        
        let LocalStrains                = def "993162f5-ad29-45af-a653-b57dc8ee31a1" "Hera.Particle.LocalStrains"
                                              "Local_strain of SPH particle."
                                              Durable.Primitives.Float32Array true
        
        let Sigmas                      = def "9c3c64a7-cb96-45a3-aa6b-ab6da3316128" "Hera.Particle.Sigmas"
                                              "Stress tensor component of SPH particle."
                                              Durable.Aardvark.M33fArray true
        
        let AlphaJutzi                  = def "4cc0437b-a096-4794-ba24-5175fdd54095" "Hera.Particle.AlphaJutzi"
                                              "alpha_jutzi = density_compact / density_porous of SPH particle."
                                              Durable.Primitives.Float32Array true
        
        let Pressures                   = def "1cb212a4-d33f-4f02-b38d-8cf69eafb6dc" "Hera.Particle.Pressures"
                                              "pressure 28->28+number of flaws:activation thresholds for this particle."
                                              Durable.Primitives.Float32Array true

        let AverageSquaredDistances     = def "23354127-f93f-4216-a0af-b26f29e6e8fa" "Hera.Particle.AverageSquaredDistances"
                                              "Average squared distance of k-nearest points to their centroid."
                                              Durable.Primitives.Float32Array true

        [<MethodImpl(MethodImplOptions.NoInlining ||| MethodImplOptions.NoOptimization)>]
        let private keep (_ : 'a) = ()

        [<OnAardvarkInit;CompilerMessage("Internal only",1337,IsHidden=true)>]
        let init () = keep ParticleSet

    let lineDef = [|
        Ascii.Token.PositionX; Ascii.Token.PositionY; Ascii.Token.PositionZ
        |]

    type Particle = {
        /// Spatial position.
        Position                    : V3f
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

    type HeraData(data : IReadOnlyDictionary<Durable.Def, obj>) =

        let data = Dictionary<_,_>(data)
        do
            // backwards compatibility
            let update olddef newdef = match data.TryGetValue(olddef) with | true, xs -> data.Add(newdef, xs) | _ -> ()
            update Durable.Octree.PositionsLocal3f Defs.Positions
            update Durable.Octree.Normals3f Defs.EstimatedNormals
            update Durable.Octree.Velocities3f Defs.Velocities
            update Durable.Octree.Densities1f Defs.AverageSquaredDistances



            let (hasPositions, ps) = data.TryGetValue(Defs.Positions)
            if not hasPositions then failwith "Data must contain Defs.PositionArray."
            let ps = ps :?> V3f[]

            let (hasNormals, ns) = data.TryGetValue(Defs.EstimatedNormals)
            if not hasNormals then failwith "Data must contain Defs.EstimatedNormalArray."
            let ns = ns :?> V3f[]

            let (hasVelocities, vs) = data.TryGetValue(Defs.Velocities)
            if not hasVelocities then failwith "Data must contain Hera.Defs.VelocityArray."
            let vs = vs :?> V3f[]

            if ps.Length <> ns.Length || ps.Length <> vs.Length then 
                failwith "All arrays must be of same length."

        member this.Positions                   with get() = data.[Defs.Positions]                      :?> V3f[]
        member this.EstimatedNormals            with get() = data.[Defs.EstimatedNormals]               :?> V3f[]
        member this.AverageSquaredDistances     with get() = data.[Defs.AverageSquaredDistances]        :?> float32[]
        member this.Velocities                  with get() = data.[Defs.Velocities]                     :?> V3f[]
        member this.Masses                      with get() = data.[Defs.Masses]                         :?> float32[]
        member this.Densities                   with get() = data.[Defs.Densities]                      :?> float32[]
        member this.InternalEnergies            with get() = data.[Defs.InternalEnergies]               :?> float32[]
        member this.SmoothingLengths            with get() = data.[Defs.SmoothingLengths]               :?> float32[]
        member this.NumberOfInteractionPartners with get() = data.[Defs.NumberOfInteractionPartners]    :?> int32[]
        member this.MaterialTypes               with get() = data.[Defs.MaterialTypes]                  :?> int32[]
        member this.NumberOfFlaws               with get() = data.[Defs.NumbersOfFlaws]                 :?> int32[]
        member this.NumberOfActivatedFlaws      with get() = data.[Defs.NumbersOfActivatedFlaws]        :?> int32[]
        member this.CubicRootOfDamage           with get() = data.[Defs.CubicRootsOfDamage]             :?> float32[]
        member this.LocalStrains                with get() = data.[Defs.LocalStrains]                   :?> float32[]
        member this.Sigmas                      with get() = data.[Defs.Sigmas]                         :?> M33f[]
        member this.AlphaJutzi                  with get() = data.[Defs.AlphaJutzi]                     :?> float32[]
        member this.Pressures                   with get() = data.[Defs.Pressures]                      :?> float32[]

        member this.Count                       with get() = this.Positions.Length

        member this.CheckNormals () =
            for n in this.EstimatedNormals do
                if not (n.Length.ApproximateEquals(1.0f, 0.001f)) then
                    printfn "[unstable normal] %A" n

        member this.Serialize () =
            data.DurableEncode(Defs.ParticleSet, false)

        member this.ToGenericChunk () =
            [
                (GenericChunk.Defs.Positions3f,    this.Positions :> obj)
                (GenericChunk.Defs.Normals3f,      this.EstimatedNormals :> obj)
                (Defs.AlphaJutzi,                  this.AlphaJutzi :> obj)
                (Defs.AverageSquaredDistances,     this.AverageSquaredDistances :> obj)
                (Defs.CubicRootsOfDamage,          this.CubicRootOfDamage :> obj)
                (Defs.Densities,                   this.Densities :> obj)
                (Defs.InternalEnergies,            this.InternalEnergies :> obj)
                (Defs.LocalStrains,                this.LocalStrains :> obj)
                (Defs.Masses,                      this.Masses :> obj)
                (Defs.MaterialTypes,               this.MaterialTypes :> obj)
                (Defs.NumbersOfActivatedFlaws,     this.NumberOfActivatedFlaws :> obj)
                (Defs.NumbersOfFlaws,              this.NumberOfFlaws :> obj)
                (Defs.NumberOfInteractionPartners, this.NumberOfInteractionPartners :> obj)
                (Defs.Pressures,                   this.Pressures :> obj)
                (Defs.Sigmas,                      this.Sigmas :> obj)
                (Defs.SmoothingLengths,            this.SmoothingLengths :> obj)
                (Defs.Velocities,                  this.Velocities :> obj)
            ]
            |> Map.ofList
            |> GenericChunk

        member this.SaveToStore store verbose =
            let config = 
                ImportConfig.Default
                  .WithStorage(store)
                  .WithRandomKey()
                  .WithVerbose(verbose)
                  .WithMaxDegreeOfParallelism(1)
                  .WithMinDist(0.0)
                  .WithOctreeSplitLimit(8192)
                  .WithNormalizePointDensityGlobal(false)
            let chunk = this.ToGenericChunk ()
            let pointcloud = PointCloud.Import(chunk, config)   
            pointcloud

        static member Serialize(data : HeraData) = data.Serialize()

        static member Deserialize(buffer : byte[]) =
            let d = Aardvark.Data.Codec.DeserializeAs<ImmutableDictionary<Durable.Def, obj>>(buffer)
            HeraData(d)

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
            |]

        let chunks = Ascii.Chunks(stream, -1L, lineDef, ParseConfig.Default).ToArray()
        let data = Chunk.ImmutableMerge(chunks)

        let ps = data.Positions.Map(fun p -> V3f p)
        //let vs = data.Velocities.ToArray()
        let struct (ns, ds) = ps.EstimateNormalsAndLocalDensity(16)

        let data = ImmutableDictionary<Durable.Def, obj>.Empty
                    .Add(Defs.Positions, ps)
                    .Add(Defs.EstimatedNormals, ns)
                    //.Add(Defs.Velocities, vs)
                    .Add(Defs.AverageSquaredDistances, ds)
        HeraData(data)

    let importHeraDataFromFile filename =
        use fs = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read)
        importHeraDataFromStream(fs)

    let importHeraDataFromFileFull filename =
        let particles = 
          File
            .ReadLines(filename)
            //.Take(50000)
            .AsParallel()
            .Select(fun line ->
                let ts = line.SplitOnWhitespace()
                {
                    Position                = V3f(float32 ts.[ 0], float32 ts.[1], float32 ts.[2])
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

        let ps = particles |> Array.map (fun p -> p.Position)
        let struct (ns, ds) = ps.EstimateNormalsAndLocalDensity(16)

        let data = 
          ImmutableDictionary<Durable.Def, obj>
            .Empty
            .Add(Defs.Positions,                    ps)
            .Add(Defs.EstimatedNormals,             ns)
            .Add(Defs.AverageSquaredDistances,      ds)
            .Add(Defs.Velocities,                   particles |> Array.map (fun p -> p.Velocity))
            .Add(Defs.Masses,                       particles |> Array.map (fun p -> p.Mass))
            .Add(Defs.Densities,                    particles |> Array.map (fun p -> p.Density))
            .Add(Defs.InternalEnergies,             particles |> Array.map (fun p -> p.InternalEnergy))
            .Add(Defs.SmoothingLengths,             particles |> Array.map (fun p -> p.SmoothingLength))
            .Add(Defs.NumberOfInteractionPartners,  particles |> Array.map (fun p -> p.NumberOfInteractionPartners))
            .Add(Defs.MaterialTypes,                particles |> Array.map (fun p -> p.MaterialType))
            .Add(Defs.NumbersOfFlaws,               particles |> Array.map (fun p -> p.NumberOfFlaws))
            .Add(Defs.NumbersOfActivatedFlaws,      particles |> Array.map (fun p -> p.NumberOfActivatedFlaws))
            .Add(Defs.CubicRootsOfDamage,           particles |> Array.map (fun p -> p.CubicRootOfDamage))
            .Add(Defs.LocalStrains,                 particles |> Array.map (fun p -> p.LocalStrain))
            .Add(Defs.Sigmas,                       particles |> Array.map (fun p -> p.Sigma))
            .Add(Defs.AlphaJutzi,                   particles |> Array.map (fun p -> p.AlphaJutzi))
            .Add(Defs.Pressures,                    particles |> Array.map (fun p -> p.Pressure))

        HeraData(data)
        
    let importHeraDataIntoStore datafile storepath verbose : string =

        Report.BeginTimed("importing " + datafile)

        Report.BeginTimed("parsing")
        let particles = importHeraDataFromFileFull datafile
        Report.EndTimed() |> ignore

        Report.BeginTimed("building octree")
        use store = (new SimpleDiskStore(storepath)).ToPointCloudStore()
        let pointcloud = particles.SaveToStore store verbose
        Report.EndTimed() |> ignore

        if verbose then
            printfn "pointcloud bounds  : %A" pointcloud.BoundingBox
            printfn "pointcloud.Id      : %s" pointcloud.Id
            printfn "pointcloud.Root.Id : %A" pointcloud.Root.Value.Id
        
        Report.EndTimed() |> ignore

        File.WriteAllText(storepath + ".key", pointcloud.Id)

        let root = pointcloud.Root.Value
        root.ForEachNode(true, fun n ->
            let isLeaf = if n.IsLeaf then "leaf" else "    "
            let pl3f    = if n.Has(Durable.Octree.PositionsLocal3f) then "PositionsLocal3f" else "                "
            let pl3fref = if n.Has(Durable.Octree.PositionsLocal3fReference) then "PositionsLocal3fReference" else "                         "
            printfn "node %A    %s    %s    %s" n.Id isLeaf pl3f pl3fref
        )

        pointcloud.Id

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




