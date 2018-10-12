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

namespace Aardvark.Geometry.Clustering
{
    /// <summary>
    /// Extensions on int arrays for clustering. The int array needs to be
    /// initialized with SetByIndex(i => i), so that each element is contained
    /// in its own cluster. With the methods ClusterMergeLeft and ClusterMerge
    /// </summary>
    public static class ClusteringExtensions
    {
        /// <summary>
        /// Obtain the cluster index of item i in the given cluster index
        /// array. Note that hte cluster index array may be modified to
        /// improve subsequent performance.
        /// </summary>
        public static int GetClusterIndex(this int[] clusterIndexArray, int i)
        {
            int ci = clusterIndexArray[i];
            if (clusterIndexArray[i] != ci)
            {
                do { ci = clusterIndexArray[ci]; } while (clusterIndexArray[ci] != ci);
                clusterIndexArray[i] = ci;
            }
            return ci;
        }

        /// <summary>
        /// Given two cluster indices (ci and cj) obtained with
        /// <see cref="GetClusterIndex"/> and an item index (j) merge the two
        /// clusters so that the get the left cluster index (ci), and add the
        /// item to the combined cluster.
        /// </summary>
        public static void ClusterMergeLeft(
                this int[] clusterIndexArray, int ci, int cj, int j)
        {
            clusterIndexArray[cj] = ci; clusterIndexArray[j] = ci;
        }

        /// <summary>
        /// Given two cluster indices (ci and cj) obtained with
        /// <see cref="GetClusterIndex"/> and an item index (i) merge the two
        /// clusters so that the get the right cluster index (cj) and add the
        /// item to the combined cluster.  In a typical double loop with the
        /// outer loop iterating over elements i with cluster ci, and the
        /// inner loop iterating over elements j with cluster cj, this call
        /// must be followed by updating the cluster of the outer element
        /// with the following assignment: ci = cj.
        /// </summary>
        public static void ClusterMergeRight(
                this int[] clusterIndexArray, int ci, int cj, int i)
        {
            clusterIndexArray[ci] = cj; clusterIndexArray[i] = cj;
        }

        /// <summary>
        /// After performing all necessary merges, this call consolidates all
        /// entries of the cluster index array to directly point to the root
        /// of their cluster.
        /// </summary>
        /// <param name="clusterIndexArray"></param>
        public static void ClusterConsolidate(this int[] clusterIndexArray)
        {
            var count = clusterIndexArray.Length;
            var ca = clusterIndexArray;
            for (int i = 0; i < count; i++)
            {
                int ci = ca[i]; if (ca[ci] != ci) { do { ci = ca[ci]; } while (ca[ci] != ci); ca[i] = ci; }
            }
        }

        public static void ClusterConsolidate(this List<int> clusterIndexList)
        {
            var count = clusterIndexList.Count;
            var ca = clusterIndexList;
            for (int i = 0; i < count; i++)
            {
                int ci = ca[i]; if (ca[ci] != ci) { do { ci = ca[ci]; } while (ca[ci] != ci); ca[i] = ci; }
            }
        }
        
        /// <summary>
        /// Finally this call compacts the cluster indices to be contiguous
        /// and returns a cluster count array containing the counts of each
        /// cluster.
        /// </summary>
        /// <param name="clusterIndexArray"></param>
        /// <returns></returns>
        public static int[] CompactAndComputeCountArray(
                this int[] clusterIndexArray)
        {
            var count = clusterIndexArray.Length;
            var cc = -1;
            for (int i = 0; i < count; i++)
            {
                if (clusterIndexArray[i] != i) continue;
                clusterIndexArray[i] = cc--;
            }
            var clusterCount = -cc - 1;

            var clusterCountArray = new int[clusterCount];

            for (int i = 0; i < count; i++)
            {
                int ci = clusterIndexArray[i];
                if (ci < 0)
                    clusterCountArray[-ci - 1] = i;
                else
                {
                    ci = clusterIndexArray[ci];
                    clusterIndexArray[i] = -ci - 1;
                }
            }
            for (int i = 0; i < clusterCount; i++)
                clusterIndexArray[clusterCountArray[i]] = i;
            clusterCountArray.Set(0);

            for (int i = 0; i < count; i++)
                clusterCountArray[clusterIndexArray[i]]++;

            return clusterCountArray;
        }

