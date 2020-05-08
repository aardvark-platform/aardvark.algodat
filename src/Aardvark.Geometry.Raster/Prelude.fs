namespace Aardvark.Geometry

open Aardvark.Base

[<AutoOpen>]
module PowersOfTwo =

    /// power-of-two exponent
    [<Measure>] type potexp
    type PowerOfTwoExp = int<potexp>

    /// power-of-two
    [<Measure>] type pot

    /// length
    [<Measure>] type length

    let potexp2pot (e : PowerOfTwoExp) =
        if e < 0<potexp> then invalidArg "e" "Exponent must not be negative."
        (1L <<< int e) * 1L<pot>

[<AutoOpen>]
module RasterExtensions =

    type Box2l with
        member this.IsSizePowerOfTwoSquared with get() = this.SizeX = this.SizeY && this.SizeX.IsPowerOfTwo()
        member this.SplitAtCenter () =
            let c = this.Center
            [|
                Box2l(this.Min.X, this.Min.Y,        c.X,        c.Y)
                Box2l(       c.X, this.Min.Y, this.Max.X,        c.Y)
                Box2l(this.Min.X,        c.Y,        c.X, this.Max.Y)
                Box2l(       c.X,        c.Y, this.Max.X, this.Max.Y)
            |]

    type Cell2d with
        static member Create(xy : V2l, e : PowerOfTwoExp) = Cell2d(xy, int e)
