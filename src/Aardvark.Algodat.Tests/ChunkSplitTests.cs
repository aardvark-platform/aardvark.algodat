/*
    Copyright (C) 2026. Aardvark Platform Team. http://github.com/aardvark-platform.
    This program is free software under the GNU Affero General Public License,
    version 3 or later. See <http://www.gnu.org/licenses/>.
*/
#nullable enable
using Aardvark.Base;
using Aardvark.Data.Points;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class ChunkSplitTests
    {
        private static readonly string[] Representations =
            { "none", "int", "uint", "byte[]", "short[]", "int[]", "byte-list", "short-list", "int-list" };
        private static readonly string[] Collections =
            { "byte[]", "short[]", "int[]", "byte-list", "short-list", "int-list" };
        private static readonly Range1i SourceRange = new(int.MinValue, int.MaxValue);

        private static object? Parts(string kind, int count) => kind switch
        {
            "none" => null,
            "int" => -17,
            "uint" => 0x80000025u,
            "byte[]" => Enumerable.Range(0, count).Select(i => (byte)(255 - i % 256)).ToArray(),
            "short[]" => Enumerable.Range(0, count).Select(i => (short)(i % 2 == 0 ? short.MinValue + i : short.MaxValue - i)).ToArray(),
            "int[]" => Enumerable.Range(0, count).Select(i => i % 2 == 0 ? int.MinValue + i : int.MaxValue - i).ToArray(),
            "byte-list" => new List<byte>((byte[])Parts("byte[]", count)!),
            "short-list" => new List<short>((short[])Parts("short[]", count)!),
            "int-list" => new List<int>((int[])Parts("int[]", count)!),
            _ => throw new ArgumentException(kind)
        };

        private static Chunk Create(int count, object? parts, int attributes = 15) => new(
            Enumerable.Range(0, count).Select(i => new V3d(i - 3, i % 3 - 2, i * 7 % 11)).ToArray(),
            (attributes & 1) == 0 ? null : Enumerable.Range(0, count).Select(i => new C4b((byte)i, (byte)(i + 1), (byte)(i + 2), (byte)255)).ToArray(),
            (attributes & 2) == 0 ? null : Enumerable.Range(0, count).Select(i => new V3f(i, -i, 1)).ToArray(),
            (attributes & 4) == 0 ? null : Enumerable.Range(0, count).Select(i => 3 * i - 40).ToArray(),
            (attributes & 8) == 0 ? null : Enumerable.Range(0, count).Select(i => (byte)(i % 4)).ToArray(),
            parts, SourceRange, new Box3d(new V3d(-1000), new V3d(1000)));

        private static int[]? Expand(object? parts, int count) => parts switch
        {
            null => null,
            int x => Enumerable.Repeat(x, count).ToArray(),
            uint x => Enumerable.Repeat(unchecked((int)x), count).ToArray(),
            IList<byte> xs => xs.Select(x => (int)x).ToArray(),
            IList<short> xs => xs.Select(x => (int)x).ToArray(),
            IList<int> xs => xs.ToArray(),
            _ => throw new ArgumentException()
        };

        private static void CheckAttribute<T>(IList<T>? source, IList<T>? result, int offset, int count)
        {
            if (source == null) Assert.That(result, Is.Null);
            else
            {
                Assert.That(result, Is.TypeOf<T[]>());
                Assert.That(result, Is.Not.SameAs(source));
                Assert.That(result, Is.EqualTo(source.Skip(offset).Take(count)));
            }
        }

        private static void CheckSlice(Chunk source, Chunk result, int offset, int count)
        {
            Assert.That(result, Is.Not.SameAs(source));
            Assert.That(result.Count, Is.EqualTo(count));
            var positions = source.Positions.Skip(offset).Take(count).ToArray();
            Assert.That(result.Positions, Is.EqualTo(positions));
            Assert.That(result.BoundingBox, Is.EqualTo(new Box3d(positions)));
            CheckAttribute(source.Colors, result.Colors, offset, count);
            CheckAttribute(source.Normals, result.Normals, offset, count);
            CheckAttribute(source.Intensities, result.Intensities, offset, count);
            CheckAttribute(source.Classifications, result.Classifications, offset, count);

            var expected = Expand(source.PartIndices, source.Count)?.Skip(offset).Take(count).ToArray();
            Assert.That(Expand(result.PartIndices, count), Is.EqualTo(expected));
            Assert.That(result.PartIndexRange, Is.EqualTo(expected == null ? (Range1i?)null : new Range1i(expected.Min(), expected.Max())));
            var type = source.PartIndices switch
            {
                null => null,
                int or uint => typeof(int),
                IList<byte> => typeof(byte[]),
                IList<short> => typeof(short[]),
                _ => typeof(int[])
            };
            Assert.That(result.PartIndices?.GetType(), Is.EqualTo(type));
            if (result.PartIndices is Array) Assert.That(result.PartIndices, Is.Not.SameAs(source.PartIndices));
        }

        [Test]
        public void SlicesPreserveAlignmentAndOwnTheirArrays(
            [ValueSource(nameof(Representations))] string kind,
            [Values(1, 4, 6)] int size, [Values(false, true)] bool attributes)
        {
            var source = Create(11, Parts(kind, 11), attributes ? 15 : 0);
            var chunks = source.Split(size).ToArray();
            Assert.That(chunks.Length, Is.EqualTo((source.Count + size - 1) / size));
            for (var i = 0; i < chunks.Length; i++) CheckSlice(source, chunks[i], i * size, Math.Min(size, source.Count - i * size));

            // Mutating an output must not affect either the source or a sibling slice.
            var first = chunks[0];
            first.Positions[0] = new V3d(9999);
            if (first.Colors != null) first.Colors[0] = default;
            if (first.Normals != null) first.Normals[0] = default;
            if (first.Intensities != null) first.Intensities[0] = 9999;
            if (first.Classifications != null) first.Classifications[0] = 255;
            if (first.PartIndices is byte[] b) b[0] = 0;
            if (first.PartIndices is short[] s) s[0] = 0;
            if (first.PartIndices is int[] n) n[0] = 0;
            var original = Create(11, Parts(kind, 11), attributes ? 15 : 0);
            Assert.That(source.Positions, Is.EqualTo(original.Positions));
            Assert.That(source.Colors, Is.EqualTo(original.Colors));
            Assert.That(source.Normals, Is.EqualTo(original.Normals));
            Assert.That(source.Intensities, Is.EqualTo(original.Intensities));
            Assert.That(source.Classifications, Is.EqualTo(original.Classifications));
            Assert.That(Expand(source.PartIndices, source.Count), Is.EqualTo(Expand(original.PartIndices, original.Count)));
            Assert.That(source.BoundingBox, Is.EqualTo(original.BoundingBox));
            Assert.That(source.PartIndexRange, Is.EqualTo(SourceRange));
            CheckSlice(source, chunks[1], size, Math.Min(size, source.Count - size));
        }

        [Test]
        public void OptionalAttributesAreIndependent([Range(0, 15)] int mask)
        {
            var source = Create(7, Parts("int-list", 7), mask);
            var result = source.Split(4).ToArray();
            CheckSlice(source, result[0], 0, 4);
            CheckSlice(source, result[1], 4, 3);
        }

        [Test]
        public void WholeChunkIdentityIncludesEmptyChunks(
            [ValueSource(nameof(Representations))] string kind, [Values(0, 7)] int count,
            [Values(false, true)] bool large)
        {
            var source = Create(count, Parts(kind, count));
            var result = source.Split(large ? int.MaxValue : Math.Max(1, count)).ToArray();
            Assert.That(result, Has.Length.EqualTo(1));
            Assert.That(result[0], Is.SameAs(source));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(int.MinValue)]
        public void InvalidSizesAreRejectedOnlyOnEnumeration(int size)
        {
            foreach (var source in new[] { Chunk.Empty, Create(4, null) })
            {
                var sequence = source.Split(size);
                using var iterator = sequence.GetEnumerator();
                Assert.Throws<Exception>(() => iterator.MoveNext());
            }
        }

        [TestCase(0u)]
        [TestCase(42u)]
        [TestCase(uint.MaxValue)]
        public void ScalarUIntKeepsExistingSignedSliceRepresentation(uint value)
        {
            var source = Create(5, value);
            foreach (var output in source.Split(2))
            {
                Assert.That(output.PartIndices, Is.TypeOf<int>().And.EqualTo(unchecked((int)value)));
                Assert.That(output.PartIndexRange, Is.EqualTo(new Range1i(unchecked((int)value))));
            }
            Assert.That(source.Split(5).Single().PartIndices, Is.TypeOf<uint>().And.EqualTo(value));
        }

        private static void CheckReadCounts<T>(T[] data)
        {
            var parts = new CountingList<T>(data);
            // Explicit metadata avoids unrelated constructor range calculation on the source list.
            var source = Create(data.Length, parts, 0);
            var sequence = source.Split(64);
            using (var iterator = sequence.GetEnumerator())
            {
                Assert.That(parts.Reads.Sum(), Is.Zero);
                Assert.That(iterator.MoveNext(), Is.True);
                Assert.That(parts.Reads.Take(64), Is.All.EqualTo(1));
                Assert.That(parts.Reads.Skip(64), Is.All.Zero);
                Assert.That(parts.Reads.Sum(), Is.EqualTo(64));
                var count = iterator.Current.Count;
                while (iterator.MoveNext()) count += iterator.Current.Count;
                Assert.That(count, Is.EqualTo(4096));
                Assert.That(parts.Reads, Is.All.EqualTo(1));
            }
            Array.Clear(parts.Reads, 0, parts.Reads.Length);
            Assert.That(sequence.Take(1).Single().Count, Is.EqualTo(64));
            Assert.That(parts.Reads.Sum(), Is.EqualTo(64), "Abandoning the sequence must not read a suffix.");
        }

        [Test] public void ByteListReadsOnlyConsumedRanges() => CheckReadCounts((byte[])Parts("byte[]", 4096)!);
        [Test] public void ShortListReadsOnlyConsumedRanges() => CheckReadCounts((short[])Parts("short[]", 4096)!);
        [Test] public void IntListReadsOnlyConsumedRanges() => CheckReadCounts((int[])Parts("int[]", 4096)!);

        [Test]
        public void PendingSlicesReadValuesLazilyAndRepeatedEnumerationIsIndependent()
        {
            var parts = new List<int>(Enumerable.Range(0, 8));
            var source = Create(8, parts);
            var sequence = source.Split(3);
            using var iterator = sequence.GetEnumerator();
            Assert.That(iterator.MoveNext(), Is.True);
            var first = iterator.Current;
            parts[3] = 1234;
            Assert.That(iterator.MoveNext(), Is.True);
            Assert.That(iterator.Current.PartIndices, Is.EqualTo(new[] { 1234, 4, 5 }));
            Assert.That(iterator.Current.PartIndexRange, Is.EqualTo(new Range1i(4, 1234)));
            Assert.That(first.PartIndices, Is.EqualTo(new[] { 0, 1, 2 }));
            var repeated = sequence.ToArray();
            Assert.That(repeated[0], Is.Not.SameAs(first));
            Assert.That(repeated[0].PartIndices, Is.Not.SameAs(first.PartIndices));
            CheckSlice(source, repeated[2], 6, 2);
        }

        private static long Allocated(Chunk source, bool firstOnly)
        {
            static int Consume(Chunk chunk, bool first)
            {
                using var iterator = chunk.Split(64).GetEnumerator();
                var count = 0;
                while (iterator.MoveNext())
                {
                    count += iterator.Current.Count;
                    if (first) break;
                }
                return count;
            }
            for (var i = 0; i < 8; i++) Consume(source, firstOnly);
            var before = GC.GetAllocatedBytesForCurrentThread();
            var total = 0;
            for (var i = 0; i < 8; i++) total += Consume(source, firstOnly);
            var bytes = (GC.GetAllocatedBytesForCurrentThread() - before) / 8;
            Assert.That(total, Is.EqualTo(8 * (firstOnly ? 64 : source.Count)));
            return bytes;
        }

        [Test]
        public void AllocationScalesWithConsumedPointsNotSourceSuffixes(
            [ValueSource(nameof(Collections))] string kind, [Values(false, true)] bool firstOnly)
        {
            var small = Create(4096, Parts(kind, 4096), 0);
            var count = firstOnly ? 65536 : 8192;
            var large = Create(count, Parts(kind, count), 0);
            var bytes = Allocated(small, firstOnly);
            Assert.That(Allocated(large, firstOnly), Is.LessThanOrEqualTo(firstOnly ? bytes + 1024 : 2 * bytes + 4096));
        }

        private sealed class CountingList<T> : IList<T>
        {
            private readonly T[] data;
            public int[] Reads { get; }
            public CountingList(T[] data) { this.data = data; Reads = new int[data.Length]; }
            public T this[int index] { get { Reads[index]++; return data[index]; } set => throw new NotSupportedException(); }
            public int Count => data.Length;
            public bool IsReadOnly => true;
            public IEnumerator<T> GetEnumerator() { for (var i = 0; i < Count; i++) yield return this[i]; }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            public void CopyTo(T[] array, int offset) { for (var i = 0; i < Count; i++) array[offset + i] = this[i]; }
            public int IndexOf(T item) => Array.IndexOf(data, item);
            public bool Contains(T item) => IndexOf(item) >= 0;
            public void Add(T item) => throw new NotSupportedException();
            public void Clear() => throw new NotSupportedException();
            public void Insert(int index, T item) => throw new NotSupportedException();
            public bool Remove(T item) => throw new NotSupportedException();
            public void RemoveAt(int index) => throw new NotSupportedException();
        }
    }
}
