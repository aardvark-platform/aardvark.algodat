/*
    Copyright (C) 2006-2018. Aardvark Platform Team. http://github.com/aardvark-platform.
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

            var pointset = PointSet.Create(storage, "test", ps.ToList(), cs.ToList(), null, null, 5000, false, CancellationToken.None);
            pointset.Root.Value.ForEachNode(true, cell =>
            {
                Assert.IsTrue(cell.LodPointCount == 0);
            });

            var config = ImportConfig.Default
                .WithKey("Test")
                .WithOctreeSplitLimit(1)
                ;
            var lodded = pointset.GenerateLod(config);
            lodded.Root.Value.ForEachNode(true, cell =>
            {
                Assert.IsTrue(cell.LodPointCount > 0);
            });
        }

        [Test]
        public void DisablingLodCreationWorks()
        {
            var r = new Random();
            var storage = PointSetTests.CreateStorage();

            var ps = new V3d[42000].SetByIndex(_ => new V3d(r.NextDouble(), r.NextDouble(), r.NextDouble()));
            var cs = ps.Map(_ => C4b.White);

            var pointset = PointSet.Create(storage, "test", ps.ToList(), cs.ToList(), null, null, 5000, false, CancellationToken.None);
            pointset.Root.Value.ForEachNode(true, cell =>
            {
                Assert.IsTrue(cell.LodPointCount == 0);
            });

            var config = ImportConfig.Default
                .WithKey("Test")
                .WithOctreeSplitLimit(1)
                ;
            var lodded = pointset.GenerateLod(config);
            lodded.Root.Value.ForEachNode(true, cell =>
            {
                Assert.IsTrue(cell.LodPointCount == 0);
                Assert.IsTrue(cell.HasLodPositions == false);
            });
        }

        [Test]
        public void LodPositions()
        {
            var r = new Random();
            var storage = PointSetTests.CreateStorage();

            var ps = new V3d[42000].SetByIndex(_ => new V3d(r.NextDouble(), r.NextDouble(), r.NextDouble()));
            var cs = ps.Map(_ => C4b.White);

            var pointset = PointSet.Create(storage, "test", ps.ToList(), cs.ToList(), null, null, 5000, false, CancellationToken.None);
            pointset.Root.Value.ForEachNode(true, cell =>
            {
                if (cell.IsLeaf)
                {
                    Assert.IsTrue(cell.PointCount > 0);
                    Assert.IsTrue(cell.Positions.Value.Length == cell.PointCount);
                    Assert.IsTrue(cell.LodPositions == null);
                    Assert.IsTrue(cell.LodPointCount == 0);
                }
                else
                {
                    Assert.IsTrue(cell.PointCount == 0);
                    Assert.IsTrue(cell.Positions == null);
                    Assert.IsTrue(cell.LodPositions == null);
                    Assert.IsTrue(cell.LodPointCount == 0);
                }
            });

            var config = ImportConfig.Default
                .WithKey("lod")
                .WithOctreeSplitLimit(1)
                ;
            var lodded = pointset.GenerateLod(config);
            lodded.Root.Value.ForEachNode(true, cell =>
            {
                if (cell.IsLeaf)
                {
                    Assert.IsTrue(cell.PointCount > 0);
                    Assert.IsTrue(cell.Positions.Value.Length == cell.PointCount);
                    Assert.IsTrue(cell.LodPositions.Value.Length == cell.PointCount);
                    Assert.IsTrue(cell.LodPointCount > 0);
                }
                else
                {
                    Assert.IsTrue(cell.PointCount == 0);
                    Assert.IsTrue(cell.Positions == null);
                    Assert.IsTrue(cell.LodPositions.Value.Length > 0);
                    Assert.IsTrue(cell.LodPointCount > 0);
                }
            });
        }

        [Test]
        public void Serialization()
        {
            var r = new Random();
            var storage = PointSetTests.CreateStorage();

            var ps = new V3d[42000].SetByIndex(_ => new V3d(r.NextDouble(), r.NextDouble(), r.NextDouble()));
            var cs = ps.Map(_ => C4b.White);

            var pointset = PointSet.Create(storage, "test", ps.ToList(), cs.ToList(), null, null, 5000, false, CancellationToken.None);

            var config = ImportConfig.Default
                  .WithKey("lod")
                  .WithOctreeSplitLimit(1)
                  ;
            var lodded = pointset.GenerateLod(config);
            
            var json = lodded.ToJson();
            var relodded = PointSet.Parse(json, storage);

            var xs = new Queue<long>();
            lodded.Root.Value.ForEachNode(true, cell =>
            {
                xs.Enqueue(cell.PointCount);
                xs.Enqueue(cell.PointCountTree);
            });
            relodded.Root.Value.ForEachNode(true, cell =>
            {
                Assert.IsTrue(xs.Dequeue() == cell.PointCount);
                Assert.IsTrue(xs.Dequeue() == cell.PointCountTree);
            });
        }

        [Test]
        public void LodCreationSetsPointCountCell_FromPts()
        {
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
                Assert.IsTrue(cell.LodPointCount > 0);
            });
        }

        [Test]
        public void Serialization_FromPts()
        {
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
                xs.Enqueue(cell.PointCount);
                xs.Enqueue(cell.PointCountTree);
            });
            jsonReloaded.Root.Value.ForEachNode(true, cell =>
            {
                Assert.IsTrue(xs.Dequeue() == cell.PointCount);
                Assert.IsTrue(xs.Dequeue() == cell.PointCountTree);
            });
        }

        [Test]
        public void Serialization_FromPts_Really()
        {
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
                    if (cell.IsLeaf)
                    {
                        Assert.IsTrue(cell.PointCount > 0);
                        Assert.IsTrue(cell.Positions.Value.Length == cell.PointCount);
                        Assert.IsTrue(cell.LodPositions.Value.Length == cell.PointCount);
                    }
                    else
                    {
                        Assert.IsTrue(cell.PointCount == 0);
                        Assert.IsTrue(cell.Positions == null);
                        Assert.IsTrue(cell.LodPositions.Value.Length > 0);
                    }
                });

                id = pointset.Id;
            }

            using (var storageB = PointSetTests.CreateDiskStorage(dbDiskLocation))
            {
                var pointset = storageB.GetPointSet(id, CancellationToken.None);
                pointset.Root.Value.ForEachNode(true, cell =>
                {
                    if (cell.IsLeaf)
                    {
                        Assert.IsTrue(cell.PointCount > 0);
                        Assert.IsTrue(cell.Positions.Value.Length == cell.PointCount);
                        Assert.IsTrue(cell.LodPositions.Value.Length == cell.PointCount);
                    }
                    else
                    {
                        Assert.IsTrue(cell.PointCount == 0);
                        Assert.IsTrue(cell.Positions == null);
                        Assert.IsTrue(cell.LodPositions.Value.Length > 0);
                    }
                });
            }

            Directory.Delete(dbDiskLocation, true);
        }
    }
}
