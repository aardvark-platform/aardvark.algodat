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
using Aardvark.Data.Points;
using Aardvark.Geometry.Points;
using NUnit.Framework;
using System;
using System.Threading;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class MergeTests
    {
        [Test]
        public void CanMergePointSets()
        {
            var r = new Random();
            var storage = PointSetTests.CreateStorage();
            var config = ImportConfig.Default
                .WithStorage(storage)
                .WithNormalizePointDensityGlobal(false)
                .WithMinDist(0)
                .WithDeduplicateChunks(false)
                .WithVerbose(true)
                ;

            var ps1 = new V3d[42000].SetByIndex(_ => new V3d(r.NextDouble(), r.NextDouble(), r.NextDouble()));
            var cs1 = ps1.Map(_ => C4b.White);
            var ns1 = ps1.Map(_ => V3f.XAxis);
            var is1 = ps1.Map(_ => 123);
            var ks1 = ps1.Map(_ => (byte)42);
            var pointset1 = PointSet.Create(storage, "test1", ps1, cs1, ns1, is1, ks1, 1000, true, CancellationToken.None);

            var ps2 = new V3d[42000].SetByIndex(_ => new V3d(r.NextDouble() + 0.3, r.NextDouble() + 0.3, r.NextDouble() + 0.3));
            var cs2 = ps2.Map(_ => C4b.White);
            var ns2 = ps2.Map(_ => V3f.XAxis);
            var is2 = ps2.Map(_ => 456);
            var ks2 = ps1.Map(_ => (byte)7);
            var pointset2 = PointSet.Create(storage, "test2", ps2, cs2, ns2, is2, ks2, 1000, true, CancellationToken.None);

            var merged = pointset1.Merge(pointset2, null, config);
            Assert.IsTrue(merged.PointCount == 84000);
            Assert.IsTrue(merged.Octree.Value.PointCountTree == 84000);
            Assert.IsTrue(merged.Octree.Value.CountPoints() == 84000);
        }

        [Test]
        public void CanMergePointSets_WithoutColors()
        {
            var r = new Random();
            var storage = PointSetTests.CreateStorage();
            var config = ImportConfig.Default
                .WithStorage(storage)
                .WithNormalizePointDensityGlobal(false)
                ;

            var ps1 = new V3d[42000].SetByIndex(_ => new V3d(r.NextDouble(), r.NextDouble(), r.NextDouble()));
            var ns1 = ps1.Map(_ => V3f.XAxis);
            var is1 = ps1.Map(_ => 123);
            var ks1 = ps1.Map(_ => (byte)42);
            var pointset1 = PointSet.Create(storage, "test1", ps1, null, ns1, is1, ks1, 1000, true, CancellationToken.None);

            var ps2 = new V3d[42000].SetByIndex(_ => new V3d(r.NextDouble() + 0.3, r.NextDouble() + 0.3, r.NextDouble() + 0.3));
            var ns2 = ps2.Map(_ => V3f.XAxis);
            var is2 = ps2.Map(_ => 456);
            var ks2 = ps1.Map(_ => (byte)7);
            var pointset2 = PointSet.Create(storage, "test2", ps2, null, ns2, is2, ks2, 1000, true, CancellationToken.None);

            var merged = pointset1.Merge(pointset2, null, config);
            Assert.IsTrue(merged.PointCount == 84000);
            Assert.IsTrue(merged.Octree.Value.PointCountTree == 84000);
            Assert.IsTrue(merged.Octree.Value.CountPoints() == 84000);
        }

        [Test]
        public void CanMergePointSets_WithoutNormals()
        {
            var r = new Random();
            var storage = PointSetTests.CreateStorage();
            var config = ImportConfig.Default
                .WithStorage(storage)
                .WithNormalizePointDensityGlobal(false)
                ;

            var ps1 = new V3d[42000].SetByIndex(_ => new V3d(r.NextDouble(), r.NextDouble(), r.NextDouble()));
            var cs1 = ps1.Map(_ => C4b.White);
            var is1 = ps1.Map(_ => 123);
            var ks1 = ps1.Map(_ => (byte)42);
            var pointset1 = PointSet.Create(storage, "test1", ps1, cs1, null, is1, ks1, 1000, true, CancellationToken.None);

            var ps2 = new V3d[42000].SetByIndex(_ => new V3d(r.NextDouble() + 0.3, r.NextDouble() + 0.3, r.NextDouble() + 0.3));
            var cs2 = ps2.Map(_ => C4b.White);
            var is2 = ps2.Map(_ => 456);
            var ks2 = ps2.Map(_ => (byte)7);
            var pointset2 = PointSet.Create(storage, "test2", ps2, cs2, null, is2, ks2, 1000, true, CancellationToken.None);

            var merged = pointset1.Merge(pointset2, null, config);
            Assert.IsTrue(merged.PointCount == 84000);
            Assert.IsTrue(merged.Octree.Value.PointCountTree == 84000);
            Assert.IsTrue(merged.Octree.Value.CountPoints() == 84000);
        }
    }
}
