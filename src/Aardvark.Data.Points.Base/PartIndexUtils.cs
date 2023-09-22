/*
   Aardvark Platform
   Copyright (C) 2006-2023  Aardvark Platform Team
   https://aardvark.graphics

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/
using Aardvark.Base;
using System;
using System.Collections.Generic;
using System.Linq;

#pragma warning disable CS1591

namespace Aardvark.Data.Points;

/// <summary>
/// Utils for part indices.
/// </summary>
public static class PartIndexUtils
{
    /// <summary>
    /// Returns union of two part indices (uint, IList of [byte|short|int]).
    /// </summary>
    public static object? Union(object? first, object? second)
    {
        checked
        {
            return (first, second) switch
            {
                (null, null) => null,
                (object x, null) => x,
                (null, object y) => y,
                (uint x, uint y) => (x == y) ? x : new Range1i(new[] { (int)x, (int)y }),

                (uint x, IList<byte> ys) => ((Range1i)new Range1b(ys)).ExtendedBy((int)x),
                (uint x, IList<short> ys) => ((Range1i)new Range1s(ys)).ExtendedBy((int)x),
                (uint x, IList<int> ys) => new Range1i(ys).ExtendedBy((int)x),

                (IList<byte> xs, uint y) => ((Range1i)new Range1b(xs)).ExtendedBy((int)y),
                (IList<short> xs, uint y) => ((Range1i)new Range1s(xs)).ExtendedBy((int)y),
                (IList<int> xs, uint y) => new Range1i(xs).ExtendedBy((int)y),

                (IList<byte> xs, IList<byte> ys) => (Range1i)new Range1b(xs.Concat(ys)),
                (IList<byte> xs, IList<short> ys) => (Range1i)new Range1s(xs.Select(x => (short)x).Concat(ys)),
                (IList<byte> xs, IList<int> ys) => new Range1i(xs.Select(x => (int)x).Concat(ys)),

                (IList<short> xs, IList<byte> ys) => (Range1i)new Range1s(xs.Concat(ys.Select(x => (short)x))),
                (IList<short> xs, IList<short> ys) => (Range1i)new Range1s(xs.Concat(ys)),
                (IList<short> xs, IList<int> ys) => new Range1i(xs.Select(x => (int)x).Concat(ys)),

                (IList<int> xs, IList<byte> ys) => new Range1i(xs.Concat(ys.Select(x => (int)x))),
                (IList<int> xs, IList<short> ys) => new Range1i(xs.Concat(ys.Select(x => (int)x))),
                (IList<int> xs, IList<int> ys) => new Range1i(xs.Concat(ys)),

                _ => throw new Exception(
                    $"Unexpected part indices types {first.GetType().FullName} and {second.GetType().FullName}. " +
                    $"Error 2f0672f5-8c6b-400b-8172-e83a30d70c28"
                    )
            };
        }
    }

    /// <summary>
    /// Returns union of part indices (uint, IList of [byte|short|int]).
    /// </summary>
    public static object? Union(params object?[] xs)
    {
        if (xs.Length == 0) return null;

        var result = xs[0];
        for (var i = 1; i < xs.Length; i++) result = Union(result, xs[i]);
        return result;
    }

    /// <summary>
    /// Returns union of part indices (uint, IList of [byte|short|int]).
    /// </summary>
    public static object? Union(IEnumerable<object?> xs)
    {
        var result = xs.FirstOrDefault();
        foreach (var x in xs.Skip(1)) result = Union(result, x);
        return result;
    }

    public static Range1i ExtendedBy(in Range1i range, object? partIndices)
    {
        checked
        {
            return partIndices switch
            {
                null => range,
                uint x => range.ExtendedBy((int)x),
                IList<byte> xs => range.ExtendedBy((Range1i)new Range1b(xs)),
                IList<short> xs => range.ExtendedBy((Range1i)new Range1s(xs)),
                IList<int> xs => range.ExtendedBy(new Range1i(xs)),

                _ => throw new Exception(
                    $"Unexpected part indices type {partIndices.GetType().FullName}. " +
                    $"Error 3915b996-1842-4418-9445-e4c4f1b678a6"
                    )
            };
        }
    }

    /// <summary>
    /// Skips n part indices and returns the remaining ones.
    /// </summary>
    public static object? Skip(object? partIndices, int n) => partIndices switch
    {
        null => null,
        uint x => x,
        IList<byte> xs => xs.Skip(n).ToArray(),
        IList<short> xs => xs.Skip(n).ToArray(),
        IList<int> xs => xs.Skip(n).ToArray(),

        _ => throw new Exception(
            $"Unexpected part indices type {partIndices.GetType().FullName}. " +
            $"Error 792d520b-fa95-4760-a2e8-3fecca773337"
            )
    };

    /// <summary>
    /// Returns the first n part indices.
    /// </summary>
    public static object? Take(object? partIndices, int n) => partIndices switch
    {
        null => null,
        uint x => x,
        IList<byte> xs => xs.Take(n).ToArray(),
        IList<short> xs => xs.Take(n).ToArray(),
        IList<int> xs => xs.Take(n).ToArray(),

        _ => throw new Exception(
            $"Unexpected part indices type {partIndices.GetType().FullName}. " +
            $"Error 49e1b5e2-5380-4ce3-a2d2-b80fec39337f"
            )
    };

    internal static object? Subset(object? partIndices, IReadOnlyList<int> subsetIndices) => partIndices switch
    {
        null => null,
        uint x => x,
        IList<byte> xs => subsetIndices.MapToArray(i => xs[i]),
        IList<short> xs => subsetIndices.MapToArray(i => xs[i]),
        IList<int> xs => subsetIndices.MapToArray(i => xs[i]),

        _ => throw new Exception(
            $"Unexpected part indices type {partIndices.GetType().FullName}. " +
            $"Error 37b3981f-ea67-489c-8fc9-354c01057792"
            )
    };
}