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
using Aardvark.Base;
using System;
using System.Threading.Tasks;
using Uncodium;

namespace Aardvark.Geometry
{
    /// <summary>
    /// Normals estimation.
    /// </summary>
    public static class Normals
    {
        /// <summary>
        /// Estimates normals from k-closest neighbours. 
        /// </summary>
        public static V3f[] EstimateNormals(this V3f[] points, int k, PointRkdTreeF<V3f[], V3f> kdtree)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (k < 3) throw new ArgumentOutOfRangeException($"Expected k >= 3, but k is {k}.");
            if (kdtree == null) throw new ArgumentNullException(nameof(kdtree));

            if (points.Length == 0) return Array.Empty<V3f>();
            return points.Map((p, i) =>
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
            });
        }

        /// <summary>
        /// Estimates normals from k-closest neighbours. 
        /// </summary>
        public static async Task<V3f[]> EstimateNormalsAsync(this V3f[] points, int k, PointRkdTreeF<V3f[], V3f> kdtree)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (k < 3) throw new ArgumentOutOfRangeException($"Expected k >= 3, but k is {k}.");
            if (kdtree == null) throw new ArgumentNullException(nameof(kdtree));

            if (points.Length == 0) return Array.Empty<V3f>();
            return await Task.Run(() => EstimateNormals(points, k, kdtree));
        }

        /// <summary>
        /// Estimates normals from k-closest neighbours.
        /// Computes temporary kd-tree! If you already have a kd-tree for given points, use overload which takes a kd-tree instead.
        /// </summary>
        public static V3f[] EstimateNormals(this V3f[] points, int k)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (k < 3) throw new ArgumentOutOfRangeException($"Expected k >= 3, but k is {k}.");

            if (points.Length == 0) return Array.Empty<V3f>();
            return EstimateNormals(points, k, points.BuildKdTree());
        }

        /// <summary>
        /// Estimates normals from k-closest neighbours.
        /// Computes temporary kd-tree! If you already have a kd-tree for given points, use overload which takes a kd-tree instead.
        /// </summary>
        public static async Task<V3f[]> EstimateNormalsAsync(this V3f[] points, int k)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (k < 3) throw new ArgumentOutOfRangeException($"Expected k >= 3, but k is {k}.");

            if (points.Length == 0) return Array.Empty<V3f>();
            return await EstimateNormalsAsync(points, k, await points.BuildKdTreeAsync());
        }
    }
}
