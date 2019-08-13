namespace Aardvark.Algodat

open System
open Aardvark.Base
open System.Threading.Tasks
open System.Threading
open System.Linq


type RansacResult<'d, 't, 's> = 
    { 
        data            : 'd[]
        test            : 't[]
        value           : 's
        modelIndices    : int[]
        inliers         : int[]
        iterations      : int
        time            : MicroTime 
    }
    member x.model = x.modelIndices |> Array.map (Array.get x.data)
    
module RansacHelpers =
    
    let rec contains (v : int) (o : int) (n : int) (arr : int[]) =
        if n < 1 then
            false
        else
            if arr.[o] = v then
                true
            else
                contains n (o+1) (n-1) arr
                
    let inline timed (f : unit -> seq<RansacResult<_,_,_>>) =
        let sw = System.Diagnostics.Stopwatch.StartNew()
        let res = f() 
        sw.Stop()
        res |> Seq.map (fun res -> { res with time = sw.MicroTime })
    
    [<Struct>]
    type ValueOption<'a> =
        | VSome of 'a
        | VNone
    
    let inline private mul (l : 'a) (r : 'a) =
        try Checked.(*) l r |> VSome
        with _ -> VNone
    
    let binom (n : int64) (k : int64) =
        if k <= 0L || n <= k then
            Some 1L
        else
            let nk = n - k
            let k, nk = 
                if k > nk then nk, k
                else k, nk
                    
            // n! / ((n-k)! * k!)
                
            // (n*(n-1)*(n-2)*...*(n-k+1)) / k!
    
            // n/k * ((n-1)/(k-1))
            // ((n/k)/(k-1)) * (n-1)
            // (n/k * (n-1))/(k-1)
    
            let mutable res = VSome 1L
            let mutable n = n
            //printf "1"
            for j in 1L .. k do
                match res with
                    | VSome r ->
                        if j = 1L then
                            //printf " * %d" n
                            res <- mul r n
                        elif n % j = 0L then
                            //printf " * (%d / %d)" n j
                            res <- mul r (n / j)
                        elif r % j = 0L then
                            //printf " / %d * %d" j n
                            res <- mul (r / j) n
                        else
                            //printf " * %d * %d" n j
                            match mul r n with
                                | VSome r ->
                                    res <- VSome (r / j)
                                | _ ->
                                    res <- VNone
                        n <- n - 1L
                    | VNone ->
                        ()
                            
            match res with
                | VSome v -> Some v
                | VNone -> None
    
    let allSubsetsOfLength (k : int) (n : int) =
        let rec allSubsetsOfLengthAcc (i : int) (k : int) (n : int) =
            if k = 0 then
                Seq.singleton []
            elif k < 0 then
                Seq.empty
            else
                if i >= n then
                    Seq.empty
                else
                    Seq.append
                        (allSubsetsOfLengthAcc (i+1) (k-1) n |> Seq.map (fun r -> i::r))
                        (allSubsetsOfLengthAcc (i+1) k n)
    
        allSubsetsOfLengthAcc 0 k n
    
open RansacHelpers

[<AutoOpen>]
module private Ransac =

    let takeRandomIndices (rand : System.Random) (d : int[]) (n : int) =
    
        let k = d.Length
        if k > n || k < 0 then 
            failwithf "cannot take %d elements out of %d" k n

        elif k = n then
            for i in 0 .. k - 1 do d.[i] <- i

        else
            let mutable rand = rand
            let mutable bad = true
            while bad do
                let mutable iter = 0
                let mutable cnt = 0
                let mutable set = Set.empty
                while cnt < d.Length && iter < d.Length * 4 do
                    let r = rand.Next(n)
                    if not (Set.contains r set) then
                        set <- Set.add r set
                        d.[cnt] <- r
                        cnt <- cnt + 1
                    iter <- iter + 1

                if cnt = d.Length then
                    bad <- false
                else
                    Log.warn "random is bad"
                    rand <- Random()