        public static int[] CompactAndComputeCountArray(
                this List<int> clusterIndexList)
        {
            var count = clusterIndexList.Count;
            var cc = -1;
            for (int i = 0; i < count; i++)
            {
                if (clusterIndexList[i] != i) continue;
                clusterIndexList[i] = cc--;
            }
            var clusterCount = -cc - 1;

            var clusterCountArray = new int[clusterCount];

            for (int i = 0; i < count; i++)
            {
                int ci = clusterIndexList[i];
                if (ci < 0)
                    clusterIndexList[i] = -ci - 1;
                else
                {
                    ci = clusterIndexList[ci];
                    clusterIndexList[i] = ci < 0 ? -ci - 1 : ci;
                }
            }
            for (int i = 0; i < clusterCount; i++)
                clusterIndexList[clusterCountArray[i]] = i;
            clusterCountArray.Set(0);

            for (int i = 0; i < count; i++)
                clusterCountArray[clusterIndexList[i]]++;

            return clusterCountArray;
        }

        /// <summary>
        /// For the given point array, cluster count array, and cluster
        /// index array compute the cluster centroid points.
        /// </summary>
        public static V3d[] ClusterCentroidArray(
                this V3d[] array,
                int[] clusterCountArray,
                int[] clusterIndexArray)
        {
            return array.ClusterCentroidArray(clusterCountArray, clusterIndexArray,
                                              V3d.Zero, (v0, v1) => v0 + v1, (v, s) => v * s);
        }

        public static C4f[] ClusterCentroidArray(
                this C4f[] array,
                int[] clusterCountArray,
                int[] clusterIndexArray)
        {
            return array.ClusterCentroidArray(clusterCountArray, clusterIndexArray,
                                              C4f.Black, (v0, v1) => v0 + v1, (v, s) => v * s);
        }

        public static T[] ClusterCentroidArray<T>(
                this T[] array,
                int[] clusterCountArray,
                int[] clusterIndexArray,
                T zero,
                Func<T,T,T> addFun,
                Func<T,double,T> scaleFun
                )
        {
            var clusterCount = clusterCountArray.Length;
            T[] cpa = new T[clusterCount].Set(zero);
            var pointCount = array.Length;
            for (int pi = 0; pi < pointCount; pi++)
            {
                var ci = clusterIndexArray[pi];
                cpa[ci] = addFun(cpa[ci], array[pi]);
            }
            for (int ci = 0; ci < clusterCount; ci++)
                cpa[ci] = scaleFun(cpa[ci], 1.0 / (double)clusterCountArray[ci]);
            return cpa;
        }
    }

    public class DynamicClustering
    {
        protected List<int> m_indexList;

        public DynamicClustering()
        {
            m_indexList = new List<int>();
        }

        /// <summary>
        /// The number of clusters.
        /// </summary>
        public int Count { get; private set; }

        /// <summary>
        /// An array containing the sizes of all clusters.
        /// </summary>
        public int[] CountArray { get; private set; }

        /// <summary>
        /// An array that contains index of the clsuter for each item.
        /// </summary>
        public List<int> IndexList => m_indexList;

        public void AddItem()
        {
            var index = m_indexList.Count;
            m_indexList.Add(index);
        }

        public void MergeLeft(int i, int j)
        {
            var ca = m_indexList;
            int ci = ca[i]; if (ca[ci] != ci) { do { ci = ca[ci]; } while (ca[ci] != ci); ca[i] = ci; }
            int cj = ca[j]; if (ca[cj] != cj) { do { cj = ca[cj]; } while (ca[cj] != cj); ca[j] = cj; }
            ca[cj] = ci; ca[j] = ci;
        }

        public void MergeRight(int i, int j)
        {
            var ca = m_indexList;
            int ci = ca[i]; if (ca[ci] != ci) { do { ci = ca[ci]; } while (ca[ci] != ci); ca[i] = ci; }
            int cj = ca[j]; if (ca[cj] != cj) { do { cj = ca[cj]; } while (ca[cj] != cj); ca[j] = cj; }
            ca[ci] = cj; ca[i] = cj;
        }

        public void Init()
        {
            m_indexList.ClusterConsolidate();
            CountArray = m_indexList.CompactAndComputeCountArray();
            Count = CountArray.Length;
        }
    }
    
    public class Clustering
    {
        protected int m_count;
        protected int[] m_countArray;
        protected int[] m_indexArray;

        /// <summary>
        /// The number of clusters.
        /// </summary>
        public int Count => m_count;

        /// <summary>
        /// An array containing the sizes of all clusters.
        /// </summary>
        public int[] CountArray => m_countArray;

