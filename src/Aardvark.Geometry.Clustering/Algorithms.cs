/*
    Copyright (C) 2006-2020. Aardvark Platform Team. http://github.com/aardvark-platform.
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.
    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/
using Aardvark.Base;
using System;

namespace Aardvark.Geometry.Clustering
{
    public class V4dClustering : Clustering
    {
        #region Constructor

        /// <summary>
        /// Create clusters for all vecs which are within a supplied delta
        /// distance from each other. This algorithm works well if the
        /// average vec distance is not too much smaller than the given
        /// delta.
        /// </summary>
        public V4dClustering(V4d[] va, double delta)
        {
            var count = va.Length;
            Alloc(count);
            var ca = m_indexArray;
            var sa = new int[count].Set(1);
            var kdTree = va.CreateRkdTree(Metric.Euclidean, 1e-8);
            var query = kdTree.CreateClosestToPointQuery(delta, 0);
            for (int i = 0; i < count; i++)
            {
                int ci = ca[i]; if (ca[ci] != ci) { do { ci = ca[ci]; } while (ca[ci] != ci); ca[i] = ci; }
                int si = sa[ci];
                foreach (var id in kdTree.GetClosest(query, va[i]))
                {
                    int j = (int)id.Index;
                    int cj = ca[j]; if (ca[cj] != cj) { do { cj = ca[cj]; } while (ca[cj] != cj); ca[j] = cj; }
                    if (ci == cj) continue;
                    int sj = sa[cj];
                    if (si < sj) { ca[ci] = cj; ca[i] = cj; ci = cj; }
                    else { ca[cj] = ci; ca[j] = ci; }
                    si += sj; sa[ci] = si;
                }
                query.Clear();
            }
            Init();
        }

        #endregion
    }

    public class VecClustering : Clustering
    {
        #region Constructor

        /// <summary>
        /// Create clusters for all vecs which are within a spupplied delta
        /// distance from each other. This algorihtm works well if the
        /// average vec distance is not too much smaller than the given
        /// delta.
        /// </summary>
        public VecClustering(VecArray<double> va, double delta)
        {
            var count = (int)va.Count;
            Alloc(count);
            var ca = m_indexArray;
            var sa = new int[count].Set(1);
            var kdTree = va.CreateRkdTree(Metric.Euclidean, 1e-8);
            var query = kdTree.CreateClosestToPointQuery(delta, 0);
            for (int i = 0; i < count; i++)
            {
                int ci = ca[i]; if (ca[ci] != ci) { do { ci = ca[ci]; } while (ca[ci] != ci); ca[i] = ci; }
                int si = sa[ci];
                foreach (var id in kdTree.GetClosest(query, va[i]))
                {
                    int j = (int)id.Index;
                    int cj = ca[j]; if (ca[cj] != cj) { do { cj = ca[cj]; } while (ca[cj] != cj); ca[j] = cj; }
                    if (ci == cj) continue;
                    int sj = sa[cj];
                    if (si < sj) { ca[ci] = cj; ca[i] = cj; ci = cj; }
                    else { ca[cj] = ci; ca[j] = ci; }
                    si += sj; sa[ci] = si;
                }
                query.Clear();
            }
            Init();
        }

        #endregion
    }

    public class PointClustering : Clustering
    {
        #region Constructor

        /// <summary>
        /// Create clusters for all points which are within a supplied delta
        /// distance from each other. This algorithm uses an rkd-tree and works 
        /// well if the average point distance is not too much smaller than the 
        /// given delta.
        /// </summary>
        public PointClustering(V3d[] pa, double delta)
        {
            var count = pa.Length;
            Alloc(count);
            var ca = m_indexArray;
            var sa = new int[count].Set(1);
            var kdTree = pa.CreateRkdTree(Metric.Euclidean, 1e-8);
            var query = kdTree.CreateClosestToPointQuery(delta, 0);
            for (int i = 0; i < count; i++)
            {
                int ci = ca[i]; if (ca[ci] != ci) { do { ci = ca[ci]; } while (ca[ci] != ci); ca[i] = ci; }
                int si = sa[ci];
                foreach (var id in kdTree.GetClosest(query, pa[i]))
                {
                    int j = (int)id.Index;
                    int cj = ca[j]; if (ca[cj] != cj) { do { cj = ca[cj]; } while (ca[cj] != cj); ca[j] = cj; }
                    if (ci == cj) continue;
                    int sj = sa[cj];
                    if (si < sj) { ca[ci] = cj; ca[i] = cj; ci = cj; }
                    else { ca[cj] = ci; ca[j] = ci; }
                    si += sj; sa[ci] = si;
                }
                query.Clear();
            }
            Init();
        }

        #endregion
    }

    public class NormalsClustering : Clustering
    {
        public readonly V3d[] SumArray;

        #region Constructor

        public NormalsClustering(V3d[] normalArray, double delta)
        {
            var count = normalArray.Length;
            Alloc(count);
            var ca = m_indexArray;
            var sa = new int[count].Set(1);
            var suma = SumArray;
            var kdTree = normalArray.CreateRkdTreeDistDotProduct(0);
            var query = kdTree.CreateClosestToPointQuery(delta, 0);
            for (int i = 0; i < count; i++)
            {
                int ci = ca[i]; if (ca[ci] != ci) { do { ci = ca[ci]; } while (ca[ci] != ci); ca[i] = ci; }
                int si = sa[ci];
                V3d avgNormali = suma[ci].Normalized;

                kdTree.GetClosest(query, avgNormali);
                kdTree.GetClosest(query, avgNormali.Negated);

                foreach (var id in query.List)
                {
                    int j = (int)id.Index;
                    int cj = ca[j]; if (ca[cj] != cj) { do { cj = ca[cj]; } while (ca[cj] != cj); ca[j] = cj; }
                    if (ci == cj) continue;

                    int sj = sa[cj];
                    V3d avgNormalj = suma[cj].Normalized;
                    double avgDot = avgNormali.Dot(avgNormalj);
                    if (avgDot.Abs() < 1.0 - 2.0 * delta) continue;

                    V3d sum = suma[ci] + (avgDot > 0 ? suma[cj] : suma[cj].Negated);
                    if (si < sj) { ca[ci] = cj; ca[i] = cj; ci = cj; }
                    else { ca[cj] = ci; ca[j] = ci; }
                    si += sj; sa[ci] = si; suma[ci] = sum;
                }
                query.Clear();
            }
            Init();
        }

        #endregion
    }

    public class PointEpsilonClustering : Clustering
    {
        #region Constructor

        /// <summary>
        /// Creates clusters for all points which are within an epsilon
        /// distance from each other. This algorithm uses a hash-grid and 
        /// only works fast if the supplied epsilon is small enough that 
        /// not too many points fall within each cluster. Thus it is ideal 
        /// for merging vertices in meshes and point sets, that are different 
        /// due to numerical inaccuracies.
        /// </summary>
        public PointEpsilonClustering(V3d[] pa, double epsilon, IRandomUniform rnd = null)
        {
            rnd = rnd ?? new RandomSystem();
            var count = pa.Length;
            Alloc(count);
            var ca = m_indexArray;
            var dict = new IntDict<int>(count, stackDuplicateKeys: true);
            int rndBits = 0; int bit = 0;
            var ha = new int[8];
            var eps2 = epsilon * epsilon;
            for (int i = 0; i < count; i++)
            {
                var p = pa[i]; p.HashCodes8(epsilon, ha);
                int ci = ca[i]; if (ca[ci] != ci) { do { ci = ca[ci]; } while (ca[ci] != ci); ca[i] = ci; }
                for (int hi = 0; hi < 8; hi++)
                    foreach (var j in dict.ValuesWithKey(ha[hi]))
                    {
                        int cj = ca[j]; if (ca[cj] != cj) { do { cj = ca[cj]; } while (ca[cj] != cj); ca[j] = cj; }
                        if (ci == cj || V3d.DistanceSquared(p, pa[j]) >= eps2) continue;
                        bit >>= 1; if (bit == 0) { rndBits = rnd.UniformInt(); bit = 1 << 30; }
                        if ((rndBits & bit) != 0) { ca[ci] = cj; ca[i] = cj; ci = cj; }
                        else { ca[cj] = ci; ca[j] = ci; }
                    }
                dict[ha[0]] = i;
            }
            Init();
        }

        #endregion
    }

    public class PointEqualClustering : Clustering
    {
        #region Constructor

        /// <summary>
        /// Merge clusters of exactly equal points. The supplied epsilon is used to define a 
        /// grid as acceleration data structure and the algorithm is only fast if not too many 
        /// points fall within the supplied epsilon.
        /// </summary>
        public PointEqualClustering(V3d[] pa, double eps, IRandomUniform rnd = null)
        {
            rnd = rnd ?? new RandomSystem();
            var count = pa.Length;
            Alloc(count);
            var ca = m_indexArray;
            var dict = new IntDict<int>(count, stackDuplicateKeys: true);
            int rndBits = 0; int bit = 0;
            for (int i = 0; i < count; i++)
            {
                var p = pa[i]; var hc = p.HashCode1(eps);
                int ci = ca[i]; if (ca[ci] != ci) { do { ci = ca[ci]; } while (ca[ci] != ci); ca[i] = ci; }
                foreach (var j in dict.ValuesWithKey(hc))
                {
                    int cj = ca[j]; if (ca[cj] != cj) { do { cj = ca[cj]; } while (ca[cj] != cj); ca[j] = cj; }
                    if (ci == cj || p != pa[j]) continue;
                    bit >>= 1; if (bit == 0) { rndBits = rnd.UniformInt(); bit = 1 << 30; }
                    if ((rndBits & bit) != 0) { ca[ci] = cj; ca[i] = cj; ci = cj; }
                    else { ca[cj] = ci; ca[j] = ci; }
                }
                dict[hc] = i;
            }
            Init();
        }

        #endregion
    }

    public class PointEpsilonClustering<TArray> : Clustering
    {
        #region Constructor

        /// <summary>
        /// Creates clusters for all points which are within an epsilon
        /// distance from each other. This algorithm uses a hash-grid and 
        /// only works fast if the supplied epsilon is small enough that 
        /// not too many points fall within each cluster. Thus it is ideal 
        /// for merging vertices in meshes and point sets, that are different 
        /// due to numerical inaccuracies.
        /// </summary>
        public PointEpsilonClustering(int count, TArray pa, Func<TArray, int, V3d> get, double eps = 1e-6, IRandomUniform rnd = null)
        {
            rnd = rnd ?? new RandomSystem();
            Alloc(count);
            var ca = m_indexArray;
            var dict = new IntDict<int>(count, stackDuplicateKeys: true);
            int rndBits = 0; int bit = 0;
            var ha = new int[8];
            var eps2 = eps * eps;
            for (int i = 0; i < count; i++)
            {
                var p = get(pa, i); p.HashCodes8(eps, ha);
                int ci = ca[i]; if (ca[ci] != ci) { do { ci = ca[ci]; } while (ca[ci] != ci); ca[i] = ci; }
                for (int hi = 0; hi < 8; hi++)
                    foreach (var j in dict.ValuesWithKey(ha[hi]))
                    {
                        int cj = ca[j]; if (ca[cj] != cj) { do { cj = ca[cj]; } while (ca[cj] != cj); ca[j] = cj; }
                        if (ci == cj || V3d.DistanceSquared(p, get(pa, j)) >= eps2) continue;
                        bit >>= 1; if (bit == 0) { rndBits = rnd.UniformInt(); bit = 1 << 30; }
                        if ((rndBits & bit) != 0) { ca[ci] = cj; ca[i] = cj; ci = cj; }
                        else { ca[cj] = ci; ca[j] = ci; }
                    }
                dict[ha[0]] = i;
            }
            Init();
        }

        #endregion
    }

    public class PointEqualClustering<TArray> : Clustering
    {
        #region Merge Clusters

        /// <summary>
        /// Merge clusters of exactly equal points. The supplied epsilon is used to define a 
        /// grid as acceleration data structure and the algorithm is only fast if not too many 
        /// points fall within the supplied epsilon.
        /// </summary>
        public PointEqualClustering(int count, TArray pa, Func<TArray, int, V3d> get, double eps = 1e-6, IRandomUniform rnd = null)
        {
            rnd = rnd ?? new RandomSystem();
            Alloc(count);
            var ca = m_indexArray;
            var dict = new IntDict<int>(count, stackDuplicateKeys: true);
            int rndBits = 0; int bit = 0;
            for (int i = 0; i < count; i++)
            {
                var p = get(pa, i); var hc = p.HashCode1(eps);
                int ci = ca[i]; if (ca[ci] != ci) { do { ci = ca[ci]; } while (ca[ci] != ci); ca[i] = ci; }
                foreach (var j in dict.ValuesWithKey(hc))
                {
                    int cj = ca[j]; if (ca[cj] != cj) { do { cj = ca[cj]; } while (ca[cj] != cj); ca[j] = cj; }
                    if (ci == cj || p != get(pa, j)) continue;
                    bit >>= 1; if (bit == 0) { rndBits = rnd.UniformInt(); bit = 1 << 30; }
                    if ((rndBits & bit) != 0) { ca[ci] = cj; ca[i] = cj; ci = cj; }
                    else { ca[cj] = ci; ca[j] = ci; }
                }
                dict[hc] = i;
            }
            Init();
        }

        #endregion
    }

    public class PlaneEpsilonClustering<TArray> : Clustering
    {
        #region Constructor

        /// <summary>
        /// Creates clusters of planes within a certain epsilon from each other
        /// (euclidean distance between normal vectors and between offsets). 
        /// This algorithm uses a 4d hash-grid and only works fast if the supplied 
        /// epsilons are small enough that not too many planes fall within each cluster.
        /// Thus it is ideal for merging planes with small variations in orientation and 
        /// offset due to numerical inaccuracies.
        /// </summary>
        public PlaneEpsilonClustering(
                int count, TArray pa,
                Func<TArray, int, V3d> getNormal,
                Func<TArray, int, double> getDist,
                double epsNormal, double epsDist,
                double deltaEpsFactor = 0.25, IRandomUniform rnd = null)
        {
            rnd = rnd ?? new RandomSystem();
            Alloc(count);
            var ca = m_indexArray;
            var dict = new IntDict<int>(count, stackDuplicateKeys: true);
            int rndBits = 0; int bit = 0;
            var ha = new int[16];
            var ne2 = epsNormal * epsNormal;
            var de2 = epsDist * epsDist;
            var deps = (ne2 + de2) * deltaEpsFactor * deltaEpsFactor;
            for (int i = 0; i < count; i++)
            {
                var ni = getNormal(pa, i); var di = getDist(pa, i);
                ni.HashCodes16(di, epsNormal, epsDist, ha);
                int ci = ca[i]; if (ca[ci] != ci) { do { ci = ca[ci]; } while (ca[ci] != ci); ca[i] = ci; }
                double dmin = double.MaxValue;
                for (int hi = 0; hi < 16; hi++)
                    foreach (var j in dict.ValuesWithKey(ha[hi]))
                    {
                        int cj = ca[j]; if (ca[cj] != cj) { do { cj = ca[cj]; } while (ca[cj] != cj); ca[j] = cj; }
                        if (ci == cj) continue;
                        var dd = Fun.Square(di - getDist(pa, j)); if (dd >= de2) continue;
                        var dn = V3d.DistanceSquared(ni, getNormal(pa, j)); if (dn >= ne2) continue;
                        var d = dn + dd; if (d < dmin) dmin = d;
                        bit >>= 1; if (bit == 0) { rnd.UniformInt(); bit = 1 << 30; }
                        if ((rndBits & bit) != 0) { ca[ci] = cj; ca[i] = cj; ci = cj; }
                        else { ca[cj] = ci; ca[j] = ci; }
                    }
                if (dmin > deps) dict[ha[0]] = i; // only sparsely populate hashtable for performance reasons
            }
            Init();
        }

        #endregion
    }
}
