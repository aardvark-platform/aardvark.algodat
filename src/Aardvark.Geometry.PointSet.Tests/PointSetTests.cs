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
using System.Collections.Generic;
using System.Threading;
using Aardvark.Base;
using Aardvark.Geometry.Points;
using NUnit.Framework;
using Uncodium.SimpleStore;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class PointSetTests
    {
        internal static Storage CreateStorage()
        {
            var x = new SimpleMemoryStore();
            return new Storage(
                (a, b, c, _) => x.Add(a, b, c), (a, _) => x.Get(a), (a, _) => x.Remove(a),
                (a, _) => x.TryGetFromCache(a), x.Dispose, x.Flush
                );
        }

        internal static Storage CreateDiskStorage(string dbDiskLocation)
        {
            var x = new SimpleDiskStore(dbDiskLocation);
            return new Storage(
                (a, b, c, _) => x.Add(a, b, c), (a, _) => x.Get(a), (a, _) => x.Remove(a),
                (a, _) => x.TryGetFromCache(a), x.Dispose, x.Flush
                );
        }

        [Test]
        public void CanCreatePointSetFromSinglePoint()
        {
            var store = CreateStorage();
            var ps = new List<V3d> { new V3d(0.1, 0.2, 0.3) };
            var cs = new List<C4b> { C4b.White };
            var pointset = PointSet.Create(store, "id", ps, cs, 1000, true, CancellationToken.None);
            Assert.IsTrue(pointset.PointCount == 1);
            Assert.IsTrue(pointset.Root.Value.IsLeaf);
            Assert.IsTrue(pointset.Root.Value.PointCount == 1);
            Assert.IsTrue(pointset.Root.Value.PointCountTree == 1);
        }

        [Test]
        public void CanCreateInMemoryPointSet()
        {
            var ps = new List<V3d> { new V3d(0.5, 0.5, 0.5) };
            var cs = new List<C4b> { C4b.White };
            var imps = InMemoryPointSet.Build(ps, cs, Cell.Unit, 1);
        }

        [Test]
        public void CanCreateInMemoryPointSetWithoutColors()
        {
            var ps = new List<V3d> { new V3d(0.5, 0.5, 0.5) };
            var imps = InMemoryPointSet.Build(ps, null, Cell.Unit, 1);
        }

        [Test]
        public void CanCreateInMemoryPointSet_Many()
        {
            var storage = PointCloud.CreateInMemoryStore();
            var ps = new List<V3d>();
            for (var x = 0.125; x < 1.0; x += 0.25)
                for (var y = 0.125; y < 1.0; y += 0.25)
                    for (var z = 0.125; z < 1.0; z += 0.25)
                        ps.Add(new V3d(x, y, z));

            Assert.IsTrue(ps.Count == 4 * 4 * 4);

            var imps = InMemoryPointSet.Build(ps, null, new Cell(0, 0, 0, 0), 1);
            var root = imps.ToPointSetCell(storage, ct: CancellationToken.None);
            Assert.IsTrue(root.PointCountTree == 4 * 4 * 4);
            var countNodes = root.CountLeafNodes(true);
            Assert.IsTrue(countNodes == 4 * 4 * 4);
        }
    }
}
