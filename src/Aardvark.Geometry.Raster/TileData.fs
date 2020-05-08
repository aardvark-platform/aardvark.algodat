namespace Aardvark.Geometry

open Aardvark.Base
open System

/// A two-dim raster of values.
type TileData<'a> =
    /// (data, mapping), where mapping places the samples globally.
    | ArrayTile of 'a[] * Box2l
    /// (data, mapping, window), where window is absolute (not relative to mapping).
    | WindowedTile of 'a[] * Box2l * Box2l
with
    override this.ToString() =
        match this with
        | ArrayTile (data, mapping) -> sprintf "ArrayTile(%d, %A)" data.Length mapping
        | WindowedTile (data, mapping, window) -> sprintf "WindowedTile(%d, %A, %A)" data.Length mapping window

module TileData =

    /// Create tile data from array.
    let ofArray mapping data = ArrayTile (data, mapping)

    /// Create tile data from array.
    let OfArray(mapping, data) = ofArray mapping data

    /// Creates windowed tile data.
    let withWindow (window : Box2l) tile =
        match tile with

        | ArrayTile (data, mapping) ->
            if not (mapping.Contains(window)) then sprintf "Window must be contained in data mapping %A, but is %A." mapping window |> invalidArg "window"
            WindowedTile (data, mapping, window)

        | WindowedTile (data, mapping, oldWindow) ->
            if not (mapping.Contains(window)) then sprintf "Window must be contained in data mapping %A, but is %A." mapping window |> invalidArg "window"
            if window = mapping || window = oldWindow then tile
            else WindowedTile (data, mapping, window)

    /// Ensure that tile is not windowed, i.e. copy out windowed data.
    let materialize tile =
        match tile with

        | ArrayTile _ -> tile

        | WindowedTile (data, mapping, window) ->
            let xs : 'a[] = Array.zeroCreate (int window.Area)
            let widthMapping = int (mapping.Max.X - mapping.Min.X)
            let widthWindow = int (window.Max.X - window.Min.X)
            let heightWindow = int (window.Max.Y - window.Min.Y)
            let originWindowLocal = V2i(window.Min - mapping.Min)
            let mutable i = 0
            let mutable j0 = originWindowLocal.Y * widthMapping + originWindowLocal.X
            for _ = 1 to heightWindow do
                let jmax = j0 + widthWindow - 1
                let mutable j = j0
                for j = j0 to jmax do
                    xs.[i] <- data.[j]
                    i <- i + 1
                j0 <- j0 + widthMapping
            ArrayTile (xs, window)

    /// Get raw data array of tile.
    let data tile = match tile with | ArrayTile (data, _) -> data | WindowedTile (data, _, _) -> data

    /// Get bounds of tile.
    let bounds tile = match tile with | ArrayTile (_, m) -> m | WindowedTile (_, _, w) -> w

    /// Get size (columns x rows) of tile. 
    let size tile = match tile with | ArrayTile (_, m) -> m.Size | WindowedTile (_, _, w) -> w.Size

    /// Get raster value at absolute position.
    let getValue (posAbsolute : V2l) tile =
        match tile with
        | ArrayTile (data, mapping) ->
            if posAbsolute.X >= mapping.Min.X && posAbsolute.Y >= mapping.Min.Y && posAbsolute.X < mapping.Max.X && posAbsolute.Y < mapping.Max.Y then 
                let x = posAbsolute.X - mapping.Min.X
                let y = posAbsolute.Y - mapping.Min.Y
                let i = int (y * mapping.SizeX + x)
                data.[i]
            else
                raise (IndexOutOfRangeException())
        | WindowedTile (data, mapping, window) ->
            if posAbsolute.X >= window.Min.X && posAbsolute.Y >= window.Min.Y && posAbsolute.X < window.Max.X && posAbsolute.Y < window.Max.Y then 
                let x = posAbsolute.X - mapping.Min.X
                let y = posAbsolute.Y - mapping.Min.Y
                let i = int (y * mapping.SizeX + x)
                data.[i]
            else
                raise (IndexOutOfRangeException())

    /// Get raster value at relative position.
    let getValueRelative (posRelative : V2i) tile =
        match tile with
        | ArrayTile (_, mapping)      -> tile |> getValue (mapping.Min + V2l posRelative)
        | WindowedTile (_, _, window) -> tile |> getValue (window.Min  + V2l posRelative)

    // (tilecoords, tilewindow) seq
    let splitIntoTiles (tilesize : V2l) tile =
        let bb = bounds tile
        let f x s = if x < 0L then (x+1L) / s - 1L else x / s
        let minIncl = V2l(f bb.Min.X tilesize.X, f bb.Min.Y tilesize.Y)
        let maxIncl = V2l(f bb.Max.X tilesize.X, f bb.Max.Y tilesize.Y) // bb.Max is exclusive, therefore result is inclusive
        let mutable y = minIncl.Y
        seq {
            while y <= maxIncl.Y do
                let mutable x = minIncl.X
                while x <= maxIncl.X do
                    let t = V2l(x,y)
                    let tbb = Box2l(t * tilesize, (t + 1L) * tilesize)
                    //printfn "[%d, %d] -> %A" x y tbb
                    let intsctn = bb.Intersection(tbb) 
                    //printfn "            %A    %d" intsctn intsctn.Area
                    yield (t, tile |> withWindow intsctn)

                    x <- x + 1L
                y <- y + 1L
        }

type TileData<'a> with
    member this.WithWindow (window : Box2l) = TileData.withWindow window this
    member this.WithWindow (window : Box2i) = TileData.withWindow (Box2l window) this
    member this.Materialize () = TileData.materialize this
    member this.Data with get() = TileData.data this
    member this.Bounds with get() = TileData.bounds this
    member this.Size with get() = TileData.size this
    member this.SplitIntoTiles (tileSize : V2l) = TileData.splitIntoTiles tileSize this
    member this.GetValue (posAbsolute : V2l) = TileData.getValue posAbsolute this
    member this.GetValue (posAbsoluteX : int64, posAbsoluteY : int64) = TileData.getValue (V2l(posAbsoluteX, posAbsoluteY)) this
    member this.GetValueRelative (posRelative : V2i) = TileData.getValueRelative posRelative this
    member this.GetValueRelative (posRelativeX : int, posRelativeY : int) = TileData.getValueRelative (V2i(posRelativeX, posRelativeY)) this