type Ransac =

    static member solve<'a, 'b, 't>(p : float, w : float, needed : int, takeRandom : Random -> int[] -> int -> unit,construct :  'a[] -> list<'b>, 
                                    countInliers : 'b -> 't[] -> int,  getInliers : 'b -> 't[] -> int[], train : 'a[], test : 't[] ) : seq<_> =
        let inlier = OptimizedClosures.FSharpFunc<'b, 't[], int>.Adapt countInliers
        timed (fun () ->

            if train.Length < needed then
                Seq.empty

            elif train.Length = needed then
                let mutable bestSol = Unchecked.defaultof<_>
                let mutable bestCnt = 0
                let sol = construct train
                match sol with
                    | [] -> 
                        ()
                    | sols ->
                        for s in sols do
                            let cnt = inlier.Invoke(s, test) 
                            if cnt >= needed && cnt > bestCnt then
                                bestSol <- s
                                bestCnt <- cnt
                        

                if bestCnt = 0 then
                    Seq.empty
                else
                    Seq.singleton {
                        data = train
                        test = test
                        value = bestSol
                        modelIndices = Array.init train.Length id
                        inliers = getInliers bestSol test
                        iterations = 1
                        time = MicroTime.Zero
                    }

            else
                let p = clamp 0.1 0.999999 p
                let w = clamp 0.01 0.99 w
            
                // https://en.wikipedia.org/wiki/Random_sample_consensus#Parameters
                // the number of iterations must be nonzero
                let iter = log (1.0 - p) / log (1.0 - pown w needed) |> ceil |> int |> max 1

                let iterateAll = 
                    match binom train.LongLength (int64 needed) with
                        | Some all -> all <= int64 iter
                        | None -> false
                

                if iterateAll then
                    let all = allSubsetsOfLength needed train.Length
                    
                    let bestModel, bestSol, bestCount = 
                        all
                            .AsParallel()
                            .SelectMany(fun comb ->
                                let index = comb |> List.toArray
                                let input : 'a[] = index |> Array.map (fun i -> train.[i])
                        
                                match construct input with
                                    | [] ->
                                        Seq.singleton (index, Unchecked.defaultof<_>, 0)
                                    | sols ->
                                        sols |> Seq.map (fun sol ->
                                            let inlierCount = inlier.Invoke(sol, test)
                                            index, sol, inlierCount
                                        )
                                    //| None ->
                                    //    input, Unchecked.defaultof<_>, 0
                            )
                            .MaxElement (fun (_,_,c) -> c)

                    if bestCount <= 0 then
                        Seq.empty
                    else
                        let indices = getInliers bestSol test
                        Seq.singleton {
                            data = train
                            test = test
                            value = bestSol
                            modelIndices = bestModel
                            inliers = indices
                            iterations = iter
                            time = MicroTime.Zero
                        }
                else
                    let l = obj()
                    let mutable bestCount = -1
                    let mutable bestSol = Unchecked.defaultof<'b>
                    let mutable bestModel = Unchecked.defaultof<int[]>
                    
                    let mutable total = iter
                    let mutable finished = 0

                    let rand = System.Random()
                    

                    Parallel.For(0, Environment.ProcessorCount, fun pi ->
                        let rand = lock rand (fun () -> System.Random(rand.Next() + Thread.CurrentThread.ManagedThreadId))
                        let index = Array.zeroCreate needed
                        let input = Array.zeroCreate needed
                        let mutable bC = -1
                        let mutable bS = Unchecked.defaultof<_>
                        let mutable bM = Unchecked.defaultof<_>

                        let mutable good = 0
                        let mutable bad = 0
                        let mutable failed = false
                        let inline reasonable() =
                            if bad > 1000 then
                                failed <- true
                                //Log.warn "too much badness"
                                false
                            else
                                true

                        while finished < total && reasonable() do
                            //takeRandomIndices rand index train.Length
                            takeRandom rand index train.Length

                            // construct a new solution
                            for i in 0 .. needed-1 do input.[i] <- train.[index.[i]]
                            let sol = input |> construct
                            match sol with
                            | [] ->
                                bad <- bad + 1

                            | sols -> 
                                Interlocked.Increment(&finished) |> ignore

                                for sol in sols do
                                    // count inliers
                                    let inlierCount = inlier.Invoke(sol, test)
                                    

                                    // store the best solution
                                    if inlierCount > bC then
                                        bC <- inlierCount
                                        bS <- sol
                                        bM <- Array.copy index

                                        let rel = max w (float inlierCount / float test.Length)
                                        let newIter = log (1.0 - p) / log (1.0 - pown rel needed) |> ceil |> int |> max 1
                                    
                                        if newIter < total then
                                            Interlocked.Change(&total, fun t -> min t newIter) |> ignore
                                    
                                good <- good + 1
                                bad <- 0
                            
                        if not failed then
                            lock l (fun () ->
                                // store the best solution
                                if bC > bestCount then
                                    bestCount <- bC
                                    bestSol <- bS
                                    bestModel <- bM
                            )
                    ) |> ignore

                    if bestCount < 0 then
                        Seq.empty
                    else
                        let indices = getInliers bestSol test
                        Seq.singleton {
                            data = train
                            test = test
                            value = bestSol
                            modelIndices = bestModel
                            inliers = indices
                            iterations = iter
                            time = MicroTime.Zero
                        }
        ) 

    static member solve (p : float, w : float, needed : int,construct :  'a[] -> list<'b>, countInliers : 'b -> 't[] -> int,  getInliers : 'b -> 't[] -> int[], train : 'a[], test : 't[] ) =
        Ransac.solve(p,w,needed,takeRandomIndices,construct,countInliers,getInliers,train,test)
    
    static member solve(p : float, w : float, needed : int, construct :  'a[] -> list<'b>, inlier : 'b -> 't -> bool, train : 'a[], test : 't[] ) =
        let countInliers (s : 'b) (t : 't[]) =
            let mutable cnt = 0
            for e in t do 
                if inlier s e then cnt <- cnt + 1
            cnt

        let getInliers (s : 'b) (t : 't[]) =
            let res = System.Collections.Generic.List<int>()
            for i in 0 .. t.Length - 1 do 
                if inlier s t.[i] then res.Add i
            CSharpList.toArray res

        Ransac.solve(p, w, needed, construct, countInliers, getInliers, train, test)

    static member solve(p : float, w : float, needed : int, construct :  'a[] -> Option<'b>, inlier : 'b -> 't -> bool, train : 'a[], test : 't[] ) =
        Ransac.solve(p, w, needed, construct >> Option.toList, inlier, train, test)
   
