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
using Aardvark.Geometry.Points;
using NUnit.Framework;
using System;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class PointCloudNodeTests
    {
        private static readonly Random r = new Random();
        private static V3d RandomPosition() => new V3d(r.NextDouble(), r.NextDouble(), r.NextDouble());

        [Test]
        public void Create_Empty()
        {
            var storage = PointCloud.CreateInMemoryStore();
            var a = new PointCloudNode(storage, "a", Cell.Unit, Box3d.Unit, 0, null, null);
            Assert.IsTrue(a.Id == "a");
            Assert.IsTrue(a.Cell == Cell.Unit);
            Assert.IsTrue(a.BoundingBoxExact == Box3d.Unit);
        }

        [Test]
        public void Create_Positions()
        {
            var storage = PointCloud.CreateInMemoryStore();
            var ps0 = new V3d[100].SetByIndex(_ => RandomPosition());
            var ps0Id = Guid.NewGuid();
            var cell = new Cell(ps0);
            var c = cell.GetCenter();
            var ps0f = ps0.Map(p => new V3f(p - c));
            storage.Add(ps0Id, ps0f, default);
            var a = new PointCloudNode(storage, "a", cell, new Box3d(ps0), ps0.Length, null, new[]
            {
                (PointCloudAttribute.Positions, ps0Id.ToString(), (object)new PersistentRef<V3f[]>(ps0Id.ToString(), storage.GetV3fArray))
            });
            Assert.IsTrue(a.Id == "a");
            Assert.IsTrue(a.Cell == new Cell(ps0));
            Assert.IsTrue(a.BoundingBoxExact == new Box3d(ps0));
            Assert.IsTrue(a.HasPositions());
            var ps1 = a.GetPositions();
            for (var i = 0; i < ps0.Length; i++)
                Assert.IsTrue(ps0f[i] == ps1.Value[i]);
        }
    }
}
