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
using Aardvark.Base.Coder;
using Aardvark.Base.Sorting;
using System;
using System.Collections.Generic;

namespace Aardvark.Geometry
{
    // AUTO GENERATED CODE - DO NOT CHANGE!

    //# foreach (var t in Meta.VecFieldTypes) { if (!t.IsReal) continue;
    //#     var ctype = t.Name;
    //#     var cname = Meta.GetXmlTypeName(t.Name);
    //#     var cchar = t.Char.ToUpper();
    //#     foreach (var hasSel in new[] { false, true }) {
    //#     var funArrayType = hasSel ? "[]" : "";
    //#     var vectorGetterType = "Func<TPoint, " + (hasSel ? "" : "long, ") + ctype + ">" + funArrayType;
    //#     var vectorGetter = hasSel ? "selectorArray" : "vectorGetter";
    //#     var mvget = hasSel ? "m_sela" : "m_vget";
    //#     var dimDistType = "Func<" + (hasSel ? "" : "long, ") + ctype + ", " + ctype + ", " + ctype + ">" + funArrayType;
    //#     var selArray = hasSel ? "Array" : "";
    //#     var selA = hasSel ? "A" : "";
    //#     var sel = hasSel ? "Selector" : "";
    //#     var asc = hasSel ? "Ascending" : "";
    #region PointRkdTree__cchar____sel__

    [RegisterTypeInfo]
    public class PointRkdTree__cchar____sel__Data : IFieldCodeable
    {
        public long[] PermArray;
        public int[] AxisArray;
        public __ctype__[] RadiusArray;

        public PointRkdTree__cchar____sel__Data()
        { }

