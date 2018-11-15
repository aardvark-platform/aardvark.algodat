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
        
        [Test]
        public void Create_Empty()
        {
            var storage = PointCloud.CreateInMemoryStore();

            var a = new PointCloudNode(storage,
                id              : "a",
                cell            : Cell.Unit,
                boundingBoxExact: Box3d.Unit,
                pointCountTree  : 0,
                subnodes        : null
                );

            Assert.IsTrue(a.Id == "a");
            Assert.IsTrue(a.Cell == Cell.Unit);
            Assert.IsTrue(a.BoundingBoxExact == Box3d.Unit);
        }

       
    }
}
