(*
    Copied from https://github.com/aardvark-community/hum.
*)
namespace Aardvark.Algodat.App.Viewer

open Aardvark.Base
open System.IO
open System.Linq
open System.Net
open System.Text.RegularExpressions

module Download =

    let listHrefs (url : string) =
        let wc = new WebClient()
        let s = wc.DownloadString(url)
        let r = new Regex(@"<a href=""(?<filename>.*)""")
        r.Matches(s)
        |> Seq.map (fun x -> x.Groups.["filename"].ToString())
        |> Seq.toArray

    let listHrefsForKnownFormats (url : string) =
        let known = getKnownFileExtensions ()
        let hrefs = listHrefs url
        hrefs |> Seq.filter (fun x -> List.contains (Path.GetExtension(x).ToLowerInvariant()) known)

    let batchDownload (baseUrl : string) (targetDirectory : string) (filenames : seq<string>)  =
        if not (Directory.Exists(targetDirectory)) then
            Directory.CreateDirectory(targetDirectory) |> ignore

        let count = filenames.Count()

        let wc = new WebClient()
        let tmpFileName = Path.Combine(targetDirectory, "tmp");
        let mutable i = 1
        for filename in filenames do
            let address = baseUrl + filename
            let targetFileName = Path.Combine(targetDirectory, filename)
            
            printf "[%i/%i] downloading %s " i count address
            if File.Exists(targetFileName) then
                printfn "already downloaded"
            else
                wc.DownloadFile(address, tmpFileName)
                File.Move(tmpFileName, targetFileName)
                printfn "%s" targetFileName
                
            i <- i + 1
        
  