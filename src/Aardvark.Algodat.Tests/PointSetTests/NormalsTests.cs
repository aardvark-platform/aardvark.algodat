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
using System.Linq;
using System.Threading;
using Uncodium.SimpleStore;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class NormalsTests
    {
        private Storage CreateInMemoryStore() => new SimpleMemoryStore().ToPointCloudStore();

        [Test]
        public void CanCreateChunkWithNormals()
        {
            var chunk = new Chunk(new[] { V3d.IOO }, new[] { C4b.White }, new[] { V3f.OIO });
            Assert.IsTrue(chunk.Normals != null);
            Assert.IsTrue(chunk.Normals[0] == V3f.OIO);
        }

        [Test]
        public void CanCreateInMemoryPointSetWithNormals()
        {
            var chunk = new Chunk(new[] { V3d.IOO }, new[] { C4b.White }, new[] { V3f.OIO });

            var builder = InMemoryPointSet.Build(chunk, 8192);
            var root = builder.ToPointSetNode(CreateInMemoryStore());
          
            Assert.IsTrue(root.IsLeaf);
            Assert.IsTrue(root.PointCount == 1);
            Assert.IsTrue(root.HasNormals);
            Assert.IsTrue(root.Normals.Value[0] == V3f.OIO);
        }

        [Test]
        public void CanCreatePointSetWithNormals()
        {
            var ps = PointSet.Create(CreateInMemoryStore(), Guid.NewGuid().ToString(),
                new[] { V3d.IOO }, new[] { C4b.White }, new[] { V3f.OIO }, null, null, 8192, true,
                CancellationToken.None
                );

            Assert.IsTrue(!ps.IsEmpty);
            Assert.IsTrue(ps.Octree.Value.IsLeaf());
            Assert.IsTrue(ps.Octree.Value.GetPositions().Value.Length == 1);
            Assert.IsTrue(ps.Octree.Value.HasNormals());
            Assert.IsTrue(ps.Octree.Value.GetNormals().Value[0] == V3f.OIO);
        }

        [Test]
        public void CanAddNormals()
        {
            var r = new Random();
            var storage = PointSetTests.CreateStorage();

            var ps = new V3d[10000].SetByIndex(_ => new V3d(r.NextDouble(), r.NextDouble(), r.NextDouble()));

            var pointset = PointSet
                .Create(storage, "test", ps.ToList(), null, null, null, null, 5000, false, CancellationToken.None)
                .GenerateLod(ImportConfig.Default.WithKey("lod").WithOctreeSplitLimit(5000))
                ;
            storage.Add("pss", pointset, CancellationToken.None);

            var withNormals = WithRandomNormals(pointset.Root.Value);
            storage.Add("psWithNormals", withNormals, CancellationToken.None);

            withNormals.ForEachNode(true, node =>
            {
                if (node.IsLeaf)
                {
                    Assert.IsTrue(node.HasNormals);
                    Assert.IsTrue(!node.HasLodNormals);
                    Assert.IsTrue(node.Normals.Value.Length == node.PointCount);
                }
                else
                {
                    Assert.IsTrue(!node.HasNormals);
                    Assert.IsTrue(node.HasLodNormals);
                    Assert.IsTrue(node.LodNormals.Value.Length == node.LodPointCount);
                }

                //
                var binary = node.ToBinary();
                var node2 = PointSetNode.ParseBinary(binary, storage);
                Assert.IsTrue(node.HasNormals == node2.HasNormals);
                Assert.IsTrue(node.HasLodNormals == node2.HasLodNormals);
                Assert.IsTrue(node.Normals?.Value?.Length == node2.Normals?.Value?.Length);
                Assert.IsTrue(node.LodNormals?.Value?.Length == node2.LodNormals?.Value?.Length);
            });

            PointSetNode WithRandomNormals(PointSetNode n)
            {
                var id = Guid.NewGuid();
                var ns = new V3f[n.IsLeaf ? n.PointCount : n.LodPointCount].Set(V3f.OOI);
                storage.Add(id, ns, CancellationToken.None);

                if (n.IsLeaf)
                {
                    var m = n.WithNormals(id);
                    return m;
                }
                else
                {
                    var subnodes = n.Subnodes.Map(x => x != null ? WithRandomNormals(x.Value) : null);
                    var m = n.WithLodNormals(id, subnodes);
                    return m;
                }
            }
        }
    }
}
