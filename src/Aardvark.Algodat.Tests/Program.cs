using Aardvark.Base;
using Aardvark.Base.Coder;
using Aardvark.Data;
using Aardvark.Data.Points;
using Aardvark.Data.Points.Import;
using Aardvark.Geometry.Points;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Uncodium.SimpleStore;

namespace Aardvark.Geometry.Tests
{
    public class Program
    {
        internal static void CreateStore(string filename, string storePath, string key, double minDist)
        {
            using (var store = new SimpleDiskStore(storePath).ToPointCloudStore())
            {
                var config = ImportConfig.Default
                    .WithStorage(store)
                    .WithKey(key)
                    .WithVerbose(true)
                    .WithMaxDegreeOfParallelism(0)
                    .WithMinDist(minDist)
                    .WithNormalizePointDensityGlobal(true)
                    ;

                Report.BeginTimed($"importing {filename}");
                var ps = PointCloud.Import(filename, config);
                Report.EndTimed();
            }
        }

        internal static void PerfTestJuly2019()
        {
            var filename = @"T:\Vgm\Data\2017-10-20_09-44-27_1mm_shade_norm_5pp - Cloud.pts";
            //var filename = @"T:\Vgm\Data\JBs_Haus.pts";

            var config = ImportConfig.Default
                .WithInMemoryStore()
                .WithRandomKey()
                .WithVerbose(true)
                .WithMaxDegreeOfParallelism(0)
                .WithMinDist(0.01)
                .WithNormalizePointDensityGlobal(true)
                ;

            Report.BeginTimed("total");

            var chunks = Pts.Chunks(filename, config.ParseConfig);
            var pc = PointCloud.Chunks(chunks, config);

            Report.EndTimed();

            Report.Line($"count -> {pc.PointCount}");
        }
        internal static void TestE57()
        {
            var sw = new Stopwatch();

            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            var filename = @"T:\Vgm\Data\E57\43144_K2_1-0_int_3dWorx_Error_bei-Import.e57";
            var fileSizeInBytes = new FileInfo(filename).Length;

            var info = E57.E57Info(filename, ParseConfig.Default);
            Report.Line($"total bounds: {info.Bounds}");
            Report.Line($"total count : {info.PointCount:N0}");

            var config = ImportConfig.Default
                .WithInMemoryStore()
                .WithRandomKey()
                .WithVerbose(true)
                .WithMaxDegreeOfParallelism(0)
                .WithMinDist(0.01)
                ;
            
            Report.BeginTimed("total");

            var chunks = E57
                .Chunks(filename, config.ParseConfig)
                //.Take(10)
                //.AsParallel()
                //.Select(x => x.ImmutableFilterMinDistByCell(new Cell(x.BoundingBox), config))
                //.Select(x => x.ImmutableFilterSequentialMinDistL1(0.01))
                //.ToArray()
                ;
            var pc = PointCloud.Chunks(chunks, config);

            Report.EndTimed();

            //var memstore = new SimpleMemoryStore().ToPointCloudStore();

            // classic
            //var foo = chunks.AsParallel().Select(x => InMemoryPointSet.Build(x, 8192).ToPointSetCell(memstore));
            //var r = foo.MapReduceParallel((first, second, ct2) =>
            //{
            //    var merged = first.Merge(second, 8192, null, default);
            //    memstore.Add(Guid.NewGuid().ToString(), merged, default);
            //    Report.Line($"{first.PointCountTree,12:N0} + {second.PointCountTree,12:N0} -> {merged.PointCountTree,12:N0}");
            //    return merged;
            //}, 0);

            //// test 1
            //Report.BeginTimed("merging all chunks");
            //var chunk = Chunk.Empty;
            //foreach (var x in chunks) chunk = Chunk.ImmutableMerge(chunk, x);
            //Report.Line($"points     : {chunk.Count:N0}");
            //Report.EndTimed();

            //Report.BeginTimed("filter mindist");
            //chunk = chunk.ImmutableFilterMinDistByCell(0.01, new Cell(chunk.BoundingBox));
            //Report.Line($"points     : {chunk.Count:N0}");
            //Report.EndTimed();

            //Report.BeginTimed("slots");
            //var slots = chunk.Positions.GroupBy(p => (V3i)p).ToArray();
            //Report.Line($"[slots] {slots.Length}");
            //var slotsCountAvg = slots.Sum(x => x.Count() / (double)slots.Length);
            //var slotsCountMin = slots.Min(x => x.Count());
            //var slotsCountMax = slots.Max(x => x.Count());
            //var slotsCountSd = (slots.Sum(x => (x.Count() - slotsCountAvg).Square()) / slots.Length).Sqrt();
            //Report.Line($"[slots] count avg = {slotsCountAvg}");
            //Report.Line($"[slots] count min = {slotsCountMin}");
            //Report.Line($"[slots] count max = {slotsCountMax}");
            //Report.Line($"[slots] count sd  = {slotsCountSd}");
            //Report.EndTimed();


            //Report.BeginTimed("build octree");
            //var octree = InMemoryPointSet.Build(chunk, 8192);
            //Report.EndTimed();

            //Report.BeginTimed("toPointSetCell");
            //var psc = octree.ToPointSetCell(memstore);
            //var ps = new PointSet(memstore, "foo", psc.Id, 8192);
            //Report.EndTimed();

            //Report.BeginTimed("generate lod");
            //var psWithNormals = ps.GenerateLod(config);
            //Report.EndTimed();



            //Report.Line($"chunks: {foo.Count}");

            

            //return;

            //var lastProgress = 0.0;
            //config = ImportConfig.Default
            //    .WithInMemoryStore()
            //    .WithRandomKey()
            //    .WithVerbose(false)
            //    .WithMaxDegreeOfParallelism(0)
            //    .WithMinDist(0.01)
            //    .WithProgressCallback(x =>
            //    {
            //        if (x < lastProgress) Console.WriteLine("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAH"); else lastProgress = x;
            //        Console.WriteLine($"[PROGRESS]; {x,8:0.00000}; {sw.Elapsed.TotalSeconds:0.00}");
            //    })
            //    ;

            //chunks = E57.Chunks(filename, config.ParseConfig);
            //var pointcloud = PointCloud.Chunks(chunks, config);
            //Console.WriteLine($"pointcloud.PointCount  : {pointcloud.PointCount}");
            //Console.WriteLine($"pointcloud.Bounds      : {pointcloud.Bounds}");
            //Console.WriteLine($"pointcloud.BoundingBox : {pointcloud.BoundingBox}");

            //var chunks = E57.Chunks(filename, config);
            //var pointcloud = PointCloud.Chunks(chunks, config);
            //Console.WriteLine($"pointcloud.PointCount  : {pointcloud.PointCount}");
            //Console.WriteLine($"pointcloud.Bounds      : {pointcloud.Bounds}");
            //Console.WriteLine($"pointcloud.BoundingBox : {pointcloud.BoundingBox}");


            //var leafLodPointCount = 0L;
            //pointcloud.Root.Value.ForEachNode(true, n => { if (n.IsLeaf) leafLodPointCount += n.LodPositionsAbsolute.Length; });
            //Console.WriteLine($"leaf lod point count :{leafLodPointCount}");

            //foreach (var chunk in chunks)
            //{
            //    for (var i = 0; i < chunk.Count; i++)
            //    {
            //        Console.WriteLine($"{chunk.Positions[i]:0.000} {chunk.Colors?[i]}");
            //    }
            //}

            //Console.WriteLine($"chunks point count: {chunks.Sum(x => x.Positions.Count)}");
            //Console.WriteLine($"chunks bounds     : {new Box3d(chunks.SelectMany(x => x.Positions))}");

            //using (var w = File.CreateText("test.txt"))
            //{
            //    foreach (var chunk in chunks)
            //    {
            //        for (var i = 0; i < chunk.Count; i++)
            //        {
            //            var p = chunk.Positions[i];
            //            var c = chunk.Colors[i];
            //            w.WriteLine($"{p.X} {p.Y} {p.Z} {c.R} {c.G} {c.B}");
            //        }
            //    }
            //}
            //return;

            /*
            var stream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
            ASTM_E57.VerifyChecksums(stream, fileSizeInBytes);
            var header = ASTM_E57.E57FileHeader.Parse(stream);

            //Report.BeginTimed("parsing E57 file");
            //var take = int.MaxValue;
            //var data = header.E57Root.Data3D.SelectMany(x => x.StreamCartesianCoordinates(false)).Take(take).Chunk(1000000).ToList();
            //Report.EndTimed();
            //Report.Line($"#points: {data.Sum(xs => xs.Length)}");

            foreach (var p in header.E57Root.Data3D.SelectMany(x => x.StreamPoints(false))) Console.WriteLine(p.Item1);

            //var ps = PointCloud.Parse(filename, ImportConfig.Default)
            //    .SelectMany(x => x.Positions)
            //    .ToArray()
            //    ;
            */
        }

