/*
    Copyright (C) 2006-2025. Aardvark Platform Team. http://github.com/aardvark-platform.
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
        /// Obtains the root cluster index of item <paramref name="i"/> and fully compresses
        /// every parent link on the traversed path. The operation is allocation-free.
        /// </summary>
        public static int GetClusterIndex(this int[] clusterIndexArray, int i)
        {
            var root = clusterIndexArray[i];
            if (root == i) return root;
            while (clusterIndexArray[root] != root) root = clusterIndexArray[root];

            while (clusterIndexArray[i] != i)
            {
                var parent = clusterIndexArray[i];
                clusterIndexArray[i] = root;
                i = parent;
            }
            return root;
        }

        /// <summary>
        /// Obtains the root cluster index of item <paramref name="i"/> and fully compresses
        /// every parent link on the traversed path. The operation is allocation-free.
        /// </summary>
        public static int GetClusterIndex(this List<int> clusterIndexList, int i)
        {
            var root = clusterIndexList[i];
            if (root == i) return root;
            while (clusterIndexList[root] != root) root = clusterIndexList[root];

            while (clusterIndexList[i] != i)
            {
                var parent = clusterIndexList[i];
                clusterIndexList[i] = root;
                i = parent;
            }
            return root;
        }

        /// <summary>
        /// Given two cluster indices (ci and cj) obtained with
        /// <see cref="GetClusterIndex(int[], int)"/> and an item index (j) merge the two
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
        /// <see cref="GetClusterIndex(int[], int)"/> and an item index (i) merge the two
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
            for (var i = 0; i < clusterIndexArray.Length; i++)
                clusterIndexArray.GetClusterIndex(i);
        }

        /// <summary>
        /// Rewrites every list entry to directly reference its cluster root, fully compressing
        /// intermediate parent paths without allocating temporary storage.
        /// </summary>
        public static void ClusterConsolidate(this List<int> clusterIndexList)
        {
            for (var i = 0; i < clusterIndexList.Count; i++)
                clusterIndexList.GetClusterIndex(i);
        }
        
        /// <summary>
        /// Compacts consolidated root indices to contiguous IDs in ascending root-index order
        /// and returns the exact item count of each dense cluster. After the call, every item
        /// index is in the range [0, returned array length).
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

        /// <summary>
        /// Compacts consolidated root indices to contiguous IDs in ascending root-index order
        /// and returns the exact item count of each dense cluster. This is equivalent to the
        /// array overload.
        /// </summary>
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
                    clusterCountArray[-ci - 1] = i;
                else
                {
                    ci = clusterIndexList[ci];
                    clusterIndexList[i] = -ci - 1;
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
                                              C4f.Black, (v0, v1) => v0 + v1, (v, s) => v * (float) s);
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
            m_indexList = [];
        }

        /// <summary>
        /// The number of clusters.
        /// </summary>
        public int Count { get; private set; }

        /// <summary>
        /// Dense cluster counts. <c>CountArray[c]</c> is the number of items whose
        /// <see cref="IndexList"/> entry equals <c>c</c>.
        /// </summary>
        public int[] CountArray { get; private set; }

        /// <summary>
        /// A list containing the dense cluster ID for each item after <see cref="Init"/>.
        /// </summary>
        public List<int> IndexList => m_indexList;

        public void AddItem()
        {
            var index = m_indexList.Count;
            m_indexList.Add(index);
        }

        public void MergeLeft(int i, int j)
        {
            var ci = m_indexList.GetClusterIndex(i);
            var cj = m_indexList.GetClusterIndex(j);
            m_indexList[cj] = ci;
            m_indexList[j] = ci;
        }

        public void MergeRight(int i, int j)
        {
            var ci = m_indexList.GetClusterIndex(i);
            var cj = m_indexList.GetClusterIndex(j);
            m_indexList[ci] = cj;
            m_indexList[i] = cj;
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
        /// Dense cluster counts. <c>CountArray[c]</c> is the number of items whose
        /// <see cref="IndexArray"/> entry equals <c>c</c>.
        /// </summary>
        public int[] CountArray => m_countArray;

        /// <summary>
        /// An array containing the dense cluster ID for each item.
        /// </summary>
        public int[] IndexArray => m_indexArray;

        protected void Alloc(int count)
        {
            m_indexArray = new int[count].SetByIndex(i => i);
        }

        protected void Init()
        {
            m_indexArray.ClusterConsolidate();
            InitConsolidated();
        }

        protected void InitConsolidated()
        {
            m_countArray = m_indexArray.CompactAndComputeCountArray();
            m_count = m_countArray.Length;
        }

        public void MergeLeft(int i, int j)
        {
            var ci = m_indexArray.GetClusterIndex(i);
            var cj = m_indexArray.GetClusterIndex(j);
            m_indexArray[cj] = ci;
            m_indexArray[j] = ci;
        }

        public void MergeRight(int i, int j)
        {
            var ci = m_indexArray.GetClusterIndex(i);
            var cj = m_indexArray.GetClusterIndex(j);
            m_indexArray[ci] = cj;
            m_indexArray[i] = cj;
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
                                              C4f.Black, (v0, v1) => v0 + v1, (v, s) => v * (float) s);
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
}
