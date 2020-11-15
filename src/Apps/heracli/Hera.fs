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

        let private def (id : string) (name : string) (description : string) (t : Durable.Def) (isArray : bool) = 
            Durable.Def(new Guid(id), name, description, t.Id, isArray)

        let ParticleSet                 = def "3c65f19c-50fa-4183-9f82-411a60ed8039" "Hera.ParticleSet"
                                              "Set of Hera particles."
                                              Durable.Primitives.DurableMap false

        let Position                    = def "e613884f-4c4d-448d-a4bf-7d22f7da7d49" "Hera.Particle.Position"
                                              "Spatial position of SPH particle."
                                              Durable.Aardvark.V3f false
        let PositionArray               = def "3fcec3fa-282d-4255-b541-94c6d4f8a14c" "Hera.Particle.PositionArray"
                                              "Hera.Particle.Position array."
                                              Durable.Aardvark.V3fArray true

        let EstimatedNormal             = def "b11e3d42-bc5f-459c-8776-1efbef00853c" "Hera.Particle.EstimatedNormal"
                                              "Estimated normal vector (from k-nearest neighbours positions) of SPH particle."
                                              Durable.Aardvark.V3f false
        let EstimatedNormalArray        = def "b2bb7961-0422-4f1c-8d64-0d4bf049083d" "Hera.Particle.EstimatedNormalArray"
                                              "Hera.Particle.EstimatedNormal array."
                                              Durable.Aardvark.V3fArray true

        let Velocity                    = def "71bd64d9-b18b-44a6-b52f-bcbc811005fe" "Hera.Particle.Velocity"
                                              "Velocity of SPH particle."
                                              Durable.Aardvark.V3f false
        let VelocityArray               = def "2b2c08af-d81a-4d2e-a174-74b49ea231f3" "Hera.Particle.VelocityArray"
                                              "Hera.Particle.Velocity array."
                                              Durable.Aardvark.V3fArray true
        
        let Mass                        = def "aea3be45-708e-4657-8b89-7c888630c7a9" "Hera.Particle.Mass"
                                              "Mass of SPH particle."
                                              Durable.Primitives.Float32 false
        let MassArray                   = def "8b47ae79-aacc-4f72-9652-cf60d6ffe948" "Hera.Particle.MassArray"
                                              "Hera.Particle.Mass array."
                                              Durable.Primitives.Float32Array true
        
        let Density                     = def "7654dbb4-09d6-4b1b-ba20-c9d12a2895e4" "Hera.Particle.Density"
                                              "Density of SPH particle."
                                              Durable.Primitives.Float32 false
        let DensityArray                = def "f9da3db2-3e3e-4d62-97e7-c46e0d979be9" "Hera.Particle.DensityArray"
                                              "Hera.Particle.Density array."
                                              Durable.Primitives.Float32Array true
        
        let InternalEnergy              = def "df469fa5-5ff8-45be-917a-d19bc31655f2" "Hera.Particle.InternalEnergy"
                                              "Internal energy of SPH particle."
                                              Durable.Primitives.Float32 false
        let InternalEnergyArray         = def "47a77854-8397-410f-aa41-58a5ce7695e7" "Hera.Particle.InternalEnergyArray"
                                              "Hera.Particle.InternalEnergy array."
                                              Durable.Primitives.Float32Array true
        
        let SmoothingLength             = def "8cec485b-1d82-4ba8-b198-7b3635cd4fe5" "Hera.Particle.SmoothingLength"
                                              "Smoothing length (always the same)."
                                              Durable.Primitives.Float32 false
        let SmoothingLengthArray        = def "b5e0c915-34c4-4bab-ae48-f43726d72726" "Hera.Particle.SmoothingLengthArray"
                                              "Hera.Particle.SmoothingLength array."
                                              Durable.Primitives.Float32Array true
        
        let NumberOfInteractionPartners = def "025a2ad0-19cf-4ee2-90a3-2aca8924aa80" "Hera.Particle.NumberOfInteractionPartners"
                                              "Number of interaction partners of SPH particle."
                                              Durable.Primitives.Int32 false
        let NumberOfInteractionPartnersArray =
                                          def "6cb36a49-b3ac-4c94-9869-7f0eb09950ac" "Hera.Particle.NumberOfInteractionPartnersArray"
                                              "Hera.Particle.NumberOfInteractionPartners array."
                                              Durable.Primitives.Int32Array true
        
        let MaterialType                = def "d7e8a38f-d406-4970-b51e-934938d1ba98" "Hera.Particle.MaterialType"
                                              "Material type of SPH particle."
                                              Durable.Primitives.Int32 false
        let MaterialTypeArray           = def "1da0927e-6b72-4a47-8d89-c58d8876e954" "Hera.Particle.MaterialTypeArray"
                                              "Hera.Particle.MaterialType array."
                                              Durable.Primitives.Int32Array true
        
        let NumberOfFlaws               = def "97dd2bef-5664-4755-9ab3-88568be093b5" "Hera.Particle.NumberOfFlaws"
                                              "Number of flaws of SPH particle (Grady-Kipp damage model)."
                                              Durable.Primitives.Int32 false
        let NumberOfFlawsArray          = def "467008f9-2ad8-4aa3-a6ff-cb412b59e70d" "Hera.Particle.NumberOfFlawsArray"
                                              "Hera.Particle.NumberOfFlaws array."
                                              Durable.Primitives.Int32Array true
        
        let NumberOfActivatedFlaws      = def "c7c8e397-3f8c-4df0-b803-518c413fb552" "Hera.Particle.NumberOfActivatedFlaws"
                                              "Number of activated flaws of SPH particle."
                                              Durable.Primitives.Int32 false
        let NumberOfActivatedFlawsArray = def "4833966e-a2ec-4f6a-94f8-76fbebb9d97f" "Hera.Particle.NumberOfActivatedFlawsArray"
                                              "Hera.Particle.NumberOfActivatedFlaws array."
                                              Durable.Primitives.Int32Array true
        
        let CubicRootOfDamage           = def "49254ad8-9e3e-4427-ad38-d1d0fcda7512" "Hera.Particle.CubicRootOfDamage"
                                              "Cubic root of damage of SPH particle."
                                              Durable.Primitives.Float32 false
        let CubicRootOfDamageArray      = def "0fb53221-5bd7-4caa-8c72-b9f0cc7b8d9c" "Hera.Particle.CubicRootOfDamageArray"
                                              "Hera.Particle.CubicRootOfDamage array."
                                              Durable.Primitives.Float32Array true
        
        let LocalStrain                 = def "7c4957d1-42d1-411d-9ccd-6db89acab579" "Hera.Particle.LocalStrain"
                                              "Local_strain of SPH particle."
                                              Durable.Primitives.Float32 false
        let LocalStrainArray            = def "993162f5-ad29-45af-a653-b57dc8ee31a1" "Hera.Particle.LocalStrainArray"
                                              "Hera.Particle.LocalStrain array."
                                              Durable.Primitives.Float32Array true
        
        let Sigma                       = def "c37db1d5-21b8-4264-97e7-fd17bb051296" "Hera.Particle.Sigma"
                                              "Stress tensor component of SPH particle."
                                              Durable.Aardvark.M33f false
        let SigmaArray                  = def "9c3c64a7-cb96-45a3-aa6b-ab6da3316128" "Hera.Particle.SigmaArray"
                                              "Hera.Particle.Sigma array."
                                              Durable.Aardvark.M33fArray true
        
        let AlphaJutzi                  = def "4934c779-38ac-472c-a0ba-4edb3bebef1e" "Hera.Particle.AlphaJutzi"
                                              "alpha_jutzi = density_compact / density_porous of SPH particle."
                                              Durable.Primitives.Float32 false
        let AlphaJutziArray             = def "4cc0437b-a096-4794-ba24-5175fdd54095" "Hera.Particle.AlphaJutziArray"
                                              "Hera.Particle.AlphaJutzi array."
                                              Durable.Primitives.Float32Array true
        
        let Pressure                    = def "33258bed-9d43-4d86-89bf-9f38cabd3640" "Hera.Particle.Pressure"
                                              "pressure 28->28+number of flaws:activation thresholds for this particle."
                                              Durable.Primitives.Float32 false
        let PressureArray               = def "1cb212a4-d33f-4f02-b38d-8cf69eafb6dc" "Hera.Particle.PressureArray"
                                              "Hera.Particle.Pressure array."
                                              Durable.Primitives.Float32Array true

        let AverageSquaredDistance      = def "5859a470-976b-49fd-9133-eab9d53d52d3" "Hera.Particle.AverageSquaredDistance"
                                              "Average squared distance of k-nearest points to their centroid."
                                              Durable.Primitives.Float32 false
        let AverageSquaredDistanceArray = def "23354127-f93f-4216-a0af-b26f29e6e8fa" "Hera.Particle.AverageSquaredDistanceArray"
                                              "Hera.Particle.AverageSquaredDistance array."
                                              Durable.Primitives.Float32Array true

    let lineDef = [|
        Ascii.Token.PositionX; Ascii.Token.PositionY; Ascii.Token.PositionZ
        Ascii.Token.VelocityX; Ascii.Token.VelocityY; Ascii.Token.VelocityZ
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

    type HeraData(data : IDictionary<Durable.Def, obj>) =

        let data = Dictionary<_,_>(data)
        do
            // backwards compatibility
            let update olddef newdef = match data.TryGetValue(olddef) with | true, xs -> data.Add(newdef, xs) | _ -> ()
            update Durable.Octree.PositionsLocal3f Defs.PositionArray
            update Durable.Octree.Normals3f Defs.EstimatedNormalArray
            update Durable.Octree.Velocities3f Defs.VelocityArray
            update Durable.Octree.Densities1f Defs.AverageSquaredDistanceArray



            let (hasPositions, ps) = data.TryGetValue(Defs.PositionArray)
            if not hasPositions then failwith "Data must contain Defs.PositionArray."
            let ps = ps :?> V3f[]

            let (hasNormals, ns) = data.TryGetValue(Defs.EstimatedNormalArray)
            if not hasNormals then failwith "Data must contain Defs.EstimatedNormalArray."
            let ns = ns :?> V3f[]

            let (hasVelocities, vs) = data.TryGetValue(Defs.VelocityArray)
            if not hasVelocities then failwith "Data must contain Hera.Defs.VelocityArray."
            let vs = vs :?> V3f[]

            if ps.Length <> ns.Length || ps.Length <> vs.Length then 
                failwith "All arrays must be of same length."

        member this.Positions                   with get() = data.[Defs.PositionArray]                      :?> V3f[]
        member this.EstimatedNormals            with get() = data.[Defs.EstimatedNormalArray]               :?> V3f[]
        member this.AverageSquaredDistances     with get() = data.[Defs.AverageSquaredDistanceArray]        :?> float32[]
        member this.Velocities                  with get() = data.[Defs.VelocityArray]                      :?> V3f[]
        member this.Masses                      with get() = data.[Defs.MassArray]                          :?> float32[]
        member this.Densities                   with get() = data.[Defs.DensityArray]                       :?> float32[]
        member this.InternalEnergies            with get() = data.[Defs.InternalEnergyArray]                :?> float32[]
        member this.SmoothingLengths            with get() = data.[Defs.SmoothingLengthArray]               :?> float32[]
        member this.NumberOfInteractionPartners with get() = data.[Defs.NumberOfInteractionPartnersArray]   :?> int32[]
        member this.MaterialTypes               with get() = data.[Defs.MaterialTypeArray]                  :?> int32[]
        member this.NumberOfFlaws               with get() = data.[Defs.NumberOfFlawsArray]                 :?> int32[]
        member this.NumberOfActivatedFlaws      with get() = data.[Defs.NumberOfActivatedFlawsArray]        :?> int32[]
        member this.CubicRootOfDamage           with get() = data.[Defs.CubicRootOfDamageArray]             :?> float32[]
        member this.LocalStrains                with get() = data.[Defs.LocalStrainArray]                   :?> float32[]
        member this.Sigmas                      with get() = data.[Defs.SigmaArray]                         :?> M33f[]
        member this.AlphaJutzi                  with get() = data.[Defs.AlphaJutziArray]                    :?> float32[]
        member this.Pressures                   with get() = data.[Defs.PressureArray]                      :?> float32[]

        member this.Count                       with get() = this.Positions.Length

        member this.CheckNormals () =
            for n in this.EstimatedNormals do
                if not (n.Length.ApproximateEquals(1.0f, 0.001f)) then
                    printfn "[unstable normal] %A" n

        member this.Serialize() =
            data.DurableEncode(Defs.ParticleSet, false)

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
            Ascii.Token.VelocityX; Ascii.Token.VelocityY; Ascii.Token.VelocityZ
            |]

        let chunks = Ascii.Chunks(stream, -1L, lineDef, ParseConfig.Default).ToArray()
        let data = Chunk.ImmutableMerge(chunks)

        let ps = data.Positions.Map(fun p -> V3f p)
        let vs = data.Velocities.ToArray()
        let struct (ns, ds) = ps.EstimateNormalsAndLocalDensity(16)

        let data = ImmutableDictionary<Durable.Def, obj>.Empty
                    .Add(Defs.PositionArray, ps)
                    .Add(Defs.EstimatedNormalArray, ns)
                    .Add(Defs.VelocityArray, vs)
                    .Add(Defs.AverageSquaredDistanceArray, ds)
        HeraData(data)

    let importHeraDataFromFile filename =
        use fs = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read)
        importHeraDataFromStream(fs)

    let importHeraDataFromFileFull filename =
        let particles = 
          File
            .ReadLines(filename)
            //.Take(100)
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
            .Add(Defs.PositionArray,                    ps)
            .Add(Defs.EstimatedNormalArray,             ns)
            .Add(Defs.AverageSquaredDistanceArray,      ds)
            .Add(Defs.VelocityArray,                    particles |> Array.map (fun p -> p.Velocity))
            .Add(Defs.MassArray,                        particles |> Array.map (fun p -> p.Mass))
            .Add(Defs.DensityArray,                     particles |> Array.map (fun p -> p.Density))
            .Add(Defs.InternalEnergyArray,              particles |> Array.map (fun p -> p.InternalEnergy))
            .Add(Defs.SmoothingLengthArray,             particles |> Array.map (fun p -> p.SmoothingLength))
            .Add(Defs.NumberOfInteractionPartnersArray, particles |> Array.map (fun p -> p.NumberOfInteractionPartners))
            .Add(Defs.MaterialTypeArray,                particles |> Array.map (fun p -> p.MaterialType))
            .Add(Defs.NumberOfFlawsArray,               particles |> Array.map (fun p -> p.NumberOfFlaws))
            .Add(Defs.NumberOfActivatedFlawsArray,      particles |> Array.map (fun p -> p.NumberOfActivatedFlaws))
            .Add(Defs.CubicRootOfDamageArray,           particles |> Array.map (fun p -> p.CubicRootOfDamage))
            .Add(Defs.LocalStrainArray,                 particles |> Array.map (fun p -> p.LocalStrain))
            .Add(Defs.SigmaArray,                       particles |> Array.map (fun p -> p.Sigma))
            .Add(Defs.AlphaJutziArray,                  particles |> Array.map (fun p -> p.AlphaJutzi))
            .Add(Defs.PressureArray,                    particles |> Array.map (fun p -> p.Pressure))

        HeraData(data)
        
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




