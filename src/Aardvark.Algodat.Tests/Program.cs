using Aardvark.Base;
using Aardvark.Data;
using Aardvark.Data.Points;
using Aardvark.Data.Points.Import;
using Aardvark.Geometry.Points;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Uncodium.SimpleStore;

namespace Aardvark.Geometry.Tests
{
    public static class Program
    {
        internal static V3i ShiftRight(this V3i self, int i)
            => i > 1 ? new V3i(self.X >> i, self.Y >> i, self.Z >> i) : self;

        internal struct PointD
        {
            public V3d Pos;
            public C3b Col;
            public PointD(V3d pos, C3b col) { Pos = pos; Col = col; }
        }

        internal class Node
        {
            public Cell Cell;
            public long PointCountTree;
            public V3f[] Positions;
            public C3b[] Colors;
            public Node[] Children;

            public Box3f BoundingBoxExactLocal;
            public Box3d BoundingBoxExactGlobal;
            public int TreeDepthMin;
            public int TreeDepthMax;
            public PointRkdTreeF<V3f[], V3f> KdTree;
            public V3f[] Normals;

            public long CountPoints()
            {
                var count = Positions != null ? Positions.Length : 0L;
                if (Children != null)
                {
                    foreach (var x in Children)
                    {
                        if (x != null) count += x.CountPoints();
                    }
                }
                return count;
            }

            public long CountNodes()
            {
                var count = 1L;
                if (Children != null)
                {
                    foreach (var x in Children)
                    {
                        if (x != null) count += x.CountNodes();
                    }
                }
                return count;
            }

            public bool IsLeaf => Children == null;

            public async Task GenerateLod(int splitLimit)
            {
                if (IsLeaf)
                {
                    PointCountTree = Positions.Length;
                    BoundingBoxExactLocal = new Box3f(Positions);
                    BoundingBoxExactGlobal = (Box3d)BoundingBoxExactLocal + Cell.GetCenter();
                    TreeDepthMin = 0;
                    TreeDepthMax = 0;
                }
                else
                {
                    await Task.WhenAll(Children.Where(x => x != null).Select(x => x.GenerateLod(splitLimit)));
                    
                    PointCountTree = Children.Sum(x => x != null ? x.PointCountTree : 0L);
                    BoundingBoxExactGlobal = new Box3d(Children.Where(x => x != null).Select(x => x.BoundingBoxExactGlobal));
                    TreeDepthMin = Children.Select(x => x != null ? x.TreeDepthMin + 1 : 0).Min();
                    TreeDepthMax = Children.Select(x => x != null ? x.TreeDepthMax + 1 : 0).Max();

                    Positions = new V3f[splitLimit];
                    Colors = new C3b[splitLimit];

                    var pointCountChildren = Children.Sum(x => x != null ? x.Positions.Length : 0);
                    var df = (float)pointCountChildren / splitLimit;

                    var i = 0; var fi = 0.0f;
                    for (var ci = 0; ci < 8; ci++)
                    {
                        var c = Children[ci];
                        if (c == null) continue;
                        var fiMax = c.Positions.Length - 1.0f;
                        while (fi < fiMax)
                        {
                            Positions[i] = c.Positions[(int)fi];
                            i++;
                            fi += df;
                        }
                        fi -= fiMax;
                    }

                    i = 0; fi = 0.0f;
                    for (var ci = 0; ci < 8; ci++)
                    {
                        var c = Children[ci];
                        if (c == null) continue;
                        var fiMax = c.Positions.Length - 1.0f;
                        while (fi < fiMax)
                        {
                            Colors[i] = c.Colors[(int)fi];
                            i++;
                            fi += df;
                        }
                        fi -= fiMax;
                    }
                }

                KdTree = await Positions.BuildKdTreeAsync();
                Normals = await Positions.EstimateNormalsAsync(16, KdTree);
            }

            private Node(Cell cell, List<PointD> points, Node[] children)
            {
                Cell = cell;
                var center = Cell.GetCenter();
                Positions = points?.MapToArray(x => (V3f)(x.Pos - center));
                Colors = points?.MapToArray(x => x.Col);
                Children = children;
            }

