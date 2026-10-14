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
using System.Collections.Generic;
using System.Linq;

#pragma warning disable IDE1006 // Naming Styles

namespace Aardvark.Geometry.Tests;

[TestFixture]
public class PartIndicesTests
{
    private static bool cmp<T>(IReadOnlyList<T> xs, IReadOnlyList<T> ys) where T : IEquatable<T>
    {
        if (xs.Count != ys.Count) return false;
        for (var i = 0; i < xs.Count; i++) if (!xs[i].Equals(ys[i])) return false;
        return true;
    }

    private static IEnumerable<(string Name, object Array, int[] Values)> ReaderData()
    {
        yield return ("byte boundaries", new byte[] { 255, 0, 128, 127, 1, 254 }, new[] { 255, 0, 128, 127, 1, 254 });
        yield return ("short signed boundaries", new short[] { short.MaxValue, -1, 0, 255, 256, short.MinValue },
            new[] { (int)short.MaxValue, -1, 0, 255, 256, short.MinValue });
        yield return ("int signed boundaries", new[] { int.MaxValue, -32769, 32768, -1, 0, 255, 256, int.MinValue },
            new[] { int.MaxValue, -32769, 32768, -1, 0, 255, 256, int.MinValue });
        yield return ("empty byte", Array.Empty<byte>(), Array.Empty<int>());
        yield return ("empty short", Array.Empty<short>(), Array.Empty<int>());
        yield return ("empty int", Array.Empty<int>(), Array.Empty<int>());
        yield return ("constant byte", new byte[] { 42, 42 }, new[] { 42, 42 });
        yield return ("constant short", new short[] { 42, 42 }, new[] { 42, 42 });
        yield return ("constant int", new[] { 42, 42 }, new[] { 42, 42 });
    }

    private static IEnumerable<(string Name, object Source)> Lists(object array) => array switch
    {
        byte[] xs => Lists(xs), short[] xs => Lists(xs), int[] xs => Lists(xs),
        _ => throw new ArgumentException(nameof(array))
    };

    private static IEnumerable<(string Name, object Source)> Lists<T>(T[] values)
    {
        yield return ("List", new List<T>(values));
        yield return ("read-only wrapper", Array.AsReadOnly(values));
        yield return ("IList only", new ListOnly<T>(values));
    }