type RansacConfig =
    {
        probability             : float
        expectedRelativeCount   : float
    }

module RansacConfig =
    let inline probability (cfg : RansacConfig) = cfg.probability
    let inline expectedRelativeCount (cfg : RansacConfig) = cfg.expectedRelativeCount

type IRansacProblem<'d, 't, 's> =
    abstract member Solve : cfg : RansacConfig * train : 'd[] * test : 't[] -> seq<RansacResult<'d, 't, 's>>

type RansacProblem<'d, 't, 'i, 's> =
    {
        neededSamples   : int
        solve           : 'd[] -> list<'i>
        countInliers    : 'i -> 't[] -> int
        getInliers      : 'i -> 't[] -> int[]
        getSolution     : RansacResult<'d, 't, 'i> -> 's
    }


    member x.Solve(cfg : RansacConfig, train : 'd[], test : 't[]) =
        let result = 
            Ransac.solve(
                cfg.probability, cfg.expectedRelativeCount,
                x.neededSamples,
                x.solve, 
                x.countInliers,
                x.getInliers,
                train, test
            )
        result |> Seq.map ( fun res -> 
                {
                    data            = res.data
                    test            = res.test
                    value           = x.getSolution res
                    modelIndices    = res.modelIndices
                    inliers         = res.inliers
                    iterations      = res.iterations
                    time            = res.time
                })

    interface IRansacProblem<'d, 't, 's> with
        member x.Solve(cfg, train, test) = x.Solve(cfg,train,test)

module RansacProblem =
    let inline solve (cfg : RansacConfig) (train : 'd[]) (test : 't[]) (problem : IRansacProblem<'d, 't, 's>) = problem.Solve(cfg, train, test)