        internal static void TestImport()
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            var filename = @"T:\Vgm\Data\erdgeschoss.e57";

            var chunks = E57.Chunks(filename, ParseConfig.Default);

            var queue = new Queue<Chunk>();
            var parseChunksDone = false;

            Report.BeginTimed("total");

            Task.Run(() =>
            {
                foreach (var x in chunks.Take(10))
                {
                    lock (queue) queue.Enqueue(x);
        }
                Report.Line("set parseChunksDone");
                parseChunksDone = true;
            });

            Dictionary<Cell, (List<V3f>, List<C4b>, V3d)> Grid(Chunk x)
            {
                var result = new Dictionary<Cell, (List<V3f>, List<C4b>, V3d)>();
                var dups = new HashSet<V3f>();
                for (var i = 0; i < x.Count; i++)
                {
                    var key = new Cell((V3l)x.Positions[i], 0);
                    if (!result.TryGetValue(key, out var val))
                    {
                        val = (new List<V3f>(), new List<C4b>(), key.GetCenter());
                        result[key] = val;
                    }

                    var dupKey = (V3f)(x.Positions[i]).Round(3);
                    if (dups.Add(dupKey))
                    {
                        val.Item1.Add((V3f)(x.Positions[i] - val.Item3));
                        val.Item2.Add(x.Colors[i]);
                    }
                }
                return result;
            }

