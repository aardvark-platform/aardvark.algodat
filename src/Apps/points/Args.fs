(*
    Based on https://github.com/aardvark-community/hum.
*)
namespace Aardvark.Apps.Points

open Aardvark.Data.Points.Import
open System
open System.Globalization
open System.IO

type ArgsCommand = Import | Export

type ArgsStoreType = Store | Folder
  
type Args =
    {
        command             : ArgsCommand option

        /// output path to store or folder
        outPath             : string option
        /// output type (store or folder)
        outType             : ArgsStoreType option
        /// output pointcloud key
        outKey              : string option

        /// input path to store or folder
        inPath             : string option
        /// input type (store or folder)
        inType             : ArgsStoreType option
        /// input pointcloud key
        inKey              : string option

        /// import command: splitLimit
        splitLimit          : int

        /// import command: minDist
        minDist             : float

        /// import command: format for ascii parser
        asciiFormat         : Ascii.Token[] option

        /// export command: inline arrays referenced by octree nodes 
        inlining            : bool option

        /// export command: gzip nodes 
        gzipped             : bool option

        /// optionally store metadata JSON with this key
        metadataKey         : string option

        /// normal generation: k-nearest
        kNearest            : int option

        /// files
        files               : string list

        /// verbose
        verbose             : bool
    }

module Args =

    let private defaultArgs = {  
        command = None
        outPath = None
        outType = None
        outKey = None
        inPath = None
        inType = None
        inKey = None
        splitLimit = 8192
        minDist = 0.0
        asciiFormat = None
        inlining = None
        gzipped = None
        metadataKey = None
        kNearest = None
        files = list.Empty
        verbose = true
    }
    
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

    let rec private parse' (a : Args) (argv : string list) : Args =
        match argv with
        | [] -> a
        
        | "import" :: xs                    ->  parse' { a with command = Some Import } xs

        | "export" :: xs                    ->  parse' { a with command = Some Export } xs
        
        | "-o" :: "store" :: outpath :: xs  ->  parse' { a with outPath = Some outpath; outType = Some Store } xs
        | "-o" :: "folder" :: outpath :: xs ->  parse' { a with outPath = Some outpath; outType = Some Folder } xs
        | "-o" :: outpath :: xs             ->  parse' { a with outPath = Some outpath; outType = Some Store } xs
        | "-o" :: []                        ->  failwith "missing argument: -o <outpath>"
        | "-okey" :: key :: xs              ->  parse' { a with outKey = Some key } xs

        | "-i" :: "store" :: inpath :: xs  ->  parse' { a with inPath = Some inpath; inType = Some Store } xs
        | "-i" :: "folder" :: inpath :: xs ->  parse' { a with inPath = Some inpath; inType = Some Folder } xs
        | "-i" :: inpath :: xs             ->  parse' { a with inPath = Some inpath; inType = Some Store } xs
        | "-i" :: []                        ->  failwith "missing argument: -i <inpath>"
        | "-ikey" :: key :: xs              ->  parse' { a with inKey = Some key } xs

        | "-splitLimit" :: splitLimit :: xs ->  parse' { a with splitLimit = Int32.Parse splitLimit } xs
        | "-splitLimit" :: []               ->  failwith "missing argument: -splitLimit <???>"

        | "-minDist" :: minDist :: xs       ->  parse' { a with minDist = Double.Parse(minDist, CultureInfo.InvariantCulture) } xs
        | "-minDist" :: []                  ->  failwith "missing argument: -minDist <???>"

        | "-inline" :: xs                   ->  parse' { a with inlining = Some true } xs

        | "-z" :: xs                        ->  parse' { a with gzipped = Some true } xs

        | "-meta" :: key :: xs              ->  parse' { a with metadataKey = Some key } xs

        | "-kNearest" :: kNearest :: xs     ->  parse' { a with kNearest = Some (Int32.Parse kNearest) } xs
        | "-kNearest" :: []                 ->  failwith "missing argument: -kNearest <???>"

        | "-ascii" :: f :: xs               ->  parse' { a with asciiFormat = Some (parseAsciiFormat f) } xs
        | "-ascii" :: []                    ->  failwith "missing argument: -ascii <???>"

        | filename :: xs                    ->  if File.Exists filename then
                                                    parse' { a with files = filename :: a.files } xs
                                                else
                                                    failwith (sprintf "file does not exit: %s" filename)
        
    /// Parses command line arguments.
    let parse (argv : string[]) : Args =
        parse' defaultArgs (List.ofArray argv)
