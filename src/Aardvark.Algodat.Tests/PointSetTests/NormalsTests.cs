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
using NUnit.Framework.Legacy;
using System;
using Uncodium.SimpleStore;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class NormalsTests
    {
        private static Storage CreateInMemoryStore() => new SimpleMemoryStore().ToPointCloudStore(cache: default);

        [Test]
        public void CanCreateChunkWithNormals()
        {
            var chunk = new Chunk(new[] { V3d.IOO }, new[] { C4b.White }, new[] { V3f.OIO }, null, null, null, null, null);
            ClassicAssert.IsTrue(chunk.Normals != null);
            ClassicAssert.IsTrue(chunk.Normals[0] == V3f.OIO);
        }

        [Test]
        public void CanCreateInMemoryPointSetWithNormals()
        {
            var chunk = new Chunk(new[] { V3d.IOO }, new[] { C4b.White }, new[] { V3f.OIO }, null, null, null, null, null);

            var builder = InMemoryPointSet.Build(chunk, 8192);
            var root = builder.ToPointSetNode(CreateInMemoryStore(), isTemporaryImportNode: false);
          
            ClassicAssert.IsTrue(root.IsLeaf);
            ClassicAssert.IsTrue(root.PointCountCell == 1);
            ClassicAssert.IsTrue(root.HasNormals);
            ClassicAssert.IsTrue(root.Normals.Value[0] == V3f.OIO);
        }

        [Test]
        public void CanCreatePointSetWithNormals()
        {
            var ps = PointSet.Create(CreateInMemoryStore(), Guid.NewGuid().ToString(),
                new[] { V3d.IOO }, new[] { C4b.White }, new[] { V3f.OIO }, null, null, null, 8192,
                generateLod: false, isTemporaryImportNode: true, default
                );

            ClassicAssert.IsTrue(!ps.IsEmpty);
            ClassicAssert.IsTrue(ps.Root.Value.IsLeaf());
            ClassicAssert.IsTrue(ps.Root.Value.Positions.Value.Length == 1);
            ClassicAssert.IsTrue(ps.Root.Value.HasNormals);
            ClassicAssert.IsTrue(ps.Root.Value.Normals.Value[0] == V3f.OIO);
        }
    }
}
