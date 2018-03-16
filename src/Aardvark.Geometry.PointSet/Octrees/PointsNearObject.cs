/*
    Copyright (C) 2017. Aardvark Platform Team. http://github.com/aardvark-platform.
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
        public PointSetNode Cell { get; }

        /// <summary>
        /// </summary>
        public bool IsFullyInside { get; }

        /// <summary>
        /// Cell == null.
        /// </summary>
        public bool IsEmpty => Cell == null;

        /// <summary>
        /// </summary>
        public CellQueryResult(PointSetNode cell, bool isFullyInside)
        {
            Cell = cell;
            IsFullyInside = isFullyInside;
        }
    }

    /// <summary>
    /// </summary>
    public class PointsNearObject<T>
    {
        /// <summary>
        /// </summary>
        public static PointsNearObject<T> Empty = new PointsNearObject<T>(default(T), 0.0, new V3d[0], new C4b[0], new V3f[0], new double[0]);

        /// <summary>
        /// </summary>
        public T Object { get; }

        /// <summary>
        /// </summary>
        public double MaxDistance { get; }

        /// <summary>
        /// </summary>
        public V3d[] Positions { get; }

        /// <summary>
        /// </summary>
        public C4b[] Colors { get; }

        /// <summary>
        /// </summary>
        public V3f[] Normals { get; }

        /// <summary>
        /// </summary>
        public double[] Distances { get; }

        /// <summary>
        /// </summary>
        public PointsNearObject(T obj, double maxDistance, V3d[] positions, C4b[] colors, V3f[] normals, double[] distances)
        {
            if (maxDistance < 0.0) throw new ArgumentOutOfRangeException(nameof(maxDistance), $"Parameter 'maxDistance' must not be less than 0.0, but is {maxDistance}.");

            Object = obj;
            MaxDistance = maxDistance;
            Positions = positions ?? throw new ArgumentNullException(nameof(positions));
            Normals = normals;
            Colors = colors;
            Distances = distances;
        }

        /// <summary>
        /// </summary>
        public int Count => Positions.Length;

        /// <summary>
        /// </summary>
        public bool IsEmpty => Positions.Length == 0;

        /// <summary>
        /// </summary>
        public PointsNearObject<U> WithObject<U>(U other)
        {
            return new PointsNearObject<U>(other, MaxDistance, Positions, Colors, Normals, Distances);
        }
    }
}
