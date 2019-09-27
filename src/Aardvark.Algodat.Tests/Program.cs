using Aardvark.Base;
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
using Uncodium.SimpleStore;

namespace Aardvark.Geometry.Tests
{
    public class Program
    {
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
            var filename = @"T:\Vgm\Data\E57\Innenscan_FARO.e57";
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
            var filename = @"test.e57";

            var store = new SimpleDiskStore(@"./store").ToPointCloudStore(new LruDictionary<string, object>(1024 * 1024 * 1024));

            var config = ImportConfig.Default
                .WithStorage(store)
                .WithKey("mykey")
                .WithVerbose(true)
                ;

            Report.BeginTimed("importing");
            var pointcloud = PointCloud.Import(filename, config);
            Report.EndTimed();
            store.Flush();
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
            var store2Path = @"T:\Vgm\Stores\copytest2";
            var store3Path = @"T:\Vgm\Stores\copytest3";
            var store4Path = @"T:\Vgm\Stores\JBs_Haus.pts";

            // create stores
            var store1 = new SimpleDiskStore(store1Path).ToPointCloudStore();
            var store2 = new SimpleDiskStore(store2Path).ToPointCloudStore();

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
            if (!Directory.Exists(store3Path)) { Directory.CreateDirectory(store3Path); Thread.Sleep(1000); }
            if (!Directory.Exists(store4Path)) { Directory.CreateDirectory(store4Path); Thread.Sleep(1000); }

            // copy point cloud from store1 to store2
            var pc1 = store1.GetPointSet(rootKey);
            var root1 = pc1.Root.Value;
            var totalNodes = root1.CountNodes(outOfCore: true);
            store2.Add(rootKey, pc1);
            Report.BeginTimed("copy");
            var totalBytes = 0L;
            //pc1.Root.Value.ForEachNode(outOfCore: true, n => Report.Line($"{n.Id} {n.PointCountCell,12:N0}"));
            Convert(root1.Id);
            Report.Line($"{totalNodes}");
            Report.EndTimed();
            store2.Flush();

            // meta
            var rootJson = JObject.FromObject(new
            {
                rootId = root1.Id.ToString(),
                splitLimit = splitLimit,
                minDist = minDist,
                pointCount = root1.PointCountTree,
                bounds = root1.BoundingBoxExactGlobal,
                centroid = (V3d)root1.CentroidLocal + root1.Center,
                centroidStdDev = root1.CentroidLocalStdDev,
                cell = root1.Cell,
                totalNodes = totalNodes,
                totalBytes = totalBytes,
                gzipped = false
            });
            File.WriteAllText(Path.Combine(store3Path, "root.json"), rootJson.ToString(Formatting.Indented));

            rootJson["gzipped"] = true;
            File.WriteAllText(Path.Combine(store4Path, "root.json"), rootJson.ToString(Formatting.Indented));

            void Convert(Guid key)
            {
                if (key == Guid.Empty) return;

                var (def, raw) = store1.GetDurable(key);
                var node = raw as IDictionary<Durable.Def, object>;
                node.TryGetValue(Durable.Octree.SubnodesGuids, out var subnodeGuids);

                // write inlined node to store
                var inlinedNode = store1.ConvertToInline(node);
                var inlinedBlob = store2.Add(key, Durable.Octree.Node, inlinedNode, false);
                totalBytes += inlinedBlob.Length;
                //Report.Line($"stored node {key}");

                // write blob as file
                File.WriteAllBytes(Path.Combine(store3Path, key.ToString()), inlinedBlob);
                
                // write blob as file (gzipped)
                using (var fs = File.OpenWrite(Path.Combine(store4Path, key.ToString())))
                using (var zs = new GZipStream(fs, CompressionLevel.Fastest))
                {
                    zs.Write(inlinedBlob, 0, inlinedBlob.Length);
                }

                // children
                if (subnodeGuids != null)
                    foreach (var x in (Guid[])subnodeGuids) Convert(x);
            }
        }

        public static void ExportExamples()
        {
            // Example 1: export point cloud to folder
            {
                using (var storeSource = new SimpleDiskStore(@"T:\Vgm\Stores\copytest1").ToPointCloudStore())
                using (var storeTarget = new SimpleFolderStore(@"T:\Vgm\Stores\exportFolder").ToPointCloudStore())
                {
                    storeSource.ExportPointSet("097358dc-d89a-434c-8a4e-fe03c063d886", storeTarget, true);
                }
            }

            // Example 2: export point cloud to another store
            {
                using (var storeSource = new SimpleDiskStore(@"T:\Vgm\Stores\copytest1").ToPointCloudStore())
                using (var storeTarget = new SimpleDiskStore(@"T:\Vgm\Stores\exportStore").ToPointCloudStore())
                {
                    storeSource.ExportPointSet("097358dc-d89a-434c-8a4e-fe03c063d886", storeTarget, true);
                    storeTarget.Flush();
                }
            }

            // Example 3: inline point cloud nodes
            {
                using (var storeSource = new SimpleDiskStore(@"T:\Vgm\Stores\copytest1").ToPointCloudStore())
                using (var storeTarget = new SimpleFolderStore(@"T:\Vgm\Stores\exportInlined").ToPointCloudStore())
                {
                    storeSource.InlinePointSet("097358dc-d89a-434c-8a4e-fe03c063d886", storeTarget, false);
                    storeTarget.Flush();
                }
            }
        }

        public static void EnumerateCellsTest()
        {
            var store = new SimpleDiskStore(@"T:\Vgm\Pinter_Dachboden_3Dworx_Store\Pinter_Dachboden_store").ToPointCloudStore(cache: default);
            var pc = store.GetPointSet("1bd9ab70-0245-4bf2-bbad-8929ae94105e");
            var foo = store.GetPointCloudNode("1bd9ab70-0245-4bf2-bbad-8929ae94105e");
            var root = pc.Root.Value;

            foreach (var x in root.QueryCells(0))
            {
                Console.WriteLine($"{x.cell,20} {x.chunk.Count,8:N0}    {x.chunk.BoundingBox:0.00}");
            }
        }

        internal static void DumpPointSetKeys()
        {
            var storeFolder = @"T:\Vgm\Pinter_Dachboden_3Dworx_Store\Pinter_Dachboden_store";

            var sds = new SimpleDiskStore(storeFolder);
            var keys = sds.SnapshotKeys();
            var store = sds.ToPointCloudStore(cache: default);

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

        public static void Main(string[] args)
        {
            DumpPointSetKeys();

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

            //TestE57();

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
