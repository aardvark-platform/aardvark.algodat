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
    public class ViewsPointCloudNodeTests
    {
        private static readonly Random r = new Random();
        private static V3f RandomPosition() => new V3f(r.NextDouble(), r.NextDouble(), r.NextDouble());
        
        [Test]
        public void Create_Empty()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var ps0 = new V3f[0];
            var ps0Id = Guid.NewGuid();
            storage.Add(ps0Id, ps0);

            var aId = Guid.NewGuid();

            var a = new PointSetNode(storage, writeToStore: true,
                (Durable.Octree.NodeId, aId),
                (Durable.Octree.Cell, Cell.Unit),
                (Durable.Octree.BoundingBoxExactGlobal, Box3d.Unit),
                (Durable.Octree.PointCountTreeLeafs, 0L),
                (Durable.Octree.PositionsLocal3fReference, ps0Id),
                (Durable.Octree.PointRkdTreeFDataReference, Guid.Empty)
                );

            Assert.IsTrue(a.Id == aId);
            Assert.IsTrue(a.Cell == Cell.Unit);
            Assert.IsTrue(a.BoundingBoxExactGlobal == Box3d.Unit);
        }

        [Test]
        public void Create_Positions()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var ps0 = new V3f[100].SetByIndex(_ => RandomPosition());
            var ps0Id = Guid.NewGuid();
            storage.Add(ps0Id, ps0);
            var bbLocal = new Box3f(ps0);
            var kd0 = ps0.BuildKdTree();
            var kd0Id = Guid.NewGuid();
            storage.Add(kd0Id, kd0.Data);

            var aId = Guid.NewGuid();
            var a = new PointSetNode(storage, writeToStore: true,
                (Durable.Octree.NodeId, aId),
                (Durable.Octree.Cell, new Cell(ps0)),
                (Durable.Octree.BoundingBoxExactLocal, bbLocal),
                (Durable.Octree.PointCountTreeLeafs, ps0.LongLength),
                (Durable.Octree.PositionsLocal3fReference, ps0Id),
                (Durable.Octree.PointRkdTreeFDataReference, kd0Id)
                );

            Assert.IsTrue(a.Id == aId);
            Assert.IsTrue(a.Cell == new Cell(ps0));
            Assert.IsTrue(a.BoundingBoxExactLocal == bbLocal);
            Assert.IsTrue(a.BoundingBoxExactGlobal == ((Box3d)bbLocal) + a.Cell.GetCenter());
            Assert.IsTrue(a.HasPositions);
        }
    }
}
