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
using Aardvark.Data.Points;
using Aardvark.Geometry.Points;
using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class ViewsFilterTests
    {
        private static readonly Random r = new Random();
        private static V3f RandomPosition() => new V3f(r.NextDouble(), r.NextDouble(), r.NextDouble());
        private static V3f[] RandomPositions(int n) => new V3f[n].SetByIndex(_ => RandomPosition());

        private static IPointCloudNode CreateNode(Storage storage, V3f[] psGlobal, int[] intensities = null)
        {
            var id = Guid.NewGuid();
            var cell = new Cell(psGlobal);
            var center = (V3f)cell.GetCenter();
            var bbGlobal = (Box3d)new Box3f(psGlobal);
            var bbLocal = (Box3f)bbGlobal - center;

            var psLocal = psGlobal.Map(p => p - center);

            var psLocalId = Guid.NewGuid();
            storage.Add(psLocalId, psLocal);

            var kdLocal = psLocal.BuildKdTree();
            var kdLocalId = Guid.NewGuid();
            storage.Add(kdLocalId, kdLocal.Data);

            var result = new PointSetNode(storage, writeToStore: true,
                (Durable.Octree.NodeId, id),
                (Durable.Octree.Cell, cell),
                (Durable.Octree.BoundingBoxExactGlobal, bbGlobal),
                (Durable.Octree.BoundingBoxExactLocal, bbLocal),
                (Durable.Octree.PointCountTreeLeafs, psLocal.LongLength),
                (Durable.Octree.PositionsLocal3fReference, psLocalId),
                (Durable.Octree.PointRkdTreeFDataReference, kdLocalId)
                );

            if (intensities != null)
            {
                var jsId = Guid.NewGuid();
                storage.Add(jsId, intensities);
                result = result.WithUpsert(Durable.Octree.Intensities1iReference, jsId);
            }

            return result;
        }

        #region FilterInsideBox3d

        [Test]
        public void FilterInsideBox3d_AllInside()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var a = CreateNode(storage, RandomPositions(100));

            var f = FilteredNode.Create(a, new FilterInsideBox3d(a.BoundingBoxExactGlobal));
            Assert.IsTrue(f.HasPositions);
            var ps = f.PositionsAbsolute;
            Assert.IsTrue(ps.Length == 100);
        }

        [Test]
        public void FilterInsideBox3d_AllOutside()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var a = CreateNode(storage, RandomPositions(100));

            var f = FilteredNode.Create(a, new FilterInsideBox3d(a.BoundingBoxExactGlobal + V3d.IOO));
            Assert.IsTrue(f == null);
        }

        [Test]
        public void FilterInsideBox3d_Partial()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var a = CreateNode(storage, RandomPositions(100));

            var f = FilteredNode.Create(a, new FilterInsideBox3d(new Box3d(new V3d(0, 0, 0), new V3d(1, 1, 0.5))));
            Assert.IsTrue(f.HasPositions);
            var ps = f.PositionsAbsolute;
            var count = ps.Count(p => p.Z <= 0.5);
            Assert.IsTrue(ps.Length == count);
        }

        #endregion

        #region FilterInsideBox3d

        [Test]
        public void FilterOutsideBox3d_AllInside()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var a = CreateNode(storage, RandomPositions(100));

            var f = FilteredNode.Create(a, new FilterOutsideBox3d(a.BoundingBoxExactGlobal + V3d.IOO));
            Assert.IsTrue(f.HasPositions);
            var ps = f.PositionsAbsolute;
            Assert.IsTrue(ps.Length == 100);
        }

        [Test]
        public void FilterOutsideBox3d_AllOutside()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var a = CreateNode(storage, RandomPositions(100));

            var f = FilteredNode.Create(a, new FilterOutsideBox3d(a.BoundingBoxExactGlobal));
            Assert.IsTrue(f == null);
        }

        [Test]
        public void FilterOutsideBox3d_Partial()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var a = CreateNode(storage, RandomPositions(100));

            var f = FilteredNode.Create(a, new FilterOutsideBox3d(new Box3d(new V3d(0, 0, 0), new V3d(1, 1, 0.5))));
            Assert.IsTrue(f.HasPositions);
            var ps = f.PositionsAbsolute;
            var count = ps.Count(p => p.Z <= 0.5);
            Assert.IsTrue(ps.Length == count);
        }

        #endregion

        #region FilterIntensity

        [Test]
        public void FilterIntensity_AllInside()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var a = CreateNode(storage, RandomPositions(100), new[] { -4, -3, -2, -1, 0, 1, 2, 3, 4, 5 });

            var f = FilteredNode.Create(a, new FilterIntensity(new Range1i(-100, +100)));
            Assert.IsTrue(f.HasIntensities);
            var js = f.Intensities.Value;
            Assert.IsTrue(js.Length == 10);
        }
        
        [Test]
        public void FilterIntensity_AllOutside()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var a = CreateNode(storage, RandomPositions(100), new[] { -4, -3, -2, -1, 0, 1, 2, 3, 4, 5 });

            var f = FilteredNode.Create(a, new FilterIntensity(new Range1i(6, 10000)));
            Assert.IsTrue(f.PointCountCell == 0);
        }

        [Test]
        public void FilterIntensity_Partial()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var a = CreateNode(storage, RandomPositions(100), new[] { -4, -3, -2, -1, 0, 1, 2, 3, 4, 5 });

            var f = FilteredNode.Create(a, new FilterIntensity(new Range1i(-2, +2)));
            Assert.IsTrue(f.HasIntensities);
            var js = f.Intensities.Value;
            Assert.IsTrue(js.Length == 5);
        }

        #endregion

        #region Serialization

        [Test]
        public void Serialize_FilterInsideBox3d()
        {
            new FilterInsideBox3d(Box3d.Unit).Serialize();
        }
        [Test]
        public void Serialize_FilterOutsideBox3d()
        {
            new FilterOutsideBox3d(Box3d.Unit).Serialize();
        }
        [Test]
        public void Serialize_FilterClassification()
        {
            new FilterClassification(new byte[] { 1, 2, 3, 4, 5 }).Serialize();
        }
        [Test]
        public void Serialize_FilterIntensity()
        {
            new FilterIntensity(new Range1i(-5, +17)).Serialize();
        }
        [Test]
        public void Serialize_FilterNormalDirection()
        {
            new FilterNormalDirection(V3f.ZAxis, 0.1f).Serialize();
        }
        [Test]
        public void Serialize_FilterOr()
        {
            new FilterOr(new FilterInsideBox3d(Box3d.Unit), new FilterOutsideBox3d(Box3d.Unit)).Serialize();
        }
        [Test]
        public void Serialize_FilterAnd()
        {
            new FilterAnd(new FilterInsideBox3d(Box3d.Unit), new FilterOutsideBox3d(Box3d.Unit)).Serialize();
        }

        #endregion

        #region FilteredNode

        [Test]
        public void EncodeDecodeRoundtrip()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var a = CreateNode(storage, RandomPositions(100));

            var f = (FilteredNode)FilteredNode.Create(a, new FilterInsideBox3d(a.BoundingBoxExactGlobal + new V3d(0.5, 0.0, 0.0)));
            var buffer = f.Encode();
            Assert.IsTrue(buffer != null);

            var g = FilteredNode.Decode(storage, buffer);
            Assert.IsTrue(f.Id == g.Id);
            Assert.IsTrue(f.Node.Id == g.Node.Id);

            var fFilterJson = f.Filter.Serialize().ToString();
            var gFilterJson = g.Filter.Serialize().ToString();
            Assert.IsTrue(fFilterJson == gFilterJson);
        }

        #endregion

        #region Delete

        [Test]
        public void CanDeletePoints()
        {
            var q = new Box3d(new V3d(0.3), new V3d(0.7));
            var q1 = new Box3d(new V3d(0.4), new V3d(0.6));

            var a = DeleteTests.CreateRegularPointsInUnitCube(10, 1024).Root.Value;
            var store = ((PointSetNode)a).Storage;
            a.ForEachNode(true, n =>
            {
                Assert.IsTrue(store.GetPointCloudNode(n.Id) != null);
            });

            var f = FilteredNode.Create(a, new FilterInsideBox3d(q));

            var b = f.Delete(
                n => q1.Contains(n.BoundingBoxExactGlobal),
                n => !(q1.Contains(n.BoundingBoxExactGlobal) || q1.Intersects(n.BoundingBoxExactGlobal)),
                p => q1.Contains(p), a.Storage, default);

            Assert.IsTrue(a.PointCountTree > b.PointCountTree);

            Assert.IsTrue(!b.QueryAllPoints().SelectMany(chunk => chunk.Positions).Any(p => q1.Contains(p)));

            Assert.IsTrue(b.HasCentroidLocal);
        }

        #endregion
    }
}
