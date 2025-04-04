open Aardvark.Base
open Aardvark.Rendering.Text
open Aardvark.Algodat.App.Viewer


module TestSimpleCloud = 
    open Aardvark.Application
    open Aardvark.SceneGraph
    open Aardvark.Rendering
    open Aardvark.Rendering.PointSet
    open FSharp.Data.Adaptive

    let run() =
        Aardvark.Init()
        let win =
            window {
                backend Backend.GL
                debug false
            }
        
        let rand = RandomSystem()
        let points = Array.init 8192 (fun _ -> 10.0 * rand.UniformV2d().XYO |> V3f)
        let colors = Array.init points.Length (fun _ -> rand.UniformC3f().ToC4b())
        

        let scene =
            Sg.draw IndexedGeometryMode.PointList
            |> Sg.vertexAttribute' DefaultSemantic.Positions points
            |> Sg.vertexAttribute' DefaultSemantic.Colors colors
            |> Sg.shader {
                do! DefaultSurfaces.trafo
            }
            
        let pick = ref (fun _ _ _ -> [||])
        let config =
            {
                preShader = None
                postShader = None
                runtime = win.Runtime
                viewTrafo = win.View |> AVal.map (Array.item 0)
                projTrafo = win.Proj |> AVal.map (Array.item 0)
                size = win.Sizes
                colors = AVal.constant true
                pointSize = AVal.constant 50.0
                planeFit = AVal.constant true
                planeFitTol = AVal.constant 0.009
                planeFitRadius = AVal.constant 7.0
                ssao = AVal.constant false
                diffuse = AVal.constant true
                gamma = AVal.constant 1.0
                lodConfig =
                    {
                        time = win.Time
                        renderBounds = AVal.constant false
                        stats = cval (Unchecked.defaultof<_>)
                        pickTrees = None
                        alphaToCoverage = false
                        maxSplits = AVal.constant 1
                        splitfactor = AVal.constant 1.0
                        budget = AVal.constant -1
                    }
                ssaoConfig = 
                    {
                        radius = AVal.constant 0.04
                        threshold = AVal.constant 0.1
                        sigma = AVal.constant 5.0
                        sharpness = AVal.constant 4.0
                        sampleDirections = AVal.constant 2
                        samples = AVal.constant 4
                    }
                pickCallback = Some pick
            }
            
        let scene =
            Sg.ofList [
                Sg.wrapPointCloudSg config scene
                //scene
                
                Sg.box' C4b.Red Box3d.Unit
                |> Sg.shader {
                    do! DefaultSurfaces.trafo
                }
            ]
        win.Scene <- scene
        
        win.Mouse.Move.Values.Add (fun _ ->
            let loc = AVal.force win.Mouse.Position
            pick.Value loc.Position 5 5 |> printfn "%A"
        )
        
        
        win.Run()

[<EntryPoint>]
let main args =  
    //let pts = @"C:\bla\pts\lowergetikum 20230321.e57"
    //import pts  @"C:\bla\store\lowergetikum\data.bin" "a" (Args.parse [||])
    //exit 0

    //view @"C:\stores\innen_store" ["a2b7e0c1-e672-48d3-8958-9ff8678f2dc4"] (Args.parse [||])
    //view @"C:\Users\sm\Downloads\C_31EN2.LAZ.store" [File.readAllText @"C:\Users\sm\Downloads\C_31EN2.LAZ.key"] (Args.parse [||])
    //view @"C:\Users\sm\Downloads\C_30DN2.LAZ.store" [File.readAllText @"C:\Users\sm\Downloads\C_30DN2.LAZ.key"] (Args.parse [||])
    //view @"C:\Users\sm\Downloads\test.store" ["128330b1-8761-4a07-b160-76bcd7e2f70a"; "ab2f6f76-7eae-47c9-82d1-ad28b816abb9"] (Args.parse [||])
    
    let store = @"\\heap\aszabo\rmdata\Data\stores\SD-Speicher.e57.000.exported\data.uds"
    let key = Path.combine [System.IO.Path.GetDirectoryName store;"key.txt"] |> File.readAllText
    //view store [key] (Args.parse [||])

    let args = Args.parse args
    
    match args.command with

    | Some (Info filename) -> info filename args

    | Some (Import (filename, store, key)) -> import filename store key args
      
    | Some (View (store, key)) ->
        //view store [key] args
        ()
    | Some Gui ->
        failwith "not implemented"
       
    | Some (Download (baseurl, targetdir)) -> download baseurl targetdir args
      
    | None ->
        printUsage()

    0
