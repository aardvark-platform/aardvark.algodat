namespace Aardvark.Data.Potree

open System
open System.IO
open System.Text.Json
open System.Buffers.Binary
open System.Collections.Generic
open System.Collections.Concurrent
open System.Linq
open System.Threading.Tasks
open Aardvark.Base
open Aardvark.Geometry
open Aardvark.Geometry.Points
open Aardvark.Data.Points

// ============================================================================
// PotreeMetaData - JSON deserialization types and helpers
// ============================================================================

type PotreeHierarchy = {
    firstChunkSize: int
    stepSize: int
    depth: int
}

type PotreeBoundingBox = {
    min: float[]
    max: float[]
}

type PotreeAttribute = {
    name: string
    description: string
    size: int
    numElements: int
    elementSize: int
    ``type``: string
    min: JsonElement[]
    max: JsonElement[]
    scale: JsonElement[]
    offset: JsonElement[]
    histogram: int64[]
}

type PotreeData = {
    version: string
    name: string
    description: string
    points: float
    projection: string
    hierarchy: PotreeHierarchy
    offset: float[]
    scale: float[]
    spacing: float
    boundingBox: PotreeBoundingBox
    encoding: string
    attributes: PotreeAttribute[]
}

module PotreeMetaData =

    let rec jsonElementToObject (element: JsonElement) : obj =
        match element.ValueKind with
        | JsonValueKind.Object ->
            let obj = Dictionary<string, obj>()
            for property in element.EnumerateObject() do
                obj.[property.Name] <- jsonElementToObject property.Value
            obj :> obj
        | JsonValueKind.Array ->
            let list = ResizeArray<obj>()
            for item in element.EnumerateArray() do
                list.Add(jsonElementToObject item)
            list :> obj
        | JsonValueKind.String ->
            element.GetString() :> obj
        | JsonValueKind.Number ->
            match element.TryGetInt32() with
            | true, l -> l :> obj
            | _ ->
                match element.TryGetDouble() with
                | true, d -> d :> obj
                | _ -> element.GetRawText() :> obj
        | JsonValueKind.True -> true :> obj
        | JsonValueKind.False -> false :> obj
        | JsonValueKind.Null -> null
        | JsonValueKind.Undefined -> null
        | _ -> null

    let tryDeserialize (filePath: string) : PotreeData option =
        try
            let json = File.ReadAllText(filePath)
            let options = JsonSerializerOptions(PropertyNameCaseInsensitive = true)
            let data = JsonSerializer.Deserialize<PotreeData>(json, options)
            Some data
        with ex ->
            Console.WriteLine(ex.ToString())
            None

// ============================================================================
// PotreeOctree - Point attributes and octree structure
// ============================================================================

module PointAttributeNames =
    [<Literal>]
    let POSITION_CARTESIAN = "POSITION_CARTESIAN"
    [<Literal>]
    let COLOR_PACKED = "COLOR_PACKED"
    [<Literal>]
    let NORMAL_FLOATS = "NORMAL_FLOATS"
    [<Literal>]
    let INTENSITY = "INTENSITY"
    [<Literal>]
    let CLASSIFICATION = "CLASSIFICATION"
    [<Literal>]
    let NORMAL_SPHEREMAPPED = "NORMAL_SPHEREMAPPED"
    [<Literal>]
    let NORMAL_OCT16 = "NORMAL_OCT16"
    [<Literal>]
    let NORMAL = "NORMAL"
    [<Literal>]
    let RETURN_NUMBER = "RETURN_NUMBER"
    [<Literal>]
    let NUMBER_OF_RETURNS = "NUMBER_OF_RETURNS"
    [<Literal>]
    let SOURCE_ID = "SOURCE_ID"
    [<Literal>]
    let INDICES = "INDICES"
    [<Literal>]
    let SPACING = "SPACING"
    [<Literal>]
    let GPS_TIME = "GPS_TIME"

