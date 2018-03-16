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
using System.Runtime.Serialization;
using System.Threading;
using Aardvark.Base;
using Aardvark.Geometry.Points;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
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
            var root = builder.ToPointSetCell(CreateInMemoryStore());
          
            Assert.IsTrue(root.IsLeaf);
            Assert.IsTrue(root.PointCount == 1);
            Assert.IsTrue(root.HasNormals);
            Assert.IsTrue(root.Normals.Value[0] == V3f.OIO);
        }

        [Test]
        public void CanCreatePointSetWithNormals()
        {
            var ps = PointSet.Create(CreateInMemoryStore(), Guid.NewGuid().ToString(),
                new[] { V3d.IOO }, new[] { C4b.White }, new[] { V3f.OIO }, 8192,
                CancellationToken.None
                );

            Assert.IsTrue(!ps.IsEmpty);
            var root = ps.Root.Value;
            Assert.IsTrue(root.IsLeaf);
            Assert.IsTrue(root.PointCount == 1);
            Assert.IsTrue(root.HasNormals);
            Assert.IsTrue(root.Normals.Value[0] == V3f.OIO);
        }
    }
}
