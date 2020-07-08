namespace Aardvark.Rendering.PointSet

open System
open Aardvark.Base
open Aardvark.Base.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph

type internal SSAOConfig =
    {
        radius          : aval<float>
        threshold       : aval<float>
        sigma           : aval<float>
        sharpness       : aval<float>
        samples         : aval<int>
    }

module internal SSAO =
    
    module Semantic =
        let Ambient = Symbol.Create "Ambient"

    [<ReflectedDefinition>]
    module Shader =
        open FShade

        type UniformScope with
            member x.Radius : float = uniform?Radius
            member x.Threshold : float = uniform?Threshold
            member x.Sigma : float = uniform?Sigma
            member x.Sharpness : float = uniform?Sharpness
            member x.Gamma : float = uniform?Gamma
            member x.Samples : int = uniform?Samples
            member x.Light : V3d = uniform?Light
            member x.SampleDirections : V4d[] = uniform?StorageBuffer?SampleDirections
   
           
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
        let project (vp : V3d) =
            let mutable vp = vp
            vp.Z <- min -0.01 vp.Z
            let pp = uniform.ProjTrafo * V4d(vp, 1.0)
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
         
        [<ReflectedDefinition>]
        let getAmbient (ndc : V2d) =
            let tc = 0.5 * (ndc + V2d.II)
            ambient.SampleLevel(tc, 0.0)



        //let normal =
        //    sampler2d {
        //        texture uniform?Normals
        //        addressU WrapMode.Clamp
        //        addressV WrapMode.Clamp
        //        filter Filter.MinMagLinear
        //    }
            
        let color =
            sampler2d {
                texture uniform?DiffuseColorTexture
                addressU WrapMode.Clamp
                addressV WrapMode.Clamp
                filter Filter.MinMagLinear
            }

        let depth =
            sampler2d {
                texture uniform?Depth
                addressU WrapMode.Clamp
                addressV WrapMode.Clamp
                filter Filter.MinMagLinear
            }

        let depthCmp =
            sampler2dShadow {
                texture uniform?Depth
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

                let z = 2.0 * depth.SampleLevel(v.tc, 0.0).X - 1.0
                let vn = normal.SampleLevel(v.tc, 0.0).XYZ 
                let l = Vec.length vn
                if l < 0.001 then
                    return V4d.IIII
                else
                    let vn = vn / l
                

                    let pp = V4d(ndc.X, ndc.Y, z, 1.0)
            
                    let vp = 
                        let temp = uniform.ProjTrafoInv * pp
                        temp.XYZ / temp.W
                    let vn = if Vec.dot vn -vp < 0.0 then -vn else vn

                    let x = random.Sample(pp.XY).XYZ |> Vec.normalize
                    let z = vn
                    let y = Vec.cross z x |> Vec.normalize
                    let x = Vec.cross y z |> Vec.normalize
                    
                    let mutable occlusion = 0.0
                    let mutable cnt = 0
                    for si in 0 .. uniform.Samples - 1 do

                        let dir = uniform.SampleDirections.[si].XYZ * uniform.Radius
                        let p = vp + x * dir.X + y * dir.Y + z * dir.Z
              
                        let f = 1.0 - uniform.Threshold / -p.Z
                        let pp = 0.5 * (project p + V3d.III)
                        let ppo = 0.5 * (project (p * f) + V3d.III)
                        if depthCmp.Sample(pp.XY, ppo.Z) < 0.5 then
                            occlusion <- occlusion + depthCmp.Sample(pp.XY, pp.Z)
                            cnt <- cnt + 1

                    let occlusion = occlusion / float cnt
                    let ambient = 1.0 - occlusion
                
                    return V4d(ambient, ambient, ambient, 1.0)
            }

            
        
        //[<ReflectedDefinition>]
        //let blurFunction (ndc : V2d) (r : float) (centerC : V4d) (centerD : V4d) (w : float) =
            

        [<ReflectedDefinition>]
        let getLinearDepth (ndc : V2d) =
            let tc = 0.5 * (ndc + V2d.II)
            let z = 2.0 * depth.SampleLevel(tc, 0.0).X - 1.0

            let pp = V4d(ndc.X, ndc.Y, z, 1.0) 
            let temp = uniform.ProjTrafoInv * pp
            temp.Z / temp.W
            

        let blur (v : Effects.Vertex) =
            fragment {
                let s = 2.0 / V2d ambient.Size
                let ndc = v.pos.XY / v.pos.W
                

                let sigmaPos = uniform.Sigma
                if sigmaPos <= 0.0 then
                    return getAmbient ndc
                else
                    let sigmaPos2 = sigmaPos * sigmaPos
                    let sharpness = uniform.Sharpness
                    let sharpness2 = sharpness * sharpness
                    let r = 4
                    let d0 = getLinearDepth ndc
                    let mutable sum = V4d.Zero
                    let mutable wsum = 0.0
                    for x in -r .. r do
                        for y in -r .. r do
                            let deltaPos = V2d(x,y) * s
                            let pos = ndc + deltaPos

                            let deltaDepth = getLinearDepth pos - d0
                            let value = getAmbient pos

                            let wp = exp (-V2d(x,y).LengthSquared / sigmaPos2)
                            let wd = exp (-deltaDepth*deltaDepth * sharpness2)

                            let w = wp * wd

                            sum <- sum + w * value
                            wsum <- wsum + w



                    return sum / wsum
            }

        
        type Fragment =
            {
                [<Color>]
                color : V4d

                [<Depth>]
                depth : float
            }


        open FXAA

        let compose (v : Effects.Vertex) =
            fragment {
                let d = depth.SampleLevel(v.tc, 0.0).X
                if d > 0.99999 then discard()

                let a = if uniform?SSAO then ambient.Sample(v.tc).X else 1.0

                let c =
                    color.SampleLevelFXAA(
                        v.tc, 0.0,
                        FXAA.PRESET.Quality29,
                        (float (FXAA.getEdgeThreshold FXAA.EDGETHRESHOLD.DEFAULT)),
                        (float (FXAA.getEdgeThresholdMin FXAA.EDGETHRESHOLDMIN.DEFAULT)),
                        (float (FXAA.getSubpixParam FXAA.SUBPIX.DEFAULT))
                    )


                return { color = V4d(a * c.XYZ, c.W); depth = d }
            }

    let getAmbient (enabled : aval<bool>) (config : SSAOConfig) (runtime : IRuntime) (proj : aval<Trafo3d>) (depth : IOutputMod<ITexture>) (normals : IOutputMod<ITexture>) (colors : IOutputMod<ITexture>) (size : aval<V2i>)  =
        let ambientSignature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, RenderbufferFormat.Rgba8
            ]

        let randomTex = 
            let img = PixImage<float32>(Col.Format.RGB, V2i.II * 512)

            let rand = RandomSystem()
            img.GetMatrix<C3f>().SetByCoord (fun _ ->
                rand.UniformV3dDirection().ToC3d().ToC3f()
            ) |> ignore

            runtime.PrepareTexture(PixTexture2d(PixImageMipMap [| img :> PixImage |], TextureParams.empty))

        let ambient = 
            Sg.fullScreenQuad
                |> Sg.shader {  
                    do! Shader.ambientOcclusion
                }
                |> Sg.texture DefaultSemantic.Depth depth
                |> Sg.texture DefaultSemantic.Normals normals
                |> Sg.projTrafo proj
                |> Sg.uniform "SampleDirections" (AVal.constant Shader.sampleDirections)
                |> Sg.uniform "Random" (AVal.constant (randomTex :> ITexture))
                |> Sg.uniform "Radius" config.radius
                |> Sg.uniform "Threshold" config.threshold
                |> Sg.uniform "Samples" config.samples
                |> Sg.uniform "ViewportSize" size
                |> Sg.compile runtime ambientSignature
                |> RenderTask.renderToColor size

        let blurredAmbient =
            let task = 
                Sg.fullScreenQuad
                |> Sg.shader {
                    do! Shader.blur                    
                }
                |> Sg.texture DefaultSemantic.Depth depth
                |> Sg.texture Semantic.Ambient ambient
                |> Sg.projTrafo proj
                |> Sg.uniform "Radius" config.radius
                |> Sg.uniform "Threshold" config.threshold
                |> Sg.uniform "Sigma" config.sigma
                |> Sg.uniform "Sharpness" config.sharpness
                |> Sg.uniform "ViewportSize" size
                |> Sg.compile runtime ambientSignature

            let clear =
                runtime.CompileClear(ambientSignature, AVal.constant C4f.White)

            RenderTask.ofList [
                clear

                RenderTask.custom (fun (t, rt, o) ->
                    if enabled.GetValue t then
                        task.Run(t, rt, o)
                )
            ]
            |> RenderTask.renderToColor size
                
        Sg.fullScreenQuad
        |> Sg.shader {
            do! Shader.compose                    
        }
        |> Sg.texture DefaultSemantic.Depth depth
        |> Sg.diffuseTexture colors
        |> Sg.texture Semantic.Ambient blurredAmbient
        |> Sg.projTrafo proj