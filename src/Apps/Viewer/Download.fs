(*
    Copied from https://github.com/aardvark-community/hum.
*)
namespace Aardvark.Algodat.App.Viewer

open Aardvark.Base
open System.IO
open System.Linq
open System.Net
open System.Net.Http
open System.Text.RegularExpressions

module Download =

    let listHrefs (url : string) =
        let wc = new HttpClient()
        let s = wc.GetStringAsync(url).Result
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

        let wc = new HttpClient()
        let tmpFileName = Path.Combine(targetDirectory, "tmp");
        let mutable i = 1
        for filename in filenames do
            let address = baseUrl + filename
            let targetFileName = Path.Combine(targetDirectory, filename)
            
            printf "[%i/%i] downloading %s " i count address
            if File.Exists(targetFileName) then
                printfn "already downloaded"
            else
                let fs = File.OpenWrite(tmpFileName);
                wc.GetStreamAsync(address).Result.CopyToAsync(fs).Wait()
                fs.Flush()
                fs.Close()
                File.Move(tmpFileName, targetFileName)
                printfn "%s" targetFileName
                
            i <- i + 1
        
  