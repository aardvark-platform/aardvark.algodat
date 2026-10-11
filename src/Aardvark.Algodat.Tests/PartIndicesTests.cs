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
using System.Collections;
using System.Collections.Generic;

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

    private enum SourceKind { Repeatable, SingleUse, Consumptive, Changing }

    private sealed class IndexSource : IEnumerable<(object indices, int count)>
    {
        private readonly (object indices, int count)[] values;
        private readonly SourceKind kind;
        private int next;
        public int Opens, Moves, Reads, Disposals, Active;
        public int FailMoveAt = -1, FailCurrentAt = -1;
        public bool FailOpen;
        public readonly Exception Failure = new InvalidOperationException("Injected source failure.");

        public IndexSource((object indices, int count)[] values, SourceKind kind = SourceKind.Repeatable)
        {
            this.values = values;
            this.kind = kind;
        }

        public IEnumerator<(object indices, int count)> GetEnumerator()
        {
            Opens++;
            if (FailOpen || (kind == SourceKind.SingleUse && Opens > 1)) throw Failure;
            Active++;
            return new Cursor(this, Opens);
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private sealed class Cursor : IEnumerator<(object indices, int count)>
        {
            private readonly IndexSource source;
            private readonly int pass;
            private int position = -1;
            public Cursor(IndexSource source, int pass) { this.source = source; this.pass = pass; }
            public (object indices, int count) Current
            {
                get
                {
                    source.Reads++;
                    if (position == source.FailCurrentAt) throw source.Failure;
                    var value = source.values[position];
                    return source.kind == SourceKind.Changing && pass > 1 && value.indices is int x
                        ? (x + 1000, value.count) : value;
                }
            }
            object IEnumerator.Current => Current;
            public bool MoveNext()
            {
                source.Moves++;
                position = source.kind == SourceKind.Consumptive ? source.next++ : position + 1;
                if (position == source.FailMoveAt) throw source.Failure;
                return position < source.values.Length;
            }
            public void Dispose() { source.Disposals++; source.Active--; }
            public void Reset() => throw new NotSupportedException();
        }
    }

    private static void CheckEnumeration(IndexSource source, int moves, int reads, int disposals, string context)
    {
        Assert.That(source.Opens, Is.EqualTo(1), $"GetEnumerator: {context}");
        Assert.That(source.Moves, Is.EqualTo(moves), $"MoveNext: {context}");
        Assert.That(source.Reads, Is.EqualTo(reads), $"Current: {context}");
        Assert.That(source.Disposals, Is.EqualTo(disposals), $"Dispose: {context}");
        Assert.That(source.Active, Is.Zero, $"No retained enumerator: {context}");
    }

    [Test]
    public void EnumerableConcatConsumesSingleUseConsumptiveAndChangingSourcesOnce()
    {
        var segments = new (object, int)[] { (2, 2), (7, 2), (300, 3), (70000, 1) };
        var expected = new[] { 2, 2, 7, 7, 300, 300, 300, 70000 };
        Assert.Multiple(() =>
        {
            foreach (var kind in new[] { SourceKind.SingleUse, SourceKind.Consumptive, SourceKind.Changing, SourceKind.Repeatable })
            {
                var source = new IndexSource(segments, kind);
                object actual = null;
                var context = $"source={kind}, counts=2/2/3/1";
                Assert.That(() => actual = PartIndexUtils.ConcatIndices(source), Throws.Nothing, context);
                Assert.That(actual?.GetType(), Is.EqualTo(typeof(int[])), context);
                Assert.That(actual, Is.EqualTo(expected), context);
                CheckEnumeration(source, segments.Length + 1, segments.Length, 1, context);
            }
        });
    }

    [Test]
    public void EnumerableConcatPreservesEmptySingletonAndRepresentationSemantics()
    {
        var error = Assert.Throws<ArgumentNullException>(() =>
            PartIndexUtils.ConcatIndices((IEnumerable<(object, int)>)null));
        Assert.That(error.ParamName, Is.EqualTo("source"), "Retain the original LINQ null-argument contract.");
        Assert.Multiple(() =>
        {
            foreach (var value in new object[] { null, -7, 42u, uint.MaxValue,
                new byte[] { 2, 3 }, new short[] { -1, 300 }, new[] { -7, 70000 }, new object() })
            {
                var source = new IndexSource(new[] { (value, -3) });
                var actual = PartIndexUtils.ConcatIndices(source);
                var context = $"singleton={value?.GetType().Name ?? "null"}, declared count=-3";
                Assert.That(actual, Is.SameAs(value), context);
                CheckEnumeration(source, 2, 1, 1, context);
            }
            foreach (var (name, segments, expected) in new (string, (object, int)[], object)[]
            {
                ("empty", Array.Empty<(object, int)>(), null),
                ("nulls", new (object, int)[] { (null, 4), (null, 2) }, null),
                ("equal unsigned scalars", new (object, int)[] { (42u, 2), (42u, 3) }, 42u),
                ("accumulated scalar count", new (object, int)[] { (4, 2), (4, 3), (7, 1) }, new byte[] { 4, 4, 4, 4, 4, 7 }),
                ("leading null count", new (object, int)[] { (null, 2), (4, 1), (7, 2) }, new byte[] { 4, 4, 4, 7, 7 }),
                ("intermediate null count", new (object, int)[] { (4, 2), (null, 3), (7, 1) }, new byte[] { 4, 4, 4, 4, 4, 7 }),
                ("zero counts", new (object, int)[] { (2, 0), (3, 2), (4, 0) }, new byte[] { 3, 3 }),
                ("byte scalars", new (object, int)[] { (1, 2), (255, 1) }, new byte[] { 1, 1, 255 }),
                ("short scalars", new (object, int)[] { (256, 2), (32767, 1) }, new short[] { 256, 256, 32767 }),
                ("int scalars", new (object, int)[] { (32768, 2), (int.MaxValue, 1) }, new[] { 32768, 32768, int.MaxValue }),
                ("unsigned promotion", new (object, int)[] { (256u, 2), (new byte[] { 1, 2 }, 2) }, new short[] { 256, 256, 1, 2 }),
                ("mixed widths", new (object, int)[] { (3, 2), (new byte[] { 4, 5 }, 2), (new short[] { 300 }, 1),
                    (70000, 1), (new[] { -7, 12 }, 2) }, new[] { 3, 3, 4, 5, 300, 70000, -7, 12 }),
                ("array lengths override declared counts", new (object, int)[] { (new byte[] { 2, 3 }, 99),
                    (new byte[] { 4, 5 }, -88), (7, 1) }, new byte[] { 2, 3, 4, 5, 7 })
            })
            {
                var source = new IndexSource(segments);
                var actual = PartIndexUtils.ConcatIndices(source);
                Assert.That(actual?.GetType(), Is.EqualTo(expected?.GetType()), name);
                Assert.That(actual, Is.EqualTo(expected), name);
                CheckEnumeration(source, segments.Length + 1, segments.Length, 1, name);
            }
        });
    }

    [Test]
    public void EnumerableConcatDisposesOnSourceAndBinaryFailures()
    {
        var segments = new (object, int)[] { (2, 2), (7, 1), (300, 1), (4, 1) };
        Assert.Multiple(() =>
        {
            foreach (var phase in new[] { "open", "first move", "later move", "first current", "later current" })
            {
                var source = new IndexSource(segments)
                {
                    FailOpen = phase == "open",
                    FailMoveAt = phase == "first move" ? 0 : phase == "later move" ? 2 : -1,
                    FailCurrentAt = phase == "first current" ? 0 : phase == "later current" ? 2 : -1
                };
                var actual = Assert.Catch(() => PartIndexUtils.ConcatIndices(source), phase);
                Assert.That(actual, Is.SameAs(source.Failure), phase);
                var moves = phase == "open" ? 0 : phase.StartsWith("first") ? 1 : 3;
                var reads = phase.Contains("move") ? moves - 1 : moves;
                CheckEnumeration(source, moves, reads, phase == "open" ? 0 : 1, phase);
            }
            foreach (var overflow in new[] { false, true })
            {
                var source = new IndexSource(new (object, int)[] {
                    (overflow ? 1 : new object(), overflow ? int.MaxValue : 1), (2, 1), (3, 1) });
                var actual = Assert.Catch(() => PartIndexUtils.ConcatIndices(source), $"binary overflow={overflow}");
                Assert.That(actual?.GetType(), Is.EqualTo(overflow ? typeof(OverflowException) : typeof(Exception)));
                if (!overflow) Assert.That(actual.Message, Does.Contain("ee1933cb-f9d9-4cea-8bd8-ab702f1a8b97"));
                CheckEnumeration(source, 2, 2, 1, $"Binary failure does not consume the tail; overflow={overflow}");
            }
        });
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
