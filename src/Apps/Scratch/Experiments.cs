using Aardvark.Base;
using Aardvark.Data.Points;
using Aardvark.Geometry.Points;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Uncodium.SimpleStore;
using static System.Console;

namespace Scratch;

public record Cloud(string BaseDir, string Key)
{
    private SimpleDiskStore? _store = null;
    private SimpleDiskStore Store
    {
        get
        {
            if (_store == null)
            {
                Directory.CreateDirectory(StorePath);
                _store = new SimpleDiskStore(Path.Combine(StorePath, "data.uds"));
            }

            return _store;
        }
    }

    public string StorePath => Path.Combine(BaseDir, Key);

    public void Save(IEnumerable<Chunk> chunks)
    {
        var store = Store.ToPointCloudStore();

        var cfg = ImportConfig.Default
                .WithStorage(store)
                .WithKey(Key)
                .WithVerbose(true)
                .WithMaxDegreeOfParallelism(0)
                //.WithMinDist(0.001)
                //.WithNormalizePointDensityGlobal(true)
                //.WithProgressCallback(p => { Report.Line($"{p:0.00}"); })
                ;

        Report.BeginTimed($"saving pointcloud {StorePath} ...");
        var pcl = PointCloud.Import(chunks, cfg);
        File.WriteAllText(Path.Combine(StorePath, "key.txt"), pcl.Id);
        Report.EndTimed();
    }

    public IEnumerable<Chunk> Chunks()
    {
        var store = Store.ToPointCloudStore();
        var pc = store.GetPointSet(Key).Root.Value;
        return pc.QueryAllPoints();
    }
}

internal static class Experiments
{
    public static void Run()
    {
        var key = "test5";
        var cloud = new Cloud(@"E:\e57tests\stores", key);

        //var storepath = @"E:\e57tests\stores\test5\data.uds";
        //var key = File.ReadAllText(Path.Combine(Path.GetDirectoryName(storepath), "key.txt"));

        //using var storeRaw = new SimpleDiskStore(Path.Combine(storepath));
        //var store = storeRaw.ToPointCloudStore();
        //var pc = store.GetPointSet(key).Root.Value;

        //WriteLine($"cound chunks: {chunks.Count()}");

        //{
        //    var countFlat = 0;
        //    var countSteep = 0;
        //    var count = 0;

        //    foreach (var chunk in chunks)
        //    {
        //        var i = chunk.Normals.Count(n => n.Dot(V3f.ZAxis) > threshold);
        //        countFlat += i;
        //        countSteep += chunk.Count - i;
        //        count += chunk.Count;

        //        if (Random.Shared.Next(1000) < 5) WriteLine($"count: {count,12:N0}     countFlat : {countFlat,12:N0}    countSteep: {countSteep,12:N0}");

        //        var filteredNode = chunk.ImmutableFilterByNormal(n => n.Dot(V3f.ZAxis) < threshold);
        //    }
        //    WriteLine($"count: {count,12:N0}     countFlat : {countFlat,12:N0}    countSteep: {countSteep,12:N0}");
        //}
        
        var threshold = Math.Cos(Conversion.RadiansFromDegrees(60.0));
        var chunks = cloud.Chunks(); // pc.QueryAllPoints();
        var chunksFiltered = chunks.Select(chunk => chunk.ImmutableFilterByNormal(n => n.Dot(V3f.ZAxis) > threshold)).ToArray();
        WriteLine($"filtered: {chunksFiltered.Select(x => x.Count).Sum():N0} points left");
        var bigChunk = Chunk.ImmutableMerge(chunksFiltered);
        WriteLine($"created big chunk");

        var cloud2 = new Cloud(@"E:\e57tests\stores", "experiment2");

        cloud2.Save(bigChunk.Split(16 * 1024 * 1024));

        WriteLine("done");
    }

    public static void MakeRaster(IEnumerable<Chunk> chunks, Box3d bbox, double resolution, string imageName)
    {
        var size = (V2i)bbox.Size;
        var w = size.X + 1;
        var h = size.Y + 1;

        var raster = new int[w * h];
        var i = 0;
        var ps = chunks.SelectMany(chunk => chunk.Positions);
        foreach (var p0 in ps)
        {
            var p = (V2i)(p0 / resolution);
            raster[p.Y * w + p.X]++;
            if (++i % 1000000 == 0) WriteLine($"{i,10:N0}");
            //var c = img[p.X, p.Y];
            //img[p.X, p.Y] = new Rgb24((byte)Math.Min(255, c.R + 1), (byte)Math.Min(255, c.G + 1), (byte)Math.Min(255, c.B + 1));
        }

        var max = raster.Max();
        var scale = 255f / max;

        var img = new Image<Rgb24>(w, h, new Rgb24(0, 0, 0));
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var c = (byte)(raster[y * w + x] * scale);
                img[x, y] = new Rgb24(c, c, c);
            }
        }

        img.SaveAsPng($"{imageName}.png");

        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var c = img[x, y].R;
                if (c > 64) { img[x, y] = new Rgb24(255, 0, 0); continue; }
                if (c > 32) { img[x, y] = new Rgb24(0, 255, 0); continue; }
                if (c > 16) { img[x, y] = new Rgb24(0, 0, 255); continue; }
                //if (c > 191) { img[x, y] = new Rgb24(c, 0, 0); continue; }
                //if (c > 159) { img[x, y] = new Rgb24(0, c, 0); continue; }
                //if (c > 127) { img[x, y] = new Rgb24(0, 0, c); continue; }
                //if (c > 95) { img[x, y] = new Rgb24(c, c, 0); continue; }
                //if (c > 63) { img[x, y] = new Rgb24(c, 0, c); continue; }
                //if (c > 31) { img[x, y] = new Rgb24(0, c, c); continue; }
                //img[x, y] = new Rgb24(0, 0, 0);
            }
        }

        img.SaveAsPng($"{imageName}.2.png");
    }
}
