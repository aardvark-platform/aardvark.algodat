/*
    Copyright (C) 2006-2023. Aardvark Platform Team. http://github.com/aardvark-platform.
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.
    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/
using Aardvark.Base;
using Aardvark.Data.Points;
using Aardvark.Geometry.Points;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class LodTests
    {
        [Test]
        public void LodCreationSetsLodPointCountCell()
        {
            var r = new Random();
            var storage = PointSetTests.CreateStorage();

            var ps = new V3d[42000].SetByIndex(_ => new V3d(r.NextDouble(), r.NextDouble(), r.NextDouble()));
            var cs = ps.Map(_ => C4b.White);

            var pointset = PointSet.Create(
                storage, "test", ps.ToList(), cs.ToList(), null, null, null, null, 5000,
                generateLod: false, isTemporaryImportNode: true, ct: default
                );
            pointset.Root.Value.ForEachNode(true, cell =>
            {
                ClassicAssert.IsTrue(cell.IsNotLeaf() || cell.Positions != null);
            });

            var config = ImportConfig.Default
                .WithKey("Test")
                .WithOctreeSplitLimit(1)
                ;
            var lodded = pointset.GenerateLod(config);
            lodded.Root.Value.ForEachNode(true, cell =>
            {
                ClassicAssert.IsTrue(cell.Positions.Value.Length > 0);
            });
        }

        [Test]
        public void LodCreationSetsPartIndices()
        {
            var r = new Random();
            var storage = PointSetTests.CreateStorage();

            var ps = new V3d[42000].SetByIndex(_ => new V3d(r.NextDouble(), r.NextDouble(), r.NextDouble()));
            var cs = ps.Map(_ => C4b.White);

            var pointset = PointSet.Create(
                storage, "test", ps.ToList(), cs.ToList(), null, null, null, partIndices: 42, 5000,
                generateLod: false, isTemporaryImportNode: true, ct: default
                );
            pointset.Root.Value.ForEachNode(true, cell =>
            {
                ClassicAssert.IsTrue(cell.IsNotLeaf() || cell.Positions != null);
            });

            var config = ImportConfig.Default
                .WithKey("Test")
                .WithOctreeSplitLimit(1)
                ;
            var lodded = pointset.GenerateLod(config);
            lodded.Root.Value.ForEachNode(true, cell =>
            {
                ClassicAssert.IsTrue(cell.HasPartIndices);
                ClassicAssert.IsTrue(cell.HasPartIndexRange);
                ClassicAssert.IsTrue(cell.PartIndexRange == new Range1i(42, 42));
            });
        }

        [Test]
        public void LodPositions()
        {
            var r = new Random();
            var storage = PointSetTests.CreateStorage();

            var ps = new V3d[42000].SetByIndex(_ => new V3d(r.NextDouble(), r.NextDouble(), r.NextDouble()));
            var cs = ps.Map(_ => C4b.White);

            var pointset = PointSet.Create(
                storage, "test", ps.ToList(), cs.ToList(), null, null, null, partIndices: 42u, 5000,
                generateLod: true, isTemporaryImportNode: true, default
                );
            pointset.Root.Value.ForEachNode(true, cell =>
            {
                var pointcount = cell.Positions.Value.Length;
                ClassicAssert.IsTrue(pointcount > 0);
            });
        }

        [Test]
        public void LodCreationSetsPointCountCell_FromPts()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Pts.PtsFormat != null);
            var filename = Config.TEST_FILE_NAME_PTS;
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");

            var config = ImportConfig.Default
                .WithStorage(PointSetTests.CreateStorage())
                .WithKey("test")
                .WithOctreeSplitLimit(5000)
                ;
            var pointset = PointCloud.Import(filename, config);

            pointset.Root.Value.ForEachNode(true, cell =>
            {
                ClassicAssert.IsTrue(cell.Positions.Value.Length > 0);
            });
        }

        [Test]
        public void Serialization_FromPts()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Pts.PtsFormat != null);
            var filename = Config.TEST_FILE_NAME_PTS;
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");

            var config = ImportConfig.Default
                .WithStorage(PointSetTests.CreateStorage())
                .WithKey("test")
                .WithOctreeSplitLimit(5000)
                ;
            var pointset = PointCloud.Import(filename, config);

            var json = pointset.ToJson();
            var jsonReloaded = PointSet.Parse(json, config.Storage);

            var xs = new Queue<long>();
            pointset.Root.Value.ForEachNode(true, cell =>
            {
                xs.Enqueue(cell.Positions?.Value.Length ?? 0);
                xs.Enqueue(cell.PointCountTree);
            });
            jsonReloaded.Root.Value.ForEachNode(true, cell =>
            {
                ClassicAssert.IsTrue(xs.Dequeue() == (cell.Positions?.Value.Length ?? 0));
                ClassicAssert.IsTrue(xs.Dequeue() == cell.PointCountTree);
            });
        }

        [Test]
        public void Serialization_FromPts_Really()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Pts.PtsFormat != null);
            var filename = Config.TEST_FILE_NAME_PTS;
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            Console.WriteLine($"filename: {filename}");

            string id = null;

            var dbDiskLocation = Path.Combine(Path.GetTempPath(), "teststore_" + Guid.NewGuid());
            using (var storageA = PointSetTests.CreateDiskStorage(dbDiskLocation))
            {
                var config = ImportConfig.Default
                   .WithStorage(storageA)
                   .WithKey("test")
                   .WithOctreeSplitLimit(5000)
                   ;
                var pointset = PointCloud.Import(filename, config);
                pointset.Root.Value.ForEachNode(true, cell =>
                {
                    var pointcount = cell.Positions?.Value.Length ?? 0;
                    ClassicAssert.IsTrue(pointcount > 0);
                    ClassicAssert.IsTrue(cell.Positions.Value.Length == pointcount);
                });

                id = pointset.Id;
            }

            using (var storageB = PointSetTests.CreateDiskStorage(dbDiskLocation))
            {
                var pointset = storageB.GetPointSet(id);
                pointset.Root.Value.ForEachNode(true, cell =>
                {
                    var pointcount = cell.Positions?.Value.Length ?? 0;
                    ClassicAssert.IsTrue(pointcount > 0);
                    ClassicAssert.IsTrue(cell.Positions.Value.Length == pointcount);
                });
            }

            try
            {
                Directory.Delete(dbDiskLocation, true);
            }
            catch
            {
                File.Delete(dbDiskLocation);
            }
        }

        [Test]
        public void HasCentroid()
        {
            var storage = PointSetTests.CreateStorage();

            var ps = new[]
            {
                new V3d(0.1, 0.1, 0.1),
                new V3d(0.9, 0.1, 0.1),
                new V3d(0.9, 0.9, 0.1),
                new V3d(0.1, 0.9, 0.1),
                new V3d(0.1, 0.1, 0.9),
                new V3d(0.9, 0.1, 0.9),
                new V3d(0.9, 0.9, 0.9),
                new V3d(0.1, 0.9, 0.9),
            };

            var config = ImportConfig.Default.WithStorage(storage).WithRandomKey();
            var chunk = new Chunk(ps);
            if (config.ParseConfig.EnabledProperties.PartIndices) chunk = chunk.WithPartIndices(42u, null);
            var n = PointCloud.Chunks(chunk, config).Root.Value;

            ClassicAssert.IsTrue(n.HasCentroidLocal);
            ClassicAssert.IsTrue(n.HasCentroidLocalStdDev);
            ClassicAssert.IsTrue(n.CentroidLocal.ApproximateEquals(V3f.Zero, 1e-5f));
        }

        [Test]
        public void HasBoundingBoxExact()
        {
            var storage = PointSetTests.CreateStorage();

            var ps = new[]
            {
                new V3d(0.1, 0.1, 0.1),
                new V3d(0.9, 0.1, 0.1),
                new V3d(0.9, 0.9, 0.1),
                new V3d(0.1, 0.9, 0.1),
                new V3d(0.1, 0.1, 0.9),
                new V3d(0.9, 0.1, 0.9),
                new V3d(0.9, 0.9, 0.9),
                new V3d(0.1, 0.9, 0.9),
            };

            var config = ImportConfig.Default.WithStorage(storage).WithRandomKey();
            var chunk = new Chunk(ps);
            if (config.ParseConfig.EnabledProperties.PartIndices) chunk = chunk.WithPartIndices(42u, null);
            var n = PointCloud.Chunks(chunk, config).Root.Value;

            ClassicAssert.IsTrue(n.HasBoundingBoxExactLocal);
            ClassicAssert.IsTrue(n.BoundingBoxExactLocal == new Box3f(new V3f(-0.4f), new V3f(0.4f)));

            ClassicAssert.IsTrue(n.HasBoundingBoxExactGlobal);
            ClassicAssert.IsTrue(n.BoundingBoxExactGlobal.Min.ApproximateEquals(new V3d(0.1), 1e-6));
            ClassicAssert.IsTrue(n.BoundingBoxExactGlobal.Max.ApproximateEquals(new V3d(0.9), 1e-6));

        }

        [Test]
        public void HasTreeDepth()
        {
            var r = new Random();
            var storage = PointSetTests.CreateStorage();

            var ps = new V3d[10].SetByIndex(_ => new V3d(r.NextDouble(), r.NextDouble(), r.NextDouble()));

            var config = ImportConfig.Default.WithStorage(storage).WithRandomKey();
            var chunk = new Chunk(ps);
            if (config.ParseConfig.EnabledProperties.PartIndices) chunk = chunk.WithPartIndices(42u, null);
            var n = PointCloud.Chunks(chunk, config).Root.Value;

            ClassicAssert.IsTrue(n.HasMinTreeDepth);
            ClassicAssert.IsTrue(n.HasMaxTreeDepth);

            ClassicAssert.IsTrue(n.MinTreeDepth == 0);
            ClassicAssert.IsTrue(n.MaxTreeDepth == 0);
        }

        [Test]
        public void HasTreeDepth2()
        {
            var r = new Random();
            var storage = PointSetTests.CreateStorage();

            var ps = new V3d[20000].SetByIndex(_ => new V3d(r.NextDouble(), r.NextDouble(), r.NextDouble()));

            var config = ImportConfig.Default.WithStorage(storage).WithRandomKey();
            var chunk = new Chunk(ps);
            if (config.ParseConfig.EnabledProperties.PartIndices) chunk = chunk.WithPartIndices(42u, null);
            var n = PointCloud.Chunks(chunk, config).Root.Value;

            ClassicAssert.IsTrue(n.HasMinTreeDepth);
            ClassicAssert.IsTrue(n.HasMaxTreeDepth);

            ClassicAssert.IsTrue(n.MinTreeDepth == 1);
            ClassicAssert.IsTrue(n.MaxTreeDepth == 1);
        }

        [Test]
        [Ignore("PointDistance is no longer calculated.")]
        public void HasPointDistance()
        {
            var storage = PointSetTests.CreateStorage();

            var ps = new[]
            {
                new V3d(0.1, 0.1, 0.1),
                new V3d(0.9, 0.1, 0.1),
                new V3d(0.9, 0.9, 0.1),
                new V3d(0.1, 0.9, 0.1),
                new V3d(0.1, 0.1, 0.9),
                new V3d(0.9, 0.1, 0.9),
                new V3d(0.9, 0.9, 0.9),
                new V3d(0.1, 0.9, 0.9),
            };

            var config = ImportConfig.Default.WithStorage(storage).WithRandomKey();
            var chunk = new Chunk(ps);
            if (config.ParseConfig.EnabledProperties.PartIndices) chunk = chunk.WithPartIndices(42u, null);
            var n = PointCloud.Chunks(chunk, config).Root.Value;

            ClassicAssert.IsTrue(n.HasPointDistanceAverage);
            ClassicAssert.IsTrue(n.HasPointDistanceStandardDeviation);

            ClassicAssert.IsTrue(n.PointDistanceAverage.ApproximateEquals(0.8f, 1e-5f));
            ClassicAssert.IsTrue(n.PointDistanceStandardDeviation.ApproximateEquals(0.0f, 1e-5f));
        }
    }
}
