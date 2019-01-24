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

open Aardvark.Base
open System.IO
open System.Linq
open System.Net
open System.Text.RegularExpressions

(*
    Example data:
    
    San Simeon, airborne lidar
    https://cloud.sdsc.edu/v1/AUTH_opentopography/PC_Bulk/CA13_SAN_SIM/
    http://opentopo.sdsc.edu/datasetMetadata?otCollectionID=OT.032013.26910.2
*)

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
        
  