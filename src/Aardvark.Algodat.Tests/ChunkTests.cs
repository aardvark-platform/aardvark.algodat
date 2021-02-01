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
using Aardvark.Data.Points;
using NUnit.Framework;
using System;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class ChunkTests
    {
        [Test]
        public void Chunk_EmptyChunk1()
        {
            var x = Chunk.Empty;
            Assert.IsTrue(x.IsEmpty);
            Assert.IsTrue(!x.HasPositions);
            Assert.IsTrue(!x.HasColors);
        }

        [Test]
        public void Chunk_EmptyChunk2()
        {
            var x = new Chunk(new V3d[0], new C4b[0]);
            Assert.IsTrue(x.IsEmpty);
            Assert.IsTrue(!x.HasPositions);
            Assert.IsTrue(!x.HasColors);
        }

        [Test]
        public void ChunkFromPositionsOnly()
        {
            var x = new Chunk(new V3d[1]);
            Assert.IsTrue(x.Count == 1);
            Assert.IsTrue(x.HasPositions);
            Assert.IsTrue(!x.HasColors);
        }

        [Test]
        public void Chunk_ChunkFromPositionsAndColors()
        {
            var x = new Chunk(new V3d[1], new C4b[1]);
            Assert.IsTrue(x.Count == 1);
            Assert.IsTrue(x.HasPositions);
            Assert.IsTrue(x.HasColors);
        }

        [Test]
        public void Chunk_BoundingBoxIsComputedFromPositions()
        {
            var x = new Chunk(new[] { new V3d(1, 2, 3), new V3d(4, 5, 6) });
            Assert.IsTrue(x.Count == 2);
            Assert.IsTrue(x.BoundingBox == new Box3d(new V3d(1, 2, 3), new V3d(4, 5, 6)));
        }

        [Test]
        public void Chunk_BoundingBoxIsNotComputedFromPositions()
        {
            var x = new Chunk(
                new[] { new V3d(1, 2, 3), new V3d(4, 5, 6) },
                bbox: new Box3d(new V3d(0, 0, 0), new V3d(8, 8, 8))
                );
            Assert.IsTrue(x.Count == 2);
            Assert.IsTrue(x.BoundingBox == new Box3d(new V3d(0, 0, 0), new V3d(8, 8, 8)));
        }

        [Test]
        public void Chunk_MismatchingNumberOfColorsFails1()
        {
            Assert.Catch(() =>
            {
                new Chunk(new[] { new V3d(1, 2, 3), new V3d(4, 5, 6) }, new C4b[0]);
            });
        }

        [Test]
        public void Chunk_MismatchingNumberOfColorsFails2()
        {
            Assert.Catch(() =>
            {
                new Chunk(new[] { new V3d(1, 2, 3), new V3d(4, 5, 6) }, new C4b[123]);
            });
        }

        [Test]
        public void Chunk_MismatchingNumberOfColorsFails3()
        {
            Assert.Catch(() =>
            {
                new Chunk(null, new C4b[123]);
            });
        }

        [Test]
        public void Chunk_EmptyColors()
        {
            new Chunk(new[] { new V3d(1, 2, 3), new V3d(4, 5, 6) }, null);
        }

        [Test]
        public void Chunk_EmptyPositionsButNonEmptyColorsFails()
        {
            Assert.Catch(() =>
            {
                new Chunk(null, new C4b[1]);
            });
        }

        [Test]
        public void Chunk_ImmutableFilterSequentialMinDistL2()
        {
            var ps = new[] { new V3d(1, 2, 3), new V3d(1.5, 2, 3), new V3d(2, 2, 3), new V3d(2.5, 2, 3) };
            var a = new Chunk(ps.Copy(), null);
            var b = a.ImmutableFilterSequentialMinDistL2(0.75);

            for (var i = 0; i < ps.Length; i++)
                Assert.IsTrue(a.Positions[i] == ps[i]);

            Assert.IsTrue(b.Positions.Count == 2);
            Assert.IsTrue(b.Positions[0] == ps[0]);
            Assert.IsTrue(b.Positions[1] == ps[2]);
        }

        [Test]
        public void Chunk_ImmutableMapPositions()
        {
            var a = new Chunk(new[] { new V3d(1, 2, 3), new V3d(4, 5, 6) }, null);
            var b = a.ImmutableMapPositions(p => p + new V3d(10, 20, 30));

            Assert.IsTrue(a.Positions[0] == new V3d(1, 2, 3));
            Assert.IsTrue(a.Positions[1] == new V3d(4, 5, 6));

            Assert.IsTrue(b.Positions[0] == new V3d(11, 22, 33));
            Assert.IsTrue(b.Positions[1] == new V3d(14, 25, 36));
        }

        [Test]
        public void Chunk_ImmutableDeduplicate_1()
        {
            var a = new Chunk(new[] { new V3d(1, 2, 3), new V3d(4, 5, 6), new V3d(1, 2, 3), new V3d(4, 5, 6) }, null);
            var b = a.ImmutableDeduplicate(verbose: false);

            Assert.IsTrue(a.Positions.Count == 4);
            Assert.IsTrue(b.Positions.Count == 2);
            Assert.IsTrue(b.Positions[0] == new V3d(1, 2, 3));
            Assert.IsTrue(b.Positions[1] == new V3d(4, 5, 6));
        }

        [Test]
        public void Chunk_ImmutableDeduplicate_2()
        {
            var a = new Chunk(new[] { new V3d(1, 2, 3), new V3d(4, 5, 6), new V3d(4, 5, 7), new V3d(4, 5, 8) }, null);
            var b = a.ImmutableDeduplicate(verbose: false);

            Assert.IsTrue(a.Positions.Count == 4);
            Assert.IsTrue(b.Positions.Count == 4);
        }
    }
}
