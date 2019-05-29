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
using Aardvark.Data;
using Aardvark.Geometry.Points;
using NUnit.Framework;
using System;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class ViewsFilterTests
    {
        private static readonly Random r = new Random();
        private static V3f RandomPosition() => new V3f(r.NextDouble(), r.NextDouble(), r.NextDouble());
        private static V3f[] RandomPositions(int n) => new V3f[n].SetByIndex(_ => RandomPosition());

        #region FilterInsideBox3d

        [Test]
        public void FilterInsideBox3d_AllInside()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var ps0 = RandomPositions(100);
            var ps0Id = Guid.NewGuid();
            storage.Add(ps0Id, ps0);
            var cell = new Cell(ps0);
            var bbLocal = new Box3f(ps0) - (V3f)cell.GetCenter();
            var kd0 = ps0.BuildKdTree();
            var kd0Id = Guid.NewGuid();
            storage.Add(kd0Id, kd0.Data);
            var a = new PointSetNode(storage, writeToStore: true,
                (Durable.Octree.NodeId, Guid.NewGuid()),
                (Durable.Octree.Cell, cell),
                (Durable.Octree.BoundingBoxExactLocal, bbLocal),
                (Durable.Octree.PointCountTreeLeafs, ps0.LongLength),
                (Durable.Octree.PositionsLocal3fReference, ps0Id),
                (Durable.Octree.PointRkdTreeFDataReference, kd0Id)
                );

            var f = new FilteredNode(a, new FilterInsideBox3d((Box3d)new Box3f(ps0)));
            Assert.IsTrue(f.HasPositions);
            var ps1 = f.PositionsAbsolute;
            Assert.IsTrue(ps1.Length == 100);
        }

        [Test]
        public void FilterInsideBox3d_AllOutside()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var ps0 = RandomPositions(100);
            var ps0Id = Guid.NewGuid();
            storage.Add(ps0Id, ps0);
            var cell = new Cell(ps0);
            var bbLocal = new Box3f(ps0) - (V3f)cell.GetCenter();
            var kd0 = ps0.BuildKdTree();
            var kd0Id = Guid.NewGuid();
            storage.Add(kd0Id, kd0.Data);
            var a = new PointSetNode(storage, writeToStore: true,
                (Durable.Octree.NodeId, Guid.NewGuid()),
                (Durable.Octree.Cell, cell),
                (Durable.Octree.BoundingBoxExactLocal, bbLocal),
                (Durable.Octree.PointCountTreeLeafs, ps0.LongLength),
                (Durable.Octree.PositionsLocal3fReference, ps0Id),
                (Durable.Octree.PointRkdTreeFDataReference, kd0Id)
                );

            var f = new FilteredNode(a, new FilterInsideBox3d((Box3d)new Box3f(ps0) + V3d.IOO));
            var ps1 = f.PositionsAbsolute;
            Assert.IsTrue(ps1 == null);
        }

        [Test]
        public void FilterInsideBox3d_Partial()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var ps0 = RandomPositions(100);
            var ps0Id = Guid.NewGuid();
            storage.Add(ps0Id, ps0);
            var cell = new Cell(ps0);
            var bbLocal = new Box3f(ps0) - (V3f)cell.GetCenter();
            var kd0 = ps0.BuildKdTree();
            var kd0Id = Guid.NewGuid();
            storage.Add(kd0Id, kd0.Data);
            var a = new PointSetNode(storage, writeToStore: true,
                (Durable.Octree.NodeId, Guid.NewGuid()),
                (Durable.Octree.Cell, cell),
                (Durable.Octree.BoundingBoxExactLocal, bbLocal),
                (Durable.Octree.PointCountTreeLeafs, ps0.LongLength),
                (Durable.Octree.PositionsLocal3fReference, ps0Id),
                (Durable.Octree.PointRkdTreeFDataReference, kd0Id)
                );

            var f = new FilteredNode(a, new FilterInsideBox3d(new Box3d(new V3d(0, 0, 0), new V3d(1, 1, 0.5))));
            Assert.IsTrue(f.HasPositions);
            var ps1 = f.PositionsAbsolute;
            Assert.IsTrue(ps1.Length < 100);
        }

        #endregion

        #region FilterInsideBox3d

        [Test]
        public void FilterOutsideBox3d_AllInside()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var ps0 = RandomPositions(100);
            var ps0Id = Guid.NewGuid();
            storage.Add(ps0Id, ps0);
            var cell = new Cell(ps0);
            var bbLocal = new Box3f(ps0) - (V3f)cell.GetCenter();
            var kd0 = ps0.BuildKdTree();
            var kd0Id = Guid.NewGuid();
            storage.Add(kd0Id, kd0.Data);
            var a = new PointSetNode(storage, writeToStore: true,
                (Durable.Octree.NodeId, Guid.NewGuid()),
                (Durable.Octree.Cell, cell),
                (Durable.Octree.BoundingBoxExactLocal, bbLocal),
                (Durable.Octree.PointCountTreeLeafs, ps0.LongLength),
                (Durable.Octree.PositionsLocal3fReference, ps0Id),
                (Durable.Octree.PointRkdTreeFDataReference, kd0Id)
                );

            var f = new FilteredNode(a, new FilterOutsideBox3d((Box3d)new Box3f(ps0) + V3d.IOO));
            Assert.IsTrue(f.HasPositions);
            var ps1 = f.PositionsAbsolute;
            Assert.IsTrue(ps1.Length == 100);
        }

        [Test]
        public void FilterOutsideBox3d_AllOutside()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var ps0 = RandomPositions(100);
            var ps0Id = Guid.NewGuid();
            storage.Add(ps0Id, ps0);
            var cell = new Cell(ps0);
            var bbLocal = new Box3f(ps0) - (V3f)cell.GetCenter();
            var kd0 = ps0.BuildKdTree();
            var kd0Id = Guid.NewGuid();
            storage.Add(kd0Id, kd0.Data);
            var a = new PointSetNode(storage, writeToStore: true,
                (Durable.Octree.NodeId, Guid.NewGuid()),
                (Durable.Octree.Cell, cell),
                (Durable.Octree.BoundingBoxExactLocal, bbLocal),
                (Durable.Octree.PointCountTreeLeafs, ps0.LongLength),
                (Durable.Octree.PositionsLocal3fReference, ps0Id),
                (Durable.Octree.PointRkdTreeFDataReference, kd0Id)
                );

            var f = new FilteredNode(a, new FilterOutsideBox3d((Box3d)new Box3f(ps0)));
            Assert.IsTrue(!f.HasPositions);
            var ps1 = f.PositionsAbsolute;
            Assert.IsTrue(ps1 == null);
        }

        [Test]
        public void FilterOutsideBox3d_Partial()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var ps0 = RandomPositions(100);
            var ps0Id = Guid.NewGuid();
            storage.Add(ps0Id, ps0);
            var cell = new Cell(ps0);
            var bbLocal = new Box3f(ps0) - (V3f)cell.GetCenter();
            var kd0 = ps0.BuildKdTree();
            var kd0Id = Guid.NewGuid();
            storage.Add(kd0Id, kd0.Data);
            var a = new PointSetNode(storage, writeToStore: true,
                (Durable.Octree.NodeId, Guid.NewGuid()),
                (Durable.Octree.Cell, cell),
                (Durable.Octree.BoundingBoxExactLocal, bbLocal),
                (Durable.Octree.PointCountTreeLeafs, ps0.LongLength),
                (Durable.Octree.PositionsLocal3fReference, ps0Id),
                (Durable.Octree.PointRkdTreeFDataReference, kd0Id)
                );

            var f = new FilteredNode(a, new FilterOutsideBox3d(new Box3d(new V3d(0, 0, 0), new V3d(1, 1, 0.5))));
            Assert.IsTrue(f.HasPositions);
            var ps1 = f.PositionsAbsolute;
            Assert.IsTrue(ps1.Length < 100);
        }

        #endregion

        #region FilterIntensity

        [Test]
        public void FilterIntensity_AllInside()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var ps0 = RandomPositions(10);
            var ps0Id = Guid.NewGuid();
            storage.Add(ps0Id, ps0);
            var cell = new Cell(ps0);
            var bbLocal = new Box3f(ps0) - (V3f)cell.GetCenter();
            var kd0 = ps0.BuildKdTree();
            var kd0Id = Guid.NewGuid();
            storage.Add(kd0Id, kd0.Data);
            var js0 = new[] { -4, -3, -2, -1, 0, 1, 2, 3, 4, 5 };
            var js0Id = Guid.NewGuid();
            storage.Add(js0Id, js0);

            var a = new PointSetNode(storage, writeToStore: true,
                (Durable.Octree.NodeId, Guid.NewGuid()),
                (Durable.Octree.Cell, cell),
                (Durable.Octree.BoundingBoxExactLocal, bbLocal),
                (Durable.Octree.PointCountTreeLeafs, ps0.LongLength),
                (Durable.Octree.PositionsLocal3fReference, ps0Id),
                (Durable.Octree.PointRkdTreeFDataReference, kd0Id),
                (Durable.Octree.Intensities1iReference, js0Id)
                );

            var f = new FilteredNode(a, new FilterIntensity(new Range1i(-100, +100)));
            Assert.IsTrue(f.HasIntensities);
            var js1 = f.Intensities.Value;
            Assert.IsTrue(js1.Length == 10);
        }
        
        [Test]
        public void FilterIntensity_AllOutside()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var ps0 = RandomPositions(10);
            var ps0Id = Guid.NewGuid();
            storage.Add(ps0Id, ps0);
            var cell = new Cell(ps0);
            var bbLocal = new Box3f(ps0) - (V3f)cell.GetCenter();
            var kd0 = ps0.BuildKdTree();
            var kd0Id = Guid.NewGuid();
            storage.Add(kd0Id, kd0.Data);
            var js0 = new[] { -4, -3, -2, -1, 0, 1, 2, 3, 4, 5 };
            var js0Id = Guid.NewGuid();
            storage.Add(js0Id, js0);

            var a = new PointSetNode(storage, writeToStore: true,
                (Durable.Octree.NodeId, Guid.NewGuid()),
                (Durable.Octree.Cell, cell),
                (Durable.Octree.BoundingBoxExactLocal, bbLocal),
                (Durable.Octree.PointCountTreeLeafs, ps0.LongLength),
                (Durable.Octree.PositionsLocal3fReference, ps0Id),
                (Durable.Octree.PointRkdTreeFDataReference, kd0Id),
                (Durable.Octree.Intensities1iReference, js0Id)
                );

            var f = new FilteredNode(a, new FilterIntensity(new Range1i(6, 10000)));
            Assert.IsTrue(!f.HasIntensities);
            var js1 = f.Intensities;
            Assert.IsTrue(js1 == null);
        }

        [Test]
        public void FilterIntensity_Partial()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var ps0 = RandomPositions(10);
            var ps0Id = Guid.NewGuid();
            storage.Add(ps0Id, ps0);
            var cell = new Cell(ps0);
            var bbLocal = new Box3f(ps0) - (V3f)cell.GetCenter();
            var kd0 = ps0.BuildKdTree();
            var kd0Id = Guid.NewGuid();
            storage.Add(kd0Id, kd0.Data);
            var js0 = new[] { -4, -3, -2, -1, 0, 1, 2, 3, 4, 5 };
            var js0Id = Guid.NewGuid();
            storage.Add(js0Id, js0);

            var a = new PointSetNode(storage, writeToStore: true,
                (Durable.Octree.NodeId, Guid.NewGuid()),
                (Durable.Octree.Cell, cell),
                (Durable.Octree.BoundingBoxExactLocal, bbLocal),
                (Durable.Octree.PointCountTreeLeafs, ps0.LongLength),
                (Durable.Octree.PositionsLocal3fReference, ps0Id),
                (Durable.Octree.PointRkdTreeFDataReference, kd0Id),
                (Durable.Octree.Intensities1iReference, js0Id)
                );

            var f = new FilteredNode(a, new FilterIntensity(new Range1i(-2, +2)));
            Assert.IsTrue(f.HasIntensities);
            var js1 = f.Intensities.Value;
            Assert.IsTrue(js1.Length == 5);
        }

        #endregion
    }
}
