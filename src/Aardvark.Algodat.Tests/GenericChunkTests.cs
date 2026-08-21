/*
    Copyright (C) 2006-2026. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using NUnit.Framework.Legacy;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class GenericChunkTests
    {
        [TestCase(0.5)]
        [TestCase(2.0)]
        public void ImmutableFilterSequentialMinDistL1UsesUnsquaredThreshold(double minDist)
        {
            var coordinates = minDist < 1.0
                ? new[] { 0.0, 0.25, 0.5, 1.0 }
                : new[] { 0.0, 1.5, 3.0, 4.5, 6.0 };
            var expectedIndices = minDist < 1.0
                ? new[] { 0, 2, 3 }
                : new[] { 0, 2, 4 };

            AssertSequentialFilter(
                coordinates.Select(x => new V2f((float)x, 0.0f)).ToArray(),
                GenericChunk.Defs.Positions2f,
                p => (V3d)p.XYO,
                minDist,
                expectedIndices
                );
            AssertSequentialFilter(
                coordinates.Select(x => new V2d(x, 0.0)).ToArray(),
                GenericChunk.Defs.Positions2d,
                p => p.XYO,
                minDist,
                expectedIndices
                );
            AssertSequentialFilter(
                coordinates.Select(x => new V3f((float)x, 0.0f, 0.0f)).ToArray(),
                GenericChunk.Defs.Positions3f,
                p => (V3d)p,
                minDist,
                expectedIndices
                );
            AssertSequentialFilter(
                coordinates.Select(x => new V3d(x, 0.0, 0.0)).ToArray(),
                GenericChunk.Defs.Positions3d,
                p => p,
                minDist,
                expectedIndices
                );
        }

        [Test]
        public void GenericChunkImportUsesUnsquaredNonGlobalMinDist()
        {
            var positions = new[]
            {
                new V3d(0.0, 0.0, 0.0),
                new V3d(0.25, 0.0, 0.0),
                new V3d(0.5, 0.0, 0.0),
                new V3d(1.0, 0.0, 0.0)
            };
            var source = CreateChunk(positions, GenericChunk.Defs.Positions3d);
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("generic-chunk-min-dist")
                .WithOctreeSplitLimit(2)
                .WithMinDist(0.5)
                .WithNormalizePointDensityGlobal(false);

            var pointSet = PointCloud.Chunks(source, config);
            var imported = pointSet.QueryAllPoints()
                .SelectMany(chunk => Enumerable.Range(0, chunk.Count)
                    .Select(i => (Position: chunk.Positions[i], Intensity: chunk.Intensities[i])))
                .OrderBy(x => x.Intensity)
                .ToArray();

            ClassicAssert.AreEqual(3, pointSet.PointCount);
            ClassicAssert.IsTrue(imported.Select(x => x.Position).SequenceEqual(new[] { positions[0], positions[2], positions[3] }));
            ClassicAssert.IsTrue(imported.Select(x => x.Intensity).SequenceEqual(new[] { 100, 102, 103 }));
            ClassicAssert.AreEqual(new Box3d(positions[0], positions[3]), pointSet.BoundingBox);
        }

        private static void AssertSequentialFilter<T>(
            T[] positions,
            Durable.Def positionsDef,
            Func<T, V3d> toV3d,
            double minDist,
            int[] expectedIndices
            )
        {
            var source = CreateChunk(positions, positionsDef);
            var filtered = source.ImmutableFilterSequentialMinDistL1(minDist);
            var expectedPositions = expectedIndices.Select(i => positions[i]).ToArray();
            var expectedColors = expectedIndices.Select(i => ((C4b[])source.Colors)[i]).ToArray();
            var expectedNormals = expectedIndices.Select(i => ((V3f[])source.Normals)[i]).ToArray();
            var expectedIntensities = expectedIndices.Select(i => ((int[])source.Intensities)[i]).ToArray();
            var expectedClassifications = expectedIndices.Select(i => ((byte[])source.Classifications)[i]).ToArray();
            var expectedBounds = new Box3d(expectedPositions.Select(toV3d).ToArray());

            ClassicAssert.AreEqual(expectedIndices.Length, filtered.Count);
            ClassicAssert.AreSame(positionsDef, filtered.PositionsDef);
            ClassicAssert.IsInstanceOf<T[]>(filtered.Positions);
            ClassicAssert.IsTrue(((T[])filtered.Positions).SequenceEqual(expectedPositions));
            ClassicAssert.IsTrue(((C4b[])filtered.Colors).SequenceEqual(expectedColors));
            ClassicAssert.IsTrue(((V3f[])filtered.Normals).SequenceEqual(expectedNormals));
            ClassicAssert.IsTrue(((int[])filtered.Intensities).SequenceEqual(expectedIntensities));
            ClassicAssert.IsTrue(((byte[])filtered.Classifications).SequenceEqual(expectedClassifications));
            ClassicAssert.AreEqual(expectedBounds, filtered.BoundingBox);
            ClassicAssert.IsTrue(((T[])source.Positions).SequenceEqual(positions));

            if (positions is V3d[] positions3d)
            {
                var chunk = new Chunk(
                    positions3d,
                    (C4b[])source.Colors,
                    (V3f[])source.Normals,
                    (int[])source.Intensities,
                    (byte[])source.Classifications,
                    partIndices: null,
                    partIndexRange: null,
                    bbox: null
                    ).ImmutableFilterSequentialMinDistL1(minDist);

                ClassicAssert.IsTrue(chunk.Positions.SequenceEqual(filtered.PositionsAsV3d));
                ClassicAssert.IsTrue(chunk.Colors.SequenceEqual((C4b[])filtered.Colors));
                ClassicAssert.IsTrue(chunk.Normals.SequenceEqual((V3f[])filtered.Normals));
                ClassicAssert.IsTrue(chunk.Intensities.SequenceEqual((int[])filtered.Intensities));
                ClassicAssert.IsTrue(chunk.Classifications.SequenceEqual((byte[])filtered.Classifications));
                ClassicAssert.AreEqual(chunk.BoundingBox, filtered.BoundingBox);
            }
        }

        private static GenericChunk CreateChunk<T>(T[] positions, Durable.Def positionsDef)
        {
            var colors = new C4b[positions.Length].SetByIndex(i =>
                new C4b((byte)(i + 1), (byte)(i + 11), (byte)(i + 21), byte.MaxValue)
                );
            var normals = new V3f[positions.Length].SetByIndex(i => new V3f(i + 1, i + 2, i + 3));
            var intensities = new int[positions.Length].SetByIndex(i => 100 + i);
            var classifications = new byte[positions.Length].SetByIndex(i => (byte)(20 + i));
            var data = ImmutableDictionary<Durable.Def, object>.Empty
                .Add(positionsDef, positions)
                .Add(GenericChunk.Defs.Colors4b, colors)
                .Add(GenericChunk.Defs.Normals3f, normals)
                .Add(GenericChunk.Defs.Intensities1i, intensities)
                .Add(GenericChunk.Defs.Classifications1b, classifications);
            return new GenericChunk(data);
        }
    }
}
