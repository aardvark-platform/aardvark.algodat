// Learn more about F# at http://fsharp.org

open System
open Aardvark.Base
open Aardvark.Geodetics

[<EntryPoint>]
let main argv =
    
    
    let a = DotSpatial.Projections.ProjectionInfo.FromEpsgCode 32756
    let b = DotSpatial.Projections.ProjectionInfo.FromEpsgCode 3857

    
    let xy = [|337865.2269059850368649;6250798.0734301581978798|]
    let z = [|0.0|]
    DotSpatial.Projections.Reproject.ReprojectPoints(xy, z, a, b, 0, 1)

    let rb = V3d(xy.[0], xy.[1], z.[0])

    //let a = CoordinateSystem.get 32756
    //let b = CoordinateSystem.get 3857
    //let c = CoordinateSystem.WebMercator

    let p3857 = V3d(16836745.65,-4011453.12, 0.0)
    //let p32756 = V3d(337865.2269059850368649,6250798.0734301581978798, 0.0)
    //let p3857 = V3d(16836745.65,-4011453.12, 0.0)
    ////let rb = CoordinateSystem.transform a b p32756
    //let rc = CoordinateSystem.transform a c p32756

    
    //let rt = CoordinateSystem.transform b a rb


    let dist = rb.XY - p3857.XY
    Log.line "%A" rb
    Log.line "%A" dist

    
    //let dist = rc.XY - p3857.XY
    //Log.line "%A" rc
    //Log.line "%A" dist
    
    //let dist = rt.XY - p32756.XY
    //Log.warn "%A" dist

    0 // return an integer exit code