        public IEnumerable<FieldCoder> GetFieldCoders(int coderVersion)
        {
            return new[]
            {
                new FieldCoder(0, "PermArray",
                        (c,o) => c.CodeLongArray(ref ((PointRkdTree__cchar____sel__Data)o).PermArray) ),
                new FieldCoder(1, "AxisArray",
                        (c,o) => c.CodeIntArray(ref ((PointRkdTree__cchar____sel__Data)o).AxisArray) ),
                new FieldCoder(2, "RadiusArray",
                        (c,o) => c.Code__cname__Array(ref ((PointRkdTree__cchar____sel__Data)o).RadiusArray) ),
            };
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
    public partial class PointRkdTree__cchar____sel__<TArray, TPoint>
    {
        long m_dim;
        long m_size;
        TArray m_array;
        Func<TArray, long, TPoint> m_aget;
        __vectorGetterType__ __mvget__;
        Func<TPoint, TPoint, __ctype__> m_dist;
        __dimDistType__ m_dimDist__selA__;
        //# if (t.IsReal) {
        Func<TPoint, TPoint, TPoint, __ctype__> m_lineDist;
        Func<__ctype__, TPoint, TPoint, TPoint> m_lerp;
        //# }
        __ctype__ m_eps;

        long[] m_perm;
        int[] m_axis; // 2^31 dimensions are way enough
        __ctype__[] m_radius;

        #region Constructor

        public PointRkdTree__cchar____sel__(
                long dim, long size, TArray array,
                Func<TArray, long, TPoint> arrayGetter,
                __vectorGetterType__ __vectorGetter__,
                Func<TPoint, TPoint, __ctype__> distanceFun,
                __dimDistType__ dimDistanceFun__selArray__,
                //# if (t.IsReal) {
                Func<TPoint, TPoint, TPoint, __ctype__> lineDistFun,
                Func<__ctype__, TPoint, TPoint, TPoint> lerpFun,
                //# }
                __ctype__ absoluteEps)
            : this(dim, size, array, arrayGetter, __vectorGetter__, distanceFun,
                   dimDistanceFun__selArray__, /*# if (t.IsReal) { */lineDistFun, lerpFun,/*# } */absoluteEps,
                   new PointRkdTree__cchar____sel__Data
                   {
                        PermArray = new long[size],
                        AxisArray = new int[size/2], // heap internal nodes only => size/2
                        RadiusArray = new __ctype__[size / 2],
                   })
        {
            var perm = new long[size].SetByIndexLong(i => i);

            long p2 = Fun.PrevPowerOfTwo(size);
            long row = size + 1 - p2; // length of last row of heap
            long left = p2 / 2; // full width of left subtree in last row

            Balance(perm, 0, left, row, 0, size);
        }

        public PointRkdTree__cchar____sel__(
                long dim, long size, TArray array,
                Func<TArray, long, TPoint> arrayGetter,
                __vectorGetterType__ __vectorGetter__,
                Func<TPoint, TPoint, __ctype__> distanceFun,
                __dimDistType__ dimDistanceFun__selArray__,
                //# if (t.IsReal) {
                Func<TPoint, TPoint, TPoint, __ctype__> lineDistFun,
                Func<__ctype__, TPoint, TPoint, TPoint> lerpFun,
                //# }
                __ctype__ absoluteEps,
                PointRkdTree__cchar____sel__Data data
                )
        {
            m_dim = dim;
            m_size = size;
            m_array = array;
            m_aget = arrayGetter;
            __mvget__ = __vectorGetter__;
            m_dist = distanceFun;
            m_dimDist__selA__ = dimDistanceFun__selArray__;
            //# if (t.IsReal) {
            m_lineDist = lineDistFun;
            m_lerp = lerpFun;
            //# }
            m_eps = absoluteEps;

            m_perm = data?.PermArray;
            m_axis = data?.AxisArray;
            m_radius = data?.RadiusArray;
        }

        private __ctype__ GetMaxDist(
                TPoint p, long[] perm, long start, long end)
        {
            var max = __ctype__.MinValue;
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
            var min = new __ctype__[m_dim].Set(__ctype__.MaxValue);
            var max = new __ctype__[m_dim].Set(__ctype__.MinValue);
            for (long i = start; i < end; i++) // calculate bounding box
            {
                var v = m_aget(m_array, perm[i]);
                for (long vi = 0; vi < m_dim; vi++)
                {
                    //# if (hasSel) {
                    var x = m_sela[vi](v);
                    //# } else {
                    var x = m_vget(v, vi);
                    //# }
                    if (x < min[vi]) min[vi] = x;
                    if (x > max[vi]) max[vi] = x;
                }
            }
            long dim = 0;
            __ctype__ size = max[0] - min[0];
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
            perm.PermutationQuickMedian__asc__(m_array, m_aget,
                    //# if (hasSel) {
                    m_sela[dim],
                    //# } else {
                    (v0, v1) => m_vget(v0, dim).CompareTo(m_vget(v1, dim)),
                    //# }
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
            public __ctype__ MaxDist;
            public __ctype__ MaxDistEps;
            public int MaxCount;
            public List<IndexDist<__ctype__>> List;
            public readonly bool DynamicSize;
            public __ctype__ OriginalMaxDist;
            public __ctype__ OriginalMaxDistEps;

            public ClosestQuery()
            { }

            public ClosestQuery(__ctype__ maxDistance,
                           __ctype__ maxDistancePlusEps, int maxCount,
                           List<IndexDist<__ctype__>> list)
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
            public Func<TPoint, TPoint, __ctype__> Dist;
            public __dimDistType__ DimDist__selArray__;
            public Func<long, bool> Filter;

            public ClosestToPointQuery(
                    __ctype__ maxDistance,
                    __ctype__ maxDistancePlusEps, int maxCount,
                    Func<TPoint, TPoint, __ctype__> dist,
                    __dimDistType__ dimDist__selA__,
                    List<IndexDist<__ctype__>> list)
                : base(maxDistance, maxDistancePlusEps, maxCount, list)
            {
                Dist = dist;
                DimDist__selArray__ = dimDist__selA__;
            }

            public ClosestToPointQuery(
                    TPoint query, __ctype__ maxDistance,
                    __ctype__ maxDistancePlusEps, int maxCount,
                    Func<TPoint, TPoint, __ctype__> dist,
                    __dimDistType__ dimDist__selA__,
                    List<IndexDist<__ctype__>> list)
                : base(maxDistance, maxDistancePlusEps, maxCount, list)
            {
                Point = query;
                Dist = dist;
                DimDist__selArray__ = dimDist__selA__;
            }
        }

        #endregion

        #region Properties

        public PointRkdTree__cchar____sel__Data Data
        {
            get
            {
                return new PointRkdTree__cchar____sel__Data
                {
                    PermArray = m_perm,
                    AxisArray = m_axis,
                    RadiusArray = m_radius
                };
            }
        }

        #endregion

        #region Operations

        private static List<IndexDist<__ctype__>> StaticCreateList(int maxCount)
        {
            if (maxCount > 0)
                return new List<IndexDist<__ctype__>>(maxCount + 1);
            else
                return new List<IndexDist<__ctype__>>();
        }

        /// <summary>
        /// Create a closest to point query object for multiple point
        /// queries with the same maximum distance and maximum count
        /// values.
        /// </summary>
        public ClosestToPointQuery CreateClosestToPointQuery(
                __ctype__ maxDistance, int maxCount)
        {
            var maxDistPlusEps = maxDistance < __ctype__.MaxValue
                    ? maxDistance + m_eps : maxDistance;
            var q = new ClosestToPointQuery(
                                maxDistance, maxDistPlusEps, maxCount,
                                m_dist, m_dimDist__selA__,
                                StaticCreateList(maxCount));
            return q;
        }

        /// <summary>
        /// Add the query result with the supplied point to the closest to
        /// point query object. The accumulated result is returned, or can
        /// be retrieved as the List property of the query object.
        /// </summary>
        public List<IndexDist<__ctype__>> GetClosest(
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
        public List<IndexDist<__ctype__>> GetClosest(
                TPoint query, __ctype__ maxDistance, int maxCount)
        {
            var list = StaticCreateList(maxCount);
            var mdplus = maxDistance < __ctype__.MaxValue
                                ? maxDistance + m_eps
                                : maxDistance;
            var q = new ClosestToPointQuery(query, maxDistance, mdplus,
                                    maxCount, m_dist, m_dimDist__selA__, list);
            GetClosest(q, 0);
            return q.List;
        }

        /// <summary>
        /// Get the single closest point from the set.
        /// </summary>
        public IndexDist<__ctype__> GetClosest(TPoint query)
        {
            return GetClosest(query, __ctype__.MaxValue, 1)[0];
        }

        private void GetAllList(ClosestToPointQuery q, long top)
        {
            q.List.Add(new IndexDist<__ctype__>(m_perm[top], __ctype__.MinValue));
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
                    q.List.Add(new IndexDist<__ctype__>(index, dist));
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
                    q.List.HeapDescendingEnqueue(new IndexDist<__ctype__>(index, dist));
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
            //# if (hasSel) {
            var sel = m_sela[dim]; var x = sel(q.Point); var s = sel(splitPoint);
            //# } else {
            var x = m_vget(q.Point, dim); var s = m_vget(splitPoint, dim);
            //# }
            if (x < s)
            {
                GetClosest(q, t1);
                //# if (hasSel) {
                if (q.MaxDistEps < q.DimDistArray[dim](x, s)) return;
                //# } else {
                if (q.MaxDistEps < q.DimDist(dim, x, s)) return;
                //# }
                long t2 = t1 + 1; if (t2 >= m_size) return;
                GetClosest(q, t2);
            }
            else
            {
                long t2 = t1 + 1;
                if (t2 < m_size) GetClosest(q, t2);
                //# if (hasSel) {
                if (q.MaxDistEps < q.DimDistArray[dim](s, x)) return;
                //# } else {
                if (q.MaxDistEps < q.DimDist(dim, s, x)) return;
                //# }
                GetClosest(q, t1);
            }
        }

        private void GetAllListFilter(ClosestToPointQuery q, long top)
        {
            var index = m_perm[top];
            if (q.Filter(index))
                q.List.Add(new IndexDist<__ctype__>(m_perm[top], __ctype__.MinValue));
            long t1 = 2 * top + 1; if (t1 >= m_size) return;
            GetAllList(q, t1);
            long t2 = t1 + 1; if (t2 >= m_size) return;
            GetAllList(q, t2);
        }

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
                        q.List.Add(new IndexDist<__ctype__>(index, dist));
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
                        q.List.HeapDescendingEnqueue(new IndexDist<__ctype__>(index, dist));
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
            //# if (hasSel) {
            var sel = m_sela[dim]; var x = sel(q.Point); var s = sel(splitPoint);
            //# } else {
            var x = m_vget(q.Point, dim); var s = m_vget(splitPoint, dim);
            //# }
            if (x < s)
            {
                GetClosestFilter(q, t1);
                //# if (hasSel) {
                if (q.MaxDistEps < q.DimDistArray[dim](x, s)) return;
                //# } else {
                if (q.MaxDistEps < q.DimDist(dim, x, s)) return;
                //# }
                long t2 = t1 + 1; if (t2 >= m_size) return;
                GetClosestFilter(q, t2);
            }
            else
            {
                long t2 = t1 + 1;
                if (t2 < m_size) GetClosestFilter(q, t2);
                //# if (hasSel) {
                if (q.MaxDistEps < q.DimDistArray[dim](s, x)) return;
                //# } else {
                if (q.MaxDistEps < q.DimDist(dim, s, x)) return;
                //# }
                GetClosestFilter(q, t1);
            }
        }

        #endregion

    }

    #endregion

    //# } // hasSel
    #region PointKdTree__cchar__

    [RegisterTypeInfo]
    public class PointKdTree__cchar__Data : IFieldCodeable
    {
        public long[] PermArray;
        public int[] AxisArray;

        public PointKdTree__cchar__Data()
        { }

        public IEnumerable<FieldCoder> GetFieldCoders(int coderVersion)
        {
            return new[]
            {
                new FieldCoder(0, "PermArray",
                        (c,o) => c.CodeLongArray(ref ((PointKdTree__cchar__Data)o).PermArray) ),
                new FieldCoder(1, "AxisArray",
                        (c,o) => c.CodeIntArray(ref ((PointKdTree__cchar__Data)o).AxisArray) ),
            };
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
    public partial class PointKdTree__cchar__<TArray, TPoint>
    {
        long m_dim;
        long m_size;
        TArray m_array;
        Func<TArray, long, TPoint> m_aget;
        Func<TPoint, long, __ctype__> m_vget;
        Func<TPoint, TPoint, __ctype__> m_dist;
        Func<long, __ctype__, __ctype__, __ctype__> m_dimDist;
        __ctype__ m_eps;

        long[] m_perm;
        int[] m_axis; // 2^31 dimensions are way enough

        #region Constructor

        public PointKdTree__cchar__(
                long dim, long size, TArray array,
                Func<TArray, long, TPoint> arrayGetter,
                Func<TPoint, long, __ctype__> vectorGetter,
                Func<TPoint, TPoint, __ctype__> distanceFun,
                Func<long, __ctype__, __ctype__, __ctype__> dimDistanceFun,
                __ctype__ absoluteEps)
            : this(dim, size, array, arrayGetter, vectorGetter, distanceFun,
                    dimDistanceFun, absoluteEps,
                    new PointKdTree__cchar__Data
                    {
                        PermArray = new long[size],
                        AxisArray = new int[size / 2] // heap internal nodes only => size/2
                    })
        {
            var min = new __ctype__[m_dim].Set(__ctype__.MaxValue);
            var max = new __ctype__[m_dim].Set(__ctype__.MinValue);

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
        
        public PointKdTree__cchar__(
                long dim, long size, TArray array,
                Func<TArray, long, TPoint> arrayGetter,
                Func<TPoint, long, __ctype__> vectorGetter,
                Func<TPoint, TPoint, __ctype__> distanceFun,
                Func<long, __ctype__, __ctype__, __ctype__> dimDistanceFun,
                __ctype__ absoluteEps,
                PointKdTree__cchar__Data data
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
                long start, long end, __ctype__[] min, __ctype__[] max)
        {
            if (row <= 0) { left /= 2; row = long.MaxValue; }
            if (left == 0) { m_perm[top] = perm[start]; return; }
            long mid = start - 1 + left + Fun.Min(left, row);
            long dim = 0;
            __ctype__ size = max[0] - min[0];
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
                var lmax = __ctype__.MinValue;
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
                var rmin = __ctype__.MaxValue;
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
            public __ctype__ MaxDist;
            public __ctype__ MaxDistEps;
            public int MaxCount;
            public List<IndexDist<__ctype__>> List;
            public readonly bool DynamicSize;
            public __ctype__ OriginalMaxDist;
            public __ctype__ OriginalMaxDistEps;

            public ClosestQuery()
            { }

            public ClosestQuery(__ctype__ maxDistance,
                           __ctype__ maxDistancePlusEps, int maxCount,
                           List<IndexDist<__ctype__>> list)
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
            public Func<TPoint, TPoint, __ctype__> Dist;
            public Func<long, __ctype__, __ctype__, __ctype__> DimDist;
            public Func<long, bool> Filter;

            public ClosestToPointQuery(
                    __ctype__ maxDistance,
                    __ctype__ maxDistancePlusEps, int maxCount,
                    Func<TPoint, TPoint, __ctype__> dist,
                    Func<long, __ctype__, __ctype__, __ctype__> dimDist,
                    List<IndexDist<__ctype__>> list)
                : base(maxDistance, maxDistancePlusEps, maxCount, list)
            {
                Dist = dist;
                DimDist = dimDist;
            }

            public ClosestToPointQuery(
                    TPoint query, __ctype__ maxDistance,
                    __ctype__ maxDistancePlusEps, int maxCount,
                    Func<TPoint, TPoint, __ctype__> dist,
                    Func<long, __ctype__, __ctype__, __ctype__> dimDist,
                    List<IndexDist<__ctype__>> list)
                : base(maxDistance, maxDistancePlusEps, maxCount, list)
            {
                Point = query;
                Dist = dist;
                DimDist = dimDist;
            }
        }

        #endregion

        #region Properties

        public PointKdTree__cchar__Data Data
        {
            get
            {
                return new PointKdTree__cchar__Data
                {
                    PermArray = m_perm,
                    AxisArray = m_axis
                };
            }
        }

        #endregion

        #region Operations

        private static List<IndexDist<__ctype__>> StaticCreateList(int maxCount)
        {
            if (maxCount > 0)
                return new List<IndexDist<__ctype__>>(maxCount + 1);
            else
                return new List<IndexDist<__ctype__>>();
        }

        /// <summary>
        /// Create a closest to point query object for multiple point
        /// queries with the same maximum distance and maximum count
        /// values.
        /// </summary>
        public ClosestToPointQuery CreateClosestToPointQuery(
                __ctype__ maxDistance, int maxCount)
        {
            var maxDistPlusEps = maxDistance < __ctype__.MaxValue
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
        public List<IndexDist<__ctype__>> GetClosest(
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
        public List<IndexDist<__ctype__>> CreateList(int maxCount)
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
                TPoint query, __ctype__ maxDistance, int maxCount,
                List<IndexDist<__ctype__>> list)
        {
            list.Clear();
            var mdplus = maxDistance < __ctype__.MaxValue
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
        public List<IndexDist<__ctype__>> GetClosest(
                TPoint query, __ctype__ maxDistance, int maxCount)
        {
            var list = StaticCreateList(maxCount);
            var mdplus = maxDistance < __ctype__.MaxValue
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
        public IndexDist<__ctype__> GetClosest(TPoint query)
        {
            return GetClosest(query, __ctype__.MaxValue, 1)[0];
        }

        private void GetClosest(ClosestToPointQuery q, long top)
        {
            long index = m_perm[top];
            var splitPoint = m_aget(m_array, index);
            var dist = q.Dist(q.Point, splitPoint);
            if (dist <= q.MaxDist)
            {
                if (q.DynamicSize)
                    q.List.Add(new IndexDist<__ctype__>(index, dist));
                else
                {
                    q.List.HeapDescendingEnqueue(new IndexDist<__ctype__>(index, dist));
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

    #region PointVpTree__cchar__

    [RegisterTypeInfo]
    public class PointVpTree__cchar__Data : IFieldCodeable
    {
        public long[] PermArray;
        public __ctype__[] LMaxArray;
        public __ctype__[] RMinArray;

        public PointVpTree__cchar__Data()
        { }

        public IEnumerable<FieldCoder> GetFieldCoders(int coderVersion)
        {
            return new[]
            {
                new FieldCoder(0, "PermArray",
                        (c,o) => c.CodeLongArray(ref ((PointVpTree__cchar__Data)o).PermArray) ),
                new FieldCoder(1, "LMaxArray",
                        (c,o) => c.Code__cname__Array(ref ((PointVpTree__cchar__Data)o).LMaxArray) ),
                new FieldCoder(2, "RMinArray",
                        (c,o) => c.Code__cname__Array(ref ((PointVpTree__cchar__Data)o).RMinArray) ),
            };
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
    public partial class PointVpTree__cchar__<TArray, TPoint>
    {
        long m_dim;
        long m_size;
        TArray m_array;
        Func<TArray, long, TPoint> m_aget;
        Func<TPoint, long, __ctype__> m_vget;
        Func<TPoint, TPoint, __ctype__> m_dist;
        readonly __ctype__ m_eps;

        long[] m_perm;
        __ctype__[] m_lmax;
        __ctype__[] m_rmin;

        #region Constructor

        public PointVpTree__cchar__(
                long dim, long size, TArray array,
                Func<TArray, long, TPoint> arrayGetter,
                Func<TPoint, long, __ctype__> vectorGetter,
                Func<TPoint, TPoint, __ctype__> distanceFun,
                __ctype__ absoluteEpsilon)
            : this(dim, size, array, arrayGetter, vectorGetter, distanceFun,
                    absoluteEpsilon,
                    new PointVpTree__cchar__Data
                    {
                        PermArray = new long[size],
                        LMaxArray = new __ctype__[size / 2], // Only internal nodes of the heap
                        RMinArray = new __ctype__[size / 2] // need these bounds => size/2.
                    })
        {
            var perm = new long[size].SetByIndexLong(i => i);

            long p2 = Fun.PrevPowerOfTwo(size);
            long row = size + 1 - p2; // length of last row of heap
            long left = p2 / 2; // length of left side in last row

            Balance(perm, 0, left, row, 0, size);
        }

        public PointVpTree__cchar__(
                long dim, long size, TArray array,
                Func<TArray, long, TPoint> arrayGetter,
                Func<TPoint, long, __ctype__> vectorGetter,
                Func<TPoint, TPoint, __ctype__> distanceFun,
                __ctype__ absoluteEpsilon,
                PointVpTree__cchar__Data data
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
                long[] perm, long start, long end, out __ctype__ min, out __ctype__ max)
        {
            min = __ctype__.MaxValue;
            max = __ctype__.MinValue;
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

        private __ctype__ GetMin(long[] perm, long start, long end, TPoint vp)
        {
            __ctype__ min = __ctype__.MaxValue;
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
            __ctype__ vmin, vmax;
            long vi = GetMinMaxIndex(perm, start, end, out vmin, out vmax);
            perm.Swap(vi, start); // vp candidate 2 @start
            __ctype__ vmin2, vmax2;
            GetMinMaxIndex(perm, start, end, out vmin2, out vmax2);
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
            public __ctype__ MaxDist;
            public int MaxCount;
            public List<IndexDist<__ctype__>> List;
            public bool DynamicSize;

            public ClosestToPointQuery(TPoint query, __ctype__ maxDistance, int maxCount,
                           List<IndexDist<__ctype__>> list)
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

        public PointVpTree__cchar__Data Data
        {
            get
            {
                return new PointVpTree__cchar__Data
                {
                    PermArray = m_perm,
                    LMaxArray = m_lmax,
                    RMinArray = m_rmin
                };
            }
        }

        #endregion

        #region Operations

        private static List<IndexDist<__ctype__>> StaticCreateList(int maxCount)
        {
            if (maxCount > 0)
                return new List<IndexDist<__ctype__>>(maxCount + 1);
            else
                return new List<IndexDist<__ctype__>>();
        }

        /// <summary>
        /// Create a heap to use in multiple GetClosest queries.
        /// </summary>
        /// <param name="maxCount"></param>
        /// <returns></returns>
        public List<IndexDist<__ctype__>> CreateList(int maxCount)
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
                TPoint query, __ctype__ maxDistance, int maxCount,
                List<IndexDist<__ctype__>> list)
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
        public List<IndexDist<__ctype__>> GetClosest(
                TPoint query, __ctype__ maxDistance, int maxCount)
        {
            var list = StaticCreateList(maxCount);
            var q = new ClosestToPointQuery(query, maxDistance, maxCount, list);
            GetClosest(q, 0);
            return q.List;
        }

        /// <summary>
        /// Get the single closest point from the set.
        /// </summary>
        public IndexDist<__ctype__> GetClosest(TPoint query)
        {
            return GetClosest(query, __ctype__.MaxValue, 1)[0];
        }

        private void GetAllList(ClosestToPointQuery q, long top)
        {
            q.List.Add(new IndexDist<__ctype__>(m_perm[top], __ctype__.MinValue));
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
                    q.List.Add(new IndexDist<__ctype__>(index, dist));
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
                    q.List.HeapDescendingEnqueue(new IndexDist<__ctype__>(index, dist));
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

    //# } // foreach f in Meta.VecFieldTypes
    //# foreach (var kd in new[] { "Kd", "Rkd", "Vp" }) {
    //#     var isVp = kd == "Vp"; var isRkd = kd == "Rkd"; var isKd = kd == "Kd";
    public static class Point__kd__TreeExtensions
    {
        //# foreach (var t in Meta.RealTypes) {
        //#     var ctype = t.Name;
        //#     var cchar = t.Char.ToUpper();
        //#     var ctypecap = ctype.Capitalized();
        //#     var ccast = ctype == "float" ? "(float)" : "";
        #region VecArray<__ctype__> Trees

        public static Point__kd__Tree__cchar__<VecArray<__ctype__>, Vec<__ctype__>> Create__kd__Tree(
                this VecArray<__ctype__> array, Metric metric, __ctype__ absoluteEps)
        {
            switch (metric)
            {
                case Metric.Manhattan: return array.Create__kd__TreeDist1(absoluteEps);
                case Metric.Euclidean: return array.Create__kd__TreeDist2(absoluteEps);
                case Metric.Maximum: return array.Create__kd__TreeDistMax(absoluteEps);
                default: throw new ArgumentException();
            }
        }

        public static Point__kd__Tree__cchar__<VecArray<__ctype__>, Vec<__ctype__>> Create__kd__TreeDist1(
                this VecArray<__ctype__> array, __ctype__ absoluteEps)
        {
            return array.Create__kd__Tree(Vec__ctypecap__.Dist1,
                                      /*# if (!isVp) { */(dim, a, b) => b - a, /*# } */absoluteEps);
        }

        public static Point__kd__Tree__cchar__<VecArray<__ctype__>, Vec<__ctype__>> Create__kd__TreeDist2(
                this VecArray<__ctype__> array, __ctype__ absoluteEps)
        {
            return array.Create__kd__Tree(Vec__ctypecap__.Dist2,
                                      /*# if (!isVp) { */(dim, a, b) => b - a, /*# } */absoluteEps);
        }

        //# if (isKd) {
        public static Point__kd__Tree__cchar__<VecArray<__ctype__>, Vec<__ctype__>> Create__kd__TreeDist2Squared(
                this VecArray<__ctype__> array, __ctype__ absoluteEps)
        {
            return array.Create__kd__Tree((a, b) => __ccast__Vec__ctypecap__.Dist2Squared(a, b),
                                      /*# if (!isVp) { */(dim, a, b) => __ccast__Fun.Square(b - a), /*# } */absoluteEps);
        }

        //# } // isKd
        public static Point__kd__Tree__cchar__<VecArray<__ctype__>, Vec<__ctype__>> Create__kd__TreeDistMax(
                this VecArray<__ctype__> array, __ctype__ absoluteEps)
        {
            return array.Create__kd__Tree(Vec__ctypecap__.DistMax,
                                      /*# if (!isVp) { */(dim, a, b) => b - a, /*# } */absoluteEps);
        }

        public static Point__kd__Tree__cchar__<VecArray<__ctype__>, Vec<__ctype__>> Create__kd__Tree(
                this VecArray<__ctype__> array,
                Func<Vec<__ctype__>, Vec<__ctype__>, __ctype__> distanceFun,
                //# if (!isVp) {
                Func<long, __ctype__, __ctype__, __ctype__> dimMaxDistanceFun,
                //# }
                __ctype__ absoluteEps)
        {
            return new Point__kd__Tree__cchar__<VecArray<__ctype__>, Vec<__ctype__>>(
                    array.Dim, array.Count, array,
                    VecArray<__ctype__>.Getter, Vec<__ctype__>.Getter,
                    distanceFun,/*# if (!isVp) { */ dimMaxDistanceFun,/*# } */
                    /*# if (isRkd && t.IsReal) { */null, null, /*# } */absoluteEps);
        }

        public static Point__kd__Tree__cchar__<VecArray<__ctype__>, Vec<__ctype__>> Create__kd__Tree(
                this VecArray<__ctype__> array, Metric metric, __ctype__ absoluteEps,
                Point__kd__Tree__cchar__Data data)
        {
            switch (metric)
            {
                case Metric.Manhattan: return array.Create__kd__TreeDist1(absoluteEps, data);
                case Metric.Euclidean: return array.Create__kd__TreeDist2(absoluteEps, data);
                case Metric.Maximum: return array.Create__kd__TreeDistMax(absoluteEps, data);
                default: throw new ArgumentException();
            }
        }

        public static Point__kd__Tree__cchar__<VecArray<__ctype__>, Vec<__ctype__>> Create__kd__TreeDist1(
                this VecArray<__ctype__> array, __ctype__ absoluteEps, Point__kd__Tree__cchar__Data data)
        {
            return array.Create__kd__Tree(Vec__ctypecap__.Dist1,
                                      /*# if (!isVp) { */(dim, a, b) => b - a, /*# } */absoluteEps, data);
        }

        public static Point__kd__Tree__cchar__<VecArray<__ctype__>, Vec<__ctype__>> Create__kd__TreeDist2(
                this VecArray<__ctype__> array, __ctype__ absoluteEps, Point__kd__Tree__cchar__Data data)
        {
            return array.Create__kd__Tree(Vec__ctypecap__.Dist2,
                                      /*# if (!isVp) { */(dim, a, b) => b - a, /*# } */absoluteEps, data);
        }

        //# if (isKd) {
        public static Point__kd__Tree__cchar__<VecArray<__ctype__>, Vec<__ctype__>> Create__kd__TreeDist2Squared(
                this VecArray<__ctype__> array, __ctype__ absoluteEps, Point__kd__Tree__cchar__Data data)
        {
            return array.Create__kd__Tree((a, b) => __ccast__Vec__ctypecap__.Dist2Squared(a, b),
                                      /*# if (!isVp) { */(dim, a, b) => __ccast__Fun.Square(b - a), /*# } */absoluteEps, data);
        }

        //# } // isKd
        public static Point__kd__Tree__cchar__<VecArray<__ctype__>, Vec<__ctype__>> Create__kd__TreeDistMax(
                this VecArray<__ctype__> array, __ctype__ absoluteEps, Point__kd__Tree__cchar__Data data)
        {
            return array.Create__kd__Tree(Vec__ctypecap__.DistMax,
                                      /*# if (!isVp) { */(dim, a, b) => b - a, /*# } */absoluteEps, data);
        }

        public static Point__kd__Tree__cchar__<VecArray<__ctype__>, Vec<__ctype__>> Create__kd__Tree(
                this VecArray<__ctype__> array,
                Func<Vec<__ctype__>, Vec<__ctype__>, __ctype__> distanceFun,
                //# if (!isVp) {
                Func<long, __ctype__, __ctype__, __ctype__> dimMaxDistanceFun,
                //# }
                __ctype__ absoluteEps, Point__kd__Tree__cchar__Data data)
        {
            return new Point__kd__Tree__cchar__<VecArray<__ctype__>, Vec<__ctype__>>(
                    array.Dim, array.Count, array,
                    VecArray<__ctype__>.Getter, Vec<__ctype__>.Getter,
                    distanceFun,/*# if (!isVp) { */ dimMaxDistanceFun,/*# } */
                    /*# if (isRkd && t.IsReal) { */null, null, /*# } */absoluteEps, data);
        }

        #endregion

        #region __ctype__[] Trees

        public static Point__kd__Tree__cchar__<__ctype__[], __ctype__> Create__kd__Tree(
                this __ctype__[] array, Metric metric, __ctype__ absoluteEps)
        {
            return array.Create__kd__TreeDist1(absoluteEps);
        }

        public static Point__kd__Tree__cchar__<__ctype__[], __ctype__> Create__kd__TreeDist1(
                this __ctype__[] array, __ctype__ absoluteEps)
        {
            return array.Create__kd__Tree((a, b) => Fun.Abs(b - a),
                                      /*# if (!isVp) { */(dim, a, b) => b - a, /*# } */absoluteEps);
        }

        public static Point__kd__Tree__cchar__<__ctype__[], __ctype__> Create__kd__TreeDist2(
                this __ctype__[] array, __ctype__ absoluteEps)
        {
            return array.Create__kd__Tree((a, b) => Fun.Abs(b - a),
                                      /*# if (!isVp) { */(dim, a, b) => b - a, /*# } */absoluteEps);
        }

        public static Point__kd__Tree__cchar__<__ctype__[], __ctype__> Create__kd__TreeDistMax(
                this __ctype__[] array, __ctype__ absoluteEps)
        {
            return array.Create__kd__Tree((a, b) => Fun.Abs(b - a),
                                      /*# if (!isVp) { */(dim, a, b) => b - a, /*# } */absoluteEps);
        }

        public static Point__kd__Tree__cchar__<__ctype__[], __ctype__> Create__kd__Tree(
                this __ctype__[] array,
                Func<__ctype__, __ctype__, __ctype__> distanceFun,
                //# if (!isVp) {
                Func<long, __ctype__, __ctype__, __ctype__> dimMaxDistanceFun,
                //# }
                __ctype__ absoluteEps)
        {
            return new Point__kd__Tree__cchar__<__ctype__[], __ctype__>(
                    1, array.LongLength, array,
                    (a, ai) => a[ai], (v, vi) => v,
                    distanceFun,/*# if (!isVp) { */ dimMaxDistanceFun,/*# } */
                    /*# if (isRkd && t.IsReal) { */null, null, /*# } */absoluteEps);
        }

        #endregion

        //# foreach (var hasSel in new[] { false, true }) {
        //# if (hasSel && !isRkd) continue;
        //# var sel = hasSel ? "Selector" : "";
        //# var selArray = hasSel ? "Array" : "";
        //# var funArrayType = hasSel ? "[]" : "";
        //# var dimDistType = "Func<" + (hasSel ? "" : "long, ") + ctype + ", " + ctype + ", " + ctype + ">" + funArrayType;
        //# if (hasSel) {
        #region Constants
        private readonly static Func<__ctype__, __ctype__, __ctype__> c_dimDistFun__cchar__ = (a, b) => b - a;

        private readonly static Func<__ctype__, __ctype__, __ctype__>[] c_dimDistFunArray__cchar__ =
            new Func<__ctype__, __ctype__, __ctype__>[] { c_dimDistFun__cchar__, c_dimDistFun__cchar__, c_dimDistFun__cchar__, c_dimDistFun__cchar__ };

        private readonly static Func<__ctype__, __ctype__, __ctype__> c_dimDistConst1Fun__cchar__ = (a, b) => (__ctype__)1;

        private readonly static Func<__ctype__, __ctype__, __ctype__>[] c_dimDistConst1FunArray__cchar__ =
            new Func<__ctype__, __ctype__, __ctype__>[] { c_dimDistConst1Fun__cchar__, c_dimDistConst1Fun__cchar__, c_dimDistConst1Fun__cchar__, c_dimDistConst1Fun__cchar__ };

        #endregion

        //# } // hasSel
        //# foreach (var dim in Meta.VecTypeDimensions) {
        //#     var vt = Meta.VecTypeOf(dim, t);
        //#     var vtype = vt.Name;
        #region __vtype__[] __sel__ Trees

        public static Point__kd__Tree__cchar____sel__<__vtype__[], __vtype__> Create__kd__Tree__sel__(
                this __vtype__[] array, Metric metric, __ctype__ absoluteEps)
        {
            switch (metric)
            {
                case Metric.Manhattan: return array.Create__kd__Tree__sel__Dist1(absoluteEps);
                case Metric.Euclidean: return array.Create__kd__Tree__sel__Dist2(absoluteEps);
                case Metric.Maximum: return array.Create__kd__Tree__sel__DistMax(absoluteEps);
                default: throw new ArgumentException();
            }
        }

        public static Point__kd__Tree__cchar____sel__<__vtype__[], __vtype__> Create__kd__Tree__sel__Dist1(
                this __vtype__[] array, __ctype__ absoluteEps)
        {
            return array.Create__kd__Tree(__vtype__.Distance1,/*# if (!isVp) { if (hasSel) { */ c_dimDistFunArray__cchar__,/*# } else { */ (dim, a, b) => b - a,/*# } } */
                                       /*# if (isRkd && t.IsReal) { */null, null, /*# } */absoluteEps);
        }

        public static Point__kd__Tree__cchar____sel__<__vtype__[], __vtype__> Create__kd__Tree__sel__Dist2(
                this __vtype__[] array, __ctype__ absoluteEps)
        {
            return array.Create__kd__Tree(__vtype__.Distance,/*# if (!isVp) { if (hasSel) { */ c_dimDistFunArray__cchar__,/*# } else { */ (dim, a, b) => b - a,/*# } } */
                                       //# if (isRkd && t.IsReal) {
                                       VecFun.DistanceToLine, VecFun.Lerp,
                                       //# }
                                       absoluteEps);
        }

        //# if (isKd) {
        public static Point__kd__Tree__cchar____sel__<__vtype__[], __vtype__> Create__kd__Tree__sel__Dist2Squared(
                this __vtype__[] array, __ctype__ absoluteEps)
        {
            return array.Create__kd__Tree((a, b) => __ccast____vtype__.DistanceSquared(a, b),
                                          (dim, a, b) => __ccast__Fun.Square(b - a), absoluteEps);
        }

        //# } // isKd 
        public static Point__kd__Tree__cchar____sel__<__vtype__[], __vtype__> Create__kd__Tree__sel__DistMax(
                this __vtype__[] array, __ctype__ absoluteEps)
        {
            return array.Create__kd__Tree(__vtype__.DistanceMax,/*# if (!isVp) { if (hasSel) { */ c_dimDistFunArray__cchar__,/*# } else { */ (dim, a, b) => b - a,/*# } } */
                                       /*# if (isRkd && t.IsReal) { */null, null, /*# } */absoluteEps);
        }

        public static Point__kd__Tree__cchar____sel__<__vtype__[], __vtype__> Create__kd__Tree__sel__DistDotProduct(
                this __vtype__[] array, __ctype__ absoluteEps)
        {
            return array.Create__kd__Tree(
                (a, b) => (__ctype__)1.0 - Fun.Abs(__vtype__.Dot(a, b)),
                //# if (!isVp) {
                /*# if (hasSel) { */c_dimDistConst1FunArray__cchar__,/*# } else { */(dim, a, b) => 1,/*# } */
                //# }
                /*# if (isRkd && t.IsReal) { */null, null, /*# } */absoluteEps);
        }

        public static Point__kd__Tree__cchar____sel__<__vtype__[], __vtype__> Create__kd__Tree(
                this __vtype__[] array,
                Func<__vtype__, __vtype__, __ctype__> distanceFun,
                //# if (!isVp) {
                __dimDistType__ dimMaxDistanceFun__selArray__,
                //# }
                //# if (isRkd && t.IsReal) {
                Func<__vtype__, __vtype__, __vtype__, __ctype__> lineDistFun,
                Func<__ctype__, __vtype__, __vtype__, __vtype__> lerpFun,
                //# }
                __ctype__ absoluteEps)
        {
            return new Point__kd__Tree__cchar____sel__<__vtype__[], __vtype__>(
                    __dim__, array.Length, array,
                    (a, ai) => a[ai], /*# if (hasSel) { */__vtype__.SelectorArray, /*# } else { */__vtype__.LongGetter, /*# } */
                    distanceFun,/*# if (!isVp) { */ dimMaxDistanceFun__selArray__,/*# } */
                    /*# if (isRkd && t.IsReal) { */lineDistFun, lerpFun, /*# } */absoluteEps);
        }

        #endregion

        //# } // foreach hasSel
        //# } // foreach dim
        //# } // foreach t
    }

    //# } // foreach kd
}
