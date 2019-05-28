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
        private static V3d RandomPosition() => new V3d(r.NextDouble(), r.NextDouble(), r.NextDouble());
        
        [Test]
        public void Create_Empty()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);

            var aId = Guid.NewGuid();

            var a = new PointSetNode(storage, writeToStore: true,
                (Durable.Octree.NodeId, aId),
                (Durable.Octree.Cell, Cell.Unit),
                (Durable.Octree.BoundingBoxExactGlobal, Box3d.Unit),
                (Durable.Octree.PointCountTreeLeafs, 0L)
                );

            Assert.IsTrue(a.Id == aId);
            Assert.IsTrue(a.Cell == Cell.Unit);
            Assert.IsTrue(a.BoundingBoxExactGlobal == Box3d.Unit);
        }

        [Test]
        public void Create_Positions()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var ps0 = new V3d[100].SetByIndex(_ => RandomPosition());
            var ps0Id = Guid.NewGuid();
            var aId = Guid.NewGuid();

            var a = new PointSetNode(storage, writeToStore: true,
                (Durable.Octree.NodeId, aId),
                (Durable.Octree.Cell, new Cell(ps0)),
                (Durable.Octree.BoundingBoxExactGlobal, new Box3d(ps0)),
                (Durable.Octree.PointCountTreeLeafs, ps0.LongLength),
                (Durable.Octree.PositionsGlobal3d, ps0)
                );

            Assert.IsTrue(a.Id == aId);
            Assert.IsTrue(a.Cell == new Cell(ps0));
            Assert.IsTrue(a.BoundingBoxExactGlobal == new Box3d(ps0));
            Assert.IsTrue(a.HasPositions());
        }
    }
}
