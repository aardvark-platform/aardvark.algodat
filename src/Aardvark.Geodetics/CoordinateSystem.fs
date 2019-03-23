namespace rec Aardvark.Geodetics

open Aardvark.Base
open System.Text

open DotSpatial.Projections

type CoordinateSystem private (system : ProjectionInfo, id : string) =
    static let epsgTable = System.Collections.Concurrent.ConcurrentDictionary<int, CoordinateSystem>()
    static let proj4Table = System.Collections.Concurrent.ConcurrentDictionary<string, CoordinateSystem>()
    static let esriTable = System.Collections.Concurrent.ConcurrentDictionary<string, CoordinateSystem>()
    static let ws = System.Text.RegularExpressions.Regex @"[ \t\r\n][ \t\r\n]+"

    member internal x.System : ProjectionInfo = system

    member x.Id = id
    member x.Name = system.Name

    member x.Zone = 
        if system.Zone.HasValue then Some system.Zone.Value else None


    override x.GetHashCode() =
        id.GetHashCode()

    override x.Equals o =
        match o with
        | :? CoordinateSystem as o -> id = o.Id
        | _ -> false
    override x.ToString() = sprintf "%s %s" id system.Name

    static member FromEPSGCode(id : int) =
        epsgTable.GetOrAdd(id, fun id -> 
            let sys = ProjectionInfo.FromEpsgCode id
            CoordinateSystem(sys, sprintf "EPSG:%d" id)
        )

    static member FromProj4(proj4 : string) =
        let proj4 = ws.Replace(proj4.Trim(), " ")
        proj4Table.GetOrAdd(proj4, fun proj4 -> 
            let sys = ProjectionInfo.FromProj4String proj4
            CoordinateSystem(sys, proj4)
        )

    static member FromEsri(esri : string) =
        let esri = ws.Replace(esri.Trim(), " ")
        esriTable.GetOrAdd(esri, fun esri -> 
            let sys = ProjectionInfo.FromEsriString esri
            CoordinateSystem(sys, esri)
        )
            
        
    static member Transform (src : CoordinateSystem, dst : CoordinateSystem, pt : V2d) = 
        let xy = [| pt.X; pt.Y |]
        Reproject.ReprojectPoints(xy, null, src.System, dst.System, 0, 1)
        V2d(xy.[0], xy.[1])
        
    static member Transform (src : CoordinateSystem, dst : CoordinateSystem, pt : V3d) = 
        let xy = [| pt.X; pt.Y |]
        let z = [| pt.Z |]
        Reproject.ReprojectPoints(xy, z, src.System, dst.System, 0, 1)
        V3d(xy.[0], xy.[1], z.[0])

    static member Transform (src : CoordinateSystem, dst : CoordinateSystem, pt : V3d[]) =
        let xy = Array.zeroCreate (2 * pt.Length)
        let z = Array.zeroCreate pt.Length
        let mutable j = 0
        for i in 0 .. pt.Length - 1 do
            let p = pt.[i]
            xy.[j] <- p.X
            xy.[j+1] <- p.Y
            z.[i] <- p.Z
            j <- j + 2
        
        Reproject.ReprojectPoints(xy, z, src.System, dst.System, 0, pt.Length)
        let res = Array.zeroCreate pt.Length
        let mutable j = 0
        for i in 0 .. pt.Length - 1 do
            res.[i] <- V3d(xy.[j], xy.[j+1], z.[i])
            j <- j + 2
        res

    static member Transform (src : CoordinateSystem, dst : CoordinateSystem, pt : V2d[]) =
        let xy = Array.zeroCreate (2 * pt.Length)
        let mutable j = 0
        for i in 0 .. pt.Length - 1 do
            let p = pt.[i]
            xy.[j] <- p.X
            xy.[j+1] <- p.Y
            j <- j + 2
            
        Reproject.ReprojectPoints(xy, null, src.System, dst.System, 0, pt.Length)
        let res = Array.zeroCreate pt.Length
        let mutable j = 0
        for i in 0 .. pt.Length - 1 do
            res.[i] <- V2d(xy.[j], xy.[j+1])
            j <- j + 2
        res

    static member Transform (src : CoordinateSystem, dst : CoordinateSystem, pt : seq<V3d>) = CoordinateSystem.Transform(src, dst, Seq.toArray pt) :> seq<_>
    static member Transform (src : CoordinateSystem, dst : CoordinateSystem, pt : list<V3d>) = CoordinateSystem.Transform(src, dst, List.toArray pt) |> Array.toList

    static member Transform (src : CoordinateSystem, dst : CoordinateSystem, pt : seq<V2d>) = CoordinateSystem.Transform(src, dst, Seq.toArray pt) :> seq<_>
    static member Transform (src : CoordinateSystem, dst : CoordinateSystem, pt : list<V2d>) = CoordinateSystem.Transform(src, dst, List.toArray pt) |> Array.toList




module CoordinateSystem =
    let inline epsg (id : int) = CoordinateSystem.FromEPSGCode id
    let inline proj4 (proj4 : string) = CoordinateSystem.FromProj4 proj4
    let inline esri (esri : string) = CoordinateSystem.FromEsri esri
    let inline name (sys : CoordinateSystem) = sys.Name
    let inline id (sys : CoordinateSystem) = sys.Id

    let inline private transformInternal (a : ^a) (b : ^b) (c : ^c) = ((^a or ^b or ^c) : (static member Transform : ^a * ^b * ^c -> ^d) (a, b, c))
    let inline transform (src : CoordinateSystem) (dst : CoordinateSystem) data = transformInternal src dst data



module Hugo = 

    let test (a : CoordinateSystem) (b : CoordinateSystem) =
        let x = CoordinateSystem.transform a b V3d.III
        let x = CoordinateSystem.transform a b [| V3d.III |]
        let x = CoordinateSystem.transform a b [ V3d.III ]
        let x = CoordinateSystem.transform a b (seq { yield V3d.III })
        ()
    //let inline transform (src : CoordinateSystem) (dst : CoordinateSystem) (pts) =
    //    let t = Transformation.Get(src, dst)
    //    tt t pts


    //let transform (src : CoordinateSystem) (dst : CoordinateSystem) =
    //    let t = Transformation.Get(src, dst)
    //    fun (pts : V3d[]) -> pts |> Array.map t.Transform

