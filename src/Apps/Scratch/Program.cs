using Aardvark.Base;
using Aardvark.Data;
using Aardvark.Data.E57;
using Aardvark.Data.Points;
using Aardvark.Data.Points.Import;
using Aardvark.Geometry.Points;
using Microsoft.FSharp.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Uncodium.SimpleStore;
using static Aardvark.Data.E57.ASTM_E57;

namespace Scratch
{
    class Program
    {
        private static readonly Durable.Def[] DefsPositions = new[]
        {
            Durable.Octree.PositionsGlobal3d,
            Durable.Octree.PositionsGlobal3dReference,
            Durable.Octree.PositionsGlobal3f,
            Durable.Octree.PositionsGlobal3fReference,
            Durable.Octree.PositionsLocal3b,
            Durable.Octree.PositionsLocal3bReference,
            Durable.Octree.PositionsLocal3d,
            Durable.Octree.PositionsLocal3dReference,
            Durable.Octree.PositionsLocal3f,
            Durable.Octree.PositionsLocal3fReference,
            Durable.Octree.PositionsLocal3ui,
            Durable.Octree.PositionsLocal3uiReference,
            Durable.Octree.PositionsLocal3ul,
            Durable.Octree.PositionsLocal3ulReference,
            Durable.Octree.PositionsLocal3us,
            Durable.Octree.PositionsLocal3usReference,
        };
        private static readonly Durable.Def[] DefsColors = new[]
        {
            Durable.Octree.Colors3b,
            Durable.Octree.Colors3bReference,
            Durable.Octree.Colors4b,
            Durable.Octree.Colors4bDeprecated20201117,
            Durable.Octree.Colors4bReference,
            Durable.Octree.ColorsRGB565,
            Durable.Octree.ColorsRGB565Reference,
        };
        private static readonly Durable.Def[] DefsIntensities = new[]
        {
            Durable.Octree.Intensities1f,
            Durable.Octree.Intensities1fReference,
            Durable.Octree.Intensities1i,
            Durable.Octree.Intensities1iReference,
            Durable.Octree.IntensitiesWithOffset1f,
            Durable.Octree.IntensitiesWithOffset1fOffset,
            Durable.Octree.IntensitiesWithOffset1fRange,
            Durable.Octree.IntensitiesWithOffset1fReference,
        };
        private static readonly Durable.Def[] DefsClassifications = new[]
        {
            Durable.Octree.Classifications1b,
            Durable.Octree.Classifications1bReference,
            Durable.Octree.Classifications1i,
            Durable.Octree.Classifications1iReference,
            Durable.Octree.Classifications1s,
            Durable.Octree.Classifications1sReference,
        };

        private static readonly Durable.Def[] DefsNormals = new[]
        {
            Durable.Octree.Normals3f,
            Durable.Octree.Normals3fReference,
            Durable.Octree.Normals3sb,
            Durable.Octree.Normals3sbReference,
            Durable.Octree.NormalsOct16,
            Durable.Octree.NormalsOct16Reference,
            Durable.Octree.NormalsOct16P,
            Durable.Octree.NormalsOct16PReference,
        };

        private static readonly HashSet<Durable.Def> DefsRefs = new()
        {
            Durable.Octree.Classifications1bReference,
            Durable.Octree.Classifications1iReference,
            Durable.Octree.Classifications1sReference,
            Durable.Octree.Colors3bReference,
            Durable.Octree.Colors4bReference,
            Durable.Octree.ColorsRGB565Reference,
            Durable.Octree.Densities1fReference,
            Durable.Octree.Intensities1fReference,
            Durable.Octree.Intensities1iReference,
            Durable.Octree.IntensitiesWithOffset1fReference,
            Durable.Octree.KdTreeIndexArrayReference,
            Durable.Octree.Normals3fReference,
            Durable.Octree.Normals3sbReference,
            Durable.Octree.NormalsOct16PReference,
            Durable.Octree.NormalsOct16Reference,
            Durable.Octree.PointRkdTreeDDataReference,
            Durable.Octree.PointRkdTreeFDataReference,
            Durable.Octree.PositionsGlobal3dReference,
            Durable.Octree.PositionsGlobal3fReference,
            Durable.Octree.PositionsLocal3bReference,
            Durable.Octree.PositionsLocal3dReference,
            Durable.Octree.PositionsLocal3fReference,
            Durable.Octree.PositionsLocal3uiReference,
            Durable.Octree.PositionsLocal3ulReference,
            Durable.Octree.PositionsLocal3usReference,
            Durable.Octree.Velocities3dReference,
            Durable.Octree.Velocities3fReference,
        };