            public static Node Build(Cell bounds, List<PointD> points, int splitLimit)
            {
                if (points.Count <= splitLimit)
                {
                    return new Node(bounds, points, children: null);
                }
                else
                {
                    var center = bounds.GetCenter();
                    var childPoints = new List<PointD>[8];
                    foreach (var p in points)
                    {
                        int o = 0;
                        if (p.Pos.X >= center.X) o = 1;
                        if (p.Pos.Y >= center.Y) o |= 2;
                        if (p.Pos.Z >= center.Z) o |= 4;

                        if (childPoints[o] == null) childPoints[o] = new List<PointD>();
                        childPoints[o].Add(p);
                    }

                    var children = childPoints.Map((x, i) => x != null ? Build(bounds.GetOctant((int)i), x, splitLimit) : null);
                    return new Node(bounds, null, children);
                }
            }
            public static Node Build(Cell bounds, IEnumerable<(Cell cell, List<PointD> points)> cells, int splitLimit)
            {
                if (cells == null || !cells.Any()) throw new InvalidOperationException();

                var center = bounds.GetCenter();
                var boundsBb = bounds.BoundingBox;
                var e = cells.First().cell.Exponent;
                if (cells.Any(c => c.cell.Exponent != e)) throw new InvalidOperationException();
                if (!cells.All(c => boundsBb.Contains(c.cell.BoundingBox))) throw new InvalidOperationException();

                var totalCount = cells.Sum(x => (long)x.points.Count);

                if (totalCount <= splitLimit)
                {
                    var cell = new Cell(new Box3d(cells.Select(x => x.cell.BoundingBox)));
                    var points = new List<PointD>(); foreach (var x in cells) points.AddRange(x.points);
                    return new Node(cell, points, children: null);
                }

                if (e < bounds.Exponent)
                {
                    var childCells = new List<(Cell cell, List<PointD> points)>[8];
                    foreach (var x in cells)
                    {
                        if (x.cell.IsCenteredAtOrigin) throw new InvalidOperationException();
                        var bb = x.cell.BoundingBox;
                        int o = 0;
                        if (bb.Min.X >= center.X) o = 1;
                        if (bb.Min.Y >= center.Y) o |= 2;
                        if (bb.Min.Z >= center.Z) o |= 4;
                        if (childCells[o] == null) childCells[o] = new List<(Cell cell, List<PointD> points)>();
                        childCells[o].Add(x);
                    }

                    var children = childCells.Map((x, i) => x != null ? Build(bounds.GetOctant((int)i), x, splitLimit) : null);
                    return new Node(bounds, null, children);
                }
                else
                {
                    var points = new List<PointD>(); foreach (var x in cells) points.AddRange(x.points);
                    return Build(bounds, points, splitLimit);
                }
            }
        }

        [ThreadStatic]
        private static HashSet<V3d> s_deduplicate;
        internal static List<PointD> Deduplicate(this IEnumerable<PointD> self, int digits)
        {
            if (s_deduplicate == null) s_deduplicate = new HashSet<V3d>();
            s_deduplicate.Clear();
            var rs = new List<PointD>();
            foreach (var p in self)
            {
                var pos = p.Pos.Round(digits);
                if (!s_deduplicate.Contains(pos))
                {
                    s_deduplicate.Add(pos);
                    rs.Add(p);
                }
            }

            return rs;
        }

        internal static void PerfTest20190705()
        {
            //var filename = @"T:\Vgm\Data\2017-10-20_09-44-27_1mm_shade_norm_5pp - Cloud.pts";
            var filename = @"T:\Vgm\Data\Kindergarten.pts";

            int splitLimit = 8192;


            Report.BeginTimed($"total");

            Report.BeginTimed($"processing");
            var cells = File
                .ReadLines(filename)
                //.Take(17000000)
                .AsParallel()
                .AsUnordered()
                .Select(line => line.Split(' '))
                .Where(ts => ts.Length == 7)
                .Select(ts => new PointD(
                    new V3d(
                        double.Parse(ts[0], CultureInfo.InvariantCulture),
                        double.Parse(ts[1], CultureInfo.InvariantCulture),
                        double.Parse(ts[2], CultureInfo.InvariantCulture)
                        ),
                    new C3b(byte.Parse(ts[4]), byte.Parse(ts[5]), byte.Parse(ts[6]))
                    ))
                .GroupBy(x => ((V3i)x.Pos))
                .Select(g => {
                    var points = g.Deduplicate(digits: 2);
                    return (key: new Cell(Box3d.FromMinAndSize((V3d)g.Key, V3d.III)), points);
                })
                .ToList()
                ;
            Report.EndTimed();

            var bb = new Box3d(cells.Select(x => x.key.BoundingBox));
            var bbCell = new Cell(bb);

            Report.Line($"points: {cells.Sum(g => g.points.Count):N0}");
            Report.Line($"bounds: {bb}");
            Report.Line($"bounds: {bbCell}");
            Report.Line($"groups:  {cells.Count:N0}");
            Report.Line($">= 16384: {cells.Count(x => x.points.Count >= 16384):N0}");
            Report.Line($">=  8192: {cells.Count(x => x.points.Count < 16384 && x.points.Count >= 8192):N0}");
            Report.Line($" <  8192: {cells.Count(x => x.points.Count < 8192):N0}");

            Report.BeginTimed("building tree");
            var tree = Node.Build(bbCell, cells, splitLimit);
            Report.EndTimed();

            Report.Line($"tree: count points: {tree.CountPoints(),15:N0}");
            Report.Line($"tree: count nodes : {tree.CountNodes(),15:N0}");

            Report.BeginTimed("generate lod");
            tree.GenerateLod(splitLimit).Wait();
            Report.EndTimed();

            Report.EndTimed(); // total
        }