module PointAttributeTypes =
    let DATA_TYPE_DOUBLE = ("double", 8)
    let DATA_TYPE_FLOAT = ("float", 4)
    let DATA_TYPE_INT8 = ("int8", 1)
    let DATA_TYPE_UINT8 = ("uint8", 1)
    let DATA_TYPE_INT16 = ("int16", 2)
    let DATA_TYPE_UINT16 = ("uint16", 2)
    let DATA_TYPE_INT32 = ("int32", 4)
    let DATA_TYPE_UINT32 = ("uint32", 4)
    let DATA_TYPE_INT64 = ("int64", 8)
    let DATA_TYPE_UINT64 = ("uint64", 8)

type PointAttribute(name: string, typeInfo: string * int, numElements: int) =
    member val Name = name with get
    member val NumElements = numElements with get
    member val ByteSize = numElements * snd typeInfo with get
    member val Range: obj[] = [||] with get, set
    member val InitialRange: obj[] = [||] with get, set

type PointAttributeVector(name: string, sourceNames: string[]) =
    member val Name = name with get
    member val AttributeSourceNames = sourceNames with get

type PointAttributes(jsonAttributes: PotreeAttribute[]) =
    let typenameTypeattributeMap =
        dict [
            "double", PointAttributeTypes.DATA_TYPE_DOUBLE
            "float", PointAttributeTypes.DATA_TYPE_FLOAT
            "int8", PointAttributeTypes.DATA_TYPE_INT8
            "uint8", PointAttributeTypes.DATA_TYPE_UINT8
            "int16", PointAttributeTypes.DATA_TYPE_INT16
            "uint16", PointAttributeTypes.DATA_TYPE_UINT16
            "int32", PointAttributeTypes.DATA_TYPE_INT32
            "uint32", PointAttributeTypes.DATA_TYPE_UINT32
            "int64", PointAttributeTypes.DATA_TYPE_INT64
            "uint64", PointAttributeTypes.DATA_TYPE_UINT64
        ]

    let replacements = dict [ "rgb", "rgba" ]

    let mutable byteSize = 0
    let attributes = ResizeArray<PointAttribute>()
    let attributeVectors = ResizeArray<PointAttributeVector>()

    do
        for jsonAttribute in jsonAttributes do
            let name = jsonAttribute.name
            let numElements = jsonAttribute.numElements
            let min = jsonAttribute.min
            let max = jsonAttribute.max
            let typeInfo = typenameTypeattributeMap.[jsonAttribute.``type``]

            let potreeAttributeName =
                match replacements.TryGetValue(name) with
                | true, v -> v
                | _ -> name

            let attribute = PointAttribute(potreeAttributeName, typeInfo, numElements)

            if numElements = 1 then
                attribute.Range <- [| PotreeMetaData.jsonElementToObject min.[0]; PotreeMetaData.jsonElementToObject max.[0] |]
            else
                attribute.Range <- [|
                    min |> Array.map PotreeMetaData.jsonElementToObject :> obj
                    max |> Array.map PotreeMetaData.jsonElementToObject :> obj
                |]

            if name = "gps-time" then
                if attribute.Range.[0] = attribute.Range.[1] then
                    attribute.Range.[1] <- (unbox<int> attribute.Range.[1]) + 1 :> obj

            attribute.InitialRange <- attribute.Range

            attributes.Add(attribute)
            byteSize <- byteSize + attribute.ByteSize

        // Check for normals
        let hasNormals =
            attributes |> Seq.exists (fun a -> a.Name = "NormalX") &&
            attributes |> Seq.exists (fun a -> a.Name = "NormalY") &&
            attributes |> Seq.exists (fun a -> a.Name = "NormalZ")

        if hasNormals then
            attributeVectors.Add(PointAttributeVector("NORMAL", [| "NormalX"; "NormalY"; "NormalZ" |]))

    member _.ByteSize = byteSize
    member _.Size = attributes.Count
    member _.Attributes = attributes :> IList<PointAttribute>
    member _.AttributeVectors = attributeVectors :> IList<PointAttributeVector>

    member _.Add(attribute: PointAttribute) =
        attributes.Add(attribute)
        byteSize <- byteSize + attribute.ByteSize

    member _.AddVector(vector: PointAttributeVector) =
        attributeVectors.Add(vector)

    member _.HasColors() =
        attributes |> Seq.exists (fun attr -> attr.Name = PointAttributeNames.COLOR_PACKED)

    member _.HasNormals() =
        attributes |> Seq.exists (fun attr ->
            attr.Name = PointAttributeNames.NORMAL ||
            attr.Name = PointAttributeNames.NORMAL_FLOATS ||
            attr.Name = PointAttributeNames.NORMAL_OCT16 ||
            attr.Name = PointAttributeNames.NORMAL_SPHEREMAPPED)

    // Static attribute instances
    static member val POSITION_CARTESIAN = PointAttribute(PointAttributeNames.POSITION_CARTESIAN, PointAttributeTypes.DATA_TYPE_FLOAT, 3)
    static member val RGBA_PACKED = PointAttribute(PointAttributeNames.COLOR_PACKED, PointAttributeTypes.DATA_TYPE_INT8, 4)
    static member val RGB_PACKED = PointAttribute(PointAttributeNames.COLOR_PACKED, PointAttributeTypes.DATA_TYPE_INT8, 3)
    static member val NORMAL_FLOATS = PointAttribute(PointAttributeNames.NORMAL_FLOATS, PointAttributeTypes.DATA_TYPE_FLOAT, 3)
    static member val INTENSITY = PointAttribute(PointAttributeNames.INTENSITY, PointAttributeTypes.DATA_TYPE_UINT16, 1)
    static member val CLASSIFICATION = PointAttribute(PointAttributeNames.CLASSIFICATION, PointAttributeTypes.DATA_TYPE_UINT8, 1)
    static member val NORMAL_SPHEREMAPPED = PointAttribute(PointAttributeNames.NORMAL_SPHEREMAPPED, PointAttributeTypes.DATA_TYPE_UINT8, 2)
    static member val NORMAL_OCT16 = PointAttribute(PointAttributeNames.NORMAL_OCT16, PointAttributeTypes.DATA_TYPE_UINT8, 2)
    static member val NORMAL = PointAttribute(PointAttributeNames.NORMAL, PointAttributeTypes.DATA_TYPE_FLOAT, 3)
    static member val RETURN_NUMBER = PointAttribute(PointAttributeNames.RETURN_NUMBER, PointAttributeTypes.DATA_TYPE_UINT8, 1)
    static member val NUMBER_OF_RETURNS = PointAttribute(PointAttributeNames.NUMBER_OF_RETURNS, PointAttributeTypes.DATA_TYPE_UINT8, 1)
    static member val SOURCE_ID = PointAttribute(PointAttributeNames.SOURCE_ID, PointAttributeTypes.DATA_TYPE_UINT16, 1)
    static member val INDICES = PointAttribute(PointAttributeNames.INDICES, PointAttributeTypes.DATA_TYPE_UINT32, 1)
    static member val SPACING = PointAttribute(PointAttributeNames.SPACING, PointAttributeTypes.DATA_TYPE_FLOAT, 1)
    static member val GPS_TIME = PointAttribute(PointAttributeNames.GPS_TIME, PointAttributeTypes.DATA_TYPE_DOUBLE, 1)

