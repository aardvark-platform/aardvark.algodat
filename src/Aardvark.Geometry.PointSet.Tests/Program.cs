using Aardvark.Base;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Uncodium.SimpleStore;
using Aardvark.Geometry.Points;
using Aardvark.Data.E57;

namespace Aardvark.Geometry.Tests
{
    public unsafe class Program
    {
        internal static void TestE57()
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            var filename = @"test.e57";
            var fileSizeInBytes = new FileInfo(filename).Length;

            var config = ImportConfig.Default.WithInMemoryStore().WithRandomKey().WithVerbose(true);
            var chunks = PointCloud.E57(filename, config).ToList();
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
            var filename = @"T:\Vgm\Data\Schottenring_2018_02_23\Laserscans\2018-02-27_BankAustria\export\sitzungssaal\Punktwolke\P40\P40_gesamt.e57";

            var store = new SimpleDiskStore(@"T:\Vgm\Stores\Sitzungssaal").ToPointCloudStore();

            var config = ImportConfig.Default
                .WithStorage(store)
                .WithKey("sitzungssaal")
                .WithVerbose(true)
                ;

            Report.BeginTimed("importing");
            var pointcloud = PointCloud.Import(filename, config);
            Report.EndTimed();
            store.Flush();
        }

        public static void Main(string[] args)
        {
            TestImport();
        }
    }
}
