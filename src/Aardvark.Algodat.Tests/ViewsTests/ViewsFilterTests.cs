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
    public class ViewsFilterTests
    {
        private static readonly Random r = new Random();
        private static V3d RandomPosition() => new V3d(r.NextDouble(), r.NextDouble(), r.NextDouble());
        private static V3d[] RandomPositions(int n) => new V3d[n].SetByIndex(_ => RandomPosition());

        [Test]
        public void FilterInsideBox3d()
        {
            var storage = PointCloud.CreateInMemoryStore();
            var ps0 = RandomPositions(100);

            var a = new PointCloudNode(storage,
                id: "a",
                cell: new Cell(ps0),
                boundingBoxExact: new Box3d(ps0),
                pointCountTree: ps0.Length,
                subnodes: null,
                (PointCloudAttribute.PositionsAbsolute, "a.positions", ps0)
                );

            var f = new FilteredNode(a, new FilterInsideBox3d(new Box3d(new V3d(0, 0, 0), new V3d(1, 1, 0.5))));
            var ps1 = f.GetPositionsAbsolute();
            Assert.IsTrue(ps1.Length < 100);
        }
    }
}
