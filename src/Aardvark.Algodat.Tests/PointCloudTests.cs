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
using Aardvark.Data.Points;
using Aardvark.Geometry.Points;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class PointCloudTests
    {
        private static PointSet CreateRandomPointsInUnitCube(int n, int splitLimit)
        {
            var r = new Random();
            var ps = new V3d[n];
            for (var i = 0; i < n; i++) ps[i] = new V3d(r.NextDouble(), r.NextDouble(), r.NextDouble());
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithOctreeSplitLimit(splitLimit)
                ;
            return PointCloud.Chunks(new Chunk(ps), config);
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
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithOctreeSplitLimit(splitLimit)
                ;
            return PointCloud.Chunks(new Chunk(ps), config);
        }

        [Test]
        public void CreateSingleCell_NoSplit()
        {
            var a = CreateRandomPointsInUnitCube(123, int.MaxValue);
            a.ValidateTree();
            Assert.IsTrue(a.CountOctreeLevels() == 1);
            Assert.IsTrue(a.PointCount == 123);
            Assert.IsTrue(a.Root.Value.CountPoints() == 123);
            Assert.IsTrue(a.Root.Value.PointCountTree == 123);
        }

        [Test]
        public void CreateMultiCell_WithSplit()
        {
            var a = CreateRandomPointsInUnitCube(1234, 1024);
            a.ValidateTree();
            Assert.IsTrue(a.CountOctreeLevels() == 2);
            Assert.IsTrue(a.PointCount == 1234);
            Assert.IsTrue(a.Root.Value.CountPoints() == 1234);
            Assert.IsTrue(a.Root.Value.PointCountTree == 1234);
        }
    }
}
