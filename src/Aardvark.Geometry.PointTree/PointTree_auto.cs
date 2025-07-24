/*
   Aardvark Platform
   Copyright (C) 2006-2025  Aardvark Platform Team
   https://aardvark.graphics

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/
using Aardvark.Base;
using Aardvark.Base.Coder;
using Aardvark.Base.Sorting;
using System;
using System.Collections.Generic;

#pragma warning disable IDE0290 // Use primary constructor

namespace Aardvark.Geometry
{
    // AUTO GENERATED CODE - DO NOT CHANGE!

    #region PointRkdTreeF

    [RegisterTypeInfo]
    public class PointRkdTreeFData : IFieldCodeable
    {
        public long[] PermArray;
        public int[] AxisArray;
        public float[] RadiusArray;

        public PointRkdTreeFData()
        { }

        public IEnumerable<FieldCoder> GetFieldCoders(int coderVersion)
        {
            return
            [
                new FieldCoder(0, "PermArray",
                        (c,o) => c.CodeLongArray(ref ((PointRkdTreeFData)o).PermArray) ),
                new FieldCoder(1, "AxisArray",
                        (c,o) => c.CodeIntArray(ref ((PointRkdTreeFData)o).AxisArray) ),
                new FieldCoder(2, "RadiusArray",
                        (c,o) => c.CodeFloatArray(ref ((PointRkdTreeFData)o).RadiusArray) ),
            ];
        }
    }

    /// <summary>
    /// A k-d tree of k-dimensional points on top of three generic type
    /// parameters: TArray an arbitrary array type for which the element
    /// getter needs to be specified, TPoint, an arbitrary point type
    /// for which the accessor to the components must be specified, and
    /// T the compoent type of the point.
    /// The k-d tree does not reorder the elements of the array, it just
    /// generates an internal index array that stores the references to
    /// the array elements in heap order.
    /// </summary>
    public partial class PointRkdTreeF<TArray, TPoint>
    {
        readonly long m_dim;
        readonly long m_size;
        readonly TArray m_array;
        readonly Func<TArray, long, TPoint> m_aget;
        readonly Func<TPoint, long, float> m_vget;
        readonly Func<TPoint, TPoint, float> m_dist;
        readonly Func<long, float, float, float> m_dimDist;
        readonly Func<TPoint, TPoint, TPoint, float> m_lineDist;
        readonly Func<float, TPoint, TPoint, TPoint> m_lerp;
        readonly float m_eps;

        readonly long[] m_perm;
        readonly int[] m_axis; // 2^31 dimensions are way enough
        readonly float[] m_radius;

        #region Constructor

        public PointRkdTreeF(
                long dim, long size, TArray array,
                Func<TArray, long, TPoint> arrayGetter,
                Func<TPoint, long, float> vectorGetter,
                Func<TPoint, TPoint, float> distanceFun,
                Func<long, float, float, float> dimDistanceFun,
                Func<TPoint, TPoint, TPoint, float> lineDistFun,
                Func<float, TPoint, TPoint, TPoint> lerpFun,
                float absoluteEps)
            : this(dim, size, array, arrayGetter, vectorGetter, distanceFun,
                   dimDistanceFun, lineDistFun, lerpFun,absoluteEps,
                   new PointRkdTreeFData
                   {
                        PermArray = new long[size],
                        AxisArray = new int[size/2], // heap internal nodes only => size/2
                        RadiusArray = new float[size / 2],
                   })
        {
            var perm = new long[size].SetByIndexLong(i => i);

            long p2 = Fun.PrevPowerOfTwo(size);
            long row = size + 1 - p2; // length of last row of heap
            long left = p2 / 2; // full width of left subtree in last row

            Balance(perm, 0, left, row, 0, size);
        }

        public PointRkdTreeF(
                long dim, long size, TArray array,
                Func<TArray, long, TPoint> arrayGetter,
                Func<TPoint, long, float> vectorGetter,
                Func<TPoint, TPoint, float> distanceFun,
                Func<long, float, float, float> dimDistanceFun,
                Func<TPoint, TPoint, TPoint, float> lineDistFun,
                Func<float, TPoint, TPoint, TPoint> lerpFun,
                float absoluteEps,
                PointRkdTreeFData data
                )
        {
            m_dim = dim;
            m_size = size;
            m_array = array;
            m_aget = arrayGetter;
            m_vget = vectorGetter;
            m_dist = distanceFun;
            m_dimDist = dimDistanceFun;
            m_lineDist = lineDistFun;
            m_lerp = lerpFun;
            m_eps = absoluteEps;

            m_perm = data?.PermArray;
            m_axis = data?.AxisArray;
            m_radius = data?.RadiusArray;
        }

        private float GetMaxDist(
                TPoint p, long[] perm, long start, long end)
        {
            var max = float.MinValue;
            for (long i = start; i < end; i++)
            {
                var d = m_dist(p, m_aget(m_array, perm[i]));
                if (d > max) max = d;
            }
            return max;
        }

        private long GetMaxDim(
                long[] perm, long start, long end)
        {
            var min = new float[m_dim].Set(float.MaxValue);
            var max = new float[m_dim].Set(float.MinValue);
            for (long i = start; i < end; i++) // calculate bounding box
            {
                var v = m_aget(m_array, perm[i]);
                for (long vi = 0; vi < m_dim; vi++)
                {
                    var x = m_vget(v, vi);
                    if (x < min[vi]) min[vi] = x;
                    if (x > max[vi]) max[vi] = x;
                }
            }
            long dim = 0;
            float size = max[0] - min[0];
            for (long d = 1; d < m_dim; d++) // find max dim of box
            {
                var dsize = max[d] - min[d];
                if (dsize > size) { size = dsize; dim = d; }
            }
            return dim;
        }

        private void Balance(
                long[] perm, long top, long left, long row,
                long start, long end)
        {
            if (row <= 0) { left /= 2; row = long.MaxValue; }
            if (left == 0) { m_perm[top] = perm[start]; return; }
            long mid = start - 1 + left + Fun.Min(left, row);
            long dim = GetMaxDim(perm, start, end);
            m_axis[top] = (int)dim;
            perm.PermutationQuickMedian(m_array, m_aget,
                    (v0, v1) => m_vget(v0, dim).CompareTo(m_vget(v1, dim)),
                    start, end, mid);
            m_perm[top] = perm[mid];
            m_radius[top] = GetMaxDist(m_aget(m_array, perm[mid]), perm, start, end) + m_eps;
            if (start < mid)
                Balance(perm, 2 * top + 1, left / 2, row, start, mid);
            ++mid;
            if (mid < end)
                Balance(perm, 2 * top + 2, left / 2, row - left, mid, end);
        }

        #endregion

        #region Closest Classes

        public class ClosestQuery
        {
            public float MaxDist;
            public float MaxDistEps;
            public int MaxCount;
            public List<IndexDist<float>> List;
            public readonly bool DynamicSize;
            public float OriginalMaxDist;
            public float OriginalMaxDistEps;

            public ClosestQuery()
            { }

            public ClosestQuery(float maxDistance,
                           float maxDistancePlusEps, int maxCount,
                           List<IndexDist<float>> list)
            {
                MaxDist = maxDistance;
                MaxDistEps = maxDistancePlusEps;
                MaxCount = maxCount;
                List = list;
                DynamicSize = maxCount == 0;
                OriginalMaxDist = maxDistance;
                OriginalMaxDistEps = maxDistancePlusEps;
            }

            public virtual void Clear()
            {
                MaxDist = OriginalMaxDist;
                MaxDistEps = OriginalMaxDistEps;
                List.Clear();
            }
        }

        /// <summary>
        /// A query object to handle multiple (possibly accumulated) closest
        /// to point queries.
        /// </summary>
        public class ClosestToPointQuery : ClosestQuery
        {
            public TPoint Point;
            public Func<TPoint, TPoint, float> Dist;
            public Func<long, float, float, float> DimDist;
            public Func<long, bool> Filter;

            public ClosestToPointQuery(
                    float maxDistance,
                    float maxDistancePlusEps, int maxCount,
                    Func<TPoint, TPoint, float> dist,
                    Func<long, float, float, float> dimDist,
                    List<IndexDist<float>> list)
                : base(maxDistance, maxDistancePlusEps, maxCount, list)
            {
                Dist = dist;
                DimDist = dimDist;
            }

            public ClosestToPointQuery(
                    TPoint query, float maxDistance,
                    float maxDistancePlusEps, int maxCount,
                    Func<TPoint, TPoint, float> dist,
                    Func<long, float, float, float> dimDist,
                    List<IndexDist<float>> list)
                : base(maxDistance, maxDistancePlusEps, maxCount, list)
            {
                Point = query;
                Dist = dist;
                DimDist = dimDist;
            }
        }

        #endregion

        #region Properties

        public PointRkdTreeFData Data
        {
            get
            {
                return new PointRkdTreeFData
                {
                    PermArray = m_perm,
                    AxisArray = m_axis,
                    RadiusArray = m_radius
                };
            }
        }

        #endregion

        #region Operations

        private static List<IndexDist<float>> StaticCreateList(int maxCount)
        {
            if (maxCount > 0)
                return new List<IndexDist<float>>(maxCount + 1);
            else
                return [];
        }

        /// <summary>
        /// Create a closest to point query object for multiple point
        /// queries with the same maximum distance and maximum count
        /// values.
        /// </summary>
        public ClosestToPointQuery CreateClosestToPointQuery(
                float maxDistance, int maxCount)
        {
            var maxDistPlusEps = maxDistance < float.MaxValue
                    ? maxDistance + m_eps : maxDistance;
            var q = new ClosestToPointQuery(
                                maxDistance, maxDistPlusEps, maxCount,
                                m_dist, m_dimDist,
                                StaticCreateList(maxCount));
            return q;
        }

        /// <summary>
        /// Add the query result with the supplied point to the closest to
        /// point query object. The accumulated result is returned, or can
        /// be retrieved as the List property of the query object.
        /// </summary>
        public List<IndexDist<float>> GetClosest(
                ClosestToPointQuery query, TPoint point)
        {
            query.Point = point;
            if (query.Filter == null)
                GetClosest(query, 0);
            else
                GetClosestFilter(query, 0);
            return query.List;
        }

        /// <summary>
        /// Query for at most maxCount points within maxDistance around
        /// the supplied query point. Note that at least one of the two
        /// criteria must be set, otherwise all points in the k-d-tree
        /// will be retrieved. The resulting points are returned in a
        /// list of index/distance structs. If a fixed maximum count is
        /// supplied, the list will be in heap order with the point with
        /// the largest distance from the query point on top.
        /// </summary>
        public List<IndexDist<float>> GetClosest(
                TPoint query, float maxDistance, int maxCount)
        {
            var list = StaticCreateList(maxCount);
            var mdplus = maxDistance < float.MaxValue
                                ? maxDistance + m_eps
                                : maxDistance;
            var q = new ClosestToPointQuery(query, maxDistance, mdplus,
                                    maxCount, m_dist, m_dimDist, list);
            GetClosest(q, 0);
            return q.List;
        }

        /// <summary>
        /// Get the single closest point from the set.
        /// </summary>
        public IndexDist<float> GetClosest(TPoint query)
        {
            return GetClosest(query, float.MaxValue, 1)[0];
        }

        private void GetAllList(ClosestToPointQuery q, long top)
        {
            q.List.Add(new IndexDist<float>(m_perm[top], float.MinValue));
            long t1 = 2 * top + 1; if (t1 >= m_size) return;
            GetAllList(q, t1);
            long t2 = t1 + 1; if (t2 >= m_size) return;
            GetAllList(q, t2);
        }

        private void GetClosest(ClosestToPointQuery q, long top)
        {
            long index = m_perm[top];
            var splitPoint = m_aget(m_array, index);
            var dist = q.Dist(q.Point, splitPoint);
            long t1 = 2 * top + 1;
            var delta = dist - q.MaxDist;
            if (delta <= 0.0)
            {
                if (q.DynamicSize)
                {
                    q.List.Add(new IndexDist<float>(index, dist));
                    if (t1 >= m_size) return;
                    if (delta < -m_radius[top])
                    {
                        GetAllList(q, t1);
                        long t2 = t1 + 1; if (t2 >= m_size) return;
                        GetAllList(q, t2);
                        return;
                    }
                }
                else
                {
                    q.List.HeapDescendingEnqueue(new IndexDist<float>(index, dist));
                    if (q.List.Count > q.MaxCount)
                    {
                        q.List.HeapDescendingDequeue();
                        var md = q.List[0].Dist;
                        q.MaxDist = md; q.MaxDistEps = md + m_eps;
                    }
                    if (t1 >= m_size) return;
                }
            }
            else
            {
                if (t1 >= m_size) return;
                if (delta > m_radius[top]) return;
            }
            var dim = m_axis[top];
            var x = m_vget(q.Point, dim); var s = m_vget(splitPoint, dim);
            if (x < s)
            {
                GetClosest(q, t1);
                if (q.MaxDistEps < q.DimDist(dim, x, s)) return;
                long t2 = t1 + 1; if (t2 >= m_size) return;
                GetClosest(q, t2);
            }
            else
            {
                long t2 = t1 + 1;
                if (t2 < m_size) GetClosest(q, t2);
                if (q.MaxDistEps < q.DimDist(dim, s, x)) return;
                GetClosest(q, t1);
            }
        }

        //private void GetAllListFilter(ClosestToPointQuery q, long top)
        //{
        //    var index = m_perm[top];
        //    if (q.Filter(index))
        //        q.List.Add(new IndexDist<float>(m_perm[top], float.MinValue));
        //    long t1 = 2 * top + 1; if (t1 >= m_size) return;
        //    GetAllList(q, t1);
        //    long t2 = t1 + 1; if (t2 >= m_size) return;
        //    GetAllList(q, t2);
        //}

        private void GetClosestFilter(ClosestToPointQuery q, long top)
        {
            long index = m_perm[top];
            var splitPoint = m_aget(m_array, index);
            var dist = q.Dist(q.Point, splitPoint);
            long t1 = 2 * top + 1;
            var delta = dist - q.MaxDist;
            if (delta <= 0.0)
            {
                if (q.DynamicSize)
                {
                    if (q.Filter(index))
                        q.List.Add(new IndexDist<float>(index, dist));
                    if (t1 >= m_size) return;
                    if (delta < -m_radius[top])
                    {
                        GetAllList(q, t1);
                        long t2 = t1 + 1; if (t2 >= m_size) return;
                        GetAllList(q, t2);
                        return;
                    }
                }
                else
                {
                    if (q.Filter(index))
                        q.List.HeapDescendingEnqueue(new IndexDist<float>(index, dist));
                    if (q.List.Count > q.MaxCount)
                    {
                        q.List.HeapDescendingDequeue();
                        var md = q.List[0].Dist;
                        q.MaxDist = md; q.MaxDistEps = md + m_eps;
                    }
                    if (t1 >= m_size) return;
                }
            }
            else
            {
                if (t1 >= m_size) return;
                if (delta > m_radius[top]) return;
            }
            var dim = m_axis[top];
            var x = m_vget(q.Point, dim); var s = m_vget(splitPoint, dim);
            if (x < s)
            {
                GetClosestFilter(q, t1);
                if (q.MaxDistEps < q.DimDist(dim, x, s)) return;
                long t2 = t1 + 1; if (t2 >= m_size) return;
                GetClosestFilter(q, t2);
            }
            else
            {
                long t2 = t1 + 1;
                if (t2 < m_size) GetClosestFilter(q, t2);
                if (q.MaxDistEps < q.DimDist(dim, s, x)) return;
                GetClosestFilter(q, t1);
            }
        }

        #endregion

    }

    #endregion

    #region PointRkdTreeFSelector

    [RegisterTypeInfo]
    public class PointRkdTreeFSelectorData : IFieldCodeable
    {
        public long[] PermArray;
        public int[] AxisArray;
        public float[] RadiusArray;

        public PointRkdTreeFSelectorData()
        { }

        public IEnumerable<FieldCoder> GetFieldCoders(int coderVersion)
        {
            return
            [
                new FieldCoder(0, "PermArray",
                        (c,o) => c.CodeLongArray(ref ((PointRkdTreeFSelectorData)o).PermArray) ),
                new FieldCoder(1, "AxisArray",
                        (c,o) => c.CodeIntArray(ref ((PointRkdTreeFSelectorData)o).AxisArray) ),
                new FieldCoder(2, "RadiusArray",
                        (c,o) => c.CodeFloatArray(ref ((PointRkdTreeFSelectorData)o).RadiusArray) ),
            ];
        }
    }

    /// <summary>
    /// A k-d tree of k-dimensional points on top of three generic type
    /// parameters: TArray an arbitrary array type for which the element
    /// getter needs to be specified, TPoint, an arbitrary point type
    /// for which the accessor to the components must be specified, and
    /// T the compoent type of the point.
    /// The k-d tree does not reorder the elements of the array, it just
    /// generates an internal index array that stores the references to
    /// the array elements in heap order.
    /// </summary>
    public partial class PointRkdTreeFSelector<TArray, TPoint>
    {
        readonly long m_dim;
        readonly long m_size;
        readonly TArray m_array;
        readonly Func<TArray, long, TPoint> m_aget;
        readonly Func<TPoint, float>[] m_sela;
        readonly Func<TPoint, TPoint, float> m_dist;
        readonly Func<float, float, float>[] m_dimDistA;
        readonly Func<TPoint, TPoint, TPoint, float> m_lineDist;
        readonly Func<float, TPoint, TPoint, TPoint> m_lerp;
        readonly float m_eps;

        readonly long[] m_perm;
        readonly int[] m_axis; // 2^31 dimensions are way enough
        readonly float[] m_radius;

        #region Constructor

        public PointRkdTreeFSelector(
                long dim, long size, TArray array,
                Func<TArray, long, TPoint> arrayGetter,
                Func<TPoint, float>[] selectorArray,
                Func<TPoint, TPoint, float> distanceFun,
                Func<float, float, float>[] dimDistanceFunArray,
                Func<TPoint, TPoint, TPoint, float> lineDistFun,
                Func<float, TPoint, TPoint, TPoint> lerpFun,
                float absoluteEps)
            : this(dim, size, array, arrayGetter, selectorArray, distanceFun,
                   dimDistanceFunArray, lineDistFun, lerpFun,absoluteEps,
                   new PointRkdTreeFSelectorData
                   {
                        PermArray = new long[size],
                        AxisArray = new int[size/2], // heap internal nodes only => size/2
                        RadiusArray = new float[size / 2],
                   })
        {
            var perm = new long[size].SetByIndexLong(i => i);

            long p2 = Fun.PrevPowerOfTwo(size);
            long row = size + 1 - p2; // length of last row of heap
            long left = p2 / 2; // full width of left subtree in last row

            Balance(perm, 0, left, row, 0, size);
        }

        public PointRkdTreeFSelector(
                long dim, long size, TArray array,
                Func<TArray, long, TPoint> arrayGetter,
                Func<TPoint, float>[] selectorArray,
                Func<TPoint, TPoint, float> distanceFun,
                Func<float, float, float>[] dimDistanceFunArray,
                Func<TPoint, TPoint, TPoint, float> lineDistFun,
                Func<float, TPoint, TPoint, TPoint> lerpFun,
                float absoluteEps,
                PointRkdTreeFSelectorData data
                )
        {
            m_dim = dim;
            m_size = size;
            m_array = array;
            m_aget = arrayGetter;
            m_sela = selectorArray;
            m_dist = distanceFun;
            m_dimDistA = dimDistanceFunArray;
            m_lineDist = lineDistFun;
            m_lerp = lerpFun;
            m_eps = absoluteEps;

            m_perm = data?.PermArray;
            m_axis = data?.AxisArray;
            m_radius = data?.RadiusArray;
        }

        private float GetMaxDist(
                TPoint p, long[] perm, long start, long end)
        {
            var max = float.MinValue;
            for (long i = start; i < end; i++)
            {
                var d = m_dist(p, m_aget(m_array, perm[i]));
                if (d > max) max = d;
            }
            return max;
        }

        private long GetMaxDim(
                long[] perm, long start, long end)
        {
            var min = new float[m_dim].Set(float.MaxValue);
            var max = new float[m_dim].Set(float.MinValue);
            for (long i = start; i < end; i++) // calculate bounding box
            {
                var v = m_aget(m_array, perm[i]);
                for (long vi = 0; vi < m_dim; vi++)
                {
                    var x = m_sela[vi](v);
                    if (x < min[vi]) min[vi] = x;
                    if (x > max[vi]) max[vi] = x;
                }
            }
            long dim = 0;
            float size = max[0] - min[0];
            for (long d = 1; d < m_dim; d++) // find max dim of box
            {
                var dsize = max[d] - min[d];
                if (dsize > size) { size = dsize; dim = d; }
            }
            return dim;
        }

        private void Balance(
                long[] perm, long top, long left, long row,
                long start, long end)
        {
            if (row <= 0) { left /= 2; row = long.MaxValue; }
            if (left == 0) { m_perm[top] = perm[start]; return; }
            long mid = start - 1 + left + Fun.Min(left, row);
            long dim = GetMaxDim(perm, start, end);
            m_axis[top] = (int)dim;
            perm.PermutationQuickMedianAscending(m_array, m_aget,
                    m_sela[dim],
                    start, end, mid);
            m_perm[top] = perm[mid];
            m_radius[top] = GetMaxDist(m_aget(m_array, perm[mid]), perm, start, end) + m_eps;
            if (start < mid)
                Balance(perm, 2 * top + 1, left / 2, row, start, mid);
            ++mid;
            if (mid < end)
                Balance(perm, 2 * top + 2, left / 2, row - left, mid, end);
        }

        #endregion

        #region Closest Classes

        public class ClosestQuery
        {
            public float MaxDist;
            public float MaxDistEps;
            public int MaxCount;
            public List<IndexDist<float>> List;
            public readonly bool DynamicSize;
            public float OriginalMaxDist;
            public float OriginalMaxDistEps;

            public ClosestQuery()
            { }

            public ClosestQuery(float maxDistance,
                           float maxDistancePlusEps, int maxCount,
                           List<IndexDist<float>> list)
            {
                MaxDist = maxDistance;
                MaxDistEps = maxDistancePlusEps;
                MaxCount = maxCount;
                List = list;
                DynamicSize = maxCount == 0;
                OriginalMaxDist = maxDistance;
                OriginalMaxDistEps = maxDistancePlusEps;
            }

            public virtual void Clear()
            {
                MaxDist = OriginalMaxDist;
                MaxDistEps = OriginalMaxDistEps;
                List.Clear();
            }
        }

        /// <summary>
        /// A query object to handle multiple (possibly accumulated) closest
        /// to point queries.
        /// </summary>
        public class ClosestToPointQuery : ClosestQuery
        {
            public TPoint Point;
            public Func<TPoint, TPoint, float> Dist;
            public Func<float, float, float>[] DimDistArray;
            public Func<long, bool> Filter;

            public ClosestToPointQuery(
                    float maxDistance,
                    float maxDistancePlusEps, int maxCount,
                    Func<TPoint, TPoint, float> dist,
                    Func<float, float, float>[] dimDistA,
                    List<IndexDist<float>> list)
                : base(maxDistance, maxDistancePlusEps, maxCount, list)
            {
                Dist = dist;
                DimDistArray = dimDistA;
            }

            public ClosestToPointQuery(
                    TPoint query, float maxDistance,
                    float maxDistancePlusEps, int maxCount,
                    Func<TPoint, TPoint, float> dist,
                    Func<float, float, float>[] dimDistA,
                    List<IndexDist<float>> list)
                : base(maxDistance, maxDistancePlusEps, maxCount, list)
            {
                Point = query;
                Dist = dist;
                DimDistArray = dimDistA;
            }
        }

        #endregion

        #region Properties

        public PointRkdTreeFSelectorData Data
        {
            get
            {
                return new PointRkdTreeFSelectorData
                {
                    PermArray = m_perm,
                    AxisArray = m_axis,
                    RadiusArray = m_radius
                };
            }
        }

        #endregion

        #region Operations

        private static List<IndexDist<float>> StaticCreateList(int maxCount)
        {
            if (maxCount > 0)
                return new List<IndexDist<float>>(maxCount + 1);
            else
                return [];
        }

        /// <summary>
        /// Create a closest to point query object for multiple point
        /// queries with the same maximum distance and maximum count
        /// values.
        /// </summary>
        public ClosestToPointQuery CreateClosestToPointQuery(
                float maxDistance, int maxCount)
        {
            var maxDistPlusEps = maxDistance < float.MaxValue
                    ? maxDistance + m_eps : maxDistance;
            var q = new ClosestToPointQuery(
                                maxDistance, maxDistPlusEps, maxCount,
                                m_dist, m_dimDistA,
                                StaticCreateList(maxCount));
            return q;
        }

        /// <summary>
        /// Add the query result with the supplied point to the closest to
        /// point query object. The accumulated result is returned, or can
        /// be retrieved as the List property of the query object.
        /// </summary>
        public List<IndexDist<float>> GetClosest(
                ClosestToPointQuery query, TPoint point)
        {
            query.Point = point;
            if (query.Filter == null)
                GetClosest(query, 0);
            else
                GetClosestFilter(query, 0);
            return query.List;
        }

        /// <summary>
        /// Query for at most maxCount points within maxDistance around
        /// the supplied query point. Note that at least one of the two
        /// criteria must be set, otherwise all points in the k-d-tree
        /// will be retrieved. The resulting points are returned in a
        /// list of index/distance structs. If a fixed maximum count is
        /// supplied, the list will be in heap order with the point with
        /// the largest distance from the query point on top.
        /// </summary>
        public List<IndexDist<float>> GetClosest(
                TPoint query, float maxDistance, int maxCount)
        {
            var list = StaticCreateList(maxCount);
            var mdplus = maxDistance < float.MaxValue
                                ? maxDistance + m_eps
                                : maxDistance;
            var q = new ClosestToPointQuery(query, maxDistance, mdplus,
                                    maxCount, m_dist, m_dimDistA, list);
            GetClosest(q, 0);
            return q.List;
        }

        /// <summary>
        /// Get the single closest point from the set.
        /// </summary>
        public IndexDist<float> GetClosest(TPoint query)
        {
            return GetClosest(query, float.MaxValue, 1)[0];
        }

        private void GetAllList(ClosestToPointQuery q, long top)
        {
            q.List.Add(new IndexDist<float>(m_perm[top], float.MinValue));
            long t1 = 2 * top + 1; if (t1 >= m_size) return;
            GetAllList(q, t1);
            long t2 = t1 + 1; if (t2 >= m_size) return;
            GetAllList(q, t2);
        }

        private void GetClosest(ClosestToPointQuery q, long top)
        {
            long index = m_perm[top];
            var splitPoint = m_aget(m_array, index);
            var dist = q.Dist(q.Point, splitPoint);
            long t1 = 2 * top + 1;
            var delta = dist - q.MaxDist;
            if (delta <= 0.0)
            {
                if (q.DynamicSize)
                {
                    q.List.Add(new IndexDist<float>(index, dist));
                    if (t1 >= m_size) return;
                    if (delta < -m_radius[top])
                    {
                        GetAllList(q, t1);
                        long t2 = t1 + 1; if (t2 >= m_size) return;
                        GetAllList(q, t2);
                        return;
                    }
                }
                else
                {
                    q.List.HeapDescendingEnqueue(new IndexDist<float>(index, dist));
                    if (q.List.Count > q.MaxCount)
                    {
                        q.List.HeapDescendingDequeue();
                        var md = q.List[0].Dist;
                        q.MaxDist = md; q.MaxDistEps = md + m_eps;
                    }
                    if (t1 >= m_size) return;
                }
            }
            else
            {
                if (t1 >= m_size) return;
                if (delta > m_radius[top]) return;
            }
            var dim = m_axis[top];
            var sel = m_sela[dim]; var x = sel(q.Point); var s = sel(splitPoint);
            if (x < s)
            {
                GetClosest(q, t1);
                if (q.MaxDistEps < q.DimDistArray[dim](x, s)) return;
                long t2 = t1 + 1; if (t2 >= m_size) return;
                GetClosest(q, t2);
            }
            else
            {
                long t2 = t1 + 1;
                if (t2 < m_size) GetClosest(q, t2);
                if (q.MaxDistEps < q.DimDistArray[dim](s, x)) return;
                GetClosest(q, t1);
            }
        }

        //private void GetAllListFilter(ClosestToPointQuery q, long top)
        //{
        //    var index = m_perm[top];
        //    if (q.Filter(index))
        //        q.List.Add(new IndexDist<float>(m_perm[top], float.MinValue));
        //    long t1 = 2 * top + 1; if (t1 >= m_size) return;
        //    GetAllList(q, t1);
        //    long t2 = t1 + 1; if (t2 >= m_size) return;
        //    GetAllList(q, t2);
        //}

        private void GetClosestFilter(ClosestToPointQuery q, long top)
        {
            long index = m_perm[top];
            var splitPoint = m_aget(m_array, index);
            var dist = q.Dist(q.Point, splitPoint);
            long t1 = 2 * top + 1;
            var delta = dist - q.MaxDist;
            if (delta <= 0.0)
            {
                if (q.DynamicSize)
                {
                    if (q.Filter(index))
                        q.List.Add(new IndexDist<float>(index, dist));
                    if (t1 >= m_size) return;
                    if (delta < -m_radius[top])
                    {
                        GetAllList(q, t1);
                        long t2 = t1 + 1; if (t2 >= m_size) return;
                        GetAllList(q, t2);
                        return;
                    }
                }
                else
                {
                    if (q.Filter(index))
                        q.List.HeapDescendingEnqueue(new IndexDist<float>(index, dist));
                    if (q.List.Count > q.MaxCount)
                    {
                        q.List.HeapDescendingDequeue();
                        var md = q.List[0].Dist;
                        q.MaxDist = md; q.MaxDistEps = md + m_eps;
                    }
                    if (t1 >= m_size) return;
                }
            }
            else
            {
                if (t1 >= m_size) return;
                if (delta > m_radius[top]) return;
            }
            var dim = m_axis[top];
            var sel = m_sela[dim]; var x = sel(q.Point); var s = sel(splitPoint);
            if (x < s)
            {
                GetClosestFilter(q, t1);
                if (q.MaxDistEps < q.DimDistArray[dim](x, s)) return;
                long t2 = t1 + 1; if (t2 >= m_size) return;
                GetClosestFilter(q, t2);
            }
            else
            {
                long t2 = t1 + 1;
                if (t2 < m_size) GetClosestFilter(q, t2);
                if (q.MaxDistEps < q.DimDistArray[dim](s, x)) return;
                GetClosestFilter(q, t1);
            }
        }

        #endregion

    }

    #endregion

    #region PointKdTreeF

    [RegisterTypeInfo]
    public class PointKdTreeFData : IFieldCodeable
    {
        public long[] PermArray;
        public int[] AxisArray;

        public PointKdTreeFData()
        { }

        public IEnumerable<FieldCoder> GetFieldCoders(int coderVersion)
        {
            return
            [
                new FieldCoder(0, "PermArray",
                        (c,o) => c.CodeLongArray(ref ((PointKdTreeFData)o).PermArray) ),
                new FieldCoder(1, "AxisArray",
                        (c,o) => c.CodeIntArray(ref ((PointKdTreeFData)o).AxisArray) ),
            ];
        }
    }

    /// <summary>
    /// A k-d tree of k-dimensional points on top of three generic type
    /// parameters: TArray an arbitrary array type for which the element
    /// getter needs to be specified, TPoint, an arbitrary point type
    /// for which the accessor to the components must be specified, and
    /// T the compoent type of the point.
    /// The k-d tree does not reorder the elements of the array, it just
    /// generates an internal index array that stores the references to
    /// the array elements in heap order.
    /// </summary>
    public partial class PointKdTreeF<TArray, TPoint>
    {
        readonly long m_dim;
        readonly long m_size;
        readonly TArray m_array;
        readonly Func<TArray, long, TPoint> m_aget;
        readonly Func<TPoint, long, float> m_vget;
        readonly Func<TPoint, TPoint, float> m_dist;
        readonly Func<long, float, float, float> m_dimDist;
        readonly float m_eps;

        readonly long[] m_perm;
        readonly int[] m_axis; // 2^31 dimensions are way enough

        #region Constructor

        public PointKdTreeF(
                long dim, long size, TArray array,
                Func<TArray, long, TPoint> arrayGetter,
                Func<TPoint, long, float> vectorGetter,
                Func<TPoint, TPoint, float> distanceFun,
                Func<long, float, float, float> dimDistanceFun,
                float absoluteEps)
            : this(dim, size, array, arrayGetter, vectorGetter, distanceFun,
                    dimDistanceFun, absoluteEps,
                    new PointKdTreeFData
                    {
                        PermArray = new long[size],
                        AxisArray = new int[size / 2] // heap internal nodes only => size/2
                    })
        {
            var min = new float[m_dim].Set(float.MaxValue);
            var max = new float[m_dim].Set(float.MinValue);

            for (long ai = 0; ai < m_size; ai++) // calculate bounding box
            {
                var v = m_aget(m_array, ai);
                for (long vi = 0; vi < m_dim; vi++)
                {
                    var x = m_vget(v, vi);
                    if (x < min[vi]) min[vi] = x;
                    if (x > max[vi]) max[vi] = x;
                }
            }

            var perm = new long[size].SetByIndexLong(i => i);

            long p2 = Fun.PrevPowerOfTwo(size);
            long row = size + 1 - p2; // length of last row of heap
            long left = p2 / 2; // full width of left subtree in last row

            Balance(perm, 0, left, row, 0, size, min, max);
        }
        
        public PointKdTreeF(
                long dim, long size, TArray array,
                Func<TArray, long, TPoint> arrayGetter,
                Func<TPoint, long, float> vectorGetter,
                Func<TPoint, TPoint, float> distanceFun,
                Func<long, float, float, float> dimDistanceFun,
                float absoluteEps,
                PointKdTreeFData data
                )
        {
            m_dim = dim;
            m_size = size;
            m_array = array;
            m_aget = arrayGetter;
            m_vget = vectorGetter;
            m_dist = distanceFun;
            m_dimDist = dimDistanceFun;
            m_eps = absoluteEps;

            m_perm = data?.PermArray;
            m_axis = data?.AxisArray;
        }

        private void Balance(
                long[] perm, long top, long left, long row,
                long start, long end, float[] min, float[] max)
        {
            if (row <= 0) { left /= 2; row = long.MaxValue; }
            if (left == 0) { m_perm[top] = perm[start]; return; }
            long mid = start - 1 + left + Fun.Min(left, row);
            long dim = 0;
            float size = max[0] - min[0];
            for (long d = 1; d < m_dim; d++) // find max dim of box
            {
                var dsize = max[d] - min[d];
                if (dsize > size) { size = dsize; dim = d; }
            }
            m_axis[top] = (int)dim;
            perm.PermutationQuickMedian(m_array, m_aget,
                    (v0, v1) => m_vget(v0, dim).CompareTo(m_vget(v1, dim)),
                    start, end, mid);
            m_perm[top] = perm[mid];
            if (start < mid)
            {
                var tmp = max[dim];
                var lmax = float.MinValue;
                for (long i = start; i < mid; i++)
                {
                    var val = m_vget(m_aget(m_array, perm[i]), dim);
                    if (val > lmax) lmax = val;
                }
                max[dim] = lmax; // modify box to avoid allocation
                Balance(perm, 2 * top + 1, left / 2, row, start, mid, min, max);
                max[dim] = tmp; // restore box
            }
            ++mid;
            if (mid < end)
            {
                var tmp = min[dim];
                var rmin = float.MaxValue;
                for (long i = mid; i < end; i++)
                {
                    var val = m_vget(m_aget(m_array, perm[i]), dim);
                    if (val < rmin) rmin = val;
                }
                min[dim] = rmin;
                Balance(perm, 2 * top + 2, left / 2, row - left, mid, end, min, max);
                min[dim] = tmp;
            }
        }

        #endregion

        #region Class ClosestToPointQuery


        public class ClosestQuery
        {
            public float MaxDist;
            public float MaxDistEps;
            public int MaxCount;
            public List<IndexDist<float>> List;
            public readonly bool DynamicSize;
            public float OriginalMaxDist;
            public float OriginalMaxDistEps;

            public ClosestQuery()
            { }

            public ClosestQuery(float maxDistance,
                           float maxDistancePlusEps, int maxCount,
                           List<IndexDist<float>> list)
            {
                MaxDist = maxDistance;
                MaxDistEps = maxDistancePlusEps;
                MaxCount = maxCount;
                List = list;
                DynamicSize = maxCount == 0;
                OriginalMaxDist = maxDistance;
                OriginalMaxDistEps = maxDistancePlusEps;
            }

            public virtual void Clear()
            {
                MaxDist = OriginalMaxDist;
                MaxDistEps = OriginalMaxDistEps;
                List.Clear();
            }
        }

        /// <summary>
        /// A query object to handle multiple (possibly accumulated) closest
        /// to point queries.
        /// </summary>
        public class ClosestToPointQuery : ClosestQuery
        {
            public TPoint Point;
            public Func<TPoint, TPoint, float> Dist;
            public Func<long, float, float, float> DimDist;
            public Func<long, bool> Filter;

            public ClosestToPointQuery(
                    float maxDistance,
                    float maxDistancePlusEps, int maxCount,
                    Func<TPoint, TPoint, float> dist,
                    Func<long, float, float, float> dimDist,
                    List<IndexDist<float>> list)
                : base(maxDistance, maxDistancePlusEps, maxCount, list)
            {
                Dist = dist;
                DimDist = dimDist;
            }

            public ClosestToPointQuery(
                    TPoint query, float maxDistance,
                    float maxDistancePlusEps, int maxCount,
                    Func<TPoint, TPoint, float> dist,
                    Func<long, float, float, float> dimDist,
                    List<IndexDist<float>> list)
                : base(maxDistance, maxDistancePlusEps, maxCount, list)
            {
                Point = query;
                Dist = dist;
                DimDist = dimDist;
            }
        }

        #endregion

        #region Properties

        public PointKdTreeFData Data
        {
            get
            {
                return new PointKdTreeFData
                {
                    PermArray = m_perm,
                    AxisArray = m_axis
                };
            }
        }

        #endregion

        #region Operations

        private static List<IndexDist<float>> StaticCreateList(int maxCount)
        {
            if (maxCount > 0)
                return new List<IndexDist<float>>(maxCount + 1);
            else
                return [];
        }

        /// <summary>
        /// Create a closest to point query object for multiple point
        /// queries with the same maximum distance and maximum count
        /// values.
        /// </summary>
        public ClosestToPointQuery CreateClosestToPointQuery(
                float maxDistance, int maxCount)
        {
            var maxDistPlusEps = maxDistance < float.MaxValue
                    ? maxDistance + m_eps : maxDistance;
            var q = new ClosestToPointQuery(
                                maxDistance, maxDistPlusEps, maxCount,
                                m_dist, m_dimDist,
                                StaticCreateList(maxCount));
            return q;
        }

        /// <summary>
        /// Add the query result with the supplied point to the closest to
        /// point query object. The accumulated result is returned, or can
        /// be retrieved as the List property of the query object.
        /// </summary>
        public List<IndexDist<float>> GetClosest(
                ClosestToPointQuery query, TPoint point)
        {
            query.Point = point;
            GetClosest(query, 0);
            return query.List;
        }

        /// <summary>
        /// Create a list to use in multiple GetClosest queries.
        /// </summary>
        /// <param name="maxCount"></param>
        /// <returns></returns>
        public List<IndexDist<float>> CreateList(int maxCount)
        {
            return StaticCreateList(maxCount);
        }

        /// <summary>
        /// Query for at most maxCount points within maxDistance around
        /// the supplied query point. Note that at least one of the two
        /// criteria must be set, otherwise all points in the k-d-tree
        /// will be retrieved. The resulting points are inserted into
        /// the supplied list of index/distance structs, which is first
        /// cleared. If a fixed maximum count is supplied, the list will
        /// be in heap order with the point with the largest distance from
        /// the query point on top.
        /// </summary>
        public void GetClosest(
                TPoint query, float maxDistance, int maxCount,
                List<IndexDist<float>> list)
        {
            list.Clear();
            var mdplus = maxDistance < float.MaxValue
                                ? maxDistance + m_eps
                                : maxDistance;
            var q = new ClosestToPointQuery(query, maxDistance, mdplus,
                                    maxCount, m_dist, m_dimDist, list);
            GetClosest(q, 0);
        }

        /// <summary>
        /// Query for at most maxCount points within maxDistance around
        /// the supplied query point. Note that at least one of the two
        /// criteria must be set, otherwise all points in the k-d-tree
        /// will be retrieved. The resulting points are returned in a
        /// list of index/distance structs. If a fixed maximum count is
        /// supplied, the list will be in heap order with the point with
        /// the largest distance from the query point on top.
        /// </summary>
        public List<IndexDist<float>> GetClosest(
                TPoint query, float maxDistance, int maxCount)
        {
            var list = StaticCreateList(maxCount);
            var mdplus = maxDistance < float.MaxValue
                                ? maxDistance + m_eps
                                : maxDistance;
            var q = new ClosestToPointQuery(query, maxDistance, mdplus,
                                    maxCount, m_dist, m_dimDist, list);
            GetClosest(q, 0);
            return q.List;
        }

        /// <summary>
        /// Get the single closest point from the set.
        /// </summary>
        public IndexDist<float> GetClosest(TPoint query)
        {
            return GetClosest(query, float.MaxValue, 1)[0];
        }

        private void GetClosest(ClosestToPointQuery q, long top)
        {
            long index = m_perm[top];
            var splitPoint = m_aget(m_array, index);
            var dist = q.Dist(q.Point, splitPoint);
            if (dist <= q.MaxDist)
            {
                if (q.DynamicSize)
                    q.List.Add(new IndexDist<float>(index, dist));
                else
                {
                    q.List.HeapDescendingEnqueue(new IndexDist<float>(index, dist));
                    if (q.List.Count > q.MaxCount)
                    {
                        q.List.HeapDescendingDequeue();
                        var md = q.List[0].Dist;
                        q.MaxDist = md; q.MaxDistEps = md + m_eps;
                    }
                }
            }
            long t1 = 2 * top + 1; if (t1 >= m_size) return;
            var dim = m_axis[top];
            var x = m_vget(q.Point, dim);
            var s = m_vget(splitPoint, dim);
            if (x < s)
            {
                GetClosest(q, t1);
                if (q.MaxDistEps < q.DimDist(dim, x, s)) return;
                long t2 = t1 + 1; if (t2 >= m_size) return;
                GetClosest(q, t2);
            }
            else
            {
                long t2 = t1 + 1;
                if (t2 < m_size) GetClosest(q, t2);
                if (q.MaxDistEps < q.DimDist(dim, s, x)) return;
                GetClosest(q, t1);
            }
        }

        #endregion
    }

    #endregion

    #region PointVpTreeF

    [RegisterTypeInfo]
    public class PointVpTreeFData : IFieldCodeable
    {
        public long[] PermArray;
        public float[] LMaxArray;
        public float[] RMinArray;

        public PointVpTreeFData()
        { }

        public IEnumerable<FieldCoder> GetFieldCoders(int coderVersion)
        {
            return
            [
                new FieldCoder(0, "PermArray",
                        (c,o) => c.CodeLongArray(ref ((PointVpTreeFData)o).PermArray) ),
                new FieldCoder(1, "LMaxArray",
                        (c,o) => c.CodeFloatArray(ref ((PointVpTreeFData)o).LMaxArray) ),
                new FieldCoder(2, "RMinArray",
                        (c,o) => c.CodeFloatArray(ref ((PointVpTreeFData)o).RMinArray) ),
            ];
        }
    }

    /// <summary>
    /// A vp tree of k-dimensional points on top of three generic type
    /// parameters: TArray an arbitrary array type for which the element
    /// getter needs to be specified, TPoint, and an arbitrary point type
    /// for which the accessor to the components must be specified.
    /// The vp tree does not reorder the elements of the array, it just
    /// generates an internal index array that stores the references to
    /// the array elements in heap order.
    /// </summary>
    public partial class PointVpTreeF<TArray, TPoint>
    {
        readonly long m_dim;
        readonly long m_size;
        readonly TArray m_array;
        readonly Func<TArray, long, TPoint> m_aget;
        readonly Func<TPoint, long, float> m_vget;
        readonly Func<TPoint, TPoint, float> m_dist;
        readonly float m_eps;

        readonly long[] m_perm;
        readonly float[] m_lmax;
        readonly float[] m_rmin;

        #region Constructor

        public PointVpTreeF(
                long dim, long size, TArray array,
                Func<TArray, long, TPoint> arrayGetter,
                Func<TPoint, long, float> vectorGetter,
                Func<TPoint, TPoint, float> distanceFun,
                float absoluteEpsilon)
            : this(dim, size, array, arrayGetter, vectorGetter, distanceFun,
                    absoluteEpsilon,
                    new PointVpTreeFData
                    {
                        PermArray = new long[size],
                        LMaxArray = new float[size / 2], // Only internal nodes of the heap
                        RMinArray = new float[size / 2] // need these bounds => size/2.
                    })
        {
            var perm = new long[size].SetByIndexLong(i => i);

            long p2 = Fun.PrevPowerOfTwo(size);
            long row = size + 1 - p2; // length of last row of heap
            long left = p2 / 2; // length of left side in last row

            Balance(perm, 0, left, row, 0, size);
        }

        public PointVpTreeF(
                long dim, long size, TArray array,
                Func<TArray, long, TPoint> arrayGetter,
                Func<TPoint, long, float> vectorGetter,
                Func<TPoint, TPoint, float> distanceFun,
                float absoluteEpsilon,
                PointVpTreeFData data
                )
        {
            m_dim = dim;
            m_size = size;
            m_array = array;
            m_aget = arrayGetter;
            m_vget = vectorGetter;
            m_dist = distanceFun;
            m_eps = absoluteEpsilon;

            m_perm = data?.PermArray;
            m_lmax = data?.LMaxArray;
            m_rmin = data?.RMinArray;
        }

        private long GetMinMaxIndex(
                long[] perm, long start, long end, out float min, out float max)
        {
            min = float.MaxValue;
            max = float.MinValue;
            var vp = m_aget(m_array, perm[start]);
            long vi = -1;
            for (long i = start + 1; i < end; i++)
            {
                var d = m_dist(vp, m_aget(m_array, perm[i]));
                if (d > max) { max = d; vi = i; }
                if (d < min) min = d;
            }
            return vi;
        }

        private float GetMin(long[] perm, long start, long end, TPoint vp)
        {
            float min = float.MaxValue;
            for (long i = start; i < end; i++)
            {
                var d = m_dist(vp, m_aget(m_array, perm[i]));
                if (d < min) min = d;
            }
            return min;
        }

        private void Balance(
                long[] perm, long top, long left, long row,
                long start, long end)
        {
            if (row <= 0) { left /= 2; row = long.MaxValue; }
            if (left == 0) { m_perm[top] = perm[start]; return; }
            long mid = start - 1 + left + Fun.Min(left, row);
            perm.Swap(mid, start); // vp candidate @start
            long vi = GetMinMaxIndex(perm, start, end, out var vmin, out var vmax);
            perm.Swap(vi, start); // vp candidate 2 @start
            GetMinMaxIndex(perm, start, end, out var vmin2, out var vmax2);
            if (vmax - vmin > vmax2 - vmin2) perm.Swap(vi, start);
            var vp = m_aget(m_array, perm[start]); // vp @ start
            perm.PermutationQuickMedian(m_array, m_aget,
                    (v0, v1) => m_dist(v0, vp).CompareTo(m_dist(v1, vp)),
                    start + 1, end, mid);
            perm.Swap(mid, start); // vp @ mid, median point @ start
            m_perm[top] = perm[mid];
            m_lmax[top] = m_dist(vp, m_aget(m_array, perm[start])) + m_eps;
            m_rmin[top] = GetMin(perm, mid + 1, end, vp) - m_eps;
            if (start < mid)
                Balance(perm, 2 * top + 1, left / 2, row, start, mid);
            ++mid;
            if (mid < end)
                Balance(perm, 2 * top + 2, left / 2, row - left, mid, end);
        }

        #endregion

        #region Class ClosestToPointQuery

        private class ClosestToPointQuery
        {
            public TPoint Point;
            public float MaxDist;
            public int MaxCount;
            public List<IndexDist<float>> List;
            public bool DynamicSize;

            public ClosestToPointQuery(TPoint query, float maxDistance, int maxCount,
                           List<IndexDist<float>> list)
            {
                Point = query;
                MaxDist = maxDistance;
                MaxCount = maxCount;
                DynamicSize = maxCount == 0;
                List = list;
            }
        }

        #endregion

        #region Properties

        public PointVpTreeFData Data
        {
            get
            {
                return new PointVpTreeFData
                {
                    PermArray = m_perm,
                    LMaxArray = m_lmax,
                    RMinArray = m_rmin
                };
            }
        }

        #endregion

        #region Operations

        private static List<IndexDist<float>> StaticCreateList(int maxCount)
        {
            if (maxCount > 0)
                return new List<IndexDist<float>>(maxCount + 1);
            else
                return [];
        }

        /// <summary>
        /// Create a heap to use in multiple GetClosest queries.
        /// </summary>
        /// <param name="maxCount"></param>
        /// <returns></returns>
        public List<IndexDist<float>> CreateList(int maxCount)
        {
            return StaticCreateList(maxCount);
        }

        /// <summary>
        /// Query for at most maxCount points within maxDistance around
        /// the supplied query point. Note that at least one of the two
        /// criteria must be set, otherwise all points in the vp-tree
        /// will be retrieved.The resulting points are inserted into
        /// the supplied list of index/distance structs, which is first
        /// cleared. If a fixed maximum count is supplied, the list will
        /// be in heap order with the point with the largest distance from
        /// the query point on top.
        /// </summary>
        public void GetClosest(
                TPoint query, float maxDistance, int maxCount,
                List<IndexDist<float>> list)
        {
            list.Clear();
            var q = new ClosestToPointQuery(query, maxDistance, maxCount, list);
            GetClosest(q, 0);
        }

        /// <summary>
        /// Query for at most maxCount points within maxDistance around
        /// the supplied query point. Note that at least one of the two
        /// criteria must be set, otherwise all points in the vp-tree
        /// will be retrieved.The resulting points are returned in a
        /// list of index/distance structs. If a fixed maximum count is
        /// supplied, the list will be in heap order with the point with
        /// the largest distance from the query point on top.
        /// </summary>
        public List<IndexDist<float>> GetClosest(
                TPoint query, float maxDistance, int maxCount)
        {
            var list = StaticCreateList(maxCount);
            var q = new ClosestToPointQuery(query, maxDistance, maxCount, list);
            GetClosest(q, 0);
            return q.List;
        }

        /// <summary>
        /// Get the single closest point from the set.
        /// </summary>
        public IndexDist<float> GetClosest(TPoint query)
        {
            return GetClosest(query, float.MaxValue, 1)[0];
        }

        private void GetAllList(ClosestToPointQuery q, long top)
        {
            q.List.Add(new IndexDist<float>(m_perm[top], float.MinValue));
            long t1 = 2 * top + 1; if (t1 >= m_size) return;
            GetAllList(q, t1);
            long t2 = t1 + 1; if (t2 >= m_size) return;
            GetAllList(q, t2);
        }

        private void GetClosest(ClosestToPointQuery q, long top)
        {
            long index = m_perm[top];
            var vp = m_aget(m_array, index);
            var dist = m_dist(q.Point, vp);
            long t1 = 2 * top + 1;
            if (dist <= q.MaxDist)
            {
                if (q.DynamicSize)
                {
                    q.List.Add(new IndexDist<float>(index, dist));
                    if (t1 >= m_size) return;
                    if (dist + m_lmax[top] < q.MaxDist)
                    {
                        GetAllList(q, t1);
                        long t2 = t1 + 1; if (t2 >= m_size) return;
                        GetClosest(q, t2);
                        return;
                    }
                }
                else
                {
                    q.List.HeapDescendingEnqueue(new IndexDist<float>(index, dist));
                    if (q.List.Count > q.MaxCount)
                    {
                        q.List.HeapDescendingDequeue();
                        q.MaxDist = q.List[0].Dist;
                    }
                    if (t1 >= m_size) return;
                }
            }
            else if (t1 >= m_size) return;
            var splitDist = 0.5 * (m_lmax[top] + m_rmin[top]);
            if (dist < splitDist)
            {
                if (dist - q.MaxDist <= m_lmax[top])
                    GetClosest(q, t1);
                if (dist + q.MaxDist < m_rmin[top]) return;
                long t2 = t1 + 1; if (t2 >= m_size) return;
                GetClosest(q, t2);
            }
            else
            {
                long t2 = t1 + 1;
                if (t2 < m_size
                    && dist + q.MaxDist >= m_rmin[top])
                    GetClosest(q, t2);
                if (dist - q.MaxDist > m_lmax[top]) return;
                GetClosest(q, t1);
            }
        }

        #endregion
    }

    #endregion

    #region PointRkdTreeD

    [RegisterTypeInfo]
    public class PointRkdTreeDData : IFieldCodeable
    {
        public long[] PermArray;
        public int[] AxisArray;
        public double[] RadiusArray;

        public PointRkdTreeDData()
        { }

        public IEnumerable<FieldCoder> GetFieldCoders(int coderVersion)
        {
            return
            [
                new FieldCoder(0, "PermArray",
                        (c,o) => c.CodeLongArray(ref ((PointRkdTreeDData)o).PermArray) ),
                new FieldCoder(1, "AxisArray",
                        (c,o) => c.CodeIntArray(ref ((PointRkdTreeDData)o).AxisArray) ),
                new FieldCoder(2, "RadiusArray",
                        (c,o) => c.CodeDoubleArray(ref ((PointRkdTreeDData)o).RadiusArray) ),
            ];
        }
    }

    /// <summary>
    /// A k-d tree of k-dimensional points on top of three generic type
    /// parameters: TArray an arbitrary array type for which the element
    /// getter needs to be specified, TPoint, an arbitrary point type
    /// for which the accessor to the components must be specified, and
    /// T the compoent type of the point.
    /// The k-d tree does not reorder the elements of the array, it just
    /// generates an internal index array that stores the references to
    /// the array elements in heap order.
    /// </summary>
    public partial class PointRkdTreeD<TArray, TPoint>
    {
        readonly long m_dim;
        readonly long m_size;
        readonly TArray m_array;
        readonly Func<TArray, long, TPoint> m_aget;
        readonly Func<TPoint, long, double> m_vget;
        readonly Func<TPoint, TPoint, double> m_dist;
        readonly Func<long, double, double, double> m_dimDist;
        readonly Func<TPoint, TPoint, TPoint, double> m_lineDist;
        readonly Func<double, TPoint, TPoint, TPoint> m_lerp;
        readonly double m_eps;

        readonly long[] m_perm;
        readonly int[] m_axis; // 2^31 dimensions are way enough
        readonly double[] m_radius;

        #region Constructor

        public PointRkdTreeD(
                long dim, long size, TArray array,
                Func<TArray, long, TPoint> arrayGetter,
                Func<TPoint, long, double> vectorGetter,
                Func<TPoint, TPoint, double> distanceFun,
                Func<long, double, double, double> dimDistanceFun,
                Func<TPoint, TPoint, TPoint, double> lineDistFun,
                Func<double, TPoint, TPoint, TPoint> lerpFun,
                double absoluteEps)
            : this(dim, size, array, arrayGetter, vectorGetter, distanceFun,
                   dimDistanceFun, lineDistFun, lerpFun,absoluteEps,
                   new PointRkdTreeDData
                   {
                        PermArray = new long[size],
                        AxisArray = new int[size/2], // heap internal nodes only => size/2
                        RadiusArray = new double[size / 2],
                   })
        {
            var perm = new long[size].SetByIndexLong(i => i);

            long p2 = Fun.PrevPowerOfTwo(size);
            long row = size + 1 - p2; // length of last row of heap
            long left = p2 / 2; // full width of left subtree in last row

            Balance(perm, 0, left, row, 0, size);
        }

        public PointRkdTreeD(
                long dim, long size, TArray array,
                Func<TArray, long, TPoint> arrayGetter,
                Func<TPoint, long, double> vectorGetter,
                Func<TPoint, TPoint, double> distanceFun,
                Func<long, double, double, double> dimDistanceFun,
                Func<TPoint, TPoint, TPoint, double> lineDistFun,
                Func<double, TPoint, TPoint, TPoint> lerpFun,
                double absoluteEps,
                PointRkdTreeDData data
                )
        {
            m_dim = dim;
            m_size = size;
            m_array = array;
            m_aget = arrayGetter;
            m_vget = vectorGetter;
            m_dist = distanceFun;
            m_dimDist = dimDistanceFun;
            m_lineDist = lineDistFun;
            m_lerp = lerpFun;
            m_eps = absoluteEps;

            m_perm = data?.PermArray;
            m_axis = data?.AxisArray;
            m_radius = data?.RadiusArray;
        }

        private double GetMaxDist(
                TPoint p, long[] perm, long start, long end)
        {
            var max = double.MinValue;
            for (long i = start; i < end; i++)
            {
                var d = m_dist(p, m_aget(m_array, perm[i]));
                if (d > max) max = d;
            }
            return max;
        }

        private long GetMaxDim(
                long[] perm, long start, long end)
        {
            var min = new double[m_dim].Set(double.MaxValue);
            var max = new double[m_dim].Set(double.MinValue);
            for (long i = start; i < end; i++) // calculate bounding box
            {
                var v = m_aget(m_array, perm[i]);
                for (long vi = 0; vi < m_dim; vi++)
                {
                    var x = m_vget(v, vi);
                    if (x < min[vi]) min[vi] = x;
                    if (x > max[vi]) max[vi] = x;
                }
            }
            long dim = 0;
            double size = max[0] - min[0];
            for (long d = 1; d < m_dim; d++) // find max dim of box
            {
                var dsize = max[d] - min[d];
                if (dsize > size) { size = dsize; dim = d; }
            }
            return dim;
        }

        private void Balance(
                long[] perm, long top, long left, long row,
                long start, long end)
        {
            if (row <= 0) { left /= 2; row = long.MaxValue; }
            if (left == 0) { m_perm[top] = perm[start]; return; }
            long mid = start - 1 + left + Fun.Min(left, row);
            long dim = GetMaxDim(perm, start, end);
            m_axis[top] = (int)dim;
            perm.PermutationQuickMedian(m_array, m_aget,
                    (v0, v1) => m_vget(v0, dim).CompareTo(m_vget(v1, dim)),
                    start, end, mid);
            m_perm[top] = perm[mid];
            m_radius[top] = GetMaxDist(m_aget(m_array, perm[mid]), perm, start, end) + m_eps;
            if (start < mid)
                Balance(perm, 2 * top + 1, left / 2, row, start, mid);
            ++mid;
            if (mid < end)
                Balance(perm, 2 * top + 2, left / 2, row - left, mid, end);
        }

        #endregion

        #region Closest Classes

        public class ClosestQuery
        {
            public double MaxDist;
            public double MaxDistEps;
            public int MaxCount;
            public List<IndexDist<double>> List;
            public readonly bool DynamicSize;
            public double OriginalMaxDist;
            public double OriginalMaxDistEps;

            public ClosestQuery()
            { }

            public ClosestQuery(double maxDistance,
                           double maxDistancePlusEps, int maxCount,
                           List<IndexDist<double>> list)
            {
                MaxDist = maxDistance;
                MaxDistEps = maxDistancePlusEps;
                MaxCount = maxCount;
                List = list;
                DynamicSize = maxCount == 0;
                OriginalMaxDist = maxDistance;
                OriginalMaxDistEps = maxDistancePlusEps;
            }

            public virtual void Clear()
            {
                MaxDist = OriginalMaxDist;
                MaxDistEps = OriginalMaxDistEps;
                List.Clear();
            }
        }

        /// <summary>
        /// A query object to handle multiple (possibly accumulated) closest
        /// to point queries.
        /// </summary>
        public class ClosestToPointQuery : ClosestQuery
        {
            public TPoint Point;
            public Func<TPoint, TPoint, double> Dist;
            public Func<long, double, double, double> DimDist;
            public Func<long, bool> Filter;

            public ClosestToPointQuery(
                    double maxDistance,
                    double maxDistancePlusEps, int maxCount,
                    Func<TPoint, TPoint, double> dist,
                    Func<long, double, double, double> dimDist,
                    List<IndexDist<double>> list)
                : base(maxDistance, maxDistancePlusEps, maxCount, list)
            {
                Dist = dist;
                DimDist = dimDist;
            }

            public ClosestToPointQuery(
                    TPoint query, double maxDistance,
                    double maxDistancePlusEps, int maxCount,
                    Func<TPoint, TPoint, double> dist,
                    Func<long, double, double, double> dimDist,
                    List<IndexDist<double>> list)
                : base(maxDistance, maxDistancePlusEps, maxCount, list)
            {
                Point = query;
                Dist = dist;
                DimDist = dimDist;
            }
        }

        #endregion

        #region Properties

        public PointRkdTreeDData Data
        {
            get
            {
                return new PointRkdTreeDData
                {
                    PermArray = m_perm,
                    AxisArray = m_axis,
                    RadiusArray = m_radius
                };
            }
        }

        #endregion

        #region Operations

        private static List<IndexDist<double>> StaticCreateList(int maxCount)
        {
            if (maxCount > 0)
                return new List<IndexDist<double>>(maxCount + 1);
            else
                return [];
        }

        /// <summary>
        /// Create a closest to point query object for multiple point
        /// queries with the same maximum distance and maximum count
        /// values.
        /// </summary>
        public ClosestToPointQuery CreateClosestToPointQuery(
                double maxDistance, int maxCount)
        {
            var maxDistPlusEps = maxDistance < double.MaxValue
                    ? maxDistance + m_eps : maxDistance;
            var q = new ClosestToPointQuery(
                                maxDistance, maxDistPlusEps, maxCount,
                                m_dist, m_dimDist,
                                StaticCreateList(maxCount));
            return q;
        }

        /// <summary>
        /// Add the query result with the supplied point to the closest to
        /// point query object. The accumulated result is returned, or can
        /// be retrieved as the List property of the query object.
        /// </summary>
        public List<IndexDist<double>> GetClosest(
                ClosestToPointQuery query, TPoint point)
        {
            query.Point = point;
            if (query.Filter == null)
                GetClosest(query, 0);
            else
                GetClosestFilter(query, 0);
            return query.List;
        }

        /// <summary>
        /// Query for at most maxCount points within maxDistance around
        /// the supplied query point. Note that at least one of the two
        /// criteria must be set, otherwise all points in the k-d-tree
        /// will be retrieved. The resulting points are returned in a
        /// list of index/distance structs. If a fixed maximum count is
        /// supplied, the list will be in heap order with the point with
        /// the largest distance from the query point on top.
        /// </summary>
        public List<IndexDist<double>> GetClosest(
                TPoint query, double maxDistance, int maxCount)
        {
            var list = StaticCreateList(maxCount);
            var mdplus = maxDistance < double.MaxValue
                                ? maxDistance + m_eps
                                : maxDistance;
            var q = new ClosestToPointQuery(query, maxDistance, mdplus,
                                    maxCount, m_dist, m_dimDist, list);
            GetClosest(q, 0);
            return q.List;
        }

        /// <summary>
        /// Get the single closest point from the set.
        /// </summary>
        public IndexDist<double> GetClosest(TPoint query)
        {
            return GetClosest(query, double.MaxValue, 1)[0];
        }

        private void GetAllList(ClosestToPointQuery q, long top)
        {
            q.List.Add(new IndexDist<double>(m_perm[top], double.MinValue));
            long t1 = 2 * top + 1; if (t1 >= m_size) return;
            GetAllList(q, t1);
            long t2 = t1 + 1; if (t2 >= m_size) return;
            GetAllList(q, t2);
        }

        private void GetClosest(ClosestToPointQuery q, long top)
        {
            long index = m_perm[top];
            var splitPoint = m_aget(m_array, index);
            var dist = q.Dist(q.Point, splitPoint);
            long t1 = 2 * top + 1;
            var delta = dist - q.MaxDist;
            if (delta <= 0.0)
            {
                if (q.DynamicSize)
                {
                    q.List.Add(new IndexDist<double>(index, dist));
                    if (t1 >= m_size) return;
                    if (delta < -m_radius[top])
                    {
                        GetAllList(q, t1);
                        long t2 = t1 + 1; if (t2 >= m_size) return;
                        GetAllList(q, t2);
                        return;
                    }
                }
                else
                {
                    q.List.HeapDescendingEnqueue(new IndexDist<double>(index, dist));
                    if (q.List.Count > q.MaxCount)
                    {
                        q.List.HeapDescendingDequeue();
                        var md = q.List[0].Dist;
                        q.MaxDist = md; q.MaxDistEps = md + m_eps;
                    }
                    if (t1 >= m_size) return;
                }
            }
            else
            {
                if (t1 >= m_size) return;
                if (delta > m_radius[top]) return;
            }
            var dim = m_axis[top];
            var x = m_vget(q.Point, dim); var s = m_vget(splitPoint, dim);
            if (x < s)
            {
                GetClosest(q, t1);
                if (q.MaxDistEps < q.DimDist(dim, x, s)) return;
                long t2 = t1 + 1; if (t2 >= m_size) return;
                GetClosest(q, t2);
            }
            else
            {
                long t2 = t1 + 1;
                if (t2 < m_size) GetClosest(q, t2);
                if (q.MaxDistEps < q.DimDist(dim, s, x)) return;
                GetClosest(q, t1);
            }
        }

        //private void GetAllListFilter(ClosestToPointQuery q, long top)
        //{
        //    var index = m_perm[top];
        //    if (q.Filter(index))
        //        q.List.Add(new IndexDist<double>(m_perm[top], double.MinValue));
        //    long t1 = 2 * top + 1; if (t1 >= m_size) return;
        //    GetAllList(q, t1);
        //    long t2 = t1 + 1; if (t2 >= m_size) return;
        //    GetAllList(q, t2);
        //}

        private void GetClosestFilter(ClosestToPointQuery q, long top)
        {
            long index = m_perm[top];
            var splitPoint = m_aget(m_array, index);
            var dist = q.Dist(q.Point, splitPoint);
            long t1 = 2 * top + 1;
            var delta = dist - q.MaxDist;
            if (delta <= 0.0)
            {
                if (q.DynamicSize)
                {
                    if (q.Filter(index))
                        q.List.Add(new IndexDist<double>(index, dist));
                    if (t1 >= m_size) return;
                    if (delta < -m_radius[top])
                    {
                        GetAllList(q, t1);
                        long t2 = t1 + 1; if (t2 >= m_size) return;
                        GetAllList(q, t2);
                        return;
                    }
                }
                else
                {
                    if (q.Filter(index))
                        q.List.HeapDescendingEnqueue(new IndexDist<double>(index, dist));
                    if (q.List.Count > q.MaxCount)
                    {
                        q.List.HeapDescendingDequeue();
                        var md = q.List[0].Dist;
                        q.MaxDist = md; q.MaxDistEps = md + m_eps;
                    }
                    if (t1 >= m_size) return;
                }
            }
            else
            {
                if (t1 >= m_size) return;
                if (delta > m_radius[top]) return;
            }
            var dim = m_axis[top];
            var x = m_vget(q.Point, dim); var s = m_vget(splitPoint, dim);
            if (x < s)
            {
                GetClosestFilter(q, t1);
                if (q.MaxDistEps < q.DimDist(dim, x, s)) return;
                long t2 = t1 + 1; if (t2 >= m_size) return;
                GetClosestFilter(q, t2);
            }
            else
            {
                long t2 = t1 + 1;
                if (t2 < m_size) GetClosestFilter(q, t2);
                if (q.MaxDistEps < q.DimDist(dim, s, x)) return;
                GetClosestFilter(q, t1);
            }
        }

        #endregion

    }

    #endregion

    #region PointRkdTreeDSelector

    [RegisterTypeInfo]
    public class PointRkdTreeDSelectorData : IFieldCodeable
    {
        public long[] PermArray;
        public int[] AxisArray;
        public double[] RadiusArray;

        public PointRkdTreeDSelectorData()
        { }

        public IEnumerable<FieldCoder> GetFieldCoders(int coderVersion)
        {
            return
            [
                new FieldCoder(0, "PermArray",
                        (c,o) => c.CodeLongArray(ref ((PointRkdTreeDSelectorData)o).PermArray) ),
                new FieldCoder(1, "AxisArray",
                        (c,o) => c.CodeIntArray(ref ((PointRkdTreeDSelectorData)o).AxisArray) ),
                new FieldCoder(2, "RadiusArray",
                        (c,o) => c.CodeDoubleArray(ref ((PointRkdTreeDSelectorData)o).RadiusArray) ),
            ];
        }
    }

    /// <summary>
    /// A k-d tree of k-dimensional points on top of three generic type
    /// parameters: TArray an arbitrary array type for which the element
    /// getter needs to be specified, TPoint, an arbitrary point type
    /// for which the accessor to the components must be specified, and
    /// T the compoent type of the point.
    /// The k-d tree does not reorder the elements of the array, it just
    /// generates an internal index array that stores the references to
    /// the array elements in heap order.
    /// </summary>
    public partial class PointRkdTreeDSelector<TArray, TPoint>
    {
        readonly long m_dim;
        readonly long m_size;
        readonly TArray m_array;
        readonly Func<TArray, long, TPoint> m_aget;
        readonly Func<TPoint, double>[] m_sela;
        readonly Func<TPoint, TPoint, double> m_dist;
        readonly Func<double, double, double>[] m_dimDistA;
        readonly Func<TPoint, TPoint, TPoint, double> m_lineDist;
        readonly Func<double, TPoint, TPoint, TPoint> m_lerp;
        readonly double m_eps;

        readonly long[] m_perm;
        readonly int[] m_axis; // 2^31 dimensions are way enough
        readonly double[] m_radius;

        #region Constructor

        public PointRkdTreeDSelector(
                long dim, long size, TArray array,
                Func<TArray, long, TPoint> arrayGetter,
                Func<TPoint, double>[] selectorArray,
                Func<TPoint, TPoint, double> distanceFun,
                Func<double, double, double>[] dimDistanceFunArray,
                Func<TPoint, TPoint, TPoint, double> lineDistFun,
                Func<double, TPoint, TPoint, TPoint> lerpFun,
                double absoluteEps)
            : this(dim, size, array, arrayGetter, selectorArray, distanceFun,
                   dimDistanceFunArray, lineDistFun, lerpFun,absoluteEps,
                   new PointRkdTreeDSelectorData
                   {
                        PermArray = new long[size],
                        AxisArray = new int[size/2], // heap internal nodes only => size/2
                        RadiusArray = new double[size / 2],
                   })
        {
            var perm = new long[size].SetByIndexLong(i => i);

            long p2 = Fun.PrevPowerOfTwo(size);
            long row = size + 1 - p2; // length of last row of heap
            long left = p2 / 2; // full width of left subtree in last row

            Balance(perm, 0, left, row, 0, size);
        }

        public PointRkdTreeDSelector(
                long dim, long size, TArray array,
                Func<TArray, long, TPoint> arrayGetter,
                Func<TPoint, double>[] selectorArray,
                Func<TPoint, TPoint, double> distanceFun,
                Func<double, double, double>[] dimDistanceFunArray,
                Func<TPoint, TPoint, TPoint, double> lineDistFun,
                Func<double, TPoint, TPoint, TPoint> lerpFun,
                double absoluteEps,
                PointRkdTreeDSelectorData data
                )
        {
            m_dim = dim;
            m_size = size;
            m_array = array;
            m_aget = arrayGetter;
            m_sela = selectorArray;
            m_dist = distanceFun;
            m_dimDistA = dimDistanceFunArray;
            m_lineDist = lineDistFun;
            m_lerp = lerpFun;
            m_eps = absoluteEps;

            m_perm = data?.PermArray;
            m_axis = data?.AxisArray;
            m_radius = data?.RadiusArray;
        }

        private double GetMaxDist(
                TPoint p, long[] perm, long start, long end)
        {
            var max = double.MinValue;
            for (long i = start; i < end; i++)
            {
                var d = m_dist(p, m_aget(m_array, perm[i]));
                if (d > max) max = d;
            }
            return max;
        }

        private long GetMaxDim(
                long[] perm, long start, long end)
        {
            var min = new double[m_dim].Set(double.MaxValue);
            var max = new double[m_dim].Set(double.MinValue);
            for (long i = start; i < end; i++) // calculate bounding box
            {
                var v = m_aget(m_array, perm[i]);
                for (long vi = 0; vi < m_dim; vi++)
                {
                    var x = m_sela[vi](v);
                    if (x < min[vi]) min[vi] = x;
                    if (x > max[vi]) max[vi] = x;
                }
            }
            long dim = 0;
            double size = max[0] - min[0];
            for (long d = 1; d < m_dim; d++) // find max dim of box
            {
                var dsize = max[d] - min[d];
                if (dsize > size) { size = dsize; dim = d; }
            }
            return dim;
        }

        private void Balance(
                long[] perm, long top, long left, long row,
                long start, long end)
        {
            if (row <= 0) { left /= 2; row = long.MaxValue; }
            if (left == 0) { m_perm[top] = perm[start]; return; }
            long mid = start - 1 + left + Fun.Min(left, row);
            long dim = GetMaxDim(perm, start, end);
            m_axis[top] = (int)dim;
            perm.PermutationQuickMedianAscending(m_array, m_aget,
                    m_sela[dim],
                    start, end, mid);
            m_perm[top] = perm[mid];
            m_radius[top] = GetMaxDist(m_aget(m_array, perm[mid]), perm, start, end) + m_eps;
            if (start < mid)
                Balance(perm, 2 * top + 1, left / 2, row, start, mid);
            ++mid;
            if (mid < end)
                Balance(perm, 2 * top + 2, left / 2, row - left, mid, end);
        }

        #endregion

        #region Closest Classes

        public class ClosestQuery
        {
            public double MaxDist;
            public double MaxDistEps;
            public int MaxCount;
            public List<IndexDist<double>> List;
            public readonly bool DynamicSize;
            public double OriginalMaxDist;
            public double OriginalMaxDistEps;

            public ClosestQuery()
            { }

            public ClosestQuery(double maxDistance,
                           double maxDistancePlusEps, int maxCount,
                           List<IndexDist<double>> list)
            {
                MaxDist = maxDistance;
                MaxDistEps = maxDistancePlusEps;
                MaxCount = maxCount;
                List = list;
                DynamicSize = maxCount == 0;
                OriginalMaxDist = maxDistance;
                OriginalMaxDistEps = maxDistancePlusEps;
            }

            public virtual void Clear()
            {
                MaxDist = OriginalMaxDist;
                MaxDistEps = OriginalMaxDistEps;
                List.Clear();
            }
        }

        /// <summary>
        /// A query object to handle multiple (possibly accumulated) closest
        /// to point queries.
        /// </summary>
        public class ClosestToPointQuery : ClosestQuery
        {
            public TPoint Point;
            public Func<TPoint, TPoint, double> Dist;
            public Func<double, double, double>[] DimDistArray;
            public Func<long, bool> Filter;

            public ClosestToPointQuery(
                    double maxDistance,
                    double maxDistancePlusEps, int maxCount,
                    Func<TPoint, TPoint, double> dist,
                    Func<double, double, double>[] dimDistA,
                    List<IndexDist<double>> list)
                : base(maxDistance, maxDistancePlusEps, maxCount, list)
            {
                Dist = dist;
                DimDistArray = dimDistA;
            }

            public ClosestToPointQuery(
                    TPoint query, double maxDistance,
                    double maxDistancePlusEps, int maxCount,
                    Func<TPoint, TPoint, double> dist,
                    Func<double, double, double>[] dimDistA,
                    List<IndexDist<double>> list)
                : base(maxDistance, maxDistancePlusEps, maxCount, list)
            {
                Point = query;
                Dist = dist;
                DimDistArray = dimDistA;
            }
        }

        #endregion

        #region Properties

        public PointRkdTreeDSelectorData Data
        {
            get
            {
                return new PointRkdTreeDSelectorData
                {
                    PermArray = m_perm,
                    AxisArray = m_axis,
                    RadiusArray = m_radius
                };
            }
        }

        #endregion

        #region Operations

        private static List<IndexDist<double>> StaticCreateList(int maxCount)
        {
            if (maxCount > 0)
                return new List<IndexDist<double>>(maxCount + 1);
            else
                return [];
        }

        /// <summary>
        /// Create a closest to point query object for multiple point
        /// queries with the same maximum distance and maximum count
        /// values.
        /// </summary>
        public ClosestToPointQuery CreateClosestToPointQuery(
                double maxDistance, int maxCount)
        {
            var maxDistPlusEps = maxDistance < double.MaxValue
                    ? maxDistance + m_eps : maxDistance;
            var q = new ClosestToPointQuery(
                                maxDistance, maxDistPlusEps, maxCount,
                                m_dist, m_dimDistA,
                                StaticCreateList(maxCount));
            return q;
        }

        /// <summary>
        /// Add the query result with the supplied point to the closest to
        /// point query object. The accumulated result is returned, or can
        /// be retrieved as the List property of the query object.
        /// </summary>
        public List<IndexDist<double>> GetClosest(
                ClosestToPointQuery query, TPoint point)
        {
            query.Point = point;
            if (query.Filter == null)
                GetClosest(query, 0);
            else
                GetClosestFilter(query, 0);
            return query.List;
        }

        /// <summary>
        /// Query for at most maxCount points within maxDistance around
        /// the supplied query point. Note that at least one of the two
        /// criteria must be set, otherwise all points in the k-d-tree
        /// will be retrieved. The resulting points are returned in a
        /// list of index/distance structs. If a fixed maximum count is
        /// supplied, the list will be in heap order with the point with
        /// the largest distance from the query point on top.
        /// </summary>
        public List<IndexDist<double>> GetClosest(
                TPoint query, double maxDistance, int maxCount)
        {
            var list = StaticCreateList(maxCount);
            var mdplus = maxDistance < double.MaxValue
                                ? maxDistance + m_eps
                                : maxDistance;
            var q = new ClosestToPointQuery(query, maxDistance, mdplus,
                                    maxCount, m_dist, m_dimDistA, list);
            GetClosest(q, 0);
            return q.List;
        }

        /// <summary>
        /// Get the single closest point from the set.
        /// </summary>
        public IndexDist<double> GetClosest(TPoint query)
        {
            return GetClosest(query, double.MaxValue, 1)[0];
        }

        private void GetAllList(ClosestToPointQuery q, long top)
        {
            q.List.Add(new IndexDist<double>(m_perm[top], double.MinValue));
            long t1 = 2 * top + 1; if (t1 >= m_size) return;
            GetAllList(q, t1);
            long t2 = t1 + 1; if (t2 >= m_size) return;
            GetAllList(q, t2);
        }

        private void GetClosest(ClosestToPointQuery q, long top)
        {
            long index = m_perm[top];
            var splitPoint = m_aget(m_array, index);
            var dist = q.Dist(q.Point, splitPoint);
            long t1 = 2 * top + 1;
            var delta = dist - q.MaxDist;
            if (delta <= 0.0)
            {
                if (q.DynamicSize)
                {
                    q.List.Add(new IndexDist<double>(index, dist));
                    if (t1 >= m_size) return;
                    if (delta < -m_radius[top])
                    {
                        GetAllList(q, t1);
                        long t2 = t1 + 1; if (t2 >= m_size) return;
                        GetAllList(q, t2);
                        return;
                    }
                }
                else
                {
                    q.List.HeapDescendingEnqueue(new IndexDist<double>(index, dist));
                    if (q.List.Count > q.MaxCount)
                    {
                        q.List.HeapDescendingDequeue();
                        var md = q.List[0].Dist;
                        q.MaxDist = md; q.MaxDistEps = md + m_eps;
                    }
                    if (t1 >= m_size) return;
                }
            }
            else
            {
                if (t1 >= m_size) return;
                if (delta > m_radius[top]) return;
            }
            var dim = m_axis[top];
            var sel = m_sela[dim]; var x = sel(q.Point); var s = sel(splitPoint);
            if (x < s)
            {
                GetClosest(q, t1);
                if (q.MaxDistEps < q.DimDistArray[dim](x, s)) return;
                long t2 = t1 + 1; if (t2 >= m_size) return;
                GetClosest(q, t2);
            }
            else
            {
                long t2 = t1 + 1;
                if (t2 < m_size) GetClosest(q, t2);
                if (q.MaxDistEps < q.DimDistArray[dim](s, x)) return;
                GetClosest(q, t1);
            }
        }

        //private void GetAllListFilter(ClosestToPointQuery q, long top)
        //{
        //    var index = m_perm[top];
        //    if (q.Filter(index))
        //        q.List.Add(new IndexDist<double>(m_perm[top], double.MinValue));
        //    long t1 = 2 * top + 1; if (t1 >= m_size) return;
        //    GetAllList(q, t1);
        //    long t2 = t1 + 1; if (t2 >= m_size) return;
        //    GetAllList(q, t2);
        //}

        private void GetClosestFilter(ClosestToPointQuery q, long top)
        {
            long index = m_perm[top];
            var splitPoint = m_aget(m_array, index);
            var dist = q.Dist(q.Point, splitPoint);
            long t1 = 2 * top + 1;
            var delta = dist - q.MaxDist;
            if (delta <= 0.0)
            {
                if (q.DynamicSize)
                {
                    if (q.Filter(index))
                        q.List.Add(new IndexDist<double>(index, dist));
                    if (t1 >= m_size) return;
                    if (delta < -m_radius[top])
                    {
                        GetAllList(q, t1);
                        long t2 = t1 + 1; if (t2 >= m_size) return;
                        GetAllList(q, t2);
                        return;
                    }
                }
                else
                {
                    if (q.Filter(index))
                        q.List.HeapDescendingEnqueue(new IndexDist<double>(index, dist));
                    if (q.List.Count > q.MaxCount)
                    {
                        q.List.HeapDescendingDequeue();
                        var md = q.List[0].Dist;
                        q.MaxDist = md; q.MaxDistEps = md + m_eps;
                    }
                    if (t1 >= m_size) return;
                }
            }
            else
            {
                if (t1 >= m_size) return;
                if (delta > m_radius[top]) return;
            }
            var dim = m_axis[top];
            var sel = m_sela[dim]; var x = sel(q.Point); var s = sel(splitPoint);
            if (x < s)
            {
                GetClosestFilter(q, t1);
                if (q.MaxDistEps < q.DimDistArray[dim](x, s)) return;
                long t2 = t1 + 1; if (t2 >= m_size) return;
                GetClosestFilter(q, t2);
            }
            else
            {
                long t2 = t1 + 1;
                if (t2 < m_size) GetClosestFilter(q, t2);
                if (q.MaxDistEps < q.DimDistArray[dim](s, x)) return;
                GetClosestFilter(q, t1);
            }
        }

        #endregion

    }

    #endregion

    #region PointKdTreeD

    [RegisterTypeInfo]
    public class PointKdTreeDData : IFieldCodeable
    {
        public long[] PermArray;
        public int[] AxisArray;

        public PointKdTreeDData()
        { }

        public IEnumerable<FieldCoder> GetFieldCoders(int coderVersion)
        {
            return
            [
                new FieldCoder(0, "PermArray",
                        (c,o) => c.CodeLongArray(ref ((PointKdTreeDData)o).PermArray) ),
                new FieldCoder(1, "AxisArray",
                        (c,o) => c.CodeIntArray(ref ((PointKdTreeDData)o).AxisArray) ),
            ];
        }
    }

    /// <summary>
    /// A k-d tree of k-dimensional points on top of three generic type
    /// parameters: TArray an arbitrary array type for which the element
    /// getter needs to be specified, TPoint, an arbitrary point type
    /// for which the accessor to the components must be specified, and
    /// T the compoent type of the point.
    /// The k-d tree does not reorder the elements of the array, it just
    /// generates an internal index array that stores the references to
    /// the array elements in heap order.
    /// </summary>
    public partial class PointKdTreeD<TArray, TPoint>
    {
        readonly long m_dim;
        readonly long m_size;
        readonly TArray m_array;
        readonly Func<TArray, long, TPoint> m_aget;
        readonly Func<TPoint, long, double> m_vget;
        readonly Func<TPoint, TPoint, double> m_dist;
        readonly Func<long, double, double, double> m_dimDist;
        readonly double m_eps;

        readonly long[] m_perm;
        readonly int[] m_axis; // 2^31 dimensions are way enough

        #region Constructor

        public PointKdTreeD(
                long dim, long size, TArray array,
                Func<TArray, long, TPoint> arrayGetter,
                Func<TPoint, long, double> vectorGetter,
                Func<TPoint, TPoint, double> distanceFun,
                Func<long, double, double, double> dimDistanceFun,
                double absoluteEps)
            : this(dim, size, array, arrayGetter, vectorGetter, distanceFun,
                    dimDistanceFun, absoluteEps,
                    new PointKdTreeDData
                    {
                        PermArray = new long[size],
                        AxisArray = new int[size / 2] // heap internal nodes only => size/2
                    })
        {
            var min = new double[m_dim].Set(double.MaxValue);
            var max = new double[m_dim].Set(double.MinValue);

            for (long ai = 0; ai < m_size; ai++) // calculate bounding box
            {
                var v = m_aget(m_array, ai);
                for (long vi = 0; vi < m_dim; vi++)
                {
                    var x = m_vget(v, vi);
                    if (x < min[vi]) min[vi] = x;
                    if (x > max[vi]) max[vi] = x;
                }
            }

            var perm = new long[size].SetByIndexLong(i => i);

            long p2 = Fun.PrevPowerOfTwo(size);
            long row = size + 1 - p2; // length of last row of heap
            long left = p2 / 2; // full width of left subtree in last row

            Balance(perm, 0, left, row, 0, size, min, max);
        }
        
        public PointKdTreeD(
                long dim, long size, TArray array,
                Func<TArray, long, TPoint> arrayGetter,
                Func<TPoint, long, double> vectorGetter,
                Func<TPoint, TPoint, double> distanceFun,
                Func<long, double, double, double> dimDistanceFun,
                double absoluteEps,
                PointKdTreeDData data
                )
        {
            m_dim = dim;
            m_size = size;
            m_array = array;
            m_aget = arrayGetter;
            m_vget = vectorGetter;
            m_dist = distanceFun;
            m_dimDist = dimDistanceFun;
            m_eps = absoluteEps;

            m_perm = data?.PermArray;
            m_axis = data?.AxisArray;
        }

        private void Balance(
                long[] perm, long top, long left, long row,
                long start, long end, double[] min, double[] max)
        {
            if (row <= 0) { left /= 2; row = long.MaxValue; }
            if (left == 0) { m_perm[top] = perm[start]; return; }
            long mid = start - 1 + left + Fun.Min(left, row);
            long dim = 0;
            double size = max[0] - min[0];
            for (long d = 1; d < m_dim; d++) // find max dim of box
            {
                var dsize = max[d] - min[d];
                if (dsize > size) { size = dsize; dim = d; }
            }
            m_axis[top] = (int)dim;
            perm.PermutationQuickMedian(m_array, m_aget,
                    (v0, v1) => m_vget(v0, dim).CompareTo(m_vget(v1, dim)),
                    start, end, mid);
            m_perm[top] = perm[mid];
            if (start < mid)
            {
                var tmp = max[dim];
                var lmax = double.MinValue;
                for (long i = start; i < mid; i++)
                {
                    var val = m_vget(m_aget(m_array, perm[i]), dim);
                    if (val > lmax) lmax = val;
                }
                max[dim] = lmax; // modify box to avoid allocation
                Balance(perm, 2 * top + 1, left / 2, row, start, mid, min, max);
                max[dim] = tmp; // restore box
            }
            ++mid;
            if (mid < end)
            {
                var tmp = min[dim];
                var rmin = double.MaxValue;
                for (long i = mid; i < end; i++)
                {
                    var val = m_vget(m_aget(m_array, perm[i]), dim);
                    if (val < rmin) rmin = val;
                }
                min[dim] = rmin;
                Balance(perm, 2 * top + 2, left / 2, row - left, mid, end, min, max);
                min[dim] = tmp;
            }
        }

        #endregion

        #region Class ClosestToPointQuery


        public class ClosestQuery
        {
            public double MaxDist;
            public double MaxDistEps;
            public int MaxCount;
            public List<IndexDist<double>> List;
            public readonly bool DynamicSize;
            public double OriginalMaxDist;
            public double OriginalMaxDistEps;

            public ClosestQuery()
            { }

            public ClosestQuery(double maxDistance,
                           double maxDistancePlusEps, int maxCount,
                           List<IndexDist<double>> list)
            {
                MaxDist = maxDistance;
                MaxDistEps = maxDistancePlusEps;
                MaxCount = maxCount;
                List = list;
                DynamicSize = maxCount == 0;
                OriginalMaxDist = maxDistance;
                OriginalMaxDistEps = maxDistancePlusEps;
            }

            public virtual void Clear()
            {
                MaxDist = OriginalMaxDist;
                MaxDistEps = OriginalMaxDistEps;
                List.Clear();
            }
        }

        /// <summary>
        /// A query object to handle multiple (possibly accumulated) closest
        /// to point queries.
        /// </summary>
        public class ClosestToPointQuery : ClosestQuery
        {
            public TPoint Point;
            public Func<TPoint, TPoint, double> Dist;
            public Func<long, double, double, double> DimDist;
            public Func<long, bool> Filter;

            public ClosestToPointQuery(
                    double maxDistance,
                    double maxDistancePlusEps, int maxCount,
                    Func<TPoint, TPoint, double> dist,
                    Func<long, double, double, double> dimDist,
                    List<IndexDist<double>> list)
                : base(maxDistance, maxDistancePlusEps, maxCount, list)
            {
                Dist = dist;
                DimDist = dimDist;
            }

            public ClosestToPointQuery(
                    TPoint query, double maxDistance,
                    double maxDistancePlusEps, int maxCount,
                    Func<TPoint, TPoint, double> dist,
                    Func<long, double, double, double> dimDist,
                    List<IndexDist<double>> list)
                : base(maxDistance, maxDistancePlusEps, maxCount, list)
            {
                Point = query;
                Dist = dist;
                DimDist = dimDist;
            }
        }

        #endregion

        #region Properties

        public PointKdTreeDData Data
        {
            get
            {
                return new PointKdTreeDData
                {
                    PermArray = m_perm,
                    AxisArray = m_axis
                };
            }
        }

        #endregion

        #region Operations

        private static List<IndexDist<double>> StaticCreateList(int maxCount)
        {
            if (maxCount > 0)
                return new List<IndexDist<double>>(maxCount + 1);
            else
                return [];
        }

        /// <summary>
        /// Create a closest to point query object for multiple point
        /// queries with the same maximum distance and maximum count
        /// values.
        /// </summary>
        public ClosestToPointQuery CreateClosestToPointQuery(
                double maxDistance, int maxCount)
        {
            var maxDistPlusEps = maxDistance < double.MaxValue
                    ? maxDistance + m_eps : maxDistance;
            var q = new ClosestToPointQuery(
                                maxDistance, maxDistPlusEps, maxCount,
                                m_dist, m_dimDist,
                                StaticCreateList(maxCount));
            return q;
        }

        /// <summary>
        /// Add the query result with the supplied point to the closest to
        /// point query object. The accumulated result is returned, or can
        /// be retrieved as the List property of the query object.
        /// </summary>
        public List<IndexDist<double>> GetClosest(
                ClosestToPointQuery query, TPoint point)
        {
            query.Point = point;
            GetClosest(query, 0);
            return query.List;
        }

        /// <summary>
        /// Create a list to use in multiple GetClosest queries.
        /// </summary>
        /// <param name="maxCount"></param>
        /// <returns></returns>
        public List<IndexDist<double>> CreateList(int maxCount)
        {
            return StaticCreateList(maxCount);
        }

        /// <summary>
        /// Query for at most maxCount points within maxDistance around
        /// the supplied query point. Note that at least one of the two
        /// criteria must be set, otherwise all points in the k-d-tree
        /// will be retrieved. The resulting points are inserted into
        /// the supplied list of index/distance structs, which is first
        /// cleared. If a fixed maximum count is supplied, the list will
        /// be in heap order with the point with the largest distance from
        /// the query point on top.
        /// </summary>
        public void GetClosest(
                TPoint query, double maxDistance, int maxCount,
                List<IndexDist<double>> list)
        {
            list.Clear();
            var mdplus = maxDistance < double.MaxValue
                                ? maxDistance + m_eps
                                : maxDistance;
            var q = new ClosestToPointQuery(query, maxDistance, mdplus,
                                    maxCount, m_dist, m_dimDist, list);
            GetClosest(q, 0);
        }

        /// <summary>
        /// Query for at most maxCount points within maxDistance around
        /// the supplied query point. Note that at least one of the two
        /// criteria must be set, otherwise all points in the k-d-tree
        /// will be retrieved. The resulting points are returned in a
        /// list of index/distance structs. If a fixed maximum count is
        /// supplied, the list will be in heap order with the point with
        /// the largest distance from the query point on top.
        /// </summary>
        public List<IndexDist<double>> GetClosest(
                TPoint query, double maxDistance, int maxCount)
        {
            var list = StaticCreateList(maxCount);
            var mdplus = maxDistance < double.MaxValue
                                ? maxDistance + m_eps
                                : maxDistance;
            var q = new ClosestToPointQuery(query, maxDistance, mdplus,
                                    maxCount, m_dist, m_dimDist, list);
            GetClosest(q, 0);
            return q.List;
        }

        /// <summary>
        /// Get the single closest point from the set.
        /// </summary>
        public IndexDist<double> GetClosest(TPoint query)
        {
            return GetClosest(query, double.MaxValue, 1)[0];
        }

        private void GetClosest(ClosestToPointQuery q, long top)
        {
            long index = m_perm[top];
            var splitPoint = m_aget(m_array, index);
            var dist = q.Dist(q.Point, splitPoint);
            if (dist <= q.MaxDist)
            {
                if (q.DynamicSize)
                    q.List.Add(new IndexDist<double>(index, dist));
                else
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
            long t1 = 2 * top + 1; if (t1 >= m_size) return;
            var dim = m_axis[top];
            var x = m_vget(q.Point, dim);
            var s = m_vget(splitPoint, dim);
            if (x < s)
            {
                GetClosest(q, t1);
                if (q.MaxDistEps < q.DimDist(dim, x, s)) return;
                long t2 = t1 + 1; if (t2 >= m_size) return;
                GetClosest(q, t2);
            }
            else
            {
                long t2 = t1 + 1;
                if (t2 < m_size) GetClosest(q, t2);
                if (q.MaxDistEps < q.DimDist(dim, s, x)) return;
                GetClosest(q, t1);
            }
        }

        #endregion
    }

    #endregion

    #region PointVpTreeD

    [RegisterTypeInfo]
    public class PointVpTreeDData : IFieldCodeable
    {
        public long[] PermArray;
        public double[] LMaxArray;
        public double[] RMinArray;

        public PointVpTreeDData()
        { }

        public IEnumerable<FieldCoder> GetFieldCoders(int coderVersion)
        {
            return
            [
                new FieldCoder(0, "PermArray",
                        (c,o) => c.CodeLongArray(ref ((PointVpTreeDData)o).PermArray) ),
                new FieldCoder(1, "LMaxArray",
                        (c,o) => c.CodeDoubleArray(ref ((PointVpTreeDData)o).LMaxArray) ),
                new FieldCoder(2, "RMinArray",
                        (c,o) => c.CodeDoubleArray(ref ((PointVpTreeDData)o).RMinArray) ),
            ];
        }
    }

    /// <summary>
    /// A vp tree of k-dimensional points on top of three generic type
    /// parameters: TArray an arbitrary array type for which the element
    /// getter needs to be specified, TPoint, and an arbitrary point type
    /// for which the accessor to the components must be specified.
    /// The vp tree does not reorder the elements of the array, it just
    /// generates an internal index array that stores the references to
    /// the array elements in heap order.
    /// </summary>
    public partial class PointVpTreeD<TArray, TPoint>
    {
        readonly long m_dim;
        readonly long m_size;
        readonly TArray m_array;
        readonly Func<TArray, long, TPoint> m_aget;
        readonly Func<TPoint, long, double> m_vget;
        readonly Func<TPoint, TPoint, double> m_dist;
        readonly double m_eps;

        readonly long[] m_perm;
        readonly double[] m_lmax;
        readonly double[] m_rmin;

        #region Constructor

        public PointVpTreeD(
                long dim, long size, TArray array,
                Func<TArray, long, TPoint> arrayGetter,
                Func<TPoint, long, double> vectorGetter,
                Func<TPoint, TPoint, double> distanceFun,
                double absoluteEpsilon)
            : this(dim, size, array, arrayGetter, vectorGetter, distanceFun,
                    absoluteEpsilon,
                    new PointVpTreeDData
                    {
                        PermArray = new long[size],
                        LMaxArray = new double[size / 2], // Only internal nodes of the heap
                        RMinArray = new double[size / 2] // need these bounds => size/2.
                    })
        {
            var perm = new long[size].SetByIndexLong(i => i);

            long p2 = Fun.PrevPowerOfTwo(size);
            long row = size + 1 - p2; // length of last row of heap
            long left = p2 / 2; // length of left side in last row

            Balance(perm, 0, left, row, 0, size);
        }

        public PointVpTreeD(
                long dim, long size, TArray array,
                Func<TArray, long, TPoint> arrayGetter,
                Func<TPoint, long, double> vectorGetter,
                Func<TPoint, TPoint, double> distanceFun,
                double absoluteEpsilon,
                PointVpTreeDData data
                )
        {
            m_dim = dim;
            m_size = size;
            m_array = array;
            m_aget = arrayGetter;
            m_vget = vectorGetter;
            m_dist = distanceFun;
            m_eps = absoluteEpsilon;

            m_perm = data?.PermArray;
            m_lmax = data?.LMaxArray;
            m_rmin = data?.RMinArray;
        }

        private long GetMinMaxIndex(
                long[] perm, long start, long end, out double min, out double max)
        {
            min = double.MaxValue;
            max = double.MinValue;
            var vp = m_aget(m_array, perm[start]);
            long vi = -1;
            for (long i = start + 1; i < end; i++)
            {
                var d = m_dist(vp, m_aget(m_array, perm[i]));
                if (d > max) { max = d; vi = i; }
                if (d < min) min = d;
            }
            return vi;
        }

        private double GetMin(long[] perm, long start, long end, TPoint vp)
        {
            double min = double.MaxValue;
            for (long i = start; i < end; i++)
            {
                var d = m_dist(vp, m_aget(m_array, perm[i]));
                if (d < min) min = d;
            }
            return min;
        }

        private void Balance(
                long[] perm, long top, long left, long row,
                long start, long end)
        {
            if (row <= 0) { left /= 2; row = long.MaxValue; }
            if (left == 0) { m_perm[top] = perm[start]; return; }
            long mid = start - 1 + left + Fun.Min(left, row);
            perm.Swap(mid, start); // vp candidate @start
            long vi = GetMinMaxIndex(perm, start, end, out var vmin, out var vmax);
            perm.Swap(vi, start); // vp candidate 2 @start
            GetMinMaxIndex(perm, start, end, out var vmin2, out var vmax2);
            if (vmax - vmin > vmax2 - vmin2) perm.Swap(vi, start);
            var vp = m_aget(m_array, perm[start]); // vp @ start
            perm.PermutationQuickMedian(m_array, m_aget,
                    (v0, v1) => m_dist(v0, vp).CompareTo(m_dist(v1, vp)),
                    start + 1, end, mid);
            perm.Swap(mid, start); // vp @ mid, median point @ start
            m_perm[top] = perm[mid];
            m_lmax[top] = m_dist(vp, m_aget(m_array, perm[start])) + m_eps;
            m_rmin[top] = GetMin(perm, mid + 1, end, vp) - m_eps;
            if (start < mid)
                Balance(perm, 2 * top + 1, left / 2, row, start, mid);
            ++mid;
            if (mid < end)
                Balance(perm, 2 * top + 2, left / 2, row - left, mid, end);
        }

        #endregion

        #region Class ClosestToPointQuery

        private class ClosestToPointQuery
        {
            public TPoint Point;
            public double MaxDist;
            public int MaxCount;
            public List<IndexDist<double>> List;
            public bool DynamicSize;

            public ClosestToPointQuery(TPoint query, double maxDistance, int maxCount,
                           List<IndexDist<double>> list)
            {
                Point = query;
                MaxDist = maxDistance;
                MaxCount = maxCount;
                DynamicSize = maxCount == 0;
                List = list;
            }
        }

        #endregion

        #region Properties

        public PointVpTreeDData Data
        {
            get
            {
                return new PointVpTreeDData
                {
                    PermArray = m_perm,
                    LMaxArray = m_lmax,
                    RMinArray = m_rmin
                };
            }
        }

        #endregion

        #region Operations

        private static List<IndexDist<double>> StaticCreateList(int maxCount)
        {
            if (maxCount > 0)
                return new List<IndexDist<double>>(maxCount + 1);
            else
                return [];
        }

        /// <summary>
        /// Create a heap to use in multiple GetClosest queries.
        /// </summary>
        /// <param name="maxCount"></param>
        /// <returns></returns>
        public List<IndexDist<double>> CreateList(int maxCount)
        {
            return StaticCreateList(maxCount);
        }

        /// <summary>
        /// Query for at most maxCount points within maxDistance around
        /// the supplied query point. Note that at least one of the two
        /// criteria must be set, otherwise all points in the vp-tree
        /// will be retrieved.The resulting points are inserted into
        /// the supplied list of index/distance structs, which is first
        /// cleared. If a fixed maximum count is supplied, the list will
        /// be in heap order with the point with the largest distance from
        /// the query point on top.
        /// </summary>
        public void GetClosest(
                TPoint query, double maxDistance, int maxCount,
                List<IndexDist<double>> list)
        {
            list.Clear();
            var q = new ClosestToPointQuery(query, maxDistance, maxCount, list);
            GetClosest(q, 0);
        }

        /// <summary>
        /// Query for at most maxCount points within maxDistance around
        /// the supplied query point. Note that at least one of the two
        /// criteria must be set, otherwise all points in the vp-tree
        /// will be retrieved.The resulting points are returned in a
        /// list of index/distance structs. If a fixed maximum count is
        /// supplied, the list will be in heap order with the point with
        /// the largest distance from the query point on top.
        /// </summary>
        public List<IndexDist<double>> GetClosest(
                TPoint query, double maxDistance, int maxCount)
        {
            var list = StaticCreateList(maxCount);
            var q = new ClosestToPointQuery(query, maxDistance, maxCount, list);
            GetClosest(q, 0);
            return q.List;
        }

        /// <summary>
        /// Get the single closest point from the set.
        /// </summary>
        public IndexDist<double> GetClosest(TPoint query)
        {
            return GetClosest(query, double.MaxValue, 1)[0];
        }

        private void GetAllList(ClosestToPointQuery q, long top)
        {
            q.List.Add(new IndexDist<double>(m_perm[top], double.MinValue));
            long t1 = 2 * top + 1; if (t1 >= m_size) return;
            GetAllList(q, t1);
            long t2 = t1 + 1; if (t2 >= m_size) return;
            GetAllList(q, t2);
        }

        private void GetClosest(ClosestToPointQuery q, long top)
        {
            long index = m_perm[top];
            var vp = m_aget(m_array, index);
            var dist = m_dist(q.Point, vp);
            long t1 = 2 * top + 1;
            if (dist <= q.MaxDist)
            {
                if (q.DynamicSize)
                {
                    q.List.Add(new IndexDist<double>(index, dist));
                    if (t1 >= m_size) return;
                    if (dist + m_lmax[top] < q.MaxDist)
                    {
                        GetAllList(q, t1);
                        long t2 = t1 + 1; if (t2 >= m_size) return;
                        GetClosest(q, t2);
                        return;
                    }
                }
                else
                {
                    q.List.HeapDescendingEnqueue(new IndexDist<double>(index, dist));
                    if (q.List.Count > q.MaxCount)
                    {
                        q.List.HeapDescendingDequeue();
                        q.MaxDist = q.List[0].Dist;
                    }
                    if (t1 >= m_size) return;
                }
            }
            else if (t1 >= m_size) return;
            var splitDist = 0.5 * (m_lmax[top] + m_rmin[top]);
            if (dist < splitDist)
            {
                if (dist - q.MaxDist <= m_lmax[top])
                    GetClosest(q, t1);
                if (dist + q.MaxDist < m_rmin[top]) return;
                long t2 = t1 + 1; if (t2 >= m_size) return;
                GetClosest(q, t2);
            }
            else
            {
                long t2 = t1 + 1;
                if (t2 < m_size
                    && dist + q.MaxDist >= m_rmin[top])
                    GetClosest(q, t2);
                if (dist - q.MaxDist > m_lmax[top]) return;
                GetClosest(q, t1);
            }
        }

        #endregion
    }

    #endregion

    public static class PointKdTreeExtensions
    {
        #region Matrix<float> Trees

        public static PointKdTreeF<Matrix<float>, Vector<float>> CreateKdTree(
                this Matrix<float> array, Metric metric, float absoluteEps)
            => metric switch
            {
                Metric.Manhattan => array.CreateKdTreeDist1(absoluteEps),
                Metric.Euclidean => array.CreateKdTreeDist2(absoluteEps),
                Metric.Maximum => array.CreateKdTreeDistMax(absoluteEps),
                _ => throw new ArgumentException()
            };

        public static PointKdTreeF<Matrix<float>, Vector<float>> CreateKdTreeDist1(
                this Matrix<float> array, float absoluteEps)
        {
            return array.CreateKdTree((a, b) => a.Dist1(b),
                                      (dim, a, b) => b - a, absoluteEps);
        }

        public static PointKdTreeF<Matrix<float>, Vector<float>> CreateKdTreeDist2(
                this Matrix<float> array, float absoluteEps)
        {
            return array.CreateKdTree((a, b) => a.Dist2(b),
                                      (dim, a, b) => b - a, absoluteEps);
        }

        public static PointKdTreeF<Matrix<float>, Vector<float>> CreateKdTreeDist2Squared(
                this Matrix<float> array, float absoluteEps)
        {
            return array.CreateKdTree((a, b) => a.Dist2Squared(b),
                                      (dim, a, b) => (float)Fun.Square(b - a), absoluteEps);
        }

        public static PointKdTreeF<Matrix<float>, Vector<float>> CreateKdTreeDistMax(
                this Matrix<float> array, float absoluteEps)
        {
            return array.CreateKdTree((a, b) => a.DistMax(b),
                                      (dim, a, b) => b - a, absoluteEps);
        }

        public static PointKdTreeF<Matrix<float>, Vector<float>> CreateKdTree(
                this Matrix<float> array,
                Func<Vector<float>, Vector<float>, float> distanceFun,
                Func<long, float, float, float> dimMaxDistanceFun,
                float absoluteEps)
        {
            return new PointKdTreeF<Matrix<float>, Vector<float>>(
                    array.Dim.Y, array.Dim.X, array,
                    (m, i) => m.Col(i), (v, i) => v[i],
                    distanceFun, dimMaxDistanceFun,
                    absoluteEps);
        }

        public static PointKdTreeF<Matrix<float>, Vector<float>> CreateKdTree(
                this Matrix<float> array, Metric metric, float absoluteEps,
                PointKdTreeFData data)
            => metric switch
            {
                Metric.Manhattan => array.CreateKdTreeDist1(absoluteEps, data),
                Metric.Euclidean => array.CreateKdTreeDist2(absoluteEps, data),
                Metric.Maximum => array.CreateKdTreeDistMax(absoluteEps, data),
                _ => throw new ArgumentException()
            };

        public static PointKdTreeF<Matrix<float>, Vector<float>> CreateKdTreeDist1(
                this Matrix<float> array, float absoluteEps, PointKdTreeFData data)
        {
            return array.CreateKdTree((a, b) => a.InnerProduct(b, (x0, x1) => Fun.Abs(x1 - x0), 0.0f, (s, p) => s + p),
                                      (dim, a, b) => b - a, absoluteEps, data);
        }

        public static PointKdTreeF<Matrix<float>, Vector<float>> CreateKdTreeDist2(
                this Matrix<float> array, float absoluteEps, PointKdTreeFData data)
        {
            return array.CreateKdTree((a, b) => a.Dist2(b),
                                      (dim, a, b) => b - a, absoluteEps, data);
        }

        public static PointKdTreeF<Matrix<float>, Vector<float>> CreateKdTreeDist2Squared(
                this Matrix<float> array, float absoluteEps, PointKdTreeFData data)
        {
            return array.CreateKdTree((a, b) => a.Dist2Squared(b),
                                      (dim, a, b) => (float)Fun.Square(b - a), absoluteEps, data);
        }

        public static PointKdTreeF<Matrix<float>, Vector<float>> CreateKdTreeDistMax(
                this Matrix<float> array, float absoluteEps, PointKdTreeFData data)
        {
            return array.CreateKdTree((a, b) => a.DistMax(b),
                                      (dim, a, b) => b - a, absoluteEps, data);
        }

        public static PointKdTreeF<Matrix<float>, Vector<float>> CreateKdTree(
                this Matrix<float> array,
                Func<Vector<float>, Vector<float>, float> distanceFun,
                Func<long, float, float, float> dimMaxDistanceFun,
                float absoluteEps, PointKdTreeFData data)
        {
            return new PointKdTreeF<Matrix<float>, Vector<float>>(
                    array.Dim.Y, array.Dim.X, array,
                    (m, i) => m.Col(i), (v, i) => v[i],
                    distanceFun, dimMaxDistanceFun,
                    absoluteEps, data);
        }

        #endregion

        #region float[] Trees

        public static PointKdTreeF<float[], float> CreateKdTree(
                this float[] array, Metric metric, float absoluteEps)
        {
            return array.CreateKdTreeDist1(absoluteEps);
        }

        public static PointKdTreeF<float[], float> CreateKdTreeDist1(
                this float[] array, float absoluteEps)
        {
            return array.CreateKdTree((a, b) => Fun.Abs(b - a),
                                      (dim, a, b) => b - a, absoluteEps);
        }

        public static PointKdTreeF<float[], float> CreateKdTreeDist2(
                this float[] array, float absoluteEps)
        {
            return array.CreateKdTree((a, b) => Fun.Abs(b - a),
                                      (dim, a, b) => b - a, absoluteEps);
        }

        public static PointKdTreeF<float[], float> CreateKdTreeDistMax(
                this float[] array, float absoluteEps)
        {
            return array.CreateKdTree((a, b) => Fun.Abs(b - a),
                                      (dim, a, b) => b - a, absoluteEps);
        }

        public static PointKdTreeF<float[], float> CreateKdTree(
                this float[] array,
                Func<float, float, float> distanceFun,
                Func<long, float, float, float> dimMaxDistanceFun,
                float absoluteEps)
        {
            return new PointKdTreeF<float[], float>(
                    1, array.LongLength, array,
                    (a, ai) => a[ai], (v, vi) => v,
                    distanceFun, dimMaxDistanceFun,
                    absoluteEps);
        }

        #endregion

        #region V2f[]  Trees

        public static PointKdTreeF<V2f[], V2f> CreateKdTree(
                this V2f[] array, Metric metric, float absoluteEps)
         => metric switch
            {
                Metric.Manhattan => array.CreateKdTreeDist1(absoluteEps),
                Metric.Euclidean => array.CreateKdTreeDist2(absoluteEps),
                Metric.Maximum => array.CreateKdTreeDistMax(absoluteEps),
                _ => throw new ArgumentException()
            };

        public static PointKdTreeF<V2f[], V2f> CreateKdTreeDist1(
                this V2f[] array, float absoluteEps)
        {
            return array.CreateKdTree(Vec.Distance1, (dim, a, b) => b - a,
                                       absoluteEps);
        }

        public static PointKdTreeF<V2f[], V2f> CreateKdTreeDist2(
                this V2f[] array, float absoluteEps)
        {
            return array.CreateKdTree(Vec.Distance, (dim, a, b) => b - a,
                                       absoluteEps);
        }

        public static PointKdTreeF<V2f[], V2f> CreateKdTreeDist2Squared(
                this V2f[] array, float absoluteEps)
        {
            return array.CreateKdTree((a, b) => (float)Vec.DistanceSquared(a, b),
                                          (dim, a, b) => (float)Fun.Square(b - a), absoluteEps);
        }

        public static PointKdTreeF<V2f[], V2f> CreateKdTreeDistMax(
                this V2f[] array, float absoluteEps)
        {
            return array.CreateKdTree(Vec.DistanceMax, (dim, a, b) => b - a,
                                       absoluteEps);
        }

        public static PointKdTreeF<V2f[], V2f> CreateKdTreeDistDotProduct(
                this V2f[] array, float absoluteEps)
        {
            return array.CreateKdTree(
                (a, b) => (float)1.0 - Fun.Abs(Vec.Dot(a, b)),
                (dim, a, b) => 1,
                absoluteEps);
        }

        public static PointKdTreeF<V2f[], V2f> CreateKdTree(
                this V2f[] array,
                Func<V2f, V2f, float> distanceFun,
                Func<long, float, float, float> dimMaxDistanceFun,
                float absoluteEps)
        {
            return new PointKdTreeF<V2f[], V2f>(
                    2, array.Length, array,
                    (a, ai) => a[ai], V2f.LongGetter, 
                    distanceFun, dimMaxDistanceFun,
                    absoluteEps);
        }

        #endregion

        #region V3f[]  Trees

        public static PointKdTreeF<V3f[], V3f> CreateKdTree(
                this V3f[] array, Metric metric, float absoluteEps)
         => metric switch
            {
                Metric.Manhattan => array.CreateKdTreeDist1(absoluteEps),
                Metric.Euclidean => array.CreateKdTreeDist2(absoluteEps),
                Metric.Maximum => array.CreateKdTreeDistMax(absoluteEps),
                _ => throw new ArgumentException()
            };

        public static PointKdTreeF<V3f[], V3f> CreateKdTreeDist1(
                this V3f[] array, float absoluteEps)
        {
            return array.CreateKdTree(Vec.Distance1, (dim, a, b) => b - a,
                                       absoluteEps);
        }

        public static PointKdTreeF<V3f[], V3f> CreateKdTreeDist2(
                this V3f[] array, float absoluteEps)
        {
            return array.CreateKdTree(Vec.Distance, (dim, a, b) => b - a,
                                       absoluteEps);
        }

        public static PointKdTreeF<V3f[], V3f> CreateKdTreeDist2Squared(
                this V3f[] array, float absoluteEps)
        {
            return array.CreateKdTree((a, b) => (float)Vec.DistanceSquared(a, b),
                                          (dim, a, b) => (float)Fun.Square(b - a), absoluteEps);
        }

        public static PointKdTreeF<V3f[], V3f> CreateKdTreeDistMax(
                this V3f[] array, float absoluteEps)
        {
            return array.CreateKdTree(Vec.DistanceMax, (dim, a, b) => b - a,
                                       absoluteEps);
        }

        public static PointKdTreeF<V3f[], V3f> CreateKdTreeDistDotProduct(
                this V3f[] array, float absoluteEps)
        {
            return array.CreateKdTree(
                (a, b) => (float)1.0 - Fun.Abs(Vec.Dot(a, b)),
                (dim, a, b) => 1,
                absoluteEps);
        }

        public static PointKdTreeF<V3f[], V3f> CreateKdTree(
                this V3f[] array,
                Func<V3f, V3f, float> distanceFun,
                Func<long, float, float, float> dimMaxDistanceFun,
                float absoluteEps)
        {
            return new PointKdTreeF<V3f[], V3f>(
                    3, array.Length, array,
                    (a, ai) => a[ai], V3f.LongGetter, 
                    distanceFun, dimMaxDistanceFun,
                    absoluteEps);
        }

        #endregion

        #region V4f[]  Trees

        public static PointKdTreeF<V4f[], V4f> CreateKdTree(
                this V4f[] array, Metric metric, float absoluteEps)
         => metric switch
            {
                Metric.Manhattan => array.CreateKdTreeDist1(absoluteEps),
                Metric.Euclidean => array.CreateKdTreeDist2(absoluteEps),
                Metric.Maximum => array.CreateKdTreeDistMax(absoluteEps),
                _ => throw new ArgumentException()
            };

        public static PointKdTreeF<V4f[], V4f> CreateKdTreeDist1(
                this V4f[] array, float absoluteEps)
        {
            return array.CreateKdTree(Vec.Distance1, (dim, a, b) => b - a,
                                       absoluteEps);
        }

        public static PointKdTreeF<V4f[], V4f> CreateKdTreeDist2(
                this V4f[] array, float absoluteEps)
        {
            return array.CreateKdTree(Vec.Distance, (dim, a, b) => b - a,
                                       absoluteEps);
        }

        public static PointKdTreeF<V4f[], V4f> CreateKdTreeDist2Squared(
                this V4f[] array, float absoluteEps)
        {
            return array.CreateKdTree((a, b) => (float)Vec.DistanceSquared(a, b),
                                          (dim, a, b) => (float)Fun.Square(b - a), absoluteEps);
        }

        public static PointKdTreeF<V4f[], V4f> CreateKdTreeDistMax(
                this V4f[] array, float absoluteEps)
        {
            return array.CreateKdTree(Vec.DistanceMax, (dim, a, b) => b - a,
                                       absoluteEps);
        }

        public static PointKdTreeF<V4f[], V4f> CreateKdTreeDistDotProduct(
                this V4f[] array, float absoluteEps)
        {
            return array.CreateKdTree(
                (a, b) => (float)1.0 - Fun.Abs(Vec.Dot(a, b)),
                (dim, a, b) => 1,
                absoluteEps);
        }

        public static PointKdTreeF<V4f[], V4f> CreateKdTree(
                this V4f[] array,
                Func<V4f, V4f, float> distanceFun,
                Func<long, float, float, float> dimMaxDistanceFun,
                float absoluteEps)
        {
            return new PointKdTreeF<V4f[], V4f>(
                    4, array.Length, array,
                    (a, ai) => a[ai], V4f.LongGetter, 
                    distanceFun, dimMaxDistanceFun,
                    absoluteEps);
        }

        #endregion

        #region Matrix<double> Trees

        public static PointKdTreeD<Matrix<double>, Vector<double>> CreateKdTree(
                this Matrix<double> array, Metric metric, double absoluteEps)
            => metric switch
            {
                Metric.Manhattan => array.CreateKdTreeDist1(absoluteEps),
                Metric.Euclidean => array.CreateKdTreeDist2(absoluteEps),
                Metric.Maximum => array.CreateKdTreeDistMax(absoluteEps),
                _ => throw new ArgumentException()
            };

        public static PointKdTreeD<Matrix<double>, Vector<double>> CreateKdTreeDist1(
                this Matrix<double> array, double absoluteEps)
        {
            return array.CreateKdTree((a, b) => a.Dist1(b),
                                      (dim, a, b) => b - a, absoluteEps);
        }

        public static PointKdTreeD<Matrix<double>, Vector<double>> CreateKdTreeDist2(
                this Matrix<double> array, double absoluteEps)
        {
            return array.CreateKdTree((a, b) => a.Dist2(b),
                                      (dim, a, b) => b - a, absoluteEps);
        }

        public static PointKdTreeD<Matrix<double>, Vector<double>> CreateKdTreeDist2Squared(
                this Matrix<double> array, double absoluteEps)
        {
            return array.CreateKdTree((a, b) => a.Dist2Squared(b),
                                      (dim, a, b) => Fun.Square(b - a), absoluteEps);
        }

        public static PointKdTreeD<Matrix<double>, Vector<double>> CreateKdTreeDistMax(
                this Matrix<double> array, double absoluteEps)
        {
            return array.CreateKdTree((a, b) => a.DistMax(b),
                                      (dim, a, b) => b - a, absoluteEps);
        }

        public static PointKdTreeD<Matrix<double>, Vector<double>> CreateKdTree(
                this Matrix<double> array,
                Func<Vector<double>, Vector<double>, double> distanceFun,
                Func<long, double, double, double> dimMaxDistanceFun,
                double absoluteEps)
        {
            return new PointKdTreeD<Matrix<double>, Vector<double>>(
                    array.Dim.Y, array.Dim.X, array,
                    (m, i) => m.Col(i), (v, i) => v[i],
                    distanceFun, dimMaxDistanceFun,
                    absoluteEps);
        }

        public static PointKdTreeD<Matrix<double>, Vector<double>> CreateKdTree(
                this Matrix<double> array, Metric metric, double absoluteEps,
                PointKdTreeDData data)
            => metric switch
            {
                Metric.Manhattan => array.CreateKdTreeDist1(absoluteEps, data),
                Metric.Euclidean => array.CreateKdTreeDist2(absoluteEps, data),
                Metric.Maximum => array.CreateKdTreeDistMax(absoluteEps, data),
                _ => throw new ArgumentException()
            };

        public static PointKdTreeD<Matrix<double>, Vector<double>> CreateKdTreeDist1(
                this Matrix<double> array, double absoluteEps, PointKdTreeDData data)
        {
            return array.CreateKdTree((a, b) => a.InnerProduct(b, (x0, x1) => Fun.Abs(x1 - x0), 0.0, (s, p) => s + p),
                                      (dim, a, b) => b - a, absoluteEps, data);
        }

        public static PointKdTreeD<Matrix<double>, Vector<double>> CreateKdTreeDist2(
                this Matrix<double> array, double absoluteEps, PointKdTreeDData data)
        {
            return array.CreateKdTree((a, b) => a.Dist2(b),
                                      (dim, a, b) => b - a, absoluteEps, data);
        }

        public static PointKdTreeD<Matrix<double>, Vector<double>> CreateKdTreeDist2Squared(
                this Matrix<double> array, double absoluteEps, PointKdTreeDData data)
        {
            return array.CreateKdTree((a, b) => a.Dist2Squared(b),
                                      (dim, a, b) => Fun.Square(b - a), absoluteEps, data);
        }

        public static PointKdTreeD<Matrix<double>, Vector<double>> CreateKdTreeDistMax(
                this Matrix<double> array, double absoluteEps, PointKdTreeDData data)
        {
            return array.CreateKdTree((a, b) => a.DistMax(b),
                                      (dim, a, b) => b - a, absoluteEps, data);
        }

        public static PointKdTreeD<Matrix<double>, Vector<double>> CreateKdTree(
                this Matrix<double> array,
                Func<Vector<double>, Vector<double>, double> distanceFun,
                Func<long, double, double, double> dimMaxDistanceFun,
                double absoluteEps, PointKdTreeDData data)
        {
            return new PointKdTreeD<Matrix<double>, Vector<double>>(
                    array.Dim.Y, array.Dim.X, array,
                    (m, i) => m.Col(i), (v, i) => v[i],
                    distanceFun, dimMaxDistanceFun,
                    absoluteEps, data);
        }

        #endregion

        #region double[] Trees

        public static PointKdTreeD<double[], double> CreateKdTree(
                this double[] array, Metric metric, double absoluteEps)
        {
            return array.CreateKdTreeDist1(absoluteEps);
        }

        public static PointKdTreeD<double[], double> CreateKdTreeDist1(
                this double[] array, double absoluteEps)
        {
            return array.CreateKdTree((a, b) => Fun.Abs(b - a),
                                      (dim, a, b) => b - a, absoluteEps);
        }

        public static PointKdTreeD<double[], double> CreateKdTreeDist2(
                this double[] array, double absoluteEps)
        {
            return array.CreateKdTree((a, b) => Fun.Abs(b - a),
                                      (dim, a, b) => b - a, absoluteEps);
        }

        public static PointKdTreeD<double[], double> CreateKdTreeDistMax(
                this double[] array, double absoluteEps)
        {
            return array.CreateKdTree((a, b) => Fun.Abs(b - a),
                                      (dim, a, b) => b - a, absoluteEps);
        }

        public static PointKdTreeD<double[], double> CreateKdTree(
                this double[] array,
                Func<double, double, double> distanceFun,
                Func<long, double, double, double> dimMaxDistanceFun,
                double absoluteEps)
        {
            return new PointKdTreeD<double[], double>(
                    1, array.LongLength, array,
                    (a, ai) => a[ai], (v, vi) => v,
                    distanceFun, dimMaxDistanceFun,
                    absoluteEps);
        }

        #endregion

        #region V2d[]  Trees

        public static PointKdTreeD<V2d[], V2d> CreateKdTree(
                this V2d[] array, Metric metric, double absoluteEps)
         => metric switch
            {
                Metric.Manhattan => array.CreateKdTreeDist1(absoluteEps),
                Metric.Euclidean => array.CreateKdTreeDist2(absoluteEps),
                Metric.Maximum => array.CreateKdTreeDistMax(absoluteEps),
                _ => throw new ArgumentException()
            };

        public static PointKdTreeD<V2d[], V2d> CreateKdTreeDist1(
                this V2d[] array, double absoluteEps)
        {
            return array.CreateKdTree(Vec.Distance1, (dim, a, b) => b - a,
                                       absoluteEps);
        }

        public static PointKdTreeD<V2d[], V2d> CreateKdTreeDist2(
                this V2d[] array, double absoluteEps)
        {
            return array.CreateKdTree(Vec.Distance, (dim, a, b) => b - a,
                                       absoluteEps);
        }

        public static PointKdTreeD<V2d[], V2d> CreateKdTreeDist2Squared(
                this V2d[] array, double absoluteEps)
        {
            return array.CreateKdTree((a, b) => Vec.DistanceSquared(a, b),
                                          (dim, a, b) => Fun.Square(b - a), absoluteEps);
        }

        public static PointKdTreeD<V2d[], V2d> CreateKdTreeDistMax(
                this V2d[] array, double absoluteEps)
        {
            return array.CreateKdTree(Vec.DistanceMax, (dim, a, b) => b - a,
                                       absoluteEps);
        }

        public static PointKdTreeD<V2d[], V2d> CreateKdTreeDistDotProduct(
                this V2d[] array, double absoluteEps)
        {
            return array.CreateKdTree(
                (a, b) => (double)1.0 - Fun.Abs(Vec.Dot(a, b)),
                (dim, a, b) => 1,
                absoluteEps);
        }

        public static PointKdTreeD<V2d[], V2d> CreateKdTree(
                this V2d[] array,
                Func<V2d, V2d, double> distanceFun,
                Func<long, double, double, double> dimMaxDistanceFun,
                double absoluteEps)
        {
            return new PointKdTreeD<V2d[], V2d>(
                    2, array.Length, array,
                    (a, ai) => a[ai], V2d.LongGetter, 
                    distanceFun, dimMaxDistanceFun,
                    absoluteEps);
        }

        #endregion

        #region V3d[]  Trees

        public static PointKdTreeD<V3d[], V3d> CreateKdTree(
                this V3d[] array, Metric metric, double absoluteEps)
         => metric switch
            {
                Metric.Manhattan => array.CreateKdTreeDist1(absoluteEps),
                Metric.Euclidean => array.CreateKdTreeDist2(absoluteEps),
                Metric.Maximum => array.CreateKdTreeDistMax(absoluteEps),
                _ => throw new ArgumentException()
            };

        public static PointKdTreeD<V3d[], V3d> CreateKdTreeDist1(
                this V3d[] array, double absoluteEps)
        {
            return array.CreateKdTree(Vec.Distance1, (dim, a, b) => b - a,
                                       absoluteEps);
        }

        public static PointKdTreeD<V3d[], V3d> CreateKdTreeDist2(
                this V3d[] array, double absoluteEps)
        {
            return array.CreateKdTree(Vec.Distance, (dim, a, b) => b - a,
                                       absoluteEps);
        }

        public static PointKdTreeD<V3d[], V3d> CreateKdTreeDist2Squared(
                this V3d[] array, double absoluteEps)
        {
            return array.CreateKdTree((a, b) => Vec.DistanceSquared(a, b),
                                          (dim, a, b) => Fun.Square(b - a), absoluteEps);
        }

        public static PointKdTreeD<V3d[], V3d> CreateKdTreeDistMax(
                this V3d[] array, double absoluteEps)
        {
            return array.CreateKdTree(Vec.DistanceMax, (dim, a, b) => b - a,
                                       absoluteEps);
        }

        public static PointKdTreeD<V3d[], V3d> CreateKdTreeDistDotProduct(
                this V3d[] array, double absoluteEps)
        {
            return array.CreateKdTree(
                (a, b) => (double)1.0 - Fun.Abs(Vec.Dot(a, b)),
                (dim, a, b) => 1,
                absoluteEps);
        }

        public static PointKdTreeD<V3d[], V3d> CreateKdTree(
                this V3d[] array,
                Func<V3d, V3d, double> distanceFun,
                Func<long, double, double, double> dimMaxDistanceFun,
                double absoluteEps)
        {
            return new PointKdTreeD<V3d[], V3d>(
                    3, array.Length, array,
                    (a, ai) => a[ai], V3d.LongGetter, 
                    distanceFun, dimMaxDistanceFun,
                    absoluteEps);
        }

        #endregion

        #region V4d[]  Trees

        public static PointKdTreeD<V4d[], V4d> CreateKdTree(
                this V4d[] array, Metric metric, double absoluteEps)
         => metric switch
            {
                Metric.Manhattan => array.CreateKdTreeDist1(absoluteEps),
                Metric.Euclidean => array.CreateKdTreeDist2(absoluteEps),
                Metric.Maximum => array.CreateKdTreeDistMax(absoluteEps),
                _ => throw new ArgumentException()
            };

        public static PointKdTreeD<V4d[], V4d> CreateKdTreeDist1(
                this V4d[] array, double absoluteEps)
        {
            return array.CreateKdTree(Vec.Distance1, (dim, a, b) => b - a,
                                       absoluteEps);
        }

        public static PointKdTreeD<V4d[], V4d> CreateKdTreeDist2(
                this V4d[] array, double absoluteEps)
        {
            return array.CreateKdTree(Vec.Distance, (dim, a, b) => b - a,
                                       absoluteEps);
        }

        public static PointKdTreeD<V4d[], V4d> CreateKdTreeDist2Squared(
                this V4d[] array, double absoluteEps)
        {
            return array.CreateKdTree((a, b) => Vec.DistanceSquared(a, b),
                                          (dim, a, b) => Fun.Square(b - a), absoluteEps);
        }

        public static PointKdTreeD<V4d[], V4d> CreateKdTreeDistMax(
                this V4d[] array, double absoluteEps)
        {
            return array.CreateKdTree(Vec.DistanceMax, (dim, a, b) => b - a,
                                       absoluteEps);
        }

        public static PointKdTreeD<V4d[], V4d> CreateKdTreeDistDotProduct(
                this V4d[] array, double absoluteEps)
        {
            return array.CreateKdTree(
                (a, b) => (double)1.0 - Fun.Abs(Vec.Dot(a, b)),
                (dim, a, b) => 1,
                absoluteEps);
        }

        public static PointKdTreeD<V4d[], V4d> CreateKdTree(
                this V4d[] array,
                Func<V4d, V4d, double> distanceFun,
                Func<long, double, double, double> dimMaxDistanceFun,
                double absoluteEps)
        {
            return new PointKdTreeD<V4d[], V4d>(
                    4, array.Length, array,
                    (a, ai) => a[ai], V4d.LongGetter, 
                    distanceFun, dimMaxDistanceFun,
                    absoluteEps);
        }

        #endregion

    }

    public static class PointRkdTreeExtensions
    {
        #region Matrix<float> Trees

        public static PointRkdTreeF<Matrix<float>, Vector<float>> CreateRkdTree(
                this Matrix<float> array, Metric metric, float absoluteEps)
            => metric switch
            {
                Metric.Manhattan => array.CreateRkdTreeDist1(absoluteEps),
                Metric.Euclidean => array.CreateRkdTreeDist2(absoluteEps),
                Metric.Maximum => array.CreateRkdTreeDistMax(absoluteEps),
                _ => throw new ArgumentException()
            };

        public static PointRkdTreeF<Matrix<float>, Vector<float>> CreateRkdTreeDist1(
                this Matrix<float> array, float absoluteEps)
        {
            return array.CreateRkdTree((a, b) => a.Dist1(b),
                                      (dim, a, b) => b - a, absoluteEps);
        }

        public static PointRkdTreeF<Matrix<float>, Vector<float>> CreateRkdTreeDist2(
                this Matrix<float> array, float absoluteEps)
        {
            return array.CreateRkdTree((a, b) => a.Dist2(b),
                                      (dim, a, b) => b - a, absoluteEps);
        }

        public static PointRkdTreeF<Matrix<float>, Vector<float>> CreateRkdTreeDistMax(
                this Matrix<float> array, float absoluteEps)
        {
            return array.CreateRkdTree((a, b) => a.DistMax(b),
                                      (dim, a, b) => b - a, absoluteEps);
        }

        public static PointRkdTreeF<Matrix<float>, Vector<float>> CreateRkdTree(
                this Matrix<float> array,
                Func<Vector<float>, Vector<float>, float> distanceFun,
                Func<long, float, float, float> dimMaxDistanceFun,
                float absoluteEps)
        {
            return new PointRkdTreeF<Matrix<float>, Vector<float>>(
                    array.Dim.Y, array.Dim.X, array,
                    (m, i) => m.Col(i), (v, i) => v[i],
                    distanceFun, dimMaxDistanceFun,
                    null, null, absoluteEps);
        }

        public static PointRkdTreeF<Matrix<float>, Vector<float>> CreateRkdTree(
                this Matrix<float> array, Metric metric, float absoluteEps,
                PointRkdTreeFData data)
            => metric switch
            {
                Metric.Manhattan => array.CreateRkdTreeDist1(absoluteEps, data),
                Metric.Euclidean => array.CreateRkdTreeDist2(absoluteEps, data),
                Metric.Maximum => array.CreateRkdTreeDistMax(absoluteEps, data),
                _ => throw new ArgumentException()
            };

        public static PointRkdTreeF<Matrix<float>, Vector<float>> CreateRkdTreeDist1(
                this Matrix<float> array, float absoluteEps, PointRkdTreeFData data)
        {
            return array.CreateRkdTree((a, b) => a.InnerProduct(b, (x0, x1) => Fun.Abs(x1 - x0), 0.0f, (s, p) => s + p),
                                      (dim, a, b) => b - a, absoluteEps, data);
        }

        public static PointRkdTreeF<Matrix<float>, Vector<float>> CreateRkdTreeDist2(
                this Matrix<float> array, float absoluteEps, PointRkdTreeFData data)
        {
            return array.CreateRkdTree((a, b) => a.Dist2(b),
                                      (dim, a, b) => b - a, absoluteEps, data);
        }

        public static PointRkdTreeF<Matrix<float>, Vector<float>> CreateRkdTreeDistMax(
                this Matrix<float> array, float absoluteEps, PointRkdTreeFData data)
        {
            return array.CreateRkdTree((a, b) => a.DistMax(b),
                                      (dim, a, b) => b - a, absoluteEps, data);
        }

        public static PointRkdTreeF<Matrix<float>, Vector<float>> CreateRkdTree(
                this Matrix<float> array,
                Func<Vector<float>, Vector<float>, float> distanceFun,
                Func<long, float, float, float> dimMaxDistanceFun,
                float absoluteEps, PointRkdTreeFData data)
        {
            return new PointRkdTreeF<Matrix<float>, Vector<float>>(
                    array.Dim.Y, array.Dim.X, array,
                    (m, i) => m.Col(i), (v, i) => v[i],
                    distanceFun, dimMaxDistanceFun,
                    null, null, absoluteEps, data);
        }

        #endregion

        #region float[] Trees

        public static PointRkdTreeF<float[], float> CreateRkdTree(
                this float[] array, Metric metric, float absoluteEps)
        {
            return array.CreateRkdTreeDist1(absoluteEps);
        }

        public static PointRkdTreeF<float[], float> CreateRkdTreeDist1(
                this float[] array, float absoluteEps)
        {
            return array.CreateRkdTree((a, b) => Fun.Abs(b - a),
                                      (dim, a, b) => b - a, absoluteEps);
        }

        public static PointRkdTreeF<float[], float> CreateRkdTreeDist2(
                this float[] array, float absoluteEps)
        {
            return array.CreateRkdTree((a, b) => Fun.Abs(b - a),
                                      (dim, a, b) => b - a, absoluteEps);
        }

        public static PointRkdTreeF<float[], float> CreateRkdTreeDistMax(
                this float[] array, float absoluteEps)
        {
            return array.CreateRkdTree((a, b) => Fun.Abs(b - a),
                                      (dim, a, b) => b - a, absoluteEps);
        }

        public static PointRkdTreeF<float[], float> CreateRkdTree(
                this float[] array,
                Func<float, float, float> distanceFun,
                Func<long, float, float, float> dimMaxDistanceFun,
                float absoluteEps)
        {
            return new PointRkdTreeF<float[], float>(
                    1, array.LongLength, array,
                    (a, ai) => a[ai], (v, vi) => v,
                    distanceFun, dimMaxDistanceFun,
                    null, null, absoluteEps);
        }

        #endregion

        #region V2f[]  Trees

        public static PointRkdTreeF<V2f[], V2f> CreateRkdTree(
                this V2f[] array, Metric metric, float absoluteEps)
         => metric switch
            {
                Metric.Manhattan => array.CreateRkdTreeDist1(absoluteEps),
                Metric.Euclidean => array.CreateRkdTreeDist2(absoluteEps),
                Metric.Maximum => array.CreateRkdTreeDistMax(absoluteEps),
                _ => throw new ArgumentException()
            };

        public static PointRkdTreeF<V2f[], V2f> CreateRkdTreeDist1(
                this V2f[] array, float absoluteEps)
        {
            return array.CreateRkdTree(Vec.Distance1, (dim, a, b) => b - a,
                                       null, null, absoluteEps);
        }

        public static PointRkdTreeF<V2f[], V2f> CreateRkdTreeDist2(
                this V2f[] array, float absoluteEps)
        {
            return array.CreateRkdTree(Vec.Distance, (dim, a, b) => b - a,
                                       Vec.DistanceToLine, Fun.Lerp,
                                       absoluteEps);
        }

        public static PointRkdTreeF<V2f[], V2f> CreateRkdTreeDistMax(
                this V2f[] array, float absoluteEps)
        {
            return array.CreateRkdTree(Vec.DistanceMax, (dim, a, b) => b - a,
                                       null, null, absoluteEps);
        }

        public static PointRkdTreeF<V2f[], V2f> CreateRkdTreeDistDotProduct(
                this V2f[] array, float absoluteEps)
        {
            return array.CreateRkdTree(
                (a, b) => (float)1.0 - Fun.Abs(Vec.Dot(a, b)),
                (dim, a, b) => 1,
                null, null, absoluteEps);
        }

        public static PointRkdTreeF<V2f[], V2f> CreateRkdTree(
                this V2f[] array,
                Func<V2f, V2f, float> distanceFun,
                Func<long, float, float, float> dimMaxDistanceFun,
                Func<V2f, V2f, V2f, float> lineDistFun,
                Func<float, V2f, V2f, V2f> lerpFun,
                float absoluteEps)
        {
            return new PointRkdTreeF<V2f[], V2f>(
                    2, array.Length, array,
                    (a, ai) => a[ai], V2f.LongGetter, 
                    distanceFun, dimMaxDistanceFun,
                    lineDistFun, lerpFun, absoluteEps);
        }

        #endregion

        #region V3f[]  Trees

        public static PointRkdTreeF<V3f[], V3f> CreateRkdTree(
                this V3f[] array, Metric metric, float absoluteEps)
         => metric switch
            {
                Metric.Manhattan => array.CreateRkdTreeDist1(absoluteEps),
                Metric.Euclidean => array.CreateRkdTreeDist2(absoluteEps),
                Metric.Maximum => array.CreateRkdTreeDistMax(absoluteEps),
                _ => throw new ArgumentException()
            };

        public static PointRkdTreeF<V3f[], V3f> CreateRkdTreeDist1(
                this V3f[] array, float absoluteEps)
        {
            return array.CreateRkdTree(Vec.Distance1, (dim, a, b) => b - a,
                                       null, null, absoluteEps);
        }

        public static PointRkdTreeF<V3f[], V3f> CreateRkdTreeDist2(
                this V3f[] array, float absoluteEps)
        {
            return array.CreateRkdTree(Vec.Distance, (dim, a, b) => b - a,
                                       Vec.DistanceToLine, Fun.Lerp,
                                       absoluteEps);
        }

        public static PointRkdTreeF<V3f[], V3f> CreateRkdTreeDistMax(
                this V3f[] array, float absoluteEps)
        {
            return array.CreateRkdTree(Vec.DistanceMax, (dim, a, b) => b - a,
                                       null, null, absoluteEps);
        }

        public static PointRkdTreeF<V3f[], V3f> CreateRkdTreeDistDotProduct(
                this V3f[] array, float absoluteEps)
        {
            return array.CreateRkdTree(
                (a, b) => (float)1.0 - Fun.Abs(Vec.Dot(a, b)),
                (dim, a, b) => 1,
                null, null, absoluteEps);
        }

        public static PointRkdTreeF<V3f[], V3f> CreateRkdTree(
                this V3f[] array,
                Func<V3f, V3f, float> distanceFun,
                Func<long, float, float, float> dimMaxDistanceFun,
                Func<V3f, V3f, V3f, float> lineDistFun,
                Func<float, V3f, V3f, V3f> lerpFun,
                float absoluteEps)
        {
            return new PointRkdTreeF<V3f[], V3f>(
                    3, array.Length, array,
                    (a, ai) => a[ai], V3f.LongGetter, 
                    distanceFun, dimMaxDistanceFun,
                    lineDistFun, lerpFun, absoluteEps);
        }

        #endregion

        #region V4f[]  Trees

        public static PointRkdTreeF<V4f[], V4f> CreateRkdTree(
                this V4f[] array, Metric metric, float absoluteEps)
         => metric switch
            {
                Metric.Manhattan => array.CreateRkdTreeDist1(absoluteEps),
                Metric.Euclidean => array.CreateRkdTreeDist2(absoluteEps),
                Metric.Maximum => array.CreateRkdTreeDistMax(absoluteEps),
                _ => throw new ArgumentException()
            };

        public static PointRkdTreeF<V4f[], V4f> CreateRkdTreeDist1(
                this V4f[] array, float absoluteEps)
        {
            return array.CreateRkdTree(Vec.Distance1, (dim, a, b) => b - a,
                                       null, null, absoluteEps);
        }

        public static PointRkdTreeF<V4f[], V4f> CreateRkdTreeDist2(
                this V4f[] array, float absoluteEps)
        {
            return array.CreateRkdTree(Vec.Distance, (dim, a, b) => b - a,
                                       Vec.DistanceToLine, Fun.Lerp,
                                       absoluteEps);
        }

        public static PointRkdTreeF<V4f[], V4f> CreateRkdTreeDistMax(
                this V4f[] array, float absoluteEps)
        {
            return array.CreateRkdTree(Vec.DistanceMax, (dim, a, b) => b - a,
                                       null, null, absoluteEps);
        }

        public static PointRkdTreeF<V4f[], V4f> CreateRkdTreeDistDotProduct(
                this V4f[] array, float absoluteEps)
        {
            return array.CreateRkdTree(
                (a, b) => (float)1.0 - Fun.Abs(Vec.Dot(a, b)),
                (dim, a, b) => 1,
                null, null, absoluteEps);
        }

        public static PointRkdTreeF<V4f[], V4f> CreateRkdTree(
                this V4f[] array,
                Func<V4f, V4f, float> distanceFun,
                Func<long, float, float, float> dimMaxDistanceFun,
                Func<V4f, V4f, V4f, float> lineDistFun,
                Func<float, V4f, V4f, V4f> lerpFun,
                float absoluteEps)
        {
            return new PointRkdTreeF<V4f[], V4f>(
                    4, array.Length, array,
                    (a, ai) => a[ai], V4f.LongGetter, 
                    distanceFun, dimMaxDistanceFun,
                    lineDistFun, lerpFun, absoluteEps);
        }

        #endregion

        #region Constants
        private readonly static Func<float, float, float> c_dimDistFunF = (a, b) => b - a;

        private readonly static Func<float, float, float>[] c_dimDistFunArrayF =
            [c_dimDistFunF, c_dimDistFunF, c_dimDistFunF, c_dimDistFunF];

        private readonly static Func<float, float, float> c_dimDistConst1FunF = (a, b) => (float)1;

        private readonly static Func<float, float, float>[] c_dimDistConst1FunArrayF =
            [c_dimDistConst1FunF, c_dimDistConst1FunF, c_dimDistConst1FunF, c_dimDistConst1FunF];

        #endregion

        #region V2f[] Selector Trees

        public static PointRkdTreeFSelector<V2f[], V2f> CreateRkdTreeSelector(
                this V2f[] array, Metric metric, float absoluteEps)
         => metric switch
            {
                Metric.Manhattan => array.CreateRkdTreeSelectorDist1(absoluteEps),
                Metric.Euclidean => array.CreateRkdTreeSelectorDist2(absoluteEps),
                Metric.Maximum => array.CreateRkdTreeSelectorDistMax(absoluteEps),
                _ => throw new ArgumentException()
            };

        public static PointRkdTreeFSelector<V2f[], V2f> CreateRkdTreeSelectorDist1(
                this V2f[] array, float absoluteEps)
        {
            return array.CreateRkdTree(Vec.Distance1, c_dimDistFunArrayF,
                                       null, null, absoluteEps);
        }

        public static PointRkdTreeFSelector<V2f[], V2f> CreateRkdTreeSelectorDist2(
                this V2f[] array, float absoluteEps)
        {
            return array.CreateRkdTree(Vec.Distance, c_dimDistFunArrayF,
                                       Vec.DistanceToLine, Fun.Lerp,
                                       absoluteEps);
        }

        public static PointRkdTreeFSelector<V2f[], V2f> CreateRkdTreeSelectorDistMax(
                this V2f[] array, float absoluteEps)
        {
            return array.CreateRkdTree(Vec.DistanceMax, c_dimDistFunArrayF,
                                       null, null, absoluteEps);
        }

        public static PointRkdTreeFSelector<V2f[], V2f> CreateRkdTreeSelectorDistDotProduct(
                this V2f[] array, float absoluteEps)
        {
            return array.CreateRkdTree(
                (a, b) => (float)1.0 - Fun.Abs(Vec.Dot(a, b)),
                c_dimDistConst1FunArrayF,
                null, null, absoluteEps);
        }

        public static PointRkdTreeFSelector<V2f[], V2f> CreateRkdTree(
                this V2f[] array,
                Func<V2f, V2f, float> distanceFun,
                Func<float, float, float>[] dimMaxDistanceFunArray,
                Func<V2f, V2f, V2f, float> lineDistFun,
                Func<float, V2f, V2f, V2f> lerpFun,
                float absoluteEps)
        {
            return new PointRkdTreeFSelector<V2f[], V2f>(
                    2, array.Length, array,
                    (a, ai) => a[ai], V2f.SelectorArray, 
                    distanceFun, dimMaxDistanceFunArray,
                    lineDistFun, lerpFun, absoluteEps);
        }

        #endregion

        #region V3f[] Selector Trees

        public static PointRkdTreeFSelector<V3f[], V3f> CreateRkdTreeSelector(
                this V3f[] array, Metric metric, float absoluteEps)
         => metric switch
            {
                Metric.Manhattan => array.CreateRkdTreeSelectorDist1(absoluteEps),
                Metric.Euclidean => array.CreateRkdTreeSelectorDist2(absoluteEps),
                Metric.Maximum => array.CreateRkdTreeSelectorDistMax(absoluteEps),
                _ => throw new ArgumentException()
            };

        public static PointRkdTreeFSelector<V3f[], V3f> CreateRkdTreeSelectorDist1(
                this V3f[] array, float absoluteEps)
        {
            return array.CreateRkdTree(Vec.Distance1, c_dimDistFunArrayF,
                                       null, null, absoluteEps);
        }

        public static PointRkdTreeFSelector<V3f[], V3f> CreateRkdTreeSelectorDist2(
                this V3f[] array, float absoluteEps)
        {
            return array.CreateRkdTree(Vec.Distance, c_dimDistFunArrayF,
                                       Vec.DistanceToLine, Fun.Lerp,
                                       absoluteEps);
        }

        public static PointRkdTreeFSelector<V3f[], V3f> CreateRkdTreeSelectorDistMax(
                this V3f[] array, float absoluteEps)
        {
            return array.CreateRkdTree(Vec.DistanceMax, c_dimDistFunArrayF,
                                       null, null, absoluteEps);
        }

        public static PointRkdTreeFSelector<V3f[], V3f> CreateRkdTreeSelectorDistDotProduct(
                this V3f[] array, float absoluteEps)
        {
            return array.CreateRkdTree(
                (a, b) => (float)1.0 - Fun.Abs(Vec.Dot(a, b)),
                c_dimDistConst1FunArrayF,
                null, null, absoluteEps);
        }

        public static PointRkdTreeFSelector<V3f[], V3f> CreateRkdTree(
                this V3f[] array,
                Func<V3f, V3f, float> distanceFun,
                Func<float, float, float>[] dimMaxDistanceFunArray,
                Func<V3f, V3f, V3f, float> lineDistFun,
                Func<float, V3f, V3f, V3f> lerpFun,
                float absoluteEps)
        {
            return new PointRkdTreeFSelector<V3f[], V3f>(
                    3, array.Length, array,
                    (a, ai) => a[ai], V3f.SelectorArray, 
                    distanceFun, dimMaxDistanceFunArray,
                    lineDistFun, lerpFun, absoluteEps);
        }

        #endregion

        #region V4f[] Selector Trees

        public static PointRkdTreeFSelector<V4f[], V4f> CreateRkdTreeSelector(
                this V4f[] array, Metric metric, float absoluteEps)
         => metric switch
            {
                Metric.Manhattan => array.CreateRkdTreeSelectorDist1(absoluteEps),
                Metric.Euclidean => array.CreateRkdTreeSelectorDist2(absoluteEps),
                Metric.Maximum => array.CreateRkdTreeSelectorDistMax(absoluteEps),
                _ => throw new ArgumentException()
            };

        public static PointRkdTreeFSelector<V4f[], V4f> CreateRkdTreeSelectorDist1(
                this V4f[] array, float absoluteEps)
        {
            return array.CreateRkdTree(Vec.Distance1, c_dimDistFunArrayF,
                                       null, null, absoluteEps);
        }

        public static PointRkdTreeFSelector<V4f[], V4f> CreateRkdTreeSelectorDist2(
                this V4f[] array, float absoluteEps)
        {
            return array.CreateRkdTree(Vec.Distance, c_dimDistFunArrayF,
                                       Vec.DistanceToLine, Fun.Lerp,
                                       absoluteEps);
        }

        public static PointRkdTreeFSelector<V4f[], V4f> CreateRkdTreeSelectorDistMax(
                this V4f[] array, float absoluteEps)
        {
            return array.CreateRkdTree(Vec.DistanceMax, c_dimDistFunArrayF,
                                       null, null, absoluteEps);
        }

        public static PointRkdTreeFSelector<V4f[], V4f> CreateRkdTreeSelectorDistDotProduct(
                this V4f[] array, float absoluteEps)
        {
            return array.CreateRkdTree(
                (a, b) => (float)1.0 - Fun.Abs(Vec.Dot(a, b)),
                c_dimDistConst1FunArrayF,
                null, null, absoluteEps);
        }

        public static PointRkdTreeFSelector<V4f[], V4f> CreateRkdTree(
                this V4f[] array,
                Func<V4f, V4f, float> distanceFun,
                Func<float, float, float>[] dimMaxDistanceFunArray,
                Func<V4f, V4f, V4f, float> lineDistFun,
                Func<float, V4f, V4f, V4f> lerpFun,
                float absoluteEps)
        {
            return new PointRkdTreeFSelector<V4f[], V4f>(
                    4, array.Length, array,
                    (a, ai) => a[ai], V4f.SelectorArray, 
                    distanceFun, dimMaxDistanceFunArray,
                    lineDistFun, lerpFun, absoluteEps);
        }

        #endregion

        #region Matrix<double> Trees

        public static PointRkdTreeD<Matrix<double>, Vector<double>> CreateRkdTree(
                this Matrix<double> array, Metric metric, double absoluteEps)
            => metric switch
            {
                Metric.Manhattan => array.CreateRkdTreeDist1(absoluteEps),
                Metric.Euclidean => array.CreateRkdTreeDist2(absoluteEps),
                Metric.Maximum => array.CreateRkdTreeDistMax(absoluteEps),
                _ => throw new ArgumentException()
            };

        public static PointRkdTreeD<Matrix<double>, Vector<double>> CreateRkdTreeDist1(
                this Matrix<double> array, double absoluteEps)
        {
            return array.CreateRkdTree((a, b) => a.Dist1(b),
                                      (dim, a, b) => b - a, absoluteEps);
        }

        public static PointRkdTreeD<Matrix<double>, Vector<double>> CreateRkdTreeDist2(
                this Matrix<double> array, double absoluteEps)
        {
            return array.CreateRkdTree((a, b) => a.Dist2(b),
                                      (dim, a, b) => b - a, absoluteEps);
        }

        public static PointRkdTreeD<Matrix<double>, Vector<double>> CreateRkdTreeDistMax(
                this Matrix<double> array, double absoluteEps)
        {
            return array.CreateRkdTree((a, b) => a.DistMax(b),
                                      (dim, a, b) => b - a, absoluteEps);
        }

        public static PointRkdTreeD<Matrix<double>, Vector<double>> CreateRkdTree(
                this Matrix<double> array,
                Func<Vector<double>, Vector<double>, double> distanceFun,
                Func<long, double, double, double> dimMaxDistanceFun,
                double absoluteEps)
        {
            return new PointRkdTreeD<Matrix<double>, Vector<double>>(
                    array.Dim.Y, array.Dim.X, array,
                    (m, i) => m.Col(i), (v, i) => v[i],
                    distanceFun, dimMaxDistanceFun,
                    null, null, absoluteEps);
        }

        public static PointRkdTreeD<Matrix<double>, Vector<double>> CreateRkdTree(
                this Matrix<double> array, Metric metric, double absoluteEps,
                PointRkdTreeDData data)
            => metric switch
            {
                Metric.Manhattan => array.CreateRkdTreeDist1(absoluteEps, data),
                Metric.Euclidean => array.CreateRkdTreeDist2(absoluteEps, data),
                Metric.Maximum => array.CreateRkdTreeDistMax(absoluteEps, data),
                _ => throw new ArgumentException()
            };

        public static PointRkdTreeD<Matrix<double>, Vector<double>> CreateRkdTreeDist1(
                this Matrix<double> array, double absoluteEps, PointRkdTreeDData data)
        {
            return array.CreateRkdTree((a, b) => a.InnerProduct(b, (x0, x1) => Fun.Abs(x1 - x0), 0.0, (s, p) => s + p),
                                      (dim, a, b) => b - a, absoluteEps, data);
        }

        public static PointRkdTreeD<Matrix<double>, Vector<double>> CreateRkdTreeDist2(
                this Matrix<double> array, double absoluteEps, PointRkdTreeDData data)
        {
            return array.CreateRkdTree((a, b) => a.Dist2(b),
                                      (dim, a, b) => b - a, absoluteEps, data);
        }

        public static PointRkdTreeD<Matrix<double>, Vector<double>> CreateRkdTreeDistMax(
                this Matrix<double> array, double absoluteEps, PointRkdTreeDData data)
        {
            return array.CreateRkdTree((a, b) => a.DistMax(b),
                                      (dim, a, b) => b - a, absoluteEps, data);
        }

        public static PointRkdTreeD<Matrix<double>, Vector<double>> CreateRkdTree(
                this Matrix<double> array,
                Func<Vector<double>, Vector<double>, double> distanceFun,
                Func<long, double, double, double> dimMaxDistanceFun,
                double absoluteEps, PointRkdTreeDData data)
        {
            return new PointRkdTreeD<Matrix<double>, Vector<double>>(
                    array.Dim.Y, array.Dim.X, array,
                    (m, i) => m.Col(i), (v, i) => v[i],
                    distanceFun, dimMaxDistanceFun,
                    null, null, absoluteEps, data);
        }

        #endregion

        #region double[] Trees

        public static PointRkdTreeD<double[], double> CreateRkdTree(
                this double[] array, Metric metric, double absoluteEps)
        {
            return array.CreateRkdTreeDist1(absoluteEps);
        }

        public static PointRkdTreeD<double[], double> CreateRkdTreeDist1(
                this double[] array, double absoluteEps)
        {
            return array.CreateRkdTree((a, b) => Fun.Abs(b - a),
                                      (dim, a, b) => b - a, absoluteEps);
        }

        public static PointRkdTreeD<double[], double> CreateRkdTreeDist2(
                this double[] array, double absoluteEps)
        {
            return array.CreateRkdTree((a, b) => Fun.Abs(b - a),
                                      (dim, a, b) => b - a, absoluteEps);
        }

        public static PointRkdTreeD<double[], double> CreateRkdTreeDistMax(
                this double[] array, double absoluteEps)
        {
            return array.CreateRkdTree((a, b) => Fun.Abs(b - a),
                                      (dim, a, b) => b - a, absoluteEps);
        }

        public static PointRkdTreeD<double[], double> CreateRkdTree(
                this double[] array,
                Func<double, double, double> distanceFun,
                Func<long, double, double, double> dimMaxDistanceFun,
                double absoluteEps)
        {
            return new PointRkdTreeD<double[], double>(
                    1, array.LongLength, array,
                    (a, ai) => a[ai], (v, vi) => v,
                    distanceFun, dimMaxDistanceFun,
                    null, null, absoluteEps);
        }

        #endregion

        #region V2d[]  Trees

        public static PointRkdTreeD<V2d[], V2d> CreateRkdTree(
                this V2d[] array, Metric metric, double absoluteEps)
         => metric switch
            {
                Metric.Manhattan => array.CreateRkdTreeDist1(absoluteEps),
                Metric.Euclidean => array.CreateRkdTreeDist2(absoluteEps),
                Metric.Maximum => array.CreateRkdTreeDistMax(absoluteEps),
                _ => throw new ArgumentException()
            };

        public static PointRkdTreeD<V2d[], V2d> CreateRkdTreeDist1(
                this V2d[] array, double absoluteEps)
        {
            return array.CreateRkdTree(Vec.Distance1, (dim, a, b) => b - a,
                                       null, null, absoluteEps);
        }

        public static PointRkdTreeD<V2d[], V2d> CreateRkdTreeDist2(
                this V2d[] array, double absoluteEps)
        {
            return array.CreateRkdTree(Vec.Distance, (dim, a, b) => b - a,
                                       Vec.DistanceToLine, Fun.Lerp,
                                       absoluteEps);
        }

        public static PointRkdTreeD<V2d[], V2d> CreateRkdTreeDistMax(
                this V2d[] array, double absoluteEps)
        {
            return array.CreateRkdTree(Vec.DistanceMax, (dim, a, b) => b - a,
                                       null, null, absoluteEps);
        }

        public static PointRkdTreeD<V2d[], V2d> CreateRkdTreeDistDotProduct(
                this V2d[] array, double absoluteEps)
        {
            return array.CreateRkdTree(
                (a, b) => (double)1.0 - Fun.Abs(Vec.Dot(a, b)),
                (dim, a, b) => 1,
                null, null, absoluteEps);
        }

        public static PointRkdTreeD<V2d[], V2d> CreateRkdTree(
                this V2d[] array,
                Func<V2d, V2d, double> distanceFun,
                Func<long, double, double, double> dimMaxDistanceFun,
                Func<V2d, V2d, V2d, double> lineDistFun,
                Func<double, V2d, V2d, V2d> lerpFun,
                double absoluteEps)
        {
            return new PointRkdTreeD<V2d[], V2d>(
                    2, array.Length, array,
                    (a, ai) => a[ai], V2d.LongGetter, 
                    distanceFun, dimMaxDistanceFun,
                    lineDistFun, lerpFun, absoluteEps);
        }

        #endregion

        #region V3d[]  Trees

        public static PointRkdTreeD<V3d[], V3d> CreateRkdTree(
                this V3d[] array, Metric metric, double absoluteEps)
         => metric switch
            {
                Metric.Manhattan => array.CreateRkdTreeDist1(absoluteEps),
                Metric.Euclidean => array.CreateRkdTreeDist2(absoluteEps),
                Metric.Maximum => array.CreateRkdTreeDistMax(absoluteEps),
                _ => throw new ArgumentException()
            };

        public static PointRkdTreeD<V3d[], V3d> CreateRkdTreeDist1(
                this V3d[] array, double absoluteEps)
        {
            return array.CreateRkdTree(Vec.Distance1, (dim, a, b) => b - a,
                                       null, null, absoluteEps);
        }

        public static PointRkdTreeD<V3d[], V3d> CreateRkdTreeDist2(
                this V3d[] array, double absoluteEps)
        {
            return array.CreateRkdTree(Vec.Distance, (dim, a, b) => b - a,
                                       Vec.DistanceToLine, Fun.Lerp,
                                       absoluteEps);
        }

        public static PointRkdTreeD<V3d[], V3d> CreateRkdTreeDistMax(
                this V3d[] array, double absoluteEps)
        {
            return array.CreateRkdTree(Vec.DistanceMax, (dim, a, b) => b - a,
                                       null, null, absoluteEps);
        }

        public static PointRkdTreeD<V3d[], V3d> CreateRkdTreeDistDotProduct(
                this V3d[] array, double absoluteEps)
        {
            return array.CreateRkdTree(
                (a, b) => (double)1.0 - Fun.Abs(Vec.Dot(a, b)),
                (dim, a, b) => 1,
                null, null, absoluteEps);
        }

        public static PointRkdTreeD<V3d[], V3d> CreateRkdTree(
                this V3d[] array,
                Func<V3d, V3d, double> distanceFun,
                Func<long, double, double, double> dimMaxDistanceFun,
                Func<V3d, V3d, V3d, double> lineDistFun,
                Func<double, V3d, V3d, V3d> lerpFun,
                double absoluteEps)
        {
            return new PointRkdTreeD<V3d[], V3d>(
                    3, array.Length, array,
                    (a, ai) => a[ai], V3d.LongGetter, 
                    distanceFun, dimMaxDistanceFun,
                    lineDistFun, lerpFun, absoluteEps);
        }

        #endregion

        #region V4d[]  Trees

        public static PointRkdTreeD<V4d[], V4d> CreateRkdTree(
                this V4d[] array, Metric metric, double absoluteEps)
         => metric switch
            {
                Metric.Manhattan => array.CreateRkdTreeDist1(absoluteEps),
                Metric.Euclidean => array.CreateRkdTreeDist2(absoluteEps),
                Metric.Maximum => array.CreateRkdTreeDistMax(absoluteEps),
                _ => throw new ArgumentException()
            };

        public static PointRkdTreeD<V4d[], V4d> CreateRkdTreeDist1(
                this V4d[] array, double absoluteEps)
        {
            return array.CreateRkdTree(Vec.Distance1, (dim, a, b) => b - a,
                                       null, null, absoluteEps);
        }

        public static PointRkdTreeD<V4d[], V4d> CreateRkdTreeDist2(
                this V4d[] array, double absoluteEps)
        {
            return array.CreateRkdTree(Vec.Distance, (dim, a, b) => b - a,
                                       Vec.DistanceToLine, Fun.Lerp,
                                       absoluteEps);
        }

        public static PointRkdTreeD<V4d[], V4d> CreateRkdTreeDistMax(
                this V4d[] array, double absoluteEps)
        {
            return array.CreateRkdTree(Vec.DistanceMax, (dim, a, b) => b - a,
                                       null, null, absoluteEps);
        }

        public static PointRkdTreeD<V4d[], V4d> CreateRkdTreeDistDotProduct(
                this V4d[] array, double absoluteEps)
        {
            return array.CreateRkdTree(
                (a, b) => (double)1.0 - Fun.Abs(Vec.Dot(a, b)),
                (dim, a, b) => 1,
                null, null, absoluteEps);
        }

        public static PointRkdTreeD<V4d[], V4d> CreateRkdTree(
                this V4d[] array,
                Func<V4d, V4d, double> distanceFun,
                Func<long, double, double, double> dimMaxDistanceFun,
                Func<V4d, V4d, V4d, double> lineDistFun,
                Func<double, V4d, V4d, V4d> lerpFun,
                double absoluteEps)
        {
            return new PointRkdTreeD<V4d[], V4d>(
                    4, array.Length, array,
                    (a, ai) => a[ai], V4d.LongGetter, 
                    distanceFun, dimMaxDistanceFun,
                    lineDistFun, lerpFun, absoluteEps);
        }

        #endregion

        #region Constants
        private readonly static Func<double, double, double> c_dimDistFunD = (a, b) => b - a;

        private readonly static Func<double, double, double>[] c_dimDistFunArrayD =
            [c_dimDistFunD, c_dimDistFunD, c_dimDistFunD, c_dimDistFunD];

        private readonly static Func<double, double, double> c_dimDistConst1FunD = (a, b) => (double)1;

        private readonly static Func<double, double, double>[] c_dimDistConst1FunArrayD =
            [c_dimDistConst1FunD, c_dimDistConst1FunD, c_dimDistConst1FunD, c_dimDistConst1FunD];

        #endregion

        #region V2d[] Selector Trees

        public static PointRkdTreeDSelector<V2d[], V2d> CreateRkdTreeSelector(
                this V2d[] array, Metric metric, double absoluteEps)
         => metric switch
            {
                Metric.Manhattan => array.CreateRkdTreeSelectorDist1(absoluteEps),
                Metric.Euclidean => array.CreateRkdTreeSelectorDist2(absoluteEps),
                Metric.Maximum => array.CreateRkdTreeSelectorDistMax(absoluteEps),
                _ => throw new ArgumentException()
            };

        public static PointRkdTreeDSelector<V2d[], V2d> CreateRkdTreeSelectorDist1(
                this V2d[] array, double absoluteEps)
        {
            return array.CreateRkdTree(Vec.Distance1, c_dimDistFunArrayD,
                                       null, null, absoluteEps);
        }

        public static PointRkdTreeDSelector<V2d[], V2d> CreateRkdTreeSelectorDist2(
                this V2d[] array, double absoluteEps)
        {
            return array.CreateRkdTree(Vec.Distance, c_dimDistFunArrayD,
                                       Vec.DistanceToLine, Fun.Lerp,
                                       absoluteEps);
        }

        public static PointRkdTreeDSelector<V2d[], V2d> CreateRkdTreeSelectorDistMax(
                this V2d[] array, double absoluteEps)
        {
            return array.CreateRkdTree(Vec.DistanceMax, c_dimDistFunArrayD,
                                       null, null, absoluteEps);
        }

        public static PointRkdTreeDSelector<V2d[], V2d> CreateRkdTreeSelectorDistDotProduct(
                this V2d[] array, double absoluteEps)
        {
            return array.CreateRkdTree(
                (a, b) => (double)1.0 - Fun.Abs(Vec.Dot(a, b)),
                c_dimDistConst1FunArrayD,
                null, null, absoluteEps);
        }

        public static PointRkdTreeDSelector<V2d[], V2d> CreateRkdTree(
                this V2d[] array,
                Func<V2d, V2d, double> distanceFun,
                Func<double, double, double>[] dimMaxDistanceFunArray,
                Func<V2d, V2d, V2d, double> lineDistFun,
                Func<double, V2d, V2d, V2d> lerpFun,
                double absoluteEps)
        {
            return new PointRkdTreeDSelector<V2d[], V2d>(
                    2, array.Length, array,
                    (a, ai) => a[ai], V2d.SelectorArray, 
                    distanceFun, dimMaxDistanceFunArray,
                    lineDistFun, lerpFun, absoluteEps);
        }

        #endregion

        #region V3d[] Selector Trees

        public static PointRkdTreeDSelector<V3d[], V3d> CreateRkdTreeSelector(
                this V3d[] array, Metric metric, double absoluteEps)
         => metric switch
            {
                Metric.Manhattan => array.CreateRkdTreeSelectorDist1(absoluteEps),
                Metric.Euclidean => array.CreateRkdTreeSelectorDist2(absoluteEps),
                Metric.Maximum => array.CreateRkdTreeSelectorDistMax(absoluteEps),
                _ => throw new ArgumentException()
            };

        public static PointRkdTreeDSelector<V3d[], V3d> CreateRkdTreeSelectorDist1(
                this V3d[] array, double absoluteEps)
        {
            return array.CreateRkdTree(Vec.Distance1, c_dimDistFunArrayD,
                                       null, null, absoluteEps);
        }

        public static PointRkdTreeDSelector<V3d[], V3d> CreateRkdTreeSelectorDist2(
                this V3d[] array, double absoluteEps)
        {
            return array.CreateRkdTree(Vec.Distance, c_dimDistFunArrayD,
                                       Vec.DistanceToLine, Fun.Lerp,
                                       absoluteEps);
        }

        public static PointRkdTreeDSelector<V3d[], V3d> CreateRkdTreeSelectorDistMax(
                this V3d[] array, double absoluteEps)
        {
            return array.CreateRkdTree(Vec.DistanceMax, c_dimDistFunArrayD,
                                       null, null, absoluteEps);
        }

        public static PointRkdTreeDSelector<V3d[], V3d> CreateRkdTreeSelectorDistDotProduct(
                this V3d[] array, double absoluteEps)
        {
            return array.CreateRkdTree(
                (a, b) => (double)1.0 - Fun.Abs(Vec.Dot(a, b)),
                c_dimDistConst1FunArrayD,
                null, null, absoluteEps);
        }

        public static PointRkdTreeDSelector<V3d[], V3d> CreateRkdTree(
                this V3d[] array,
                Func<V3d, V3d, double> distanceFun,
                Func<double, double, double>[] dimMaxDistanceFunArray,
                Func<V3d, V3d, V3d, double> lineDistFun,
                Func<double, V3d, V3d, V3d> lerpFun,
                double absoluteEps)
        {
            return new PointRkdTreeDSelector<V3d[], V3d>(
                    3, array.Length, array,
                    (a, ai) => a[ai], V3d.SelectorArray, 
                    distanceFun, dimMaxDistanceFunArray,
                    lineDistFun, lerpFun, absoluteEps);
        }

        #endregion

        #region V4d[] Selector Trees

        public static PointRkdTreeDSelector<V4d[], V4d> CreateRkdTreeSelector(
                this V4d[] array, Metric metric, double absoluteEps)
         => metric switch
            {
                Metric.Manhattan => array.CreateRkdTreeSelectorDist1(absoluteEps),
                Metric.Euclidean => array.CreateRkdTreeSelectorDist2(absoluteEps),
                Metric.Maximum => array.CreateRkdTreeSelectorDistMax(absoluteEps),
                _ => throw new ArgumentException()
            };

        public static PointRkdTreeDSelector<V4d[], V4d> CreateRkdTreeSelectorDist1(
                this V4d[] array, double absoluteEps)
        {
            return array.CreateRkdTree(Vec.Distance1, c_dimDistFunArrayD,
                                       null, null, absoluteEps);
        }

        public static PointRkdTreeDSelector<V4d[], V4d> CreateRkdTreeSelectorDist2(
                this V4d[] array, double absoluteEps)
        {
            return array.CreateRkdTree(Vec.Distance, c_dimDistFunArrayD,
                                       Vec.DistanceToLine, Fun.Lerp,
                                       absoluteEps);
        }

        public static PointRkdTreeDSelector<V4d[], V4d> CreateRkdTreeSelectorDistMax(
                this V4d[] array, double absoluteEps)
        {
            return array.CreateRkdTree(Vec.DistanceMax, c_dimDistFunArrayD,
                                       null, null, absoluteEps);
        }

        public static PointRkdTreeDSelector<V4d[], V4d> CreateRkdTreeSelectorDistDotProduct(
                this V4d[] array, double absoluteEps)
        {
            return array.CreateRkdTree(
                (a, b) => (double)1.0 - Fun.Abs(Vec.Dot(a, b)),
                c_dimDistConst1FunArrayD,
                null, null, absoluteEps);
        }

        public static PointRkdTreeDSelector<V4d[], V4d> CreateRkdTree(
                this V4d[] array,
                Func<V4d, V4d, double> distanceFun,
                Func<double, double, double>[] dimMaxDistanceFunArray,
                Func<V4d, V4d, V4d, double> lineDistFun,
                Func<double, V4d, V4d, V4d> lerpFun,
                double absoluteEps)
        {
            return new PointRkdTreeDSelector<V4d[], V4d>(
                    4, array.Length, array,
                    (a, ai) => a[ai], V4d.SelectorArray, 
                    distanceFun, dimMaxDistanceFunArray,
                    lineDistFun, lerpFun, absoluteEps);
        }

        #endregion

    }

    public static class PointVpTreeExtensions
    {
        #region Matrix<float> Trees

        public static PointVpTreeF<Matrix<float>, Vector<float>> CreateVpTree(
                this Matrix<float> array, Metric metric, float absoluteEps)
            => metric switch
            {
                Metric.Manhattan => array.CreateVpTreeDist1(absoluteEps),
                Metric.Euclidean => array.CreateVpTreeDist2(absoluteEps),
                Metric.Maximum => array.CreateVpTreeDistMax(absoluteEps),
                _ => throw new ArgumentException()
            };

        public static PointVpTreeF<Matrix<float>, Vector<float>> CreateVpTreeDist1(
                this Matrix<float> array, float absoluteEps)
        {
            return array.CreateVpTree((a, b) => a.Dist1(b),
                                      absoluteEps);
        }

        public static PointVpTreeF<Matrix<float>, Vector<float>> CreateVpTreeDist2(
                this Matrix<float> array, float absoluteEps)
        {
            return array.CreateVpTree((a, b) => a.Dist2(b),
                                      absoluteEps);
        }

        public static PointVpTreeF<Matrix<float>, Vector<float>> CreateVpTreeDistMax(
                this Matrix<float> array, float absoluteEps)
        {
            return array.CreateVpTree((a, b) => a.DistMax(b),
                                      absoluteEps);
        }

        public static PointVpTreeF<Matrix<float>, Vector<float>> CreateVpTree(
                this Matrix<float> array,
                Func<Vector<float>, Vector<float>, float> distanceFun,
                float absoluteEps)
        {
            return new PointVpTreeF<Matrix<float>, Vector<float>>(
                    array.Dim.Y, array.Dim.X, array,
                    (m, i) => m.Col(i), (v, i) => v[i],
                    distanceFun,
                    absoluteEps);
        }

        public static PointVpTreeF<Matrix<float>, Vector<float>> CreateVpTree(
                this Matrix<float> array, Metric metric, float absoluteEps,
                PointVpTreeFData data)
            => metric switch
            {
                Metric.Manhattan => array.CreateVpTreeDist1(absoluteEps, data),
                Metric.Euclidean => array.CreateVpTreeDist2(absoluteEps, data),
                Metric.Maximum => array.CreateVpTreeDistMax(absoluteEps, data),
                _ => throw new ArgumentException()
            };

        public static PointVpTreeF<Matrix<float>, Vector<float>> CreateVpTreeDist1(
                this Matrix<float> array, float absoluteEps, PointVpTreeFData data)
        {
            return array.CreateVpTree((a, b) => a.InnerProduct(b, (x0, x1) => Fun.Abs(x1 - x0), 0.0f, (s, p) => s + p),
                                      absoluteEps, data);
        }

        public static PointVpTreeF<Matrix<float>, Vector<float>> CreateVpTreeDist2(
                this Matrix<float> array, float absoluteEps, PointVpTreeFData data)
        {
            return array.CreateVpTree((a, b) => a.Dist2(b),
                                      absoluteEps, data);
        }

        public static PointVpTreeF<Matrix<float>, Vector<float>> CreateVpTreeDistMax(
                this Matrix<float> array, float absoluteEps, PointVpTreeFData data)
        {
            return array.CreateVpTree((a, b) => a.DistMax(b),
                                      absoluteEps, data);
        }

        public static PointVpTreeF<Matrix<float>, Vector<float>> CreateVpTree(
                this Matrix<float> array,
                Func<Vector<float>, Vector<float>, float> distanceFun,
                float absoluteEps, PointVpTreeFData data)
        {
            return new PointVpTreeF<Matrix<float>, Vector<float>>(
                    array.Dim.Y, array.Dim.X, array,
                    (m, i) => m.Col(i), (v, i) => v[i],
                    distanceFun,
                    absoluteEps, data);
        }

        #endregion

        #region float[] Trees

        public static PointVpTreeF<float[], float> CreateVpTree(
                this float[] array, Metric metric, float absoluteEps)
        {
            return array.CreateVpTreeDist1(absoluteEps);
        }

        public static PointVpTreeF<float[], float> CreateVpTreeDist1(
                this float[] array, float absoluteEps)
        {
            return array.CreateVpTree((a, b) => Fun.Abs(b - a),
                                      absoluteEps);
        }

        public static PointVpTreeF<float[], float> CreateVpTreeDist2(
                this float[] array, float absoluteEps)
        {
            return array.CreateVpTree((a, b) => Fun.Abs(b - a),
                                      absoluteEps);
        }

        public static PointVpTreeF<float[], float> CreateVpTreeDistMax(
                this float[] array, float absoluteEps)
        {
            return array.CreateVpTree((a, b) => Fun.Abs(b - a),
                                      absoluteEps);
        }

        public static PointVpTreeF<float[], float> CreateVpTree(
                this float[] array,
                Func<float, float, float> distanceFun,
                float absoluteEps)
        {
            return new PointVpTreeF<float[], float>(
                    1, array.LongLength, array,
                    (a, ai) => a[ai], (v, vi) => v,
                    distanceFun,
                    absoluteEps);
        }

        #endregion

        #region V2f[]  Trees

        public static PointVpTreeF<V2f[], V2f> CreateVpTree(
                this V2f[] array, Metric metric, float absoluteEps)
         => metric switch
            {
                Metric.Manhattan => array.CreateVpTreeDist1(absoluteEps),
                Metric.Euclidean => array.CreateVpTreeDist2(absoluteEps),
                Metric.Maximum => array.CreateVpTreeDistMax(absoluteEps),
                _ => throw new ArgumentException()
            };

        public static PointVpTreeF<V2f[], V2f> CreateVpTreeDist1(
                this V2f[] array, float absoluteEps)
        {
            return array.CreateVpTree(Vec.Distance1,
                                       absoluteEps);
        }

        public static PointVpTreeF<V2f[], V2f> CreateVpTreeDist2(
                this V2f[] array, float absoluteEps)
        {
            return array.CreateVpTree(Vec.Distance,
                                       absoluteEps);
        }

        public static PointVpTreeF<V2f[], V2f> CreateVpTreeDistMax(
                this V2f[] array, float absoluteEps)
        {
            return array.CreateVpTree(Vec.DistanceMax,
                                       absoluteEps);
        }

        public static PointVpTreeF<V2f[], V2f> CreateVpTreeDistDotProduct(
                this V2f[] array, float absoluteEps)
        {
            return array.CreateVpTree(
                (a, b) => (float)1.0 - Fun.Abs(Vec.Dot(a, b)),
                absoluteEps);
        }

        public static PointVpTreeF<V2f[], V2f> CreateVpTree(
                this V2f[] array,
                Func<V2f, V2f, float> distanceFun,
                float absoluteEps)
        {
            return new PointVpTreeF<V2f[], V2f>(
                    2, array.Length, array,
                    (a, ai) => a[ai], V2f.LongGetter, 
                    distanceFun,
                    absoluteEps);
        }

        #endregion

        #region V3f[]  Trees

        public static PointVpTreeF<V3f[], V3f> CreateVpTree(
                this V3f[] array, Metric metric, float absoluteEps)
         => metric switch
            {
                Metric.Manhattan => array.CreateVpTreeDist1(absoluteEps),
                Metric.Euclidean => array.CreateVpTreeDist2(absoluteEps),
                Metric.Maximum => array.CreateVpTreeDistMax(absoluteEps),
                _ => throw new ArgumentException()
            };

        public static PointVpTreeF<V3f[], V3f> CreateVpTreeDist1(
                this V3f[] array, float absoluteEps)
        {
            return array.CreateVpTree(Vec.Distance1,
                                       absoluteEps);
        }

        public static PointVpTreeF<V3f[], V3f> CreateVpTreeDist2(
                this V3f[] array, float absoluteEps)
        {
            return array.CreateVpTree(Vec.Distance,
                                       absoluteEps);
        }

        public static PointVpTreeF<V3f[], V3f> CreateVpTreeDistMax(
                this V3f[] array, float absoluteEps)
        {
            return array.CreateVpTree(Vec.DistanceMax,
                                       absoluteEps);
        }

        public static PointVpTreeF<V3f[], V3f> CreateVpTreeDistDotProduct(
                this V3f[] array, float absoluteEps)
        {
            return array.CreateVpTree(
                (a, b) => (float)1.0 - Fun.Abs(Vec.Dot(a, b)),
                absoluteEps);
        }

        public static PointVpTreeF<V3f[], V3f> CreateVpTree(
                this V3f[] array,
                Func<V3f, V3f, float> distanceFun,
                float absoluteEps)
        {
            return new PointVpTreeF<V3f[], V3f>(
                    3, array.Length, array,
                    (a, ai) => a[ai], V3f.LongGetter, 
                    distanceFun,
                    absoluteEps);
        }

        #endregion

        #region V4f[]  Trees

        public static PointVpTreeF<V4f[], V4f> CreateVpTree(
                this V4f[] array, Metric metric, float absoluteEps)
         => metric switch
            {
                Metric.Manhattan => array.CreateVpTreeDist1(absoluteEps),
                Metric.Euclidean => array.CreateVpTreeDist2(absoluteEps),
                Metric.Maximum => array.CreateVpTreeDistMax(absoluteEps),
                _ => throw new ArgumentException()
            };

        public static PointVpTreeF<V4f[], V4f> CreateVpTreeDist1(
                this V4f[] array, float absoluteEps)
        {
            return array.CreateVpTree(Vec.Distance1,
                                       absoluteEps);
        }

        public static PointVpTreeF<V4f[], V4f> CreateVpTreeDist2(
                this V4f[] array, float absoluteEps)
        {
            return array.CreateVpTree(Vec.Distance,
                                       absoluteEps);
        }

        public static PointVpTreeF<V4f[], V4f> CreateVpTreeDistMax(
                this V4f[] array, float absoluteEps)
        {
            return array.CreateVpTree(Vec.DistanceMax,
                                       absoluteEps);
        }

        public static PointVpTreeF<V4f[], V4f> CreateVpTreeDistDotProduct(
                this V4f[] array, float absoluteEps)
        {
            return array.CreateVpTree(
                (a, b) => (float)1.0 - Fun.Abs(Vec.Dot(a, b)),
                absoluteEps);
        }

        public static PointVpTreeF<V4f[], V4f> CreateVpTree(
                this V4f[] array,
                Func<V4f, V4f, float> distanceFun,
                float absoluteEps)
        {
            return new PointVpTreeF<V4f[], V4f>(
                    4, array.Length, array,
                    (a, ai) => a[ai], V4f.LongGetter, 
                    distanceFun,
                    absoluteEps);
        }

        #endregion

        #region Matrix<double> Trees

        public static PointVpTreeD<Matrix<double>, Vector<double>> CreateVpTree(
                this Matrix<double> array, Metric metric, double absoluteEps)
            => metric switch
            {
                Metric.Manhattan => array.CreateVpTreeDist1(absoluteEps),
                Metric.Euclidean => array.CreateVpTreeDist2(absoluteEps),
                Metric.Maximum => array.CreateVpTreeDistMax(absoluteEps),
                _ => throw new ArgumentException()
            };

        public static PointVpTreeD<Matrix<double>, Vector<double>> CreateVpTreeDist1(
                this Matrix<double> array, double absoluteEps)
        {
            return array.CreateVpTree((a, b) => a.Dist1(b),
                                      absoluteEps);
        }

        public static PointVpTreeD<Matrix<double>, Vector<double>> CreateVpTreeDist2(
                this Matrix<double> array, double absoluteEps)
        {
            return array.CreateVpTree((a, b) => a.Dist2(b),
                                      absoluteEps);
        }

        public static PointVpTreeD<Matrix<double>, Vector<double>> CreateVpTreeDistMax(
                this Matrix<double> array, double absoluteEps)
        {
            return array.CreateVpTree((a, b) => a.DistMax(b),
                                      absoluteEps);
        }

        public static PointVpTreeD<Matrix<double>, Vector<double>> CreateVpTree(
                this Matrix<double> array,
                Func<Vector<double>, Vector<double>, double> distanceFun,
                double absoluteEps)
        {
            return new PointVpTreeD<Matrix<double>, Vector<double>>(
                    array.Dim.Y, array.Dim.X, array,
                    (m, i) => m.Col(i), (v, i) => v[i],
                    distanceFun,
                    absoluteEps);
        }

        public static PointVpTreeD<Matrix<double>, Vector<double>> CreateVpTree(
                this Matrix<double> array, Metric metric, double absoluteEps,
                PointVpTreeDData data)
            => metric switch
            {
                Metric.Manhattan => array.CreateVpTreeDist1(absoluteEps, data),
                Metric.Euclidean => array.CreateVpTreeDist2(absoluteEps, data),
                Metric.Maximum => array.CreateVpTreeDistMax(absoluteEps, data),
                _ => throw new ArgumentException()
            };

        public static PointVpTreeD<Matrix<double>, Vector<double>> CreateVpTreeDist1(
                this Matrix<double> array, double absoluteEps, PointVpTreeDData data)
        {
            return array.CreateVpTree((a, b) => a.InnerProduct(b, (x0, x1) => Fun.Abs(x1 - x0), 0.0, (s, p) => s + p),
                                      absoluteEps, data);
        }

        public static PointVpTreeD<Matrix<double>, Vector<double>> CreateVpTreeDist2(
                this Matrix<double> array, double absoluteEps, PointVpTreeDData data)
        {
            return array.CreateVpTree((a, b) => a.Dist2(b),
                                      absoluteEps, data);
        }

        public static PointVpTreeD<Matrix<double>, Vector<double>> CreateVpTreeDistMax(
                this Matrix<double> array, double absoluteEps, PointVpTreeDData data)
        {
            return array.CreateVpTree((a, b) => a.DistMax(b),
                                      absoluteEps, data);
        }

        public static PointVpTreeD<Matrix<double>, Vector<double>> CreateVpTree(
                this Matrix<double> array,
                Func<Vector<double>, Vector<double>, double> distanceFun,
                double absoluteEps, PointVpTreeDData data)
        {
            return new PointVpTreeD<Matrix<double>, Vector<double>>(
                    array.Dim.Y, array.Dim.X, array,
                    (m, i) => m.Col(i), (v, i) => v[i],
                    distanceFun,
                    absoluteEps, data);
        }

        #endregion

        #region double[] Trees

        public static PointVpTreeD<double[], double> CreateVpTree(
                this double[] array, Metric metric, double absoluteEps)
        {
            return array.CreateVpTreeDist1(absoluteEps);
        }

        public static PointVpTreeD<double[], double> CreateVpTreeDist1(
                this double[] array, double absoluteEps)
        {
            return array.CreateVpTree((a, b) => Fun.Abs(b - a),
                                      absoluteEps);
        }

        public static PointVpTreeD<double[], double> CreateVpTreeDist2(
                this double[] array, double absoluteEps)
        {
            return array.CreateVpTree((a, b) => Fun.Abs(b - a),
                                      absoluteEps);
        }

        public static PointVpTreeD<double[], double> CreateVpTreeDistMax(
                this double[] array, double absoluteEps)
        {
            return array.CreateVpTree((a, b) => Fun.Abs(b - a),
                                      absoluteEps);
        }

        public static PointVpTreeD<double[], double> CreateVpTree(
                this double[] array,
                Func<double, double, double> distanceFun,
                double absoluteEps)
        {
            return new PointVpTreeD<double[], double>(
                    1, array.LongLength, array,
                    (a, ai) => a[ai], (v, vi) => v,
                    distanceFun,
                    absoluteEps);
        }

        #endregion

        #region V2d[]  Trees

        public static PointVpTreeD<V2d[], V2d> CreateVpTree(
                this V2d[] array, Metric metric, double absoluteEps)
         => metric switch
            {
                Metric.Manhattan => array.CreateVpTreeDist1(absoluteEps),
                Metric.Euclidean => array.CreateVpTreeDist2(absoluteEps),
                Metric.Maximum => array.CreateVpTreeDistMax(absoluteEps),
                _ => throw new ArgumentException()
            };

        public static PointVpTreeD<V2d[], V2d> CreateVpTreeDist1(
                this V2d[] array, double absoluteEps)
        {
            return array.CreateVpTree(Vec.Distance1,
                                       absoluteEps);
        }

        public static PointVpTreeD<V2d[], V2d> CreateVpTreeDist2(
                this V2d[] array, double absoluteEps)
        {
            return array.CreateVpTree(Vec.Distance,
                                       absoluteEps);
        }

        public static PointVpTreeD<V2d[], V2d> CreateVpTreeDistMax(
                this V2d[] array, double absoluteEps)
        {
            return array.CreateVpTree(Vec.DistanceMax,
                                       absoluteEps);
        }

        public static PointVpTreeD<V2d[], V2d> CreateVpTreeDistDotProduct(
                this V2d[] array, double absoluteEps)
        {
            return array.CreateVpTree(
                (a, b) => (double)1.0 - Fun.Abs(Vec.Dot(a, b)),
                absoluteEps);
        }

        public static PointVpTreeD<V2d[], V2d> CreateVpTree(
                this V2d[] array,
                Func<V2d, V2d, double> distanceFun,
                double absoluteEps)
        {
            return new PointVpTreeD<V2d[], V2d>(
                    2, array.Length, array,
                    (a, ai) => a[ai], V2d.LongGetter, 
                    distanceFun,
                    absoluteEps);
        }

        #endregion

        #region V3d[]  Trees

        public static PointVpTreeD<V3d[], V3d> CreateVpTree(
                this V3d[] array, Metric metric, double absoluteEps)
         => metric switch
            {
                Metric.Manhattan => array.CreateVpTreeDist1(absoluteEps),
                Metric.Euclidean => array.CreateVpTreeDist2(absoluteEps),
                Metric.Maximum => array.CreateVpTreeDistMax(absoluteEps),
                _ => throw new ArgumentException()
            };

        public static PointVpTreeD<V3d[], V3d> CreateVpTreeDist1(
                this V3d[] array, double absoluteEps)
        {
            return array.CreateVpTree(Vec.Distance1,
                                       absoluteEps);
        }

        public static PointVpTreeD<V3d[], V3d> CreateVpTreeDist2(
                this V3d[] array, double absoluteEps)
        {
            return array.CreateVpTree(Vec.Distance,
                                       absoluteEps);
        }

        public static PointVpTreeD<V3d[], V3d> CreateVpTreeDistMax(
                this V3d[] array, double absoluteEps)
        {
            return array.CreateVpTree(Vec.DistanceMax,
                                       absoluteEps);
        }

        public static PointVpTreeD<V3d[], V3d> CreateVpTreeDistDotProduct(
                this V3d[] array, double absoluteEps)
        {
            return array.CreateVpTree(
                (a, b) => (double)1.0 - Fun.Abs(Vec.Dot(a, b)),
                absoluteEps);
        }

        public static PointVpTreeD<V3d[], V3d> CreateVpTree(
                this V3d[] array,
                Func<V3d, V3d, double> distanceFun,
                double absoluteEps)
        {
            return new PointVpTreeD<V3d[], V3d>(
                    3, array.Length, array,
                    (a, ai) => a[ai], V3d.LongGetter, 
                    distanceFun,
                    absoluteEps);
        }

        #endregion

        #region V4d[]  Trees

        public static PointVpTreeD<V4d[], V4d> CreateVpTree(
                this V4d[] array, Metric metric, double absoluteEps)
         => metric switch
            {
                Metric.Manhattan => array.CreateVpTreeDist1(absoluteEps),
                Metric.Euclidean => array.CreateVpTreeDist2(absoluteEps),
                Metric.Maximum => array.CreateVpTreeDistMax(absoluteEps),
                _ => throw new ArgumentException()
            };

        public static PointVpTreeD<V4d[], V4d> CreateVpTreeDist1(
                this V4d[] array, double absoluteEps)
        {
            return array.CreateVpTree(Vec.Distance1,
                                       absoluteEps);
        }

        public static PointVpTreeD<V4d[], V4d> CreateVpTreeDist2(
                this V4d[] array, double absoluteEps)
        {
            return array.CreateVpTree(Vec.Distance,
                                       absoluteEps);
        }

        public static PointVpTreeD<V4d[], V4d> CreateVpTreeDistMax(
                this V4d[] array, double absoluteEps)
        {
            return array.CreateVpTree(Vec.DistanceMax,
                                       absoluteEps);
        }

        public static PointVpTreeD<V4d[], V4d> CreateVpTreeDistDotProduct(
                this V4d[] array, double absoluteEps)
        {
            return array.CreateVpTree(
                (a, b) => (double)1.0 - Fun.Abs(Vec.Dot(a, b)),
                absoluteEps);
        }

        public static PointVpTreeD<V4d[], V4d> CreateVpTree(
                this V4d[] array,
                Func<V4d, V4d, double> distanceFun,
                double absoluteEps)
        {
            return new PointVpTreeD<V4d[], V4d>(
                    4, array.Length, array,
                    (a, ai) => a[ai], V4d.LongGetter, 
                    distanceFun,
                    absoluteEps);
        }

        #endregion

    }

}