            var global = new Dictionary<Cell, (List<V3f>, List<C4b>, V3d)>();

            void MergeIntoGlobal(Dictionary<Cell, (List<V3f>, List<C4b>, V3d)> x)
            {
                lock (global)
                {
                    foreach (var kv in x)
                    {
                        if (!global.TryGetValue(kv.Key, out var gval))
                        {
                            gval = kv.Value;
                            global[kv.Key] = gval;
                        }
                        else
                        {
                            gval.Item1.AddRange(kv.Value.Item1);
                            gval.Item2.AddRange(kv.Value.Item2);
                        }
                    }
                }
            }

            var globalGridReady = new ManualResetEventSlim();

            Task.Run(async () =>
            {
                var processedChunksCount = 0;
                var processedPointsCount = 0L;
                var tasks = new List<Task>();
                while (queue.Count > 0 || !parseChunksDone)
                {
                    var c = Chunk.Empty;
                    bool wait = false;
                    lock (queue)
                    {
                        if (queue.Count > 0) c = queue.Dequeue();
                        else wait = true;
                    }
                    if (wait) { await Task.Delay(1000); continue; }

                    tasks.Add(Task.Run(() =>
                    {
                        var grid = Grid(c);
                        MergeIntoGlobal(grid);

                        var a = Interlocked.Increment(ref processedChunksCount);
                        var b = Interlocked.Add(ref processedPointsCount, c.Count);
                        Report.Line($"processed chunk #{a} with {c.Count:N0} points ({b:N0} total points)");
                    }));
                }

                await Task.WhenAll(tasks);

                Report.Line("set globalGridReady");
                globalGridReady.Set();
            });

            globalGridReady.Wait();
            Report.Line($"global grid cell  count: {global.Count:N0}");
            Report.Line($"            point count: {global.Sum(x => x.Value.Item1.Count):N0}");

            Report.EndTimed("total");

            //Report.BeginTimed("parsing");
            //var foo = chunks.ToArray();
            //Report.EndTimed();

            //Console.WriteLine($"chunk count: {foo.Length}");
            //Console.WriteLine($"point count: {foo.Sum(x => x.Count)}");





            //var store = new SimpleDiskStore(@"C:\Users\sm\Desktop\Staatsoper.store").ToPointCloudStore(new LruDictionary<string, object>(1024 * 1024 * 1024));

            //var config = ImportConfig.Default
            //    .WithStorage(store)
            //    .WithKey("staatsoper")
            //    .WithVerbose(true)
            //    ;

            //Report.BeginTimed("importing");
            //var pointcloud = PointCloud.Import(filename, config);
            //Report.EndTimed();
            //store.Flush();
        }

        internal static void TestImportPts(string filename)
        {
            var chunks = Pts.Chunks(filename, ParseConfig.Default);

            Console.WriteLine(filename);
            var sw = new Stopwatch();
            var count = 0L;
            sw.Start();
            foreach (var chunk in chunks)
            {
                Console.WriteLine($"    {chunk.Count}, {chunk.BoundingBox}");
                count += chunk.Count;
            }
            sw.Stop();
            Console.WriteLine($"    {count:N0} points");
            Console.WriteLine($"    {sw.Elapsed} ({(int)(count / sw.Elapsed.TotalSeconds):N0} points/s)");
        }

