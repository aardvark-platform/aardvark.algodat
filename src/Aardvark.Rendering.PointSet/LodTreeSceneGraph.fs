namespace Aardvark.Rendering.PointSet

open FSharp.Data.Adaptive
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph

#nowarn "9"
module Readback =
    open OpenTK
    open OpenTK.Graphics.OpenGL4
    open Aardvark.Rendering.GL
    open Microsoft.FSharp.NativeInterop

    [<AutoOpen>]
    module DitherMatrix =
        type private A = A

        let dither1024 =
            let ass = typeof<A>.Assembly
            let name = ass.GetName().Name
            use stream = ass.GetManifestResourceStream (sprintf "%s.resource.forced-1024.bin" name)
            let r = new System.IO.BinaryReader(stream)
            let data = Array.init (1024 * 1024) (fun _ -> r.ReadInt32())
            Matrix<int>(data, V2l(1024, 1024)).Map(fun v -> float32 v / 1048576.0f)

    module Shader =
        open FShade

        let depthSam =
            sampler2d {
                texture uniform?DepthStencil
                addressU WrapMode.Clamp
                addressV WrapMode.Clamp
                filter Filter.MinMagPoint
            }
        let ditherSam =
            sampler2d {
                texture uniform?Dither
                addressU WrapMode.Wrap
                addressV WrapMode.Wrap
                filter Filter.MinMagPoint
            }

        [<GLSLIntrinsic("atomicAdd({0}, {1})"); KeepCall>]
        let atomicAdd (l : int) (v : int) : int = onlyInShaderCode "atomicAdd"

        [<LocalSize(X = 8, Y = 8)>]
        let computer (ndcs : V4f[]) (cnt : int[]) (center : V2i) (radius : int) (offset : V2i) (size : V2i) =
            compute {
                let o = getGlobalId().XY - V2i(radius,radius)

                if (V2f o).Length < float32 radius then
                    let id = o + center + offset
                    let s = depthSam.Size

                    if id.X < s.X && id.Y < s.Y && id.X >= 0 && id.Y >= 0 then
                        let dither = ditherSam.SampleLevel((V2f(id)+V2f.Half) / V2f ditherSam.Size, 0.0f).X
                        let thresh = uniform?Threshold

                        if dither <= thresh then
                            let d = depthSam.[id].X
                            if d > 0.0f && d < 1.0f then
                                let tc = (V2f (id - offset) + V2f.Half) / V2f size
                                let ndc = V3f(tc.X * 2.0f - 1.0f, tc.Y * 2.0f - 1.0f, d * 2.0f - 1.0f)
                                let idx = atomicAdd cnt.[0] 1
                                ndcs.[idx] <- V4f(ndc, dither)
            }

    type DepthReader(rt : IRuntime) =
        let ditherTex =
            let tex = rt.CreateTexture2D(V2i dither1024.Size, TextureFormat.R32f, 1, 1)
            let img = PixImage<float32>(Col.Format.Gray, dither1024)
            rt.Upload(tex,img)
            tex

        let computerShader = rt.CreateComputeShader(Shader.computer)


        member x.Run(depth : IBackendTexture, center : V2i, radius : int, offset : V2i, texSize : V2i, wantedCt : int) =
            let size = 2 * radius + 1

            let ca = float radius * float radius * Constant.Pi
            let threshold = float wantedCt / ca
            use cntBuffer = rt.CreateBuffer<int> 1
            use ndcsBuffer = rt.CreateBuffer<V4f>(max (wantedCt*2) 64)

            use ip = rt.CreateInputBinding(computerShader)
            ip.["DepthStencil"] <- depth
            ip.["Dither"] <- ditherTex
            ip.["center"] <- center
            ip.["radius"] <- radius
            ip.["offset"] <- offset
            ip.["size"] <- texSize
            ip.["Threshold"] <- threshold
            ip.["cnt"] <- cntBuffer
            ip.["ndcs"] <- ndcsBuffer
            ip.Flush()

            let groups =
                let inline ceilDiv a b =
                    if a % b = 0 then a / b
                    else 1 + a / b
                V2i (
                    ceilDiv size 8,
                    ceilDiv size 8
                )

            let cntArr = [|0|]
            cntBuffer.Upload cntArr
            rt.Run [
                //ComputeCommand.Copy(cntArr, cntBuffer)
                ComputeCommand.Bind computerShader
                ComputeCommand.SetInput ip
                ComputeCommand.Dispatch groups
                ComputeCommand.Sync cntBuffer.Buffer
                //ComputeCommand.Copy(cntBuffer, cntArr)
            ]
            cntBuffer.Download cntArr

            let cnt = cntArr.[0]
            if cnt > 0 then
                ndcsBuffer.[0..cnt-1].Download()
            else
                [||]

    let cachy = System.Collections.Concurrent.ConcurrentDictionary<IRuntime, DepthReader>()

    let readDepth (depth : IBackendTexture) (center : V2i) (radius : int) (offset : V2i) (size : V2i) (maxCt : int) = //(offset : V2i) (size : V2i) =
        match depth.Runtime with
        | :? Aardvark.Rendering.GL.Runtime as rt when rt.Context.Driver.version < System.Version(4,3,0) ->
            //let center = center - offset
            if center.AllGreaterOrEqual 0 && center.AllSmaller size then

                let tex = depth :?> Aardvark.Rendering.GL.Texture

                let c = V2i(center.X, size.Y - 1 - center.Y)

                let box = Box2i.FromCenterAndSize(c, 2*radius*V2i.II) //Box2i(c, c + V2i.II)
                let depths = rt.DownloadDepth(tex, 0, 0, Box2i(box.Min + offset, box.Max + offset))



                let ndcs = Matrix<V4f>(depths.Size)
                ndcs.SetByCoord(fun (c : V2l) ->
                    let z = 2.0 * float depths.[c] - 1.0

                    let pixel = box.Min + V2i c
                    let pixel = V2i(pixel.X, size.Y - 1 - pixel.Y)

                    let tc = (V2d pixel + V2d.Half) / V2d size
                    let ndc = V3d(tc.X * 2.0 - 1.0, 2.0 * tc.Y - 1.0, z)
                    V4f(V3f ndc, Vec.distance (V2f pixel) (V2f center))
                ) |> ignore

                ndcs.Data |> Array.filter (fun ndc -> ndc.Z < 1.0f) |> Array.sortBy (fun v -> v.W) |> Array.truncate maxCt
            else
                [||]
        | _ ->
            let reader = cachy.GetOrAdd(unbox depth.Runtime, fun r -> DepthReader(r))
            let ptr = reader.Run(depth, center, radius, offset, size, maxCt)
            ptr


