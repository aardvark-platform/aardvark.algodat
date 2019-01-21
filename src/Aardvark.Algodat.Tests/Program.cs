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
using Uncodium.SimpleStore;

namespace Aardvark.Geometry.Tests
{
    public unsafe class Program
    {
        internal static void TestE57()
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            var filename = @"T:\Vgm\Data\E57\CloudCompare_Technologiezentrum_Teil1.e57";
            var fileSizeInBytes = new FileInfo(filename).Length;

            var sw = new Stopwatch(); sw.Start();

            var lastProgress = 0.0;
            var config = ImportConfig.Default
                .WithInMemoryStore()
                .WithRandomKey()
                .WithVerbose(false)
                .WithMaxDegreeOfParallelism(0)
                .WithMinDist(0.0)
                .WithEstimateNormals(ps =>
                {
                    //Console.WriteLine($"[NORMALS]");
                    return Normals.EstimateNormals((V3d[])ps, 16);
                })
                .WithProgressCallback(x =>
                {
                    if (x < lastProgress) Console.WriteLine("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAH"); else lastProgress = x;
                    Console.WriteLine($"[PROGRESS]; {x,8:0.00000}; {sw.Elapsed.TotalSeconds:0.00}");
                })
                ;

            var chunks = E57.Chunks(filename, config);
            var pointcloud = PointCloud.Chunks(chunks, config);
            Console.WriteLine($"pointcloud.PointCount  : {pointcloud.PointCount}");
            Console.WriteLine($"pointcloud.Bounds      : {pointcloud.Bounds}");
            Console.WriteLine($"pointcloud.BoundingBox : {pointcloud.BoundingBox}");

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

            var store = new SimpleDiskStore(@"./store").ToPointCloudStore();

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
                    .WithStorage(PointCloud.CreateInMemoryStore())
                    .WithKey("test")
                    .WithOctreeSplitLimit(splitLimit)
                    ;
                return PointCloud.Chunks(new Chunk(ps, null), config);
            }
        }

        public static void Main(string[] args)
        {
            TestE57();

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
