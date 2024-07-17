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
using NUnit.Framework;
using NUnit.Framework.Legacy;
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
            ClassicAssert.IsTrue(x.IsEmpty);
            ClassicAssert.IsTrue(x.HasPositions);
            ClassicAssert.IsTrue(!x.HasColors);
        }

        [Test]
        public void Chunk_EmptyChunk2()
        {
            var x = new Chunk(Array.Empty<V3d>(), Array.Empty<C4b>(), null, null, null, null, null, null);
            ClassicAssert.IsTrue(x.IsEmpty);
            ClassicAssert.IsTrue(x.HasPositions);
            ClassicAssert.IsTrue(x.HasColors);
        }

        [Test]
        public void ChunkFromPositionsOnly()
        {
            var x = new Chunk(new V3d[1]);
            ClassicAssert.IsTrue(x.Count == 1);
            ClassicAssert.IsTrue(x.HasPositions);
            ClassicAssert.IsTrue(!x.HasColors);
        }

        [Test]
        public void Chunk_ChunkFromPositionsAndColors()
        {
            var x = new Chunk(new V3d[1], new C4b[1], null, null, null, null, null, null);
            ClassicAssert.IsTrue(x.Count == 1);
            ClassicAssert.IsTrue(x.HasPositions);
            ClassicAssert.IsTrue(x.HasColors);
        }

        [Test]
        public void Chunk_BoundingBoxIsComputedFromPositions()
        {
            var x = new Chunk(new[] { new V3d(1, 2, 3), new V3d(4, 5, 6) });
            ClassicAssert.IsTrue(x.Count == 2);
            ClassicAssert.IsTrue(x.BoundingBox == new Box3d(new V3d(1, 2, 3), new V3d(4, 5, 6)));
        }

        [Test]
        public void Chunk_BoundingBoxIsNotComputedFromPositions()
        {
            var x = new Chunk(
                new[] { new V3d(1, 2, 3), new V3d(4, 5, 6) },
                null, null, null, null, null, null,
                bbox: new Box3d(new V3d(0, 0, 0), new V3d(8, 8, 8))
                );
            ClassicAssert.IsTrue(x.Count == 2);
            ClassicAssert.IsTrue(x.BoundingBox == new Box3d(new V3d(0, 0, 0), new V3d(8, 8, 8)));
        }

        [Test]
        public void Chunk_MismatchingNumberOfColorsDoesNotFail1()
        {
            Assert.DoesNotThrow(() =>
            {
                var chunk = new Chunk(new[] { new V3d(1, 2, 3), new V3d(4, 5, 6) }, Array.Empty<C4b>(), null, null, null, null, null, null);
                ClassicAssert.IsTrue(chunk.Count == 0);
                ClassicAssert.IsTrue(chunk.Positions.Count == 0);
                ClassicAssert.IsTrue(chunk.Colors.Count == 0);
            });
        }

        [Test]
        public void Chunk_MismatchingNumberOfColorsDoesNotFail2()
        {
            Assert.DoesNotThrow(() =>
            {
                var chunk = new Chunk(new[] { new V3d(1, 2, 3), new V3d(4, 5, 6) }, new C4b[123], null, null, null, null, null, null);
                ClassicAssert.IsTrue(chunk.Count == 2);
                ClassicAssert.IsTrue(chunk.Positions.Count == 2);
                ClassicAssert.IsTrue(chunk.Colors.Count == 2);
            });
        }

        [Test]
        public void Chunk_MismatchingNumberOfColorsFails3()
        {
            Assert.Catch(() =>
            {
                new Chunk(null, new C4b[123],null, null, null, null, null, null);
            });
        }

        [Test]
        public void Chunk_EmptyColors()
        {
            _ = new Chunk(new[] { new V3d(1, 2, 3), new V3d(4, 5, 6) });
        }

        [Test]
        public void Chunk_EmptyPositionsButNonEmptyColorsFails()
        {
            Assert.Catch(() =>
            {
                new Chunk(null, new C4b[1], null, null, null, null, null, null);
            });
        }

        [Test]
        public void Chunk_ImmutableFilterSequentialMinDistL2()
        {
            var ps = new[] { new V3d(1, 2, 3), new V3d(1.5, 2, 3), new V3d(2, 2, 3), new V3d(2.5, 2, 3) };
            var a = new Chunk(ps.Copy());
            var b = a.ImmutableFilterSequentialMinDistL2(0.75);

            for (var i = 0; i < ps.Length; i++)
                ClassicAssert.IsTrue(a.Positions[i] == ps[i]);

            ClassicAssert.IsTrue(b.Positions.Count == 2);
            ClassicAssert.IsTrue(b.Positions[0] == ps[0]);
            ClassicAssert.IsTrue(b.Positions[1] == ps[2]);
        }

        [Test]
        public void Chunk_ImmutableMapPositions()
        {
            var a = new Chunk(new[] { new V3d(1, 2, 3), new V3d(4, 5, 6) });
            var b = a.ImmutableMapPositions(p => p + new V3d(10, 20, 30));

            ClassicAssert.IsTrue(a.Positions[0] == new V3d(1, 2, 3));
            ClassicAssert.IsTrue(a.Positions[1] == new V3d(4, 5, 6));

            ClassicAssert.IsTrue(b.Positions[0] == new V3d(11, 22, 33));
            ClassicAssert.IsTrue(b.Positions[1] == new V3d(14, 25, 36));
        }

        [Test]
        public void Chunk_ImmutableDeduplicate_1()
        {
            var a = new Chunk(new[] { new V3d(1, 2, 3), new V3d(4, 5, 6), new V3d(1, 2, 3), new V3d(4, 5, 6) });
            var b = a.ImmutableDeduplicate(verbose: false);

            ClassicAssert.IsTrue(a.Positions.Count == 4);
            ClassicAssert.IsTrue(b.Positions.Count == 2);
            ClassicAssert.IsTrue(b.Positions[0] == new V3d(1, 2, 3));
            ClassicAssert.IsTrue(b.Positions[1] == new V3d(4, 5, 6));
        }

        [Test]
        public void Chunk_ImmutableDeduplicate_2()
        {
            var a = new Chunk(new[] { new V3d(1, 2, 3), new V3d(4, 5, 6), new V3d(4, 5, 7), new V3d(4, 5, 8) });
            var b = a.ImmutableDeduplicate(verbose: false);

            ClassicAssert.IsTrue(a.Positions.Count == 4);
            ClassicAssert.IsTrue(b.Positions.Count == 4);
            ClassicAssert.IsTrue(b.Positions[0] == new V3d(1, 2, 3));
            ClassicAssert.IsTrue(b.Positions[1] == new V3d(4, 5, 6));
        }

        [Test]
        public void Chunk_ImmutableFilterMinDistByCell()
        {
            var a = new Chunk(
                positions: new[] { new V3d(0.4, 0.4, 0.4), new V3d(0.6, 0.6, 0.6), new V3d(1.1, 0.1, 0.1), new V3d(1.2, 0.2, 0.2) }
                );

            {
                var b = a.ImmutableFilterMinDistByCell(new Cell(0, 0, 0, 1), ParseConfig.Default.WithMinDist(1.0));
                ClassicAssert.IsTrue(b.Positions.Count == 2);
                ClassicAssert.IsTrue(b.Positions[0] == new V3d(0.4, 0.4, 0.4));
                ClassicAssert.IsTrue(b.Positions[1] == new V3d(1.1, 0.1, 0.1));
            }

            {
                var b = a.ImmutableFilterMinDistByCell(new Cell(0, 0, 0, 1), ParseConfig.Default.WithMinDist(0.5));
                ClassicAssert.IsTrue(b.Positions.Count == 3);
                ClassicAssert.IsTrue(b.Positions[0] == new V3d(0.4, 0.4, 0.4));
                ClassicAssert.IsTrue(b.Positions[1] == new V3d(0.6, 0.6, 0.6));
                ClassicAssert.IsTrue(b.Positions[2] == new V3d(1.1, 0.1, 0.1));
            }
        }

        [Test]
        public void Chunk_ImmutableFilterMinDistByCell_Parts()
        {
            var a = new Chunk(
                positions: new[] { new V3d(0.4, 0.4, 0.4), new V3d(0.6, 0.6, 0.6), new V3d(1.1, 0.1, 0.1), new V3d(1.2, 0.2, 0.2) },
                colors: null, normals: null, intensities: null, classifications: null,
                partIndices: new byte[] { 0, 1, 2, 3 }, partIndexRange: null,
                bbox: null
                );

            {
                var b = a.ImmutableFilterMinDistByCell(new Cell(0, 0, 0, 1), ParseConfig.Default.WithMinDist(1.0));
                ClassicAssert.IsTrue(b.Positions.Count == 2);
                var qs = b.PartIndices as byte[];
                ClassicAssert.NotNull(qs);
                ClassicAssert.IsTrue(qs[0] == 0);
                ClassicAssert.IsTrue(qs[1] == 2);
                ClassicAssert.IsTrue(b.HasPartIndices);
            }

            {
                var b = a.ImmutableFilterMinDistByCell(new Cell(0, 0, 0, 1), ParseConfig.Default.WithMinDist(0.5));
                ClassicAssert.IsTrue(b.Positions.Count == 3);
                var qs = b.PartIndices as byte[];
                ClassicAssert.NotNull(qs);
                ClassicAssert.IsTrue(qs[0] == 0);
                ClassicAssert.IsTrue(qs[1] == 1);
                ClassicAssert.IsTrue(qs[2] == 2);
                ClassicAssert.IsTrue(b.HasPartIndices);
            }
        }
    }
}
