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
using System.Collections.Generic;
using System.Threading;
using Uncodium.SimpleStore;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class PointSetTests
    {
        internal static Storage CreateStorage()
        {
            var x = new SimpleMemoryStore();
            return new Storage(x.Add, x.Get, x.Remove, x.Dispose, x.Flush, cache: default);
        }

        internal static Storage CreateDiskStorage(string dbDiskLocation)
        {
            var x = new SimpleDiskStore(dbDiskLocation);
            return new Storage(x.Add, x.Get, x.Remove, x.Dispose, x.Flush, cache: default);
        }

        [Test]
        public void CanCreateEmptyPointSet()
        {
            var pointset = PointSet.Empty;
            Assert.IsTrue(pointset.Bounds.IsInvalid);
            Assert.IsTrue(pointset.BoundingBox.IsInvalid);
            Assert.IsTrue(pointset.Id == "PointSet.Empty");
            Assert.IsTrue(pointset.IsEmpty == true);
            Assert.IsTrue(pointset.PointCount == 0);
            Assert.IsTrue(pointset.Root == null);
            Assert.IsTrue(pointset.SplitLimit == 0);
        }

        [Test]
        public void CanCreatePointSetFromSinglePoint()
        {
            var store = CreateStorage();
            var ps = new List<V3d> { new V3d(0.1, 0.2, 0.3) };
            var cs = new List<C4b> { C4b.White };
            var pointset = PointSet.Create(store, "id", ps, cs, null, null, null, 1000, false, CancellationToken.None);
            Assert.IsTrue(pointset.PointCount == 1);
            Assert.IsTrue(pointset.Root.Value.IsLeaf());
            Assert.IsTrue(pointset.Root.Value.Positions.Value.Length == 1);
            Assert.IsTrue(pointset.Root.Value.PointCountTree == 1);
        }

        [Test]
        public void CanCreateInMemoryPointSet()
        {
            var ps = new List<V3d> { new V3d(0.5, 0.5, 0.5) };
            var cs = new List<C4b> { C4b.White };
            var ns = new List<V3f> { V3f.ZAxis };
            var js = new List<int> { 123 };
            var ks = new List<byte> { 42 };
            var imps = InMemoryPointSet.Build(ps, cs, ns, js, ks, Cell.Unit, 1);
        }

        [Test]
        public void CanCreateInMemoryPointSetWithoutColors()
        {
            var ps = new List<V3d> { new V3d(0.5, 0.5, 0.5) };
            var ns = new List<V3f> { V3f.ZAxis };
            var js = new List<int> { 123 };
            var ks = new List<byte> { 42 };
            var imps = InMemoryPointSet.Build(ps, null, ns, js, ks, Cell.Unit, 1);
        }

        [Test]
        public void CanCreateInMemoryPointSetWithoutNormals()
        {
            var ps = new List<V3d> { new V3d(0.5, 0.5, 0.5) };
            var cs = new List<C4b> { C4b.White };
            var js = new List<int> { 123 };
            var ks = new List<byte> { 42 };
            var imps = InMemoryPointSet.Build(ps, cs, null, js, ks, Cell.Unit, 1);
        }

        [Test]
        public void CanCreateInMemoryPointSetWithoutIntensities()
        {
            var ps = new List<V3d> { new V3d(0.5, 0.5, 0.5) };
            var cs = new List<C4b> { C4b.White };
            var ns = new List<V3f> { V3f.ZAxis };
            var ks = new List<byte> { 42 };
            var imps = InMemoryPointSet.Build(ps, cs, ns, null, ks, Cell.Unit, 1);
        }

        [Test]
        public void CanCreateInMemoryPointSetWithoutClassifications()
        {
            var ps = new List<V3d> { new V3d(0.5, 0.5, 0.5) };
            var cs = new List<C4b> { C4b.White };
            var ns = new List<V3f> { V3f.ZAxis };
            var js = new List<int> { 123 };
            var imps = InMemoryPointSet.Build(ps, cs, ns, js, null, Cell.Unit, 1);
        }

        [Test]
        public void CanCreateInMemoryPointSet_Many()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var ps = new List<V3d>();
            var ns = new List<V3f>();
            for (var x = 0.125; x < 1.0; x += 0.25)
                for (var y = 0.125; y < 1.0; y += 0.25)
                    for (var z = 0.125; z < 1.0; z += 0.25)
                    {
                        ps.Add(new V3d(x, y, z));
                        ns.Add(V3f.ZAxis);
                    }

            Assert.IsTrue(ps.Count == 4 * 4 * 4);

            var imps = InMemoryPointSet.Build(ps, null, ns, null, null, new Cell(0, 0, 0, 0), 1);
            var root = imps.ToPointSetNode(storage, isTemporaryImportNode: false);
            Assert.IsTrue(root.PointCountTree == 4 * 4 * 4);
            var countNodes = root.CountLeafNodes(true);
            Assert.IsTrue(countNodes == 4 * 4 * 4);
        }


        [Test]
        public void PointSetAttributes_EmptyPointSet()
        {
            var pointset = PointSet.Empty;
            Assert.IsTrue(pointset.HasColors == false);
            Assert.IsTrue(pointset.HasIntensities == false);
            Assert.IsTrue(pointset.HasKdTree == false);
            Assert.IsTrue(pointset.HasNormals == false);
            Assert.IsTrue(pointset.HasPositions == false);
        }

        [Test]
        public void PointSetAttributes_All()
        {
            var ps = new List<V3d> { new V3d(0.5, 0.5, 0.5) };
            var cs = new List<C4b> { C4b.White };
            var ns = new List<V3f> { V3f.ZAxis };
            var js = new List<int> { 123 };
            var ks = new List<byte> { 42 };
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var pointset = PointSet.Create(storage, "test", ps, cs, ns, js, ks, 1, false, default);
            Assert.IsTrue(pointset.HasColors == true);
            Assert.IsTrue(pointset.HasIntensities == true);
            Assert.IsTrue(pointset.HasClassifications == true);
            Assert.IsTrue(pointset.HasKdTree == true);
            Assert.IsTrue(pointset.HasNormals == true);
            Assert.IsTrue(pointset.HasPositions == true);
        }

        [Test]
        public void PointSetAttributes_NoLod()
        {
            var ps = new List<V3d> { new V3d(0.5, 0.5, 0.5) };
            var cs = new List<C4b> { C4b.White };
            var ns = new List<V3f> { V3f.ZAxis };
            var js = new List<int> { 123 };
            var ks = new List<byte> { 42 };
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var pointset = PointSet.Create(storage, "test", ps, cs, ns, js, ks, 1, false, default);
            Assert.IsTrue(pointset.HasColors == true);
            Assert.IsTrue(pointset.HasIntensities == true);
            Assert.IsTrue(pointset.HasClassifications == true);
            Assert.IsTrue(pointset.HasKdTree == true);
            Assert.IsTrue(pointset.HasNormals == true);
            Assert.IsTrue(pointset.HasPositions == true);
        }

        [Test]
        public void PointSetAttributes_PositionsAndColors()
        {
            var ps = new List<V3d> { new V3d(0.5, 0.5, 0.5) };
            var cs = new List<C4b> { C4b.White };
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var pointset = PointSet.Create(storage, "test", ps, cs, null, null, null, 1, true, default);
            Assert.IsTrue(pointset.HasColors == true);
            Assert.IsTrue(pointset.HasIntensities == false);
            Assert.IsTrue(pointset.HasKdTree == true);
            Assert.IsTrue(pointset.HasNormals == true);
            Assert.IsTrue(pointset.HasPositions == true);
        }
    }
}