        static void PointCloudStats(string storePath, bool detailedNodeScan)
        {
            Report.Line();
            Report.BeginTimed($"{storePath}");

            var key = File.ReadAllText(Path.Combine(storePath, "key.txt"));

            using var storeRaw = new SimpleDiskStore(storePath);
            var store = storeRaw.ToPointCloudStore();

            var ps = store.GetPointSet(key);
            var root = ps.Root.Value;

            Report.Line($"key : {key}");
            Report.Line($"root: {root.Id}");

            Report.Line($"point count");
            Report.Line($"    root node     : {root.PointCountCell,20:N0}");
            Report.Line($"    tree          : {root.PointCountTree,20:N0}");

            Report.Line($"storage");
            Report.Line($"    version       : SimpleDiskStore {storeRaw.Version}");
            Report.Line($"    used     bytes: {storeRaw.GetUsedBytes(),20:N0}");
            Report.Line($"    reserved bytes: {storeRaw.GetReservedBytes(),20:N0}");

            static void CheckDuplicateProperties(IPointCloudNode node, IEnumerable<Durable.Def> defs)
            {
                var exist = default(List<Durable.Def>);
                foreach (var def in defs)
                {
                    if (node.Properties.ContainsKey(def))
                    {
                        if (exist == null) exist = new List<Durable.Def>();
                        exist.Add(def);
                    }
                }
                if (exist?.Count > 1)
                {
                    Report.Begin($"duplicate properties in node {node.Id}");
                    foreach (var def in exist) Report.Line($"{def.Name}");
                    Report.End();
                }
            }

            //Report.Begin("root node properties");
            //var i = 0;
            //foreach (var k in root.Properties.Keys.OrderBy(x => x.Name)) Report.Line($"[{i++}] {k.Name}");
            //Report.End();

            if (detailedNodeScan)
            {
                Report.BeginTimed($"scanning nodes");
                var nodeCount = 0L;
                var leafCount = 0L;
                var nodeBlobsCount = 0L;
                var nodeBlobsTotalBytes = 0L;
                var refBlobsCount = 0L;
                var refBlobsTotalBytes = 0L;

                root.ForEachNode(outOfCore: true, node =>
                {
                    if (node.Properties.ContainsKey(Durable.Octree.Colors4b)) throw new Exception();
                    if (node.Properties.ContainsKey(Durable.Octree.Intensities1i)) throw new Exception();
                    if (node.Properties.ContainsKey(Durable.Octree.Normals3f)) throw new Exception();
                    if (node.Properties.ContainsKey(Durable.Octree.PositionsLocal3f)) throw new Exception();

                    nodeCount++;
                    if (node.IsLeaf) leafCount++;

                    CheckDuplicateProperties(node, DefsClassifications);
                    CheckDuplicateProperties(node, DefsColors);
                    CheckDuplicateProperties(node, DefsIntensities);
                    CheckDuplicateProperties(node, DefsNormals);
                    CheckDuplicateProperties(node, DefsPositions);

                    nodeBlobsCount++;
                    nodeBlobsTotalBytes += storeRaw.Get(node.Id.ToString()).Length;

                    var foo = new HashSet<Durable.Def>(node.Properties.Keys.Where(k => k.Name.EndsWith("Reference")));
                    foreach (var k in foo)
                    {
                        if (!DefsRefs.Contains(k)) throw new Exception($"DefsRefs does not contain {k}.");
                        var o = node.Properties[k];
                        var key = o switch
                        {
                            Guid x => x.ToString(),
                            string x => x,
                            _ => throw new Exception($"Unknown reference of type {o?.GetType()}")
                        };

                        refBlobsCount++;
                        refBlobsTotalBytes += storeRaw.Get(key).Length;
                    }
                });
                Report.EndTimed();

                Report.Line();
                Report.Line($"node blobs");
                Report.Line($"    count       : {nodeBlobsCount,20:N0}");
                Report.Line($"    total bytes : {nodeBlobsTotalBytes,20:N0}");
                Report.Line($"ref blobs");
                Report.Line($"    count       : {refBlobsCount,20:N0}");
                Report.Line($"    total bytes : {refBlobsTotalBytes,20:N0}");
                Report.Line($"TOTAL");
                Report.Line($"    count       : {nodeBlobsCount + refBlobsCount,20:N0}");
                Report.Line($"    total bytes : {nodeBlobsTotalBytes + refBlobsTotalBytes,20:N0}");
                Report.Line();
                Report.Line($"node counts");
                Report.Line($"    total       : {nodeCount,20:N0}");
                Report.Line($"    leafs       : {leafCount,20:N0}");
            }

            Report.EndTimed();
        }

