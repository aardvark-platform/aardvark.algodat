namespace ScratchFSharp

open System
open System.IO
open Aardvark.Application
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open FSharp.Data.Adaptive
open Aardvark.Base
open Aardvark.Data.Points
open Aardvark.Geometry.Points
open Aardvark.Data
open LASZip
open FSharp.Data
open System.Collections.Generic
type Trajectory = CsvProvider<"C:\\bla\\ResultDatarm-1_2025-04-18-175724_0\\rm-2_2025-04-18-180147\\high_frequency_poses.csv",Separators=" ">
open Aardvark.SceneGraph
open Aardvark.Rendering
open Aardvark.Rendering.PointSet

type OctNode(data : List<Line3d>, cell : Cell, splitLimit : int) =
    
    let bounds = cell.BoundingBox
    let mutable children : OctNode[] = [||]
    let entries = HashSet<int>()
    
    
    static let wrapNonCentered (node : OctNode) =
        let parentCell = node.Cell.Parent
        let parent = OctNode(node.Data, parentCell, node.SplitLimit)
        if node.IsLeaf then
            parent.Entries.UnionWith node.Entries
        else
             parent.Children <-
                parent.Cell.Children |> Array.map (fun c ->
                    if c = node.Cell then node
                    else OctNode(node.Data, c, node.SplitLimit)
                )
        parent

    member x.Data = data
    member x.SplitLimit : int = splitLimit
    
    member x.Entries : HashSet<int> = entries
    
    member x.Children
        with get() = children
        and set v = children <- v
    
    member x.Cell : Cell = cell
    
    member x.Bounds = bounds
    
    member x.IsLeaf = children.Length = 0
    member x.IsEmpty = children.Length = 0 && entries.Count = 0

    
    member x.AddInternal(level : int, lines : array<int * Line3d>) =
        let newLines = List<int * Line3d>(lines.Length)
        let mutable bb = Box3d.Invalid
        for (_, l) as tup in lines do
            if bounds.Intersects l then
                newLines.Add tup
                bb.ExtendBy l.BoundingBox3d
        
      
        
        if newLines.Count > 0 then
            if x.IsLeaf then
                // leaf
                for i, _ in newLines do
                    entries.Add i |> ignore
                
                if entries.Count > splitLimit && level < 30 then
                    let myLines =
                        entries |> Aardvark.Base.HashSet.toArray |> Array.map (fun i ->
                            i, data.[i]    
                        )
                        
                    let cs =
                        cell.Children |> Array.map (fun cc ->
                            let n = OctNode(data, cc, splitLimit)
                            n.AddInternal(level + 1, myLines)
                            n
                        )
                        
                    children <- cs
                    entries.Clear()
                    
            else
                for c in children do
                    if c.Bounds.Intersects bb then
                        c.AddInternal(level + 1, CSharpList.toArray newLines)
                        
        
    member x.Add(lines : array<int * Line3d>): OctNode =
        let bb =
            let mutable bb = Box3d.Invalid
            for _, l in lines do bb.ExtendBy l.BoundingBox3d
            bb
            
        if x.IsEmpty then
            let c = Cell bb
            let n = OctNode(data, c, splitLimit)
            n.AddInternal(0, lines)
            n
        elif bounds.Contains bb then
            x.AddInternal(0, lines)
            x
        else
            let bbb = Box.Union(bb, bounds)
            let needCentered = bbb.RangeX.Contains 0.0 || bbb.RangeY.Contains 0.0 || bbb.RangeZ.Contains 0.0
            if cell.IsCenteredAtOrigin then
                let p = OctNode(data, cell.Parent, splitLimit)
                if x.IsLeaf then
                    p.Entries.UnionWith entries
                else
                    p.Children <- children |> Array.map wrapNonCentered
                p.Add lines
            elif not needCentered then
                let parent = wrapNonCentered x
                parent.Add(lines)
            else
                let mutable root = x
                while not root.Cell.TouchesOrigin do
                    root <- wrapNonCentered root
                
                let parentCell = Cell(root.Cell.Exponent + 1)
                let parent = OctNode(data, parentCell, splitLimit)
                
                if root.IsLeaf then
                    parent.Entries.UnionWith root.Entries
                    parent.Add(lines)
                else
                    parent.Children <-
                        parent.Cell.Children |> Array.map (fun c ->
                            if c = root.Cell then root
                            else OctNode(data, c, root.SplitLimit)
                        )
                    parent.Add(lines)
                    
    member x.FindIntersecting(s : Sphere3d, result : HashSet<int>) =
        let inline intersect (l : Line3d) (s : Sphere3d) =
            l.GetMinimalDistanceTo s.Center <= s.Radius
        
        if bounds.Intersects s then
            if x.IsLeaf then
                for i in entries do
                    let l = data.[i]
                    if intersect l s then
                        result.Add i |> ignore
        
            else
                for c in children do
                    c.FindIntersecting(s, result)
                        
    member x.FindIntersecting(s : Sphere3d) =
        let l = HashSet()
        x.FindIntersecting(s, l)
        l
                        