        internal static void PerfTest20190701()
        {
            var filename = @"T:\Vgm\Data\2017-10-20_09-44-27_1mm_shade_norm_5pp - Cloud.pts";

            var config = ImportConfig.Default
                .WithInMemoryStore()
                .WithRandomKey()
                .WithVerbose(true)
                .WithMaxDegreeOfParallelism(0)
                .WithMinDist(0.01)
                .WithNormalizePointDensityGlobal(true)
                ;

            Report.BeginTimed("total");

            var chunks = Pts.Chunks(filename, config.ParseConfig);
            var pc = PointCloud.Chunks(chunks, config);

            Report.EndTimed();

            Report.Line($"count -> {pc.PointCount}");
        }

        internal static void TestE57()
        {
            var sw = new Stopwatch();

            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            var filename = @"T:\Vgm\Data\E57\Innenscan_FARO.e57";
            var fileSizeInBytes = new FileInfo(filename).Length;

            var info = E57.E57Info(filename, ParseConfig.Default);
            Report.Line($"total bounds: {info.Bounds}");
            Report.Line($"total count : {info.PointCount:N0}");

            var config = ImportConfig.Default
                .WithInMemoryStore()
                .WithRandomKey()
                .WithVerbose(true)
                .WithMaxDegreeOfParallelism(0)
                .WithMinDist(0.01)
                ;
            
            Report.BeginTimed("total");

            var chunks = E57
                .Chunks(filename, config.ParseConfig)
                //.Take(10)
                //.AsParallel()
                //.Select(x => x.ImmutableFilterMinDistByCell(new Cell(x.BoundingBox), config))
                //.Select(x => x.ImmutableFilterSequentialMinDistL1(0.01))
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

            //chunks = E57.Chunks(filename, config.ParseConfig);
            //var pointcloud = PointCloud.Chunks(chunks, config);
            //Console.WriteLine($"pointcloud.PointCount  : {pointcloud.PointCount}");
            //Console.WriteLine($"pointcloud.Bounds      : {pointcloud.Bounds}");
            //Console.WriteLine($"pointcloud.BoundingBox : {pointcloud.BoundingBox}");

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

        internal static void TestLoadOldStore()
        {
            var store = new SimpleDiskStore(@"T:\Vgm\Stores\referenz_2019_21_store").ToPointCloudStore(cache: default);
            var pc = store.GetPointSet("770ed498-5544-4313-9873-5449f2bd823e");
            var root = store.GetPointCloudNode("e06a1e87-5ab1-4c73-8c3f-3daf1bdac1d9");
        }

        internal static void TestOebb()
        {
            dynamic json = JObject.Parse(File.ReadAllText(@"T:\OEBB\20190619_Trajektorie_Verbindungsbahn\path2.json"));
            Console.WriteLine(json.properties);
            var offset = new V3d(
                double.Parse((string)json.properties.x, CultureInfo.InvariantCulture),
                double.Parse((string)json.properties.y, CultureInfo.InvariantCulture),
                double.Parse((string)json.properties.z, CultureInfo.InvariantCulture)
                );
            Console.WriteLine($"offset: {offset}");

            var coords = (JArray)json.geometry.coordinates;
            var path = coords
                .Map(xs => (JArray)xs)
                .Map(xs => new V3d((double)xs[0], (double)xs[1], (double)xs[2]))
                .Map(x => x + offset)
                ;
            var pathBounds = new Box3d(path);
            Console.WriteLine($"path points: {path.Length:N0}");
            Console.WriteLine($"path bounds: {pathBounds}");
        }

        public static void Main(string[] args)
        {
            TestOebb();

            //PerfTest20190705();

            //var path = JObject.Parse(File.ReadAllText(@"T:\OEBB\20190619_Trajektorie_Verbindungsbahn\path.json"));
            //File.WriteAllText(@"T:\OEBB\20190619_Trajektorie_Verbindungsbahn\path2.json", path.ToString(Formatting.Indented));
            //Console.WriteLine(path);

            //PerfTest20190701();

            //TestLoadOldStore();

            //new ViewsFilterTests().CanDeletePoints();

            //new DeleteTests().DeleteDelete();
            //Console.WriteLine("done");

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
