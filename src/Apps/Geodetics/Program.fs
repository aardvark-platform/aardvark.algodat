// Learn more about F# at http://fsharp.org

open System
open Aardvark.Base
open Aardvark.Geodetics

[<EntryPoint>]
let main argv =
    
    let a = CoordinateSystem.esri "PROJCS[\"WGS_1984_UTM_Zone_56S\",GEOGCS[\"GCS_WGS_1984\",DATUM[\"D_WGS_1984\",SPHEROID[\"WGS_1984\",6378137,298.257223563]],PRIMEM[\"Greenwich\",0],UNIT[\"Degree\",0.017453292519943295]],PROJECTION[\"Transverse_Mercator\"],PARAMETER[\"latitude_of_origin\",0],PARAMETER[\"central_meridian\",153],PARAMETER[\"scale_factor\",0.9996],PARAMETER[\"false_easting\",500000],PARAMETER[\"false_northing\",10000000],UNIT[\"Meter\",1]]" //32756
    let b = CoordinateSystem.proj4 "+proj=merc +a=6378137 +b=6378137 +lat_ts=0.0 +lon_0=0.0 +x_0=0.0 +y_0=0 +k=1.0 +units=m +nadgrids=@null +wktext  +no_defs" // 3857

    
    let pa = V2d(337865.2269059850368649,6250798.0734301581978798)
    let pb = V2d(16836745.65,-4011453.12)

    let res = CoordinateSystem.transform a b pa


    let dist = res.XY - pb.XY
    Log.line "%A" res
    Log.line "%A" dist

    
    //let dist = rc.XY - p3857.XY
    //Log.line "%A" rc
    //Log.line "%A" dist
    
    //let dist = rt.XY - p32756.XY
    //Log.warn "%A" dist

    0 // return an integer exit code