// ============================================================================
// PotreeNode - Octree node structure
// ============================================================================

type PotreeOctree() =
    member val Url: string = null with get, set
    member val Scale: float[] = null with get, set
    member val Offset: float[] = null with get, set
    member val Root: PotreeNode = Unchecked.defaultof<_> with get, set
    member val Attributes: PointAttributes = Unchecked.defaultof<_> with get, set

and [<AllowNullLiteral>] PotreeNode(name: string, octreeRoot: PotreeOctree, bbox: Box3d) =
    let id = Guid.NewGuid()
    let children = Dictionary<int, PotreeNode>()

    member _.Id = id
    member val Name = name with get, set
    member val NodeType = 0 with get, set
    member _.HasChildren = children.Count > 0
    member val ByteOffset = 0L with get, set
    member val ByteSize = 0L with get, set
    member _.OctreeRoot = octreeRoot
    member _.BoundingBox = bbox
    member _.Children = children
    member val NumPoints = 0L with get, set
    member val Parent: PotreeNode = null with get, set

// ============================================================================
// PotreePointNode - IPointNode implementation
// ============================================================================

type PotreePointNode(id: string, parent: PotreePointNode, node: PotreeNode, storage: PotreeStorage) =
    let mutable positions: V3d[] = [||]
    let mutable colors: C4b[] option = None
    let mutable intensities: int[] option = None
    let mutable classifications: byte[] option = None

    let getPointsFromParentParallelSIMD (inputPoints: V3d[]) (bounds: Box3d) =
        let childIndex = int (node.Name.[node.Name.Length - 1]) - int '0'

        let vecCompX, compX =
            if childIndex = 4 || childIndex = 5 || childIndex = 6 || childIndex = 7 then
                (fun (a: System.Numerics.Vector<float>) (b: System.Numerics.Vector<float>) -> System.Numerics.Vector.LessThanOrEqual(a, b)), (fun a b -> a <= b)
            else
                (fun (a: System.Numerics.Vector<float>) (b: System.Numerics.Vector<float>) -> System.Numerics.Vector.LessThan(a, b)), (fun a b -> a < b)

        let vecCompY, compY =
            if childIndex = 2 || childIndex = 3 || childIndex = 6 || childIndex = 7 then
                (fun (a: System.Numerics.Vector<float>) (b: System.Numerics.Vector<float>) -> System.Numerics.Vector.LessThanOrEqual(a, b)), (fun a b -> a <= b)
            else
                (fun (a: System.Numerics.Vector<float>) (b: System.Numerics.Vector<float>) -> System.Numerics.Vector.LessThan(a, b)), (fun a b -> a < b)

        let vecCompZ, compZ =
            if childIndex = 1 || childIndex = 3 || childIndex = 5 || childIndex = 7 then
                (fun (a: System.Numerics.Vector<float>) (b: System.Numerics.Vector<float>) -> System.Numerics.Vector.LessThanOrEqual(a, b)), (fun a b -> a <= b)
            else
                (fun (a: System.Numerics.Vector<float>) (b: System.Numerics.Vector<float>) -> System.Numerics.Vector.LessThan(a, b)), (fun a b -> a < b)

        let count = inputPoints.Length
        let xs = Array.zeroCreate<float> count
        let ys = Array.zeroCreate<float> count
        let zs = Array.zeroCreate<float> count

        for i = 0 to inputPoints.Length - 1 do
            xs.[i] <- inputPoints.[i].X
            ys.[i] <- inputPoints.[i].Y
            zs.[i] <- inputPoints.[i].Z

        let vectorSize = System.Numerics.Vector<float>.Count
        let minX = System.Numerics.Vector<float>(bounds.Min.X)
        let minY = System.Numerics.Vector<float>(bounds.Min.Y)
        let minZ = System.Numerics.Vector<float>(bounds.Min.Z)
        let maxX = System.Numerics.Vector<float>(bounds.Max.X)
        let maxY = System.Numerics.Vector<float>(bounds.Max.Y)
        let maxZ = System.Numerics.Vector<float>(bounds.Max.Z)

        let resultBag = ConcurrentBag<ResizeArray<V3d * int>>()

        let coreCount = Environment.ProcessorCount
        let minChunkSize = System.Numerics.Vector<float>.Count * 4
        let maxChunkSize = 65536
        let estimatedChunkSize = max minChunkSize (count / (coreCount * 4))
        let chunkSize = min (max estimatedChunkSize minChunkSize) maxChunkSize
        let chunkCount = (count + chunkSize - 1) / chunkSize

        Parallel.For(0, chunkCount, fun chunkIndex ->
            let chunkStart = chunkIndex * chunkSize
            let chunkEnd = min (chunkStart + chunkSize) count
            let local = ResizeArray<V3d * int>(chunkSize / 2)
            let simdEnd = chunkEnd - ((chunkEnd - chunkStart) % vectorSize)

            // SIMD section
            let mutable i = chunkStart
            while i < simdEnd do
                let vx = System.Numerics.Vector<float>(xs, i)
                let vy = System.Numerics.Vector<float>(ys, i)
                let vz = System.Numerics.Vector<float>(zs, i)

                let mask =
                    System.Numerics.Vector.GreaterThanOrEqual(vx, minX) &&& (vecCompX vx maxX) &&&
                    System.Numerics.Vector.GreaterThanOrEqual(vy, minY) &&& (vecCompY vy maxY) &&&
                    System.Numerics.Vector.GreaterThanOrEqual(vz, minZ) &&& (vecCompZ vz maxZ)

                for j = 0 to vectorSize - 1 do
                    if mask.[j] <> 0L then
                        let idx = i + j
                        local.Add((V3d(xs.[idx], ys.[idx], zs.[idx]), idx))

                i <- i + vectorSize

            // Scalar remainder
            for i = simdEnd to chunkEnd - 1 do
                let p = V3d(xs.[i], ys.[i], zs.[i])
                if p.X >= bounds.Min.X && compX p.X bounds.Max.X &&
                   p.Y >= bounds.Min.Y && compY p.Y bounds.Max.Y &&
                   p.Z >= bounds.Min.Z && compZ p.Z bounds.Max.Z then
                    local.Add((p, i))

            if local.Count > 0 then
                resultBag.Add(local)
        ) |> ignore

        let totalCount = resultBag.Sum(fun l -> l.Count)
        let result = Array.zeroCreate<V3d * int> totalCount
        let mutable index = 0
        for list in resultBag do
            list.CopyTo(result, index)
            index <- index + list.Count
        result

    member _.Id = id
    member _.Parent = parent
    member _.PotreeNodeData = node
    member _.PotreeStorage = storage

    member _.CellBounds = node.BoundingBox
    member _.DataBounds = node.BoundingBox
    member _.Positions = positions

    member this.KdTree =
        let pts = this.Positions
        if pts.Length > 0 then
            let origin = pts.[0]
            let tree = pts |> Array.map (fun p -> V3f(p - origin)) |> fun arr -> arr.BuildKdTree()
            Some (PointKdTree(tree, origin))
        else
            None

    member this.Children =
        node.Children
        |> Seq.choose (fun kvp ->
            match storage.GetPointCloudNode(kvp.Value, this) with
            | null -> None
            | n -> Some n)
        |> Seq.toArray

    member this.Fill(buffer: byte[] byref) =
        let bufferSpan = buffer.AsSpan()
        let bytesPerPoint = node.OctreeRoot.Attributes.ByteSize
        let mutable attributeOffset = 0

        let scale = node.OctreeRoot.Scale
        let scaleX, scaleY, scaleZ = scale.[0], scale.[1], scale.[2]

        let offset = node.OctreeRoot.Offset
        let offsetX, offsetY, offsetZ = offset.[0], offset.[1], offset.[2]

        for pointAttribute in node.OctreeRoot.Attributes.Attributes do
            match pointAttribute.Name with
            | "position" ->
                positions <- Array.zeroCreate<V3d> (int node.NumPoints)
                for j = 0 to int node.NumPoints - 1 do
                    let pointOffset = j * bytesPerPoint + attributeOffset
                    let xRaw = BinaryPrimitives.ReadInt32LittleEndian(bufferSpan.Slice(pointOffset, 4))
                    let yRaw = BinaryPrimitives.ReadInt32LittleEndian(bufferSpan.Slice(pointOffset + 4, 4))
                    let zRaw = BinaryPrimitives.ReadInt32LittleEndian(bufferSpan.Slice(pointOffset + 8, 4))
                    let x = float xRaw * scaleX + offsetX
                    let y = float yRaw * scaleY + offsetY
                    let z = float zRaw * scaleZ + offsetZ
                    positions.[j] <- V3d(x, y, z)

            | "rgba" ->
                let cols = Array.zeroCreate<C4b> (int node.NumPoints)
                for j = 0 to int node.NumPoints - 1 do
                    let pointOffset = j * bytesPerPoint + attributeOffset
                    let rRaw = BinaryPrimitives.ReadUInt16LittleEndian(bufferSpan.Slice(pointOffset, 2))
                    let gRaw = BinaryPrimitives.ReadUInt16LittleEndian(bufferSpan.Slice(pointOffset + 2, 2))
                    let bRaw = BinaryPrimitives.ReadUInt16LittleEndian(bufferSpan.Slice(pointOffset + 4, 2))
                    let r = if rRaw > 255us then int rRaw / 256 else int rRaw
                    let g = if gRaw > 255us then int gRaw / 256 else int gRaw
                    let b = if bRaw > 255us then int bRaw / 256 else int bRaw
                    cols.[j] <- C4b(r, g, b)
                colors <- Some cols

            | "intensity" ->
                let ints = Array.zeroCreate<int> (int node.NumPoints)
                for j = 0 to int node.NumPoints - 1 do
                    let pointOffset = j * bytesPerPoint + attributeOffset
                    ints.[j] <- int (BinaryPrimitives.ReadUInt16LittleEndian(bufferSpan.Slice(pointOffset, 2)))
                intensities <- Some ints

            | "classification" ->
                let cls = Array.zeroCreate<byte> (int node.NumPoints)
                for j = 0 to int node.NumPoints - 1 do
                    let pointOffset = j * bytesPerPoint + attributeOffset
                    cls.[j] <- bufferSpan.[pointOffset]
                classifications <- Some cls

            | _ -> ()

            attributeOffset <- attributeOffset + pointAttribute.ByteSize

        // Add parent infos to current node
        if not (isNull (box parent)) then
            let parentPositions = parent.Positions
            let parentData = getPointsFromParentParallelSIMD parentPositions node.BoundingBox

            let mergeParentData (currentData: 'T[]) (parentValueSelector: int -> 'T) =
                let merged = Array.zeroCreate (currentData.Length + parentData.Length)
                Array.Copy(currentData, merged, currentData.Length)
                Parallel.For(0, parentData.Length, fun i ->
                    merged.[currentData.Length + i] <- parentValueSelector i
                ) |> ignore
                merged

            positions <- mergeParentData positions (fun i -> fst parentData.[i])

            match parent.TryGetAttribute(PointNodeAttributes.Colors) with
            | true, (:? (C4b[]) as parentColors) ->
                match colors with
                | Some c -> colors <- Some (mergeParentData c (fun i -> parentColors.[snd parentData.[i]]))
                | None -> ()
            | _ -> ()

            match parent.TryGetAttribute(PointNodeAttributes.Intensities) with
            | true, (:? (int[]) as parentIntensities) ->
                match intensities with
                | Some ints -> intensities <- Some (mergeParentData ints (fun i -> parentIntensities.[snd parentData.[i]]))
                | None -> ()
            | _ -> ()

            match parent.TryGetAttribute(PointNodeAttributes.Classifications) with
            | true, (:? (byte[]) as parentClassifications) ->
                match classifications with
                | Some cls -> classifications <- Some (mergeParentData cls (fun i -> parentClassifications.[snd parentData.[i]]))
                | None -> ()
            | _ -> ()

    member this.TryGetAttribute(name: Symbol) : bool * Array =
        if name = PointNodeAttributes.Positions then
            true, this.Positions :> Array
        elif name = PointNodeAttributes.Colors then
            match colors with
            | Some c -> true, c :> Array
            | None -> false, null
        elif name = PointNodeAttributes.Intensities then
            match intensities with
            | Some i -> true, i :> Array
            | None -> false, null
        elif name = PointNodeAttributes.Classifications then
            match classifications with
            | Some c -> true, c :> Array
            | None -> false, null
        else
            false, null

    interface IPointNode with
        member this.Id = this.Id
        member this.CellBounds = this.CellBounds
        member this.DataBounds = this.DataBounds
        member this.Positions = this.Positions
        member this.KdTree =
            match this.KdTree with
            | Some kt -> kt
            | None -> Unchecked.defaultof<_>
        member this.Children = this.Children |> Array.map (fun c -> c :> IPointNode)
        member this.TryGetAttribute(name, data) =
            let success, arr = this.TryGetAttribute(name)
            data <- arr
            success

