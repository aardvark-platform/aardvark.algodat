/*
    Copyright (C) 2006-2020. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using NUnit.Framework;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class ViewsFilterTests
    {
        private static readonly Random r = new();
        private static V3f RandomPosition() => new(r.NextDouble(), r.NextDouble(), r.NextDouble());
        private static V3f[] RandomPositions(int n) => new V3f[n].SetByIndex(_ => RandomPosition());
        private static int[] RandomIntensities(int n) => new int[n].SetByIndex(_ => -999 + r.Next(1998));

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

            var data = ImmutableDictionary<Durable.Def, object>.Empty
                .Add(Durable.Octree.NodeId, id)
                .Add(Durable.Octree.Cell, cell)
                .Add(Durable.Octree.BoundingBoxExactGlobal, bbGlobal)
                .Add(Durable.Octree.BoundingBoxExactLocal, bbLocal)
                .Add(Durable.Octree.PointCountTreeLeafs, psLocal.LongLength)
                .Add(Durable.Octree.PositionsLocal3fReference, psLocalId)
                .Add(Durable.Octree.PointRkdTreeFDataReference, kdLocalId)
                ;

            if (intensities != null)
            {
                var jsId = Guid.NewGuid();
                storage.Add(jsId, intensities);
                data = data.Add(Durable.Octree.Intensities1iReference, jsId);
            }

            var result = new PointSetNode(data, storage, writeToStore: true);
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
            Assert.IsTrue(f.PointCountCell == 0);
        }

        [Test]
        public void FilterInsideBox3d_StoreAddGet()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var a = CreateNode(storage, RandomPositions(100));

            var f = FilteredNode.Create(a, new FilterInsideConvexHull3d(new Hull3d(a.BoundingBoxExactGlobal + V3d.IOO * 0.1)));
            var g = f.Id;
            var k = g.ToString();
            storage.Add(k, f);
            //var data = storage.GetByteArray(k);
            //var f2 = FilteredNode.Decode(storage, data);

            var ps = storage.GetPointCloudNode(g);

            Assert.IsTrue(ps.Id == f.Id);

            var f2 = (FilteredNode)ps;
            var f1 = (FilteredNode)f;
            Assert.IsTrue(f2.Node.Id == f1.Node.Id); // how to compare nodes structurally?
            var f2s = f2.Filter.Serialize().ToString();
            var f1s = f1.Filter.Serialize().ToString();
            Assert.IsTrue(f2s == f1s); // how to compare filters structurally ?
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
            Assert.IsTrue(f.PointCountCell == 0);
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
            var intensities = RandomIntensities(100);
            var a = CreateNode(storage, RandomPositions(100), intensities);

            var f = FilteredNode.Create(a, new FilterIntensity(new Range1i(-1000, +1000)));
            Assert.IsTrue(f.HasIntensities);
            var js = f.Intensities.Value;
            Assert.IsTrue(js.Length == 100);
        }
        
        [Test]
        public void FilterIntensity_AllOutside()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var a = CreateNode(storage, RandomPositions(100), RandomIntensities(100));

            var f = FilteredNode.Create(a, new FilterIntensity(new Range1i(-30000, -10000)));
            Assert.IsTrue(f.PointCountCell == 0);
        }

        [Test]
        public void FilterIntensity_Partial()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var intensities = RandomIntensities(100);
            intensities[17] = 10000;
            intensities[42] = 20000;
            var a = CreateNode(storage, RandomPositions(100), intensities);

            var f = FilteredNode.Create(a, new FilterIntensity(new Range1i(10000, 30000)));
            Assert.IsTrue(f.HasIntensities);
            var js = f.Intensities.Value;
            Assert.IsTrue(js.Length == 2);
            Assert.IsTrue(js[0] == 10000);
            Assert.IsTrue(js[1] == 20000);
        }

        #endregion

        #region Serialization

        [Test]
        public void Serialize_FilterInsideBox3d()
        {
            var f = new FilterInsideBox3d(Box3d.Unit);
            var json = f.Serialize().ToString();
            var g = Filter.Deserialize(json);
            Assert.True(f.Equals(g));
        }
        [Test]
        public void Serialize_FilterOutsideBox3d()
        {
            var f = new FilterOutsideBox3d(Box3d.Unit);
            var json = f.Serialize().ToString();
            var g = Filter.Deserialize(json);
            Assert.True(f.Equals(g));
        }
        [Test]
        public void Serialize_FilterInsideConvexHull3d()
        {
            var f = new FilterInsideConvexHull3d(new Hull3d(Box3d.Unit));
            var json = f.Serialize().ToString();
            var g = Filter.Deserialize(json);
            Assert.True(f.Equals(g));
        }
        [Test]
        public void Serialize_FilterInsideConvexHulls3d_A()
        {
            var f = new FilterInsideConvexHulls3d(new Hull3d(Box3d.Unit), new Hull3d(Box3d.Unit.Translated(new V3d(-1, 3.14, 12345.67))));
            var json = f.Serialize().ToString();
            var g = Filter.Deserialize(json);
            Assert.True(f.Equals(g));
        }
        [Test]
        public void Serialize_FilterInsideConvexHulls3d_B()
        {
            var f = new FilterInsideConvexHulls3d(Box2d.Unit.ToPolygon2dCCW(), Range1d.Unit, Trafo3d.Translation(1,2,-3));
            var json = f.Serialize().ToString();
            var g = Filter.Deserialize(json);
            Assert.True(f.Equals(g));
        }
        [Test]
        public void Serialize_FilterInsidePrismXY()
        {
            var f = new FilterInsidePrismXY(Box2d.Unit.ToPolygon2dCCW(), Range1d.Unit);
            var json = f.Serialize().ToString();
            var g = Filter.Deserialize(json);
            Assert.True(f.Equals(g));
        }
        [Test]
        public void Serialize_FilterInsideSphere3d()
        {
            var f = new FilterInsideSphere3d(new Sphere3d(new V3d(1,2,3), 4));
            var json = f.Serialize().ToString();
            var g = Filter.Deserialize(json);
            Assert.True(f.Equals(g));
        }
        [Test]
        public void Serialize_FilterClassification()
        {
            var f = new FilterClassification(new byte[] { 1, 2, 3, 4, 5 });
            var json = f.Serialize().ToString();
            var g = Filter.Deserialize(json);
            Assert.True(f.Equals(g));
        }
        [Test]
        public void Serialize_FilterIntensity()
        {
            var f = new FilterIntensity(new Range1i(-5, +17));
            var json = f.Serialize().ToString();
            var g = Filter.Deserialize(json);
            Assert.True(f.Equals(g));
        }
        [Test]
        public void Serialize_FilterNormalDirection()
        {
            var f = new FilterNormalDirection(V3f.ZAxis, 0.1f);
            var json = f.Serialize().ToString();
            var g = Filter.Deserialize(json);
            Assert.True(f.Equals(g));
        }
        [Test]
        public void Serialize_FilterOr()
        {
            var f = new FilterOr(new FilterInsideBox3d(Box3d.Unit), new FilterOutsideBox3d(Box3d.Unit));
            var json = f.Serialize().ToString();
            var g = Filter.Deserialize(json);
            Assert.True(f.Equals(g));
        }
        [Test]
        public void Serialize_FilterAnd()
        {
            var f = new FilterAnd(new FilterInsideBox3d(Box3d.Unit), new FilterOutsideBox3d(Box3d.Unit));
            var json = f.Serialize().ToString();
            var g = Filter.Deserialize(json);
            Assert.True(f.Equals(g));
        }

        #endregion

        #region FilteredNode

        [Test]
        public void EncodeDecodeRoundtrip()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var a = CreateNode(storage, RandomPositions(100));

            var f = (FilteredNode)FilteredNode.Create(a, new FilterInsideBox3d(a.BoundingBoxExactGlobal + new V3d(0.5, 0.0, 0.0)));
            var buffer = ((IPointCloudNode)f).Encode();
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
                p => q1.Contains(p), a.Storage, default, 1024);

            Assert.IsTrue(a.PointCountTree > b.PointCountTree);

            Assert.IsTrue(!b.QueryAllPoints().SelectMany(chunk => chunk.Positions).Any(p => q1.Contains(p)));

            Assert.IsTrue(b.HasCentroidLocal);
        }

        #endregion
    }
}
