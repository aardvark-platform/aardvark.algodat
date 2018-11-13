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

            var dirLabels = @"C:\Users\kellner\Desktop\Diplomarbeit\Semantic3d\sem8_labels_training";
            var dirData = @"C:\Users\kellner\Desktop\Diplomarbeit\Semantic3d\sem8_data_training";

            var key = "sg27_station2";
            var fn = "sg27_station2_intensity_rgb";

            Report.Line("importing pointcloud");
            Import(
                Path.Combine(dirData, fn+".txt"),
                Path.Combine(dirLabels, fn+".labels"),
                path2store, key);

            //Report.Line("estimating normals");
            //AddNormals(path2store, key);
        }

        /// <summary>
        /// Filters pointcloud.
        /// </summary>
        private static void FilterPoints(string path2store, string key)
        {
            var store = PointCloud.OpenStore(path2store);
            var pointset = store.GetPointSet(key, CancellationToken.None);

            var chunks = pointset.QueryAllPoints().ToArray();

            var label2filter = 0;

            var filteredChunks = chunks.Map(chunk => 
            {
                var indices = new List<int>();
                chunk.Classifications.ForEach((c, i) => 
                {
                    if ((int)c == label2filter)
                        indices.Add(i);
                });

                var pos = new List<V3d>();
                var col = new List<C4b>();
                var normals = new List<V3f>();
                var intensities = new List<int>();
                var classifications = new List<byte>();

                indices.ForEach(idx => 
                {
                    if (chunk.HasPositions)
                        pos.Add(chunk.Positions[idx]);

                    if (chunk.HasColors)
                        col.Add(chunk.Colors[idx]);

                    if (chunk.HasNormals)
                        normals.Add(chunk.Normals[idx]);

                    if (chunk.HasIntensities)
                        intensities.Add(chunk.Intensities[idx]);

                    if (chunk.HasClassifications)
                        classifications.Add(chunk.Classifications[idx]);
                });

                var bb = new Box3d(pos);
                // TODO: aufpassen auf werte die vorher null waren!!!!
                return new Chunk(pos, col, normals, intensities, classifications, bb); 
            });
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

            var chunkedData = data.Chunk(maxChunkSize).ToArray();
            var chunkedLabels = labels.Chunk(maxChunkSize).ToArray();

            var amountLabels = chunkedData.Map(c => c.Length).Sum();
            var amountData = chunkedLabels.Map(c => c.Length).Sum();

            if (amountLabels != amountData)
                throw new ArgumentException("Files don't have same amount of rows.");

            var positions = new IList<V3d>[chunkedData.Length];
            var colors = new IList<C4b>[chunkedData.Length];

            // parsing files
            chunkedData.ForEach((ch, i) =>
            {
                var chunkPos = new V3d[ch.Length];
                var chunkCol = new C4b[ch.Length];

                ch.ForEach( (l,j) => 
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

                positions[i] = chunkPos;
                colors[i] = chunkCol;
            });
            
            var chunks = positions.Map((p, i) =>
            {
                var bbChunk = new Box3d(p);
                return new Chunk(p, colors[i], null, null, chunkedLabels[i], bbChunk);
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
