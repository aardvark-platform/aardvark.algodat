using Aardvark.Base;
using Aardvark.Data.Points;
using Aardvark.Data.Points.Import;
using Aardvark.Geometry.Points;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Uncodium.SimpleStore;

namespace ImportTest
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("usage: ImportTest <basedir>");
                Console.WriteLine("         <basedir> contains point cloud files or folders with such files");
                return;
            }

            var basedir = args[0];

            var _1 = E57.E57Format;
            var _2 = Pts.PtsFormat;

            bool hasValidExtension(string s)
            {
                var e = Path.GetExtension(s).ToLower();
                return e == ".pts" || e == ".e57";
            }

            (string dir, string[] files) GetPointCloudsInDirectory(string dir)
                => (dir, Directory.GetFiles(dir).Where(hasValidExtension).ToArray());

            var stats = new List<object>();
            var tsStart = DateTimeOffset.UtcNow;

            var files = GetPointCloudsInDirectory(basedir);
            foreach (var x in files.files) stats.Add(ImportFile(x, x));

            var dirs = Directory.GetDirectories(basedir)
                .Where(x => !x.EndsWith("_STORE"))
                .Select(GetPointCloudsInDirectory)
                .Where(x => x.files.Length > 0)
                .ToArray();
            foreach (var x in dirs) stats.Add(ImportFile(x.dir, x.files));


            var tsEnd = DateTimeOffset.UtcNow;
            var json = JObject.FromObject(new
            {
                tsStart,
                tsEnd,
                durationInSeconds = Math.Round((tsEnd-tsStart).TotalSeconds, 2),
                stats
            });
            var n = DateTimeOffset.Now;
            var statsFileName = Path.Combine(basedir, $"stats_{n.Year:0000}{n.Month:00}{n.Day:00}_{n.Hour:00}{n.Minute:00}{n.Second:00}.json");
            File.WriteAllText(statsFileName, json.ToString(Formatting.Indented));
        }

        static object ImportFile(string path, params string[] filenames)
        {
            var tsStart = DateTimeOffset.UtcNow;

            try
            {
                var storePath = path + "_STORE";

                if (Directory.Exists(storePath))
                {
                    Directory.Delete(storePath, true);
                    Thread.Sleep(1000);
                }

                var store = new SimpleDiskStore(storePath).ToPointCloudStore();

                Report.BeginTimed($"importing");
                var sw = new Stopwatch(); sw.Start();
                var keys = new List<string>();
                var pointCount = 0L;
                foreach (var filename in filenames)
                {
                    var key = Path.GetFileName(filename);
                    keys.Add(key);
                    var config = ImportConfig.Default
                       .WithStorage(store)
                       .WithKey(key)
                       .WithVerbose(false)
                       .WithMaxDegreeOfParallelism(0)
                       .WithMinDist(0.005)
                       .WithNormalizePointDensityGlobal(true)
                       ;
                    var pc = PointCloud.Import(filename, config);
                    pointCount += pc.PointCount;
                }
                store.Flush();
                sw.Stop();
                Report.EndTimed();
                var tsEnd = DateTimeOffset.UtcNow;

                var storeDataSizeInBytes = new FileInfo(Path.Combine(storePath, "data.bin")).Length;
                var storeIndexSizeInBytes = new FileInfo(Path.Combine(storePath, "index.bin")).Length;

                var stats = new
                {
                    tsStart,
                    tsEnd,
                    path,
                    filenames,
                    keys,
                    pointCount,
                    durationInSeconds = Math.Round(sw.Elapsed.TotalSeconds, 2),
                    storeDataSizeInBytes,
                    storeDataSizeInGb = Math.Round(storeDataSizeInBytes / (1024.0 * 1024 * 1024), 3),
                    storeIndexSizeInBytes,
                    storeIndexSizeInMb = Math.Round(storeIndexSizeInBytes / (1024.0 * 1024), 3),
                };

                var json = JsonConvert.SerializeObject(stats, Formatting.Indented);
                Console.WriteLine(json);
                var n = DateTimeOffset.Now;

                var statsFileName = path + $"_{n.Year:0000}{n.Month:00}{n.Day:00}_{n.Hour:00}{n.Minute:00}{n.Second:00}_stats.json";
                File.WriteAllText(statsFileName, json);

                return stats;
            }
            catch (Exception e)
            {
                var tsEnd = DateTimeOffset.UtcNow;
                var stats = new
                {
                    tsStart,
                    tsEnd,
                    path,
                    filenames,
                    error = e.ToString()
                };
                return stats;
            }
        }
    }
}