        /// <summary>
        /// An array that contains index of the clsuter for each item.
        /// </summary>
        public int[] IndexArray => m_indexArray;

        protected void Alloc(int count)
        {
            m_indexArray = new int[count].SetByIndex(i => i);
        }

        protected void Init()
        {
            m_indexArray.ClusterConsolidate();
            m_countArray = m_indexArray.CompactAndComputeCountArray();
            m_count = m_countArray.Length;
        }

        public void MergeLeft(int i, int j)
        {
            var ca = m_indexArray;
            int ci = ca[i]; if (ca[ci] != ci) { do { ci = ca[ci]; } while (ca[ci] != ci); ca[i] = ci; }
            int cj = ca[j]; if (ca[cj] != cj) { do { cj = ca[cj]; } while (ca[cj] != cj); ca[j] = cj; }
            ca[cj] = ci; ca[j] = ci;
        }

        public void MergeRight(int i, int j)
        {
            var ca = m_indexArray;
            int ci = ca[i]; if (ca[ci] != ci) { do { ci = ca[ci]; } while (ca[ci] != ci); ca[i] = ci; }
            int cj = ca[j]; if (ca[cj] != cj) { do { cj = ca[cj]; } while (ca[cj] != cj); ca[j] = cj; }
            ca[ci] = cj; ca[i] = cj;
        }

        public void ClusterMinSize(int minSize)
        {
            int clusterCount = 0;
            int count = 0;
            for (int i = 0; i < m_count; i++)
            {
                if (m_countArray[i] < minSize)
                {
                    count += m_countArray[i];
                    m_countArray[i] = -1;
                }
                else
                    ++clusterCount;
            }
            var newCount = clusterCount + 1;

            if (count <= 0) return;
            if (newCount > m_count) return;

            var forwardMap = new int[m_count];
            var newCountArray = new int[newCount];

            var nc = 0;
            for (int i = 0; i < m_count; i++)
            {
                var c = m_countArray[i];
                var ni = c < 0 ? clusterCount : nc++;
                forwardMap[i] = ni;
                newCountArray[ni] = c;
            }
            newCountArray[clusterCount] = count;

            m_count = newCount;
            m_countArray = newCountArray;
            m_indexArray.Apply(ci => forwardMap[ci]);
        }

        public int[] ClusterSortedIndex(int[] startArray = null)
        {
            var count = m_indexArray.Length;
            if (startArray == null)
            {
                startArray = m_countArray.Copy(m_count);
                startArray.Integrate();
            }
            var index = new int[count];
            for (int i = 0; i < count; i++)
                index[startArray[m_indexArray[i]]++] = i;
            return index;
        }

        public T[] ClusterSorted<T>(T[] array, int[] startArray = null)
        {
            var count = m_indexArray.Length;
            if (startArray == null)
            {
                startArray = m_countArray.Copy(m_count);
                startArray.Integrate();
            }
            var sorted = new T[count];
            for (int i = 0; i < count; i++)
                sorted[startArray[m_indexArray[i]]++] = array[i];
            return sorted;
        }

        public V3d[] CentroidArray(V3d[] array)
        {
            return array.ClusterCentroidArray(m_countArray, m_indexArray,
                                              V3d.Zero, (v0, v1) => v0 + v1, (v, s) => v * s);
        }

        public C4f[] CentroidArray(C4f[] array)
        {
            return array.ClusterCentroidArray(m_countArray, m_indexArray,
                                              C4f.Black, (v0, v1) => v0 + v1, (v, s) => v * s);
        }

        public T[] ClusteredArray<T>(
                T[] array, T zero, Func<T, T, T> addFun, Func<T, double, T> scaleFun)
        {
            return array.ClusterCentroidArray(m_countArray, m_indexArray, zero, addFun, scaleFun);
        }

        public int[] CreateBackToFirstMap()
        {
            return m_indexArray.CreateBackToFirstMap(m_count);
        }
    }

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
        /// distance from each other. This algorithm works well if the
        /// average point distance is not too much smaller than the given
        /// delta.
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
    
    #region Hashing

    public static class V3dClusteringHashExtensions
    {
        public static int HashCode1(this V2d point, double epsilon)
        {
            var xi = (long)Math.Floor(point.X / epsilon);
            var yi = (long)Math.Floor(point.Y / epsilon);

            return HashCode.Combine((int)xi, (int)yi);
        }

        public static int HashCode1(this V2d point, V2d epsilon)
        {
            var xi = (long)Math.Floor(point.X / epsilon.X);
            var yi = (long)Math.Floor(point.Y / epsilon.Y);
 
            return HashCode.Combine((int)xi, (int)yi);
        }

