/*
    Copyright (C) 2006-2024. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System;
using Aardvark.Base;
using Aardvark.Base.Sorting;
using Aardvark.Data.Points;

namespace Aardvark.Geometry.Points;

/// <summary>
/// </summary>
public class CellQueryResult(IPointCloudNodeOld cell, bool isFullyInside)
{
    /// <summary>
    /// </summary>
    public static CellQueryResult Empty = new(null!, false);

    /// <summary>
    /// </summary>
    public IPointCloudNodeOld Cell { get; } = cell;

    /// <summary>
    /// </summary>
    public bool IsFullyInside { get; } = isFullyInside;

    /// <summary>
    /// Cell == null.
    /// </summary>
    public bool IsEmpty => Cell == null;
}

/// <summary>
/// </summary>
public class PointsNearObject<T>
{
    /// <summary></summary>
    public static readonly PointsNearObject<T> Empty = new(
        default!, 0.0, Chunk.Empty, null!
        );

    /// <summary></summary>
    public T Object { get; }

    /// <summary></summary>
    public double MaxDistance { get; }

    /// <summary></summary>
    public Chunk Chunk { get; }

    public double[] Distances { get; }

    /// <summary></summary>
    public PointsNearObject(T obj, double maxDistance, Chunk chunk, double[] distances)
    {
        if (maxDistance < 0.0) throw new ArgumentOutOfRangeException(nameof(maxDistance), $"Parameter 'maxDistance' must not be less than 0.0, but is {maxDistance}.");

        Object = obj;
        MaxDistance = maxDistance;
        Chunk = chunk;
        Distances = distances;
    }

    /// <summary>
    /// </summary>
    public int Count => Chunk.Count;

    /// <summary>
    /// </summary>
    public bool IsEmpty => Chunk.Count == 0;

    /// <summary>
    /// Returns this PointsNearObject merged with other PointsNearObject.
    /// </summary>
    public PointsNearObject<T> Merge(PointsNearObject<T> other, int maxCount)
    {
        if (maxCount < 0) throw new ArgumentOutOfRangeException(nameof(maxCount));
        if (maxCount == 0) return Empty;
        if (other == null || other.IsEmpty) return this;
        if (IsEmpty) return other;

        var mergedChunks = Chunk.ImmutableMerge(Chunk, other.Chunk);
        
        var merged = new PointsNearObject<T>(Object,
            Math.Max(MaxDistance, other.MaxDistance),
            mergedChunks,
            Distances.Append(other.Distances)
            );

        if (mergedChunks.Count > maxCount)
        {
            // take 'maxCount' nearest items
            merged = merged.OrderedByDistanceAscending().Take(maxCount); 
        }

        return merged;
    }

    /// <summary>
    /// Returns PointsNearObject ordered by ascending distance.
    /// </summary>
    public PointsNearObject<T> OrderedByDistanceAscending() => Reordered(Distances.CreatePermutationQuickSortAscending());

    /// <summary>
    /// Returns PointsNearObject ordered by descending distance.
    /// </summary>
    public PointsNearObject<T> OrderedByDistanceDescending() => Reordered(Distances.CreatePermutationQuickSortDescending());

    /// <summary>
    /// Returns PointsNearObject ordered by descending distance.
    /// </summary>
    public PointsNearObject<T> Reordered(int[] ia) => new(
        Object, MaxDistance,
        Chunk.ImmutableReorder(ia),
        Distances.Reordered(ia)
        );

    /// <summary>
    /// Takes first 'count' items. 
    /// </summary>
    public PointsNearObject<T> Take(int count)
    {
        if (count >= Count) return this;
        var ds = Distances!.Take(count);
        return new PointsNearObject<T>(Object, ds.Max(), Chunk.Take(count), ds);
    }

    /// <summary>
    /// </summary>
    public PointsNearObject<U> WithObject<U>(U other)
        => new(other, MaxDistance, Chunk, Distances);
}
