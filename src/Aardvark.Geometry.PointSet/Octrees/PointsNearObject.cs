/*
    Copyright (C) 2006-2019. Aardvark Platform Team. http://github.com/aardvark-platform.
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

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public class CellQueryResult
    {
        /// <summary>
        /// </summary>
        public static CellQueryResult Empty = new CellQueryResult(null, false);

        /// <summary>
        /// </summary>
        public IPointCloudNode Cell { get; }

        /// <summary>
        /// </summary>
        public bool IsFullyInside { get; }

        /// <summary>
        /// Cell == null.
        /// </summary>
        public bool IsEmpty => Cell == null;

        /// <summary>
        /// </summary>
        public CellQueryResult(IPointCloudNode cell, bool isFullyInside)
        {
            Cell = cell;
            IsFullyInside = isFullyInside;
        }
    }

    /// <summary>
    /// </summary>
    public class PointsNearObject<T>
    {
        /// <summary></summary>
        public static readonly PointsNearObject<T> Empty = new PointsNearObject<T>(
            default, 0.0, 
            Array.Empty<V3d>(), Array.Empty<C4b>(), Array.Empty<V3f>(), 
            Array.Empty<double>(), Array.Empty<byte>(), Array.Empty<double>()
            );

        /// <summary></summary>
        public T Object { get; }

        /// <summary></summary>
        public double MaxDistance { get; }

        /// <summary></summary>
        public V3d[] Positions { get; }

        /// <summary></summary>
        public C4b[] Colors { get; }

        /// <summary></summary>
        public V3f[] Normals { get; }

        /// <summary></summary>
        public double[] Intensities { get; }
        
        /// <summary></summary>
        public byte[] Classifications { get; }

        /// <summary></summary>
        public double[] Distances { get; }

        /// <summary></summary>
        public PointsNearObject(T obj, double maxDistance, V3d[] positions, C4b[] colors, V3f[] normals,double[] intensities, byte[] classifications, double[] distances)
        {
            if (maxDistance < 0.0) throw new ArgumentOutOfRangeException(nameof(maxDistance), $"Parameter 'maxDistance' must not be less than 0.0, but is {maxDistance}.");

            Object = obj;
            MaxDistance = maxDistance;
            Positions = positions ?? throw new ArgumentNullException(nameof(positions));
            Colors = colors;
            Normals = normals;
            Intensities = intensities;
            Classifications = classifications;
            Distances = distances;
        }

        /// <summary>
        /// </summary>
        public int Count => Positions.Length;

        /// <summary>
        /// </summary>
        public bool IsEmpty => Positions.Length == 0;

        /// <summary>
        /// Returns this PointsNearObject merged with other PointsNearObject.
        /// </summary>
        public PointsNearObject<T> Merge(PointsNearObject<T> other, int maxCount)
        {
            if (maxCount < 0) throw new ArgumentOutOfRangeException(nameof(maxCount));
            if (maxCount == 0) return PointsNearObject<T>.Empty;
            if (other == null || other.IsEmpty) return this;

            var merged = new PointsNearObject<T>(Object,
                Math.Max(MaxDistance, other.MaxDistance),
                Positions.Append(other.Positions),
                Colors.Append(other.Colors),
                Normals.Append(other.Normals),
                Intensities.Append(other.Intensities),
                Classifications.Append(other.Classifications),
                Distances.Append(other.Distances)
                );

            if (Count + other.Count > maxCount)
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
        public PointsNearObject<T> Reordered(int[] ia) => new PointsNearObject<T>(
            Object, MaxDistance,
            Positions.Reordered(ia),
            Colors.Length > 0 ? Colors.Reordered(ia) : Colors,
            Normals.Length > 0 ? Normals.Reordered(ia) : Normals,
            Intensities.Length > 0 ? Intensities.Reordered(ia) : Intensities,
            Classifications.Length > 0 ? Classifications.Reordered(ia) : Classifications,
            Distances.Reordered(ia)
            );

        /// <summary>
        /// Takes first 'count' items. 
        /// </summary>
        public PointsNearObject<T> Take(int count)
        {
            if (count >= Count) return this;
            var ds = Distances.Take(count);
            return new PointsNearObject<T>(Object, ds.Max(),
                Positions.Take(count), Colors.Take(count), Normals.Take(count), Intensities.Take(count), Classifications.Take(count), ds
                );
        }

        /// <summary>
        /// </summary>
        public PointsNearObject<U> WithObject<U>(U other)
            => new PointsNearObject<U>(other, MaxDistance, Positions, Colors, Normals, Intensities, Classifications, Distances);
    }
}