        public static int HashCode1(this V3d point, double epsilon)
        {
            var xi = (long)Math.Floor(point.X / epsilon);
            var yi = (long)Math.Floor(point.Y / epsilon);
            var zi = (long)Math.Floor(point.Z / epsilon);

            return HashCode.Combine((int)xi, (int)yi, (int)zi);
        }

        public static int HashCode1(this V3d point, V3d epsilon)
        {
            var xi = (long)Math.Floor(point.X / epsilon.X);
            var yi = (long)Math.Floor(point.Y / epsilon.Y);
            var zi = (long)Math.Floor(point.Z / epsilon.Z);

            return HashCode.Combine((int)xi, (int)yi, (int)zi);
        }

        public static int HashCode1of4(this V2d point, V2d epsilon)
        {
            var xi = (long)Math.Floor(point.X / epsilon.X);
            var yi = (long)Math.Floor(point.Y / epsilon.Y);

            return HashCode.Combine((int)(xi >> 1), (int)(yi >> 1));
        }

        public static void HashCodes4(this V2d point, V2d epsilon, int[] hca)
        {
            var xi = (long)Math.Floor(point.X / epsilon.X);
            var yi = (long)Math.Floor(point.Y / epsilon.Y);

            int xh0 = (int)(xi >> 1), xh1 = xh0 - 1 + ((int)(xi & 1) << 1);
            int yh0 = (int)(yi >> 1), yh1 = yh0 - 1 + ((int)(yi & 1) << 1);

            hca[0] = HashCode.Combine(xh0, yh0);
            hca[1] = HashCode.Combine(xh1, yh0);
            hca[2] = HashCode.Combine(xh0, yh1);
            hca[3] = HashCode.Combine(xh1, yh1);
        }

        public static int HashCode1of4(this V2d point, double epsilon)
        {
            var xi = (long)Math.Floor(point.X / epsilon);
            var yi = (long)Math.Floor(point.Y / epsilon);

            return HashCode.Combine((int)(xi >> 1), (int)(yi >> 1));
        }

        public static void HashCodes4(this V2d point, double epsilon, int[] hca)
        {
            var xi = (long)Math.Floor(point.X / epsilon);
            var yi = (long)Math.Floor(point.Y / epsilon);

            int xh0 = (int)(xi >> 1), xh1 = xh0 - 1 + ((int)(xi & 1) << 1);
            int yh0 = (int)(yi >> 1), yh1 = yh0 - 1 + ((int)(yi & 1) << 1);

            hca[0] = HashCode.Combine(xh0, yh0);
            hca[1] = HashCode.Combine(xh1, yh0);
            hca[2] = HashCode.Combine(xh0, yh1);
            hca[3] = HashCode.Combine(xh1, yh1);
        }
        
        public static int HashCode1of8(this V3d point, double epsilon)
        {
            var xi = (long)Math.Floor(point.X / epsilon);
            var yi = (long)Math.Floor(point.Y / epsilon);
            var zi = (long)Math.Floor(point.Z / epsilon);

            return HashCode.Combine((int)(xi >> 1), (int)(yi >> 1), (int)(zi >> 1));
        }

        public static void HashCodes8(this V3d point, double epsilon, int[] hca)
        {
            var xi = (long)Math.Floor(point.X / epsilon);
            var yi = (long)Math.Floor(point.Y / epsilon);
            var zi = (long)Math.Floor(point.Z / epsilon);

            int xh0 = (int)(xi >> 1), xh1 = xh0 - 1 + ((int)(xi & 1) << 1);
            int yh0 = (int)(yi >> 1), yh1 = yh0 - 1 + ((int)(yi & 1) << 1);
            int zh0 = (int)(zi >> 1), zh1 = zh0 - 1 + ((int)(zi & 1) << 1);

            hca[0] = HashCode.Combine(xh0, yh0, zh0);
            hca[1] = HashCode.Combine(xh1, yh0, zh0);
            hca[2] = HashCode.Combine(xh0, yh1, zh0);
            hca[3] = HashCode.Combine(xh1, yh1, zh0);
            hca[4] = HashCode.Combine(xh0, yh0, zh1);
            hca[5] = HashCode.Combine(xh1, yh0, zh1);
            hca[6] = HashCode.Combine(xh0, yh1, zh1);
            hca[7] = HashCode.Combine(xh1, yh1, zh1);
        }

