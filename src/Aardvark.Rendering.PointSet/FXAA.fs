namespace Aardvark.Rendering.PointSet

module internal FXAA =
    open System.Runtime.CompilerServices
    open FShade
    open Aardvark.Base

    let EdgeThreshold    = TypedSymbol<float32> "EdgeThreshold"
    let EdgeThresholdMin = TypedSymbol<float32> "EdgeThresholdMin"
    let Subpix           = TypedSymbol<float32> "Subpix"

    type UniformScope with
        // Only used on FXAA Quality.
        // This used to be the FXAA_QUALITY__EDGE_THRESHOLD define.
        // It is here now to allow easier tuning.
        // The minimum amount of local contrast required to apply algorithm.
        //   0.333 - too little (faster)
        //   0.250 - low quality
        //   0.166 - default
        //   0.125 - high quality
        //   0.063 - overkill (slower)
        member x.EdgeThreshold     : float32 = x?Global?EdgeThreshold

        // Only used on FXAA Quality.
        // This used to be the FXAA_QUALITY__EDGE_THRESHOLD_MIN define.
        // It is here now to allow easier tuning.
        // Trims the algorithm from processing darks.
        //   0.0833 - upper limit (default, the start of visible unfiltered edges)
        //   0.0625 - high quality (faster)
        //   0.0312 - visible limit (slower)
        // Special notes when using FXAA_GREEN_AS_LUMA,
        //   Likely want to set this to zero.
        //   As colors that are mostly not-green
        //   will appear very dark in the green channel!
        //   Tune by looking at mostly non-green content,
        //   then start at zero and increase until aliasing is a problem.
        member x.EdgeThresholdMin  : float32 = x?Global?EdgeThresholdMin

        // Only used on FXAA Quality.
        // This used to be the FXAA_QUALITY__SUBPIX define.
        // It is here now to allow easier tuning.
        // Choose the amount of sub-pixel aliasing removal.
        // This can effect sharpness.
        //   1.00 - upper limit (softer)
        //   0.75 - default amount of filtering
        //   0.50 - lower limit (sharper, less sub-pixel aliasing removal)
        //   0.25 - almost off
        //   0.00 - completely off
        member x.Subpix            : float32 = x?Global?Subpix

    type EDGETHRESHOLD =
        | LOWER = 0
        | LOW = 1
        | DEFAULT = 2
        | HIGH = 3
        | OVERKILL = 4

    let getEdgeThreshold(config : EDGETHRESHOLD) =
        match config with
        | EDGETHRESHOLD.LOWER    -> 0.333f
        | EDGETHRESHOLD.LOW      -> 0.250f
        | EDGETHRESHOLD.DEFAULT  -> 0.166f
        | EDGETHRESHOLD.HIGH     -> 0.125f
        | EDGETHRESHOLD.OVERKILL -> 0.063f
        | _ -> 0.333f

    type EDGETHRESHOLDMIN =
        | DEFAULT = 0
        | QUALITY = 1
        | LIMIT = 2

    let getEdgeThresholdMin(config : EDGETHRESHOLDMIN) =
        match config with
        | EDGETHRESHOLDMIN.DEFAULT -> 0.0833f
        | EDGETHRESHOLDMIN.QUALITY -> 0.0625f
        | EDGETHRESHOLDMIN.LIMIT   -> 0.0312f
        | _ -> 0.0833f

    type SUBPIX =
        | SOFT = 0
        | DEFAULT = 1
        | LOW = 2
        | LOWER = 3
        | OFF = 4

    let getSubpixParam(config : SUBPIX) =
        match config with
        | SUBPIX.SOFT    -> 1.00f
        | SUBPIX.DEFAULT -> 0.75f
        | SUBPIX.LOW     -> 0.50f
        | SUBPIX.LOWER   -> 0.25f
        | SUBPIX.OFF     -> 0.00f
        | _ -> 0.0f

    type PRESET =
        | Dither10 = 0
        | Dither11 = 1
        | Dither12 = 2
        | Dither13 = 3
        | Dither14 = 4
        | Dither15 = 5
        | Quality20 = 6
        | Quality21 = 7
        | Quality22 = 8
        | Quality23 = 9
        | Quality24 = 10
        | Quality25 = 11
        | Quality26 = 12
        | Quality27 = 13
        | Quality28 = 14
        | Quality29 = 15
        | Extreme39 = 16

    let private FXAA_PRESET_10__P =
        [|
            1.5f
            3.0f
            12.0f
        |]

    let private FXAA_PRESET_11__P =
        [|
            1.0f
            1.5f
            3.0f
            12.0f
        |]

    let private FXAA_PRESET_12__P =
        [|
            1.0f
            1.5f
            2.0f
            4.0f
            12.0f
        |]

    let private FXAA_PRESET_13__P =
        [|
            1.0f
            1.5f
            2.0f
            2.0f
            4.0f
            12.0f
        |]

    let private FXAA_PRESET_14__P =
        [|
            1.0f
            1.5f
            2.0f
            2.0f
            2.0f
            4.0f
            12.0f
        |]

    let private FXAA_PRESET_15__P =
        [|
            1.0f
            1.5f
            2.0f
            2.0f
            2.0f
            2.0f
            4.0f
            12.0f
        |]

    let private FXAA_PRESET_20__P =
        [|
            1.0f
            1.5f
            8.0f
        |]

    let private FXAA_PRESET_21__P =
        [|
            1.0f
            1.5f
            2.0f
            8.0f
        |]

    let private FXAA_PRESET_22__P =
        [|
            1.0f
            1.5f
            2.0f
            2.0f
            8.0f
        |]

    let private FXAA_PRESET_23__P =
        [|
            1.0f
            1.5f
            2.0f
            2.0f
            2.0f
            8.0f
        |]

    let private FXAA_PRESET_24__P =
        [|
            1.0f
            1.5f
            2.0f
            2.0f
            2.0f
            3.0f
            8.0f
        |]

    let private FXAA_PRESET_25__P =
        [|
            1.0f
            1.5f
            2.0f
            2.0f
            2.0f
            2.0f
            4.0f
            8.0f
        |]

    let private FXAA_PRESET_26__P =
        [|
            1.0f
            1.5f
            2.0f
            2.0f
            2.0f
            2.0f
            2.0f
            4.0f
            8.0f
        |]

    let private FXAA_PRESET_27__P =
        [|
            1.0f
            1.5f
            2.0f
            2.0f
            2.0f
            2.0f
            2.0f
            2.0f
            4.0f
            8.0f
        |]

    let private FXAA_PRESET_28__P =
        [|
            1.0f
            1.5f
            2.0f
            2.0f
            2.0f
            2.0f
            2.0f
            2.0f
            2.0f
            4.0f
            8.0f
        |]

    let private FXAA_PRESET_29__P =
        [|
            1.0f
            1.5f
            2.0f
            2.0f
            2.0f
            2.0f
            2.0f
            2.0f
            2.0f
            2.0f
            4.0f
            8.0f
        |]

    let private FXAA_PRESET_39__P =
        [|
            1.0f
            1.0f
            1.0f
            1.0f
            1.0f
            1.5f
            2.0f
            2.0f
            2.0f
            2.0f
            4.0f
            8.0f
        |]

    [<GLSLIntrinsic("{0} = {1}")>] [<KeepCall>]
    let set (a: 'a) (b : 'a) = onlyInShaderCode "set"

    [<GLSLIntrinsic("break")>] [<KeepCall>]
    let brk() = onlyInShaderCode "break"

    [<AbstractClass; Sealed; Extension>]
    type Sampler2dExtensions private() =

        [<Extension; ReflectedDefinition; Inline>]
        static member SampleLevelFXAA(x : Sampler2d, fragCoord : V2f, level : float32, preset : PRESET, edgeThreshold : float32, edgeThresholdMin : float32, subpix : float32) =
            let inverseVP = 1.0f / V2f (x.GetSize (int level))
            let luma = V3f(0.299f, 0.587f, 0.114f)

            let P =
                match preset with
                | PRESET.Dither10 -> FXAA_PRESET_10__P
                | PRESET.Dither11 -> FXAA_PRESET_11__P
                | PRESET.Dither12 -> FXAA_PRESET_12__P
                | PRESET.Dither13 -> FXAA_PRESET_13__P
                | PRESET.Dither14 -> FXAA_PRESET_14__P
                | PRESET.Dither15 -> FXAA_PRESET_15__P
                | PRESET.Quality20 -> FXAA_PRESET_20__P
                | PRESET.Quality21 -> FXAA_PRESET_21__P
                | PRESET.Quality22 -> FXAA_PRESET_22__P
                | PRESET.Quality23 -> FXAA_PRESET_23__P
                | PRESET.Quality24 -> FXAA_PRESET_24__P
                | PRESET.Quality25 -> FXAA_PRESET_25__P
                | PRESET.Quality26 -> FXAA_PRESET_26__P
                | PRESET.Quality27 -> FXAA_PRESET_27__P
                | PRESET.Quality28 -> FXAA_PRESET_28__P
                | PRESET.Quality29 -> FXAA_PRESET_29__P
                | PRESET.Extreme39 -> FXAA_PRESET_39__P
                | _ -> FXAA_PRESET_10__P

            let mutable posM = fragCoord;
            let mutable lumaS = Vec.dot (x.SampleLevel(posM + V2f( 0.0f,  1.0f) * inverseVP, level).XYZ) luma
            let mutable lumaE = Vec.dot (x.SampleLevel(posM + V2f( 1.0f,  0.0f) * inverseVP, level).XYZ) luma
            let mutable lumaN = Vec.dot (x.SampleLevel(posM + V2f( 0.0f, -1.0f) * inverseVP, level).XYZ) luma
            let mutable lumaW = Vec.dot (x.SampleLevel(posM + V2f(-1.0f,  0.0f) * inverseVP, level).XYZ) luma
            let rgbM = x.SampleLevel(posM, 0.0f)
            let lumaM = Vec.dot rgbM.XYZ luma

            let maxSM = max lumaS lumaM
            let minSM = min lumaS lumaM
            let maxESM = max lumaE maxSM
            let minESM = min lumaE minSM
            let maxWN = max lumaN lumaW
            let minWN = min lumaN lumaW
            let rangeMax = max maxWN maxESM
            let rangeMin = min minWN minESM
            let rangeMaxScaled = rangeMax * edgeThreshold
            let range = rangeMax - rangeMin
            let rangeMaxClamped = max edgeThresholdMin rangeMaxScaled
            let earlyExit = range < rangeMaxClamped

            if (earlyExit) then
                rgbM
            else
                let lumaNW = Vec.dot (x.SampleLevel(posM + V2f(-1.0f, -1.0f) * inverseVP, level).XYZ) luma
                let lumaSE = Vec.dot (x.SampleLevel(posM + V2f( 1.0f,  1.0f) * inverseVP, level).XYZ) luma
                let lumaNE = Vec.dot (x.SampleLevel(posM + V2f( 1.0f, -1.0f) * inverseVP, level).XYZ) luma
                let lumaSW = Vec.dot (x.SampleLevel(posM + V2f(-1.0f,  1.0f) * inverseVP, level).XYZ) luma

                let lumaNS = lumaN + lumaS
                let lumaWE = lumaW + lumaE
                let subpixRcpRange = 1.0f/range
                let subpixNSWE = lumaNS + lumaWE
                let edgeHorz1 = (-2.0f * lumaM) + lumaNS
                let edgeVert1 = (-2.0f * lumaM) + lumaWE

                let lumaNESE = lumaNE + lumaSE
                let lumaNWNE = lumaNW + lumaNE
                let edgeHorz2 = (-2.0f * lumaE) + lumaNESE
                let edgeVert2 = (-2.0f * lumaN) + lumaNWNE

                let lumaNWSW = lumaNW + lumaSW
                let lumaSWSE = lumaSW + lumaSE
                let edgeHorz4 = (abs edgeHorz1 * 2.0f) + abs edgeHorz2
                let edgeVert4 = (abs edgeVert1 * 2.0f) + abs edgeVert2
                let edgeHorz3 = (-2.0f * lumaW) + lumaNWSW
                let edgeVert3 = (-2.0f * lumaS) + lumaSWSE
                let edgeHorz = abs(edgeHorz3) + edgeHorz4
                let edgeVert = abs(edgeVert3) + edgeVert4

                let subpixNWSWNESE = lumaNWSW + lumaNESE
                let mutable lengthSign = inverseVP.X
                let horzSpan = edgeHorz >= edgeVert
                let subpixA = subpixNSWE * 2.0f + subpixNWSWNESE

                if not horzSpan then lumaN <- lumaW
                if not horzSpan then lumaS <- lumaE
                if horzSpan then lengthSign <- inverseVP.Y
                let subpixB = (subpixA * (1.0f/12.0f)) - lumaM

                let gradientN = lumaN - lumaM
                let gradientS = lumaS - lumaM
                let mutable lumaNN = lumaN + lumaM
                let mutable lumaSS = lumaS + lumaM
                let pairN = abs gradientN >= abs gradientS
                let gradient = max (abs gradientN) (abs gradientS)
                if pairN then lengthSign <- -lengthSign
                let subpixC = saturate (abs subpixB * subpixRcpRange)

                let mutable posB = posM
                let mutable offNP = inverseVP
                if not horzSpan then set offNP.X 0.0f
                if horzSpan then set offNP.Y 0.0f
                if not horzSpan then set posB.X (posB.X + lengthSign * 0.5f)
                if horzSpan then set posB.Y (posB.Y + lengthSign * 0.5f)

                let mutable posN = posB - offNP * P.[0]
                let mutable posP = posB + offNP * P.[0]

                let subpixD = ((-2.0f)*subpixC) + 3.0f
                let mutable lumaEndN = Vec.dot (x.SampleLevel(posN, level).XYZ) luma
                let subpixE = subpixC * subpixC
                let mutable lumaEndP = Vec.dot (x.SampleLevel(posP, level).XYZ) luma

                if not pairN then lumaNN <- lumaSS
                let gradientScaled = gradient * 1.0f/4.0f
                let lumaMM = lumaM - lumaNN * 0.5f
                let subpixF = subpixD * subpixE
                let lumaMLTZero = lumaMM < 0.0f

                lumaEndN <- lumaEndN - lumaNN * 0.5f
                lumaEndP <- lumaEndP - lumaNN * 0.5f
                let mutable doneN = abs lumaEndN >= gradientScaled
                let mutable doneP = abs lumaEndP >= gradientScaled
                if not doneN then posN <- posN - offNP * P.[1]
                let mutable doneNP = (not doneN) || (not doneP)
                if not doneP then posP <- posP + offNP * P.[1]

                for i in 2..P.Length-1 do
                    if not doneNP then brk()
                    if not doneN then lumaEndN <- Vec.dot (x.SampleLevel(posN, level).XYZ) luma
                    if not doneP then lumaEndP <- Vec.dot (x.SampleLevel(posP, level).XYZ) luma
                    if not doneN then lumaEndN <- lumaEndN - lumaNN * 0.5f
                    if not doneP then lumaEndP <- lumaEndP - lumaNN * 0.5f
                    doneN <- abs lumaEndN >= gradientScaled
                    doneP <- abs lumaEndP >= gradientScaled
                    if not doneN then posN <- posN - offNP * P.[i]
                    doneNP <- (not doneN) || (not doneP)
                    if not doneP then posP <- posP + offNP * P.[i]

                let mutable dstN = posM.X - posN.X
                let mutable dstP = posP.X - posM.X
                if not horzSpan then dstN <- posM.Y - posN.Y
                if not horzSpan then dstP <- posP.Y - posM.Y

                let goodSpanN = (lumaEndN < 0.0f) <> lumaMLTZero
                let spanLength = (dstP + dstN)
                let goodSpanP = (lumaEndP < 0.0f) <> lumaMLTZero
                let spanLengthRcp = 1.0f/spanLength

                let directionN = dstN < dstP
                let dst = min dstN dstP
                let goodSpan = if directionN then goodSpanN else goodSpanP
                let subpixG = subpixF * subpixF
                let pixelOffset = (dst * (-spanLengthRcp)) + 0.5f
                let subpixH = subpixG * subpix

                let pixelOffsetGood = if goodSpan then pixelOffset else 0.0f
                let pixelOffsetSubpix = max pixelOffsetGood subpixH
                if not horzSpan then set posM.X (posM.X + pixelOffsetSubpix * lengthSign)
                if horzSpan then set posM.Y (posM.Y + pixelOffsetSubpix * lengthSign)

                V4f(x.SampleLevel(posM, level).XYZ, rgbM.W)

    let diffuseSampler =
        sampler2d {
            texture uniform?DiffuseColorTexture
            addressU WrapMode.Clamp
            addressV WrapMode.Clamp
            filter Filter.MinMagLinear
        }

    open Aardvark.Rendering

    type Vertex = {
        [<TexCoord>] tc : V2f
        }

    let fxaaUniform (preset : PRESET) (v : Vertex) =
            fragment {
                return diffuseSampler.SampleLevelFXAA(v.tc, 0.0f, preset, uniform.EdgeThreshold, uniform.EdgeThresholdMin, uniform.Subpix)
            }

    let EffectUniform(preset : PRESET) =
        toEffect (fxaaUniform preset)

    let fxaa (preset : PRESET) (edgeThreshold : float32) (edgeThresholdMin : float32) (subpix : float32) (v : Vertex) =
            fragment {
                return diffuseSampler.SampleLevelFXAA(v.tc, 0.0f, preset, edgeThreshold, edgeThresholdMin, subpix)
            }

    let Effect(preset : PRESET, edgeThreshold, edgeThresholdMin, subpix) =
        toEffect (fxaa preset edgeThreshold edgeThresholdMin subpix)
