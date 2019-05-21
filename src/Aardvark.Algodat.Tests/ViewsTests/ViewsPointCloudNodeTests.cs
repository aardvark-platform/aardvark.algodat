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
            var a = new PointCloudNode(storage,
                id              : aId,
                cell            : Cell.Unit,
                boundingBoxExact: Box3d.Unit,
                pointCountTree  : 0,
                subnodes        : null,
                storeOnCreation : true
                );

            Assert.IsTrue(a.Id == aId);
            Assert.IsTrue(a.Cell == Cell.Unit);
            Assert.IsTrue(a.BoundingBoxExact == Box3d.Unit);
        }

        [Test]
        public void Create_Positions()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var ps0 = new V3d[100].SetByIndex(_ => RandomPosition());
            var ps0Id = Guid.NewGuid();
            var aId = Guid.NewGuid();

            var a = new PointCloudNode(storage,
                id              : aId,
                cell            : new Cell(ps0),
                boundingBoxExact: new Box3d(ps0),
                pointCountTree  : ps0.Length,
                subnodes        : null,
                storeOnCreation : true,
                (Durable.Octree.PositionsGlobal3d, ps0Id, ps0)
                );

            Assert.IsTrue(a.Id == aId);
            Assert.IsTrue(a.Cell == new Cell(ps0));
            Assert.IsTrue(a.BoundingBoxExact == new Box3d(ps0));
            Assert.IsTrue(a.HasPositions());
        }

        [Test]
        public void Create_KdTree()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var ps0 = new V3d[100].SetByIndex(_ => RandomPosition()).Map(p => new V3f(p - new V3d(0.5, 0.5, 0.5)));
            var ps0Id = Guid.NewGuid();
            var kd0Id = Guid.NewGuid();
            var bb = (Box3d)new Box3f(ps0);

            var aId = Guid.NewGuid();
            var a = new PointCloudNode(storage,
                id              : aId,
                cell            : new Cell(ps0),
                boundingBoxExact: bb,
                pointCountTree  : ps0.Length,
                subnodes        : null,
                storeOnCreation : true,
                (Durable.Octree.PositionsLocal3fReference, ps0Id, ps0),
                (Durable.Octree.PointRkdTreeFDataReference, kd0Id, ps0)
                );

            Assert.IsTrue(a.Id == aId);
            Assert.IsTrue(a.Cell == new Cell(ps0));
            Assert.IsTrue(a.BoundingBoxExact == bb);
            Assert.IsTrue(a.HasPositions());
        }
    }
}