        internal static void TestKNearest()
        {
            var sw = new Stopwatch();
            var rand = new Random();

            Report.BeginTimed("generating point clouds");
            var cloud0 = CreateRandomPointsInUnitCube(1000000, 8192);
            var cloud1 = CreateRandomPointsInUnitCube(1000000, 8192);
            Report.EndTimed();

            var ps0 = cloud0.QueryAllPoints().SelectMany(chunk => chunk.Positions).ToArray();
            
            sw.Restart();
            for (var i = 0; i < ps0.Length; i++)
            {
                var p = cloud1.QueryPointsNearPoint(ps0[i], 0.1, 1);
                if (i % 100000 == 0) Console.WriteLine($"{i,20:N0}     {sw.Elapsed}");
            }
            sw.Stop();
            Console.WriteLine($"{ps0.Length,20:N0}     {sw.Elapsed}");

            PointSet CreateRandomPointsInUnitCube(int n, int splitLimit)
            {
                var r = new Random();
                var ps = new V3d[n];
                for (var i = 0; i < n; i++) ps[i] = new V3d(r.NextDouble(), r.NextDouble(), r.NextDouble());
                var config = ImportConfig.Default
                    .WithStorage(PointCloud.CreateInMemoryStore(new LruDictionary<string, object>(1024*1024*1024)))
                    .WithKey("test")
                    .WithOctreeSplitLimit(splitLimit)
                    ;
                return PointCloud.Chunks(new Chunk(ps, null), config);
            }
        }

        internal static void TestLoadOldStore()
        {
            var store = new SimpleDiskStore(@"T:\Vgm\Stores\referenz_2019_21_store").ToPointCloudStore(cache: default);
            var pc = store.GetPointSet("770ed498-5544-4313-9873-5449f2bd823e");
            var root = store.GetPointCloudNode("e06a1e87-5ab1-4c73-8c3f-3daf1bdac1d9");
        }

        internal static void CopyTest()
        {
            var filename = @"T:\Vgm\Data\JBs_Haus.pts";
            var rootKey = "097358dc-d89a-434c-8a4e-fe03c063d886";
            var splitLimit = 65536;
            var minDist = 0.001;

            var store1Path = @"T:\Vgm\Stores\copytest1";
            //var store2Path = @"T:\Vgm\Stores\copytest2";
            //var store3Path = @"T:\Vgm\Stores\copytest3";
            //var store4Path = @"T:\Vgm\Stores\JBs_Haus.pts";

            // create stores
            var store1 = new SimpleDiskStore(store1Path).ToPointCloudStore();
            //var store2 = new SimpleDiskStore(store2Path).ToPointCloudStore();

            // import point cloud into store1
            Report.BeginTimed("importing");
            var config = ImportConfig.Default
                .WithStorage(store1)
                .WithKey(rootKey)
                .WithOctreeSplitLimit(splitLimit)
                .WithMinDist(minDist)
                .WithNormalizePointDensityGlobal(true)
                .WithVerbose(true)
                ;
            var pointcloud = PointCloud.Import(filename, config);

            Report.EndTimed();
            store1.Flush();

            return;
            //if (!Directory.Exists(store3Path)) { Directory.CreateDirectory(store3Path); Thread.Sleep(1000); }
            //if (!Directory.Exists(store4Path)) { Directory.CreateDirectory(store4Path); Thread.Sleep(1000); }

            //// copy point cloud from store1 to store2
            //var pc1 = store1.GetPointSet(rootKey);
            //var root1 = pc1.Root.Value;
            //var totalNodes = root1.CountNodes(outOfCore: true);
            //store2.Add(rootKey, pc1);
            //Report.BeginTimed("copy");
            //var totalBytes = 0L;
            ////pc1.Root.Value.ForEachNode(outOfCore: true, n => Report.Line($"{n.Id} {n.PointCountCell,12:N0}"));
            //Convert(root1.Id);
            //Report.Line($"{totalNodes}");
            //Report.EndTimed();
            //store2.Flush();

            //// meta
            //var rootJson = JObject.FromObject(new
            //{
            //    rootId = root1.Id.ToString(),
            //    splitLimit = splitLimit,
            //    minDist = minDist,
            //    pointCount = root1.PointCountTree,
            //    bounds = root1.BoundingBoxExactGlobal,
            //    centroid = (V3d)root1.CentroidLocal + root1.Center,
            //    centroidStdDev = root1.CentroidLocalStdDev,
            //    cell = root1.Cell,
            //    totalNodes = totalNodes,
            //    totalBytes = totalBytes,
            //    gzipped = false
            //});
            //File.WriteAllText(Path.Combine(store3Path, "root.json"), rootJson.ToString(Formatting.Indented));

            //rootJson["gzipped"] = true;
            //File.WriteAllText(Path.Combine(store4Path, "root.json"), rootJson.ToString(Formatting.Indented));

            //void Convert(Guid key)
            //{
            //    if (key == Guid.Empty) return;

            //    var (def, raw) = store1.GetDurable(key);
            //    var node = raw as IDictionary<Durable.Def, object>;
            //    node.TryGetValue(Durable.Octree.SubnodesGuids, out var subnodeGuids);

            //    // write inlined node to store
            //    var inlinedNode = store1.ConvertToInline(node);
            //    var inlinedBlob = store2.Add(key, Durable.Octree.Node, inlinedNode, false);
            //    totalBytes += inlinedBlob.Length;
            //    //Report.Line($"stored node {key}");

            //    // write blob as file
            //    File.WriteAllBytes(Path.Combine(store3Path, key.ToString()), inlinedBlob);
                
            //    // write blob as file (gzipped)
            //    using (var fs = File.OpenWrite(Path.Combine(store4Path, key.ToString())))
            //    using (var zs = new GZipStream(fs, CompressionLevel.Fastest))
            //    {
            //        zs.Write(inlinedBlob, 0, inlinedBlob.Length);
            //    }

            //    // children
            //    if (subnodeGuids != null)
            //        foreach (var x in (Guid[])subnodeGuids) Convert(x);
            //}
        }

