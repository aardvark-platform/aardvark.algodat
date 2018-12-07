using Aardvark.Base;
using Aardvark.Data.Points;
using Aardvark.Geometry.Points;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace Aardvark.Geometry.Tests
{
    static class MasterLisa
    {
        public static void Perform()
        {
            var path2store = @"C:\Users\kellner\Desktop\Diplomarbeit\Store";
            var pData = @"C:\Users\kellner\Desktop\Diplomarbeit\networks\data.csv";

            if (File.Exists(pData)) File.Delete(pData);

            //var dirLabels = @"C:\Users\kellner\Desktop\Diplomarbeit\Semantic3d\sem8_labels_training";
            //var dirData = @"C:\Users\kellner\Desktop\Diplomarbeit\Semantic3d\sem8_data_training";

            //Import( // import new pointcloud
            //    Path.Combine(dirData, fn + ".txt"),
            //    Path.Combine(dirLabels, fn + ".labels"),
            //    path2store, "bildstein_station1");
            //AddNormals(path2store, key); // add normals
            //FilterPoints(path2store, key, 0); // filter all unlabelled points

            // -----------------------------------

            var store = PointCloud.OpenStore(path2store);
            //FilterPoints(path2store, id, 0); // TODO: fix it!

            var ids = new string[]
            {
                "bildstein_5_labelled_filtered",
                "neugasse_station1_filtered",
                "bildstein_station1_filtered"
            };

            ids.ForEach(key =>
            {
                var pointset = store.GetPointSet(key, CancellationToken.None);

                var root = pointset.Root.Value;
                var exponent = 0;
                
                var subnodes = root.Subnodes.Where(sn => sn != null).Select(sn => sn.Value);

                var nodes = subnodes.Select(sn => GetNodesWithExponent(sn, exponent)).
                    Where(x => x.Count() != 0);

                var truths = nodes.Select(ns =>
                {
                    var cl = ns.SelectMany(n =>
                    {
                        if (n.HasLodClassifications)
                            return n.LodClassifications.Value.Map(x => (int)x);
                        return new int[0];
                    });

                    var t = 0;

                    var classes = cl.Distinct();

                    var maxCount = (0, 0); // class,count
                    classes.ForEach(c =>
                        {
                            var count = cl.Where(x => x == c).Count();
                            if (count > maxCount.Item2)
                                maxCount = (c, count);
                        });
                    t = maxCount.Item1;

                    return t;
                }).ToArray();

                var maxRange = 40;

                nodes.ForEach((ns, k) =>
                {
                    var positions = ns.Select((n, i) => (n.Cell.X, n.Cell.Y, n.Cell.Z));
                    
                    var Xs = positions.Select(p => p.X).GroupBy(x => x).
                        Select(g => g.Key).OrderBy(x => x);

                    var Ys = positions.Select(p => p.Y).GroupBy(x => x).
                        Select(g => g.Key).OrderBy(x => x);

                    var Zs = positions.Select(p => p.Z).GroupBy(x => x).
                        Select(g => g.Key).OrderBy(x => x).OrderByDescending(x => x);

                    var rangeX = (new Range1d(Xs.Min(), Xs.Max())).Elements.Count();
                    var rangeY = (new Range1d(Ys.Min(), Ys.Max())).Elements.Count();
                    var rangeZ = (new Range1d(Zs.Min(), Zs.Max())).Elements.Count();

                    var minVal_XZ = (new long[] { Xs.Min(), Zs.Min() }).Min();
                    var maxVal_XZ = (new long[] { Xs.Max(), Zs.Max() }).Max();
                    var rangeXZ = (new Range1d(minVal_XZ, maxVal_XZ)).Elements.Count();

                    var minVal_XY = (new long[] { Xs.Min(), Ys.Min() }).Min();
                    var maxVal_XY = (new long[] { Xs.Max(), Ys.Max() }).Max();
                    var rangeXY = (new Range1d(minVal_XY, maxVal_XY)).Elements.Count();

                    var minVal_ZY = (new long[] { Ys.Min(), Zs.Min() }).Min();
                    var maxVal_ZY = (new long[] { Ys.Max(), Zs.Max() }).Max();
                    var rangeZY = (new Range1d(minVal_ZY, maxVal_ZY)).Elements.Count();

                    var d = (new long[] { rangeXZ, rangeXY, rangeZY }).Max();

                    if (d > maxRange)
                        return;

                    Report.Line($"range: {d}");

                    var overallDim = maxRange * maxRange * maxRange;
                    var mappedPositions = new double[overallDim];
                    for (int i = 0; i < overallDim; ++i)
                        mappedPositions[i] = 0;
                    
                    var grid = Ys.Select( (currentY, iy) =>
                    {
                         var currentPositions = positions.Where(x => x.Y == currentY);

                         var currentXs = currentPositions.Select(x => x.X);
                         var currentZs = currentPositions.Select(x => x.Z);

                         var minVal = (new long[] { currentXs.Min(), currentZs.Min() }).Min();
                         var maxVal = (new long[] { currentXs.Max(), currentZs.Max() }).Max();
                         
                         var idcs_X = new Range1d(minVal, maxVal).Elements;
                         var idcs_Z = idcs_X.Reverse();

                         int getIdx(IEnumerable<double> range, long v)
                         {
                             var idx = -1;
                             range.ForEach((elem, i) =>
                             {
                                 if (elem == v)
                                     idx = i;
                             });
                             return idx;
                         }

                         currentPositions.ForEach(pos =>
                         {
                             var xIdx = getIdx(idcs_X, pos.X);
                             var zIdx = getIdx(idcs_Z, pos.Z);

                             var mappedIdx = xIdx + zIdx * maxRange;
                             mappedPositions[mappedIdx + iy] = 1;
                         });
                         
                         Report.Line($"amount nodes: {currentPositions.Count()}");
                         
                         return mappedPositions;
                     }).ToArray();

                    File.AppendAllText(pData, $"{truths[k]},");

                    var line = mappedPositions.Map(mp => mp.ToString()).Join(",");
                    File.AppendAllText(pData, line + "\n");
                });
            });
            Report.Line();
            // ---------

            List<byte> ClassificationsOfTree(PointSetNode n, List<byte> classifications)
            {
                if (n.HasClassifications)
                    classifications.AddRange(n.Classifications.Value);

                if (n.IsLeaf())
                    return classifications;

                n.Subnodes.Where(sn => sn != null).ForEach(sn =>
                    ClassificationsOfTree(sn.Value, classifications)
                );

                return classifications;
            }

            void printTree(PointSetNode n, int counter, int wantedExp)
            {
                ++counter;
                var exp = n.Cell.Exponent;

                var tabs = "";
                for (int i = 0; i < counter - 1; ++i)
                    tabs += "\t";

                if (exp == wantedExp)
                {
                    Console.WriteLine(tabs + $"|--- exponent {exp}: node");
                    return;
                }

                if (n.IsLeaf())
                {
                    Console.WriteLine(tabs + $"|--- exponent {exp}: leaf");
                    return;
                }

                n.Subnodes.ForEach(sn =>
                {
                    if (sn == null)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine(tabs + $"|--- exponent {exp}: null");
                        Console.ForegroundColor = ConsoleColor.White;
                    }
                    else
                    {
                        Console.WriteLine(tabs + $"|--- exponent {exp}: node");
                        printTree(sn.Value, counter, wantedExp);
                    }
                });
            }
        }

        /// <summary>
        /// Returns all nodes from given tree with given exponent.
        /// </summary>
        private static IEnumerable<PointSetNode> GetNodesWithExponent(PointSetNode root, int exponent)
        {
            var nodes = new List<PointSetNode>();

            traverse(root, nodes);

            void traverse(PointSetNode n, List<PointSetNode> nodeList)
            {
                var exp = n.Cell.Exponent;
                if (exp == exponent)
                    nodeList.Add(n);
                else
                    if (n.IsNotLeaf())
                    n.Subnodes.Where(sn => sn != null).
                    ForEach(sn => traverse(sn.Value, nodeList));
            }
            return nodes;
        }

        /// <summary>
        /// Removes all points with given label from the pointcloud.
        /// </summary>
        private static void FilterPoints(string path2store, string key, int label2filter)
        {
            var store = PointCloud.OpenStore(path2store);
            var pointset = store.GetPointSet(key, CancellationToken.None);

            var chunks = pointset.QueryAllPoints();
            var newChunks = new List<Chunk>();

            // filter chunks
            chunks.ForEach((chunk, k) =>
            {
                if ((k % 100) == 0)
                    Report.Line($"filtering chunk #{k}");

                var indices = new List<int>();
                chunk.Classifications.ForEach((c, i) =>
                {
                    if ((int)c != label2filter)
                        indices.Add(i);
                });

                var positions = new List<V3d>();
                var colors = new List<C4b>();
                var normals = new List<V3f>();
                var intensities = new List<int>();
                var classifications = new List<byte>();

                indices.ForEach(idx =>
                {
                    if (chunk.HasPositions)
                        positions.Add(chunk.Positions[idx]);

                    if (chunk.HasColors)
                        colors.Add(chunk.Colors[idx]);

                    if (chunk.HasNormals)
                        normals.Add(chunk.Normals[idx]);

                    if (chunk.HasIntensities)
                        intensities.Add(chunk.Intensities[idx]);

                    if (chunk.HasClassifications)
                        classifications.Add(chunk.Classifications[idx]);
                });

                // create new chunks of filtered data
                var bbChunk = new Box3d(positions);
                newChunks.Add(new Chunk(positions, colors.IsEmpty() ? null : colors,
                    normals.IsEmpty() ? null : normals, intensities.IsEmpty() ? null : intensities,
                    classifications.IsEmpty() ? null : classifications, bbChunk));
            });

            var config = ImportConfig.Default
               .WithStorage(store)
               .WithKey(key + "_filtered")
               .WithMaxChunkPointCount(1024 * 1024)
               .WithVerbose(true);

            // add point-cloud to store
            PointCloud.Chunks(newChunks, config);
            store.Dispose();
        }

        /// <summary>
        /// Import pointcloud with labels from different files.
        /// </summary>
        private static void Import(string path2data, string path2labels, string path2store,
            string key, int maxChunkSize = 1024 * 1024)
        {
            var labels = File.ReadLines(path2labels)
                .Select(l => (byte)int.Parse(l, CultureInfo.InvariantCulture));

            var data = File.ReadLines(path2data);

            var chunkedData = data.Chunk(maxChunkSize);
            var chunkedLabels = labels.Chunk(maxChunkSize);

            // parsing files
            var parsedChunks = chunkedData.Select((ch, i) =>
           {
               var chunkPos = new V3d[ch.Length];
               var chunkCol = new C4b[ch.Length];

               ch.ForEach((l, j) =>
                   {
                       var s = l.Split(' ');

                       var pos = new V3d(double.Parse(s[0], CultureInfo.InvariantCulture),
                       double.Parse(s[1], CultureInfo.InvariantCulture),
                       double.Parse(s[2], CultureInfo.InvariantCulture));

                       var col = new C4b((byte)int.Parse(s[4], CultureInfo.InvariantCulture),
                       (byte)int.Parse(s[5], CultureInfo.InvariantCulture),
                       (byte)int.Parse(s[6], CultureInfo.InvariantCulture));

                       chunkPos[j] = pos;
                       chunkCol[j] = col;
                   });
               return (chunkPos, chunkCol);
           });

            var chunks = parsedChunks.Select((chunk, i) =>
           {
               Report.Line($"creating chunk #{i}");
               var bbChunk = new Box3d(chunk.chunkPos);
               return new Chunk(chunk.chunkPos, chunk.chunkCol, null, null, chunkedLabels.ToArray()[i], bbChunk);
           });

            //var chunk = new Chunk(positions, colors, null, null, labels, bb);

            var store = PointCloud.OpenStore(path2store);

            var config = ImportConfig.Default
               .WithStorage(store)
               .WithKey(key)
               .WithMaxChunkPointCount(maxChunkSize)
               .WithVerbose(true);

            // add point-cloud to store
            var pointset = PointCloud.Chunks(chunks, config);
            store.Dispose();
        }

        /// <summary>
        /// Computes and adds normals to pointset.
        /// </summary>
        /// <param name="k">Estimate per-point normals using k-nearest neighbours.</param>
        private static void AddNormals(string path2store, string key, int k = 16)
        {
            // compute normals
            var store = PointCloud.OpenStore(path2store);
            var pointset = store.GetPointSet(key, CancellationToken.None);
            var info = PointCloud.StoreInfo(path2store, key);

            Func<IList<V3d>, IList<V3f>> estimateNormals = (points) =>
            {
                var p = (V3d[])points;
                var n = p.EstimateNormals(k);
                return (IList<V3f>)n;
            };

            var config = ImportConfig.Default
                .WithStorage(store)
                .WithKey(key)
                .WithMaxChunkPointCount((int)info.PointCount + 1)
                .WithEstimateNormals(estimateNormals)
                .WithVerbose(true);

            var pointsetWithNormals = pointset.GenerateNormals(config);
            store.Dispose();
        }
    }
}
