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
    /// Compacts part indices.
    /// If per-point indices are all identical, then return per-cell index.
    /// If max per-point index fits in a smaller type (e.g. byte), then convert to array of smaller type.
    /// </summary>
    public static object? Compact(object? o)
    {
        switch (o)
        {
            case null: return null;
            case int x: return x;
            case uint x: return (int)x;
            case byte[] xs:
                {
                    if (xs.Length == 0) throw new Exception("Invariant fa0e5cea-c04a-4649-9018-765606529e38.");
                    var range = new Range1b(xs);
                    if (range.Min < 0) throw new Exception("Invariant 46a46203-2525-40c5-95ab-ff6f05f71f55.");
                    return range.Min == range.Max ? (int)range.Min : xs;
                }
            case short[] xs:
                {
                    if (xs.Length == 0) throw new Exception("Invariant 9d18a39b-d19c-4084-95b0-eb30c6a3e38f.");
                    var range = new Range1s(xs);
                    if (range.Min < 0) throw new Exception("Invariant 5d7b3558-e235-4ccc-9b10-2d4217fb8459.");
                    if (range.Min == range.Max) return (int)range.Min;
                    if (range.Max < 256) checked { return xs.Map(x => (byte)x); }
                    return xs;
                }
            case int[] xs:
                {
                    if (xs.Length == 0) throw new Exception("Invariant f60565d1-6cea-47a0-95c2-30625bd16c1b.");
                    var range = new Range1i(xs);
                    if (range.Min < 0) throw new Exception("Invariant 2e002802-dd0b-402b-970b-a49a6decd987.");
                    if (range.Min == range.Max) return (int)range.Min;
                    if (range.Max < 256) checked { return xs.Map(x => (byte)x); }
                    if (range.Max < 32768) checked { return xs.Map(x => (short)x); }
                    return xs;
                }
            default:
                throw new Exception(
                    $"Unexpected type {o.GetType().FullName}. " +
                    $"Invariant 5b5857b3-b389-41d8-ae81-50f6ef3c133e."
                    );
        }
    }

    public static Durable.Def GetDurableDefForPartIndices(object? partIndices) => partIndices switch
    {
        null                 => throw new Exception("Invariant 598ae146-211f-4cee-af57-985eb26ce961."),
        int                  => Durable.Octree.PerCellPartIndex1i,
        uint                 => throw new Exception($"Use PerCellPartIndex1i instead of PerCellPartIndex1ui."), //Durable.Octree.PerCellPartIndex1ui,
        IReadOnlyList<byte>  => Durable.Octree.PerPointPartIndex1b,
        IReadOnlyList<short> => Durable.Octree.PerPointPartIndex1s,
        IReadOnlyList<int>   => Durable.Octree.PerPointPartIndex1i,
        _ => throw new Exception($"Unsupported part indices type {partIndices.GetType().FullName}. Invariant 6700c73d-1842-4fe9-a6b0-28420965cecb.")
    };

    /// <summary>
    /// Get intex-th part index.
    /// </summary>
    public static int? Get(object? o, int index) => o switch
    {
        null       => null,
        int x      => x,
        uint x     => (int)x,
        byte[] xs  => xs[index],
        short[] xs => xs[index],
        int[] xs   => xs[index],
        _ => throw new Exception($"Unexpected type {o.GetType().FullName}. Invariant 98f41e6c-6065-4dd3-aa9e-6619cc71873d.")

    };

    /// <summary>
    /// Concatenates part indices (int, [byte|short|int] array).
    /// </summary>
    public static object? ConcatIndices(
        object? first , int firstCount,
        object? second, int secondCount
        )
    {
        checked
        {
            return (first, second) switch
            {
                (null, null) => null,
                (null, object y) => y,
                (object x, null) => x,

                (int  x, int  y) => (x == y) ? x : createArray1(     x, firstCount,      y, secondCount),
                (int  x, uint y) => (x == y) ? x : createArray1(     x, firstCount, (int)y, secondCount),
                (uint x, int  y) => (x == y) ? x : createArray1((int)x, firstCount,      y, secondCount),
                (uint x, uint y) => (x == y) ? x : createArray1((int)x, firstCount, (int)y, secondCount),

                (int x, _     ) => second switch
                {
                    IReadOnlyList<byte > ys when x <= byte .MaxValue => createArray2((byte )x, firstCount,     ys ),
                    IReadOnlyList<byte > ys when x <= short.MaxValue => createArray2((short)x, firstCount, b2s(ys)),
                    IReadOnlyList<byte > ys when x <= int  .MaxValue => createArray2(       x, firstCount, b2i(ys)),

                    IReadOnlyList<short> ys when x <= short.MaxValue => createArray2((short)x, firstCount,     ys ),
                    IReadOnlyList<short> ys when x <= int  .MaxValue => createArray2(       x, firstCount, s2i(ys)),

                    IReadOnlyList<int  > ys when x <= int  .MaxValue => createArray2(       x, firstCount,     ys ),

                    _ => throw new Exception("Invariant efc1aa79-06d3-4768-8302-6ed743632fe3.")
                },
                (uint x, _     ) => second switch
                {
                    IReadOnlyList<byte > ys when x <= byte .MaxValue => createArray2((byte )x, firstCount,     ys ),
                    IReadOnlyList<byte > ys when x <= short.MaxValue => createArray2((short)x, firstCount, b2s(ys)),
                    IReadOnlyList<byte > ys when x <= int  .MaxValue => createArray2((int  )x, firstCount, b2i(ys)),

                    IReadOnlyList<short> ys when x <= short.MaxValue => createArray2((short)x, firstCount,     ys ),
                    IReadOnlyList<short> ys when x <= int  .MaxValue => createArray2((int  )x, firstCount, s2i(ys)),

                    IReadOnlyList<int  > ys when x <= int  .MaxValue => createArray2((int  )x, firstCount,     ys ),

                    _ => throw new Exception("Invariant 588fea29-4daa-4356-92a4-369f64ac5778.")
                },
                
                (_     , int y) => first switch
                {
                    IReadOnlyList<byte > xs when y <= byte .MaxValue => createArray3(    xs , (byte )y, secondCount),
                    IReadOnlyList<byte > xs when y <= short.MaxValue => createArray3(b2s(xs), (short)y, secondCount),
                    IReadOnlyList<byte > xs when y <= int  .MaxValue => createArray3(b2i(xs),        y, secondCount),

                    IReadOnlyList<short> xs when y <= short.MaxValue => createArray3(    xs , (short)y, secondCount),
                    IReadOnlyList<short> xs when y <= int  .MaxValue => createArray3(s2i(xs),        y, secondCount),

                    IReadOnlyList<int  > xs when y <= int  .MaxValue => createArray3(xs, (int  )y, secondCount),

                    _ => throw new Exception("Invariant ee1933cb-f9d9-4cea-8bd8-ab702f1a8b97.")
                },
                (_     , uint y) => first switch
                {
                    IReadOnlyList<byte > xs when y <= byte .MaxValue => createArray3(    xs , (byte )y, secondCount),
                    IReadOnlyList<byte > xs when y <= short.MaxValue => createArray3(b2s(xs), (short)y, secondCount),
                    IReadOnlyList<byte > xs when y <= int  .MaxValue => createArray3(b2i(xs), (int  )y, secondCount),

                    IReadOnlyList<short> xs when y <= short.MaxValue => createArray3(    xs , (short)y, secondCount),
                    IReadOnlyList<short> xs when y <= int  .MaxValue => createArray3(s2i(xs), (int  )y, secondCount),

                    IReadOnlyList<int  > xs when y <= int  .MaxValue => createArray3(xs, (int  )y, secondCount),

                    _ => throw new Exception("Invariant 7ddfc8c0-2e66-45ef-94a9-31d21f6009f9.")
                },

                (IReadOnlyList<byte > xs, IReadOnlyList<byte > ys) => createArray4(    xs ,     ys ),
                (IReadOnlyList<byte > xs, IReadOnlyList<short> ys) => createArray4(b2s(xs),     ys ),
                (IReadOnlyList<byte > xs, IReadOnlyList<int  > ys) => createArray4(b2i(xs),     ys ),
                (IReadOnlyList<short> xs, IReadOnlyList<byte > ys) => createArray4(    xs , b2s(ys)),
                (IReadOnlyList<short> xs, IReadOnlyList<short> ys) => createArray4(    xs ,     ys ),
                (IReadOnlyList<short> xs, IReadOnlyList<int  > ys) => createArray4(s2i(xs),     ys ),
                (IReadOnlyList<int  > xs, IReadOnlyList<byte > ys) => createArray4(    xs , b2i(ys)),
                (IReadOnlyList<int  > xs, IReadOnlyList<short> ys) => createArray4(    xs , s2i(ys)),
                (IReadOnlyList<int  > xs, IReadOnlyList<int  > ys) => createArray4(    xs ,     ys ),

                _ => throw new Exception(
                    $"Unexpected part indices types {first?.GetType().FullName ?? "null"} and {second?.GetType().FullName ?? "null"}. " +
                    $"Error 2f0672f5-8c6b-400b-8172-e83a30d70c28"
                    )
            };

            object createArray1(int first, int firstCount, int second, int secondCount)
            {
                var count = firstCount + secondCount;
                return Math.Max(first, second) switch
                {
                    int max when max <= byte .MaxValue => create((byte )first, (byte )second),
                    int max when max <= short.MaxValue => create((short)first, (short)second),
                    int max when max <= int  .MaxValue => create(       first,        second),
                    _ => throw new Exception("Invariant 129edb1c-066d-4ff2-8edf-8c5a67191dea.")
                };

                object create<T>(T x0, T x1) where T : unmanaged
                {
                    var xs = new T[count];
                    for (var i = 0; i < firstCount; i++) xs[i] = x0;
                    for (var i = firstCount; i < count; i++) xs[i] = x1;
                    return xs;
                }
            }

            object createArray2<T>(T first, int firstCount, IReadOnlyList<T> second) where T : unmanaged
            {
                var count = firstCount + second.Count;
                var xs = new T[count];
                int j = 0;
                for (var i = 0; i < firstCount  ; i++) xs[j++] = first;
                for (var i = 0; i < second.Count; i++) xs[j++] = second[i];
                return xs;
            }

            object createArray3<T>(IReadOnlyList<T> first, T second, int secondCount) where T : unmanaged
            {
                var count = first.Count + secondCount;
                var xs = new T[count];
                int j = 0;
                for (var i = 0; i < first.Count; i++) xs[j++] = first[i];
                for (var i = 0; i < secondCount; i++) xs[j++] = second;
                return xs;
            }

            object createArray4<T>(IReadOnlyList<T> first, IReadOnlyList<T> second) where T : unmanaged
            {
                var count = first.Count + second.Count;
                var xs = new T[count];
                int j = 0;
                for (var i = 0; i < first .Count; i++) xs[j++] = first[i];
                for (var i = 0; i < second.Count; i++) xs[j++] = second[i];
                return xs;
            }

            short[] b2s(IReadOnlyList<byte > xs) { var ys = new short[xs.Count]; for (var i = 0; i < xs.Count; i++) ys[i] = xs[i]; return ys; }
            int  [] b2i(IReadOnlyList<byte > xs) { var ys = new int  [xs.Count]; for (var i = 0; i < xs.Count; i++) ys[i] = xs[i]; return ys; }
            int  [] s2i(IReadOnlyList<short> xs) { var ys = new int  [xs.Count]; for (var i = 0; i < xs.Count; i++) ys[i] = xs[i]; return ys; }
        }
    }

    /// <summary>
    /// Concatenates part indices (null, int, [byte|short|int] array).
    /// </summary>
    public static object? ConcatIndices(IEnumerable<(object? indices, int count)> xs)
    {
        var (resultIndices, resultCount) = xs.FirstOrDefault();
        foreach (var (xIndices, xCount) in xs.Skip(1))
        {
            ConcatIndices(resultIndices, resultCount, xIndices, xCount);
            resultCount += xCount;
        }
        return resultIndices;
    }

    public static Range1i ExtendRangeBy(in Range1i range, object partIndices)
    {
        if (partIndices == null) throw new Exception("Invariant d781e171-41c3-4272-88a7-261cea302c18.");

        checked
        {
            return partIndices switch
            {
                int x           => range.ExtendedBy(x),
                uint x          => range.ExtendedBy((int)x),
                IList<byte> xs  => range.ExtendedBy((Range1i)new Range1b(xs)),
                IList<short> xs => range.ExtendedBy((Range1i)new Range1s(xs)),
                IList<int> xs   => range.ExtendedBy(new Range1i(xs)),

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
        null            => null,
        int x           => x,
        uint x          => (int)x,
        IList<byte> xs  => xs.Skip(n).ToArray(),
        IList<short> xs => xs.Skip(n).ToArray(),
        IList<int> xs   => xs.Skip(n).ToArray(),

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
        null            => null,
        int x           => x,
        uint x          => (int)x,
        IList<byte> xs  => xs.Take(n).ToArray(),
        IList<short> xs => xs.Take(n).ToArray(),
        IList<int> xs   => xs.Take(n).ToArray(),

        _ => throw new Exception(
            $"Unexpected part indices type {partIndices.GetType().FullName}. " +
            $"Error 49e1b5e2-5380-4ce3-a2d2-b80fec39337f"
            )
    };

    /// <summary>
    /// Creates subset/reshuffle part indices according to index array.
    /// </summary>
    public static object? Subset(object? partIndices, IReadOnlyList<int> subsetIndices) => partIndices switch
    {
        null            => null,
        int x           => x,
        uint x          => (int)x,
        IList<byte> xs  => subsetIndices.MapToArray(i => xs[i]),
        IList<short> xs => subsetIndices.MapToArray(i => xs[i]),
        IList<int> xs   => subsetIndices.MapToArray(i => xs[i]),

        _ => throw new Exception(
            $"Unexpected part indices type {partIndices.GetType().FullName}. " +
            $"Error 37b3981f-ea67-489c-8fc9-354c01057792"
            )
    };
}