using Aardvark.Base;
using Aardvark.Data.Points;
using Aardvark.Data.Points.Import;
using Aardvark.Geometry.Points;
using System;
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
            var filename = @"test.e57";
            var fileSizeInBytes = new FileInfo(filename).Length;

            var config = ImportConfig.Default
                .WithInMemoryStore()
                .WithRandomKey()
                .WithVerbose(true)
                .WithMaxDegreeOfParallelism(1)
                .WithMinDist(0.005)
                ;
            var info = E57.E57Info(filename, config);

            foreach (var data3d in info.Metadata.E57Root.Data3D)
            {
                Console.WriteLine($"[{data3d.Name}]");
                Console.WriteLine($"    {data3d.Pose.RigidBodyTransform.Forward.TransformPos(V3d.Zero)}");
                Console.WriteLine($"    {data3d.Pose.Rotation.TransformPos(V3d.Zero) + data3d.Pose.Translation}");
            }

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

            using (var w = File.CreateText("test.txt"))
            {
                foreach (var chunk in chunks)
                {
                    for (var i = 0; i < chunk.Count; i++)
                    {
                        var p = chunk.Positions[i];
                        var c = chunk.Colors[i];
                        w.WriteLine($"{p.X} {p.Y} {p.Z} {c.R} {c.G} {c.B}");
                    }
                }
            }
            return;

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

        public static void Main(string[] args)
        {
            foreach (var filename in Directory.EnumerateFiles(@"C:\", "*.pts", SearchOption.AllDirectories))
            {
                TestImportPts(filename);
            }
        }
    }
}
