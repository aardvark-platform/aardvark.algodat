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
            var filename = @"T:\Vgm\Data\E57\Register360_Berlin Office_1.e57";
            var fileSizeInBytes = new FileInfo(filename).Length;

            var config = ImportConfig.Default.WithInMemoryStore().WithRandomKey();
            var chunks = PointCloud.E57(filename, config);
            var pointcloud = PointCloud.Chunks(chunks, config);
            Console.WriteLine(pointcloud.PointCount);
            Console.WriteLine(pointcloud.Bounds);
            //foreach (var chunk in chunks)
            //{
            //    for (var i = 0; i < chunk.Count; i++)
            //    {
            //        Console.WriteLine($"{chunk.Positions[i]:0.000} {chunk.Colors?[i]}");
            //    }
            //}
            var count = chunks.Sum(x => x.Positions.Count);
            Console.WriteLine(count);
            var bb = new Box3d(chunks.SelectMany(x => x.Positions));
            Console.WriteLine(bb);
            return;

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
        }

        public static void Main(string[] args)
        {
            TestE57();
        }
    }
}
