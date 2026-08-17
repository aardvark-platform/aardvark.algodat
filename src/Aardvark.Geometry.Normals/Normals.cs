/*
    Copyright (C) 2006-2025. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System.Threading.Tasks;
using Uncodium;

#pragma warning disable IDE0130 // Namespace does not match folder structure

namespace Aardvark.Geometry
{
    /// <summary>
    /// Normals estimation.
    /// </summary>
    public static class Normals
    {
        #region EstimateNormals

        /// <summary>
        /// Estimates normals from k-nearest neighbours. 
        /// </summary>
        public static V3f[] EstimateNormals(this V3f[] points, int k, PointRkdTreeF<V3f[], V3f> kdtree)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (k < 3) throw new ArgumentOutOfRangeException($"Expected k >= 3, but k is {k}.");
            if (kdtree == null) throw new ArgumentNullException(nameof(kdtree));

            if (points.Length == 0) return Array.Empty<V3f>();
            return points.Map(p =>
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
        /// Estimates normals from k-nearest neighbours. 
        /// </summary>
        public static V3f[] EstimateNormals(this IList<V3f> points, int k, PointRkdTreeF<V3f[], V3f> kdtree)
        {
            if (points is V3f[] ps1) return EstimateNormals(ps1, k, kdtree);
            else if (points is List<V3f> ps2) return EstimateNormals(ps2.ToArray(), k, kdtree);
            else return EstimateNormals(points.ToArray(points.Count), k, kdtree);
        }


        /// <summary>
        /// Estimates normals from k-nearest neighbours. 
        /// </summary>
        public static V3f[] EstimateNormals(this V3d[] points, int k, PointRkdTreeD<V3d[], V3d> kdtree)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (k < 3) throw new ArgumentOutOfRangeException($"Expected k >= 3, but k is {k}.");
            if (kdtree == null) throw new ArgumentNullException(nameof(kdtree));

            if (points.Length == 0) return Array.Empty<V3f>();
            return points.Map(p =>
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
                var cvm = M33d.Zero;
                for (var j = 0; j < k; j++) cvm.AddOuterProduct(points[closest[j].Index] - c);
                cvm /= k;

                // solve eigensystem -> eigenvector for smallest eigenvalue gives normal 
                Eigensystems.Dsyevh3(cvm, out M33d q, out V3d w);
                return (V3f)((w.X < w.Y) ? ((w.X < w.Z) ? q.C0 : q.C2) : ((w.Y < w.Z) ? q.C1 : q.C2));
            });
        }

        /// <summary>
        /// Estimates normals from k-nearest neighbours. 
        /// </summary>
        public static V3f[] EstimateNormals(this IList<V3d> points, int k, PointRkdTreeD<V3d[], V3d> kdtree)
        {
            if (points is V3d[] ps1) return EstimateNormals(ps1, k, kdtree);
            else if (points is List<V3d> ps2) return EstimateNormals(ps2.ToArray(), k, kdtree);
            else return EstimateNormals(points.ToArray(points.Count), k, kdtree);
        }


        /// <summary>
        /// Estimates normals from k-nearest neighbours. 
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
        /// Estimates normals from k-nearest neighbours. 
        /// </summary>
        public static async Task<V3f[]> EstimateNormalsAsync(this V3d[] points, int k, PointRkdTreeD<V3d[], V3d> kdtree)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (k < 3) throw new ArgumentOutOfRangeException($"Expected k >= 3, but k is {k}.");
            if (kdtree == null) throw new ArgumentNullException(nameof(kdtree));

            if (points.Length == 0) return Array.Empty<V3f>();
            return await Task.Run(() => EstimateNormals(points, k, kdtree));
        }


        /// <summary>
        /// Estimates normals from k-nearest neighbours.
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
        /// Estimates normals from k-nearest neighbours.
        /// Computes temporary kd-tree! If you already have a kd-tree for given points, use overload which takes a kd-tree instead.
        /// </summary>
        public static V3f[] EstimateNormals(this IList<V3f> points, int k)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (k < 3) throw new ArgumentOutOfRangeException($"Expected k >= 3, but k is {k}.");

            if (points.Count == 0) return Array.Empty<V3f>();
            if (points is V3f[] ps1)
            {
                return EstimateNormals(ps1, k, ps1.BuildKdTree());
            }
            else if (points is List<V3f> ps2)
            {
                var ps = ps2.ToArray();
                return EstimateNormals(ps, k, ps.BuildKdTree());
            }
            else
            {
                var ps = points.ToArray(points.Count);
                return EstimateNormals(ps, k, ps.BuildKdTree());
            }
        }


        /// <summary>
        /// Estimates normals from k-nearest neighbours.
        /// Computes temporary kd-tree! If you already have a kd-tree for given points, use overload which takes a kd-tree instead.
        /// </summary>
        public static V3f[] EstimateNormals(this V3d[] points, int k)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (k < 3) throw new ArgumentOutOfRangeException($"Expected k >= 3, but k is {k}.");

            if (points.Length == 0) return Array.Empty<V3f>();
            return EstimateNormals(points, k, points.BuildKdTree());
        }

        /// <summary>
        /// Estimates normals from k-nearest neighbours.
        /// Computes temporary kd-tree! If you already have a kd-tree for given points, use overload which takes a kd-tree instead.
        /// </summary>
        public static V3f[] EstimateNormals(this IList<V3d> points, int k)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (k < 3) throw new ArgumentOutOfRangeException($"Expected k >= 3, but k is {k}.");

            if (points.Count == 0) return Array.Empty<V3f>();
            if (points is V3d[] ps1)
            {
                return EstimateNormals(ps1, k, ps1.BuildKdTree());
            }
            else if (points is List<V3d> ps2)
            {
                var ps = ps2.ToArray();
                return EstimateNormals(ps, k, ps.BuildKdTree());
            }
            else
            {
                var ps = points.ToArray(points.Count);
                return EstimateNormals(ps, k, ps.BuildKdTree());
            }
        }


        /// <summary>
        /// Estimates normals from k-nearest neighbours.
        /// Computes temporary kd-tree! If you already have a kd-tree for given points, use overload which takes a kd-tree instead.
        /// </summary>
        public static async Task<V3f[]> EstimateNormalsAsync(this V3f[] points, int k)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (k < 3) throw new ArgumentOutOfRangeException($"Expected k >= 3, but k is {k}.");

            if (points.Length == 0) return Array.Empty<V3f>();
            return await EstimateNormalsAsync(points, k, await points.BuildKdTreeAsync());
        }

        /// <summary>
        /// Estimates normals from k-nearest neighbours.
        /// Computes temporary kd-tree! If you already have a kd-tree for given points, use overload which takes a kd-tree instead.
        /// </summary>
        public static async Task<V3f[]> EstimateNormalsAsync(this IList<V3f> points, int k)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (k < 3) throw new ArgumentOutOfRangeException($"Expected k >= 3, but k is {k}.");

            if (points.Count == 0) return Array.Empty<V3f>();
            if (points is V3f[] ps1)
            {
                return await EstimateNormalsAsync(ps1, k, await ps1.BuildKdTreeAsync());
            }
            else if (points is List<V3f> ps2)
            {
                var ps = ps2.ToArray();
                return await EstimateNormalsAsync(ps, k, await ps.BuildKdTreeAsync());
            }
            else
            {
                var ps = points.ToArray(points.Count);
                return await EstimateNormalsAsync(ps, k, await ps.BuildKdTreeAsync());
            }
        }


        /// <summary>
        /// Estimates normals from k-nearest neighbours.
        /// Computes temporary kd-tree! If you already have a kd-tree for given points, use overload which takes a kd-tree instead.
        /// </summary>
        public static async Task<V3f[]> EstimateNormalsAsync(this V3d[] points, int k)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (k < 3) throw new ArgumentOutOfRangeException($"Expected k >= 3, but k is {k}.");

            if (points.Length == 0) return Array.Empty<V3f>();
            return await EstimateNormalsAsync(points, k, await points.BuildKdTreeAsync());
        }

        /// <summary>
        /// Estimates normals from k-nearest neighbours.
        /// Computes temporary kd-tree! If you already have a kd-tree for given points, use overload which takes a kd-tree instead.
        /// </summary>
        public static async Task<V3f[]> EstimateNormalsAsync(this IList<V3d> points, int k)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (k < 3) throw new ArgumentOutOfRangeException($"Expected k >= 3, but k is {k}.");

            if (points.Count == 0) return Array.Empty<V3f>();
            if (points is V3d[] ps1)
            {
                return await EstimateNormalsAsync(ps1, k, await ps1.BuildKdTreeAsync());
            }
            else if (points is List<V3d> ps2)
            {
                var ps = ps2.ToArray();
                return await EstimateNormalsAsync(ps, k, await ps.BuildKdTreeAsync());
            }
            else
            {
                var ps = points.ToArray(points.Count);
                return await EstimateNormalsAsync(ps, k, await ps.BuildKdTreeAsync());
            }
        }

        #endregion

        #region EstimateNormalsAndQuality

        /// <summary>
        /// Estimates normals and their local planar quality from k-nearest neighbours.
        /// </summary>
        /// <remarks>
        /// For sorted covariance eigenvalues λmin ≤ λmiddle ≤ λmax, quality is
        /// clamp((λmiddle - λmin) / λmax, 0, 1). Quality is zero if λmax is
        /// non-positive or a required eigenvalue is non-finite. Zero denotes a
        /// collinear, coincident, or invalid neighbourhood; values near one denote a
        /// well-defined local plane. Normal orientation is arbitrary because eigenvector
        /// signs are undefined. A caller can, for example, replace normals whose quality
        /// is below a chosen application-specific threshold.
        /// </remarks>
        /// <example>
        /// <code>
        /// var (normals, qualities) = points.EstimateNormalsAndQuality(16, kdTree);
        /// for (var i = 0; i &lt; normals.Length; i++)
        ///     if (qualities[i] &lt; 0.1f) normals[i] = V3f.ZAxis;
        /// </code>
        /// </example>
        public static (V3f[] normals, float[] qualities) EstimateNormalsAndQuality(
            this V3f[] points, int k, PointRkdTreeF<V3f[], V3f> kdtree
            )
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (k < 3) throw new ArgumentOutOfRangeException($"Expected k >= 3, but k is {k}.");
            if (kdtree == null) throw new ArgumentNullException(nameof(kdtree));

            var ns = points.Length > 0 ? new V3f[points.Length] : Array.Empty<V3f>();
            var qs = points.Length > 0 ? new float[points.Length] : Array.Empty<float>();
            var count = Math.Min(k, points.Length);

            for (var i = 0; i < points.Length; i++)
            {
                var closest = kdtree.GetClosest(points[i], float.MaxValue, count);
                if (closest.Count == 0) continue;

                var c = points[closest[0].Index];
                for (var j = 1; j < closest.Count; j++) c += points[closest[j].Index];
                c /= closest.Count;

                var cvm = M33f.Zero;
                for (var j = 0; j < closest.Count; j++) cvm.AddOuterProduct(points[closest[j].Index] - c);
                cvm /= closest.Count;

                Eigensystems.Dsyevh3((M33d)cvm, out M33d eigenvectors, out V3d eigenvalues);
                (ns[i], qs[i]) = SelectNormalAndQuality(eigenvectors, eigenvalues);
            }

            return (normals: ns, qualities: qs);
        }

        /// <summary>
        /// Estimates normals and quality values in [0, 1] from k-nearest neighbours.
        /// </summary>
        /// <remarks>
        /// Quality is clamp((λmiddle - λmin) / λmax, 0, 1) for the sorted covariance
        /// eigenvalues. Quality is zero if λmax is non-positive or a required eigenvalue
        /// is non-finite. Zero identifies degenerate or invalid neighbourhoods and values
        /// near one identify well-defined local planes. Normal orientation is arbitrary.
        /// </remarks>
        public static (V3f[] normals, float[] qualities) EstimateNormalsAndQuality(
            this V3d[] points, int k, PointRkdTreeD<V3d[], V3d> kdtree
            )
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (k < 3) throw new ArgumentOutOfRangeException($"Expected k >= 3, but k is {k}.");
            if (kdtree == null) throw new ArgumentNullException(nameof(kdtree));

            var ns = points.Length > 0 ? new V3f[points.Length] : Array.Empty<V3f>();
            var qs = points.Length > 0 ? new float[points.Length] : Array.Empty<float>();
            var count = Math.Min(k, points.Length);

            for (var i = 0; i < points.Length; i++)
            {
                var closest = kdtree.GetClosest(points[i], float.MaxValue, count);
                if (closest.Count == 0) continue;

                var c = points[closest[0].Index];
                for (var j = 1; j < closest.Count; j++) c += points[closest[j].Index];
                c /= closest.Count;

                var cvm = M33d.Zero;
                for (var j = 0; j < closest.Count; j++) cvm.AddOuterProduct(points[closest[j].Index] - c);
                cvm /= closest.Count;

                Eigensystems.Dsyevh3(cvm, out M33d eigenvectors, out V3d eigenvalues);
                (ns[i], qs[i]) = SelectNormalAndQuality(eigenvectors, eigenvalues);
            }

            return (normals: ns, qualities: qs);
        }

        /// <summary>
        /// Asynchronously estimates normals and planar quality using a supplied kd-tree.
        /// Quality is in [0, 1]; zero is degenerate and values near one are planar.
        /// Normal orientation is arbitrary.
        /// </summary>
        public static async Task<(V3f[] normals, float[] qualities)> EstimateNormalsAndQualityAsync(
            this V3f[] points, int k, PointRkdTreeF<V3f[], V3f> kdtree
            )
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (k < 3) throw new ArgumentOutOfRangeException($"Expected k >= 3, but k is {k}.");
            if (kdtree == null) throw new ArgumentNullException(nameof(kdtree));
            return await Task.Run(() => EstimateNormalsAndQuality(points, k, kdtree));
        }

        /// <summary>
        /// Asynchronously estimates normals and planar quality using a supplied kd-tree.
        /// Quality is in [0, 1]; zero is degenerate and values near one are planar.
        /// Normal orientation is arbitrary.
        /// </summary>
        public static async Task<(V3f[] normals, float[] qualities)> EstimateNormalsAndQualityAsync(
            this V3d[] points, int k, PointRkdTreeD<V3d[], V3d> kdtree
            )
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (k < 3) throw new ArgumentOutOfRangeException($"Expected k >= 3, but k is {k}.");
            if (kdtree == null) throw new ArgumentNullException(nameof(kdtree));
            return await Task.Run(() => EstimateNormalsAndQuality(points, k, kdtree));
        }

        /// <summary>
        /// Estimates normals and planar quality, building a temporary kd-tree.
        /// Quality is in [0, 1]; zero is degenerate and values near one are planar.
        /// Normal orientation is arbitrary.
        /// </summary>
        public static (V3f[] normals, float[] qualities) EstimateNormalsAndQuality(this V3f[] points, int k)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (k < 3) throw new ArgumentOutOfRangeException($"Expected k >= 3, but k is {k}.");
            if (points.Length == 0) return (Array.Empty<V3f>(), Array.Empty<float>());
            return EstimateNormalsAndQuality(points, k, points.BuildKdTree());
        }

        /// <summary>
        /// Estimates normals and planar quality, building a temporary kd-tree.
        /// Quality is in [0, 1]; zero is degenerate and values near one are planar.
        /// Normal orientation is arbitrary.
        /// </summary>
        public static (V3f[] normals, float[] qualities) EstimateNormalsAndQuality(this V3d[] points, int k)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (k < 3) throw new ArgumentOutOfRangeException($"Expected k >= 3, but k is {k}.");
            if (points.Length == 0) return (Array.Empty<V3f>(), Array.Empty<float>());
            return EstimateNormalsAndQuality(points, k, points.BuildKdTree());
        }

        /// <summary>
        /// Asynchronously estimates normals and planar quality, building a temporary kd-tree.
        /// Quality is in [0, 1]; zero is degenerate and values near one are planar.
        /// Normal orientation is arbitrary.
        /// </summary>
        public static async Task<(V3f[] normals, float[] qualities)> EstimateNormalsAndQualityAsync(this V3f[] points, int k)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (k < 3) throw new ArgumentOutOfRangeException($"Expected k >= 3, but k is {k}.");
            if (points.Length == 0) return (Array.Empty<V3f>(), Array.Empty<float>());
            return await EstimateNormalsAndQualityAsync(points, k, await points.BuildKdTreeAsync());
        }

        /// <summary>
        /// Asynchronously estimates normals and planar quality, building a temporary kd-tree.
        /// Quality is in [0, 1]; zero is degenerate and values near one are planar.
        /// Normal orientation is arbitrary.
        /// </summary>
        public static async Task<(V3f[] normals, float[] qualities)> EstimateNormalsAndQualityAsync(this V3d[] points, int k)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (k < 3) throw new ArgumentOutOfRangeException($"Expected k >= 3, but k is {k}.");
            if (points.Length == 0) return (Array.Empty<V3f>(), Array.Empty<float>());
            return await EstimateNormalsAndQualityAsync(points, k, await points.BuildKdTreeAsync());
        }

        private static (V3f normal, float quality) SelectNormalAndQuality(M33d eigenvectors, V3d eigenvalues)
        {
            var normal = (V3f)((eigenvalues.X < eigenvalues.Y)
                ? ((eigenvalues.X < eigenvalues.Z) ? eigenvectors.C0 : eigenvectors.C2)
                : ((eigenvalues.Y < eigenvalues.Z) ? eigenvectors.C1 : eigenvectors.C2));

            var minimum = eigenvalues.X;
            var middle = eigenvalues.Y;
            var maximum = eigenvalues.Z;
            if (minimum > middle) (minimum, middle) = (middle, minimum);
            if (middle > maximum) (middle, maximum) = (maximum, middle);
            if (minimum > middle) (minimum, middle) = (middle, minimum);

            if (!IsFinite(minimum) || !IsFinite(middle) || !IsFinite(maximum) || maximum <= 0.0)
                return (normal, 0.0f);

            var quality = (middle - minimum) / maximum;
            if (quality <= 0.0) return (normal, 0.0f);
            if (quality >= 1.0) return (normal, 1.0f);
            return (normal, (float)quality);
        }

        private static bool IsFinite(double value)
            => !double.IsNaN(value) && !double.IsInfinity(value);

        #endregion

        #region EstimateNormalsAndLocalDensity

        /// <summary>
        /// Estimates normals from k-nearest neighbours and local density as average squared distance of k-nearest points to their centroid. 
        /// </summary>
        public static (V3f[] normals, float[] densities) EstimateNormalsAndLocalDensity(this V3f[] points, int k, PointRkdTreeF<V3f[], V3f> kdtree)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (k < 3) throw new ArgumentOutOfRangeException($"Expected k >= 3, but k is {k}.");
            if (kdtree == null) throw new ArgumentNullException(nameof(kdtree));

            var ns = points.Length > 0 ? new V3f[points.Length] : Array.Empty<V3f>();
            var ds = points.Length > 0 ? new float[points.Length] : Array.Empty<float>();

            for (var i = 0; i < points.Length; i++)
            {
                var p = points[i];
                if (k > points.Length) k = points.Length;

                // find k closest points
                var closest = kdtree.GetClosest(p, float.MaxValue, k);
                if (closest.Count == 0)
                {
                    ns[i] = V3f.Zero;
                    ds[i] = 0;
                }
                else
                {
                    // compute centroid of k closest points
                    var c = points[closest[0].Index];
                    for (var j = 1; j < k; j++) c += points[closest[j].Index];
                    c /= k;

                    // compute covariance matrix of k closest points relative to centroid
                    var squaredDistSum = 0.0f;
                    var cvm = M33f.Zero;
                    for (var j = 0; j < k; j++)
                    {
                        var d = points[closest[j].Index] - c;
                        squaredDistSum += d.LengthSquared;
                        cvm.AddOuterProduct(d);
                    }
                    cvm /= k;

                    // solve eigensystem -> eigenvector for smallest eigenvalue gives normal 
                    Eigensystems.Dsyevh3((M33d)cvm, out M33d q, out V3d w);
                    ns[i] = (V3f)((w.X < w.Y) ? ((w.X < w.Z) ? q.C0 : q.C2) : ((w.Y < w.Z) ? q.C1 : q.C2));
                    ds[i] = squaredDistSum * (1.0f / k);
                }
            }

            return (normals: ns, densities: ds);
        }

        /// <summary>
        /// Estimates normals from k-nearest neighbours and local density as average squared distance of k-nearest points to their centroid. 
        /// </summary>
        public static (V3f[] normals, float[] densities) EstimateNormalsAndLocalDensity(this V3d[] points, int k, PointRkdTreeD<V3d[], V3d> kdtree)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (k < 3) throw new ArgumentOutOfRangeException($"Expected k >= 3, but k is {k}.");
            if (kdtree == null) throw new ArgumentNullException(nameof(kdtree));

            var ns = points.Length > 0 ? new V3f[points.Length] : Array.Empty<V3f>();
            var ds = points.Length > 0 ? new float[points.Length] : Array.Empty<float>();

            for (var i = 0; i < points.Length; i++)
            {
                var p = points[i];
                if (k > points.Length) k = points.Length;

                // find k closest points
                var closest = kdtree.GetClosest(p, float.MaxValue, k);
                if (closest.Count == 0)
                {
                    ns[i] = V3f.Zero;
                    ds[i] = 0;
                }
                else
                {
                    // compute centroid of k closest points
                    var c = points[closest[0].Index];
                    for (var j = 1; j < k; j++) c += points[closest[j].Index];
                    c /= k;

                    // compute covariance matrix of k closest points relative to centroid
                    var squaredDistSum = 0.0;
                    var cvm = M33d.Zero;
                    for (var j = 0; j < k; j++)
                    {
                        var d = points[closest[j].Index] - c;
                        squaredDistSum += d.LengthSquared;
                        cvm.AddOuterProduct(d);
                    }
                    cvm /= k;

                    // solve eigensystem -> eigenvector for smallest eigenvalue gives normal 
                    Eigensystems.Dsyevh3(cvm, out M33d q, out V3d w);
                    ns[i] = (V3f)((w.X < w.Y) ? ((w.X < w.Z) ? q.C0 : q.C2) : ((w.Y < w.Z) ? q.C1 : q.C2));
                    ds[i] = (float)(squaredDistSum * (1.0 / k));
                }
            }

            return (normals: ns, densities: ds);
        }


        /// <summary>
        /// Estimates normals from k-nearest neighbours and local density as average squared distance of k-nearest points to their centroid.  
        /// </summary>
        public static async Task<(V3f[] normals, float[] densities)> EstimateNormalsAndLocalDensityAsync(this V3f[] points, int k, PointRkdTreeF<V3f[], V3f> kdtree)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (k < 3) throw new ArgumentOutOfRangeException($"Expected k >= 3, but k is {k}.");
            if (kdtree == null) throw new ArgumentNullException(nameof(kdtree));
            return await Task.Run(() => EstimateNormalsAndLocalDensity(points, k, kdtree));
        }

        /// <summary>
        /// Estimates normals from k-nearest neighbours and local density as average squared distance of k-nearest points to their centroid.  
        /// </summary>
        public static async Task<(V3f[] normals, float[] densities)> EstimateNormalsAndLocalDensityAsync(this V3d[] points, int k, PointRkdTreeD<V3d[], V3d> kdtree)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (k < 3) throw new ArgumentOutOfRangeException($"Expected k >= 3, but k is {k}.");
            if (kdtree == null) throw new ArgumentNullException(nameof(kdtree));
            return await Task.Run(() => EstimateNormalsAndLocalDensity(points, k, kdtree));
        }


        /// <summary>
        /// Estimates normals from k-nearest neighbours and local density as average squared distance of k-nearest points to their centroid.  
        /// Computes temporary kd-tree! If you already have a kd-tree for given points, use overload which takes a kd-tree instead.
        /// </summary>
        public static (V3f[] normals, float[] densities) EstimateNormalsAndLocalDensity(this V3f[] points, int k)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (k < 3) throw new ArgumentOutOfRangeException($"Expected k >= 3, but k is {k}.");
            return EstimateNormalsAndLocalDensity(points, k, points.BuildKdTree());
        }

        /// <summary>
        /// Estimates normals from k-nearest neighbours and local density as average squared distance of k-nearest points to their centroid.  
        /// Computes temporary kd-tree! If you already have a kd-tree for given points, use overload which takes a kd-tree instead.
        /// </summary>
        public static (V3f[] normals, float[] densities) EstimateNormalsAndLocalDensity(this V3d[] points, int k)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (k < 3) throw new ArgumentOutOfRangeException($"Expected k >= 3, but k is {k}.");
            return EstimateNormalsAndLocalDensity(points, k, points.BuildKdTree());
        }


        /// <summary>
        /// Estimates normals from k-nearest neighbours and local density as average squared distance of k-nearest points to their centroid.
        /// Computes temporary kd-tree! If you already have a kd-tree for given points, use overload which takes a kd-tree instead.
        /// </summary>
        public static async Task<(V3f[] normals, float[] densities)> EstimateNormalsAndLocalDensityAsync(this V3f[] points, int k)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (k < 3) throw new ArgumentOutOfRangeException($"Expected k >= 3, but k is {k}.");
            return await EstimateNormalsAndLocalDensityAsync(points, k, await points.BuildKdTreeAsync());
        }

        /// <summary>
        /// Estimates normals from k-nearest neighbours and local density as average squared distance of k-nearest points to their centroid.
        /// Computes temporary kd-tree! If you already have a kd-tree for given points, use overload which takes a kd-tree instead.
        /// </summary>
        public static async Task<(V3f[] normals, float[] densities)> EstimateNormalsAndLocalDensityAsync(this V3d[] points, int k)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (k < 3) throw new ArgumentOutOfRangeException($"Expected k >= 3, but k is {k}.");
            return await EstimateNormalsAndLocalDensityAsync(points, k, await points.BuildKdTreeAsync());
        }

        #endregion
    }
}
