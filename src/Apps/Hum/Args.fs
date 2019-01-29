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
open System.Globalization

type ArgsCommand =
    /// Info filename
    | Info of string
    /// Import (filename, store, key)
    | Import of string * string * string
    /// View (store, key)
    | View of string * string
    /// Download (baseurl, targetdir)
    | Download of string * string
    /// Open GUI
    | Gui
    
type Args =
    {
        command             : Option<ArgsCommand>
        useVulkan           : bool
        port                : Option<int>

        /// import command: minDist
        minDist             : Option<float>
        /// import command: format for ascii parser
        asciiFormat         : Option<Ascii.Token[]>

        // normal generation: k-nearest
        k                   : Option<int>

        /// view command: renders bounding boxes from given file 
        showBoundsFileName  : Option<string>

        /// near plane distance
        nearPlane           : float
        /// far plane distance
        farPlane            : float
        /// horizontal field-of-view in degrees
        fov                 : float

        /// batch import: skip n files
        skip                : int
        /// batch import: take n files
        take                : int

        msaa                : bool
    }

module Args =

    let private defaultArgs = {  
        command = None
        useVulkan = true
        port = None
        minDist = None
        asciiFormat = None
        k = None
        showBoundsFileName = None
        nearPlane = 1.0
        farPlane = 5000.0
        fov = 60.0
        skip = 0
        take = Int32.MaxValue
        msaa = false
    }
    
    (* parse ascii-parser format string *)
    let rec private parseAsciiFormat' xs rs =
        match xs with
        | [] -> List.rev rs
        | x :: xs -> let r = match x with
                             | "x" -> Ascii.Token.PositionX
                             | "y" -> Ascii.Token.PositionY
                             | "z" -> Ascii.Token.PositionZ
                             | "nx" -> Ascii.Token.NormalX 
                             | "ny" -> Ascii.Token.NormalY 
                             | "nz" -> Ascii.Token.NormalZ 
                             | "i" ->  Ascii.Token.Intensity
                             | "r" ->  Ascii.Token.ColorR
                             | "g" ->  Ascii.Token.ColorG
                             | "b" ->  Ascii.Token.ColorB
                             | "a" ->  Ascii.Token.ColorA
                             | "rf" -> Ascii.Token.ColorRf
                             | "gf" -> Ascii.Token.ColorGf
                             | "bf" -> Ascii.Token.ColorBf
                             | "af" -> Ascii.Token.ColorAf
                             | "_" ->  Ascii.Token.Skip
                             | _ -> failwith "unknown token"
                     parseAsciiFormat' xs (r :: rs)

    let private parseAsciiFormat (s : string) =
        Array.ofList (parseAsciiFormat' (List.ofArray (s.Split(' '))) [])

    (* parse command line args *)
    let rec private parse' (a : Args) (argv : string list) : Args =
        match argv with
        | [] -> a
        
        | "info" :: filename :: xs
            -> parse' { a with command = Some (Info filename) } xs

        | "import" :: filename :: store :: key :: xs
            -> parse' { a with command = Some (Import (filename, store, key)) } xs

        | "view" :: store :: key :: xs
            -> parse' { a with command = Some (View (store, key)) } xs

        | "download" :: basedir :: targetdir :: xs
            -> parse' { a with command = Some (Download (basedir, targetdir)) } xs

        | "gui" :: xs
            -> parse' { a with command = Some Gui } xs

        | "-opengl" :: xs
        | "-ogl" :: xs
        | "-gl" :: xs           -> parse' { a with useVulkan = false } xs
        | "-vulkan" :: xs       -> parse' { a with useVulkan = true } xs
        | "-msaa" :: xs         -> parse' { a with msaa = true } xs



        | "-port" :: x :: xs
        | "-p" :: x :: xs       -> parse' { a with port = Some (Int32.Parse x) } xs
        | "-port" :: []         
        | "-p" :: []            -> failwith "missing argument: -p <???>"

        | "-mindist" :: x :: xs
        | "-md" :: x :: xs      -> parse' { a with minDist = Some (Double.Parse(x, CultureInfo.InvariantCulture)) } xs
        | "-mindist" :: []      
        | "-md" :: []           -> failwith "missing argument: -md <???>"

        | "-n" :: k :: xs     -> parse' { a with k = Some (Int32.Parse k) } xs
        | "-n" :: []          -> failwith "missing argument: -n <???>"

        | "-ascii" :: f :: xs   -> parse' { a with asciiFormat = Some (parseAsciiFormat f) } xs
        | "-ascii" :: []        -> failwith "missing argument: -ascii <???>"

        | "-near" :: x :: xs    -> parse' { a with nearPlane = Double.Parse(x, CultureInfo.InvariantCulture) } xs
        | "-near" :: []         -> failwith "missing argument: -near <???>"

        | "-far" :: x :: xs     -> parse' { a with farPlane = Double.Parse(x, CultureInfo.InvariantCulture) } xs
        | "-far" :: []          -> failwith "missing argument: -far <???>"

        | "-fov" :: x :: xs     -> parse' { a with fov = Double.Parse(x, CultureInfo.InvariantCulture) } xs
        | "-fov" :: []          -> failwith "missing argument: -fov <???>"
        
        | "-sb" :: fn :: xs     -> parse' { a with showBoundsFileName = Some fn } xs
        | "-sb" :: []           -> failwith "missing argument: -sb <???>"

        | "-skip" :: n :: xs     -> parse' { a with skip = Int32.Parse n } xs
        | "-skip" :: []          -> failwith "missing argument: -skip <???>"

        | "-take" :: n :: xs     -> parse' { a with take = Int32.Parse n } xs
        | "-take" :: []          -> failwith "missing argument: -take <???>"

        | x :: _                -> printf "unknown argument '%s'" x
                                   printUsage ()
                                   Environment.Exit(1)
                                   failwith "never reached, but makes compiler happy ;-)"
        
    /// Parses command line arguments.
    let parse (argv : string[]) : Args =
        parse' defaultArgs (List.ofArray argv)
