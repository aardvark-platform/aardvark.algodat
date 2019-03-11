namespace Aardvark.Rendering.PointSet


open Aardvark.Base.Geometry
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.Geometry
open System
open System.Runtime.InteropServices

[<AutoOpen>]
module SimplePickExtensions =

    module Seq = 
        open System.Collections.Generic

        type private EnumeratorEnumerable<'a>(create : unit -> IEnumerator<'a>) =
            interface System.Collections.IEnumerable with
                member x.GetEnumerator() = create() :> _

            interface IEnumerable<'a> with
                member x.GetEnumerator() = create()


        [<AbstractClass>]
        type AbstractEnumerator<'a>() =
            abstract member MoveNext : unit -> bool
            abstract member Current : 'a
            abstract member Reset : unit -> unit
            abstract member Dispose : unit -> unit

            interface System.Collections.IEnumerator with
                member x.MoveNext() = x.MoveNext()
                member x.Current = x.Current :> obj
                member x.Reset() = x.Reset()

            interface IEnumerator<'a> with
                member x.Current = x.Current
                member x.Dispose() = x.Dispose()


        let ofEnumerator (create : unit -> #IEnumerator<'a>) =
            EnumeratorEnumerable (fun () -> create() :> IEnumerator<'a>) :> seq<'a>

        let mergeSorted (cmp : 'a -> 'a -> int) (l : IEnumerable<'a>) (r : IEnumerable<'a>) =
            let newEnumerator() =
                let l = l.GetEnumerator()
                let r = r.GetEnumerator()

                let mutable initial = true
                let mutable lh = None
                let mutable rh = None
                let mutable current = Unchecked.defaultof<'a>

                { new AbstractEnumerator<'a>() with
                    member x.MoveNext() =
                        if initial then
                            initial <- false
                            lh <- if l.MoveNext() then Some l.Current else None
                            rh <- if r.MoveNext() then Some r.Current else None

                        match lh, rh with
                        | Some lv, Some rv ->
                            let c = cmp lv rv
                            if c <= 0 then
                                current <- lv
                                if l.MoveNext() then lh <- Some l.Current
                                else lh <- None
                            else
                                current <- rv
                                if r.MoveNext() then rh <- Some r.Current
                                else rh <- None
                            true
                        | Some lv, None ->
                            current <- lv
                            if l.MoveNext() then lh <- Some l.Current
                            else lh <- None
                            true
                        | None, Some rv ->
                            current <- rv
                            if r.MoveNext() then rh <- Some r.Current
                            else rh <- None
                            true
                        | None, None ->
                            false

                    member x.Reset() =
                        l.Reset()
                        r.Reset()
                        initial <- true
                        lh <- None
                        rh <- None
                        current <- Unchecked.defaultof<'a>

                    member x.Dispose() =
                        l.Dispose()
                        r.Dispose()
                        initial <- false
                        lh <- None
                        rh <- None
                        current <- Unchecked.defaultof<'a>

                    member x.Current = current

                } :> IEnumerator<_>

            EnumeratorEnumerable newEnumerator :> seq<_>



    //let private transform (trafo : Trafo3d) (part : RayPart) =
    //    let ray = part.Ray.Ray
    //    let np = trafo.Forward.TransformPos ray.Origin
    //    let nd = trafo.Forward.TransformDir ray.Direction
    //    let l = Vec.length nd
    //    let nr = FastRay3d(Ray3d(np,nd / l))

    //    let f = 1.0 / part.Ray.Ray.Direction.Length
    //    let ntmi = part.TMin * f
    //    let ntma = part.TMax * f

    //    RayPart(nr,ntmi,ntma)


    //type Circle2d with

    //    member x.Intersects (ray : Ray2d, tMin : float, tMax : float, [<Out>] t0 : byref<float>, [<Out>] t1 : byref<float>) =
    //        let o = ray.Origin - x.Center
    //        let d = ray.Direction
    //        let a = Vec.lengthSquared d
    //        if Fun.IsTiny a then
    //            false
    //        else
    //            let b = 2.0 * Vec.dot o d
    //            let c = Vec.lengthSquared o - x.RadiusSquared
    //            let r = b*b - 4.0*a*c
    //            if r < 0.0 then 
    //                false

    //            elif Fun.IsTiny r then
    //                let ta = -b / (2.0 * a)
    //                if ta >= tMin && ta <= tMax then
    //                    t0 <- ta
    //                    t1 <- ta
    //                    true
    //                else
    //                    false
    //            else
    //                let r = sqrt r
    //                let ta = (-b + r) / (2.0 * a)
    //                let tb = (-b - r) / (2.0 * a)

    //                let ia = ta >= tMin && ta <= tMax
    //                let ib = tb >= tMin && tb <= tMax
    //                if ia && ib then
    //                    t0 <- ta
    //                    t1 <- tb
    //                    true
    //                elif ia then
    //                    t0 <- ta
    //                    t1 <- ta
    //                    true
    //                elif ib then
    //                    t0 <- tb
    //                    t1 <- tb
    //                    true
    //                else
    //                    false

    //    member x.Intersects (ray : Ray2d, tMin : float, tMax : float) =
    //        let o = ray.Origin - x.Center
    //        let d = ray.Direction
    //        // |o + t * d| = r
    //        // (ox + t*dx)^2 + (oy + t*dy)^2 + (oz + t*dz)^2 = r^2

    //        // ox^2 + 2*t*ox*dx + t^2*dx^2 +
    //        // oy^2 + 2*t*oy*dy + t^2*dy^2 +
    //        // oz^2 + 2*t*oz*dz + t^2*dz^2 = r^2

    //        // t^2*(dx^2 + dy^2 + dz^2) + t*2*(ox*dx + oy*dy + oz*dz) + (ox^2 + oy^2 + oz^2) = r^2

    //        // t^2      * |d|^2         + 
    //        // t        * 2*<d|o>       +
    //        // 1        * |o|^2 - r^2
    //        // = 0

    //        let a = Vec.lengthSquared d
    //        let b = 2.0 * Vec.dot o d
    //        let c = Vec.lengthSquared o - x.RadiusSquared
    //        let struct (t0, t1) = Polynomial.RealRootsOf(a,b,c)
    //        (t0 >= tMin && t0 <= tMax) || 
    //        (t1 >= tMin && t1 <= tMax)

    //    member x.Intersects (line : Line2d, [<Out>] p0 : byref<V2d>, [<Out>] p1 : byref<V2d>) =
    //        let ray = Ray2d(line.P0, line.P1 - line.P0)
    //        let mutable t0 = 0.0
    //        let mutable t1 = 0.0
    //        if x.Intersects(ray, 0.0, 1.0, &t0, &t1) then
    //            p0 <- ray.Origin + t0 * ray.Direction
    //            p1 <- ray.Origin + t1 * ray.Direction
    //            true
    //        else
    //            false

    //    member x.Intersects (line : Line2d) =
    //        x.Intersects(Ray2d(line.P0, line.P1 - line.P0), 0.0, 1.0)

    //    member x.Intersects (t : Triangle2d) =
    //        x.Contains t.P0 || x.Contains t.P1 || x.Contains t.P2 ||
    //        t.Contains x.Center ||
    //        x.Intersects t.Line01 || x.Intersects t.Line12 || x.Intersects t.Line20

            
    //    member x.IntersectsConvex (t : Polygon2d) =
    //        t.Points |> Seq.exists x.Contains ||
    //        t.Contains x.Center ||
    //        t.EdgeLines |> Seq.exists x.Intersects

    //type Ellipse2d with
    //    member x.Contains (pt : V2d) =
    //        let m = M22d.FromCols(x.Axis0, x.Axis1).Inverse
    //        let p = m * (pt - x.Center)
    //        p.LengthSquared <= 1.0
            
    //    member x.Intersects(ray : Ray2d, tMin : float, tMax : float) =
    //        let m = M22d.FromCols(x.Axis0, x.Axis1).Inverse
    //        let o = m * (ray.Origin - x.Center)
    //        let d = m * ray.Direction

    //        let a = Vec.lengthSquared d
    //        let b = 2.0 * Vec.dot o d
    //        let c = Vec.lengthSquared o - 1.0
    //        let struct (t0, t1) = Polynomial.RealRootsOf(a,b,c)
    //        (t0 >= tMin && t0 <= tMax) || 
    //        (t1 >= tMin && t1 <= tMax)
            
    //    member x.Intersects (line : Line2d) =
    //        x.Intersects(Ray2d(line.P0, line.P1 - line.P0), 0.0, 1.0)

    //    member x.Intersects (t : Triangle2d) =
    //        x.Contains t.P0 || x.Contains t.P1 || x.Contains t.P2 ||
    //        t.Contains x.Center ||
    //        x.Intersects t.Line01 || x.Intersects t.Line12 || x.Intersects t.Line20

    [<Struct>]
    type SimplePickPoint =
        {
            DataPosition : V3d
            WorldPosition : V3d
            Ndc : V3d
            // Original : ILodTreeNode
            // Index : int
        }

    [<AutoOpen>]
    module private Helpers =
        type BvhTraversal<'q, 'a, 'r> =
            {
                intersectLeaf : 'q -> 'a -> seq<'r>
                intersectBox : 'q -> Box3d -> Option<'q>
                compare : 'r -> 'r -> int
                leftBeforeRight : 'q -> Box3d -> Box3d -> bool
            }

        module BvhTraversal =
            let ray (tryIntersect : RayPart -> 'a -> seq<RayHit<'r>>) =
                let intersectBox (rp : RayPart) (b : Box3d) =
                    let mutable rp = rp
                    if rp.Ray.Intersects(b, &rp.TMin, &rp.TMax) && rp.TMin <= rp.TMax then Some rp
                    else None

            
                let leftBeforeRight (r : RayPart) (lBox : Box3d) (rBox : Box3d) =
                    let mutable lmin = r.TMin
                    let mutable rmin = r.TMin
                    let mutable foo = r.TMax
                    r.Ray.Intersects(lBox, &lmin, &foo) |> ignore
                    r.Ray.Intersects(rBox, &rmin, &foo) |> ignore
                    lmin < rmin


                {
                    intersectLeaf = tryIntersect
                    intersectBox = intersectBox
                    //compareQuery = fun (l : RayPart) (r : RayPart) -> 
                    //    if l.TMin >= r.TMax then
                    //        { overlap = false; order = false }
                    //    elif r.TMin >= l.TMax then
                    //        { overlap = false; order = true }
                    //    else
                    //        { overlap = true; order = compare l.TMin r.TMin <= 0 }
                    compare = fun (l : RayHit<'r>) (r : RayHit<'r>) -> compare l.T r.T
                    leftBeforeRight = leftBeforeRight
                }

            let region (cam : V3d) (tryIntersect : Region3d -> 'a -> seq<RayHit<'r>>) =
                let intersectBox (r : Region3d) (b : Box3d) =
                    if Region3d.intersects b r then Some r
                    else None
                    
                let leftBeforeRight (r : Region3d) (lBox : Box3d) (rBox : Box3d) =
                    let i = lBox.Intersection rBox
                    let dim = i.MinorDim
                    let v = i.Center.[dim]
                    cam.[dim] < v
                

                {
                    intersectLeaf = tryIntersect
                    intersectBox = intersectBox
                    //compareQuery = fun _ _ -> { overlap = true; order = true }
                    compare = fun (l : RayHit<'r>) (r : RayHit<'r>) -> compare l.T r.T
                    leftBeforeRight = leftBeforeRight
                }
            
        let rec traverse (t : BvhTraversal<'q, 'a, 'r>)  (data : 'a[]) (part : 'q) (node : BvhNode) =
            match node with
                | BvhNode.Leaf id ->
                    t.intersectLeaf part data.[id]
                | BvhNode.Node(lBox, rBox, left, right) ->
                    match t.intersectBox part lBox, t.intersectBox part rBox with
                    | None, None ->
                        Seq.empty
                    | Some l, None ->
                        traverse t data l left
                    | None, Some r ->
                        traverse t data r right
                    | Some l, Some r ->
                        let leftBeforeRight = t.leftBeforeRight part lBox rBox
                        if leftBeforeRight then Seq.append (traverse t data l left) (Seq.delay (fun () -> traverse t data r right))
                        else Seq.append (traverse t data r right) (Seq.delay (fun () -> traverse t data l left))

        type BvhTree<'a> with
            member x.Traverse (t : BvhTraversal<'q, 'a, 'r>, query : 'q) =
                match x.Root with
                | Some r -> traverse t x.Data query r
                | None -> Seq.empty


        let toHull3d (viewProj : Trafo3d) =
            let r0 = viewProj.Forward.R0
            let r1 = viewProj.Forward.R1
            let r2 = viewProj.Forward.R2
            let r3 = viewProj.Forward.R3

            let inline toPlane (v : V4d) =
                Plane3d(-v.XYZ, v.W)

            Hull3d [|
                r3 - r0 |> toPlane  // right
                r3 + r0 |> toPlane  // left
                r3 + r1 |> toPlane  // bottom
                r3 - r1 |> toPlane  // top
                r3 + r2 |> toPlane  // near
                //r3 - r2 |> toPlane  // far
            |]

        type RegionQuery = 
            {
                viewProj : Trafo3d
                region : Region3d
                cam : V3d
            }

    type SimplePickTree with

        member private x.FindInternal(query : RegionQuery) =
            if Region3d.intersects x.bounds query.region then
                let bvh = x.bvh
                match bvh.Root with
                | Some _ -> 
                    let traversal = BvhTraversal.region query.cam (fun (r : Region3d) (t : SimplePickTree) -> t.FindInternal(query))
                    bvh.Traverse(traversal, query.region)
                    //intersections (fun r (t : SimplePickTree) -> t.FindInternal(r, radiusD, radiusK)) bvh.Data ray root
                | None ->
                    let hits = 
                        x.positions |> Array.choose ( fun p -> 
                            let p = x.dataTrafo.Forward.TransformPos (V3d p)
                            if Region3d.contains p query.region then
                                let t = Vec.length (p - query.cam)
                                let pt = { SimplePickPoint.DataPosition = p; SimplePickPoint.WorldPosition = x.dataTrafo.Backward.TransformPos p; SimplePickPoint.Ndc = V3d.Zero  }
                                Some (RayHit(t, pt))
                            else
                                None
                        )
                    hits |> Seq.sortBy (fun h -> h.T)
            else
                Seq.empty

        member x.FindPoints(viewProj : Trafo3d, box : Box2d) =
            let t = x.trafo.GetValue()
            let dataViewProj = x.dataTrafo.Inverse * t * viewProj
            let cam = dataViewProj.Backward.TransformPosProj(V3d(0.0, 0.0, -100000.0))

            let hull = 
                let c = box.Center
                let scale = 2.0 / box.Size
                
                let lvp = 
                    dataViewProj *
                    Trafo3d.Scale(scale.X, scale.Y, 1.0) *
                    Trafo3d.Translation(-scale.X * c.X, -scale.Y * c.Y, 0.0)

                toHull3d lvp
                |> FastHull3d
                

            let region = 
                Region3d.ofIntersectable {
                    new Intersectable3d() with
                        override x.Contains (pt : V3d) = hull.Hull.Contains pt
                        override x.Contains (box : Box3d) = box.ComputeCorners() |> Array.forall hull.Hull.Contains
                        override x.Intersects (box : Box3d) = hull.Intersects box
                }

            x.FindInternal({ region = region; cam = cam; viewProj = viewProj }) 
            |> Seq.map (fun hit -> 
                let wp = V3d hit.Value.WorldPosition |> t.Forward.TransformPos
                let ndc = viewProj.Forward.TransformPosProj wp
                RayHit(hit.T, { hit.Value with WorldPosition = wp; Ndc = ndc })
            )
            
        member x.FindPoints(viewProj : Trafo3d, ellipse : Ellipse2d) =
            let ellipse =
                let d = Vec.dot ellipse.Axis0 ellipse.Axis1 
                if Fun.IsTiny d then ellipse
                else Ellipse2d.FromConjugateDiameters(ellipse.Center, ellipse.Axis0, ellipse.Axis1)

            let bounds = 
                Box2d [| ellipse.Center - ellipse.Axis0; ellipse.Center + ellipse.Axis0;  ellipse.Center - ellipse.Axis1; ellipse.Center + ellipse.Axis1 |]
                
            let m = M22d.FromCols(ellipse.Axis0, ellipse.Axis1).Inverse
           
            x.FindPoints(viewProj, bounds)
            |> Seq.filter (fun p -> Vec.lengthSquared (m * (p.Value.Ndc.XY - ellipse.Center)) <= 1.0)

        member x.FindPoints(viewProj : Trafo3d, c : Circle2d) =
            x.FindPoints(viewProj, c.BoundingBox2d)
            |> Seq.filter (fun p -> c.Contains p.Value.Ndc.XY)

        member x.FindPoints(viewProj : Trafo3d, region : PolyRegion) =
            let bounds = PolyRegion.bounds region
            x.FindPoints(viewProj, bounds)
            |> Seq.filter (fun p -> PolyRegion.containsPoint p.Value.Ndc.XY region)
