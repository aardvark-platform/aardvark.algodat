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
using Aardvark.Data;
using Aardvark.Geometry.Points;
using NUnit.Framework;
using NUnit.Framework.Legacy;
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

            ClassicAssert.IsTrue(a.Id == aId);
            ClassicAssert.IsTrue(a.Cell == Cell.Unit);
            ClassicAssert.IsTrue(a.BoundingBoxExactGlobal == Box3d.Unit);
        }

        [Test]
        public void Create_Positions()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var ps0Global = new V3d[100].SetByIndex(_ => RandomPosition());
            var bbGlobal = new Box3d(ps0Global);
            var ps0 = ps0Global.Map(x => new V3f(x - new V3d(0.5)));
            var ps0Id = Guid.NewGuid();
            storage.Add(ps0Id, ps0);
            var bbLocal = new Box3f(ps0);
            var kd0 = ps0.BuildKdTree();
            var kd0Id = Guid.NewGuid();
            storage.Add(kd0Id, kd0.Data);

            var aId = Guid.NewGuid();
            var a = new PointSetNode(storage, writeToStore: true,
                (Durable.Octree.NodeId, aId),
                (Durable.Octree.Cell, new Cell(ps0Global)),
                (Durable.Octree.BoundingBoxExactLocal, bbLocal),
                (Durable.Octree.BoundingBoxExactGlobal, bbGlobal),
                (Durable.Octree.PointCountTreeLeafs, ps0Global.LongLength),
                (Durable.Octree.PositionsLocal3fReference, ps0Id),
                (Durable.Octree.PointRkdTreeFDataReference, kd0Id)
                );

            ClassicAssert.IsTrue(a.Id == aId);
            ClassicAssert.IsTrue(a.Cell == new Cell(ps0Global));
            ClassicAssert.IsTrue(a.BoundingBoxExactLocal == bbLocal);
            ClassicAssert.IsTrue(a.BoundingBoxExactGlobal == bbGlobal);
            ClassicAssert.IsTrue(a.HasPositions);
        }
    }
}
