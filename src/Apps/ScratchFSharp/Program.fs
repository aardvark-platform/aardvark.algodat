namespace ScratchFSharp

open System
open System.IO
open Aardvark.Base
open Aardvark.Data.Points
open Aardvark.Geometry.Points
open Aardvark.Data
open LASZip
open FSharp.Data
open System.Collections.Generic
type Trajectory = CsvProvider<"C:\\bla\\ResultDatarm-1_2025-04-18-175724_0\\rm-2_2025-04-18-180147\\high_frequency_poses.csv",Separators=" ">

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
    
    member x.AddInternal(level : int, bb : Box3d, i : int, line : Line3d) =
        if bounds.Intersects line then
            if x.IsLeaf then
                // leaf
                entries.Add i |> ignore
                
                if entries.Count > splitLimit && level < 30 then
                    let cs =
                        cell.Children |> Array.map (fun cc ->
                            let n = OctNode(data, cc, splitLimit)
                            for e in entries do
                                let line = data.[e]
                                n.AddInternal(level + 1, line.BoundingBox3d, e, line)
                            n
                        )
                        
                    children <- cs
                    entries.Clear()
                    
            else
                for c in children do
                    if c.Bounds.Intersects bb then
                        c.AddInternal(level + 1, bb, i, line)
                        
        
        
        
    member x.Add(i : int, line : Line3d) : OctNode =
        let bb = line.BoundingBox3d
        
        if bounds.Contains bb then
            x.AddInternal(0, bb, i, line)
            x
        else
            let needCentered = bb.RangeX.Contains 0.0 || bb.RangeY.Contains 0.0 || bb.RangeZ.Contains 0.0
            if cell.IsCenteredAtOrigin then
                let p = OctNode(data, cell.Parent, splitLimit)
                if x.IsLeaf then
                    p.Entries.UnionWith entries
                else
                    p.Children <- children |> Array.map wrapNonCentered
                p.Add(i, line)
            elif not needCentered then
                let parent = wrapNonCentered x
                parent.Add(i, line)
            else
                let mutable root = x
                while not root.Cell.TouchesOrigin do
                    root <- wrapNonCentered root
                
                let parentCell = Cell(root.Cell.Exponent + 1)
                let parent = OctNode(data, parentCell, splitLimit)
                
                if root.IsLeaf then
                    parent.Entries.UnionWith root.Entries
                    parent.Add(i, line)
                else
                    parent.Children <-
                        parent.Cell.Children |> Array.map (fun c ->
                            if c = root.Cell then root
                            else OctNode(data, c, root.SplitLimit)
                        )
                    parent.Add(i, line)
                
            
                    
        
        
        
        
        
    
    

module Bla = 
    [<EntryPoint>]
    let main a =
        Aardvark.Init()
        let traj = Trajectory.Load "C:\\bla\\ResultDatarm-1_2025-04-18-175724_0\\rm-2_2025-04-18-180147\\high_frequency_poses.csv"
        let poses = traj.Rows |> Seq.map (fun r -> float <| decimal r.Timestamp, (V3d(float r.X, float r.Y, float r.Z))) |> Seq.toArray
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
        
        use s = File.OpenRead(@"C:\bla\ResultDatarm-1_2025-04-18-175724_0\rm-2_2025-04-18-180147\rm-2_2025-04-18-180147.las")
        let chunks = Parser.ReadPoints(s,8192)
        let info = Parser.ReadInfo(s)
        
        
        Log.startTimed "reading %d points" info.Count
        let mutable read = 0L
        let data = List<Line3d>()
        let mutable octy = OctNode(data, Cell.Unit, 100)
        for chunk in chunks do
            for i in 0..chunk.Count-1 do
                let t = chunk.GpsTimes[i]
                let pos = chunk.Positions[i]
                let scanner = getPose t
                
                let id = data.Count
                let line = Line3d(scanner, pos)
                data.Add line
                octy <- octy.Add(id, line)
                
                read <- read + 1L
                
                if read % 10000L = 0L then
                    Report.Progress(float read / float info.Count)
                
        Log.stop()
        0
