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
using System;
using System.Threading.Tasks;
using Uncodium;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public static class KdTreeExtensions
    {
        /// <summary>
        /// </summary>
        public static PointRkdTreeF<V3f[], V3f> ToKdTree(this V3f[] self, PointRkdTreeFData data)
            => new PointRkdTreeF<V3f[], V3f>(
                    3, self.Length, self,
                    (xs, i) => xs[(int)i], (v, i) => (float)v[i],
                    (a, b) => V3f.Distance(a, b), (i, a, b) => b - a,
                    (a, b, c) => VecFun.DistanceToLine(a, b, c), VecFun.Lerp, 1e-6f,
                    data
                    );

        /// <summary>
        /// Creates point rkd-tree.
        /// </summary>
        public static PointRkdTreeF<V3f[], V3f> BuildKdTree(this V3f[] self, float kdTreeEps = 1e-6f)
        {
            if (self == null) throw new ArgumentNullException(nameof(self));
            if (self.Length == 0) return self.ToKdTree(new PointRkdTreeFData());

            return new PointRkdTreeF<V3f[], V3f>(
                3, self.Length, self,
                (xs, i) => xs[(int)i], (v, i) => (float)v[i],
                (a, b) => V3f.Distance(a, b), (i, a, b) => b - a,
                (a, b, c) => VecFun.DistanceToLine(a, b, c), VecFun.Lerp, kdTreeEps
                );
        }

        /// <summary>
        /// </summary>
        public static async Task<V3f[]> EstimateNormals(this V3f[] points, PointRkdTreeF<V3f[], V3f> kdtree, int k)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (kdtree == null) throw new ArgumentNullException(nameof(kdtree));
            if (k < 3) throw new ArgumentOutOfRangeException($"Expected k >= 3, but k is {k}.");

            if (points.Length == 0) return Array.Empty<V3f>();

            return await Task.Run(() => points.Map((p, i) =>
            {
                if (k > points.Length) k = points.Length;

                // find k closest points
                var closest = kdtree.GetClosest(p, float.MaxValue, k);
                if (closest.Count == 0) return V3f.Zero;

                // compute centroid of k closest points
                var c = points[closest[0].Index];
                for (var j = 1; j < k; j++) c += points[closest[j].Index];
                c /= k;

                // compute covariance matrix of k closest points relative to centroid
                var cvm = M33f.Zero;
                for (var j = 0; j < k; j++) cvm.AddOuterProduct(points[closest[j].Index] - c);
                cvm /= k;

                // solve eigensystem -> eigenvector for smallest eigenvalue gives normal 
                Eigensystems.Dsyevh3((M33d)cvm, out M33d q, out V3d w);
                return (V3f)((w.X < w.Y) ? ((w.X < w.Z) ? q.C0 : q.C2) : ((w.Y < w.Z) ? q.C1 : q.C2));
            })
            );
        }
    }
}
