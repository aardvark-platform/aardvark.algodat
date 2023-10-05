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
using System;
using System.Collections.Generic;

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
        { Assert.True(PartIndexUtils.ConcatIndices(first: null, firstCount: 0, second: null, secondCount: 0) is null); }
        { Assert.True(PartIndexUtils.ConcatIndices(first: null, firstCount: 0, second: 1u, secondCount: 3) is uint x && x == 1u); }
        { Assert.True(PartIndexUtils.ConcatIndices(first: 1u, firstCount: 3, second: null, secondCount: 0) is uint x && x == 1u); }
    }

    [Test]
    public void ConcatIndices_single_identical()
    {
        { Assert.True(PartIndexUtils.ConcatIndices(first:     1u, firstCount: 2, second:     1u, secondCount: 3) is uint x && x == 1u    ); }
        { Assert.True(PartIndexUtils.ConcatIndices(first:   256u, firstCount: 2, second:   256u, secondCount: 3) is uint x && x == 256u  ); }
        { Assert.True(PartIndexUtils.ConcatIndices(first: 32768u, firstCount: 2, second: 32768u, secondCount: 3) is uint x && x == 32768u); }
    }
    
    [Test]
    public void ConcatIndices_single_different()
    {
        { Assert.True(PartIndexUtils.ConcatIndices(first:     1u, firstCount: 2, second:     2u, secondCount: 3) is byte [] xs && cmp(xs, new byte [] { 1, 1, 2, 2, 2 })); }
        { Assert.True(PartIndexUtils.ConcatIndices(first:     1u, firstCount: 2, second:   257u, secondCount: 3) is short[] xs && cmp(xs, new short[] { 1, 1, 257, 257, 257 })); }
        { Assert.True(PartIndexUtils.ConcatIndices(first:     1u, firstCount: 2, second: 32769u, secondCount: 3) is int  [] xs && cmp(xs, new int  [] { 1, 1, 32769, 32769, 32769 })); }

        { Assert.True(PartIndexUtils.ConcatIndices(first:   256u, firstCount: 2, second:     2u, secondCount: 3) is short[] xs && cmp(xs, new short[] { 256, 256, 2, 2, 2 })); }
        { Assert.True(PartIndexUtils.ConcatIndices(first:   256u, firstCount: 2, second:   257u, secondCount: 3) is short[] xs && cmp(xs, new short[] { 256, 256, 257, 257, 257 })); }
        { Assert.True(PartIndexUtils.ConcatIndices(first:   256u, firstCount: 2, second: 32769u, secondCount: 3) is int  [] xs && cmp(xs, new int  [] { 256, 256, 32769, 32769, 32769 })); }

        { Assert.True(PartIndexUtils.ConcatIndices(first: 32768u, firstCount: 2, second:     2u, secondCount: 3) is int  [] xs && cmp(xs, new int  [] { 32768, 32768, 2, 2, 2 })); }
        { Assert.True(PartIndexUtils.ConcatIndices(first: 32768u, firstCount: 2, second:   257u, secondCount: 3) is int  [] xs && cmp(xs, new int  [] { 32768, 32768, 257, 257, 257 })); }
        { Assert.True(PartIndexUtils.ConcatIndices(first: 32768u, firstCount: 2, second: 32769u, secondCount: 3) is int  [] xs && cmp(xs, new int  [] { 32768, 32768, 32769, 32769, 32769 })); }
    }

    
    [Test]
    public void ConcatIndices_single_and_array()
    {
        { Assert.True(PartIndexUtils.ConcatIndices(first:     1u, firstCount: 2, second: new byte [] { 2, 3, 4 }, secondCount: 3) is byte [] xs && cmp(xs, new byte [] { 1, 1, 2, 3, 4 })); }
        { Assert.True(PartIndexUtils.ConcatIndices(first:     1u, firstCount: 2, second: new short[] { 2, 3, 4 }, secondCount: 3) is short[] xs && cmp(xs, new short[] { 1, 1, 2, 3, 4 })); }
        { Assert.True(PartIndexUtils.ConcatIndices(first:     1u, firstCount: 2, second: new int  [] { 2, 3, 4 }, secondCount: 3) is int  [] xs && cmp(xs, new int  [] { 1, 1, 2, 3, 4 })); }

        { Assert.True(PartIndexUtils.ConcatIndices(first:   256u, firstCount: 2, second: new byte [] { 2, 3, 4 }, secondCount: 3) is short[] xs && cmp(xs, new short[] { 256, 256, 2, 3, 4 })); }
        { Assert.True(PartIndexUtils.ConcatIndices(first:   256u, firstCount: 2, second: new short[] { 2, 3, 4 }, secondCount: 3) is short[] xs && cmp(xs, new short[] { 256, 256, 2, 3, 4 })); }
        { Assert.True(PartIndexUtils.ConcatIndices(first:   256u, firstCount: 2, second: new int  [] { 2, 3, 4 }, secondCount: 3) is int  [] xs && cmp(xs, new int  [] { 256, 256, 2, 3, 4 })); }

        { Assert.True(PartIndexUtils.ConcatIndices(first: 32768u, firstCount: 2, second: new byte [] { 2, 3, 4 }, secondCount: 3) is int  [] xs && cmp(xs, new int  [] { 32768, 32768, 2, 3, 4 })); }
        { Assert.True(PartIndexUtils.ConcatIndices(first: 32768u, firstCount: 2, second: new short[] { 2, 3, 4 }, secondCount: 3) is int  [] xs && cmp(xs, new int  [] { 32768, 32768, 2, 3, 4 })); }
        { Assert.True(PartIndexUtils.ConcatIndices(first: 32768u, firstCount: 2, second: new int  [] { 2, 3, 4 }, secondCount: 3) is int  [] xs && cmp(xs, new int  [] { 32768, 32768, 2, 3, 4 })); }
    }

    [Test]
    public void ConcatIndices_array_array()
    {
        { Assert.True(PartIndexUtils.ConcatIndices(first: new byte [] { 2, 3 }, firstCount: 2, second: new byte [] { 4, 5 }, secondCount: 2) is byte [] xs && cmp(xs, new byte [] { 2, 3, 4, 5 })); }
        { Assert.True(PartIndexUtils.ConcatIndices(first: new byte [] { 2, 3 }, firstCount: 2, second: new short[] { 4, 5 }, secondCount: 2) is short[] xs && cmp(xs, new short[] { 2, 3, 4, 5 })); }
        { Assert.True(PartIndexUtils.ConcatIndices(first: new byte [] { 2, 3 }, firstCount: 2, second: new int  [] { 4, 5 }, secondCount: 2) is int  [] xs && cmp(xs, new int  [] { 2, 3, 4, 5 })); }
        
        { Assert.True(PartIndexUtils.ConcatIndices(first: new short[] { 2, 3 }, firstCount: 2, second: new byte [] { 4, 5 }, secondCount: 2) is short[] xs && cmp(xs, new short[] { 2, 3, 4, 5 })); }
        { Assert.True(PartIndexUtils.ConcatIndices(first: new short[] { 2, 3 }, firstCount: 2, second: new short[] { 4, 5 }, secondCount: 2) is short[] xs && cmp(xs, new short[] { 2, 3, 4, 5 })); }
        { Assert.True(PartIndexUtils.ConcatIndices(first: new short[] { 2, 3 }, firstCount: 2, second: new int  [] { 4, 5 }, secondCount: 2) is int  [] xs && cmp(xs, new int  [] { 2, 3, 4, 5 })); }
        
        { Assert.True(PartIndexUtils.ConcatIndices(first: new int  [] { 2, 3 }, firstCount: 2, second: new byte [] { 4, 5 }, secondCount: 2) is int  [] xs && cmp(xs, new int  [] { 2, 3, 4, 5 })); }
        { Assert.True(PartIndexUtils.ConcatIndices(first: new int  [] { 2, 3 }, firstCount: 2, second: new short[] { 4, 5 }, secondCount: 2) is int  [] xs && cmp(xs, new int  [] { 2, 3, 4, 5 })); }
        { Assert.True(PartIndexUtils.ConcatIndices(first: new int  [] { 2, 3 }, firstCount: 2, second: new int  [] { 4, 5 }, secondCount: 2) is int  [] xs && cmp(xs, new int  [] { 2, 3, 4, 5 })); }
    }

    [Test]
    public void ExtendRangeBy()
    {
        Assert.True(PartIndexUtils.ExtendRangeBy(new Range1i(7, 11), 8u) == new Range1i(7, 11));
        Assert.True(PartIndexUtils.ExtendRangeBy(new Range1i(7, 11), 1u) == new Range1i(1, 11));
        Assert.True(PartIndexUtils.ExtendRangeBy(new Range1i(7, 11), 42u) == new Range1i(7, 42));

        Assert.True(PartIndexUtils.ExtendRangeBy(new Range1i(7, 11),  8) == new Range1i(7, 11));
        Assert.True(PartIndexUtils.ExtendRangeBy(new Range1i(7, 11),  1) == new Range1i(1, 11));
        Assert.True(PartIndexUtils.ExtendRangeBy(new Range1i(7, 11), 42) == new Range1i(7, 42));

        Assert.True(PartIndexUtils.ExtendRangeBy(new Range1i(7, 11), new byte[] { 2, 12 }) == new Range1i(2, 12));
        Assert.True(PartIndexUtils.ExtendRangeBy(new Range1i(7, 11), new byte[] { 8, 10 }) == new Range1i(7, 11));

        Assert.True(PartIndexUtils.ExtendRangeBy(new Range1i(7, 11), new short[] { 2, 12 }) == new Range1i(2, 12));
        Assert.True(PartIndexUtils.ExtendRangeBy(new Range1i(7, 11), new short[] { 8, 10 }) == new Range1i(7, 11));

        Assert.True(PartIndexUtils.ExtendRangeBy(new Range1i(7, 11), new int[] { 2, 12 }) == new Range1i(2, 12));
        Assert.True(PartIndexUtils.ExtendRangeBy(new Range1i(7, 11), new int[] { 8, 10 }) == new Range1i(7, 11));
    }

    [Test]
    public void ExtendRangeBy_Fail()
    {
        Assert.Catch(() => PartIndexUtils.ExtendRangeBy(new Range1i(7, 11), null!));
    }
}
