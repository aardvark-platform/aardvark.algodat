/*
    Copyright (C) 2006-2018. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using Aardvark.Geometry;
using System;
using System.Threading.Tasks;

namespace Aardvark.Geometry
{
    /// <summary>
    /// </summary>
    public static class KdTreeExtensions
    {
        /// <summary>
        /// Constructs rkd-tree from points and kd-tree data.
        /// </summary>
        public static PointRkdTreeF<V3f[], V3f> ToKdTree(this V3f[] points, PointRkdTreeFData data)
            => new PointRkdTreeF<V3f[], V3f>(
                    3, points.Length, points,
                    (xs, i) => xs[(int)i], (v, i) => (float)v[i],
                    (a, b) => V3f.Distance(a, b), (i, a, b) => b - a,
                    (a, b, c) => VecFun.DistanceToLine(a, b, c), VecFun.Lerp, 1e-6f,
                    data
                    );

        /// <summary>
        /// Computes rkd-tree from given points.
        /// </summary>
        public static PointRkdTreeF<V3f[], V3f> BuildKdTree(this V3f[] points, float kdTreeEps = 1e-6f)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (points.Length == 0) return points.ToKdTree(new PointRkdTreeFData());
            return new PointRkdTreeF<V3f[], V3f>(
                3, points.Length, points,
                (xs, i) => xs[(int)i], (v, i) => (float)v[i],
                (a, b) => V3f.Distance(a, b), (i, a, b) => b - a,
                (a, b, c) => VecFun.DistanceToLine(a, b, c), VecFun.Lerp, kdTreeEps
                );
        }

        /// <summary>
        /// Computes rkd-tree from given points.
        /// </summary>
        public static async Task<PointRkdTreeF<V3f[], V3f>> BuildKdTreeAsync(this V3f[] points, float kdTreeEps = 1e-6f)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (points.Length == 0) return points.ToKdTree(new PointRkdTreeFData());
            return await Task.Run(() => BuildKdTree(points, kdTreeEps));
        }
    }
}