        public static int HashCode1of8(this V3d point, V3d epsilon)
        {
            var xi = (long)Math.Floor(point.X / epsilon.X);
            var yi = (long)Math.Floor(point.Y / epsilon.Y);
            var zi = (long)Math.Floor(point.Z / epsilon.Z);

            return HashCode.Combine((int)(xi >> 1), (int)(yi >> 1), (int)(zi >> 1));
        }

        public static void HashCodes8(this V3d point, V3d epsilon, int[] hca)
        {
            var xi = (long)Math.Floor(point.X / epsilon.X);
            var yi = (long)Math.Floor(point.Y / epsilon.Y);
            var zi = (long)Math.Floor(point.Z / epsilon.Z);

            int xh0 = (int)(xi >> 1), xh1 = xh0 - 1 + ((int)(xi & 1) << 1);
            int yh0 = (int)(yi >> 1), yh1 = yh0 - 1 + ((int)(yi & 1) << 1);
            int zh0 = (int)(zi >> 1), zh1 = zh0 - 1 + ((int)(zi & 1) << 1);

            hca[0] = HashCode.Combine(xh0, yh0, zh0);
            hca[1] = HashCode.Combine(xh1, yh0, zh0);
            hca[2] = HashCode.Combine(xh0, yh1, zh0);
            hca[3] = HashCode.Combine(xh1, yh1, zh0);
            hca[4] = HashCode.Combine(xh0, yh0, zh1);
            hca[5] = HashCode.Combine(xh1, yh0, zh1);
            hca[6] = HashCode.Combine(xh0, yh1, zh1);
            hca[7] = HashCode.Combine(xh1, yh1, zh1);
        }

        public static int HashCode1of16(
                this V3d normal, double dist,
                double epsNormal, double epsDist)
        {
            var xi = (long)Math.Floor(normal.X / epsNormal);
            var yi = (long)Math.Floor(normal.Y / epsNormal);
            var zi = (long)Math.Floor(normal.Z / epsNormal);
            var di = (long)Math.Floor(dist / epsDist);

            return HashCode.Combine((int)(xi >> 1), (int)(yi >> 1), (int)(zi >> 1), (int)(di >> 1));
        }

        public static void HashCodes16(
                this V3d normal, double dist,
                double epsNormal, double epsDist,
                int[] hca)
        {
            var xi = (long)Math.Floor(normal.X / epsNormal);
            var yi = (long)Math.Floor(normal.Y / epsNormal);
            var zi = (long)Math.Floor(normal.Z / epsNormal);
            var di = (long)Math.Floor(dist / epsDist);

            int xh0 = (int)(xi >> 1), xh1 = xh0 - 1 + ((int)(xi & 1) << 1);
            int yh0 = (int)(yi >> 1), yh1 = yh0 - 1 + ((int)(yi & 1) << 1);
            int zh0 = (int)(zi >> 1), zh1 = zh0 - 1 + ((int)(zi & 1) << 1);
            int dh0 = (int)(di >> 1), dh1 = dh0 - 1 + ((int)(di & 1) << 1);

            hca[ 0] = HashCode.Combine(xh0, yh0, zh0, dh0);
            hca[ 1] = HashCode.Combine(xh1, yh0, zh0, dh0);
            hca[ 2] = HashCode.Combine(xh0, yh1, zh0, dh0);
            hca[ 3] = HashCode.Combine(xh1, yh1, zh0, dh0);
            hca[ 4] = HashCode.Combine(xh0, yh0, zh1, dh0);
            hca[ 5] = HashCode.Combine(xh1, yh0, zh1, dh0);
            hca[ 6] = HashCode.Combine(xh0, yh1, zh1, dh0);
            hca[ 7] = HashCode.Combine(xh1, yh1, zh1, dh0);
            hca[ 8] = HashCode.Combine(xh0, yh0, zh0, dh1);
            hca[ 9] = HashCode.Combine(xh1, yh0, zh0, dh1);
            hca[10] = HashCode.Combine(xh0, yh1, zh0, dh1);
            hca[11] = HashCode.Combine(xh1, yh1, zh0, dh1);
            hca[12] = HashCode.Combine(xh0, yh0, zh1, dh1);
            hca[13] = HashCode.Combine(xh1, yh0, zh1, dh1);
            hca[14] = HashCode.Combine(xh0, yh1, zh1, dh1);
            hca[15] = HashCode.Combine(xh1, yh1, zh1, dh1);
        }    
    }

    #endregion
}
