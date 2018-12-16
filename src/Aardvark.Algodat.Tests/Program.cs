using Aardvark.Base;
using Aardvark.Data.Points;
using Aardvark.Data.Points.Import;
using Aardvark.Geometry.Points;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Uncodium.SimpleStore;

namespace Aardvark.Geometry.Tests
{
    public unsafe class Program
    {
        internal static void LinkedStores()
        {
            var resolver = new IdentityResolver();

            var links = Directory
                .EnumerateDirectories(@"G:\cells", "pointcloud", SearchOption.AllDirectories)
                .Select(x => (storePath: x, key: Path.GetFileName(Path.GetDirectoryName(x))))
                .ToArray();

            var sw = new Stopwatch(); sw.Restart();
            var totalCount = 0L;
            var ls = links
                .Select(x =>
                {
                    try
                    {
                        var store = new LinkedNode(resolver, x.storePath, x.key, cache: default);

                        var _sw = new Stopwatch(); _sw.Restart();
                        var foo = store.ForEachNode().Sum(n => n.GetPositions()?.Value.Length);
                        _sw.Stop(); Console.WriteLine(_sw.Elapsed);
                        _sw.Restart();
                        var bar = store.ForEachNode().Sum(n => n.GetPositions()?.Value.Length);
                        _sw.Stop(); Console.WriteLine(_sw.Elapsed);
                        if (foo != bar) Report.Error("foo != bar");

                        Console.WriteLine($"{store.PointCountTree,20:N0}");
                        totalCount += store.PointCountTree;
                        return store;
                    }
                    catch
                    {
                        Console.WriteLine($"[ERROR] could not read {x.key}@{x.storePath}");
                        return null;
                    }
                })
                .Where(x => x != null)
                .ToArray();

            sw.Stop();
            Console.WriteLine($"{totalCount,20:N0} total");
            Console.WriteLine(sw.Elapsed);

            //var a = new LinkedStore(@"Y:\cells\3274_5507_0_10\pointcloud", "3274_5507_0_10");
            //Console.WriteLine($"{a.PointCountTree:N0}");
            Environment.Exit(0);
        }

        internal static void TestE57()
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            var filename = @"test.e57";
            var fileSizeInBytes = new FileInfo(filename).Length;

            var config = ImportConfig.Default
                .WithInMemoryStore()
                .WithRandomKey()
                .WithVerbose(true)
                .WithMaxDegreeOfParallelism(0)
                .WithMinDist(0.005)
                ;

            var chunks = E57.Chunks(filename, config).ToList();
            var pointcloud = PointCloud.Chunks(chunks, config);
            Console.WriteLine($"pointcloud.PointCount  : {pointcloud.PointCount}");
            Console.WriteLine($"pointcloud.Bounds      :{pointcloud.Bounds}");
            Console.WriteLine($"pointcloud.BoundingBox :{pointcloud.BoundingBox}");

            var leafLodPointCount = 0L;
            pointcloud.Root.Value.ForEachNode(true, n => { if (n.IsLeaf) leafLodPointCount += n.LodPositionsAbsolute.Length; });
            Console.WriteLine($"leaf lod point count :{leafLodPointCount}");

            //foreach (var chunk in chunks)
            //{
            //    for (var i = 0; i < chunk.Count; i++)
            //    {
            //        Console.WriteLine($"{chunk.Positions[i]:0.000} {chunk.Colors?[i]}");
            //    }
            //}

            Console.WriteLine($"chunks point count: {chunks.Sum(x => x.Positions.Count)}");
            Console.WriteLine($"chunks bounds     : {new Box3d(chunks.SelectMany(x => x.Positions))}");

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

            var store = new SimpleDiskStore(@"./store").ToPointCloudStore(cache: default);

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
            var chunks = Pts.Chunks(filename, ImportConfig.Default);

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
                    .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                    .WithKey("test")
                    .WithOctreeSplitLimit(splitLimit)
                    ;
                return PointCloud.Chunks(new Chunk(ps, null), config);
            }
        }

        //internal static void KeepAliveCacheTest()
        //{
        //    var cache0 = new KeepAliveCache("foo cache", 1024 * 1024, false);
        //    var run0 = true;
        //    new Thread(() => { while (run0) { cache0.Add("foo", 100); Thread.Sleep(10); } }).Start();

        //    Console.ReadLine();
        //    var store0 = PointCloud.OpenStore("teststore0");
        //    var runstore0 = true;
        //    new Thread(() => { while (runstore0) { store0.Add("foo", new byte[10]); store0.GetByteArray("foo"); Thread.Sleep(1000); } }).Start();


        //    Console.ReadLine();
        //    var store1 = PointCloud.OpenStore("teststore1");
        //    var runstore1 = true;
        //    new Thread(() => { while (runstore1) { store1.Add("foo", new byte[10]); store1.GetByteArray("foo"); Thread.Sleep(1000); } }).Start();


        //    Console.ReadLine();
        //    runstore1 = false; Thread.Sleep(10);
        //    store1.Dispose();

        //    Console.ReadLine();
        //    run0 = false; Thread.Sleep(10);
        //    cache0.Dispose();

        //    Console.ReadLine();
        //    runstore0 = false; Thread.Sleep(10);
        //    store0.Dispose();

        //    Console.ReadLine();
        //    var store2 = PointCloud.OpenStore("teststore2");
        //    var run2 = true;
        //    new Thread(() => { while (run2) { store2.Add("foo", new byte[10]); store2.GetByteArray("foo"); Thread.Sleep(100); } }).Start();

        //    Console.ReadLine();
        //    run2 = false; Thread.Sleep(10);
        //    store2.Dispose();
        //}

        public static void Main(string[] args)
        {
            new LruDictionaryTests().RandomInserts_1M_MultiThreaded();
            //KeepAliveCacheTest();

            //LinkedStores();

            //MasterLisa.Perform();
            //TestE57();

            //using (var store = PointCloud.OpenStore(@"G:\cells\3267_5514_0_10\pointcloud"))
            //{
            //    var pc = store.GetPointSet("3267_5514_0_10", default);
            //    Console.WriteLine(pc.Id);
            //    Console.WriteLine(pc.PointCount);

            //    var root = pc.Root.Value;
            //    var kd = root.LodKdTree.Value;
            //}

            //TestKNearest();
            //foreach (var filename in Directory.EnumerateFiles(@"C:\", "*.pts", SearchOption.AllDirectories))
            //{
            //    TestImportPts(filename);
            //}
        }
    }
}
