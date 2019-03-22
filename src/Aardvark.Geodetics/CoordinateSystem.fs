namespace rec Aardvark.Geodetics

open Aardvark.Base
open System.Text
open ProjNet
open ProjNet.CoordinateSystems
open ProjNet.Converters
open GeoAPI.CoordinateSystems
open GeoAPI.Geometries
open ProjNet.Converters.WellKnownText
open ProjNet.CoordinateSystems.Transformations


open DotSpatial.Projections.Transforms

[<AutoOpen>]
module private Dehate =
    let factory = CoordinateTransformationFactory()



    type Transformation private (srcSystem : CoordinateSystem, dstSystem : CoordinateSystem) =
        static let table = System.Collections.Concurrent.ConcurrentDictionary<CoordinateSystem * CoordinateSystem, Transformation>()

        let trans = factory.CreateFromCoordinateSystems(srcSystem.System, dstSystem.System)

        member x.Transform(p : V3d) = 
            V3d (trans.MathTransform.Transform [| p.X; p.Y; p.Z |])

        member x.Transform(p : V3d[]) = 
            let arr = Array.zeroCreate 3
            p |> Array.map (fun pt ->
                arr.[0] <- pt.X; arr.[1] <- pt.X; arr.[2] <- pt.X;
                let res = trans.MathTransform.Transform arr
                V3d res
            )
        
        member x.Transform(p : seq<V3d>) = 
            p |> Seq.map (fun pt ->
                let res = trans.MathTransform.Transform [|pt.X; pt.Y; pt.Z |]
                V3d res
            )
        
        member x.Transform(p : list<V3d>) = 
            p |> List.map (fun pt ->
                let res = trans.MathTransform.Transform [|pt.X; pt.Y; pt.Z |]
                V3d res
            )
        static member Get(srcSystem : CoordinateSystem, dstSystem : CoordinateSystem) =
            table.GetOrAdd((srcSystem, dstSystem), fun (srcSystem, dstSystem) -> Transformation(srcSystem, dstSystem))


type CoordinateSystem private (system : ICoordinateSystem, epsg : int) =
    static let table = System.Collections.Concurrent.ConcurrentDictionary<int, CoordinateSystem>()
    
    static let web = 
        let wkt = ProjectedCoordinateSystem.WebMercator.WKT
        Log.warn "%s" wkt
        CoordinateSystem(ProjectedCoordinateSystem.WebMercator, 1231233857)

    //let system = CoordinateSystemWktReader.Parse(WKT.get epsg, Encoding.UTF8) |> unbox<ICoordinateSystem>

    static member WebMercator = web

    member internal x.System : ICoordinateSystem = system

    member x.Id = epsg
    member x.Name = system.Name

    override x.GetHashCode() = epsg
    override x.Equals o =
        match o with
        | :? CoordinateSystem as o -> epsg = o.Id
        | _ -> false
    override x.ToString() = sprintf "EPSG:%d %s" epsg system.Name

    static member Get(id : int) =
        table.GetOrAdd(id, fun id -> 
            let sys = CoordinateSystemWktReader.Parse(WKT.get id, Encoding.UTF8) |> unbox<ICoordinateSystem>
            CoordinateSystem(sys, id)
        )

    static member Transform (src : CoordinateSystem, dst : CoordinateSystem, pt : V3d) = Transformation.Get(src, dst).Transform pt
    static member Transform (src : CoordinateSystem, dst : CoordinateSystem, pt : V3d[]) = Transformation.Get(src, dst).Transform pt
    static member Transform (src : CoordinateSystem, dst : CoordinateSystem, pt : seq<V3d>) = Transformation.Get(src, dst).Transform pt
    static member Transform (src : CoordinateSystem, dst : CoordinateSystem, pt : list<V3d>) = Transformation.Get(src, dst).Transform pt




module CoordinateSystem =
    let inline get (id : int) = CoordinateSystem.Get id
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