// ============================================================================
// PotreeStorage - Storage and loading
// ============================================================================

and PotreeStorage(storePath: string, cache: LruDictionary<string, obj>) =
    let mutable dataFileStream: FileStream = null
    let mutable metaData: PotreeData = Unchecked.defaultof<_>

    let readByteRange (fs: FileStream) (offset: int64) (count: int64) =
        if offset >= fs.Length then
            raise (ArgumentOutOfRangeException("offset", "Offset is outside the file bounds."))

        fs.Seek(offset, SeekOrigin.Begin) |> ignore
        use reader = new BinaryReader(fs, System.Text.Encoding.Default, true)
        let safeCount = min count (fs.Length - offset)
        reader.ReadBytes(int safeCount)

    let createChildAABB (aabb: Box3d) (index: int) =
        let mutable min = aabb.Min
        let mutable max = aabb.Max
        let halfSize = V3d(aabb.Size.X * 0.5, aabb.Size.Y * 0.5, aabb.Size.Z * 0.5)

        if (index &&& 0b0001) <> 0 then min.Z <- min.Z + halfSize.Z
        else max.Z <- max.Z - halfSize.Z

        if (index &&& 0b0010) <> 0 then min.Y <- min.Y + halfSize.Y
        else max.Y <- max.Y - halfSize.Y

        if (index &&& 0b0100) <> 0 then min.X <- min.X + halfSize.X
        else max.X <- max.X - halfSize.X

        Box3d(min, max)

    let rec loadHierarchyRecursive (root: PotreeNode) (data: byte[] byref) (offset: int64) (size: int64) =
        let bytesPerNode = 22
        let numNodes = int (size / int64 bytesPerNode)
        let nodes = ResizeArray<PotreeNode>(numNodes)
        nodes.Add(root)

        for i = 0 to numNodes - 1 do
            let currentNode = nodes.[i]
            let currentOffset = int (offset + int64 (i * bytesPerNode))
            let nodeSpan = data.AsSpan(currentOffset, bytesPerNode)

            let nodeType = nodeSpan.[0]
            let childMask = nodeSpan.[1]
            let numPoints = BinaryPrimitives.ReadUInt32LittleEndian(nodeSpan.Slice(2, 4))
            let byteOffset = BinaryPrimitives.ReadInt64LittleEndian(nodeSpan.Slice(6, 8))
            let byteSize = BinaryPrimitives.ReadInt64LittleEndian(nodeSpan.Slice(14, 8))

            currentNode.NodeType <- int nodeType
            currentNode.NumPoints <- int64 numPoints
            currentNode.ByteOffset <- byteOffset
            currentNode.ByteSize <- byteSize

            if currentNode.NodeType = 2 then
                loadHierarchyRecursive currentNode &data byteOffset byteSize
            else
                for childIndex = 0 to 7 do
                    if (int childMask &&& (1 <<< childIndex)) <> 0 then
                        let childName = currentNode.Name + string (char (int '0' + childIndex))
                        let childAABB = createChildAABB currentNode.BoundingBox childIndex
                        let child = PotreeNode(childName, currentNode.OctreeRoot, childAABB)
                        currentNode.Children.[childIndex] <- child
                        child.Parent <- currentNode
                        nodes.Add(child)

    let storage =
        let get key =
            match cache.TryGetValue(key) with
            | true, o ->
                match o with
                | :? PotreePointNode as node ->
                    if node.PotreeNodeData.NodeType = 2 then
                        let offset = node.PotreeNodeData.ByteOffset
                        let count = node.PotreeNodeData.ByteSize
                        let mutable data = File.ReadAllBytes(Path.Combine(storePath, "hierarchy.bin"))
                        loadHierarchyRecursive node.PotreeNodeData &data offset count

                    if isNull dataFileStream then
                        dataFileStream <- new FileStream(Path.Combine(storePath, "octree.bin"), FileMode.Open, FileAccess.Read)

                    readByteRange dataFileStream node.PotreeNodeData.ByteOffset node.PotreeNodeData.ByteSize
                | _ ->
                    failwith $"Invariant 26EA902D-4168-447B-9102-88E0C63A49E7. [get] Store key {key} is not PotreeNode."
            | _ -> [||]

        Storage(
            (fun _ _ _ -> ()),
            get,
            (fun _ _ _ -> null),
            (fun _ -> ()),
            (fun () -> ()),
            (fun () -> ()),
            cache
        )

    do
        match PotreeMetaData.tryDeserialize (Path.Combine(storePath, "metadata.json")) with
        | Some data -> metaData <- data
        | None -> raise (InvalidOperationException("Cannot open store at given path. Maybe no potree store?"))

    member _.Storage = storage

    member this.LoadRoot() : IPointNode =
        let octree = PotreeOctree()
        octree.Url <- storePath
        octree.Scale <- metaData.scale
        octree.Offset <- metaData.offset
        octree.Attributes <- PointAttributes(metaData.attributes)

        let min = V3d(metaData.boundingBox.min.[0], metaData.boundingBox.min.[1], metaData.boundingBox.min.[2])
        let max = V3d(metaData.boundingBox.max.[0], metaData.boundingBox.max.[1], metaData.boundingBox.max.[2])
        let bbox = Box3d(min, max)

        let root = PotreeNode("r", octree, bbox)
        root.NodeType <- 2
        root.ByteOffset <- 0L
        root.ByteSize <- int64 metaData.hierarchy.firstChunkSize

        octree.Root <- root

        this.GetPointCloudNode(root, Unchecked.defaultof<_>)

    member this.GetPointCloudNode(node: PotreeNode, parent: PotreePointNode) : IPointNode =
        if not storage.HasCache then
            raise (InvalidOperationException("PotreeStorage without cache is not valid"))

        let key = node.Id.ToString()

        match storage.Cache.TryGetValue(key) with
        | true, o ->
            match o with
            | :? PotreePointNode as cn -> cn :> IPointNode
            | _ -> failwith $"Invariant 56D238F3-40DC-40B8-9D94-BDBC8975B869 [GetPointCloudNode] Store key {key} is not PotreeNode."
        | _ ->
            let n = PotreePointNode(key, parent, node, this)
            storage.Cache.Add(key, n, 1)

            let buffer = storage.f_get.Invoke(key)
            if isNull buffer then
                raise (Exception($"PointCloudNode not found (id={key})."))

            storage.Cache.Remove(key) |> ignore
            let mutable buf = buffer
            n.Fill(&buf)
            storage.Cache.Add(key, n, buffer.Length)
            n :> IPointNode

    member _.TryGetPointNode(key: string) : IPointNode option =
        if not storage.HasCache then
            raise (InvalidOperationException("PotreeStorage without cache is not valid"))

        match storage.Cache.TryGetValue(key) with
        | true, (:? PotreePointNode as n) -> Some (n :> IPointNode)
        | _ -> None

    interface IDisposable with
        member _.Dispose() =
            if not (isNull dataFileStream) then
                dataFileStream.Dispose()
