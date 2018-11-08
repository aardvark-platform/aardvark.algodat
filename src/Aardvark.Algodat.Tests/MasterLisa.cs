using Aardvark.Base;
using Aardvark.Geometry.Points;

using System;
using System.IO;
using System.Globalization;
using Aardvark.Data.Points;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace Aardvark.Geometry.Tests
{
    static class MasterLisa
    {
        public static void Perform()
        {
            // import point-cloud with labels from different file
            var labels = File.ReadAllLines(@"C:\Users\kellner\Desktop\Diplomarbeit\Semantic3d\sem8_labels_training\bildstein_station5_xyz_intensity_rgb.labels")
                .Map(l => (byte)int.Parse(l, CultureInfo.InvariantCulture));
           
            var data = File.ReadAllLines(@"C:\Users\kellner\Desktop\Diplomarbeit\Semantic3d\bildstein_station5_xyz_intensity_rgb.txt");

            if (labels.Length != data.Length)
                throw new ArgumentException("Files don't have same amount of rows.");

            var positions = new V3d[data.Length];
            var colors = new C4b[data.Length];

            Report.BeginTimed("reading files");
            data.ForEach( (l,i) => 
            {
                var s = l.Split(' ');

                var pos = new V3d(double.Parse(s[0],CultureInfo.InvariantCulture),
                    double.Parse(s[1], CultureInfo.InvariantCulture),
                    double.Parse(s[2], CultureInfo.InvariantCulture));

                var col = new C4b((byte)int.Parse(s[4], CultureInfo.InvariantCulture),
                    (byte)int.Parse(s[5], CultureInfo.InvariantCulture),
                    (byte)int.Parse(s[6], CultureInfo.InvariantCulture), 
                    (byte)1);

                positions[i] = pos;
                colors[i] = col;
            });
            Report.End();

            var bb = new Box3d(positions);

            var chunk = new Chunk(positions,colors,null,null,labels,bb);

            // add point-cloud to store
            var store = PointCloud.OpenStore(@"C:\Users\kellner\Desktop\Diplomarbeit\Store");
            
            var key = "bildstein_5_labelled";
            var config = ImportConfig.Default
                .WithStorage(store)
                .WithKey(key)
                .WithVerbose(true);

            Report.BeginTimed("importing data");
            var pointset = PointCloud.Chunks(chunk, config);
            store.Add(key, pointset, CancellationToken.None);
            Report.End();

            
        }
    }
}
