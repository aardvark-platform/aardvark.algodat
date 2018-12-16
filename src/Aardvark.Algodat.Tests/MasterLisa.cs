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
            var pData = @"C:\Users\kellner\Desktop\Diplomarbeit\networks";

            //var dirLabels = @"C:\Users\kellner\Desktop\Diplomarbeit\data\Semantic3d\sem8_labels_training";
            //var dirData = @"C:\Users\kellner\Desktop\Diplomarbeit\data\Semantic3d\sem8_data_training";

            //var fn = "untermaederbrunnen_station1_xyz_intensity_rgb";
            //var k = "untermaederbrunnen_station1";

            //Import( // import new pointcloud
            //    Path.Combine(dirData, fn + ".txt"),
            //    Path.Combine(dirLabels, fn + ".labels"),
            //    path2store, k);
            //AddNormals(path2store, k); // add normals
            //FilterPoints(path2store, k, 0); // filter all unlabelled points

            var store = PointCloud.OpenStore(path2store, cache: null);

            // -----------------------------------

            //FilterPoints(path2store, id, 0); // TODO: fix it!

            var keys = new string[]
            {
                "bildstein_5_labelled_filtered",
                "neugasse_station1_filtered",
                "bildstein_station1_filtered"
            };

            keys.ForEach(key =>
            {
                Report.Warn($"processing pointcloud: {key}");

                var pointset = store.GetPointSet(key);
                //var subnodes = pointset.Root.Value.Subnodes.
                //    Where(sn => sn != null).Select(sn => sn.Value);

                var subnodes = GetNodesWithExponent(pointset.Root.Value, -3);

                //var modes = new string[] { "binary", "count", "fraction" };
                //var sizes = new double[] { 16.0, 24.0, 32.0 };

                var mode = "binary";
                var size = 8.0;

                subnodes.ForEach((subnode, j) =>
                {
                    if ((j % 100) == 0)
                        Report.Line($"processing subnode # {j}");

                    // converts the boxes into a grid with occupancy values
                    var box = subnode.BoundingBox;

                    var min = box.Min;
                    var max = box.Max;

                    var boxSize = Math.Abs(max.X - min.X);
                    var gridSize = boxSize / size;

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
                    var data = ($"{truth};{occupancies.Join(";")}\n");
                    File.AppendAllText(Path.Combine(pData, "data_" + mode + "_gs=" + size + "-all.csv"), data);
                });
            });
        }

        /// <summary>
        /// PointCloudNode attributes for machine learning.
        /// </summary>
        private static class PointCloudAttributes4ML
        {
            public const string Predictions = "Predictions";
        }

        /// <summary>
        /// Extension functions for ML-attributes
        /// </summary>
        private static class IPointCloudNodeExtensions4ML
        {
            private static bool Has(IPointCloudNode n, string attributeName)
            {
                switch (n.FilterState)
                {
                    case FilterState.FullyOutside:
                        return false;
                    case FilterState.FullyInside:
                    case FilterState.Partial:
                        return n.TryGetPropertyKey(attributeName, out string _);
                    default:
                        throw new InvalidOperationException($"Unknown FilterState {n.FilterState}.");
                }
            }

            // predictions
            public static bool HasPredictions(IPointCloudNode self) =>
                Has(self, PointCloudAttributes4ML.Predictions);

            public static PersistentRef<byte[]> GetPredictions(IPointCloudNode self)
            {
                var key = ComputeKey4Predictions(self);
                var predictions = self.Storage.GetByteArray(key);

                return new PersistentRef<byte[]>(key, self.Storage.GetByteArray, self.Storage.TryGetByteArray);
            }

            public static void AddPredictions(IPointCloudNode self, byte[] predictions) =>
                self.Storage.Add(ComputeKey4Predictions(self), predictions);

            private static string ComputeKey4Predictions(IPointCloudNode self) =>
                "predictions_123";
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
            var store = PointCloud.OpenStore(path2store, cache: default);
            var pointset = store.GetPointSet(key);

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

            var store = PointCloud.OpenStore(path2store, cache: default);

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
            var store = PointCloud.OpenStore(path2store, cache: default);
            var pointset = store.GetPointSet(key);
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
