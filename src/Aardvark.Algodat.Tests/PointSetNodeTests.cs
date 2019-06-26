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
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using Uncodium.SimpleStore;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class PointSetNodeTests
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

        internal static readonly ImmutableDictionary<Durable.Def, object> EmptyData = ImmutableDictionary<Durable.Def, object>.Empty;

        internal static PointSetNode CreateNode(Storage storage)
        {
            var id = Guid.NewGuid();
            var cell = new Cell(1, 2, 3, 0);

            var psId = Guid.NewGuid();
            var ps = new[] { new V3f(1.1f, 2.2f, 3.3f) };
            storage.Add(psId, ps);

            var kdId = Guid.NewGuid();
            var kd = ps.BuildKdTree();
            storage.Add(kdId, kd.Data);

            var data = EmptyData
                .Add(Durable.Octree.NodeId, id)
                .Add(Durable.Octree.Cell, cell)
                .Add(Durable.Octree.PointCountTreeLeafs, ps.LongLength)
                .Add(Durable.Octree.PositionsLocal3fReference, psId)
                .Add(Durable.Octree.PointRkdTreeFDataReference, kdId)
                .Add(Durable.Octree.BoundingBoxExactLocal, new Box3f(ps))
                .Add(Durable.Octree.BoundingBoxExactGlobal, ((Box3d)new Box3f(ps)) + cell.GetCenter())
                ;

            return new PointSetNode(data, storage, writeToStore: true);
        }

        [Test]
        public void CanCreatePointSetNode()
        {
            var storage = CreateStorage();

            var id = Guid.NewGuid();
            var cell = new Cell(1, 2, 3, 0);

            var psId = Guid.NewGuid();
            var ps = new[] { new V3f(1.1f, 2.2f, 3.3f) };
            storage.Add(psId, ps);

            var kdId = Guid.NewGuid();
            var kd = ps.BuildKdTree();
            storage.Add(kdId, kd.Data);

            var data = EmptyData
                .Add(Durable.Octree.NodeId, id)
                .Add(Durable.Octree.Cell, cell)
                .Add(Durable.Octree.PointCountTreeLeafs, ps.LongLength)
                .Add(Durable.Octree.PositionsLocal3fReference, psId)
                .Add(Durable.Octree.PointRkdTreeFDataReference, kdId)
                .Add(Durable.Octree.BoundingBoxExactLocal, new Box3f(ps))
                .Add(Durable.Octree.BoundingBoxExactGlobal, ((Box3d)new Box3f(ps)) + cell.GetCenter())
                ;

            var node = new PointSetNode(data, storage, writeToStore: true);

            Assert.IsTrue(node.Id == id);
            Assert.IsTrue(node.Cell == cell);
            Assert.IsTrue(node.PointCountCell == ps.LongLength);
            Assert.IsTrue(node.PointCountTree == ps.LongLength);
            Assert.IsTrue(node.Cell.BoundingBox == cell.BoundingBox);
            Assert.IsTrue(node.BoundingBoxExactLocal == new Box3f(ps));
        }

        [Test]
        public void CanEncodePointSetNode()
        {
            var storage = CreateStorage();
            var node = CreateNode(storage);
            node.Encode();
        }

        [Test]
        public void CanDecodePointSetNode()
        {
            // encode
            var storage = CreateStorage();
            var node = CreateNode(storage);
            var buffer = node.Encode();

            // decode
            var node2 = PointSetNode.Decode(storage, buffer);
            Assert.IsTrue(node.Id == node2.Id);
            Assert.IsTrue(node.Cell == node2.Cell);
            Assert.IsTrue(node.PointCountTree == node2.PointCountTree);
        }
    }
}
