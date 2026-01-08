namespace Aardvark.Rendering.PointSet

open System
open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph

type SSAOConfig =
    {
        radius              : aval<float>
        threshold           : aval<float>
        sigma               : aval<float>
        sharpness           : aval<float>
        sampleDirections    : aval<int>
        samples             : aval<int>
    }

module internal SSAO =

    module Semantic =
        let Ambient = Symbol.Create "Ambient"

    [<ReflectedDefinition>]
    module Shader =
        open FShade

        type UniformScope with
            member x.Radius : float32 = uniform?SSAO?Radius
            member x.Threshold : float32 = uniform?SSAO?Threshold
            member x.Sigma : float32 = uniform?SSAO?Sigma
            member x.Sharpness : float32 = uniform?SSAO?Sharpness
            member x.Gamma : float32 = uniform?SSAO?Gamma
            member x.Samples : int = uniform?SSAO?Samples
            member x.SampleDirectionCount : int = uniform?SSAO?SampleDirectionCount
            member x.Light : V3f = uniform?Light
            member x.SampleDirections : Arr<N<32>, V4f> = uniform?SampleDirections

            member x.FilterWeights : Arr<N<32>, V4f> = uniform?FilterWeights
            member x.FilterRadius : int = uniform?SSAO?FilterRadius
            member x.FilterDirection : V2f = uniform?SSAO?FilterDirection



        let sampleDirections =
            let rand = RandomSystem()
            let arr =
                Array.init 512 (fun _ ->
                    let phi = rand.UniformDouble() * Constant.PiTimesTwo
                    let theta = rand.UniformDouble() * (Constant.PiHalf - 10.0 * Constant.RadiansPerDegree)
                    V3d(
                        cos phi * sin theta,
                        sin phi * sin theta,
                        cos theta
                    )
                )
            arr
            |> Array.map (fun v -> v * (0.5 + 0.5 * rand.UniformDouble()))
            |> Array.map (fun v -> V4f(V3f v, 1.0f))

        [<ReflectedDefinition>]
        let project (vp : V3f) =
            let mutable vp = vp
            vp.Z <- min -0.01f vp.Z
            let pp = uniform.ProjTrafo * V4f(vp, 1.0f)
            pp.XYZ / pp.W


        let random =
            sampler2d {
                texture uniform?Random
                addressU WrapMode.Wrap
                addressV WrapMode.Wrap
                filter Filter.MinMagPoint
            }



        let ambient =
            sampler2d {
                texture uniform?Ambient
                addressU WrapMode.Clamp
                addressV WrapMode.Clamp
                filter Filter.MinMagLinear
            }

        [<ReflectedDefinition; Inline>]
        let getAmbient (tc : V2f) =
            //let tc = 0.5 * (ndc + V2d.II)
            ambient.SampleLevel(tc, 0.0f).X


        let color =
            sampler2d {
                texture uniform?DiffuseColorTexture
                addressU WrapMode.Clamp
                addressV WrapMode.Clamp
                filter Filter.MinMagLinear
            }

        let depth =
            sampler2d {
                texture uniform?DepthStencil
                addressU WrapMode.Clamp
                addressV WrapMode.Clamp
                filter Filter.MinMagLinear
            }


        let linearDepth =
            sampler2d {
                texture uniform?LinearDepth
                addressU WrapMode.Clamp
                addressV WrapMode.Clamp
                filter Filter.MinMagLinear
            }

        let depthCmp =
            sampler2dShadow {
                texture uniform?DepthStencil
                addressU WrapMode.Clamp
                addressV WrapMode.Clamp
                comparison ComparisonFunction.Greater
                filter Filter.MinMagMipLinear
            }

        let normal =
            sampler2d {
                texture uniform?Normals
                addressU WrapMode.Clamp
                addressV WrapMode.Clamp
                filter Filter.MinMagPoint
            }

        let ambientOcclusion (v : Effects.Vertex) =
            fragment {
                let ndc = v.pos.XY / v.pos.W

                let z = 2.0f * depth.SampleLevel(v.tc, 0.0f).X - 1.0f
                let vn = normal.SampleLevel(v.tc, 0.0f).XYZ
                let l = Vec.length vn
                if l < 0.001f then
                    return V4f.IIII
                else
                    let vn = vn / l
                    let pp = V4f(ndc.X, ndc.Y, z, 1.0f)

                    let vp =
                        let temp = uniform.ProjTrafoInv * pp
                        temp.XYZ / temp.W
                    let vn = if Vec.dot vn -vp < 0.0f then -vn else vn

                    let x = random.Sample(pp.XY).XYZ |> Vec.normalize
                    let z = vn
                    let y = Vec.cross z x |> Vec.normalize
                    let x = Vec.cross y z |> Vec.normalize

                    let mutable occlusion = 0.0f
                    let mutable cnt = 0
                    for si in 0 .. uniform.Samples - 1 do

                        let dir = uniform.SampleDirections.[si].XYZ * uniform.Radius
                        let p = vp + x * dir.X + y * dir.Y + z * dir.Z

                        let f = 1.0f - uniform.Threshold / -p.Z
                        let pp = 0.5f * (project p + V3f.III)
                        let ppo = 0.5f * (project (p * f) + V3f.III)
                        if depthCmp.Sample(pp.XY, ppo.Z) < 0.5f then
                            occlusion <- occlusion + depthCmp.Sample(pp.XY, pp.Z)
                            cnt <- cnt + 1

                    let occlusion = occlusion / float32 cnt
                    let ambient = 1.0f - occlusion

                    return V4f(ambient, ambient, ambient, 1.0f)
            }

        type BlurVertex =
            {
                [<FragCoord>]
                fc : V4f
            }


        [<Inline>]
        let getViewPos (tc : V2f) =
            let d = depth.SampleLevel(tc, 0.0f).X
            let pp = 2.0f * V3f(tc, d) - V3f.III
            let vp = uniform.ProjTrafoInv * V4f(pp, 1.0f)
            vp.XYZ / vp.W




        let sampleDirections2d =
            let rand = RandomSystem()
            Array.append
                [|V4f.IOOO; V4f.OIOO; V4f.IIOO.Normalized; V4f.PNOO.Normalized |]
                (Array.init 128 (fun _ -> V4f(rand.UniformV2fDirection(), 0.0f, 0.0f)))


        let hbao (v : BlurVertex) =
            fragment {
                let size = V2f uniform.ViewportSize
                let invSize = 1.0f / size
                let tc = v.fc.XY * invSize
                let ndc = 2.0f * tc - V2f.II

                let d = depth.SampleLevel(tc, 0.0f).X
                if d >= 0.99999f then
                    return V4f.IIII
                else
                    let pp = 2.0f * V3f(tc, d) - V3f.III
                    let vp = uniform.ProjTrafoInv * V4f(pp, 1.0f)
                    let vp = vp.XYZ / vp.W

                    let p0 =
                        let pp = uniform.ProjTrafo * V4f(vp + V3f(uniform.Radius, 0.0f, 0.0f), 1.0f)
                        0.5f * (pp.XY / pp.W - ndc) * size

                    let p1 =
                        let pp = uniform.ProjTrafo * V4f(vp + V3f(0.0f, uniform.Radius, 0.0f), 1.0f)
                        0.5f * (pp.XY / pp.W - ndc) * size

                    let radius = 0.5f * (Vec.length p0 + Vec.length p1)
                    let step = radius / float32 uniform.Samples

                    let normal = normal.SampleLevel(tc, 0.0f).XYZ |> Vec.normalize


                    //let rand = random.SampleLevel(v.fc.XY / V2d random.Size, 0.0f)
                    let mutable sum = 0.0f

                    for di in 0 .. uniform.SampleDirectionCount - 1 do
                        let dir = uniform.SampleDirections.[di].XY
                        //let dir = rand.XY * dir.X + rand.ZW * dir.Y

                        let mutable h0 = -ConstantF.Pi
                        let mutable h1 = -ConstantF.Pi

                        //let stepPixel = dir * step
                        //let stepPixel =
                        //    if stepPixel.X < 1.0f && stepPixel.Y < 1.0f then
                        //        stepPixel / max stepPixel.X stepPixel.Y
                        //    else
                        //        stepPixel

                        //let step = stepPixel * invSize

                        let step = dir * step * invSize

                        for o in 1 .. uniform.Samples do
                            let tco = tc + float32 o * step
                            let vpo = getViewPos tco

                            let delta = vpo - vp
                            let dz = Vec.dot normal delta

                            if dz > 0.0f && dz <= uniform.Threshold then
                                let angle = atan (delta.Z / Vec.length delta.XY)
                                h1 <- max h1 angle

                        for o in 1 .. uniform.Samples do
                            let tco = tc - float32 o * step
                            let vpo = getViewPos tco

                            let delta = vpo - vp
                            let dz = Vec.dot normal delta

                            if dz > 0.0f && dz <= uniform.Threshold then
                                let angle = atan (delta.Z / Vec.length delta.XY)
                                h0 <- max h0 angle


                        let occ = (ConstantF.Pi - h0 - h1) / ConstantF.Pi

                        sum <- sum + clamp 0.0f 1.0f occ


                    let res = sum / float32 uniform.SampleDirectionCount
                    return V4f(res, res, res, 1.0f)
            }

        [<ReflectedDefinition; Inline>]
        let getLinearDepth (tc : V2f) =
            //let tc = 0.5 * (ndc + V2d.II)
            let z = 2.0f * depth.SampleLevel(tc, 0.0f).X - 1.0f

            //let pp = V4d(ndc.X, ndc.Y, z, 1.0)
            let a = uniform.ProjTrafoInv.M22 * z + uniform.ProjTrafoInv.M23
            let b = uniform.ProjTrafoInv.M32 * z + uniform.ProjTrafoInv.M33
            a / b


        let linearizeDepth (v : BlurVertex) =
            fragment {
                let size = V2f depth.Size
                let tc = v.fc.XY / size
                let d = getLinearDepth tc

                return V4f(d,d,d,1.0f)
            }


        let blur (v : BlurVertex) =
            fragment {
                let size = V2f uniform.ViewportSize
                let tc = v.fc.XY / size

                if uniform.Sigma <= 0.0f then
                    let a = getAmbient tc
                    return V4f(a,a,a,1.0f)
                else
                    let sharpness2 = uniform.Sharpness * uniform.Sharpness

                    let cd = getLinearDepth tc
                    let mutable sum = getAmbient tc
                    let mutable wsum = 1.0f

                    let mutable tc1 = tc + uniform.FilterDirection
                    for oi in 1 .. uniform.FilterRadius do
                        let deltaDepth = getLinearDepth tc1 - cd
                        let value = getAmbient tc1
                        let w = uniform.FilterWeights.[oi].X * exp (-deltaDepth*deltaDepth * sharpness2)

                        sum <- sum + w * value
                        wsum <- wsum + w
                        tc1 <- tc1 + uniform.FilterDirection

                    tc1 <- tc - uniform.FilterDirection
                    for oi in 1 .. uniform.FilterRadius do
                        let deltaDepth = getLinearDepth tc1 - cd
                        let value = getAmbient tc1
                        let w = uniform.FilterWeights.[oi].X * exp (-deltaDepth*deltaDepth * sharpness2)

                        sum <- sum + w * value
                        wsum <- wsum + w
                        tc1 <- tc1 - uniform.FilterDirection

                    let res = sum / wsum
                    return V4f(res, res, res, 1.0f)
            }

        type Fragment =
            {
                [<Color>]
                color : V4f

                [<Depth>]
                depth : float32
            }


        open FXAA

        let compose (v : Effects.Vertex) =
            fragment {

                let tt : M33f = uniform?TextureTrafo
                let tc = tt * V3f(v.tc, 1.0f) |> Vec.xy

                let d = depth.SampleLevel(tc, 0.0f).X
                if d > 0.99999f then discard()

                let a = if uniform?SSAO then ambient.Sample(tc).X ** 1.5f else 1.0f

                let c =
                    color.SampleLevelFXAA(
                        tc, 0.0f,
                        FXAA.PRESET.Quality29,
                        (float32 (FXAA.getEdgeThreshold FXAA.EDGETHRESHOLD.DEFAULT)),
                        (float32 (FXAA.getEdgeThresholdMin FXAA.EDGETHRESHOLDMIN.DEFAULT)),
                        (float32 (FXAA.getSubpixParam FXAA.SUBPIX.DEFAULT))
                    )


                return { color = V4f(a * c.XYZ, c.W); depth = d }
            }

    let inline getAmbient
        (texCoords : aval<Trafo2d>)
        (enabled : aval<bool>)
        (config : SSAOConfig)
        (runtime : IRuntime)
        (proj : aval<Trafo3d>)
        (depth : aval<#ITexture>)
        (normals : aval<#ITexture>)
        (colors : aval<#ITexture>)
        (size : aval<V2i>)  =

        let fullSize = depth |> AVal.map (fun d -> (unbox<IBackendTexture> d).Size.XY)
        let fullSizeX = (fullSize, size) ||> AVal.map2 (fun f h -> V2i(f.X, h.Y))

        let randomTex =
            let img = PixImage<float32>(Col.Format.RGBA, V2i.II * 512)

            let rand = RandomSystem()
            img.GetMatrix<C4f>().SetByCoord (fun _ ->
                let a = rand.UniformV2dDirection()
                let b = V2d(-a.Y, a.X)
                V4d(a,b).ToC4f()
            ) |> ignore

            runtime.PrepareTexture(PixTexture2d(PixImageMipMap [| img :> PixImage |], TextureParams.None))

        let ambientSignature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.R8
            ]

        let samples =
            config.samples |> AVal.map (clamp 1 32)

        let sampleDirections =
            config.sampleDirections |> AVal.map (clamp 1 32)

        let ambient =
            Sg.fullScreenQuad
                |> Sg.shader {
                    do! Shader.hbao
                }
                |> Sg.texture DefaultSemantic.DepthStencil depth
                |> Sg.texture DefaultSemantic.Normals normals
                |> Sg.projTrafo proj
                |> Sg.uniform "SampleDirections" (AVal.constant Shader.sampleDirections2d)
                |> Sg.uniform "Random" (AVal.constant (randomTex :> ITexture))
                |> Sg.uniform "Radius" config.radius
                |> Sg.uniform "Threshold" config.threshold
                |> Sg.uniform "Samples" samples
                |> Sg.uniform "SampleDirectionCount" sampleDirections
                |> Sg.uniform "ViewportSize" size
                |> Sg.compile runtime ambientSignature
                |> RenderTask.renderToColor size

        let filterRadius =
            config.sigma |> AVal.map (fun s ->
                ceil (2.0 * s + 1.0) |> int
            )

        let filterWeights =
            (config.sigma, filterRadius) ||> AVal.map2 (fun s r ->
                Array.init (r + 1) (fun i ->
                    let v = exp (-float (sqr i) / sqr s)
                    V4f(v,v,v,v)
                )
            )

        let filterDirectionX =
            fullSize |> AVal.map (fun s -> V2d(1.0 / float s.X, 0.0))

        let filterDirectionY =
            fullSize |> AVal.map (fun s -> V2d(0.0, 1.0 / float s.Y))

        let blurredX =
            let task =
                Sg.fullScreenQuad
                |> Sg.shader {
                    do! Shader.blur
                }
                |> Sg.texture DefaultSemantic.DepthStencil depth
                |> Sg.texture Semantic.Ambient ambient
                |> Sg.projTrafo proj
                |> Sg.uniform "FilterRadius" filterRadius
                |> Sg.uniform "FilterWeights" filterWeights
                |> Sg.uniform "FilterDirection" filterDirectionX
                |> Sg.uniform "Radius" config.radius
                |> Sg.uniform "Threshold" config.threshold
                |> Sg.uniform "Sigma" config.sigma
                |> Sg.uniform "Sharpness" config.sharpness
                |> Sg.uniform "ViewportSize" fullSizeX
                |> Sg.compile runtime ambientSignature

            task |> RenderTask.renderToColor fullSizeX

        let blurredY =
            let task =
                Sg.fullScreenQuad
                |> Sg.shader {
                    do! Shader.blur
                }
                |> Sg.texture DefaultSemantic.DepthStencil depth
                |> Sg.texture Semantic.Ambient blurredX
                |> Sg.projTrafo proj
                |> Sg.uniform "FilterRadius" filterRadius
                |> Sg.uniform "FilterWeights" filterWeights
                |> Sg.uniform "FilterDirection" filterDirectionY
                |> Sg.uniform "Radius" config.radius
                |> Sg.uniform "Threshold" config.threshold
                |> Sg.uniform "Sigma" config.sigma
                |> Sg.uniform "Sharpness" config.sharpness
                |> Sg.uniform "ViewportSize" fullSize
                |> Sg.compile runtime ambientSignature

            task |> RenderTask.renderToColor fullSize

        let result =
            {
                new AdaptiveResource<ITexture>() with
                    member x.Create() = blurredY.Acquire()
                    member x.Destroy() = blurredY.Release()
                    member x.Compute(t, rt) =
                        if enabled.GetValue t then
                            blurredY.GetValue(t, rt) :> ITexture
                        else
                            nullTexture

            }

        Sg.fullScreenQuad
        |> Sg.shader {
            do! Shader.compose
        }
        |> Sg.uniform "TextureTrafo" (texCoords |> AVal.map (fun t -> t.Forward))
        |> Sg.texture DefaultSemantic.DepthStencil depth
        |> Sg.diffuseTexture colors
        |> Sg.texture Semantic.Ambient result
        |> Sg.projTrafo proj
