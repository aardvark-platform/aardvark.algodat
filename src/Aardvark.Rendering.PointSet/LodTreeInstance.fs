namespace Aardvark.Rendering.PointSet

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Aardvark.Geometry
open Aardvark.Geometry.Points
open Aardvark.Data.Points
open Aardvark.Data.Points.Import
open Aardvark.Base
open Aardvark.Base.Incremental

module LodTreeInstance =

    module private AsciiFormatParser =
        open System.Text.RegularExpressions
        open Aardvark.Data.Points.Import

        let private tokens =
            LookupTable.lookupTable [
                "x", Ascii.Token.PositionX
                "y", Ascii.Token.PositionY
                "z", Ascii.Token.PositionZ
                "px", Ascii.Token.PositionX
                "py", Ascii.Token.PositionY
                "pz", Ascii.Token.PositionZ

                "r", Ascii.Token.ColorR
                "g", Ascii.Token.ColorG
                "b", Ascii.Token.ColorB
                "a", Ascii.Token.ColorA
                "rf", Ascii.Token.ColorRf
                "gf", Ascii.Token.ColorGf
                "bf", Ascii.Token.ColorBf
                "af", Ascii.Token.ColorAf

                "i", Ascii.Token.Intensity
                "_", Ascii.Token.Skip

                "nx", Ascii.Token.NormalX
                "ny", Ascii.Token.NormalY
                "nz", Ascii.Token.NormalZ
                
            ]
            
        let private token = Regex @"^(nx|ny|nz|px|py|pz|rf|gf|bf|af|x|y|z|r|g|b|a|i|_)[ \t]*"

        let private usage =
            "[Ascii] valid tokens: nx|ny|nz|px|py|pz|rf|gf|bf|af|x|y|z|r|g|b|a|i|_"


        let tryParseTokens (str : string) =
            let str = str.Trim()
            let rec parseTokens (start : int) (str : string) =
                if start >= str.Length then
                    Some []
                else
                    let m = token.Match(str, start, str.Length - start)
                    if m.Success then
                        let t = tokens m.Groups.[1].Value
                        match parseTokens (m.Index + m.Length) str with
                        | Some rest -> 
                            Some (t :: rest)
                        | None ->
                            None
                    else
                        None

            match parseTokens 0 str with
            | Some t -> List.toArray t |> Some
            | None -> None

        let parseTokens (str : string) =
            match tryParseTokens str with
            | Some t -> t
            | None ->
                failwith usage
        



    type PointTreeNode(pointCloudId : System.Guid, cache : LruDictionary<string, obj>, source : Symbol, globalTrafo : Similarity3d, root : Option<PointTreeNode>, parent : Option<PointTreeNode>, level : int, self : IPointCloudNode) as this =
        static let cmp = Func<float,float,int>(compare)
        
        let globalTrafoTrafo = Trafo3d globalTrafo
        let worldBounds = self.BoundingBoxExactGlobal  //  todo hackihack
        let worldCellBounds = self.Cell.BoundingBox //.BoundingBoxExactGlobal
        let localBounds = worldBounds.Transformed(globalTrafoTrafo)
        let localCellBounds = worldCellBounds.Transformed(globalTrafoTrafo)
        let cell = self.Cell
        let isLeaf = self.IsLeaf
        let id = self.Id
        let scale = globalTrafo.Scale

        //let mutable refCount = 0
        //let mutable livingChildren = 0
        let mutable children : Option<list<ILodTreeNode>> = None
 
        static let nodeId (n : IPointCloudNode) =
            string n.Id + "PointTreeNode"
            
        static let cacheId (n : IPointCloudNode) =
            string n.Id + "GeometryData"
            
        let getAverageDistance (original : V3f[]) (positions : V3f[]) (tree : PointRkdTreeF<_,_>) =
            let heap = List<float>(positions.Length)
            for i in 0 .. original.Length - 1 do
                let q = tree.CreateClosestToPointQuery(Single.PositiveInfinity, 25)
                let l = tree.GetClosest(q, original.[i])
                if l.Count > 1 then
                    let mutable minDist = Double.PositiveInfinity
                    for l in l do
                        let dist = V3f.Distance(positions.[int l.Index], positions.[i])
                        if dist > 0.0f then
                            minDist <- min (float dist) minDist
                    if not (Double.IsInfinity minDist) then
                        heap.HeapEnqueue(cmp, minDist)

            if heap.Count > 0 then
                let fstThrd = heap.Count / 3
                let real = heap.Count - 2 * heap.Count / 3
                for i in 1 .. fstThrd do heap.HeapDequeue(cmp) |> ignore

                let mutable sum = 0.0
                for i in 1 .. real do
                    sum <- sum + heap.HeapDequeue(cmp)
                    
                sum / float real
            elif original.Length > 2 then
                Log.error "empty heap (%d)" original.Length
                0.0
            else 
                0.0

        let load (ct : CancellationToken) (ips : MapExt<string, Type>) =
            cache.GetOrCreate(cacheId self, fun () ->
                let center = self.Center
                let attributes = SymbolDict<Array>()
                let mutable uniforms = MapExt.empty
                let mutable vertexSize = 0L

                let original =
                    if self.HasPositions then self.Positions.Value
                    else [| V3f(System.Single.NaN, System.Single.NaN, System.Single.NaN) |]
                    
                let globalTrafo1 = globalTrafo * Euclidean3d(Rot3d.Identity, center)
                let positions = 
                    let inline fix (p : V3f) = globalTrafo1.TransformPos (V3d p) |> V3f
                    original |> Array.map fix
                attributes.[DefaultSemantic.Positions] <- positions
                vertexSize <- vertexSize + 12L

                if MapExt.containsKey "Colors" ips then
                    let colors = 
                        if self.HasColors  then self.Colors.Value
                        else Array.create original.Length C4b.White
                    attributes.[DefaultSemantic.Colors] <- colors
                    vertexSize <- vertexSize + 4L
           
                if MapExt.containsKey "Normals" ips then
                    let normals = 
                        if self.HasNormals then self.Normals.Value
                        else Array.create original.Length V3f.OOO

                    let normals =
                        let normalMat = (Trafo3d globalTrafo.EuclideanTransformation.Rot).Backward.Transposed |> M33d.op_Explicit
                        let inline fix (p : V3f) = normalMat * (V3d p) |> V3f
                        normals |> Array.map fix

                    attributes.[DefaultSemantic.Normals] <- normals
                    vertexSize <- vertexSize + 12L


                
                if MapExt.containsKey "AvgPointDistance" ips then
                    let dist =
                        match self.HasPointDistanceAverage with
                        | true -> float self.PointDistanceAverage
                        | _ -> 0.0

                    let avgDist = 
                        //bounds.Size.NormMax / 40.0
                        if dist <= 0.0 then localBounds.Size.NormMax / 40.0 else dist

                    uniforms <- MapExt.add "AvgPointDistance" ([| float32 (scale * avgDist) |] :> System.Array) uniforms
                    
                if MapExt.containsKey "TreeLevel" ips then    
                    let arr = [| float32 level |] :> System.Array
                    uniforms <- MapExt.add "TreeLevel" arr uniforms
                    
                if MapExt.containsKey "MaxTreeDepth" ips then    
                    let depth = 
                        match self.HasMaxTreeDepth with
                            | true -> self.MaxTreeDepth
                            | _ -> 1
                    let arr = [| depth |] :> System.Array
                    uniforms <- MapExt.add "MaxTreeDepth" arr uniforms
                    
                if MapExt.containsKey "MinTreeDepth" ips then     
                    let depth = 
                        match self.HasMinTreeDepth with
                        | true -> self.MinTreeDepth
                        | _ -> 1
                    let arr = [| depth |] :> System.Array
                    uniforms <- MapExt.add "MinTreeDepth" arr uniforms

                if original.Length = 0 then
                    let geometry =
                        IndexedGeometry(
                            Mode = IndexedGeometryMode.PointList,
                            IndexedAttributes = 
                                SymDict.ofList [
                                    DefaultSemantic.Positions, [| V3f(System.Single.NaN, System.Single.NaN, System.Single.NaN) |] :> System.Array
                                    DefaultSemantic.Colors, [| C4b.White |] :> System.Array
                                    DefaultSemantic.Normals, [| V3f.OOI |] :> System.Array
                                ]
                        )
                    let mem = positions.LongLength * vertexSize
                    let res = geometry, uniforms
                    struct (res :> obj, mem)
                else
                    let geometry =
                        IndexedGeometry(
                            Mode = IndexedGeometryMode.PointList,
                            IndexedAttributes = attributes
                        )
                
                    let mem = positions.LongLength * vertexSize
                    let res = geometry, uniforms
                    struct (res :> obj, mem)
            )
            |> unbox<IndexedGeometry * MapExt<string, Array>>

        let angle (view : Trafo3d) =
            let cam = view.Backward.C3.XYZ

            let avgPointDistance = localBounds.Size.NormMax / 40.0

            let minDist = localBounds.GetMinimalDistanceTo(cam)
            let minDist = max 0.01 minDist

            let angle = Constant.DegreesPerRadian * atan2 avgPointDistance minDist

            let factor = 1.0 //(minDist / 0.01) ** 0.05

            angle / factor
        
        //member x.AcquireChild() =
        //    Interlocked.Increment(&livingChildren) |> ignore
    
        member x.ReleaseChildren() =
            let old = 
                lock x (fun () -> 
                    let c = children
                    children <- None
                    c
                )

            match old with
            | Some o -> o |> List.iter (fun o -> cache.Add(nodeId (unbox<PointTreeNode> o).Original, o, 1L <<< 10) |> ignore)
            | None -> ()

        member x.WithPointCloudNode(r : IPointCloudNode) =
            assert(level = 0 && Option.isNone parent)
            PointTreeNode(
                System.Guid.NewGuid(),
                cache,
                source,
                globalTrafo,
                None,
                None,
                0, 
                r
            )
        member x.Delete(b : Box3d) =
            let nodeFullyInside = Func<_,_>(fun (node : IPointCloudNode) -> b.Contains(node.Cell.BoundingBox))
            let nodeFullyOutside = Func<_,_>(fun (node : IPointCloudNode) -> not(b.Contains(node.Cell.BoundingBox)) && not(b.Intersects(node.Cell.BoundingBox)))
            let pointCountains = Func<_,_>(fun (v : V3d) -> b.Contains(v))
            let n = self.Delete(nodeFullyInside,nodeFullyOutside,pointCountains,self.Storage,CancellationToken.None)
            Log.line "Deleted"
            if isNull n then
                Log.error "Node is null, not deleting"
                this
            else
                PointTreeNode(System.Guid.NewGuid(), cache, source, globalTrafo, root, parent, level, n)

        member x.Acquire() =
            ()
            //if Interlocked.Increment(&refCount) = 1 then
            //    match parent with
            //    | Some p -> p.AcquireChild()
            //    | None -> ()




        member x.Release() =
            lock x (fun () ->
                match children with
                | Some cs -> 
                    for c in cs do (unbox<PointTreeNode> c).ReleaseChildren()
                | None ->
                    ()
            )
            

            //match parent with
            //| Some p -> 
            //    p.ReleaseChildren()
            //| None -> 
            //    ()
            //let destroy = Interlocked.Change(&refCount, fun o -> max 0 (o - 1), o = 1)
            //if destroy then
            //    livingChildren <- 0
            //    children <- None
            //    match parent with
            //    | Some p -> p.ReleaseChild()
            //    | None -> ()

        member x.Original : IPointCloudNode = self

        member x.Root : PointTreeNode =
            match root with
            | Some r -> r
            | None -> x

        member x.Children  =
            match children with
            | Some c -> c :> seq<_>
            | None ->
                lock x (fun () ->
                    match children with
                    | Some c -> c :> seq<_>
                    | None ->
                        let c = 
                            if isNull self.Subnodes then
                                []
                            else
                                self.Subnodes |> Seq.toList |> List.choose (fun c ->
                                    if isNull c then
                                        None
                                    else
                                        let c = c.Value
                                        if isNull c then
                                            None
                                        else
                                            let id = nodeId c
                                            match cache.TryGetValue id with
                                            | (true, n) ->
                                                cache.Remove id |> ignore
                                                unbox<ILodTreeNode> n |> Some
                                            | _ -> 
                                                //Log.warn "alloc %A" id
                                                PointTreeNode(pointCloudId, cache, source, globalTrafo, Some this.Root, Some this, level + 1, c) :> ILodTreeNode |> Some
                                )
                        children <- Some c
                        c :> seq<_>
                                    
                )
         

        member x.Id = id

        member x.GetData(ct, ips) = 
            load ct ips
            
        member x.ShouldSplit (splitfactor : float, quality : float, view : Trafo3d, proj : Trafo3d) =
            not isLeaf && angle view > splitfactor / quality

        member x.ShouldCollapse (splitfactor : float, quality : float, view : Trafo3d, proj : Trafo3d) =
            angle view < (splitfactor * 0.75) / quality
            
        member x.SplitQuality (splitfactor : float, view : Trafo3d, proj : Trafo3d) =
            splitfactor / angle view

        member x.CollapseQuality (splitfactor : float, view : Trafo3d, proj : Trafo3d) =
            (splitfactor * 0.75) / angle view

        member x.DataSource = source

        override x.ToString() = 
            sprintf "%s[%d]" (string x.Id) level

        interface ILodTreeNode with
            member x.Root = x.Root :> ILodTreeNode
            member x.Level = level
            member x.Name = x.ToString()
            member x.DataSource = source
            member x.Parent = parent |> Option.map (fun n -> n :> ILodTreeNode)
            member x.Children = x.Children 
            member x.ShouldSplit(s,q,v,p) = x.ShouldSplit(s,q,v,p)
            member x.ShouldCollapse(s,q,v,p) = x.ShouldCollapse(s,q,v,p)
            member x.SplitQuality(s,v,p) = x.SplitQuality(s,v,p)
            member x.CollapseQuality(s,v,p) = x.CollapseQuality(s,v,p)
            member x.DataSize = self.PointCountCell
            member x.TotalDataSize = int self.PointCountTree
            member x.GetData(ct, ips) = x.GetData(ct, ips)
            member x.WorldBoundingBox = worldBounds
            member x.WorldCellBoundingBox = worldCellBounds
            member x.Cell = cell
            member x.DataTrafo = globalTrafoTrafo.Inverse
            member x.Acquire() = x.Acquire()
            member x.Release() = x.Release()

        member x.PointCloudId = pointCloudId

        override x.GetHashCode() = 
            HashCode.Combine(pointCloudId.GetHashCode(), x.DataSource.GetHashCode(), self.Id.GetHashCode())

        override x.Equals o =
            match o with
                | :? PointTreeNode as o -> x.PointCloudId = o.PointCloudId && x.DataSource = o.DataSource && self.Id = o.Id
                | _ -> false
    
    module IndexedGeometry =
        module private Arr = 
            let append (l : Array) (r : Array) =
                let et = l.GetType().GetElementType()
                let res = Array.CreateInstance(et, l.Length + r.Length)
                l.CopyTo(res, 0)
                r.CopyTo(res, l.Length)
                res
            
            let concat (l : seq<Array>) =
                let l = Seq.toList l
                match l with
                | [] -> Array.CreateInstance(typeof<int>, 0)
                | [a] -> a
                | f :: _ ->
                    let len = l |> List.sumBy (fun a -> a.Length)
                    let et = f.GetType().GetElementType()
                    let res = Array.CreateInstance(et, len)
                    let mutable offset = 0
                    for a in l do
                        a.CopyTo(res, offset)
                        offset <- offset + a.Length
                    res

        let union (l : IndexedGeometry) (r : IndexedGeometry) =
            assert (l.Mode = r.Mode)
            assert (isNull l.IndexArray = isNull r.IndexArray)

            let index =
                if isNull l.IndexArray then null
                else Arr.append l.IndexArray r.IndexArray

            let atts =
                l.IndexedAttributes |> Seq.choose (fun (KeyValue(sem, l)) ->
                    match r.IndexedAttributes.TryGetValue(sem) with
                    | (true, r) -> Some (sem, Arr.append l r)
                    | _ -> None
                ) |> SymDict.ofSeq

            IndexedGeometry(
                Mode = l.Mode,
                IndexArray = index,
                IndexedAttributes = atts
            )

        let unionMany (s : seq<IndexedGeometry>) =
            use e = s.GetEnumerator()
            if e.MoveNext() then
                let mutable res = e.Current
                while e.MoveNext() do
                    res <- union res e.Current
                res

            else
                IndexedGeometry()

    type TreeViewNode(inner : ILodTreeNode, limit : int, parent : Option<ILodTreeNode>, root : Option<ILodTreeNode>) as this =
        let root = match root with | Some r -> r | None -> this :> ILodTreeNode

        let isLeaf = inner.TotalDataSize <= limit
                
        member x.ShouldSplit(s,q,v,p) =
            not isLeaf && inner.ShouldSplit(s,q,v,p)

        member x.GetData(ct : CancellationToken, ips : MapExt<string, Type>) =
            if isLeaf then
                let rec traverse (n : ILodTreeNode) =
                    match Seq.toList n.Children with
                    | [] -> [inner.GetData(ct, ips)]
                    | cs -> cs |> List.collect traverse

                let datas = traverse inner
                match datas with
                    | (_,u) :: _ ->
                        Log.warn "merge %d" (List.length datas)
                        let g = datas |> List.map fst |> IndexedGeometry.unionMany
                        g,u
                    | _ -> 
                        failwith ""
            else
                inner.GetData(ct, ips)
        
        interface ILodTreeNode with
            member x.DataTrafo = inner.DataTrafo
            member x.Root = root
            member x.Level = inner.Level
            member x.Name = inner.Name
            member x.DataSource = inner.DataSource
            member x.Parent = parent
            member x.Children = 
                if isLeaf then Seq.empty
                else inner.Children |> Seq.map (fun n -> TreeViewNode(n, limit, Some (this :> ILodTreeNode), Some root) :> ILodTreeNode)
            member x.ShouldSplit(s,q,v,p) = x.ShouldSplit(s,q,v,p)
            member x.ShouldCollapse(s,q,v,p) = inner.ShouldCollapse(s,q,v,p)
            member x.SplitQuality(s,v,p) = inner.SplitQuality(s,v,p)
            member x.CollapseQuality(s,v,p) = inner.CollapseQuality(s,v,p)
            member x.DataSize = if isLeaf then inner.TotalDataSize else inner.DataSize
            member x.TotalDataSize = inner.TotalDataSize
            member x.GetData(ct, ips) = x.GetData(ct, ips)
            member x.WorldBoundingBox = inner.WorldBoundingBox
            member x.WorldCellBoundingBox = inner.WorldCellBoundingBox
            member x.Cell = inner.Cell
            member x.Acquire() = inner.Acquire()
            member x.Release() = inner.Release()

    type InCoreStructureTree(inner : ILodTreeNode, parent : Option<ILodTreeNode>, root : Option<ILodTreeNode>) as this =
        let root = match root with | Some r -> r | None -> this :> ILodTreeNode
        let mutable children = [] //inner.Children |> Seq.toList |> List.map (fun n -> InCoreStructureTree(n, Some (this :> ILodTreeNode), Some root) :> ILodTreeNode)

        member private x.Build(nodeCount : ref<int>) =
            inc &nodeCount.contents
            children <- 
                inner.Children |> Seq.toList |> List.map (fun n -> 
                    let t = InCoreStructureTree(n, Some (this :> ILodTreeNode), Some root)
                    t.Build(nodeCount)
                    t :> ILodTreeNode
                )

        member x.Build() =
            let cnt = ref 0 
            x.Build(cnt)
            !cnt


        interface ILodTreeNode with
            member x.DataTrafo = inner.DataTrafo
            member x.Root = root
            member x.Level = inner.Level
            member x.Name = inner.Name
            member x.DataSource = inner.DataSource
            member x.Parent = parent
            member x.Children = children :> seq<_>
            member x.ShouldSplit(s,q,v,p) = inner.ShouldSplit(s,q,v,p)
            member x.ShouldCollapse(s,q,v,p) = inner.ShouldCollapse(s,q,v,p)
            member x.SplitQuality(s,v,p) = inner.SplitQuality(s,v,p)
            member x.CollapseQuality(s,v,p) = inner.CollapseQuality(s,v,p)
            member x.DataSize = inner.DataSize
            member x.TotalDataSize = inner.TotalDataSize
            member x.GetData(ct, ips) = inner.GetData(ct, ips)
            member x.WorldBoundingBox = inner.WorldBoundingBox
            member x.WorldCellBoundingBox = inner.WorldCellBoundingBox
            member x.Cell = inner.Cell
            member x.Acquire() = inner.Acquire()
            member x.Release() = inner.Release()

    let private loaders =
        [|
            "Aardvark.Data.Points.Import.Pts"
            "Aardvark.Data.Points.Import.E57"
            "Aardvark.Data.Points.Import.Yxh"
            "Aardvark.Data.Points.Import.Ply"
            "Aardvark.Data.Points.Import.Laszip"
        |]
    let mutable private loaded = false

    let private init() =
        lock loaders (fun () ->
            if not loaded then
                loaded <- true
                for l in loaders do
                    Type.GetType(l) |> ignore
        )



    let gc (input : string) (key : string) (output : string) =
        init()
        
        use output = PointCloud.OpenStore(output, LruDictionary(1L <<< 30))
        use input = PointCloud.OpenStore(input, LruDictionary(1L <<< 30))
        let set = input.GetPointSet(key)   
       
        let storeStructure (node : IPointCloudNode) =
            let queue = Queue<IPointCloudNode>()
            queue.Enqueue(node)

            let mutable i = 0
            while queue.Count > 0 do
                let n = queue.Dequeue()
                output.Add(string n.Id, n)

                if i % 100000 = 0 then
                    Log.line "%d nodes" i
                    output.Flush()

                if not (isNull n.Subnodes) then
                    for c in n.Subnodes do
                        if not (isNull c) then
                            let c = c.Value
                            if not (isNull c) then queue.Enqueue c

                i <- i + 1

        let storeAttributes (node : IPointCloudNode) =
            let queue = Queue<IPointCloudNode>()
            queue.Enqueue(node)
            
            let mutable i = 0
            while queue.Count > 0 do
                let n = queue.Dequeue()
                
                if n.HasPositions then output.Add(n.Positions.Id, n.Positions.Value)
                if n.HasNormals then output.Add(n.Normals.Id, n.Normals.Value)
                if n.HasColors then output.Add(n.Colors.Id, n.Colors.Value)
                if n.HasKdTree then output.Add(n.KdTree.Id, n.KdTree.Value.Data)
                if n.HasIntensities then output.Add(n.Intensities.Id, n.Intensities.Value)
                if n.HasClassifications then output.Add(n.Classifications.Id, n.Classifications.Value)
    
                if i % 1000 = 0 then
                    Log.line "%d datas" i
                    output.Flush()

                if not (isNull n.Subnodes) then
                    for c in n.Subnodes do
                        if not (isNull c) then
                            let c = c.Value
                            if not (isNull c) then queue.Enqueue c
                            
                i <- i + 1

        let root = set.Root.Value

        output.Add(key, set)
        storeStructure root
        storeAttributes root

    let withInCoreStructure (n : LodTreeInstance) =
        match n.root with
        | :? InCoreStructureTree -> n
        | _ -> 
            let root = InCoreStructureTree(n.root, None, None)
            let cnt = root.Build()
            Log.warn "loaded %d nodes" cnt
            { n with root = root }
        
    let withSplitLimit (limit : int) (n : LodTreeInstance) =
        { n with root = TreeViewNode(n.root, limit, None, None) }
        

    let private cache = LruDictionary(1L <<< 30)

    let private importAux (import : string -> Aardvark.Data.Points.ImportConfig -> PointSet) (sourceName : string) (file : string) (key : string) (store : string) (uniforms : list<string * IMod>) =
        init()

        let store = PointCloud.OpenStore(store, cache)
        
        let key = 
            if String.IsNullOrEmpty key then
                try
                    let test = store.GetByteArray("Index")
                    if isNull test then failwith "no key given"
                    else System.Text.Encoding.Unicode.GetString(test)
                with _ ->
                    failwith "no key given"
            else
                key
        
        let set = store.GetPointSet(key)

        let points = 
            if isNull set then
                Log.startTimed "import"
                let config = 
                    Aardvark.Data.Points.ImportConfig.Default
                        .WithStorage(store)
                        .WithKey(key)
                        .WithVerbose(true)
                        .WithMaxChunkPointCount(10000000)
                        //.WithEstimateKdNormals(Func<_,_,_>(fun (t : PointRkdTreeD<_,_>) (a : V3f[]) -> a.EstimateNormals(t, 5)))
        
                let res = import file config
                store.Add("Index", System.Text.Encoding.Unicode.GetBytes key)

                store.Flush()
                Log.stop()
                res
            else
                set

        let root = points.Root.Value
        let bounds = root.Cell.BoundingBox
        //let c = V3d root.CentroidLocal + root.Center
        //let filter = FilterInsideBox3d(Box3d.FromCenterAndSize(c, float root.CentroidLocalStdDev * V3d.III))
            
        //let root = FilteredNode.Create(root,filter)

        //let query = root.QueryPointsNearPlane(Plane3d(V3d.OOI,c), 1.0) |> Seq.toList
        //printfn "%A" query

        let trafo = Similarity3d(1.0, Euclidean3d(Rot3d.Identity, -bounds.Center))
        let source = Symbol.Create sourceName
        let root = PointTreeNode(System.Guid.NewGuid(), store.Cache, source, trafo, None, None, 0, root) :> ILodTreeNode

        let uniforms = MapExt.ofList uniforms
        let uniforms = MapExt.add "Scales" (Mod.constant 1.0 :> IMod) uniforms
        

        { 
            root = root
            uniforms = uniforms
        }
        
    /// imports a file into the given store (guessing the format by extension)
    let import (sourceName : string) (file : string) (store : string) (uniforms : list<string * IMod>) =
        let key = System.IO.Path.GetFileNameWithoutExtension(file).ToLower()
        importAux (fun file cfg -> PointCloud.Import(file, cfg)) sourceName file key store uniforms

    /// imports an ASCII file into the given store
    /// valid tokens: nx|ny|nz|px|py|pz|rf|gf|bf|af|x|y|z|r|g|b|a|i|_
    let importAscii (sourceName : string) (fmt : string) (file : string) (store : string) (uniforms : list<string * IMod>) =
        let fmt = AsciiFormatParser.parseTokens fmt
        let key = System.IO.Path.GetFileNameWithoutExtension(file).ToLower()
        let import (file : string) (cfg : ImportConfig) =
            let chunks = Ascii.Chunks(file, fmt, cfg.ParseConfig)
            PointCloud.Chunks(chunks, cfg)

        importAux import sourceName file key store uniforms
        
    /// loads the specified key from the store
    let load (sourceName : string) (key : string) (store : string) (uniforms : list<string * IMod>) =
        let import (file : string) (cfg : ImportConfig) =
            failwithf "key not found: %A" key

        importAux import sourceName key key store uniforms



    let ofPointSet (uniforms : list<string * IMod>) (set : PointSet) =
        let store = set.Storage
        let root = PointTreeNode(System.Guid.NewGuid(), store.Cache, Symbol.CreateNewGuid(), Similarity3d.Identity, None, None, 0, set.Root.Value) :> ILodTreeNode
        let uniforms = MapExt.ofList uniforms
        { 
            root = root
            uniforms = uniforms
        }
        
    let ofPointCloudNode (uniforms : list<string * IMod>) (root : IPointCloudNode) =
        let store = root.Storage
        let root = PointTreeNode(System.Guid.NewGuid(), store.Cache, Symbol.CreateNewGuid(), Similarity3d.Identity, None, None, 0, root) :> ILodTreeNode
        let uniforms = MapExt.ofList uniforms
        { 
            root = root
            uniforms = uniforms
        }
    let filter (filter : IFilter) (i : LodTreeInstance) =
        match i.root with
        | :? PointTreeNode as node ->
            let o = node.Original
            let n = FilteredNode.Create(o, filter)
            { i with root = node.WithPointCloudNode(n) }
        | _ ->
            failwith "not implemented"


    let normalizeTo (box : Box3d) (instance : LodTreeInstance) =
        let tree = instance.root

        let bounds = tree.WorldBoundingBox
        let scale = (box.Size / bounds.Size).NormMin
        let t = 
            Trafo3d.Translation(box.Center - bounds.Center) * 
            Trafo3d.Scale scale

        let uniforms = instance.uniforms
        let uniforms = MapExt.add "Scales" (Mod.constant scale :> IMod) uniforms
        let uniforms = MapExt.add "ModelTrafo" (Mod.constant t :> IMod) uniforms


        {
            root = tree
            uniforms = uniforms
        }

    let normalize (maxSize : float) (instance : LodTreeInstance) =
        normalizeTo (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * maxSize)) instance
    
    let trafo (t : IMod<Trafo3d>) (instance : LodTreeInstance) =
        let uniforms = instance.uniforms

        let inline getScale (m : M44d) =
            let sx = m * V4d.IOOO |> Vec.length
            let sy = m * V4d.OIOO |> Vec.length
            let sz = m * V4d.OOIO |> Vec.length
            (sx + sy + sz) / 3.0

        let uniforms =
            match MapExt.tryFind "ModelTrafo" uniforms with
            | Some (:? IMod<Trafo3d> as old) -> 
                let n = Mod.map2 (*) old t
                MapExt.add "ModelTrafo" (n :> IMod) uniforms
            | _ ->
                MapExt.add "ModelTrafo" (t :> IMod) uniforms

        let uniforms =
            match MapExt.tryFind "Scales" uniforms with
            | Some (:? IMod<float> as sOld) -> 
                let sNew = t |> Mod.map (fun o -> getScale o.Forward)
                let sFin = Mod.map2 (*) sOld sNew
                MapExt.add "Scales" (sFin :> IMod) uniforms
            | _ ->
                let sNew = t |> Mod.map (fun o -> getScale o.Forward)
                MapExt.add "Scales" (sNew :> IMod) uniforms
                
        { instance with uniforms = uniforms }
        
    let translate (shift : V3d) (instance : LodTreeInstance) =
        trafo (Mod.constant (Trafo3d.Translation(shift))) instance
        
    let scale (scale : float) (instance : LodTreeInstance) =
        trafo (Mod.constant (Trafo3d.Scale(scale))) instance
        
    let transform (t : Trafo3d) (instance : LodTreeInstance) =
        trafo (Mod.constant t) instance

