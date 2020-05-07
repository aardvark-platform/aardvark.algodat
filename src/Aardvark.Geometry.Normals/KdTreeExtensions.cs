/*
    Copyright (C) 2006-2020. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System.Collections.Generic;
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
                    (a, b) => Vec.Distance(a, b), (i, a, b) => b - a,
                    (a, b, c) => Vec.DistanceToLine(a, b, c), Fun.Lerp, 1e-6f,
                    data
                    );

        /// <summary>
        /// Constructs rkd-tree from points and kd-tree data.
        /// </summary>
        public static PointRkdTreeF<V3f[], V3f> ToKdTree(this IList<V3f> points, PointRkdTreeFData data)
            => new PointRkdTreeF<V3f[], V3f>(
                    3, points.Count, (points is V3f[] ps) ? ps : ((points is List<V3f> ps2) ? ps2.ToArray() : points.ToArray(points.Count)),
                    (xs, i) => xs[(int)i], (v, i) => (float)v[i],
                    (a, b) => Vec.Distance(a, b), (i, a, b) => b - a,
                    (a, b, c) => Vec.DistanceToLine(a, b, c), Fun.Lerp, 1e-6f,
                    data
                    );



        /// <summary>
        /// Constructs rkd-tree from points and kd-tree data.
        /// </summary>
        public static PointRkdTreeD<V3d[], V3d> ToKdTree(this V3d[] points, PointRkdTreeDData data)
            => new PointRkdTreeD<V3d[], V3d>(
                    3, points.Length, points,
                    (xs, i) => xs[(int)i], (v, i) => v[i],
                    (a, b) => Vec.Distance(a, b), (i, a, b) => b - a,
                    (a, b, c) => Vec.DistanceToLine(a, b, c), Fun.Lerp, 1e-12,
                    data
                    );

        /// <summary>
        /// Constructs rkd-tree from points and kd-tree data.
        /// </summary>
        public static PointRkdTreeD<V3d[], V3d> ToKdTree(this IList<V3d> points, PointRkdTreeDData data)
            => new PointRkdTreeD<V3d[], V3d>(
                    3, points.Count, (points is V3d[] ps) ? ps : ((points is List<V3d> ps2) ? ps2.ToArray() : points.ToArray(points.Count)),
                    (xs, i) => xs[(int)i], (v, i) => (float)v[i],
                    (a, b) => Vec.Distance(a, b), (i, a, b) => b - a,
                    (a, b, c) => Vec.DistanceToLine(a, b, c), Fun.Lerp, 1e-12,
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
                (a, b) => Vec.Distance(a, b), (i, a, b) => b - a,
                (a, b, c) => Vec.DistanceToLine(a, b, c), Fun.Lerp, kdTreeEps
                );
        }

        /// <summary>
        /// Computes rkd-tree from given points.
        /// </summary>
        public static PointRkdTreeF<V3f[], V3f> BuildKdTree(this IList<V3f> points, float kdTreeEps = 1e-6f)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (points.Count == 0) return points.ToKdTree(new PointRkdTreeFData());
            return new PointRkdTreeF<V3f[], V3f>(
                3, points.Count, (points is V3f[] ps) ? ps : ((points is List<V3f> ps2) ? ps2.ToArray() : points.ToArray(points.Count)),
                (xs, i) => xs[(int)i], (v, i) => (float)v[i],
                (a, b) => Vec.Distance(a, b), (i, a, b) => b - a,
                (a, b, c) => Vec.DistanceToLine(a, b, c), Fun.Lerp, kdTreeEps
                );
        }



        /// <summary>
        /// Computes rkd-tree from given points.
        /// </summary>
        public static PointRkdTreeD<V3d[], V3d> BuildKdTree(this V3d[] points, double kdTreeEps = 1e-12)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (points.Length == 0) return points.ToKdTree(new PointRkdTreeDData());
            return new PointRkdTreeD<V3d[], V3d>(
                3, points.Length, points,
                (xs, i) => xs[(int)i], (v, i) => (float)v[i],
                (a, b) => Vec.Distance(a, b), (i, a, b) => b - a,
                (a, b, c) => Vec.DistanceToLine(a, b, c), Fun.Lerp, kdTreeEps
                );
        }

        /// <summary>
        /// Computes rkd-tree from given points.
        /// </summary>
        public static PointRkdTreeD<V3d[], V3d> BuildKdTree(this IList<V3d> points, double kdTreeEps = 1e-12)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (points.Count == 0) return points.ToKdTree(new PointRkdTreeDData());
            return new PointRkdTreeD<V3d[], V3d>(
                3, points.Count, (points is V3d[] ps) ? ps : ((points is List<V3d> ps2) ? ps2.ToArray() : points.ToArray(points.Count)),
                (xs, i) => xs[(int)i], (v, i) => (float)v[i],
                (a, b) => Vec.Distance(a, b), (i, a, b) => b - a,
                (a, b, c) => Vec.DistanceToLine(a, b, c), Fun.Lerp, kdTreeEps
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

        /// <summary>
        /// Computes rkd-tree from given points.
        /// </summary>
        public static async Task<PointRkdTreeF<V3f[], V3f>> BuildKdTreeAsync(this IList<V3f> points, float kdTreeEps = 1e-6f)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (points.Count == 0) return new V3f[0].ToKdTree(new PointRkdTreeFData());
            if (points is V3f[] ps1)
            {
                return await Task.Run(() => BuildKdTree(ps1, kdTreeEps));
            }
            else if (points is List<V3f> ps2)
            {
                return await Task.Run(() => BuildKdTree(ps2.ToArray(), kdTreeEps));
            }
            else
            {
                return await Task.Run(() => BuildKdTree(points.ToArray(points.Count), kdTreeEps));
            }
        }



        /// <summary>
        /// Computes rkd-tree from given points.
        /// </summary>
        public static async Task<PointRkdTreeD<V3d[], V3d>> BuildKdTreeAsync(this V3d[] points, double kdTreeEps = 1e-12)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (points.Length == 0) return points.ToKdTree(new PointRkdTreeDData());
            return await Task.Run(() => BuildKdTree(points, kdTreeEps));
        }

        /// <summary>
        /// Computes rkd-tree from given points.
        /// </summary>
        public static async Task<PointRkdTreeD<V3d[], V3d>> BuildKdTreeAsync(this IList<V3d> points, float kdTreeEps = 1e-6f)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (points.Count == 0) return new V3d[0].ToKdTree(new PointRkdTreeDData());
            if (points is V3d[] ps1)
            {
                return await Task.Run(() => BuildKdTree(ps1, kdTreeEps));
            }
            else if (points is List<V3d> ps2)
            {
                return await Task.Run(() => BuildKdTree(ps2.ToArray(), kdTreeEps));
            }
            else
            {
                return await Task.Run(() => BuildKdTree(points.ToArray(points.Count), kdTreeEps));
            }
        }
    }
}
