(*
    Copyright (c) 2018. Attila Szabo, Georg Haaser, Harald Steinlechner, Stefan Maierhofer.
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.
    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*)
namespace Hum

open Aardvark.Data.Points.Import
open System

[<AutoOpen>]
module Prelude =

    /// Print header.
    let printHeader () =
        Console.ForegroundColor <- ConsoleColor.White
        Console.WriteLine("---------------------------------------------------------------------------------------")
        Console.Write("A viewer for ")
        Console.ForegroundColor <- ConsoleColor.Green
        Console.Write("hum")
        Console.ForegroundColor <- ConsoleColor.White
        Console.WriteLine("ongous point clouds.")
        Console.WriteLine("Copyright (c) 2018. Georg Haaser, Stefan Maierhofer, Harald Steinlechner, Attila Szabo.")
        Console.WriteLine("https://github.com/aardvark-community/hum")
        Console.WriteLine("---------------------------------------------------------------------------------------")
        Console.ResetColor()
        
    /// Print usage message.
    let printUsage () =
        printfn "usage: hum <command> <args...>"
        printfn ""
        printfn "    info <filename>                 print point cloud file info"
        printfn ""
        printfn "    import <filename> <store> <id>  import <filename> into <store> with <id>"
        printfn "        [-mindist <dist>]              skip points on import, which are less"
        printfn "                                         than given distance from previous point,"
        printfn "                                         e.g. -mindist 0.001"
        printfn "        [-n <k>]                       estimate per-point normals"
        printfn "                                         using k-nearest neighbours,"
        printfn "                                         e.g. -n 16"
        printfn "        [-ascii <lineformat>]          import custom ascii format"
        printfn "                                         e.g. -ascii \"x y z _ r g b\""
        printfn "                                       format symbols"
        printfn "                                         position      : x, y, z"
        printfn "                                         normal        : nx, ny, nz"
        printfn "                                         color         : r, g, b, a"
        printfn "                                         color (float) : rf, gf, bf, af"
        printfn "                                         intensity     : i"
        printfn "                                         skip          : _"
        printfn ""
        printfn "    view <store> <id>               show point cloud with <id> in given <store>"
        printfn "        [-gl]                            use OpenGL (default)"
        printfn "        [-vulkan]                        use Vulkan (not working atm.)"
        printfn "        [-msaa]                          use multisample shading (off by default)"
        printfn "        [-near <dist>]                   near plane distance, default 1.0"
        printfn "        [-far <dist>]                    far plane distance, default 5000.0"
        printfn "        [-fov <degrees>]                 horizontal field-of-view, default 60.0"
        printfn "                                    keyboard shortcuts"
        printfn "                                         <A>/<D> ... left/right"
        printfn "                                         <W>/<S> ... forward/back"
        printfn "                                         <+>/<-> ... camera speed"
        printfn "                                         <P>/<O> ... point size (+/-)"
        printfn "                                         <T>/<R> ... target pixel distance (+/-)"
        printfn "                                         <C> ....... cycle color scheme"
        printfn "                                                     (colors, classification, normals)"
        printfn "                                         <↑>/<↓> ... octree level visualization (+/-)"
        printfn ""
        printfn "    download <baseurl> <targetdir>  bulk download of point cloud files"
        printfn "                                      scans webpage at <baseurl> for hrefs to"
        printfn "                                      files with known file extensions and"
        printfn "                                      download to <targetdir>"
        printfn ""
        printfn "    gui                             start in GUI mode"

    let private formats = [
        Pts.PtsFormat
        E57.E57Format
        Yxh.YxhFormat
        //Ply.PlyFormat
        //Laszip.LaszipFormat
        ]

    /// Init point cloud file formats.
    let initPointFileFormats () = for x in formats do x |> ignore

    /// Known file extensions.
    let getKnownFileExtensions () =
        formats
        |> List.collect (fun x -> x.FileExtensions |> List.ofArray)
        |> List.map (fun x -> x.ToLowerInvariant())
        
  