[<Flags>]
type PointVisualization =
    | None          = 0x000000
    | Color         = 0x000001
    | Normals       = 0x000002
    | White         = 0x000004
    | Lighting      = 0x000100
    | OverlayLod    = 0x001000
    | Antialias     = 0x002000
    | FancyPoints   = 0x004000

[<ReflectedDefinition>]
module PointSetShaders =
    open FShade
    open Aardvark.Base.Rendering


    let constantColor (c : C4f) (v : Effects.Vertex) =
        let c = c.ToV4d()
        vertex {
            return { v with c = c }
        }


    let heatMapColors =
        let fromInt (i : int) =
            C4b(
                byte ((i >>> 16) &&& 0xFF),
                byte ((i >>> 8) &&& 0xFF),
                byte (i &&& 0xFF),
                255uy
            ).ToC4f().ToV4d()

        Array.map fromInt [|
            0x1639fa
            0x2050fa
            0x3275fb
            0x459afa
            0x55bdfb
            0x67e1fc
            0x72f9f4
            0x72f8d3
            0x72f7ad
            0x71f787
            0x71f55f
            0x70f538
            0x74f530
            0x86f631
            0x9ff633
            0xbbf735
            0xd9f938
            0xf7fa3b
            0xfae238
            0xf4be31
            0xf29c2d
            0xee7627
            0xec5223
            0xeb3b22
        |]

    let heat (tc : float) =
        let tc = clamp 0.0 1.0 tc
        let fid = tc * float heatMapColors.Length - 0.5

        let id = int (floor fid)
        if id < 0 then 
            heatMapColors.[0]
        elif id >= heatMapColors.Length - 1 then
            heatMapColors.[heatMapColors.Length - 1]
        else
            let c0 = heatMapColors.[id]
            let c1 = heatMapColors.[id + 1]
            let t = fid - float id
            (c0 * (1.0 - t) + c1 * t)


    type UniformScope with
        member x.MagicExp : float = x?MagicExp
        member x.PointVisualization : PointVisualization = x?PointVisualization
        member x.Overlay : float[] = x?StorageBuffer?Overlay
        member x.ModelTrafos : M44d[] = x?StorageBuffer?ModelTrafos
        member x.ModelViewTrafos : M44d[] = x?StorageBuffer?ModelViewTrafos
        member x.Scales : float[] = x?StorageBuffer?Scales

    type Vertex =
        {
            [<Position>] pos : V4d
            [<Normal>] n : V3d
            [<Semantic("Offsets")>] offset : V3d
        }


    let offset ( v : Vertex) =
        vertex {
            return  { v with pos = v.pos + V4d(v.offset, 0.0)}
        }
        
    
    type PointVertex =
        {
            [<Position>] pos : V4d
            [<Color; Interpolation(InterpolationMode.Flat)>] col : V4d
            //[<Normal>] n : V3d
            [<Semantic("ViewCenter"); Interpolation(InterpolationMode.Flat)>] vc : V3d
            [<Semantic("ViewPosition")>] vp : V3d
            [<Semantic("AvgPointDistance")>] dist : float
            [<Semantic("DepthRange"); Interpolation(InterpolationMode.Flat)>] depthRange : float
            [<PointSize>] s : float
            [<Semantic("PointPixelSize")>] ps : float
            [<PointCoord>] c : V2d
            [<Normal>] n : V4d
            [<Semantic("TreeId")>] id : int
            [<Semantic("MaxTreeDepth")>] treeDepth : int
            [<FragCoord>] fc : V4d
            [<SamplePosition>] sp : V2d
        }


    let flipNormal (n : V3d) =
        let n = Vec.normalize n


        //let a = Vec.dot V3d.IOO n |> abs
        //let b = Vec.dot V3d.OIO n |> abs
        //let c = Vec.dot V3d.OOI n |> abs

        0.5 * (n + V3d.III)

        //let mutable n = n
        //let x = n.X
        //let y = n.Y
        //let z = n.Z


        //let a = abs (atan2 y x)
        //let b = abs (atan2 z x)
        //let c = abs (atan2 z y)

        //let a = min a (Constant.Pi - a) / Constant.PiHalf |> clamp 0.0 1.0
        //let b = min b (Constant.Pi - b) / Constant.PiHalf |> clamp 0.0 1.0
        //let c = min c (Constant.Pi - c) / Constant.PiHalf |> clamp 0.0 1.0

        //let a = sqrt (1.0 - a*a)
        //let b = sqrt (1.0 - b*b)
        //let c = sqrt (1.0 - c*c)


        //let a = a + Constant.Pi
        //let b = b + Constant.Pi
        //let c = c + Constant.Pi
        



        

        //V3d(a,b,c)
        //if x > y then
        //    if z > x then
        //        n <- n / n.Z
        //    else
        //        n <- n / n.X
        //else
        //    if z > y then 
        //        n <- n / n.Z
        //    else
        //        n <- n / n.Y

        //0.5 * (n + V3d.III)


        //let n = Vec.normalize n
        //let theta = asin n.Z
        //let phi = atan (abs (n.Y / n.X))
        
        //let theta =
        //    theta + Constant.PiHalf
        //V3d(phi / Constant.PiHalf,theta / Constant.Pi, 1.0)
        //if x > y then
        //    if z > x then
        //        if n.Z < 0.0 then -n
        //        else n
        //    else
        //        if n.X < 0.0 then -n
        //        else n
        //else
        //    if z > y then 
        //        if n.Z < 0.0 then -n
        //        else n
        //    else
        //        if n.Y < 0.0 then -n
        //        else n


    let getNdcPointRadius (vp : V4d) (dist : float) =
        let ppx = uniform.ProjTrafo * (vp + V4d(0.5 * dist, 0.0, 0.0, 0.0))
        let ppy = uniform.ProjTrafo * (vp + V4d(0.0, 0.5 * dist, 0.0, 0.0))
        let ppz = uniform.ProjTrafo * vp

        let ppz = ppz.XYZ / ppz.W
        let d1 = ppx.XYZ / ppx.W - ppz |> Vec.length
        let d2 = ppy.XYZ / ppy.W - ppz |> Vec.length
        0.5 * (d1 + d2)
        
    let div (v : V4d) = v.XYZ / v.W

    

    let lodPointSize (v : PointVertex) =
        vertex { 
            let mv = uniform.ModelViewTrafos.[v.id]
            //let f = if magic then 0.07 else 1.0 / 0.3

            let vp = mv * v.pos
            let pp = div (uniform.ProjTrafo * vp)

            let scale = uniform.Scales.[v.id] 
            let dist = v.dist * scale * 8.0

            let dist = getNdcPointRadius vp dist
            let r0 = 0.0390625 

            let f = abs (dist / r0)
            let r = if f > 0.0001 then 0.3 * r0 * f ** (0.35 * uniform.MagicExp) else dist

            let vpx = uniform.ProjTrafoInv * (V4d(pp.X + r, pp.Y, pp.Z, 1.0)) |> div
            let vpy = uniform.ProjTrafoInv * (V4d(pp.X, pp.Y + r, pp.Z, 1.0)) |> div
            let dist = 0.5 * (Vec.length (vpx - vp.XYZ) + Vec.length (vpy - vp.XYZ))

            let vpz = vp + V4d(0.0, 0.0, 0.5*dist, 0.0)
            let ppx = uniform.ProjTrafo * (vpz + V4d(0.5 * dist, 0.0, 0.0, 0.0))
            let ppy = uniform.ProjTrafo * (vpz + V4d(0.0, 0.5 * dist, 0.0, 0.0))
            let fpp = uniform.ProjTrafo * vpz
            let opp = uniform.ProjTrafo * vp
            
            let pp0 = opp.XYZ / opp.W
            let ppz = fpp.XYZ / fpp.W
            let d1 = ppx.XYZ / ppx.W - ppz |> Vec.length
            let d2 = ppy.XYZ / ppy.W - ppz |> Vec.length
            
            let ndcDist = 0.5 * (d1 + d2)
            let depthRange = abs (pp0.Z - ppz.Z)

            let pixelDist = 
                ndcDist * float uniform.ViewportSize.X * uniform.PointSize
            
            let pixelDist = 
                if ppz.Z < -1.0 then -1.0
                else pixelDist
                


            let col =
                if uniform.PointVisualization &&& PointVisualization.Color <> PointVisualization.None then
                    v.col.XYZ
                else
                    v.n.XYZ * 0.5 + 0.5

            let o = uniform.Overlay.[v.id]
            let h = heat (float v.treeDepth / 6.0)
            let col =
                if uniform.PointVisualization &&& PointVisualization.OverlayLod <> PointVisualization.None then
                    o * h.XYZ + (1.0 - o) * col
                else
                    col

            let pixelDist = 
                if uniform.PointVisualization &&& PointVisualization.Antialias <> PointVisualization.None then pixelDist + 1.0
                else pixelDist

            //let pp = V4d(pp.X, pp.Y, pp.Z + depthRange * pp.W, pp.W)

            //let pixelDist = 
            //    if pixelDist > 30.0 then -1.0
            //    else pixelDist //min pixelDist 30.0

            return { v with ps = 5.0; s = pixelDist; pos = fpp; depthRange = depthRange; vp = vpz.XYZ; vc = vpz.XYZ; col = V4d(col, v.col.W) }
        }



    let lodPointSize2 (v : PointVertex) =
        vertex { 
            let mv = uniform.ModelViewTrafos.[v.id]
            //let f = if magic then 0.07 else 1.0 / 0.3

            let vp = mv * v.pos
            let pp = div (uniform.ProjTrafo * vp)

            let pixelSize = uniform.Scales.[v.id]  * uniform.PointSize
            let ndcRadius = pixelSize / V2d uniform.ViewportSize

            let vpx = uniform.ProjTrafoInv * (V4d(pp.X + ndcRadius.X, pp.Y, pp.Z, 1.0)) |> div
            let vpy = uniform.ProjTrafoInv * (V4d(pp.X, pp.Y + ndcRadius.Y, pp.Z, 1.0)) |> div
            let dist = 0.5 * (Vec.length (vpx - vp.XYZ) + Vec.length (vpy - vp.XYZ))

            let vpz = vp + V4d(0.0, 0.0, 0.5*dist, 0.0)
            let fpp = uniform.ProjTrafo * vpz
            let opp = uniform.ProjTrafo * vp
        
            let pp0 = opp.XYZ / opp.W
            let ppz = fpp.XYZ / fpp.W
        
            let depthRange = abs (pp0.Z - ppz.Z)

            let pixelSize = 
                if ppz.Z < -1.0 then -1.0
                else pixelSize
            
            let col =
                if uniform.PointVisualization &&& PointVisualization.Color <> PointVisualization.None then
                    v.col.XYZ
                else
                    V3d.III

            let o = uniform.Overlay.[v.id]
            let h = heat (float v.treeDepth / 6.0)
            let col =
                if uniform.PointVisualization &&& PointVisualization.OverlayLod <> PointVisualization.None then
                    o * h.XYZ + (1.0 - o) * col
                else
                    col

            let pixelSize = 
                if uniform.PointVisualization &&& PointVisualization.Antialias <> PointVisualization.None then pixelSize + 1.0
                else pixelSize

            return { v with ps = float (int pixelSize); s = pixelSize; pos = fpp; depthRange = depthRange; vp = vpz.XYZ; vc = vpz.XYZ; col = V4d(col, v.col.W) }
        }



    type Fragment =
        {
            [<Color>] c : V4d
            [<Depth(DepthWriteMode.OnlyGreater)>] d : float
        }
        
    let lodPointCircularMSAA (v : PointVertex) =
        fragment {
            let mutable cc = v.c
            if uniform.PointVisualization &&& PointVisualization.Antialias <> PointVisualization.None then
                cc <- (v.c * v.ps + 2.0 * v.sp - V2d.II) / v.ps

            let c = cc * 2.0 - V2d.II
            let f = Vec.dot c c - 1.0
            if f > 0.0 then discard()
            
            let t = 1.0 - sqrt (-f)
            let depth = v.fc.Z
            let outDepth = depth + v.depthRange * t
            
            let mutable alpha = v.col.W
            let mutable color = v.col.XYZ
            
            if uniform.PointVisualization &&& PointVisualization.FancyPoints <> PointVisualization.None then
                let vd = heat(v.ps / 8.0).XYZ
                color <- 0.5 * (vd + V3d.III)
                
            if uniform.PointVisualization &&& PointVisualization.Lighting <> PointVisualization.None then
                let diffuse = sqrt -f
                color <- color * diffuse

            return { c = V4d(color, alpha); d = outDepth }
        }

    let lodPointCircular (v : PointVertex) =
        fragment {
            let mutable cc = v.c
            let c = v.c * 2.0 - V2d.II
            let f = Vec.dot c c - 1.0
            if f > 0.0 then discard()
            
            let t = 1.0 - sqrt (-f)
            let depth = v.fc.Z
            let outDepth = depth + v.depthRange * t
            
            let mutable alpha = v.col.W
            let mutable color = v.col.XYZ

            if uniform.PointVisualization &&& PointVisualization.Antialias <> PointVisualization.None then
                let dx = ddx(v.c) * 2.0
                let dy = ddy(v.c) * 2.0
                let dfx = 2.0*c.X*dx.X + 2.0*c.Y*dx.Y
                let dfy = 2.0*c.X*dy.X + 2.0*c.Y*dy.Y
                let d = abs f / sqrt (dfx * dfx + dfy * dfy)
                alpha <- min 1.0 (d / 2.0)
                
            if uniform.PointVisualization &&& PointVisualization.FancyPoints <> PointVisualization.None then
                let vd = heat(v.ps / 8.0).XYZ
                color <- 0.5 * (vd + V3d.III)
                
            if uniform.PointVisualization &&& PointVisualization.Lighting <> PointVisualization.None then
                let c = 
                    if uniform.PointVisualization &&& PointVisualization.Antialias <> PointVisualization.None then
                        c * (1.0 + 2.0 / v.ps)
                    else
                        c

                let f = Vec.dot c c
                let diffuse = sqrt (max 0.0 (1.0 - f))
                color <- color * diffuse

            return { c = V4d(color, alpha); d = outDepth }
        }

    //let cameraLight (v : PointVertex) =
    //    fragment {
    //        let mutable color = v.col.XYZ

    //        if uniform.PointVisualization &&& PointVisualization.FancyPoints <> PointVisualization.None then
    //            let vd = heat(v.ps / 8.0).XYZ
    //            color <- 0.5 * (vd + V3d.III)

    //        if uniform.PointVisualization &&& PointVisualization.Lighting <> PointVisualization.None then
                
    //            let mutable c = v.c * V2d(2.0, 2.0) - V2d.II
                
    //            if uniform.PointVisualization &&& PointVisualization.Antialias <> PointVisualization.None then
    //                c <- c * (1.0 + 2.0 / v.ps)
    //            let f = Vec.dot c c
    //            let z = sqrt (max 0.0 (1.0 - f))
    //            //let sn = V3d(c.X, c.Y, -z)
                
    //            let dSphere = z //Vec.dot sn vd |> abs
    //            //let dPlane = Vec.dot vn vd |> abs

    //            //let t = lvn
    //            //let pp : float = uniform?Planeness
                
    //            //let t = 
    //            //    if pp < 0.01 then 0.0
    //            //    else 1.0 - (1.0 - t) ** pp
 
    //            let diffuse = dSphere //(1.0 - t) * dSphere + t * dPlane
    //            color <- color * diffuse

    //        return V4d(color, v.col.W)
    //    }

    let env =
        samplerCube {
            texture uniform?EnvMap
            filter Filter.MinMagMipLinear
        }
    let envMap (v : PointVertex) =
        fragment {
            let c = v.c * V2d(2.0, 2.0) - V2d.II
            let f = Vec.dot c c
            let z = sqrt (max 0.0 (1.0 - f))        
            let vn = V3d(c.X, -c.Y, z)

            let wn = uniform.ViewTrafoInv * V4d(vn, 0.0) |> Vec.xyz |> Vec.normalize
            let d = uniform.ViewTrafoInv * V4d(v.vp, 0.0) |> Vec.xyz |> Vec.normalize
            //let cp = uniform.ViewTrafoInv.C3.XYZ
            
            let rc = env.Sample(Vec.reflect d wn).XYZ
            let tc = env.Sample(Vec.refract d wn 0.9).XYZ

            let ec = 0.8 * tc + 0.05 * rc + 0.15 * v.col.XYZ
            
            return V4d(ec, v.col.W)
        }

    let normalColor ( v : Vertex) =
        fragment {
            let mutable n = Vec.normalize v.n
            if n.Z < 0.0 then n <- -n

            let n = (n + V3d.III) * 0.5
            return V4d(n, 1.0)
        }