        public static void TestCreateStore(string filepath, double minDist)
        {
            var filename = Path.GetFileName(filepath);
            var storename = Path.Combine(@"T:\Vgm\Stores\", filename);
            var store = new SimpleDiskStore(storename).ToPointCloudStore(new LruDictionary<string, object>(1024 * 1024 * 1024));

            var config = ImportConfig.Default
                .WithStorage(store)
                .WithKey(filename)
                .WithVerbose(true)
                //.WithOctreeSplitLimit(65536)
                .WithMaxDegreeOfParallelism(0)
                .WithMinDist(minDist)
                ;

            Report.BeginTimed("importing");
            PointCloud.Import(filepath, config);
            Report.EndTimed();
            store.Flush();
            store.Dispose();
                }

        public static void ExportExamples(string filepath, bool collapse, bool gzipped, int? positionsRoundedToNumberOfDigits)
            {
            //// Example 1: export point cloud to folder
            //{
            //    var ct = new CancellationTokenSource(10000);
            //    using (var storeSource = new SimpleDiskStore(@"T:\Vgm\Stores\kindergarten").ToPointCloudStore())
            //    using (var storeTarget = new SimpleFolderStore(@"T:\Vgm\Stores\kindergartenExported").ToPointCloudStore())
            //    {
            //        storeSource.ExportPointSet("kindergarten", storeTarget, info => Console.Write($" ({info.Progress * 100,6:0.00}%)"), true, ct.Token);
            //    }
            //}

            //// Example 2: export point cloud to another store
            //{
            //    using (var storeSource = new SimpleDiskStore(@"T:\Vgm\Stores\copytest1").ToPointCloudStore())
            //    using (var storeTarget = new SimpleDiskStore(@"T:\Vgm\Stores\exportStore").ToPointCloudStore())
            //    {
            //        storeSource.ExportPointSet("097358dc-d89a-434c-8a4e-fe03c063d886", storeTarget, info => Console.WriteLine($"{info.Progress:0.00}"), true, CancellationToken.None);
            //        storeTarget.Flush();
            //    }
            //}

            // Example 3: inline point cloud nodes
            {
                var key = Path.GetFileName(filepath);
                var targetFolder = $@"T:\Vgm\Stores\{key}.upload2";
                using (var storeSource = new SimpleDiskStore($@"T:\Vgm\Stores\{key}").ToPointCloudStore())
                using (var storeTarget = new SimpleFolderStore(targetFolder).ToPointCloudStore())
                {
                    var foo = storeSource.GetPointSet(key);
                    var bar = foo.Root.Value.Id;

                    var config = new InlineConfig(
                        collapse, gzipped, positionsRoundedToNumberOfDigits,
                        x => Report.Line($"[progress] {x,7:0.000}")
                        );
                    storeSource.InlineOctree(bar, storeTarget, config);
                    storeTarget.Flush();

                    // meta
                    var pointset = storeSource.GetPointSet(key);
                    var root = pointset.Root.Value;
                    var rootJson = JObject.FromObject(new
                    {
                        Bounds = root.BoundingBoxExactGlobal,
                        Cell = root.Cell,
                        Centroid = (V3d)root.CentroidLocal + root.Center,
                        CentroidStdDev = root.CentroidLocalStdDev,
                        GZipped = gzipped,
                        PointCount = root.PointCountTree,
                        PointSetId = pointset.Id,
                        RootId = root.Id.ToString(),
                        TotalNodes = root.CountNodes(true),
                    });

                    File.WriteAllText(
                        Path.Combine(targetFolder, "root.json"), 
                        rootJson.ToString(Formatting.Indented)
                        );
                }


            }
        }

        public static void EnumerateCellsTest()
        {
            var store = new SimpleDiskStore(@"T:\Vgm\Pinter_Dachboden_3Dworx_Store\Pinter_Dachboden_store").ToPointCloudStore(cache: default);
            var pc = store.GetPointSet("1bd9ab70-0245-4bf2-bbad-8929ae94105e");
            var foo = store.GetPointCloudNode("1bd9ab70-0245-4bf2-bbad-8929ae94105e");
            var root = pc.Root.Value;

            foreach (var x in root.EnumerateCells(0))
            {
                var chunks = x.GetPoints(0).ToArray();
                Console.WriteLine($"{x.Cell,20} {chunks.Sum(c => c.Count),8:N0}    {new Box3d(chunks.Select(c => c.BoundingBox)):0.00}");
            }
        }

        public static void EnumerateCells2dTest()
        {
            var inputFile = @"T:\Vgm\Data\E57\aibotix_ground_points.e57";

            var storeName = Path.Combine(@"T:\Vgm\Stores", Path.GetFileName(inputFile));
            var key = Path.GetFileName(storeName);
            //CreateStore(inputFile, storeName, key, 0.005);
            
            var store = new SimpleDiskStore(storeName).ToPointCloudStore(cache: default);
            var pc = store.GetPointSet(key);
            var root = pc.Root.Value;

            //var oSize = 2;
            //var iSize = 1;
            var cellExponent = 5;

            //Report.BeginTimed("total");
            //var xs = root.EnumerateCellColumns(cellExponent);
            //var i = 0;
            //foreach (var x in xs)
            //{
            //    var cs0 = x.GetPoints(int.MaxValue).ToArray();
            //    //if (cs0.Length == 0) continue;
            //    //var cs1 = x.GetPoints(0, outer: new Box2i(new V2i(-iSize, -iSize), new V2i(+iSize, +iSize))).ToArray();
            //    //var cs2 = x.GetPoints(0, outer: new Box2i(new V2i(-oSize, -oSize), new V2i(+oSize, +oSize)), inner: new Box2i(new V2i(-iSize, -iSize), new V2i(+iSize, +iSize))).ToArray();
            //    //Report.Line($"[{x.Cell.X,3}, {x.Cell.Y,3}, {x.Cell.Exponent,3}] {cs0.Sum(c => c.Count),10:N0} {cs1.Sum(c => c.Count),10:N0} {cs2.Sum(c => c.Count),10:N0}");
            //    //if (++i % 17 == 0) 
            //    if (cs0.Sum(c => c.Count) > 0)
            //        Report.Line($"[{x.Cell.X,3}, {x.Cell.Y,3}, {x.Cell.Exponent,3}] {cs0.Sum(c => c.Count),10:N0}");
            //}
            //Report.End();

            //for (cellExponent = 11; cellExponent >= 0; cellExponent--)
            //{
            //    Report.BeginTimed($"[old] e = {cellExponent,3}");
            //    var xs = root.EnumerateCellColumns(cellExponent);
            //    var totalPointCount = 0L;
            //    var count = 0L;
            //    foreach (var x in xs)
            //    {
            //        count++;
            //        var cs0 = x.GetPoints(int.MaxValue).ToArray();
            //        var tmp = cs0.Sum(c => c.Count);
            //        totalPointCount += tmp;
            //        //Report.Line($"[{x.Cell.X,3}, {x.Cell.Y,3}, {x.Cell.Exponent,3}] {tmp,10:N0}");
            //    }
            //    //if (sum != root.PointCountTree) throw new Exception();
            //    Report.End($" | cols = {count,12:N0} | points = {totalPointCount,12:N0}");
            //}

            for (cellExponent = 11; cellExponent >= -10; cellExponent--)
            {
                Report.BeginTimed($"[new] e = {cellExponent,3}");
                var ys = new Queries.ColZ(root).EnumerateColumns(cellExponent);
                var totalPointCount = 0L;
                var count = 0L;
                foreach (var y in ys)
                {
                    count++;
                    totalPointCount += y.CountTotal;
                    //Report.Line($"[{y.Footprint.X,3}, {y.Footprint.Y,3}, {y.Footprint.Exponent,3}] {y.CountTotal,10:N0}");
                }
                if (totalPointCount != root.PointCountTree) throw new Exception();
                Report.End($" | cols = {count,12:N0} | points = {totalPointCount,12:N0}");
            }
        }

        internal static void DumpPointSetKeys()
        {
            var storeFolder = @"T:\Vgm\Pinter_Dachboden_3Dworx_Store\Pinter_Dachboden_store";

            var sds = new SimpleDiskStore(storeFolder);
            var keys = sds.SnapshotKeys();
            var store = sds.ToPointCloudStore(cache: default);

            foreach (var k in keys) if (k.StartsWith("dd0f")) Console.WriteLine(k);
            Console.WriteLine($"mmmh: {keys.SingleOrDefault(x => x == "dd0fe31f-ea1f-4bea-a1b4-f6c8bf314598")}");

            Console.CursorVisible = false;
            for (var i = 0; i < keys.Length; i++)
            {
                var k = keys[i];
                try
                {
                    var pc = store.GetPointSet(k);
                    Console.WriteLine($"\r{k} (count={pc.PointCount:N0})");
                }
                catch { }
                if (i % 100 == 0) Console.Write($"\r{100.0 * (i + 1) / keys.Length,6:0.00}%");
            }
            Console.Write($"\r{100,6:0.00}%");
            Console.CursorVisible = true;
        }

        internal static void LisaTest()
        {
            var path2store = @"E:\OEBB\stores\store_wien_small_labels";//store_wien_small_labels - Copy
            var key = "stream_3899";

            var cache = new LruDictionary<string, object>(1024 * 1024 * 1024);
            var store = PointCloud.OpenStore(path2store, cache);
            var pointset = store.GetPointSet(key);

            var ray = new Ray3d(
                new V3d(-160017.518571374, 246513.963542402, 2372.20790824948),
                new V3d(0.838664911597797, 0.54451231482662, -0.0121451659855219)
                );

            var ps = pointset.QueryPointsNearRay(ray, 0.01).SelectMany(x => x.Positions).ToArray();
            Console.WriteLine($"{ps.Length:N0}");
        }

        internal static void HeraTest()
        {
            Report.Line("Hera Test");

            var separators = new[] { '\t', ' ' };
            var culture = CultureInfo.InvariantCulture;
            var inputFile = @"T:\Hera\impact.0014";

            var storePath = Path.Combine(@"T:\Vgm\Stores", Path.GetFileName(inputFile));
            var key = Path.GetFileName(storePath);

            Report.Line($"inputFile = {inputFile}");
            Report.Line();

            //Report.BeginTimed("parsing (with string split and double.Parse)");
            //var lineCount = 0;
            //foreach (var line in File.ReadLines(inputFile))
            //{
            //    lineCount++;
            //    var ts = line.Split(separators, StringSplitOptions.RemoveEmptyEntries);
            //    var p = new V3d(double.Parse(ts[0], culture), double.Parse(ts[1], culture), double.Parse(ts[2], culture));
            //    var v = new V3d(double.Parse(ts[3], culture), double.Parse(ts[4], culture), double.Parse(ts[5], culture));
            //    //if (lineCount % 100000 == 0) Report.Line($"[{lineCount}]");
            //}
            //Report.Line($"{lineCount} lines");
            //Report.End();


            Report.Line();

            Report.BeginTimed($"processing {inputFile}");

            Report.BeginTimed("parsing (with Aardvark.Data.Points.Ascii)");

            var lineDef = new[] {
                Ascii.Token.PositionX, Ascii.Token.PositionY, Ascii.Token.PositionZ,
                Ascii.Token.VelocityX, Ascii.Token.VelocityY, Ascii.Token.VelocityZ,
                //Ascii.Token.Skip, Ascii.Token.Skip, Ascii.Token.Skip, Ascii.Token.Skip, Ascii.Token.Skip,
                //Ascii.Token.Skip, Ascii.Token.Skip, Ascii.Token.Skip, Ascii.Token.Skip, Ascii.Token.Skip,
                //Ascii.Token.Skip, Ascii.Token.Skip, Ascii.Token.Skip, Ascii.Token.Skip, Ascii.Token.Skip,
                //Ascii.Token.Skip, Ascii.Token.Skip, Ascii.Token.Skip, Ascii.Token.Skip, Ascii.Token.Skip,
                //Ascii.Token.Skip
            };
            var chunks = Ascii.Chunks(inputFile, lineDef, ParseConfig.Default).ToArray();

            var lineCount = 0;
            foreach (var chunk in chunks)
            {
                lineCount += chunk.Count;
            }
            Report.Line($"{lineCount} lines");
            Report.EndTimed();

            Report.Line();

            Report.BeginTimed("octree, normals, lod");
            PointSet pointset;
            using (var store = new SimpleDiskStore(storePath).ToPointCloudStore())
            {
                var config = ImportConfig.Default
                    //.WithStorage(store)
                    .WithInMemoryStore()
                    .WithKey(key)
                    .WithVerbose(false)
                    .WithMaxDegreeOfParallelism(0)
                    .WithMinDist(0)
                    .WithNormalizePointDensityGlobal(true)
                    ;

                pointset = PointCloud.Import(chunks, config);
            }
            Report.EndTimed();

            Report.BeginTimed("flattening");
            var flat = Chunk.ImmutableMerge(pointset.Root.Value.Collect(int.MaxValue));
            var data = ImmutableDictionary<Durable.Def, object>.Empty
                .Add(Durable.Octree.PositionsGlobal3d, flat.Positions.ToArray())
                .Add(Durable.Octree.Normals3f, flat.Normals.ToArray())
                .Add(Durable.Octree.Velocities3f, flat.Velocities.ToArray())
                ;
            Report.EndTimed();

            Report.BeginTimed("serializing");
            byte[] buffer;
            using (var ms = new MemoryStream())
            //using (var zs = new GZipStream(ms, CompressionLevel.Optimal))
            using (var bw = new BinaryWriter(ms))
            {
                Data.Codec.Encode(bw, Durable.Primitives.DurableMap, data);
                bw.Flush();
                buffer = ms.ToArray();
            }
            Report.EndTimed();

            Report.BeginTimed("deserializing");
            using (var ms = new MemoryStream(buffer))
            //using (var zs = new GZipStream(ms, System.IO.Compression.CompressionMode.Decompress))
            using (var br = new BinaryReader(ms))
            {
                var (def, o) = Data.Codec.Decode(br);
                var dict = (ImmutableDictionary<Durable.Def, object>)o;
                var ps = (V3d[])dict[Durable.Octree.PositionsGlobal3d];
                var ns = (V3f[])dict[Durable.Octree.Normals3f];
                var vs = (V3f[])dict[Durable.Octree.Velocities3f];
                Report.Line($"positions : {ps.Length}");
                Report.Line($"normals   : {ns.Length}");
                Report.Line($"velocities: {vs.Length}");
            }
            Report.EndTimed();

            Report.EndTimed();
        }

        public static void Main(string[] args)
        {
            HeraTest();

            //EnumerateCells2dTest();

            //TestE57();

            //LisaTest();

            //DumpPointSetKeys();

            //var filepath = @"T:\Vgm\Data\JBs_Haus.pts";
            ////var filepath = @"T:\Vgm\Data\Technologiezentrum_Teil1.pts";
            ////var filepath = @"T:\Vgm\Data\E57\Staatsoper.e57";
            //var filepath = @"T:\Vgm\Data\Kindergarten.pts";
            //TestCreateStore(filepath, 0.001);
            //ExportExamples(filepath, collapse: true, gzipped: true, positionsRoundedToNumberOfDigits: 3);


            //TestImport();


            //DumpPointSetKeys();
            // polygon topology test
            //new PointClusteringTest().TestPointClustering();

            //var data = File.ReadAllBytes("C:\\temp\\test.mesh");
            //PolyMesh mesh = data.Decode<PolyMesh>();


            //// fix broken edges...
            ////mesh.VertexClusteredCopy(Aardvark.Geometry.Clustering.PointClustering(mesh.PositionArray));
            //mesh.WithoutDegeneratedEdges();
            ////mesh.WithoutDegeneratedFaces();
            //mesh.BuildTopology();

            //Report.Line("yeah");

            //DumpPointSetKeys();

            //EnumerateCellsTest();

            //ExportExamples();

            //CopyTest();

            //var path = JObject.Parse(File.ReadAllText(@"T:\OEBB\20190619_Trajektorie_Verbindungsbahn\path.json"));
            //File.WriteAllText(@"T:\OEBB\20190619_Trajektorie_Verbindungsbahn\path2.json", path.ToString(Formatting.Indented));
            //Console.WriteLine(path);
            //PerfTestJuly2019();

            //TestLoadOldStore();

            //new ViewsFilterTests().CanDeletePoints();

            //new DeleteTests().DeleteDelete();
            //Console.WriteLine("done");

            //new ImportTests().CanImportChunkWithoutColor();

            //var store = PointCloud.OpenStore(@"G:\cells\3280_5503_0_10\pointcloud");
            //var pc = store.GetPointSet("3280_5503_0_10", default);
            //Console.WriteLine(pc.Id);
            //Console.WriteLine(pc.PointCount);

            //TestKNearest();
            //foreach (var filename in Directory.EnumerateFiles(@"C:\", "*.pts", SearchOption.AllDirectories))
            //{
            //    TestImportPts(filename);
            //}
        }
    }
}
