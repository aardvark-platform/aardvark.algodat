using Aardvark.Base;
using Aardvark.Data.Points;
using Aardvark.Geometry.Points;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Aardvark.Geometry.Tests
{
    static class MasterLisa
    {
        public static void Perform()
        {
            //var pathPredictions = @"C:\Users\kellner\Desktop\Diplomarbeit\src\outputs\predictions.txt";
            //AddPredictions(PointCloud.OpenStore(@"C:\Users\kellner\Desktop\Diplomarbeit\store"),
            //    pathPredictions, -3);
            //return;

            var path2store = @"G:\Semantic3d\Store";
            //var path2store = @"C:\Users\kellner\Desktop\Diplomarbeit\store";
            var dirData = @"C:\Users\kellner\Desktop\Diplomarbeit\data\Semantic3d\sem8_data_training";

            var filenames = Directory.EnumerateFiles(dirData).
                Select(x => Path.GetFileNameWithoutExtension(x));

            var keys = filenames.Select(fn =>
            {
                var splits = fn.Split('_');
                return splits[0] + "_" + splits[1];
            }).ToArray();

            var keysFiltered = keys.Take(3).Select(k => k + "_filtered").ToArray();

            var exponent = -3;
            var minPointsInBox = 10;
            var mode = "count";
            var gridsize = 16;

            var store = PointCloud.OpenStore(path2store);
            var dir = @"C:\Users\kellner\Desktop\Diplomarbeit\src\data\Semantic3d_gs=" + gridsize.ToString() + @"\" + mode;

            //var pathPredictions = @"C:\Users\kellner\Desktop\Diplomarbeit\networks\src\outputs\predictions.txt";

            //PerPointValidation(store, pathPredictions);
            ExportPointclouds(path2store, keysFiltered, dir, minPointsInBox, mode, gridsize, exponent);
            //AddPredictions(store, pathPredictions, exponent);

            // -----------------------------------

            //var pp = @"C:\Users\kellner\Desktop\Diplomarbeit\networks\predictions1.txt";
            //var pout = @"C:\Users\kellner\Desktop\Diplomarbeit\networks\";
            //PerPointValidation(store, pp, pout);
        }

        /// <summary>
        /// Adds predictions from a file to the according pointcloud.
        /// </summary>
        private static void AddPredictions(Storage store, string pathPredictions, int exponent)
        {
            var predictions = new List<(V3d, V3d, byte)>();
            var key = "";

            File.ReadLines(pathPredictions).ForEach(line =>
            {
                var splits = line.Split(';');

                if(key.IsEmptyOrNull())
                    key = splits[0];
                else
                    if (splits[0] != key)
                        throw new Exception("predictions-file contains different keys");
                
                var min = V3d.Parse(splits[1]);
                var max = V3d.Parse(splits[2]);
                var p = byte.Parse(splits[3]);
                
                predictions.Add((min, max, p));
            });
            
            var pointset = store.GetPointSet(key, CancellationToken.None);
            var bbs = predictions.Map(p => (new Box3d(p.Item1, p.Item2), p.Item3));
            var (nodes, leafs) = GetNodesWithExponent(pointset.Root.Value, exponent);

            // add predictions as property to nodes
            nodes.ForEach(node => 
            {
                var pc = (int)node.PointCountTree;
                var bb = bbs.Where(b => b.Item1 == node.BoundingBox).Single();
                var ps = Enumerable.Repeat(bb.Item2, pc).ToArray();
                FutureExtensions.AddPredictions(node, ps);
                bbs.Remove(bb);
            });

            // add predictions as property to leafs
            leafs.ForEach(leaf => 
            {
                var boxes = bbs.Where(b => leaf.BoundingBox.Contains(b.Item1)).ToArray();
                
                if(boxes.Length > 0)
                {
                    var positions = leaf.GetLodPositionsAbsolute();
                    var ps = positions.Select(pos => 
                    {
                        var box4point = boxes.Where(b => b.Item1.Contains(pos)).ToArray();

                        if (box4point.Length > 1)
                            throw new Exception("[AddPredictions] position contained by more than one bb");

                        if (box4point.Length == 0)
                            return (byte)0;

                        return box4point.Single().Item2;
                    }).ToArray();
                    FutureExtensions.AddPredictions(leaf, ps);
                }
                boxes.ForEach(b => bbs.Remove(b));
            });

            if (bbs.Count() > 0)
                throw new Exception("[AddPredictions] not all predictions could be matched.");
        }
        
        /// <summary>
        /// Imports and filters whole semantic3d dataset.
        /// </summary>
        private static void ImportSemantic3d()
        {
            var path2store = @"C:\Users\kellner\Desktop\Diplomarbeit\store";

            var dirData = @"C:\Users\kellner\Desktop\Diplomarbeit\data\Semantic3d\sem8_data_training";
            var dirLabels = @"C:\Users\kellner\Desktop\Diplomarbeit\data\Semantic3d\sem8_labels_training";

            var filenames = Directory.EnumerateFiles(dirData).
                Select(x => Path.GetFileNameWithoutExtension(x)).Where(x => x.Contains("bildstein_station3"));
            
            var keys = filenames.Select(fn =>
            {
                var splits = fn.Split('_');
                return splits[0] + "_" + splits[1];
            }).ToArray();

            filenames.ForEach((fn, i) =>
            {
                var k = keys[i];

                Import( // import new pointcloud
                    Path.Combine(dirData, fn + ".txt"),
                    Path.Combine(dirLabels, fn + ".labels"),
                    path2store, k);
                Report.Warn("import done, starting filtering");
                //AddNormals(path2store, k); // add normals
                FilterPoints(path2store, k, 0); // filter all unlabelled points
            });
        }

        /// <summary>
        /// Computes the amount of classes in a node.
        /// </summary>
        private static int AmountClassesInNode(PointSetNode n)
        {
            if(n.HasClassifications)
                return n.Classifications.Value.Distinct().Count();
            return -1;
        }

        /// <summary>
        /// Validates given predictions (one label for a bb) with labels of single points.
        /// </summary>
        private static void PerPointValidation(Storage store, string fnPredictions,
            bool writeResults = false)
        {
            var dir = Path.GetDirectoryName(fnPredictions);
            var outprediction = Path.Combine(dir, "perPointPredictions.txt");
            var outtruth = Path.Combine(dir, "perPointTargets.txt");

            var lines = File.ReadLines(fnPredictions);

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
                var values = predictions[key];

                Report.Line($"processing pointcloud: {key}");
                var pointset = store.GetPointSet(key, CancellationToken.None);

                values.ForEach(vs =>
                {
                    var prediction = vs.Item3;

                    var truths = pointset.QueryPointsInsideBox(new Box3d(vs.Item1, vs.Item2)).
                        SelectMany(ch => ch.Classifications);
                    
                    if (writeResults)
                    {
                        var ts = new List<string>();
                        var ps = new List<string>();
                        truths.ForEach(truth =>
                        {
                            cm.AddPrediction(truth, prediction);

                            ts.Add(truth.ToString());
                            ps.Add(prediction.ToString());
                        });

                        File.AppendAllLines(outtruth, ts);
                        File.AppendAllLines(outprediction, ps);
                    }
                    else
                        truths.ForEach(truth => cm.AddPrediction(truth, prediction));
                });
            });
            
            Report.Line("\nper class accuracy:");
            cm.PrintPerClassAccuracy();

            Report.Line("\nintersection over union:");
            var a_iou = cm.PrintPerClassIntersectionOverUnion();

            Report.Line("\nconfusion matrix:");
            cm.Print();

            Report.Line($"\nOverall ACC = {cm.Accuracy():0.0000}");

            Report.Line($"\nAverage IoU = {a_iou:0.0000}");
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

            public double PrintPerClassIntersectionOverUnion()
            {
                var classes = m_cm.Keys;

                var perRowError = new Dictionary<T, double>();
                var perColumnError = new Dictionary<T, double>();
                var correct = new Dict<T, double>();

                classes.ForEach(truth =>
                {
                    var wrongRow = 0.0;
                    var wrongColumn = 0.0;

                    classes.ForEach(prediction =>
                    {
                        if (!truth.Equals(prediction))
                        {
                            wrongRow += m_cm[truth][prediction];
                            wrongColumn += m_cm[prediction][truth];
                        }
                        else
                            correct.Add(truth, m_cm[truth][prediction]);
                    });
                    perRowError.Add(truth, wrongRow);
                    perColumnError.Add(truth, wrongColumn);
                });

                var ious = classes.Select(cl => 
                {
                    var iou = correct[cl] / (perRowError[cl] + perColumnError[cl] + correct[cl]);
                    Report.Line($"{m_classes2string[cl]}: {iou:0.0000}");
                    return iou;
                }).Sum();

                return ious / classes.Count();
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
        /// Splits leafs further to until given exponent is reached. 
        /// Returns only boxes with point count > minPointsInBox.
        /// </summary>
        private static IEnumerable<(List<Box3d>, PointSetNode)> ArtificialLeafSplit(
            IEnumerable<PointSetNode> leafs, int exponent, int minPointsInBox)
        {
            return leafs.Select( (n,i) =>
            {
                if ((i % 100) == 0)
                    Report.Line($"processed {i}/{leafs.Count()} leafs");

                var diff = (double)Math.Abs(exponent - n.Cell.Exponent);

                var boxes = new List<Box3d> { n.BoundingBox };
                while (diff > 0)
                {
                    var octants = boxes.SelectMany(bb =>
                        new Range1i(0, 7).Elements.Select(idx => bb.GetOctant(idx)));
                    boxes = octants.Where(bb => n.QueryPointsInsideBox(bb).
                        SelectMany(x => x.Positions).Count() > minPointsInBox).ToList();
                    diff -= 1;
                }
                return (boxes, n);
            });
        }

        /// <summary>
        /// Converts the boxes into a grid with occupancy values
        /// and appends it to the given file.
        /// </summary>
        private static void WriteBox2File(Box3d box, PointSetNode node, string mode, 
            double grids, string key, string path)
        {
            var min = box.Min;
            var max = box.Max;

            var boxSize = Math.Abs(max.X - min.X);
            var gridSize = boxSize / grids;

            var cl = node.QueryPointsInsideBox(box).SelectMany(c => c.Classifications);
            if (cl.IsEmptyOrNull()) return;

            var truth = cl.GroupBy(c => c).OrderByDescending(g => g.ToArray().Length).First().Key;
            
            var occupancies = new List<string>();

            for (double y = min.Y; y < max.Y; y += gridSize)
                for (double z = max.Z; z > min.Z; z -= gridSize)
                    for (double x = min.X; x < max.X; x += gridSize)
                    {
                        var b = new Box3d(x, y, z, x + gridSize, y + gridSize, z + gridSize);
                        var pc = node.CountPointsInsideBox(b);

                        // calculate the occupancy value of the inner cube
                        //var occ = pc > 0 ? 1 : 0; // binary 
                        //var occ = pc; // point count
                        //var occ = pc / 8192.0; // fraction of max points

                        var occ = mode == "binary" ? (pc > 0 ? 1 : 0) :
                               mode == "count" ? pc : pc / 8192.0;

                        occupancies.Add(occ.ToString(CultureInfo.InvariantCulture));
                    }
            var data = ($"{key};{min};{max};{truth};{occupancies.Join(";")}\n");
            File.AppendAllText(path, data);
        }

        /// <summary>
        /// Exports occupancy values (flattend cubes with given gridsize and mode)
        /// of pointclouds to csv-file.
        /// </summary>
        /// <param name="mode">binary, count, fraction</param>
        /// <param name="grids">gridsize of cube</param>
        private static void ExportPointclouds(string path2store, string[] keys, string baseDir,
            int minPointsInBox, string mode = "binary", double grids = 8.0, int exponent = -3)
        {
            var store = PointCloud.OpenStore(path2store);

            keys.ForEach(key =>
            {
                Report.Warn($"processing pointcloud: {key}");

                var outPath = Path.Combine(baseDir, key.ToString() + ".csv");

                var pointset = store.GetPointSet(key, CancellationToken.None);
                var (nodes, leafs) = GetNodesWithExponent(pointset.Root.Value, exponent);

                // export nodes to file
                nodes.ForEach((subnode, i) =>
                {
                    if ((i % 100) == 0)
                        Report.Line($"processing node # {i}");

                    WriteBox2File(subnode.BoundingBox, subnode, mode, grids, key, outPath);
                });

                // export leafs to file
                var leafsAsBoxes = ArtificialLeafSplit(leafs, exponent, minPointsInBox);
                leafsAsBoxes.ForEach( (lb, i) =>
                {
                    if ((i % 100) == 0)
                        Report.Line($"processing leaf # {i} ({lb.Item1.Count()} boxes)");

                    lb.Item1.ForEach(box =>
                        WriteBox2File(box, lb.Item2, mode, grids, key, outPath));
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
        /// Returns all nodes from given tree with given exponent (nodes,leafs).
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
            string key, int maxChunkSize = 1024 * 1024, int splitLimit = 8192)
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
               .WithOctreeSplitLimit(splitLimit)
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
