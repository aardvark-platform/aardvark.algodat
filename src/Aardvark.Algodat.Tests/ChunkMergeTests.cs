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
using Aardvark.Data.Points;
using Aardvark.Geometry.Points;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class ChunkMergeTests
    {
        public enum OptionalProperty
        {
            Colors,
            Normals,
            Intensities,
            Classifications,
            PartIndices,
        }

        public enum MergeEntryPoint
        {
            BinaryImmutableMerge,
            ParamsImmutableMerge,
            EnumerableImmutableMerge,
            InstanceUnion,
            EnumerableUnion,
            ParamsImmutableMergeWith,
            EnumerableImmutableMergeWith,
        }

        private static string PropertyName(OptionalProperty property) => property switch
        {
            OptionalProperty.Colors => "colors",
            OptionalProperty.Normals => "normals",
            OptionalProperty.Intensities => "intensities",
            OptionalProperty.Classifications => "classifications",
            OptionalProperty.PartIndices => "part indices",
            _ => throw new ArgumentOutOfRangeException(nameof(property)),
        };

        private static Chunk Create(int origin, OptionalProperty? property = null)
        {
            var positions = new[]
            {
                new V3d(origin, origin + 1, origin + 2),
                new V3d(origin + 3, origin + 4, origin + 5),
            };
            return new Chunk(
                positions,
                property == OptionalProperty.Colors ? new[] { new C4b(origin), new C4b(origin + 1) } : null,
                property == OptionalProperty.Normals ? new[] { new V3f(origin, 1, 0), new V3f(origin + 1, 1, 0) } : null,
                property == OptionalProperty.Intensities ? new[] { origin + 10, origin + 11 } : null,
                property == OptionalProperty.Classifications ? new[] { (byte)(origin + 20), (byte)(origin + 21) } : null,
                property == OptionalProperty.PartIndices ? new byte[] { (byte)(origin + 30), (byte)(origin + 31) } : null,
                partIndexRange: null,
                bbox: null
                );
        }

        private static Chunk CreateFull(int origin, object partIndices, Box3d? bounds = null)
        {
            var count = partIndices switch
            {
                IList<byte> xs => xs.Count,
                IList<short> xs => xs.Count,
                IList<int> xs => xs.Count,
                int => 2,
                uint => 2,
                _ => throw new ArgumentException("Unsupported part-index representation.", nameof(partIndices)),
            };
            var positions = new V3d[count];
            var colors = new C4b[count];
            var normals = new V3f[count];
            var intensities = new int[count];
            var classifications = new byte[count];
            for (var i = 0; i < count; i++)
            {
                positions[i] = new V3d(origin + i, origin - i, origin * 2 + i);
                colors[i] = new C4b((byte)(origin + i), (byte)(origin + i + 1), (byte)(origin + i + 2), (byte)255);
                normals[i] = new V3f(origin + i, 1, -1);
                intensities[i] = origin * 10 + i;
                classifications[i] = (byte)(origin + i + 40);
            }
            return new Chunk(positions, colors, normals, intensities, classifications, partIndices, null, bounds);
        }

        private static Chunk AttributeBearingEmpty(Box3d? bounds = null)
            => new(
                Array.Empty<V3d>(),
                Array.Empty<C4b>(),
                Array.Empty<V3f>(),
                Array.Empty<int>(),
                Array.Empty<byte>(),
                Array.Empty<byte>(),
                new Range1i(123),
                bounds ?? new Box3d(new V3d(-100), new V3d(100))
                );

        private static IEnumerable<TestCaseData> SchemaMismatchCases()
        {
            foreach (var entryPoint in Enum.GetValues<MergeEntryPoint>())
            foreach (var property in Enum.GetValues<OptionalProperty>())
            foreach (var reverse in new[] { false, true })
            {
                yield return new TestCaseData(entryPoint, property, reverse)
                    .SetName($"SchemaMismatch_{entryPoint}_{property}_{(reverse ? "MissingFirst" : "PresentFirst")}");
            }
        }

        [TestCaseSource(nameof(SchemaMismatchCases))]
        public void SchemaMismatchIsRejectedInBothOrders(
            MergeEntryPoint entryPoint, OptionalProperty property, bool reverse)
        {
            var present = Create(0, property);
            var missing = Create(10);
            var first = reverse ? missing : present;
            var second = reverse ? present : missing;
            var empty = AttributeBearingEmpty();

            Chunk Merge() => entryPoint switch
            {
                MergeEntryPoint.BinaryImmutableMerge => Chunk.ImmutableMerge(first, second),
                MergeEntryPoint.ParamsImmutableMerge => Chunk.ImmutableMerge(first, empty, second),
                MergeEntryPoint.EnumerableImmutableMerge => Chunk.ImmutableMerge((IEnumerable<Chunk>)new[] { first, empty, second }),
                MergeEntryPoint.InstanceUnion => first.Union(second),
                MergeEntryPoint.EnumerableUnion => new[] { first, empty, second }.AsEnumerable().Union(),
                MergeEntryPoint.ParamsImmutableMergeWith => first.ImmutableMergeWith(empty, second),
                MergeEntryPoint.EnumerableImmutableMergeWith => first.ImmutableMergeWith((IEnumerable<Chunk>)new[] { empty, second }),
                _ => throw new ArgumentOutOfRangeException(nameof(entryPoint)),
            };

            var error = Assert.Throws<InvalidOperationException>(() => Merge());
            Assert.That(error!.Message, Does.Contain("nonempty chunks"));
            Assert.That(error.Message, Does.Contain(PropertyName(property)));
            Assert.That(present.Count, Is.EqualTo(2));
            Assert.That(missing.Count, Is.EqualTo(2));
        }

        [Test]
        public void EmptyChunksAreNeutralAndAllEmptyInputsAreCanonical()
        {
            var empty0 = AttributeBearingEmpty();
            var empty1 = AttributeBearingEmpty(new Box3d(new V3d(-1000), new V3d(1000)));
            var value = CreateFull(1, 7);

            Assert.Multiple(() =>
            {
                Assert.That(Chunk.ImmutableMerge(value), Is.SameAs(value));
                Assert.That(Chunk.ImmutableMerge((IEnumerable<Chunk>)new[] { value }), Is.SameAs(value));
                Assert.That(Chunk.ImmutableMerge(empty0, value), Is.SameAs(value));
                Assert.That(Chunk.ImmutableMerge(value, empty0), Is.SameAs(value));
                Assert.That(empty0.Union(value), Is.SameAs(value));
                Assert.That(value.Union(empty0), Is.SameAs(value));
                Assert.That(Chunk.ImmutableMerge(empty0, value, empty1), Is.SameAs(value));
                Assert.That(Chunk.ImmutableMerge((IEnumerable<Chunk>)new[] { empty0, value, empty1 }), Is.SameAs(value));
                Assert.That(new[] { empty0, value, empty1 }.AsEnumerable().Union(), Is.SameAs(value));
                Assert.That(empty0.ImmutableMergeWith(value, empty1), Is.SameAs(value));
                Assert.That(empty0.ImmutableMergeWith((IEnumerable<Chunk>)new[] { value, empty1 }), Is.SameAs(value));
                Assert.That(value.ImmutableMergeWith(empty0, empty1), Is.SameAs(value));
                Assert.That(value.ImmutableMergeWith((IEnumerable<Chunk>)new[] { empty0, empty1 }), Is.SameAs(value));

                Assert.That(Chunk.ImmutableMerge(empty0), Is.SameAs(Chunk.Empty));
                Assert.That(Chunk.ImmutableMerge((IEnumerable<Chunk>)new[] { empty0 }), Is.SameAs(Chunk.Empty));
                Assert.That(empty0.ImmutableMergeWith(Array.Empty<Chunk>()), Is.SameAs(Chunk.Empty));
                Assert.That(Chunk.ImmutableMerge(empty0, empty1), Is.SameAs(Chunk.Empty));
                Assert.That(empty0.Union(empty1), Is.SameAs(Chunk.Empty));
                Assert.That(Chunk.ImmutableMerge(empty0, Chunk.Empty, empty1), Is.SameAs(Chunk.Empty));
                Assert.That(Chunk.ImmutableMerge((IEnumerable<Chunk>)new[] { empty0, empty1 }), Is.SameAs(Chunk.Empty));
                Assert.That(new[] { empty0, empty1 }.AsEnumerable().Union(), Is.SameAs(Chunk.Empty));
                Assert.That(empty0.ImmutableMergeWith(empty1), Is.SameAs(Chunk.Empty));
                Assert.That(empty0.ImmutableMergeWith((IEnumerable<Chunk>)new[] { empty1 }), Is.SameAs(Chunk.Empty));
                Assert.That(Chunk.ImmutableMerge(Array.Empty<Chunk>()), Is.SameAs(Chunk.Empty));
                Assert.That(Chunk.ImmutableMerge(Enumerable.Empty<Chunk>()), Is.SameAs(Chunk.Empty));
            });
        }

        [Test]
        public void CompatibleMergesPreserveValuesOrderBoundsAndSources()
        {
            var boundsA = new Box3d(new V3d(-10, -9, -8), new V3d(2, 3, 4));
            var boundsB = new Box3d(new V3d(5, 6, 7), new V3d(20, 21, 22));
            var a = CreateFull(1, 7, boundsA);
            var b = CreateFull(5, new byte[] { 8, 9 }, boundsB);
            var empty = AttributeBearingEmpty();

            var originalPositionsA = a.Positions.ToArray();
            var originalColorsA = a.Colors!.ToArray();
            var originalNormalsA = a.Normals!.ToArray();
            var originalIntensitiesA = a.Intensities!.ToArray();
            var originalClassificationsA = a.Classifications!.ToArray();
            var originalPartsA = PartIndexUtils.Expand(a.PartIndices, a.Count)!;
            var expectedPositions = a.Positions.Concat(b.Positions).ToArray();
            var expectedColors = a.Colors.Concat(b.Colors!).ToArray();
            var expectedNormals = a.Normals.Concat(b.Normals!).ToArray();
            var expectedIntensities = a.Intensities.Concat(b.Intensities!).ToArray();
            var expectedClassifications = a.Classifications.Concat(b.Classifications!).ToArray();
            var expectedParts = new[] { 7, 7, 8, 9 };
            var expectedBounds = new Box3d(boundsA, boundsB);

            var results = new[]
            {
                Chunk.ImmutableMerge(a, b),
                Chunk.ImmutableMerge(a, empty, b),
                Chunk.ImmutableMerge((IEnumerable<Chunk>)new[] { a, empty, b }),
                a.Union(b),
                new[] { a, empty, b }.AsEnumerable().Union(),
                a.ImmutableMergeWith(empty, b),
                a.ImmutableMergeWith((IEnumerable<Chunk>)new[] { empty, b }),
            };

            foreach (var result in results)
            {
                Assert.That(result.Positions, Is.EqualTo(expectedPositions));
                Assert.That(result.Colors, Is.EqualTo(expectedColors));
                Assert.That(result.Normals, Is.EqualTo(expectedNormals));
                Assert.That(result.Intensities, Is.EqualTo(expectedIntensities));
                Assert.That(result.Classifications, Is.EqualTo(expectedClassifications));
                Assert.That(PartIndexUtils.Expand(result.PartIndices, result.Count), Is.EqualTo(expectedParts));
                Assert.That(result.PartIndexRange, Is.EqualTo(new Range1i(7, 9)));
                Assert.That(result.BoundingBox, Is.EqualTo(expectedBounds));
            }

            Assert.Multiple(() =>
            {
                Assert.That(a.Positions, Is.EqualTo(originalPositionsA));
                Assert.That(a.Colors, Is.EqualTo(originalColorsA));
                Assert.That(a.Normals, Is.EqualTo(originalNormalsA));
                Assert.That(a.Intensities, Is.EqualTo(originalIntensitiesA));
                Assert.That(a.Classifications, Is.EqualTo(originalClassificationsA));
                Assert.That(PartIndexUtils.Expand(a.PartIndices, a.Count), Is.EqualTo(originalPartsA));
                Assert.That(a.BoundingBox, Is.EqualTo(boundsA));
                Assert.That(b.BoundingBox, Is.EqualTo(boundsB));
            });
        }

        private static IEnumerable<TestCaseData> PartIndexCases()
        {
            yield return new TestCaseData(7, 3, 7, 2, typeof(int), new[] { 7, 7, 7, 7, 7 })
                .SetName("PartIndices_EqualIntScalarsStayScalar");
            yield return new TestCaseData((uint)7, 3, (uint)7, 2, typeof(uint), new[] { 7, 7, 7, 7, 7 })
                .SetName("PartIndices_EqualUIntScalarsStayScalar");
            yield return new TestCaseData(1, 2, 2, 2, typeof(byte[]), new[] { 1, 1, 2, 2 })
                .SetName("PartIndices_SmallScalarsCompactToBytes");
            yield return new TestCaseData(300, 2, new byte[] { 1, 2 }, 2, typeof(short[]), new[] { 300, 300, 1, 2 })
                .SetName("PartIndices_ScalarAndBytesPromoteToShorts");
            yield return new TestCaseData(new byte[] { 1, 250 }, 2, new short[] { 300, 2 }, 2, typeof(short[]), new[] { 1, 250, 300, 2 })
                .SetName("PartIndices_BytesAndShortsUseShorts");
            yield return new TestCaseData(new short[] { 1, 300 }, 2, new[] { 40000, 2 }, 2, typeof(int[]), new[] { 1, 300, 40000, 2 })
                .SetName("PartIndices_ShortsAndIntsUseInts");
        }

        [TestCaseSource(nameof(PartIndexCases))]
        public void CompatiblePartIndexRepresentationsRemainCompactAndTyped(
            object firstParts, int firstCount, object secondParts, int secondCount,
            Type expectedType, int[] expectedValues)
        {
            var first = CreatePositionsWithParts(0, firstCount, firstParts);
            var second = CreatePositionsWithParts(firstCount, secondCount, secondParts);
            var results = new[]
            {
                Chunk.ImmutableMerge(first, second),
                Chunk.ImmutableMerge(first, AttributeBearingEmpty(), second),
                first.Union(second),
            };

            foreach (var result in results)
            {
                Assert.That(result.PartIndices, Is.TypeOf(expectedType));
                Assert.That(PartIndexUtils.Expand(result.PartIndices, result.Count), Is.EqualTo(expectedValues));
                Assert.That(result.PartIndexRange, Is.EqualTo(new Range1i(expectedValues)));
            }
        }

        private static Chunk CreatePositionsWithParts(int origin, int count, object partIndices)
            => new(
                Enumerable.Range(origin, count).Select(i => new V3d(i, i * 2, -i)).ToArray(),
                colors: null,
                normals: null,
                intensities: null,
                classifications: null,
                partIndices,
                partIndexRange: null,
                bbox: null
                );

        [Test]
        public void NWayCompositionValidatesEverySchemaBeforeReadingPointData()
        {
            var unreadable = new Chunk(
                new UnreadableList<V3d>(1),
                new UnreadableList<C4b>(1),
                normals: null,
                intensities: null,
                classifications: null,
                partIndices: null,
                partIndexRange: null,
                bbox: new Box3d(V3d.Zero)
                );
            var compatible = Create(10, OptionalProperty.Colors);
            var incompatible = Create(20);

            foreach (var merge in new Func<Chunk>[]
            {
                () => Chunk.ImmutableMerge(unreadable, compatible, incompatible),
                () => Chunk.ImmutableMerge((IEnumerable<Chunk>)new[] { unreadable, compatible, incompatible }),
                () => new[] { unreadable, compatible, incompatible }.AsEnumerable().Union(),
                () => unreadable.ImmutableMergeWith(compatible, incompatible),
                () => unreadable.ImmutableMergeWith((IEnumerable<Chunk>)new[] { compatible, incompatible }),
            })
            {
                var error = Assert.Throws<InvalidOperationException>(() => merge());
                Assert.That(error!.Message, Does.Contain("colors"));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PointCloudChunksSurfacesMergeSmallSchemaMismatch(bool reverse)
        {
            var withColors = Create(0, OptionalProperty.Colors);
            var withoutColors = Create(10);
            var chunks = reverse
                ? new[] { withoutColors, withColors }
                : new[] { withColors, withoutColors };
            var config = ImportConfig.Default
                .WithStorage(PointSetTests.CreateStorage())
                .WithMaxChunkPointCount(1024)
                .WithOctreeSplitLimit(16)
                .WithMinDist(0)
                .WithNormalizePointDensityGlobal(false)
                .WithMaxDegreeOfParallelism(1)
                .WithEnabledPartIndices(false)
                .WithVerbose(false);

            var error = Assert.Throws<InvalidOperationException>(() => PointCloud.Chunks(chunks, config));
            Assert.That(error!.Message, Does.Contain("nonempty chunks"));
            Assert.That(error.Message, Does.Contain("colors"));
        }

        private sealed class UnreadableList<T> : IList<T>
        {
            public UnreadableList(int count) => Count = count;

            public T this[int index]
            {
                get => throw new InvalidOperationException("Point data was read before all schemas were validated.");
                set => throw new NotSupportedException();
            }

            public int Count { get; }
            public bool IsReadOnly => true;
            public void Add(T item) => throw new NotSupportedException();
            public void Clear() => throw new NotSupportedException();
            public bool Contains(T item) => throw new NotSupportedException();
            public void CopyTo(T[] array, int arrayIndex) => throw new InvalidOperationException("Point data was copied before all schemas were validated.");
            public IEnumerator<T> GetEnumerator() => throw new InvalidOperationException("Point data was enumerated before all schemas were validated.");
            public int IndexOf(T item) => throw new NotSupportedException();
            public void Insert(int index, T item) => throw new NotSupportedException();
            public bool Remove(T item) => throw new NotSupportedException();
            public void RemoveAt(int index) => throw new NotSupportedException();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