[<ReflectedDefinition>]
module private DeferredPointSetShaders =
    open FShade
    open PointSetShaders


    [<Inline>]
    let div (v : V4f) = v.XYZ / v.W


    [<ReflectedDefinition; Inline>]
    let getDepthRange (ndcRadius : V2f) (z : float32) =
        let rwx = ndcRadius.X / uniform.ProjTrafo.M00
        let rwy = ndcRadius.Y / uniform.ProjTrafo.M11
        abs (
            (uniform.ProjTrafo.M32*z - uniform.ProjTrafo.M22) / (4.0f / (rwx + rwy) + 2.0f * uniform.ProjTrafo.M32)
        )



    type PointVertexSimple =
        {
            [<Position>] pos : V4f
            [<Color; Interpolation(InterpolationMode.Flat)>] col : V4f
            [<Semantic("DepthRange"); Interpolation(InterpolationMode.Flat)>] depthRange : float32
            [<PointSize>] s : float32
            [<PointCoord>] pc : V2f
        }

    let lodPointSizeSimple (v : Effects.Vertex) =
        vertex {
            let vp = uniform.ProjTrafoInv * v.pos
            let vp = vp / vp.W

            let pp = v.pos / v.pos.W
            let size = uniform.PointSize
            let r = size / V2f uniform.ViewportSize

            let depthRange = getDepthRange r pp.Z //abs (pp0z - ppz.Z)

            return {
                pos = v.pos
                col = v.c
                depthRange = depthRange
                s = uniform.PointSize
                pc = V2f.Zero
            }
        }

    type SphereFragment =
        {
            [<Color>] color : V4f
            [<Depth>] depth : float32
        }
    let lodPointSphereSimple (v : PointVertexSimple) =
        fragment {
            let o = 2.0f * v.pc - V2f.II
            let r2 = Vec.dot o o
            if r2 > 1.0f then
                discard()

            let dz = v.depthRange * sqrt(1.0f - r2)

            let center = v.pos.XYZ / v.pos.W

            let z = v.pos.Z / v.pos.W

            return { color = v.col; depth = 0.5f * (z - dz) + 0.5f }
        }

    type PointVertex =
        {
            [<Position>] pos : V4f
            [<Color; Interpolation(InterpolationMode.Flat)>] col : V4f
            //[<Normal>] n : V3d
            [<Semantic("ViewPosition")>] vp : V4f
            [<Semantic("AvgPointDistance")>] dist : float32
            [<Semantic("DepthRange"); Interpolation(InterpolationMode.Flat)>] depthRange : float32
            [<PointSize>] s : float32
            [<Semantic("PointPixelSize")>] ps : float32
            [<PointCoord>] c : V2f
            [<Normal>] n : V3f
            [<Semantic("TreeId")>] id : int
            [<Semantic("PartIndices")>] partIndex : int
            [<Semantic("MaxTreeDepth")>] treeDepth : int
            [<Semantic("Normal32"); Interpolation(InterpolationMode.Flat)>] n32 : int
            [<FragCoord>] fc : V4f
            [<SamplePosition>] sp : V2f
        }


    let mapColors =
        [|
            C4b(141uy, 211uy, 199uy, 255uy)
            C4b(255uy, 255uy, 179uy, 255uy)
            C4b(190uy, 186uy, 218uy, 255uy)
            C4b(251uy, 128uy, 114uy, 255uy)
            C4b(128uy, 177uy, 211uy, 255uy)
            C4b(253uy, 180uy, 98uy, 255uy)
            C4b(179uy, 222uy, 105uy, 255uy)
            C4b(252uy, 205uy, 229uy, 255uy)
            C4b(217uy, 217uy, 217uy, 255uy)
            C4b(188uy, 128uy, 189uy, 255uy)
            C4b(204uy, 235uy, 197uy, 255uy)

        |] |> Array.map (fun c -> c.ToC4f().ToV4f())


    let colorOrWhite (v : PointVertex) =
        vertex {
            let mutable color = v.col
            if not uniform?ShowColors then
                color <- mapColors.[(v.partIndex+11)%11]
                let filteredPartIndex : int = uniform?FilterPartIndex
                if filteredPartIndex < 0 || v.partIndex = filteredPartIndex then
                    return { v with col = color }
                else
                    return {v with col = V4f.OOOO; pos = V4f(-999.0f,-999.0f,-999.0f,-999.0f); ps=0.0f; vp = V4f(-999.0f,-999.0f,-999.0f,-999.0f)}
            else
                return v
        }

    let lodPointSize (v : PointVertex) =
        vertex {
            let mv = uniform.ModelViewTrafos.[v.id]

            let vp = mv * v.pos
            //let vn = mv * V4d(v.n, 0.0) |> Vec.xyz |> Vec.normalize

            let pp = uniform.ProjTrafo * vp

            let size = uniform.PointSize
            let r = size / V2f uniform.ViewportSize

            let dx = div (vp + uniform.ProjTrafoInv.C0 * r.X * pp.W) - vp.XYZ |> Vec.length
            let dy = div (vp + uniform.ProjTrafoInv.C1 * r.Y * pp.W) - vp.XYZ |> Vec.length
            let dist = 0.5f * (dx + dy)

            let pp0z = pp.Z / pp.W
            let ppz = pp + uniform.ProjTrafo.C2*dist |> div

            let depthRange = abs (pp0z - ppz.Z)


            let col = v.col.XYZ

            let o = uniform.Overlay.[v.id].X
            let col =
                if o > 0.0f then
                    let h = heat (float32 v.treeDepth / 6.0f)
                    o * h.XYZ + (1.0f - o) * col
                else
                    v.col.XYZ

            return
                { v with
                    ps = 1.0f
                    s = 1.0f
                    pos = pp / pp.W
                    depthRange = depthRange
                    vp = vp
                    col = V4f(col, v.col.W)
                }
        }

    let lodPointCircular (v : PointVertex) =
        fragment {
            return { v with pos = V4f(v.pos.XYZ, v.depthRange) }
        }



    type FillSphereVertex =
        {
            [<FragCoord>]
            fc : V4f
        }


    let cSam =
        sampler2d {
            texture uniform?Colors
            filter Filter.MinMagPoint
            addressU WrapMode.Clamp
            addressV WrapMode.Clamp
        }

    let depthSam =
        sampler2d {
            texture uniform?DepthStencil
            filter Filter.MinMagPoint
            addressU WrapMode.Clamp
            addressV WrapMode.Clamp
        }

    let ppSam =
        sampler2d {
            texture uniform?Positions
            filter Filter.MinMagPoint
            addressU WrapMode.Clamp
            addressV WrapMode.Clamp
        }



    type BlitFragment =
        {
            [<Color>] c : V4f
            [<Depth>] d : float32
            [<Semantic("PointId")>] id : float32
        }

    let blit (v : FillSphereVertex) =
        fragment {
            let size = V2f uniform.ViewportSize
            let invSize = 1.0f / size
            let tc0 = v.fc.XY * invSize
            let c = cSam.SampleLevel(tc0, 0.0f).XYZ
            return { c = V4f(c, 1.0f); d = 0.0f }

        }

    let fillSpheres (v : FillSphereVertex) =
        let dSam = ()

        fragment {
            let size = V2f uniform.ViewportSize
            let invSize = 1.0f / size
            let tc0 = v.fc.XY * invSize
            let s : float32 = uniform?PointSize
            let hs = ceil (s / 2.0f + 0.5f) |> int

            let rmax2 = sqr (s / 2.0f)
            let rmax22 = sqr (s / 2.0f + 1.0f)

            let step = invSize

            let mutable minDepth = 2.0f
            let mutable minTc = V2f.Zero

            for xo in -hs .. hs do
                for yo in -hs .. hs do
                    let r2 = sqr xo + sqr yo |> float32
                    if r2 <= rmax22 then
                        let tc = tc0 + V2f(xo, yo) * step

                        let pCenter = ppSam.SampleLevel(tc, 0.0f)
                        if pCenter.Z < 1.0f then
                            let cc = 0.5f * (pCenter.XY + V2f.II) * size
                            let rr = Vec.lengthSquared (cc.XY - v.fc.XY)
                            if rr <= rmax2 then
                                let z = sqrt (1.0f - rr / rmax2)

                                let d = if pCenter.W >= 1.0f then pCenter.Z else pCenter.Z - z * pCenter.W
                                if d < minDepth then
                                    minDepth <- d
                                    minTc <- tc

            if minDepth > 1.0f  then
                return { c = cSam.SampleLevel(tc0, 0.0f); d = 1.0f; id = -1.0f }
            else
                let pixel = minTc * size |> round |> V2i
                let id = float32 pixel.X + float32 pixel.Y * size.X
                return { c = cSam.SampleLevel(minTc, 0.0f); d = 0.5f * minDepth + 0.5f; id = id }
        }




    let realRootsOfNormed (c2 : float32) (c1 : float32) (c0 : float32) =
        let mutable d = c2 * c2
        let p3 = 1.0f/3.0f * (-1.0f/3.0f * d + c1)
        let q2 = 1.0f/2.0f * ((2.0f/27.0f * d - 1.0f/3.0f * c1) * c2 + c0)
        let p3c = p3 * p3 * p3
        let shift = 1.0f/3.0f * c2
        d <- q2 * q2 + p3c
        if d < 0.0f then
            if p3c > 0.0f || p3 > 0.0f then
                -1.0f
            else
                let v = -q2 / sqrt(-p3c)
                if v < -1.0f || v > 1.0f then
                    -1.0f
                else
                    let phi = 1.0f / 3.0f * acos v
                    let t = 2.0f * sqrt(-p3)
                    let r0 = t * cos phi - shift
                    let r1 = -t * cos (phi + ConstantF.Pi / 3.0f) - shift
                    let r2 = -t * cos (phi - ConstantF.Pi / 3.0f) - shift
                    min r0 (min r1 r2)

        else
            d <- sqrt d
            let uav = cbrt (d - q2) - cbrt (d + q2)
            let s0 = uav - shift
            let s1 = -0.5f * uav - shift
            min s0 s1

    let samples32 =
        [|
            //V2d( 0.0f, 0.0f )
            V2f( -0.1282280447374989f, 0.9832676769841681f )
            V2f( -0.16837061570862488f, -0.9830604356268391f )
            V2f( -0.9798310993397246f, -0.18741003201467368f )
            V2f( 0.7472985137846405f, -0.6138536654225581f )
            V2f( 0.8310405456198345f, 0.4374716275485648f )
            V2f( -0.7502439994363835f, 0.5809097186211367f )
            V2f( -0.6932459550231614f, -0.6776730258773993f )
            V2f( 0.2553124458770123f, 0.4959210565172609f )
            V2f( 0.5465954597348052f, -0.04472223662487038f )
            V2f( 0.09277897488068058f, -0.5113610008959538f )
            V2f( -0.47472312402622696f, -0.15862786550784555f )
            V2f( -0.266310498212525f, 0.44196009161163013f )
            V2f( 0.977150215545575f, -0.07917613624521679f )
            V2f( 0.49363631849664313f, 0.8522020775091785f )
            V2f( -0.29941898954428015f, -0.6401691156248741f )
            V2f( 0.3093408037469358f, -0.8927849883782302f )
            V2f( -0.9050970007350029f, 0.2629623257288251f )
            V2f( -0.5427419881280113f, 0.20269063268927187f )
            V2f( -0.4213485328487087f, 0.8280808387907443f )
            V2f( 0.4260665935850835f, -0.4674957666878758f )
            V2f( 0.17217971485841713f, 0.8782752290069882f )
            V2f( 0.4956360320348418f, 0.32862186679234867f )
            V2f( -0.18798732300077567f, -0.30990045623759255f )
            V2f( -0.06471963785060358f, 0.661824279105518f )
            V2f( 0.2889479496263221f, 0.11796711369520718f )
            V2f( -0.2614045619541214f, 0.119872960309031f )
            V2f( 0.7588203318614408f, -0.31438846347597377f )
            V2f( 0.03110759701461603f, 0.2698364107850206f )
            V2f( -0.7329726419319801f, -0.39620420911822984f )
            V2f( -0.7234500503415809f, -0.02221612481994348f )
            V2f( 0.5621082888631382f, 0.5854406482477675f )

        |]

    let samples24 =
        [|
            //V2d( 0.0f, 0.0f )
            V2f( -0.4612850228120782f, -0.8824263018037591f )
            V2f( 0.2033539719528926f, 0.9766070232577696f )
            V2f( 0.8622755945065503f, -0.4990552917715807f )
            V2f( -0.8458406529500018f, 0.4340626564690164f )
            V2f( 0.9145341241356336f, 0.40187426079092753f )
            V2f( -0.8095919285224212f, -0.2476471278659192f )
            V2f( 0.2443597793708885f, -0.8210571365841042f )
            V2f( -0.29522102954593127f, 0.6411496844366571f )
            V2f( 0.4013698454531175f, 0.47134750051312063f )
            V2f( -0.1573158341083741f, -0.48548502348882533f )
            V2f( 0.5674301785250454f, -0.1052346781436156f )
            V2f( -0.4929375319230899f, 0.09422383038685558f )
            V2f( 0.967785465127825f, -0.06868225365333279f )
            V2f( 0.2267967507441493f, -0.40237871966279687f )
            V2f( -0.7200979001122771f, -0.6248240905561527f )
            V2f( -0.015195608523765971f, 0.35623701723070667f )
            V2f( -0.11428925675805125f, -0.963723441683084f )
            V2f( 0.5482105069441386f, 0.781847612911249f )
            V2f( -0.6515264455787967f, 0.7473765703131305f )
            V2f( 0.5826875031269089f, -0.6956573112908789f )
            V2f( -0.8496230198638387f, 0.09209564840857346f )
            V2f( 0.38289808661249414f, 0.15269522898022844f )
            V2f( -0.4951171173546325f, -0.2654758742352245f )
        |]

    let samples16 =
        [|
            //V2d( 0.0f, 0.0f )
            V2f( 0.7361771670747784f, -0.6743709190503513f )
            V2f( -0.9845834321368149f, -0.15755179290200677f )
            V2f( 0.7700310258512124f, 0.615334433294869f )
            V2f( -0.27028343507264774f, 0.9515396198187963f )
            V2f( -0.2083205670375388f, -0.9493326049649131f )
            V2f( 0.9942225629687943f, -0.012592138144431327f )
            V2f( -0.6809634346402852f, 0.4404622010597125f )
            V2f( 0.18128290701712424f, 0.6041707799950865f )
            V2f( -0.5045588564035041f, -0.46733843194529184f )
            V2f( 0.2599379562773085f, -0.4573765455782192f )
            V2f( 0.47958155225071786f, 0.21184103802949944f )
            V2f( -0.5566981555829099f, -0.06497638287046927f )
            V2f( 0.33011199190656254f, -0.9042445608311466f )
            V2f( -0.2588703212887866f, 0.3487611093360376f )
            V2f( 0.5936641948988021f, -0.19746322908552932f )
        |]

    type UniformScope with
        member x.NearFar : V2f = uniform?NearFar
        member x.PlaneFit : bool = uniform?PlaneFit
        member x.PlaneFitTolerance : float32 = uniform?PlaneFitTolerance // 0.05
        member x.PlaneFitRadius : float32 = uniform?PlaneFitRadius // 7.0
        member x.Gamma : float32 = uniform?Gamma


    [<Inline>]
    let sampleDepth (tc : V2f) =
        depthSam.SampleLevel(tc, 0.0f).X * 2.0f - 1.0f

    [<Inline>]
    let viewPosSize() =
        V2f depthSam.Size

    let sampleViewPos (tc : V2f) : V3f =
        let z = sampleDepth tc
        if z >= 0.99999f then
            V3f.Zero
        else
            let ndc = 2.0f * tc - 1.0f
            let vp = uniform.ProjTrafoInv * V4f(ndc, z, 1.0f)
            vp.XYZ / vp.W

    [<Inline>]
    let sampleSimpleNormal (vp : V3f) (tc : V2f) =
        let s = viewPosSize()
        let vpx = sampleViewPos(tc + V2f.IO / s)
        let vpy = sampleViewPos(tc + V2f.OI / s)
        let vnx = sampleViewPos(tc - V2f.IO / s)
        let vny = sampleViewPos(tc - V2f.OI / s)

        let z =  abs vp.Z  < 0.0001f
        let zx = abs vpx.Z < 0.0001f
        let zy = abs vpy.Z < 0.0001f
        let nx = abs vpx.Z < 0.0001f
        let ny = abs vpy.Z < 0.0001f

        if z || (zx && nx) || (zy && ny) then
            -Vec.normalize vp
        //elif zx || zy || z || abs(vp.Z - vpx.Z) > 0.1 || abs(vp.Z - vpy.Z) > 0.1 then
        //    V3d.Zero
        elif not zx && not zy then
            let n = Vec.cross (vpx - vp) (vpy - vp)
            Vec.normalize n
        elif not zx && not ny then
            let n = Vec.cross (vpx - vp) (vp - vny)
            Vec.normalize n
        elif not nx && not zy then
            let n = Vec.cross (vp - vnx) (vpy - vp)
            Vec.normalize n
        else
            let n = Vec.cross (vp - vnx) (vp - vny)
            Vec.normalize n

    let randomSam =
        sampler2d {
            texture uniform?RandomTexture
            filter Filter.MinMagPoint
            addressU WrapMode.Wrap
            addressV WrapMode.Wrap
        }


    let pidSam =
        sampler2d {
            texture uniform?PointId
            filter Filter.MinMagPoint
            addressU WrapMode.Clamp
            addressV WrapMode.Clamp
        }


    let sampleNormal (vp : V3f) (tc : V2f) =
        let nf = uniform.NearFar
        let ld = -vp.Z

        if ld > 0.0f && ld < nf.Y && uniform.PlaneFit then
            let vn = sampleSimpleNormal vp tc
            if vn = V3f.Zero then
                V4f(V3f.OOI, nf.Y + 10.0f)
            else
                let size = viewPosSize()
                //let id0 = pidSam.SampleLevel(tc, 0.0).X |> abs |> round |> int
                //let mutable id1 = -1
                //let mutable id2 = -1

                let plane = V4f(vn, -Vec.dot vn vp)

                let mutable sum =   V3f.Zero
                let mutable sumSq = V3f.Zero
                let mutable off =   V3f.Zero
                let mutable cnt = 1

                let x = randomSam.SampleLevel((floor (tc * viewPosSize()) + V2f.Half) / V2f randomSam.Size, 0.0f).XY |> Vec.normalize
                let y = V2f(-x.Y, x.X)

                for o in samples24 do
                    let tc = tc + uniform.PlaneFitRadius * (x*o.X + y*o.Y) / size
                    let p = sampleViewPos tc

                    if p.Z <> 0.0f && abs (Vec.dot plane (V4f(p, 1.0f))) <= uniform.PlaneFitTolerance then

                        //if id1 < 0 then
                        //    let o = pidSam.SampleLevel(tc, 0.0).X |> abs |> round |> int
                        //    if o <> id0 then id1 <- o
                        //elif id2 < 0 then
                        //    let o = pidSam.SampleLevel(tc, 0.0).X |> abs |> round |> int
                        //    if o <> id0 && o <> id1 then id2 <- o


                        let pt = p - vp
                        sum <- sum + pt
                        sumSq <- sumSq + sqr pt
                        off <- off + V3f(pt.Y*pt.Z, pt.X*pt.Z, pt.X*pt.Y)
                        cnt <- cnt + 1

                if cnt >= 8 then

                    let n = float32 cnt
                    let avg = sum / n
                    let xx = (sumSq.X - avg.X * sum.X) / (n - 1.0f)
                    let yy = (sumSq.Y - avg.Y * sum.Y) / (n - 1.0f)
                    let zz = (sumSq.Z - avg.Z * sum.Z) / (n - 1.0f)

                    let xy = (off.Z - avg.X * sum.Y) / (n - 1.0f)
                    let xz = (off.Y - avg.X * sum.Z) / (n - 1.0f)
                    let yz = (off.X - avg.Y * sum.Z) / (n - 1.0f)

                    let _a = 1.0f
                    let b = -xx - yy - zz
                    let c = -sqr xy - sqr xz - sqr yz + xx*yy + xx*zz + yy*zz
                    let d = -xx*yy*zz - 2.0f*xy*xz*yz + sqr xz*yy + sqr xy*zz + sqr yz*xx


                    let l = realRootsOfNormed b c d
                    if l < 0.0f then
                        V4f(vn, -Vec.dot vn vp)
                    else
                        let c0 = V3f(xx - l, xy, xz)
                        let c1 = V3f(xy, yy - l, yz)
                        let c2 = V3f(xz, yz, zz - l)
                        let len0 = Vec.lengthSquared c0
                        let len1 = Vec.lengthSquared c1
                        let len2 = Vec.lengthSquared c2

                        let normal =
                            if len0 > len1 then
                                if len2 > len1 then Vec.cross c0 c2
                                else Vec.cross c0 c1
                            else
                                if len2 > len0 then Vec.cross c1 c2
                                else Vec.cross c0 c1

                        let len = Vec.length normal

                        if len > 0.0f then
                            let normal =
                                if normal.Z < 0.0f then -normal / len
                                else normal / len

                            V4f(normal, -Vec.dot normal (vp + avg))
                        else
                            V4f(vn, -Vec.dot vn vp)
                else
                    V4f(vn, -Vec.dot vn vp)


        else
            let vn = sampleSimpleNormal vp tc
            V4f(vn, -Vec.dot vn vp)




    type Fragment =
        {
            [<Color>]
            color : V4f

            [<Normal>]
            normal : V3f

            [<Depth>]
            depth : float32
        }

    let blitPlaneFit (v : Effects.Vertex) =
        fragment {
            let z = sampleDepth v.tc
            if z < -1.0f || z >= 1.0f then discard()
            let vp = sampleViewPos v.tc

            let c = cSam.SampleLevel(v.tc, 0.0f).XYZ
            let plane = sampleNormal vp v.tc
            let n = plane.XYZ

            let diffuse =
                if uniform?Diffuse then (Vec.dot (Vec.normalize vp.XYZ) n) |> abs
                else 1.0f

            let col = c.XYZ * (0.2f + 0.8f*diffuse)
            let col = col ** (1.0f / uniform.Gamma)

            let mutable finalDepth = 2.0f

            if n.Z <> 0.0f then
                // l*<vp|n> + w = 0
                let l = -plane.W / Vec.dot vp.XYZ n
                let npos = vp.XYZ * l
                let pp = uniform.ProjTrafo * V4f(npos, 1.0f)
                finalDepth <- pp.Z / pp.W
            else
                finalDepth <- sampleDepth v.tc


            //let id = pidSam.SampleLevel(v.tc, 0.0f).X |> round |> abs |> int
            //let pixel = V2i(id % uniform.ViewportSize.X, id / uniform.ViewportSize.X)
            //let tc = (V2d pixel + V2d.Half) / V2d uniform.ViewportSize

            return {
                color = V4f(col, 1.0f)
                normal = n
                depth = finalDepth * 0.5f + 0.5f
            }
        }