    private sealed class ListOnly<T> : IList<T>
    {
        private readonly T[] values;
        public ListOnly(T[] values) { this.values = values; }
        public T this[int index] { get => values[index]; set => throw new NotSupportedException(); }
        public int Count => values.Length;
        public bool IsReadOnly => true;
        public int IndexOf(T item) => Array.IndexOf(values, item);
        public bool Contains(T item) => IndexOf(item) >= 0;
        public void CopyTo(T[] array, int index) => values.CopyTo(array, index);
        public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)values).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        public void Add(T item) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
        public void Insert(int index, T item) => throw new NotSupportedException();
        public bool Remove(T item) => throw new NotSupportedException();
        public void RemoveAt(int index) => throw new NotSupportedException();
    }

    private static Range1i ExpectedRange(int[] values) => values.Length == 0
        ? new Range1i(int.MaxValue, int.MinValue) : new Range1i(values.Min(), values.Max());

    private static int[] Snapshot(object source) => source switch
    {
        IList<byte> xs => xs.Select(x => (int)x).ToArray(),
        IList<short> xs => xs.Select(x => (int)x).ToArray(),
        IList<int> xs => xs.ToArray(),
        _ => throw new ArgumentException(nameof(source))
    };

    [Test]
    public void ListReadersMatchArrayRangesExpansionAndIndexedValues()
    {
        Assert.Multiple(() =>
        {
            foreach (var data in ReaderData())
            foreach (var list in Lists(data.Array))
            {
                var context = $"{data.Name}, {list.Name}";
                Assert.DoesNotThrow(() =>
                    Assert.That(PartIndexUtils.GetRange(list.Source), Is.EqualTo(ExpectedRange(data.Values)), context + ": range"), context + ": range");
                Assert.DoesNotThrow(() =>
                    Assert.That(PartIndexUtils.ExtendRangeBy(null, list.Source), Is.EqualTo(ExpectedRange(data.Values)), context + ": null-seed range"), context + ": null-seed range");
                foreach (var count in new[] { -1, 0, 1, data.Values.Length + 3 })
                    Assert.DoesNotThrow(() =>
                    {
                        var expanded = PartIndexUtils.Expand(list.Source, count);
                        Assert.That(expanded, Is.EqualTo(data.Values), context + $": expansion count={count}");
                        Assert.That(expanded, Is.Not.SameAs(list.Source), context + ": owned expansion");
                        if (expanded.Length > 0) expanded[0] = expanded[0] == 0 ? 1 : 0;
                        Assert.That(Snapshot(list.Source), Is.EqualTo(data.Values), context + ": source unchanged");
                    }, context + $": expansion count={count}");
                for (var index = 0; index < data.Values.Length; index++)
                    Assert.DoesNotThrow(() =>
                        Assert.That(PartIndexUtils.Get(list.Source, index), Is.EqualTo(data.Values[index]), context + $": index={index}"), context + $": index={index}");
                foreach (var index in new[] { -1, data.Values.Length })
                {
                    if (list.Name == "IList only")
                        Assert.Throws<IndexOutOfRangeException>(() => PartIndexUtils.Get(list.Source, index), context + $": invalid index={index}");
                    else
                        Assert.Throws<ArgumentOutOfRangeException>(() => PartIndexUtils.Get(list.Source, index), context + $": invalid index={index}");
                }
            }
        });
    }

    [Test]
    public void ListBackedChunksKeepAutomaticAndExplicitRangeReads()
    {
        Assert.Multiple(() =>
        {
            foreach (var data in ReaderData())
            foreach (var list in Lists(data.Array))
            {
                var context = $"{data.Name}, {list.Name}";
                var positions = Enumerable.Range(0, data.Values.Length).Select(i => new V3d(i, 1, 2)).ToArray();
                foreach (var explicitRange in new[] { false, true })
                {
                    var supplied = new Range1i(-123, 456);
                    Assert.DoesNotThrow(() =>
                    {
                        var chunk = new Chunk(positions, null, null, null, null, list.Source, explicitRange ? supplied : null, null);
                        Assert.That(chunk.PartIndices, Is.SameAs(list.Source), context + ": retained source");
                        Assert.That(chunk.Positions, Is.SameAs(positions), context + ": positions preserved");
                        Assert.That(chunk.PartIndexRange, Is.EqualTo(explicitRange ? supplied : ExpectedRange(data.Values)), context + ": range");
                        Assert.That(chunk.TryGetPartIndices(), Is.EqualTo(data.Values), context + ": expanded reads");
                        Assert.That(Snapshot(list.Source), Is.EqualTo(data.Values), context + ": immutable source");
                    }, context + $", explicitRange={explicitRange}");
                }
            }
        });
    }

    [Test]
    public void ExistingArrayScalarAndFailureContractsRemainUnchanged()
    {
        Assert.Multiple(() =>
        {
            foreach (var data in ReaderData())
            {
                Assert.That(PartIndexUtils.GetRange(data.Array), Is.EqualTo(ExpectedRange(data.Values)), data.Name);
                foreach (var count in new[] { -1, 0, data.Values.Length + 3 })
                {
                    var expanded = PartIndexUtils.Expand(data.Array, count);
                    Assert.That(expanded, Is.EqualTo(data.Values), data.Name + $": count={count}");
                    Assert.That(ReferenceEquals(expanded, data.Array), Is.EqualTo(data.Array is int[]), data.Name + ": int-array identity");
                }
                for (var index = 0; index < data.Values.Length; index++)
                    Assert.That(PartIndexUtils.Get(data.Array, index), Is.EqualTo(data.Values[index]), data.Name + $": index={index}");
                Assert.Throws<IndexOutOfRangeException>(() => PartIndexUtils.Get(data.Array, data.Values.Length), data.Name);
                var seed = new Range1i(-7, 11);
                foreach (var list in Lists(data.Array))
                    Assert.That(PartIndexUtils.ExtendRangeBy(seed, list.Source), Is.EqualTo(PartIndexUtils.ExtendRangeBy(seed, data.Array)), data.Name + ": seeded range control");
            }
            foreach (var index in new[] { -1, 0, int.MaxValue })
            {
                Assert.That(PartIndexUtils.Get(null, index), Is.Null, $"null index={index}");
                Assert.That(PartIndexUtils.Get(-42, index), Is.EqualTo(-42), $"int index={index}");
                Assert.That(PartIndexUtils.Get(uint.MaxValue, index), Is.EqualTo(-1), $"uint index={index}");
            }
            Assert.That(PartIndexUtils.GetRange((object)null), Is.Null);
            Assert.That(PartIndexUtils.GetRange(-42), Is.EqualTo(new Range1i(-42)));
            Assert.That(PartIndexUtils.GetRange(uint.MaxValue), Is.EqualTo(new Range1i(-1)));
            Assert.That(PartIndexUtils.Expand(null, -1), Is.Null);
            Assert.That(PartIndexUtils.Expand(-42, 3), Is.EqualTo(new[] { -42, -42, -42 }));
            Assert.That(PartIndexUtils.Expand(255u, 3), Is.EqualTo(new[] { 255, 255, 255 }));
            Assert.That(PartIndexUtils.Expand(42, 0), Is.Empty);
            Assert.Throws<OverflowException>(() => PartIndexUtils.Expand(uint.MaxValue, 1));
            Assert.Throws<OverflowException>(() => PartIndexUtils.Expand(42, -1));
            foreach (var source in new object[] { "unsupported", new long[] { 1 }, new List<uint> { 1 }, Enumerable.Range(0, 3).Where(x => x % 2 == 0) })
            {
                Assert.That(Assert.Throws<Exception>(() => PartIndexUtils.GetRange(source)).Message, Does.Contain("8a975735-bb10-42bf-9f7f-4d570e5223e3"));
                Assert.That(Assert.Throws<Exception>(() => PartIndexUtils.Expand(source, 1)).Message, Does.Contain("278c88f6-d504-4a17-9752-8cca614505f1"));
                Assert.That(Assert.Throws<Exception>(() => PartIndexUtils.Get(source, 0)).Message, Does.Contain("98f41e6c-6065-4dd3-aa9e-6619cc71873d"));
            }
        });
    }

    private static long Allocated<T>(Func<T> action)
    {
        T result = default;
        for (var i = 0; i < 128; i++) result = action();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 256; i++) result = action();
        var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(result);
        return bytes;
    }

    [Test]
    public void ListExpansionAllocatesOnlyItsOutputAndRangeScratchDoesNotScale()
    {
        foreach (var array in new object[] { new byte[4096], new short[4096], new int[4096] })
        {
            var small = Array.CreateInstance(array.GetType().GetElementType(), 4);
            var smallLists = Lists(small).ToArray();
            var lists = Lists(array).ToArray();
            for (var i = 0; i < lists.Length; i++)
            {
                var context = $"{array.GetType().Name}, {lists[i].Name}";
                Assert.That(Allocated(() => PartIndexUtils.Expand(lists[i].Source, -1)),
                    Is.EqualTo(Allocated(() => new int[4096])), context + ": only output array allocated");
                Assert.That(Allocated(() => PartIndexUtils.GetRange(lists[i].Source)),
                    Is.EqualTo(Allocated(() => PartIndexUtils.GetRange(smallLists[i].Source))), context + ": no size-dependent range materialization");
            }
        }
    }

    [Test]
    public void ConcatIndices_nulls()
    {
        { ClassicAssert.True(PartIndexUtils.ConcatIndices(first: null, firstCount: 0, second: null, secondCount: 0) is null); }
        { ClassicAssert.True(PartIndexUtils.ConcatIndices(first: null, firstCount: 0, second: 1u, secondCount: 3) is uint x && x == 1u); }
        { ClassicAssert.True(PartIndexUtils.ConcatIndices(first: 1u, firstCount: 3, second: null, secondCount: 0) is uint x && x == 1u); }
    }

    [Test]
    public void ConcatIndices_single_identical()
    {
        { ClassicAssert.True(PartIndexUtils.ConcatIndices(first:     1u, firstCount: 2, second:     1u, secondCount: 3) is uint x && x == 1u    ); }
        { ClassicAssert.True(PartIndexUtils.ConcatIndices(first:   256u, firstCount: 2, second:   256u, secondCount: 3) is uint x && x == 256u  ); }
        { ClassicAssert.True(PartIndexUtils.ConcatIndices(first: 32768u, firstCount: 2, second: 32768u, secondCount: 3) is uint x && x == 32768u); }
    }
    
    [Test]
    public void ConcatIndices_single_different()
    {
        { ClassicAssert.True(PartIndexUtils.ConcatIndices(first:     1u, firstCount: 2, second:     2u, secondCount: 3) is byte [] xs && cmp(xs, new byte [] { 1, 1, 2, 2, 2 })); }
        { ClassicAssert.True(PartIndexUtils.ConcatIndices(first:     1u, firstCount: 2, second:   257u, secondCount: 3) is short[] xs && cmp(xs, new short[] { 1, 1, 257, 257, 257 })); }
        { ClassicAssert.True(PartIndexUtils.ConcatIndices(first:     1u, firstCount: 2, second: 32769u, secondCount: 3) is int  [] xs && cmp(xs, new int  [] { 1, 1, 32769, 32769, 32769 })); }

        { ClassicAssert.True(PartIndexUtils.ConcatIndices(first:   256u, firstCount: 2, second:     2u, secondCount: 3) is short[] xs && cmp(xs, new short[] { 256, 256, 2, 2, 2 })); }
        { ClassicAssert.True(PartIndexUtils.ConcatIndices(first:   256u, firstCount: 2, second:   257u, secondCount: 3) is short[] xs && cmp(xs, new short[] { 256, 256, 257, 257, 257 })); }
        { ClassicAssert.True(PartIndexUtils.ConcatIndices(first:   256u, firstCount: 2, second: 32769u, secondCount: 3) is int  [] xs && cmp(xs, new int  [] { 256, 256, 32769, 32769, 32769 })); }

        { ClassicAssert.True(PartIndexUtils.ConcatIndices(first: 32768u, firstCount: 2, second:     2u, secondCount: 3) is int  [] xs && cmp(xs, new int  [] { 32768, 32768, 2, 2, 2 })); }
        { ClassicAssert.True(PartIndexUtils.ConcatIndices(first: 32768u, firstCount: 2, second:   257u, secondCount: 3) is int  [] xs && cmp(xs, new int  [] { 32768, 32768, 257, 257, 257 })); }
        { ClassicAssert.True(PartIndexUtils.ConcatIndices(first: 32768u, firstCount: 2, second: 32769u, secondCount: 3) is int  [] xs && cmp(xs, new int  [] { 32768, 32768, 32769, 32769, 32769 })); }
    }

    
    [Test]
    public void ConcatIndices_single_and_array()
    {
        { ClassicAssert.True(PartIndexUtils.ConcatIndices(first:     1u, firstCount: 2, second: new byte [] { 2, 3, 4 }, secondCount: 3) is byte [] xs && cmp(xs, new byte [] { 1, 1, 2, 3, 4 })); }
        { ClassicAssert.True(PartIndexUtils.ConcatIndices(first:     1u, firstCount: 2, second: new short[] { 2, 3, 4 }, secondCount: 3) is short[] xs && cmp(xs, new short[] { 1, 1, 2, 3, 4 })); }
        { ClassicAssert.True(PartIndexUtils.ConcatIndices(first:     1u, firstCount: 2, second: new int  [] { 2, 3, 4 }, secondCount: 3) is int  [] xs && cmp(xs, new int  [] { 1, 1, 2, 3, 4 })); }

        { ClassicAssert.True(PartIndexUtils.ConcatIndices(first:   256u, firstCount: 2, second: new byte [] { 2, 3, 4 }, secondCount: 3) is short[] xs && cmp(xs, new short[] { 256, 256, 2, 3, 4 })); }
        { ClassicAssert.True(PartIndexUtils.ConcatIndices(first:   256u, firstCount: 2, second: new short[] { 2, 3, 4 }, secondCount: 3) is short[] xs && cmp(xs, new short[] { 256, 256, 2, 3, 4 })); }
        { ClassicAssert.True(PartIndexUtils.ConcatIndices(first:   256u, firstCount: 2, second: new int  [] { 2, 3, 4 }, secondCount: 3) is int  [] xs && cmp(xs, new int  [] { 256, 256, 2, 3, 4 })); }

        { ClassicAssert.True(PartIndexUtils.ConcatIndices(first: 32768u, firstCount: 2, second: new byte [] { 2, 3, 4 }, secondCount: 3) is int  [] xs && cmp(xs, new int  [] { 32768, 32768, 2, 3, 4 })); }
        { ClassicAssert.True(PartIndexUtils.ConcatIndices(first: 32768u, firstCount: 2, second: new short[] { 2, 3, 4 }, secondCount: 3) is int  [] xs && cmp(xs, new int  [] { 32768, 32768, 2, 3, 4 })); }
        { ClassicAssert.True(PartIndexUtils.ConcatIndices(first: 32768u, firstCount: 2, second: new int  [] { 2, 3, 4 }, secondCount: 3) is int  [] xs && cmp(xs, new int  [] { 32768, 32768, 2, 3, 4 })); }
    }

    [Test]
    public void ConcatIndices_array_array()
    {
        { ClassicAssert.True(PartIndexUtils.ConcatIndices(first: new byte [] { 2, 3 }, firstCount: 2, second: new byte [] { 4, 5 }, secondCount: 2) is byte [] xs && cmp(xs, new byte [] { 2, 3, 4, 5 })); }
        { ClassicAssert.True(PartIndexUtils.ConcatIndices(first: new byte [] { 2, 3 }, firstCount: 2, second: new short[] { 4, 5 }, secondCount: 2) is short[] xs && cmp(xs, new short[] { 2, 3, 4, 5 })); }
        { ClassicAssert.True(PartIndexUtils.ConcatIndices(first: new byte [] { 2, 3 }, firstCount: 2, second: new int  [] { 4, 5 }, secondCount: 2) is int  [] xs && cmp(xs, new int  [] { 2, 3, 4, 5 })); }
        
        { ClassicAssert.True(PartIndexUtils.ConcatIndices(first: new short[] { 2, 3 }, firstCount: 2, second: new byte [] { 4, 5 }, secondCount: 2) is short[] xs && cmp(xs, new short[] { 2, 3, 4, 5 })); }
        { ClassicAssert.True(PartIndexUtils.ConcatIndices(first: new short[] { 2, 3 }, firstCount: 2, second: new short[] { 4, 5 }, secondCount: 2) is short[] xs && cmp(xs, new short[] { 2, 3, 4, 5 })); }
        { ClassicAssert.True(PartIndexUtils.ConcatIndices(first: new short[] { 2, 3 }, firstCount: 2, second: new int  [] { 4, 5 }, secondCount: 2) is int  [] xs && cmp(xs, new int  [] { 2, 3, 4, 5 })); }
        
        { ClassicAssert.True(PartIndexUtils.ConcatIndices(first: new int  [] { 2, 3 }, firstCount: 2, second: new byte [] { 4, 5 }, secondCount: 2) is int  [] xs && cmp(xs, new int  [] { 2, 3, 4, 5 })); }
        { ClassicAssert.True(PartIndexUtils.ConcatIndices(first: new int  [] { 2, 3 }, firstCount: 2, second: new short[] { 4, 5 }, secondCount: 2) is int  [] xs && cmp(xs, new int  [] { 2, 3, 4, 5 })); }
        { ClassicAssert.True(PartIndexUtils.ConcatIndices(first: new int  [] { 2, 3 }, firstCount: 2, second: new int  [] { 4, 5 }, secondCount: 2) is int  [] xs && cmp(xs, new int  [] { 2, 3, 4, 5 })); }
    }

    [Test]
    public void ExtendRangeBy()
    {
        ClassicAssert.True(PartIndexUtils.ExtendRangeBy(new Range1i(7, 11), 8u) == new Range1i(7, 11));
        ClassicAssert.True(PartIndexUtils.ExtendRangeBy(new Range1i(7, 11), 1u) == new Range1i(1, 11));
        ClassicAssert.True(PartIndexUtils.ExtendRangeBy(new Range1i(7, 11), 42u) == new Range1i(7, 42));

        ClassicAssert.True(PartIndexUtils.ExtendRangeBy(new Range1i(7, 11),  8) == new Range1i(7, 11));
        ClassicAssert.True(PartIndexUtils.ExtendRangeBy(new Range1i(7, 11),  1) == new Range1i(1, 11));
        ClassicAssert.True(PartIndexUtils.ExtendRangeBy(new Range1i(7, 11), 42) == new Range1i(7, 42));

        ClassicAssert.True(PartIndexUtils.ExtendRangeBy(new Range1i(7, 11), new byte[] { 2, 12 }) == new Range1i(2, 12));
        ClassicAssert.True(PartIndexUtils.ExtendRangeBy(new Range1i(7, 11), new byte[] { 8, 10 }) == new Range1i(7, 11));

        ClassicAssert.True(PartIndexUtils.ExtendRangeBy(new Range1i(7, 11), new short[] { 2, 12 }) == new Range1i(2, 12));
        ClassicAssert.True(PartIndexUtils.ExtendRangeBy(new Range1i(7, 11), new short[] { 8, 10 }) == new Range1i(7, 11));

        ClassicAssert.True(PartIndexUtils.ExtendRangeBy(new Range1i(7, 11), new int[] { 2, 12 }) == new Range1i(2, 12));
        ClassicAssert.True(PartIndexUtils.ExtendRangeBy(new Range1i(7, 11), new int[] { 8, 10 }) == new Range1i(7, 11));
    }

    [Test]
    public void ExtendRangeBy_null()
    {
        ClassicAssert.True(PartIndexUtils.ExtendRangeBy(new Range1i(7, 11), null) == new Range1i(7, 11));
    }
}
