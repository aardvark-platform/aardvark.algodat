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

            var keys = new string[]
            {
                "bildstein_5_labelled_filtered",
                "neugasse_station1_filtered",
                "bildstein_station1_filtered"
            };

            var pointset = store.GetPointSet(keys[0], CancellationToken.None);
            var subnodes = pointset.Root.Value.Subnodes.
                Where(sn => sn != null).Select(sn => sn.Value);

            var modes = new string[] { "binary", "count", "fraction" };
            var sizes = new double[] { 16.0, 24.0, 32.0 };

            var mode = "count";
            var size = 16.0;

            subnodes.ForEach( (subnode,j) =>
            {
                Report.Warn($"processing subnode # {j}");

                var min = subnode.BoundingBoxExact.Min;
                var max = subnode.BoundingBoxExact.Max;

                var boxSize = 2.0;
                var boxes = new List<Box3d>();

                // subdivide the subnode into 2x2x2 sized boxes
                for (double x = min.X; x < max.X; x += boxSize)
                    for (double y = min.Y; y < max.Y; y += boxSize)
                        for (double z = min.Z; z < max.Z; z += boxSize)
                            boxes.Add(new Box3d(x, y, z, x + boxSize, y + boxSize, z + boxSize));

                // only use boxes with any points inside
                var boxesWithPoints = boxes.Where(box => subnode.CountPointsInsideBox(box) > 0);

                // convert the boxes into a grid with occupancy values
                var gridSize = boxSize / size;
                var data = boxesWithPoints.Select((box, i) =>
                {
                   Report.Line($"processing box #{i}");

                   var bmin = box.Min;
                   var bmax = box.Max;

                   var cl = subnode.QueryPointsInsideBox(box).SelectMany(c => c.Classifications);
                   var truth = cl.GroupBy(c => c).OrderByDescending(g => g.ToArray().Length).First().Key;

                   var occupancies = new List<string>();

                   for (double y = bmin.Y; y < bmax.Y; y += gridSize)
                       for (double z = bmax.Z; z > bmin.Z; z -= gridSize)
                           for (double x = bmin.X; x < bmax.X; x += gridSize)
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
                   return ($"{truth};{occupancies.Join(";")}");
                }).ToArray();
                File.AppendAllLines(Path.Combine(pData, "data_" + mode + "_gs=" + size + "csv"), data);
            });
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
