namespace ScratchFSharp

open System
open System.IO
open Aardvark.Base
open Aardvark.Data.Points
open Aardvark.Geometry.Points

module Bla = 
    [<EntryPoint>]
    let main a =
        //let b = Box3d.Unit
        //let o = V3d.III*0.5
        //let d = V3d.IOO
        //let r = Ray3d(o+999.0*d,-d)
        //let rf = FastRay3d(o,d)
        //let mutable t = 0.0
        //let mutable tMin = 0.0
        //let mutable tMax = 0.0
        //let i = b.Intersects(r,&t)
        //let i2 = rf.Intersects(b,&tMin,&tMax)
        //Log.line "%A %A %A %A %A" i2 i t tMin tMax



        let storePath = @"C:\bla\store\teststore\data.uds"
        let key = "3d9654b1-c37a-4d41-ba83-393d58a9bdce"

        let bounds = Box3d(V3d.III*2.0, V3d.III*20.0)
        let rnd = RandomSystem()

        let rec genPoints loc =
            let mutable phi = 0.0
            let mutable theta = 0.0
            let dPhi = 1.0 * Constant.RadiansPerDegree
            let dTheta = 1.1 * Constant.RadiansPerDegree
            let ps = System.Collections.Generic.List()
            while phi < Constant.PiTimesTwo do  
                theta <- 0.0
                while theta < Constant.PiTimesTwo  do 
                    let d = V2d(phi,theta).CartesianFromSpherical()
                    let r = Ray3d(loc+999.0*d,-d)
                    let mutable t = 0.0
                    if bounds.Intersects(r,&t) then 
                        let p = r.GetPointOnRay(t)
                        ps.Add p |> ignore
                    theta <- theta+dTheta
                phi <- phi+dPhi
            ps |> CSharpList.toArray

        let locations = Array.init 5 (fun _ -> rnd.UniformV3d(Box3d.FromCenterAndSize(bounds.Center,bounds.Size*0.8)))

        let chunks = 
            locations |> Array.mapi (fun i loc -> 
                let pts = genPoints loc
                Chunk(pts,null,null,null,null,i,Range1i(i),bounds)
            ) 

        let store = 
            new Uncodium.SimpleStore.SimpleDiskStore(storePath,System.Action<_>(fun ss -> ss |> Array.iter (printfn "%A")))

        let add = 
            Action<string,obj,Func<byte[]>>(fun (name : string) (value : obj) (create : Func<byte[]>) -> store.Add(name, create.Invoke()))
        let get s = store.Get s
        let getSlice k o i = store.GetSlice(k,o,i)
        let remove k = store.Remove k
        let dispose() = store.Dispose()
        let flush() = store.Flush()
        let dict = LruDictionary(1<<<30)
        let storage = 
            new Storage(
                add, 
                get, 
                getSlice, 
                remove, 
                dispose, 
                flush, 
                dict
            )

        let config = 
            ImportConfig.Default
                .WithStorage(storage)
                .WithKey(key)
                .WithOctreeSplitLimit(8192)

        let pc = PointCloud.Chunks(chunks,config)
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(storePath),"key.txt"),key)
        Log.line "done %A %A" pc.PointCount pc.Id
        0