[<Struct>]
type PickPoint =
    {
        World : V3d
        View  : V3d
        Pixel : V2i
        Ndc   : V3d
    }

type PointSetRenderConfig =
    {
        runtime         : IRuntime
        size            : aval<V2i>
        viewTrafo       : aval<Trafo3d>
        projTrafo       : aval<Trafo3d>

        colors          : aval<bool>
        pointSize       : aval<float>
        planeFit        : aval<bool>
        diffuse         : aval<bool>
        ssao            : aval<bool>
        planeFitTol     : aval<float>
        planeFitRadius  : aval<float>
        gamma           : aval<float>

        ssaoConfig      : SSAOConfig

        lodConfig       : LodTreeRenderConfig

        pickCallback    : Option<ref<V2i -> int -> int -> PickPoint[]>>
    }

module Sg =

    let wrapPointCloudSg (config : PointSetRenderConfig) (scene : ISg) =
        let runtime = config.runtime

        let largeSize =
            (config.size, config.pointSize) ||> AVal.map2 (fun s ps ->
                let psi = int (ceil ps)
                let psi =
                    if psi &&& 1 <> 0 then psi + 1
                    else psi

                let ps = max 32 psi
                s + V2i(ps, ps)
            )

        let largeProj =
            (config.projTrafo, config.size, largeSize) |||> AVal.map3 (fun p os ns ->
                let old = Frustum.ofTrafo p
                let factor = V2d ns / V2d os

                let frustum =
                    { old with
                        left = factor.X * old.left
                        right = factor.X * old.right
                        top = factor.Y * old.top
                        bottom = factor.Y * old.bottom
                    }
                Frustum.projTrafo frustum
            )

        let textures =
            let signature =
                runtime.CreateFramebufferSignature [
                    DefaultSemantic.Colors, TextureFormat.Rgba8
                    //DefaultSemantic.Positions, TextureFormat.Rgba32f
                    DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8
                ]

            let sems =
                Set.ofList [
                    DefaultSemantic.Colors; DefaultSemantic.DepthStencil
                ]

            let render =
                let scene =
                    scene
                    |> Sg.blendMode (AVal.constant BlendMode.None)
                    |> Sg.uniform "ShowColors" config.colors
                    |> Sg.uniform "PointSize" config.pointSize
                    |> Sg.uniform "ViewportSize" largeSize
                    |> Sg.viewTrafo config.viewTrafo
                    |> Sg.projTrafo largeProj

                let objects = Aardvark.SceneGraph.Semantics.RenderObjectSemantics.Semantic.renderObjects Aardvark.Base.Ag.Scope.Root scene

                let eff =
                    FShade.Effect.compose [
                        FShade.Effect.ofFunction DeferredPointSetShaders.lodPointSizeSimple
                        FShade.Effect.ofFunction DeferredPointSetShaders.lodPointSphereSimple
                    ]

                let objects =
                    objects |> ASet.map (fun o ->
                        match o with
                        | :? RenderObject as o ->
                            match o.Surface with
                            | Surface.Effect e ->
                                o.Surface <- Surface.Effect (FShade.Effect.compose [e; eff])
                                o :> IRenderObject
                            | _ ->
                                o :> IRenderObject
                        | _ ->
                            o
                    )

                runtime.CompileRender(signature, objects)

            let clear =
                runtime.CompileClear(signature, [DefaultSemantic.Colors, C4f(0.0f, 0.0f, 0.0f, 0.0f)], 1.0f, 0)

            RenderTask.ofList [clear; render]
            |> RenderTask.renderSemantics sems largeSize


        let color = textures.[DefaultSemantic.Colors]
        let depth = textures.[DefaultSemantic.DepthStencil]

        let nearFar =
            config.projTrafo |> AVal.map (fun t ->
                let f = Frustum.ofTrafo t
                V2d(f.near, f.far)
            )

        let randomTex =
            let img = PixImage<float32>(Col.Format.RGB, V2i.II * 512)

            let rand = RandomSystem()
            img.GetMatrix<C3f>().SetByCoord (fun _ ->
                V3d(rand.UniformV2dDirection(), 0.0).ToC3d().ToC3f()
            ) |> ignore

            runtime.PrepareTexture(PixTexture2d(PixImageMipMap [| img :> PixImage |], TextureParams.None)) :> ITexture

        let finalSg =
            Sg.fullScreenQuad
            |> Sg.texture DefaultSemantic.Colors color
            |> Sg.texture DefaultSemantic.DepthStencil depth
            //|> Sg.texture pointIdSym pointId
            |> Sg.uniform "NearFar" nearFar
            |> Sg.uniform "PlaneFit" config.planeFit
            |> Sg.uniform "SSAO" config.ssao
            |> Sg.uniform "RandomTexture" (AVal.constant randomTex)
            |> Sg.shader {
                do! DeferredPointSetShaders.blitPlaneFit
            }
            |> Sg.viewTrafo config.viewTrafo
            |> Sg.projTrafo largeProj
            |> Sg.uniform "Diffuse" config.diffuse
            |> Sg.uniform "ViewportSize" largeSize
            |> Sg.uniform "PlaneFitTolerance" config.planeFitTol
            |> Sg.uniform "PlaneFitRadius" config.planeFitRadius
            |> Sg.uniform "Gamma" config.gamma

        let sceneTextures =
            let signature =
                runtime.CreateFramebufferSignature [
                    DefaultSemantic.Colors, TextureFormat.Rgba8
                    DefaultSemantic.Normals, TextureFormat.Rgba32f
                    DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8
                ]

            let sems =
                Set.ofList [
                    DefaultSemantic.Colors; DefaultSemantic.Normals; DefaultSemantic.DepthStencil
                ]

            finalSg
            |> Sg.compile runtime signature
            |> RenderTask.renderSemantics sems largeSize



        let normals = sceneTextures.[DefaultSemantic.Normals]
        let colors = sceneTextures.[DefaultSemantic.Colors]
        let depth = sceneTextures.[DefaultSemantic.DepthStencil]

        let mutable lastDepth = None
        let depth =
            depth.Acquire()
            { new AdaptiveResource<IBackendTexture>() with
                override x.Compute(t, rt) =
                    let d = depth.GetValue(t, rt)
                    lastDepth <- Some d
                    depth.GetValue(t, rt)
                override x.Create() = depth.Acquire()
                override x.Destroy() = depth.Release()
            }


        let tt =
            (config.size, largeSize) ||> AVal.map2 (fun os ns ->
                Trafo2d.Scale(V2d os) *
                Trafo2d.Translation(V2d (ns - os) / 2.0) *
                Trafo2d.Scale(1.0 / V2d ns)
            )

        let finalSg =
            let s = largeSize // |> AVal.map (fun s -> max V2i.II (s / 2))

            SSAO.getAmbient tt config.ssao config.ssaoConfig runtime largeProj depth normals colors s
            |> Sg.uniform "SSAO" config.ssao
            |> Sg.uniform "ViewportSize" config.size
        //let mutable lastDepth : option<ITexture> = None
        let pick (px : V2i) (r : int) (maxCt : int) =
            //let normals = normals.GetValue() |> unbox<IBackendTexture>
            let depth =
                match lastDepth with
                | Some d -> d |> unbox<IBackendTexture>
                | _ -> depth.GetValue() |> unbox<IBackendTexture>

            let s = config.size.GetValue()
            let ls = largeSize.GetValue()
            let view = config.viewTrafo.GetValue()
            let proj = config.projTrafo.GetValue()



            let ndcs =
                let px = V2i(px.X, s.Y - 1 - px.Y)
                let offset = (ls - s) / 2
                let size = s
                let center = px

                Readback.readDepth depth center r offset size maxCt
            //let mat = mat.Transformed(ImageTrafo.MirrorY)

            ndcs |> Array.map (fun ndc ->
                let ndc = (V3d ndc.XYZ)
                let tc = V2d(ndc.X * 0.5 + 0.5, 0.5 - ndc.Y * 0.5)
                let px = tc * V2d(s) |> V2i
                let vp = proj.Backward.TransformPosProj ndc
                let wp = view.Backward.TransformPosProj vp
                {
                    World = wp
                    View  = vp
                    Ndc   = ndc
                    Pixel = px
                }
            )

        match config.pickCallback with
        | Some r ->
            r := pick
        | None -> ()

        finalSg

    let pointSetsFilter (config : PointSetRenderConfig) (pointClouds : aset<LodTreeInstance>) (filterPartIndex : aval<int>)=
        let runtime = config.runtime

        let largeSize =
            (config.size, config.pointSize) ||> AVal.map2 (fun s ps ->
                let psi = int (ceil ps)
                let psi =
                    if psi &&& 1 <> 0 then psi + 1
                    else psi

                let ps = max 32 psi
                s + V2i(ps, ps)
            )

        let largeProj =
            (config.projTrafo, config.size, largeSize) |||> AVal.map3 (fun p os ns ->
                let old = Frustum.ofTrafo p
                let factor = V2d ns / V2d os

                let frustum =
                    { old with
                        left = factor.X * old.left
                        right = factor.X * old.right
                        top = factor.Y * old.top
                        bottom = factor.Y * old.bottom
                    }
                Frustum.projTrafo frustum
            )

        let textures =
            let signature =
                runtime.CreateFramebufferSignature [
                    DefaultSemantic.Colors, TextureFormat.Rgba8
                    DefaultSemantic.Positions, TextureFormat.Rgba32f
                    DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8
                ]

            let sems =
                Set.ofList [
                    DefaultSemantic.Positions; DefaultSemantic.Colors; DefaultSemantic.DepthStencil
                ]

            let render =
                let cfg = config.lodConfig
                Sg.LodTreeNode(cfg.stats, cfg.pickTrees, cfg.alphaToCoverage, cfg.budget, cfg.splitfactor, cfg.renderBounds, cfg.maxSplits, cfg.time, pointClouds) :> ISg

                //Sg.lodTree config.lodConfig pointClouds
                |> Sg.shader {
                    do! DeferredPointSetShaders.colorOrWhite
                    do! DeferredPointSetShaders.lodPointSize
                    do! DeferredPointSetShaders.lodPointCircular
                }
                |> Sg.blendMode (AVal.constant BlendMode.None)
                |> Sg.uniform "ShowColors" config.colors
                |> Sg.uniform "PointSize" config.pointSize
                |> Sg.uniform "ViewportSize" largeSize
                |> Sg.uniform "FilterPartIndex" filterPartIndex
                |> Sg.viewTrafo config.viewTrafo
                |> Sg.projTrafo largeProj
                |> Sg.compile runtime signature

            let clear =
                runtime.CompileClear(signature, [DefaultSemantic.Positions, C4f(0.0f, 0.0f, 2.0f, 0.0f)], 1.0f)

            RenderTask.ofList [clear; render]
            |> RenderTask.renderSemantics sems largeSize


        let color = textures.[DefaultSemantic.Colors]
        let position = textures.[DefaultSemantic.Positions]
        let depth = textures.[DefaultSemantic.DepthStencil]
        let singlePixelDepth = depth

        //let pointIdSym = Symbol.Create "PointId"

        let sphereTextures =
            let signature =
                runtime.CreateFramebufferSignature [
                    DefaultSemantic.Colors, TextureFormat.Rgba8
                    //pointIdSym, RenderbufferFormat.R32f
                    DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8
                ]
            let sems =
                Set.ofList [
                    DefaultSemantic.Colors; DefaultSemantic.DepthStencil; //pointIdSym
                ]
            Sg.fullScreenQuad
            |> Sg.shader {
                do! DeferredPointSetShaders.fillSpheres
            }
            |> Sg.uniform "PointSize" config.pointSize
            |> Sg.uniform "ViewportSize" largeSize
            |> Sg.texture DefaultSemantic.Positions position
            |> Sg.texture DefaultSemantic.Colors color
            |> Sg.texture DefaultSemantic.DepthStencil depth
            |> Sg.viewTrafo config.viewTrafo
            |> Sg.projTrafo largeProj
            |> Sg.compile runtime signature
            |> RenderTask.renderSemantics sems largeSize


        let color = sphereTextures.[DefaultSemantic.Colors]
        //let pointId = sphereTextures.[pointIdSym]
        let depth = sphereTextures.[DefaultSemantic.DepthStencil]

        let nearFar =
            config.projTrafo |> AVal.map (fun t ->
                let f = Frustum.ofTrafo t
                V2d(f.near, f.far)
            )

        let randomTex =
            let img = PixImage<float32>(Col.Format.RGB, V2i.II * 512)

            let rand = RandomSystem()
            img.GetMatrix<C3f>().SetByCoord (fun _ ->
                V3d(rand.UniformV2dDirection(), 0.0).ToC3d().ToC3f()
            ) |> ignore

            runtime.PrepareTexture(PixTexture2d(PixImageMipMap [| img :> PixImage |], TextureParams.None)) :> ITexture

        let sceneTextures =
            let signature =
                runtime.CreateFramebufferSignature [
                    DefaultSemantic.Colors, TextureFormat.Rgba8
                    DefaultSemantic.Normals, TextureFormat.Rgba32f
                    DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8
                ]

            let sems =
                Set.ofList [
                    DefaultSemantic.Colors; DefaultSemantic.Normals; DefaultSemantic.DepthStencil
                ]

            Sg.fullScreenQuad
            |> Sg.texture DefaultSemantic.Colors color
            |> Sg.texture DefaultSemantic.DepthStencil depth
            //|> Sg.texture pointIdSym pointId
            |> Sg.uniform "NearFar" nearFar
            |> Sg.uniform "PlaneFit" config.planeFit
            |> Sg.uniform "SSAO" config.ssao
            |> Sg.uniform "RandomTexture" (AVal.constant randomTex)
            |> Sg.shader {
                do! DeferredPointSetShaders.blitPlaneFit
            }
            |> Sg.viewTrafo config.viewTrafo
            |> Sg.projTrafo largeProj
            |> Sg.uniform "Diffuse" config.diffuse
            |> Sg.uniform "ViewportSize" largeSize
            |> Sg.uniform "PlaneFitTolerance" config.planeFitTol
            |> Sg.uniform "PlaneFitRadius" config.planeFitRadius
            |> Sg.uniform "Gamma" config.gamma
            |> Sg.compile runtime signature
            |> RenderTask.renderSemantics sems largeSize

        let normals = sceneTextures.[DefaultSemantic.Normals]
        let colors = sceneTextures.[DefaultSemantic.Colors]
        let depth = sceneTextures.[DefaultSemantic.DepthStencil]

        let mutable lastDepth = None
        let depth =
            depth.Acquire()
            { new AdaptiveResource<IBackendTexture>() with
                override x.Compute(t, rt) =
                    let d = depth.GetValue(t, rt)
                    lastDepth <- Some d
                    depth.GetValue(t, rt)
                override x.Create() = depth.Acquire()
                override x.Destroy() = depth.Release()
            }

        let tt =
            (config.size, largeSize) ||> AVal.map2 (fun os ns ->
                Trafo2d.Scale(V2d os) *
                Trafo2d.Translation(V2d (ns - os) / 2.0) *
                Trafo2d.Scale(1.0 / V2d ns)
            )

        let finalSg =
            let s = largeSize

            SSAO.getAmbient tt config.ssao config.ssaoConfig runtime largeProj depth normals colors s
            |> Sg.uniform "SSAO" config.ssao
            |> Sg.uniform "ViewportSize" config.size

        let pick (px : V2i) (r : int) (maxCt : int) =
            let depth =
                match lastDepth with
                | Some d -> d |> unbox<IBackendTexture>
                | _ -> depth.GetValue() |> unbox<IBackendTexture>

            let s = config.size.GetValue()
            let ls = largeSize.GetValue()
            let view = config.viewTrafo.GetValue()
            let proj = config.projTrafo.GetValue()

            let ndcs =
                let px = V2i(px.X, s.Y - 1 - px.Y)
                let offset = (ls - s) / 2
                let size = s
                let center = px

                Readback.readDepth depth center r offset size maxCt

            ndcs |> Array.map (fun ndc ->
                let ndc = (V3d ndc.XYZ)
                let tc = V2d(ndc.X * 0.5 + 0.5, 0.5 - ndc.Y * 0.5)
                let px = tc * V2d(s) |> V2i
                let vp = proj.Backward.TransformPosProj ndc
                let wp = view.Backward.TransformPosProj vp
                {
                    World = wp
                    View  = vp
                    Ndc   = ndc
                    Pixel = px
                }
            )

        match config.pickCallback with
        | Some r ->
            r := pick
        | None -> ()

        finalSg

    let pointSets config pointClouds = pointSetsFilter config pointClouds (AVal.constant -1)
