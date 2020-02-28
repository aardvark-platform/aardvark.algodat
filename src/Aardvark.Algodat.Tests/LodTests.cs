/*
    Copyright (C) 2006-2019. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

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
                Assert.IsTrue(cell.IsNotLeaf() || cell.Positions != null);
            });

            var config = ImportConfig.Default
                .WithKey("Test")
                .WithOctreeSplitLimit(1)
                ;
            var lodded = pointset.GenerateLod(config);
            lodded.Root.Value.ForEachNode(true, cell =>
            {
                Assert.IsTrue(cell.Positions.Value.Length > 0);
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
                storage, "test", ps.ToList(), cs.ToList(), null, null, null, null, 5000,
                generateLod: true, isTemporaryImportNode: true, default
                );
            pointset.Root.Value.ForEachNode(true, cell =>
            {
                var pointcount = cell.Positions.Value.Length;
                Assert.IsTrue(pointcount > 0);
            });
        }

        [Test]
        public void LodCreationSetsPointCountCell_FromPts()
        {
            Assert.IsTrue(Data.Points.Import.Pts.PtsFormat != null);
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
                Assert.IsTrue(cell.Positions.Value.Length > 0);
            });
        }

        [Test]
        public void Serialization_FromPts()
        {
            Assert.IsTrue(Data.Points.Import.Pts.PtsFormat != null);
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
                Assert.IsTrue(xs.Dequeue() == (cell.Positions?.Value.Length ?? 0));
                Assert.IsTrue(xs.Dequeue() == cell.PointCountTree);
            });
        }

        [Test]
        public void Serialization_FromPts_Really()
        {
            Assert.IsTrue(Data.Points.Import.Pts.PtsFormat != null);
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
                    Assert.IsTrue(pointcount > 0);
                    Assert.IsTrue(cell.Positions.Value.Length == pointcount);
                });

                id = pointset.Id;
            }

            using (var storageB = PointSetTests.CreateDiskStorage(dbDiskLocation))
            {
                var pointset = storageB.GetPointSet(id);
                pointset.Root.Value.ForEachNode(true, cell =>
                {
                    var pointcount = cell.Positions?.Value.Length ?? 0;
                    Assert.IsTrue(pointcount > 0);
                    Assert.IsTrue(cell.Positions.Value.Length == pointcount);
                });
            }

            Directory.Delete(dbDiskLocation, true);
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
            var n = PointCloud.Chunks(new Chunk(ps, null), config).Root.Value;

            Assert.IsTrue(n.HasCentroidLocal);
            Assert.IsTrue(n.HasCentroidLocalStdDev);

            Assert.IsTrue(n.CentroidLocal.ApproxEqual(V3f.Zero, 1e-5f));
            Assert.IsTrue(n.CentroidLocalStdDev.ApproximateEquals(0.0f, 1e-5f));
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
            var n = PointCloud.Chunks(new Chunk(ps, null), config).Root.Value;

            Assert.IsTrue(n.HasBoundingBoxExactLocal);
            Assert.IsTrue(n.BoundingBoxExactLocal == new Box3f(new V3f(-0.4f), new V3f(0.4f)));

            Assert.IsTrue(n.HasBoundingBoxExactGlobal);
            Assert.IsTrue(n.BoundingBoxExactGlobal.Min.ApproxEqual(new V3d(0.1), 1e-6));
            Assert.IsTrue(n.BoundingBoxExactGlobal.Max.ApproxEqual(new V3d(0.9), 1e-6));

        }

        [Test]
        public void HasTreeDepth()
        {
            var r = new Random();
            var storage = PointSetTests.CreateStorage();

            var ps = new V3d[10].SetByIndex(_ => new V3d(r.NextDouble(), r.NextDouble(), r.NextDouble()));

            var config = ImportConfig.Default.WithStorage(storage).WithRandomKey();
            var n = PointCloud.Chunks(new Chunk(ps, null), config).Root.Value;

            Assert.IsTrue(n.HasMinTreeDepth);
            Assert.IsTrue(n.HasMaxTreeDepth);

            Assert.IsTrue(n.MinTreeDepth == 0);
            Assert.IsTrue(n.MaxTreeDepth == 0);
        }

        [Test]
        public void HasTreeDepth2()
        {
            var r = new Random();
            var storage = PointSetTests.CreateStorage();

            var ps = new V3d[20000].SetByIndex(_ => new V3d(r.NextDouble(), r.NextDouble(), r.NextDouble()));

            var config = ImportConfig.Default.WithStorage(storage).WithRandomKey();
            var n = PointCloud.Chunks(new Chunk(ps, null), config).Root.Value;

            Assert.IsTrue(n.HasMinTreeDepth);
            Assert.IsTrue(n.HasMaxTreeDepth);

            Assert.IsTrue(n.MinTreeDepth == 1);
            Assert.IsTrue(n.MaxTreeDepth == 1);
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
            var n = PointCloud.Chunks(new Chunk(ps, null), config).Root.Value;

            Assert.IsTrue(n.HasPointDistanceAverage);
            Assert.IsTrue(n.HasPointDistanceStandardDeviation);

            Assert.IsTrue(n.PointDistanceAverage.ApproximateEquals(0.8f, 1e-5f));
            Assert.IsTrue(n.PointDistanceStandardDeviation.ApproximateEquals(0.0f, 1e-5f));
        }
    }
}
