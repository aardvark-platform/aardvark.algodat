/*
    Copyright (C) 2017. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Aardvark.Base;
using Aardvark.Geometry.Points;
using NUnit.Framework;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class DeleteTests
    {
        private static PointSet CreateRandomPointsInUnitCube(int n, int splitLimit)
        {
            var r = new Random();
            var ps = new V3d[n];
            for (var i = 0; i < n; i++) ps[i] = new V3d(r.NextDouble(), r.NextDouble(), r.NextDouble());
            var config = new ImportConfig
            {
                Storage = PointCloud.CreateInMemoryStore(),
                Key = "test",
                OctreeSplitLimit = splitLimit
            };
            return PointCloud.Chunks(new Chunk(ps, null), config);
        }

        private static PointSet CreateRegularPointsInUnitCube(int n, int splitLimit)
        {
            var ps = new List<V3d>();
            var step = 1.0 / n;
            var start = step * 0.5;
            for (var x = start; x < 1.0; x += step)
                for (var y = start; y < 1.0; y += step)
                    for (var z = start; z < 1.0; z += step)
                        ps.Add(new V3d(x, y, z));
            var config = new ImportConfig
            {
                Storage = PointCloud.CreateInMemoryStore(),
                Key = "test",
                OctreeSplitLimit = splitLimit
            };
            return PointCloud.Chunks(new Chunk(ps, null), config);
        }

        [Test]
        public void CanDeletePoints()
        {
            var q = new Box3d(new V3d(0.3), new V3d(0.7));

            var a = CreateRegularPointsInUnitCube(10, 1);
            Assert.IsTrue(a.QueryAllPoints().SelectMany(chunk => chunk.Positions).Any(p => q.Contains(p)));

            var b = a.Delete(n => q.Contains(n.BoundingBox), n => !(q.Contains(n.BoundingBox) || q.Intersects(n.BoundingBox)), p => q.Contains(p), CancellationToken.None);

            Assert.IsTrue(a.PointCount > b.PointCount);

            Assert.IsTrue(!b.QueryAllPoints().SelectMany(chunk => chunk.Positions).Any(p => q.Contains(p)));
        }

        [Test]
        public void DeleteNothing()
        {
            var a = CreateRegularPointsInUnitCube(10, 1);
            var b = a.Delete(n => false, n => true, p => false, CancellationToken.None);

            Assert.IsTrue(a.PointCount == b.PointCount);
            Assert.IsTrue(a.Id != b.Id);
        }

        [Test]
        public void DeleteAll()
        {
            var a = CreateRegularPointsInUnitCube(10, 1);
            var b = a.Delete(n => true, n => false, p => true, CancellationToken.None);

            Assert.IsTrue(a.PointCount != b.PointCount);
            Assert.IsTrue(b.PointCount == 0);
            Assert.IsTrue(a.Id != b.Id);
        }
    }
}
