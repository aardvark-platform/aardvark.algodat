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
using NUnit.Framework;
using System;
using System.IO;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class ViewsSerializationTests
    {
        private static readonly Random r = new Random();
        private static V3d RandomPosition() => new V3d(r.NextDouble(), r.NextDouble(), r.NextDouble());
        private static V3d[] RandomPositions(int n) => new V3d[n].Map(_ => RandomPosition());

        #region LinkedNode

        [Test]
        public void LinkedNode_ToJson_Parse()
        {
            var teststore = PointCloud.CreateInMemoryStore(cache: default);
            PointCloud.Chunks(new Chunk(RandomPositions(100)), ImportConfig.Default.WithStorage(teststore).WithKey("pointcloud"));

            var store = PointCloud.CreateInMemoryStore(cache: default);
            var resolver = new MapResolver(
                ("teststore", teststore)
                );
            var link0 = new LinkedNode(store, "teststore", "pointcloud", resolver);
            var json = link0.ToJson();
            var link1 = LinkedNode.Parse(json, store, resolver);

            Assert.IsTrue(link1.Id == link0.Id);
            Assert.IsTrue(link1.LinkedStoreName == link0.LinkedStoreName);
            Assert.IsTrue(link1.LinkedPointCloudKey == link0.LinkedPointCloudKey);
        }

        [Test]
        [Ignore("not implemented yet")]
        public void LinkedNode_LinkToOldPointSetNode()
        {
            var storepath = Path.Combine(Config.TempDataDir, Guid.NewGuid().ToString());
            var store = PointCloud.OpenStore(storepath, cache: default);

            var ps0 = new V3d[100].SetByIndex(_ => RandomPosition());
            var a = InMemoryPointSet.Build(new Chunk(ps0), 8192).ToPointSetNode(store);
            
            var pointcloud = new PointSet(store, "pointcloud", a.Id, 8192);
            store.Add(pointcloud.Id, pointcloud);

            var resolver = new MapResolver(
                ("teststore", store)
                );
            var link0 = new LinkedNode(store, "teststore", "pointcloud", resolver);

            store.Add("link", link0);

            store.Flush();
            GC.Collect();

            var link1 = store.GetPointCloudNode("link", resolver);

            Assert.IsTrue(link1.Cell == new Cell(ps0));
            Assert.IsTrue(link1.BoundingBoxExactGlobal == pointcloud.BoundingBox);
            Assert.IsTrue(link1.HasPositions());
            Assert.IsTrue(link1.GetPositionsAbsolute().Length == 100);
        }

        #endregion
    }
}
