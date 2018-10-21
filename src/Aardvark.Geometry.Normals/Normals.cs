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
using System.Collections.Generic;
using System.Linq;
using Uncodium;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// Generation of per-point normals for point clouds.
    /// </summary>
    public static class Normals
    {
        /// <summary>
        /// Estimates a normal vector for each point by least-squares-fitting a plane through its k nearest neighbours.
        /// </summary>
        public static V3f[] EstimateNormals(this V3d[] points, int k)
            => EstimateNormals(points, points.CreateRkdTree(Metric.Euclidean, 0.0), k);
        
        /// <summary>
        /// Estimates a normal vector for each point by least-squares-fitting a plane through its k nearest neighbours.
        /// </summary>
        public static V3f[] EstimateNormals(this V3f[] points, int k)
        {
            var kd = new PointRkdTreeD<V3f[], V3f>(
                3, points.Length, points,
                (xs, i) => xs[(int)i], (v, i) => (float)v[i],
                (a, b) => V3f.Distance(a, b), (i, a, b) => b - a,
                (a, b, c) => VecFun.DistanceToLine(a, b, c), VecFun.Lerp, 0
                );
            return EstimateNormals(points, kd, k);
        }

        /// <summary>
        /// Estimates a normal vector for each point by least-squares-fitting a plane through its k nearest neighbours.
        /// Requires that the supplied kdtree is built from given points. 
        /// </summary>
        public static V3f[] EstimateNormals(this IList<V3d> points, PointRkdTreeD<V3d[], V3d> kdtree, int k)
            => points.Map((p, i) =>
            {
                if (k > points.Count) k = points.Count;

                // find k closest points
                var closest = kdtree.GetClosest(p, double.MaxValue, k);
                if (closest.Count == 0) return V3f.Zero;

                // compute centroid of k closest points
                var c = points[(int)closest[0].Index];
                for (var j = 1; j < k; j++) c += points[(int)closest[j].Index];
                c /= k;
                
                // compute covariance matrix of k closest points relative to centroid
                var cvm = M33d.Zero;
                for (var j = 0; j < k; j++) cvm.AddOuterProduct(points[(int)closest[j].Index] - c);
                cvm /= k;

                // solve eigensystem -> eigenvector for smallest eigenvalue gives normal 
                Eigensystems.Dsyevh3(cvm, out M33d q, out V3d w);
                return (V3f)((w.X < w.Y) ? ((w.X < w.Z) ? q.C0 : q.C2) : ((w.Y < w.Z) ? q.C1 : q.C2));
            });

        /// <summary>
        /// Estimates a normal vector for each point by least-squares-fitting a plane through its k nearest neighbours.
        /// Requires that the supplied kdtree is built from given points. 
        /// </summary>
        public static V3f[] EstimateNormals(this V3f[] points, PointRkdTreeD<V3f[], V3f> kdtree, int k)
            => points.Map((p, i) =>
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
}