        static void GeneratePointCloudStats()
        {
            var storePaths = new[]
            {
                //@"E:\rmdata\aibotix_ground_points.e57_5.0.24",
                //@"E:\rmdata\aibotix_ground_points.e57_5.1.0-prerelease0004",
                @"E:\rmdata\aibotix_ground_points.e57_5.1.0-prerelease0005",
                //@"E:\rmdata\Register360_Berlin Office_1.e57_5.0.24",
                //@"E:\rmdata\Register360_Berlin Office_1.e57_5.1.0-prerelease0004",
                @"E:\rmdata\Register360_Berlin Office_1.e57_5.1.0-prerelease0005",
                //@"E:\rmdata\Staatsoper.e57_5.0.24",
                //@"E:\rmdata\Staatsoper.e57_5.1.0-prerelease0004",
                @"E:\rmdata\Staatsoper.e57_5.1.0-prerelease0005",
                //@"E:\rmdata\Innenscan_FARO.e57_5.0.24",
                //@"E:\rmdata\Innenscan_FARO.e57_5.1.0-prerelease0004",
                @"E:\rmdata\Innenscan_FARO.e57_5.1.0-prerelease0005",
                //@"E:\rmdata\1190_31_test_Frizzo.e57_5.0.24",
                //@"E:\rmdata\1190_31_test_Frizzo.e57_5.1.0-prerelease0004",
                @"E:\rmdata\1190_31_test_Frizzo.e57_5.1.0-prerelease0005",
                //@"E:\rmdata\Neuhäusl-Hörschwang.e57_5.0.24",
                //@"E:\rmdata\Neuhäusl-Hörschwang.e57_5.1.0-prerelease0004",
                @"E:\rmdata\Neuhäusl-Hörschwang.e57_5.1.0-prerelease0005",

                // 2020-11-13-Walenta
                //@"E:\rmdata\2020452-B-3-5.e57_5.0.24",
                //@"E:\rmdata\2020452-B-3-5.e57_5.1.0-prerelease0004",
                @"E:\rmdata\2020452-B-3-5.e57_5.1.0-prerelease0005",
            };

            foreach (var x in storePaths)
            {
                PointCloudStats(storePath: x, detailedNodeScan: true);
            }
        }

        static void PrintAllFileContainingCartesianInvalidState()
        {
            var files = Directory
                .EnumerateFiles(@"W:\Datasets\Vgm\Data\E57", "*.e57", SearchOption.AllDirectories)
                ;
            foreach (var file in files)
            {
                try
                {
                    var info = E57.E57Info(file, ParseConfig.Default);
                    var data3d = info.Metadata.E57Root.Data3D[0];
                    if (!data3d.Has(PointPropertySemantics.CartesianInvalidState)) continue;
                    Console.WriteLine($"{(data3d.Has(PointPropertySemantics.CartesianInvalidState) ? 'X' : ' ')} {file} {info.FileSizeInBytes:N0}");
                }
                catch //(Exception e)
                {
                    //Console.WriteLine($"{file}: {e.Message}");
                }
            }
        }

        static void Main(string[] args)
        {
            //GeneratePointCloudStats();
            //return;

            //PrintAllFileContainingCartesianInvalidState();

            var basedir = @"W:\Datasets\Vgm\Data\E57";
            //var basedir = @"W:\Datasets\pointclouds\e57-3d-imgfmt";
            var files = Directory
                .EnumerateFiles(basedir, "*.e57", SearchOption.AllDirectories)
                .OrderBy(x => x)
                //.Where(x => x.Contains("Cylcone"))
                //.Where(x => x.Contains("Statue"))
                //.Where(x => x.Contains("illnach"))
                .ToArray()
                ;
            foreach (var file in files)
            {
                try
                {
                    var info = E57.E57Info(file, ParseConfig.Default);
                    if (info.FileSizeInBytes > 4L * 1024 * 1024 * 1024) continue;
                    var data3d = info.Metadata.E57Root.Data3D[0];
                    //if (!data3d.Has(PointPropertySemantics.CartesianInvalidState)) continue;
                    Console.WriteLine($"{(data3d.Has(PointPropertySemantics.CartesianInvalidState) || data3d.Has(PointPropertySemantics.SphericalInvalidState) ? 'X' : ' ')} {file} {info.FileSizeInBytes:N0}");
                    foreach (var chunk in E57.ChunksFull(file, ParseConfig.Default).Take(1))
                    {
                        Console.WriteLine($"{chunk.Count}");
                        //if (chunk.RawData.ContainsKey(PointPropertySemantics.NormalX)) Debugger.Break();
                        //var foo = chunk.Colors.Where(c => c.R != c.G || c.R != c.B).ToArray();
                        //if (foo.Length > 0) Debugger.Break();
                        //var gs = chunk.Timestamps.GroupBy(x => new DateTimeOffset(x.Year, x.Month, x.Day, x.Hour, x.Minute, x.Second, TimeSpan.Zero)).Select(g => (g.Key, g.Count())).ToArray();
                        if (chunk.CartesianInvalidState != null)
                        {
                            var gs = chunk.CartesianInvalidState.GroupBy(x => x).Select(g => (g.Key, g.Count())).ToArray();
                            if (gs.Length != 1) Console.WriteLine($"contains invalid points (cartesian)");
                        }
                        if (chunk.SphericalInvalidState != null)
                        {
                            var gs = chunk.SphericalInvalidState.GroupBy(x => x).Select(g => (g.Key, g.Count())).ToArray();
                            if (gs.Length != 1) Console.WriteLine($"contains invalid points (spherical)");
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"{e}");
                }
            }

            //var file = @"W:\Datasets\Vgm\Data\E57\Infinity.e57";
            //var info = E57.E57Info(file, ParseConfig.Default);
            //foreach (var chunk in E57.Chunks(file, ParseConfig.Default))
            //{
            //    Console.WriteLine($"{chunk.Count}");
            //}
        }
    }
}
