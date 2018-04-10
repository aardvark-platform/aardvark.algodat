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
using Aardvark.Base;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry
{
    /// <summary>
    /// A small structure holding an index and a distance that is used in
    /// the list that is returned by queries to the PointKdTree.
    /// </summary>
    public struct IndexDist<T> : IComparable<IndexDist<T>>
        where T : IComparable<T>
    {
        public readonly long Index;
        public readonly T Dist;

        #region Constructor

        public IndexDist(long index, T dist)
        {
            Index = index;
            Dist = dist;
        }

        #endregion

        #region IComparable<IndexDist<T>> Members

        public int CompareTo(IndexDist<T> other)
        {
            return Dist.CompareTo(other.Dist);
        }

        #endregion
    }

    public partial class PointRkdTreeD<TArray, TPoint>
    {


        private void GetClosestFlat(ClosestToPointQuery q, long top, long count) // count = 2,4,8
        {
            for (long end = Fun.Min(m_size, top + count);
                top < end; top = 2 * top + 1, count *= 2, end = Fun.Min(m_size, top + count))
            {
                for (long hi = top; hi < end; hi++)
                {
                    long index = m_perm[hi];
                    var dist = q.Dist(q.Point, m_aget(m_array, index));
                    if (dist <= q.MaxDist)
                    {
                        q.List.HeapDescendingEnqueue(new IndexDist<double>(index, dist));
                        if (q.List.Count > q.MaxCount)
                        {
                            q.List.HeapDescendingDequeue();
                            var md = q.List[0].Dist;
                            q.MaxDist = md; q.MaxDistEps = md + m_eps;
                        }
                    }

                }
            }
        }

        private void GetClosestFlatDynamic(ClosestToPointQuery q, long top, long count) // count = 2,4,8
        {
            for (long end = Fun.Min(m_size, top + count);
                top < end; top = 2 * top + 1, count *= 2, end = Fun.Min(m_size, top + count))
            {
                for (long hi = top; hi < end; hi++)
                {
                    long index = m_perm[hi];
                    var dist = q.Dist(q.Point, m_aget(m_array, index));
                    if (dist <= q.MaxDist) q.List.Add(new IndexDist<double>(index, dist));
                }
            }
        }

        /// <summary>
        /// A query object to handle multiple (possibly accumulated) closest
        /// to line queries.
        /// </summary>
        public class ClosestToLineQuery : ClosestQuery
        {
            public TPoint MinP, MaxP;
            public TPoint P0, P1;
            public double[] InvDelta;
            public double[] Scale;
            public DictSet<long> IndexSet;

            internal ClosestToLineQuery(
                    double maxDistance, double maxDistancePlusEps,
                    int maxCount, List<IndexDist<double>> list)
                : base(maxDistance, maxDistancePlusEps, maxCount, list)
            {
                IndexSet = null;
            }

            internal void Init(TPoint p0, TPoint p1, double[] vec)
            {
                var dim = vec.Length;
                var scale = new double[dim].Set(0.0);
                var invDelta = new double[dim];
                for (int d = 0; d < dim; d++)
                    invDelta[d] = Fun.IsTiny(vec[d]) ? 0.0 : 1.0 / vec[d];

                // Compute the scaling factors due to the projection of the
                // cylinder around the line onto the coorindate planes.
                for (int d0 = 0; d0 < dim - 1; d0++)
                {
                    var dx0 = vec[d0];
                    for (int d1 = d0 + 1; d1 < dim; d1++)
                    {
                        var dx1 = vec[d1];
                        var s = Fun.Sqrt(dx0 * dx0 + dx1 * dx1);
                        var s0 = Fun.IsTiny(dx1)
                                    ? double.MaxValue : s / Fun.Abs(dx1);
                        if (s0 > scale[d0]) scale[d0] = s0;
                        var s1 = Fun.IsTiny(dx0)
                                    ? double.MaxValue : s / Fun.Abs(dx0);
                        if (s1 > scale[d1]) scale[d1] = s1;
                    }
                }
                MinP = p0; MaxP = p1;
                P0 = p0; P1 = p1;
                InvDelta = invDelta; Scale = scale;
            }

            public override void Clear()
            {
                base.Clear();
                IndexSet = new DictSet<long>();
            }
        }

        /// <summary>
        /// Create a closest to line query object for a sequence of line
        /// queries that contribute to a single result. The parameters
        /// max distance and max count work the same way as in the
        /// </summary>
        public ClosestToLineQuery CreateClosestToLineQuery(
                double maxDistance, int maxCount)
        {
            var maxDistPlusEps = maxDistance < double.MaxValue
                    ? maxDistance + m_eps : maxDistance;
            var q = new ClosestToLineQuery(
                                maxDistance, maxDistPlusEps, maxCount,
                                StaticCreateList(maxCount))
                                { IndexSet = new DictSet<long>() };
            return q;
        }

        /// <summary>
        /// Add the query result from the supplied line to the closest to
        /// line query object. The accumulated result is returned, or can
        /// be retrieved as the List property of the query object.
        /// </summary>
        public List<IndexDist<double>> GetClosest(
                ClosestToLineQuery q, TPoint p0, TPoint p1)
        {
            var delta = new double[m_dim];
            for (int d = 0; d < m_dim; d++)
                delta[d] = m_vget(p1, d) - m_vget(p0, d);
            q.Init(p0, p1, delta);
            GetClosestToLine(q, 0);
            return q.List;
        }

        /// <summary>
        /// Return points closest to a sequence of lines.
        /// </summary>
        public List<IndexDist<double>> GetClosest(
                IEnumerable<(TPoint, TPoint)> lines,
                double maxDistance, int maxCount)
        {
            var q = CreateClosestToLineQuery(maxDistance, maxCount);
            foreach (var line in lines)
                GetClosest(q, line.Item1, line.Item2);
            return q.List;
        }

        public List<IndexDist<double>> GetClosest(
                (TPoint, TPoint) line, double maxDistance, int maxCount)
        {
            return GetClosestToLine(
                        line.Item1, line.Item2, maxDistance, maxCount);
        }

        /// <summary>
        /// Get the points in the rkd-tree that are closest to the supplied
        /// line segment p0...p1. It is possible to specify the maximal 
        /// distance up to which points are searched, or the maximal number
        /// of points that should be retrieved. At least one of these two
        /// constraints needs to be specified. If you want the distance
        /// constraint only, set maxCount to 0, if you want the number
        /// constraint only, set maxDistance to double.MaxValue. 
        /// </summary>
        public List<IndexDist<double>> GetClosestToLine(
                TPoint p0, TPoint p1, double maxDistance, int maxCount)
        {
            var delta = new double[m_dim];
            for (int d = 0; d < m_dim; d++)
                delta[d] = m_vget(p1, d) - m_vget(p0, d);
            var maxDistPlusEps = maxDistance < double.MaxValue
                                    ? maxDistance + m_eps : maxDistance;
            var q = new ClosestToLineQuery(
                            maxDistance, maxDistPlusEps, maxCount,
                            StaticCreateList(maxCount));
            q.Init(p0, p1, delta);
            GetClosestToLine(q, 0);
            return q.List;
        }

        private void GetClosestToLine(ClosestToLineQuery q, long top)
        {
            long index = m_perm[top];
            var topPoint = m_aget(m_array, index);
            var dist = m_lineDist(topPoint, q.P0, q.P1);
            long t1 = 2 * top + 1;
            var delta = dist - q.MaxDist;
            if (delta <= 0.0)
            {
                if (q.IndexSet == null || !q.IndexSet.Contains(index))
                {
                    if (q.DynamicSize)
                        q.List.Add(new IndexDist<double>(index, dist));
                    else
                    {
                        q.List.HeapDescendingEnqueue(
                                    new IndexDist<double>(index, dist));
                        if (q.List.Count > q.MaxCount)
                        {
                            q.List.HeapDescendingDequeue();
                            var md = q.List[0].Dist;
                            q.MaxDist = md; q.MaxDistEps = md + m_eps;
                        }
                    }
                }
                if (t1 >= m_size) return;
            }
            else
            {
                if (t1 >= m_size) return;
                if (delta > m_radius[top]) return;
            }
            var d = m_axis[top];
            var s = m_vget(topPoint, d);
            var invDelta = q.InvDelta[d];
            double x;
            if (invDelta > 0)
            {
                if ((x = m_vget(q.MaxP, d)) < s)
                {
                    GetClosestToLine(q, t1);
                    long t2 = t1 + 1; if (t2 >= m_size) return;
                    if (q.MaxDistEps * q.Scale[d] < m_dimDist(d, x, s)) return;
                    if (q.MaxDistEps < m_dimDist(d, m_vget(q.P1, d), s)) return;
                    GetClosestToLine(q, t2);
                }
                else if ((x = m_vget(q.MinP, d)) > s)
                {
                    long t2 = t1 + 1;
                    if (t2 < m_size) GetClosestToLine(q, t2);
                    if (q.MaxDistEps * q.Scale[d] < m_dimDist(d, s, x)) return;
                    if (q.MaxDistEps < m_dimDist(d, s, m_vget(q.P0, d))) return;
                    GetClosestToLine(q, t1);
                }
                else // split line
                {
                    var t = (s - m_vget(q.P0, d)) * invDelta;
                    var sp = m_lerp(t, q.P0, q.P1);
                    var tmp = q.MaxP; q.MaxP = sp;
                    GetClosestToLine(q, t1);
                    q.MaxP = tmp;
                    long t2 = t1 + 1; if (t2 >= m_size) return;
                    tmp = q.MinP; q.MinP = sp;
                    GetClosestToLine(q, t2);
                    q.MinP = tmp;
                }
            }
            else if (invDelta < 0)
            {
                if ((x = m_vget(q.MinP, d)) < s)
                {
                    GetClosestToLine(q, t1);
                    long t2 = t1 + 1; if (t2 >= m_size) return;
                    if (q.MaxDistEps * q.Scale[d] < m_dimDist(d, x, s)) return;
                    if (q.MaxDistEps < m_dimDist(d, m_vget(q.P0, d), s)) return;
                    GetClosestToLine(q, t2);
                }
                else if ((x = m_vget(q.MaxP, d)) > s)
                {
                    long t2 = t1 + 1;
                    if (t2 < m_size) GetClosestToLine(q, t2);
                    if (q.MaxDistEps * q.Scale[d] < m_dimDist(d, s, x)) return;
                    if (q.MaxDistEps < m_dimDist(d, s, m_vget(q.P1, d))) return;
                    GetClosestToLine(q, t1);
                }
                else // split line
                {
                    var t = (s - m_vget(q.P0, d)) * invDelta;
                    var sp = m_lerp(t, q.P0, q.P1);
                    var tmp = q.MinP; q.MinP = sp;
                    GetClosestToLine(q, t1);
                    q.MinP = tmp;
                    long t2 = t1 + 1; if (t2 >= m_size) return;
                    tmp = q.MaxP; q.MaxP = sp;
                    GetClosestToLine(q, t2);
                    q.MaxP = tmp;
                }
            }
            else
            {
                if ((x = m_vget(q.MinP, d)) < s)
                {
                    GetClosestToLine(q, t1);
                    if (q.MaxDistEps * q.Scale[d] < m_dimDist(d, x, s)) return;
                    if (q.MaxDistEps < m_dimDist(d, m_vget(q.P0, d), s)) return;
                    long t2 = t1 + 1; if (t2 >= m_size) return;
                    GetClosestToLine(q, t2);
                }
                else
                {
                    long t2 = t1 + 1;
                    if (t2 < m_size) GetClosestToLine(q, t2);
                    if (q.MaxDistEps * q.Scale[d] < m_dimDist(d, s, x)) return;
                    if (q.MaxDistEps < m_dimDist(d, s, m_vget(q.P0, d))) return;
                    GetClosestToLine(q, t1);
                }
            }
        }

        public TPoint GetPoint(IndexDist<double> indexDist)
        {
            return m_aget(m_array, indexDist.Index);
        }
    }

    public static class IEnumerableIndexDistExtensions
    {
        #region Enumeration of Indices and Points

        public static IEnumerable<IndexDist<T>> Sorted<T>(
                this List<IndexDist<T>> list)
            where T : IComparable<T>
        {
            return list.HeapDescendingDequeueAll();
        }

        public static IEnumerable<int> Indices<T>(this List<IndexDist<T>> indexDistList)
            where T : IComparable<T>
        {
            foreach (var id in indexDistList) yield return (int)id.Index;
        }

        public static IEnumerable<long> LongIndices<T>(this List<IndexDist<T>> indexDistList)
            where T : IComparable<T>
        {
            foreach (var id in indexDistList) yield return id.Index;
        }

        public static IEnumerable<Vec<T>> Points<T>(
                this List<IndexDist<T>> indexDistList, VecArray<T> va)
            where T : IComparable<T>
        {
            foreach (var id in indexDistList) yield return va[id.Index];
        }

        public static IEnumerable<int> Indices<T>(this IEnumerable<IndexDist<T>> indexDists)
            where T : IComparable<T>
        {
            return indexDists.Select(v => (int)v.Index);
        }

        public static IEnumerable<long> IndicesLong<T>(this IEnumerable<IndexDist<T>> indexDists)
            where T : IComparable<T>
        {
            return indexDists.Select(v => v.Index);
        }

        public static IEnumerable<Vec<T>> Points<T>(
                this IEnumerable<IndexDist<T>> indexDists, VecArray<T> va)
            where T : IComparable<T>
        {
            return indexDists.Select(v => va[v.Index]);
        }

        public static IEnumerable<V3d> Points(
                this IEnumerable<IndexDist<double>> indexDists, V3d[] va)
        {
            return indexDists.Select(v => va[v.Index]);
        }

        public static int[] IndexArray(
                this List<IndexDist<double>> indexDistList)
        {
            int count = indexDistList.Count;
            var array = new int[count];
            for (int i = 0; i < count; i++) array[i] = (int)indexDistList[i].Index;
            return array;
        }

        public static long[] IndexArrayLong(
                this List<IndexDist<double>> indexDistList)
        {
            int count = indexDistList.Count;
            var array = new long[count];
            for (int i = 0; i < count; i++) array[i] = indexDistList[i].Index;
            return array;
        }

        public static VecArray<T> PointArray<T>(
                this List<IndexDist<T>> indexDistList, VecArray<T> va)
            where T : IComparable<T>
        {
            var dim = va.Dim;
            var count = indexDistList.Count;
            var array = new VecArray<T>(dim, count);
            array.SetByIndex(i => va[indexDistList[(int)i].Index]);
            return array;
        }

        public static V3d[] PointArray(
                this List<IndexDist<double>> indexDistList, V3d[] va)
        {
            int count = indexDistList.Count;
            var array = new V3d[count];
            for (int i = 0; i < count; i++) array[i] = va[indexDistList[i].Index];
            return array;
        }

        #endregion
    }
}
