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

            //var pData = @"C:\Users\kellner\Desktop\Diplomarbeit\networks";

            var dirData = @"C:\Users\kellner\Desktop\Diplomarbeit\data\Semantic3d\sem8_data_training";
            var dirLabels = @"C:\Users\kellner\Desktop\Diplomarbeit\data\Semantic3d\sem8_labels_training";

            var fn = "bildstein_station1_xyz_intensity_rgb";
            var k = "bildstein_station1_2";

            Import( // import new pointcloud
                Path.Combine(dirData, fn + ".txt"),
                Path.Combine(dirLabels, fn + ".labels"),
                path2store, k);
            AddNormals(path2store, k); // add normals
            FilterPoints(path2store, k, 0); // filter all unlabelled points
            
            // -----------------------------------

            var store = PointCloud.OpenStore(path2store);

            var keys = new string[]
            {
                "bildstein_station1_filtered",
                "bildstein_station3_filtered",
                "bildstein_5_labelled_filtered",
                "neugasse_station1_filtered",
                "sg27_station2_filtered",
            };

            // -----------------------------------

            var dumpStore = PointSetTests.CreateDiskStorage(
                @"C:\Users\kellner\Desktop\Diplomarbeit\dumpStore");

            var exponent = -3;

            var pointset = store.GetPointSet("neugasse_station1_filtered", CancellationToken.None);
            var (nodes, leafs) = GetNodesWithExponent(pointset.Root.Value, exponent);

            var foo = nodes.Select(n =>AmountClassesInNode(n)).GroupBy(x => x);
            var asdf = leafs.Select(n => AmountClassesInNode(n)).GroupBy(x => x);

            Report.Line("nodes:");
            foo.ForEach(g => Report.Line($"{g.Key} = {g.Count()}"));

            Report.Line("leafs:");
            asdf.ForEach(g => Report.Line($"{g.Key} = {g.Count()}"));

            //var pointsInNodes = nodes.Select(l => l.PointCount).GroupBy(x => x);
            //Report.Line("points in nodes:");
            //pointsInNodes.ForEach(g => Report.Line($"{g.Key} = {g.Count()}"));

            var i = 0;
            var nextIteration = leafs.RandomOrder().Take(1000).Select( n => 
            {
                ++i;

                if ((i % 250) == 0)
                {
                    Report.Line($"processed {i}/{leafs.Count()} leafs");
                    dumpStore.Flush();
                }

                var chunks = n.QueryAllPoints();

                var ps = chunks.SelectMany(ch => ch.Positions).ToList();

                var hasCs = chunks.All(ch => ch.HasColors);
                var cs = hasCs ? chunks.SelectMany(ch => ch.Colors).ToList() : null;

                var hasNs = chunks.All(ch => ch.HasNormals);
                var ns = hasNs ? chunks.SelectMany(ch => ch.Normals).ToList() : null;

                var hasJs = chunks.All(ch => ch.HasIntensities);
                var js = hasJs ? chunks.SelectMany(ch => ch.Intensities).ToList() : null;

                var hasKs = chunks.All(ch => ch.HasClassifications);
                var ks = hasKs ? chunks.SelectMany(ch => ch.Classifications).ToList() : null;

                var bb = new Box3d(ps);

                var pts = InMemoryPointSet.Build(ps, cs, ns, js, ks, bb, 2);
                var rootNode = pts.ToPointSetCell(dumpStore);

                return GetNodesWithExponent(rootNode, exponent);
            }).ToArray();

            var nextNodes = nextIteration.Map(x => x.Item1);
            var nextLeafs = nextIteration.Map(x => x.Item2);

            var pointsInRemainingLeafs = nextLeafs.SelectMany(l => l.Select(x => x.PointCount)).
                GroupBy(x => x);
            var pointsInRemainingNodes = nextNodes.SelectMany(l => l.Select(x => x.PointCount)).
                GroupBy(x => x);

            Report.Line("points in leafs:");
            pointsInRemainingLeafs.ForEach(g => Report.Line($"{g.Key} = {g.Count()}"));

            Report.Line("points in nodes:");
            pointsInRemainingNodes.ForEach(g => Report.Line($"{g.Key} = {g.Count()}"));

            dumpStore.Dispose();
            //var pp = @"C:\Users\kellner\Desktop\Diplomarbeit\networks\predictions.txt";
            //PerPointValidation(store, pp);
        }

        private static int AmountClassesInNode(PointSetNode n)
        {
            if(n.HasClassifications)
                return n.Classifications.Value.Distinct().Count();
            return -1;
        }

        private static void PerPointValidation(Storage store, string pResults)
        {
            var lines = File.ReadLines(pResults);

            var predictions = new Dictionary<string, List<(V3d, V3d, byte)>>();
            var classes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            var mappings = new Dictionary<byte, string>
            {
                { 1, "man-made"},
                { 2, "natural"},
                { 3, "high veg"},
                { 4, "low veg"},
                { 5, "buildings"},
                { 6, "hard scape"},
                { 7, "artefacts"},
                { 8, "cars"},
            };

            var cm = new NxN_ConfusionMatrix<byte>(classes, mappings);

            lines.ForEach(line =>
            {
                var splits = line.Split(';');

                var key = splits[0];
                var min = V3d.Parse(splits[1]);
                var max = V3d.Parse(splits[2]);
                var p = byte.Parse(splits[3]);

                if (!predictions.ContainsKey(key))
                    predictions.Add(key, new List<(V3d, V3d, byte)>());
                predictions[key].Add((min, max, p));
            });

            predictions.Keys.ForEach(key =>
            {
                var boxes = predictions[key];

                Report.Line($"processing pointcloud: {key}");
                var pointset = store.GetPointSet(key, CancellationToken.None);

                boxes.ForEach(vs =>
                {
                    var prediction = vs.Item3;

                    var truths = pointset.QueryPointsInsideBox(new Box3d(vs.Item1, vs.Item2)).
                        SelectMany(ch => ch.Classifications);

                    truths.ForEach(truth => cm.AddPrediction(truth, prediction));
                });
            });

            Report.Line($"ACC = {cm.Accuracy()}");
            cm.Print();
            cm.PrintPerClassAccuracy();
        }

        private class NxN_ConfusionMatrix<T>
        {
            private Dictionary<T, Dictionary<T, int>> m_cm;
            private Dictionary<T, string> m_classes2string;

            public NxN_ConfusionMatrix(IEnumerable<T> classes,
                Dictionary<T, string> classes2string = null)
            {
                m_cm = new Dictionary<T, Dictionary<T, int>>();

                m_classes2string = classes2string ?? new Dictionary<T, string>();

                classes.ForEach(truth =>
                {
                    m_cm.Add(truth, new Dictionary<T, int>());

                    classes.ForEach(prediction =>
                        m_cm[truth].Add(prediction, 0));

                    if (classes2string == null)
                        m_classes2string.Add(truth, truth.ToString());
                });
            }

            public void AddPrediction(T truth, T prediction) =>
                m_cm[truth][prediction] += 1;

            public double Accuracy()
            {
                var classes = m_cm.Keys;

                var correct = 0.0;
                var wrong = 0.0;

                classes.ForEach(truth =>
                {
                    classes.ForEach(prediction =>
                    {
                        if (truth.Equals(prediction))
                            correct += m_cm[truth][prediction];
                        else
                            wrong += m_cm[truth][prediction];
                    });
                });
                return correct / (correct + wrong);
            }

            public Dictionary<T, double> PerClassAccuracy()
            {
                var classes = m_cm.Keys;
                var acc = new Dictionary<T, double>();

                classes.ForEach(truth =>
                {
                    var correct = 0.0;
                    var wrong = 0.0;

                    classes.ForEach(prediction =>
                    {
                        if (truth.Equals(prediction))
                            correct += m_cm[truth][prediction];
                        else
                            wrong += m_cm[truth][prediction];
                    });
                    acc.Add(truth, correct / (correct + wrong));
                });
                return acc;
            }

            public void PrintPerClassAccuracy()
            {
                var dict = PerClassAccuracy();
                dict.Keys.ForEach(c => Report.Line($"{m_classes2string[c]} = {dict[c]:0.0000}"));
            }

            public void Print()
            {
                var classes = m_cm.Keys;
                var cs = classes.Select(c => m_classes2string[c]).ToArray();

                Console.Write("{0,-15}", "");
                cs.ForEach(c => Console.Write("{0,-15}", c));
                Console.WriteLine();

                classes.ForEach(truth =>
                {
                    Console.Write("{0,-15}", m_classes2string[truth]);
                    classes.ForEach(prediction => Console.Write("{0,-15}", m_cm[truth][prediction]));
                    Console.WriteLine();
                });
            }
        }

        /// <summary>
        /// Exports occupancy values (flattend cubes with given gridsize and mode)
        /// of pointclouds to csv-file.
        /// </summary>
        /// <param name="mode">binary, count, fraction</param>
        /// <param name="grids">gridsize of cube</param>
        private static void ExportPointclouds(string path2store, string[] keys, string outPath,
            string mode = "binary", double grids = 8.0, int exponent = -3)
        {
            var store = PointCloud.OpenStore(path2store);

            keys.ForEach(key =>
            {
                Report.Warn($"processing pointcloud: {key}");

                var pointset = store.GetPointSet(key, CancellationToken.None);
                var (nodes, leafs) = GetNodesWithExponent(pointset.Root.Value, exponent);

                nodes.ForEach((subnode, j) =>
                {
                    if ((j % 100) == 0)
                        Report.Line($"processing subnode # {j}");

                    // converts the boxes into a grid with occupancy values
                    var box = subnode.BoundingBox;

                    var min = box.Min;
                    var max = box.Max;

                    var boxSize = Math.Abs(max.X - min.X);
                    var gridSize = boxSize / grids;

                    var cl = subnode.QueryPointsInsideBox(box).SelectMany(c => c.Classifications);
                    var truth = cl.GroupBy(c => c).OrderByDescending(g => g.ToArray().Length).First().Key;

                    var occupancies = new List<string>();

                    for (double y = min.Y; y < max.Y; y += gridSize)
                        for (double z = max.Z; z > min.Z; z -= gridSize)
                            for (double x = min.X; x < max.X; x += gridSize)
                            {
                                var b = new Box3d(x, y, z, x + gridSize, y + gridSize, z + gridSize);
                                var pc = subnode.CountPointsInsideBox(b);

                                // calculate the occupancy value of the inner cube
                                //var occ = pc > 0 ? 1 : 0; // binary 
                                //var occ = pc; // point count
                                //var occ = pc / 8192.0; // fraction of max points

                                var occ = mode == "binary" ? (pc > 0 ? 1 : 0) :
                                       mode == "count" ? pc : pc / 8192.0;

                                occupancies.Add(occ.ToString(CultureInfo.InvariantCulture));
                            }
                    var data = ($"{key};{min};{max};{truth};{occupancies.Join(";")}\n");
                    File.AppendAllText(outPath, data);
                });
            });
        }

        /// <summary>
        /// PointCloudNode attributes for machine learning.
        /// </summary>
        private static class AttributesML
        {
            public const string Predictions = "Predictions";
        }

        /// <summary>
        /// Extension functions for ML-attributes
        /// </summary>
        private static class FutureExtensions
        {
            public static PersistentRef<byte[]> GetPredictions(IPointCloudNode self)
            {
                var key = ComputeKey4Predictions(self);
                var predictions = self.Storage.GetByteArray(key, default);

                return new PersistentRef<byte[]>(key, (id, ct) =>
                    self.Storage.GetByteArray(id, ct), predictions);
            }

            public static void AddPredictions(IPointCloudNode self, byte[] predictions) =>
                self.Storage.Add(ComputeKey4Predictions(self), predictions, default);

            private static string ComputeKey4Predictions(IPointCloudNode self) =>
                "predictions_" + self.GetHashCode().ToString();
        }

        /// <summary>
        /// Returns all nodes from given tree with given exponent.
        /// </summary>
        private static (IEnumerable<PointSetNode>, IEnumerable<PointSetNode>) GetNodesWithExponent(
            PointSetNode root, int exponent)
        {
            var nodes = new List<PointSetNode>();
            var leafs = new List<PointSetNode>();

            traverse(root, nodes);

            void traverse(PointSetNode n, List<PointSetNode> nodeList)
            {
                var exp = n.Cell.Exponent;
                if (exp == exponent)
                    nodeList.Add(n);
                else
                {
                    if (n.IsNotLeaf())
                        n.Subnodes.Where(sn => sn != null).
                        ForEach(sn => traverse(sn.Value, nodeList));
                    else
                        leafs.Add(n);
                }
            }
            return (nodes,leafs);
        }

        /// <summary>
        /// Removes all points with given label from the pointcloud.
        /// </summary>
        private static void FilterPoints(string path2store, string key, int label2filter)
        {
            var store = PointCloud.OpenStore(path2store);
            var pointset = store.GetPointSet(key, CancellationToken.None);
            var chunks = pointset.QueryAllPoints();

            var singlePointCounter = 0;

            // filter chunks
            var newChunks = chunks.Select((chunk, k) =>
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

                if (positions.Count == 1)
                    Report.Warn($"chunks with single point: {++singlePointCounter}");

                    // create new chunks of filtered data
                    var bbChunk = new Box3d(positions);
                return new Chunk(positions, colors.IsEmpty() ? null : colors,
                    normals.IsEmpty() ? null : normals, intensities.IsEmpty() ? null : intensities,
                    classifications.IsEmpty() ? null : classifications, bbChunk);

            }).//Where(chunk => !chunk.Positions.IsEmptyOrNull());
            Where(chunk => chunk.Positions.Count > 1);

            var config = ImportConfig.Default
               .WithStorage(store)
               .WithKey(key + "_filtered")
               .WithMaxChunkPointCount(1024 * 1024)
               .WithVerbose(true);

            // add point-cloud to store
            PointCloud.Chunks(newChunks, config);
            store.Dispose();

            Report.Warn($"chunks with single point: {++singlePointCounter}");
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
               .WithOctreeSplitLimit(500)
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
                .WithMaxChunkPointCount(1024 * 1024)
                .WithEstimateNormals(estimateNormals)
                .WithVerbose(true);

            var pointsetWithNormals = pointset.GenerateNormals(config);
            store.Dispose();
        }
    }
}