module Shader =
    open FShade
    
    let colorSam =
        sampler2d {
            texture uniform?DiffuseColorTexture
            addressU WrapMode.Wrap
            addressV WrapMode.Wrap
            filter Filter.MinMagMipPoint
        }
    let depthSam =
        sampler2d {
            texture uniform?DepthTexture
            addressU WrapMode.Wrap
            addressV WrapMode.Wrap
            filter Filter.MinMagMipPoint
        }
        
    type Fraggy =
        {
            [<Color>] c : V4d
            [<Depth>] d : float
        }
    let shady (v : Effects.Vertex) =
        fragment {
            let c = colorSam.SampleLevel(v.tc,0.0)
            let d = depthSam.SampleLevel(v.tc,0.0).X
            return {c=c;d=d}
        }
            
            
            
            
    
    

module Bla = 
    [<EntryPoint>]
    let main a =
        Aardvark.Init()
        let traj = Trajectory.Load @"C:\bla\ResultDatarm-1_2025-04-18-175724_0\rm-1_2025-04-18-175724\high_frequency_poses.csv"
        let poses = traj.Rows |> Seq.map (fun r -> float <| decimal r.Timestamp, (V3d(float r.X, float r.Y, float r.Z))) |> Seq.toArray
        
        let poseLines =
            poses |> Array.pairwise |> Array.mapi (fun i ((t0, p0), (t1, p1)) -> Line3d(p0, p1)) |> ResizeArray
            
        let poseOcty = OctNode(poseLines, Cell.Unit, 10)
        let poseOcty = poseOcty.Add(poseLines |> CSharpList.toArray |> Array.indexed)
        
        
        let getPose (t : float) =
            let mutable l = 0
            let mutable r = poses.Length-1
            while l<=r do
                let m = (l+r)/2
                let (mv,_) = poses[m]
                if t>mv then
                    l <- m+1
                else
                    r <- m-1
            if r >= 0 then
                if l < poses.Length then 
                    let t0,v0 = poses[r]
                    let t1,v1 = poses[l]
                    let µ = (t-t0) / (t1-t0)
                    lerp v0 v1 µ
                else
                    poses[r] |> snd
            else
                poses[0] |> snd
        
        use s = File.OpenRead(@"C:\bla\ResultDatarm-1_2025-04-18-175724_0\rm-1_2025-04-18-175724\aussen.las")
        let chunks = Parser.ReadPoints(s,1 <<< 13)
        let cnt = 4345192
        let sphereRadius = 0.01
        
        Log.startTimed "reading las" 
        let data = List<Line3d>()
        let measurements =
            SortedSetExt<float * int> {
                new IComparer<float * int> with
                    member x.Compare((t0, i0), (t1, i1)) =
                        let c = compare t0 t1
                        if c = 0 then compare i0 i1
                        else c
            }
            
        let mutable octy = OctNode(data, Cell.Unit, 8192)
        let sw = System.Diagnostics.Stopwatch.StartNew()
        for chunk in chunks do
            let lines =
                Array.init chunk.Count (fun i ->
                    let t = chunk.GpsTimes[i]
                    let pos = chunk.Positions[i]
                    let scanner = getPose t
                    
                    let id = data.Count
                    let line = Line3d(scanner, pos)
                    data.Add line
                    
                    measurements.Add((t, id)) |> ignore
                    
                    
                    id, line
                )
            
            octy <- octy.Add(lines)
            let t = sw.Elapsed.TotalSeconds
            let total = (t / float data.Count) * float cnt
            Log.line "read %d points -> %A" data.Count (MicroTime.FromSeconds total)
                
        Log.stop()
        
        let colors = ResizeArray(data.Count)
        Log.startTimed "Colors"
        let set = HashSet<int>(10)
        for i in 0 .. data.Count - 1 do
            set.Clear()
            let l = data.[i]
            let p = l.P1
            octy.FindIntersecting(Sphere3d(p, sphereRadius), set)
            set.Remove i |> ignore
            
            let mutable bad = 0
            for oi in set do
                let lo = data.[oi]
                
                
                let dir = lo.P1 - lo.P0
                let len = Vec.length dir
                let r = Ray3d(lo.P0, dir / len)
            
                let rayT = r.GetTOfProjectedPoint p
                let pen = len - rayT
                if pen > 0.1 then
                    bad <- bad + 1
                
            
            
            
            let c = 
                match bad with
                | 0 -> C4b.DarkGray
                | 1 -> C4b.Blue
                | 2 | 3 | 4 -> C4b.Green
                | 5 | 6 | 7 | 8 | 9  -> C4b.Yellow
                | 10 | 11 | 12 | 13 | 14 -> C4b.Orange
                | _ -> C4b.Red
            colors.Add c
            if i % 1000 = 0 then
                Report.Progress(float i / float data.Count)
        Log.stop()
            
        let pts = data |> CSharpList.toArray |> Array.map (fun l -> V3f l.P1)
            
        let posePts = poses |> Array.pairwise |> Array.collect (fun ((_,pp0),(_,pp1)) -> [|V3f(pp0);V3f(pp1)|])
        let poseSg =
            Sg.draw IndexedGeometryMode.LineList
            |> Sg.vertexAttribute' DefaultSemantic.Positions posePts
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.thickLine
                do! DefaultSurfaces.thickLineRoundCaps
                do! DefaultSurfaces.constantColor (C4f(0.08,0.08,0.08))
            }
            |> Sg.uniform "LineWidth" (AVal.constant 7.0)
            
        let pointSg =
            Sg.draw IndexedGeometryMode.PointList
            |> Sg.vertexAttribute' DefaultSemantic.Positions pts
            |> Sg.vertexAttribute' DefaultSemantic.Colors (colors |> CSharpList.toArray)
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.pointSprite
                do! DefaultSurfaces.pointSpriteFragment
            }
            |> Sg.uniform "PointSize" (AVal.constant 2.5)
        
        let win =
            window {
                backend Backend.GL
            }
            
            
        let sg =
            Sg.ofList [
                pointSg
                poseSg
            ]
            |> Sg.viewTrafo (win.View|> AVal.map (Array.item 0))
            |> Sg.projTrafo (win.Proj|> AVal.map (Array.item 0))
            |> Sg.uniform "ViewportSize" win.Sizes
            
        let signature =
            win.Runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8
            ]
            
        let rt = win.Runtime.CompileRender(signature, sg)
        
        let color, depth =
            rt
            |> RenderTask.renderToColorAndDepthWithClear win.Sizes (clear { color C4f.Black; depth 1.0; stencil 0 })
        
        depth.Acquire()
        let selectedPoint = cval None
        win.Mouse.Move.Values.Add (fun (_, p) ->
            if win.Keyboard.IsDown Keys.LeftShift |> AVal.force then 
                let depth = depth |> AVal.force
                if p.Position.AllGreaterOrEqual 0 && p.Position.AllSmaller depth.Size.XY then
                    let mat = Matrix<float32>(V2i.II)
                    win.Runtime.DownloadDepth(depth, mat, offset = p.Position)
                    let depth = mat.[V2i.OO]
                    if depth < 1.0f then 
                        let viewport = win.Sizes |> AVal.force
                        let view = win.View |> AVal.force |> Array.item 0
                        let proj = win.Proj |> AVal.force |> Array.item 0
                        let vp = view * proj
                        let tc = V2d p.Position / V2d viewport
                        let ndc = V3d(2.0 * tc.X - 1.0, 1.0 - 2.0 *tc.Y, 2.0*float depth - 1.0)
                        let wp = vp.Backward.TransformPosProj ndc
                        Log.line "%A" wp
                        transact (fun _ -> selectedPoint.Value <- Some wp)
                    else
                        transact (fun _ -> selectedPoint.Value <- None)
        )
        
        let intersectingPickLines =
            selectedPoint |> AVal.map (fun p ->
                match p with
                | Some p ->
                    
                    let ls = poseOcty.FindIntersecting(Sphere3d(p,0.01))
                    if ls.Count > 0 then
                        let i0 = ls |> Seq.head
                        let t0, p0 = poses.[i0]
                        let t1, p1 = poses.[i0+1]
                        
                        let tm = (t0 + t1) / 2.0
                        
                        let view = measurements.GetViewBetween((tm - 1.0, Int32.MinValue), (tm + 1.0, Int32.MaxValue))
                        let lsc = view |> Seq.toArray
                        Log.line "trajectory %d" lsc.Length 
                        lsc |> Array.collect (fun (_, i) ->
                            let l = data[i]
                            [|V3f l.P0; V3f l.P1|]
                        )
                        
                    else
                        Log.line "point"
                        let ls = octy.FindIntersecting(Sphere3d(p,sphereRadius))
                        ls |> Seq.toArray |> Array.collect (fun i ->
                            let l = data[i]
                            [|V3f l.P0; V3f l.P1|]
                        )
                | None ->
                    [||]
            )
        let iplColors =
            intersectingPickLines |> AVal.map (fun ipls -> Array.init ipls.Length (fun i ->
                if i%2 = 0 then C4b.White else C4b.Orange
            ))
            
        let iplSg =
            Sg.draw IndexedGeometryMode.LineList
            |> Sg.vertexAttribute DefaultSemantic.Positions intersectingPickLines
            |> Sg.vertexAttribute DefaultSemantic.Colors iplColors
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.thickLine
                do! DefaultSurfaces.thickLineRoundCaps
                do! DefaultSurfaces.vertexColor
            }
            |> Sg.uniform "LineWidth" (intersectingPickLines |> AVal.map (fun l -> if l.Length > 10 then 1.0 else 2.5))
            
        let fullscreenSg =
            Sg.fullScreenQuad
            |> Sg.diffuseTexture color
            |> Sg.texture "DepthTexture" depth
            |> Sg.shader {
                do! Shader.shady
            }
            
        win.Scene <-
            Sg.ofList [
                fullscreenSg
                iplSg
            ]
            
        win.Run()
            
        
        0
