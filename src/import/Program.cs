using Aardvark.Base;
using Aardvark.Data.Points;
using Aardvark.Geometry.Points;
using System.Globalization;
using Uncodium.SimpleStore;

var filename = args[0];
var outdir = args[1];
var minDist = double.Parse(args[2], CultureInfo.InvariantCulture);

await Import.CreateStore(filename, outdir, minDist);

static class Import
{
    public static Task CreateStore(string filename, string storeDir, double minDist)
                => CreateStore(filename, new DirectoryInfo(storeDir), minDist);

    public static Task CreateStore(string filename, DirectoryInfo storeDir, double minDist)
    {
        if (!storeDir.Exists) storeDir.Create();

        var key = Path.GetFileName(filename);

        using var store = new SimpleDiskStore(Path.Combine(storeDir.FullName, "data.uds")).ToPointCloudStore();
        var config = ImportConfig.Default
            .WithStorage(store)
            .WithKey(key)
            .WithVerbose(true)
            .WithMaxDegreeOfParallelism(0)
            .WithMinDist(minDist)
            .WithNormalizePointDensityGlobal(true)
            //.WithMaxChunkPointCount(32 * 1024 * 1024)
            //.WithOctreeSplitLimit(8192*4)
            ;

        Report.BeginTimed($"importing {filename}");
        var pcl = PointCloud.Import(filename, config);
        Report.EndTimed();

        File.WriteAllText(Path.Combine(storeDir.FullName, "key.txt"), key);

        return Task.CompletedTask;
    }
}