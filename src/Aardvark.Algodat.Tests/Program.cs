using Aardvark.Base;
using Aardvark.Data;
using Aardvark.Data.Points;
using Aardvark.Data.Points.Import;
using Aardvark.Geometry.Points;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Uncodium.SimpleStore;

namespace Aardvark.Geometry.Tests
{
    public class Program
    {
        internal static void TestE57()
        {
            var sw = new Stopwatch();

            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            var filename = @"T:\Vgm\Data\E57\Lichthof grob.e57";
            var fileSizeInBytes = new FileInfo(filename).Length;

            var info = E57.E57Info(filename, ParseConfig.Default);
            Report.Line($"total bounds: {info.Bounds}");
            Report.Line($"total count : {info.PointCount:N0}");

            var config = ImportConfig.Default
                .WithInMemoryStore()
                .WithRandomKey()
                //.WithVerbose(true)
                .WithMaxDegreeOfParallelism(0)
                .WithMinDist(0.01)
                ;
            
            Report.BeginTimed("total");

            var chunks = E57
                .Chunks(filename, config.ParseConfig)
                //.Take(2)
                //.AsParallel()
                //.Select(x => x.ImmutableFilterMinDistByCell(new Cell(x.BoundingBox), config))
                ////.Select(x => x.ImmutableFilterSequentialMinDistL1(0.01))
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

            chunks = E57.Chunks(filename, config.ParseConfig);
            var pointcloud = PointCloud.Chunks(chunks, config);
            Console.WriteLine($"pointcloud.PointCount  : {pointcloud.PointCount}");
            Console.WriteLine($"pointcloud.Bounds      : {pointcloud.Bounds}");
            Console.WriteLine($"pointcloud.BoundingBox : {pointcloud.BoundingBox}");

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

        public static void Main(string[] args)
        {
            new DeleteTests().DeleteDelete();
            Console.WriteLine("done");
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
