/*
    Copyright (C) 2006-2022. Aardvark Platform Team. http://github.com/aardvark-platform.
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
    [Flags] 
    public enum SplitterOptions
    {
        None = 0,
        Negative = 0x01,
        Positive = 0x02,
        NegativeAndPositive = 0x03,
    }

    public class TriangleSplitter
    {
        #region Private Fields

        /// <summary>
        /// New vertices on the split plane, belonging to the negative
        /// split side.
        /// </summary>
        private readonly Dictionary<int, Line1iPoint> m_negVertexMap;

        /// <summary>
        /// New vertices on the split plane, belonging to the positive
        /// split side.
        /// </summary>
        private readonly Dictionary<int, Line1iPoint> m_posVertexMap;

        /// <summary>
        /// If this is null, the result does not contain a negative part.
        /// </summary>
        private readonly int[] m_negativeIndices;

        /// <summary>
        /// If this is null, the result does not contain a positive part.
        /// </summary>
        private readonly int[] m_positiveIndices;

        /// <summary>
        /// If this is null, when <see cref="m_negativeIndices"/> is not null,
        /// the whole input is on the negative side.
        /// </summary>
        private readonly int[] m_negativeVertexBackwardMap;

        /// <summary>
        /// If this is null, when <see cref="m_positiveIndices"/> is not null,
        /// the whole input is on the positive side.
        /// </summary>
        private readonly int[] m_positiveVertexBackwardMap;

        #endregion

        #region Constructor (Main Algorithm)

        /// <summary>
        /// A splitter can split a vertex-indexed triangle set, based on the
        /// supplied array of vertexHeights, which indicate for each vertex
        /// vi if it belongs to the negative split side (vertexHeights[vi]
        /// &lt; 0.0) or positive split side (vertexHeights[vi] &gt;= 0.0).
        /// </summary>
        public TriangleSplitter(int[] indices, double[] vertexHeights, double eps,
                        SplitterOptions options)
        {
            m_negativeIndices = null;
            m_positiveIndices = null;

            bool doNeg = (options & SplitterOptions.Negative) != 0;
            bool doPos = (options & SplitterOptions.Positive) != 0;

            if (doPos && vertexHeights.All(h => h >= -eps))
            {
                m_positiveIndices = indices;
                m_positiveVertexBackwardMap = null; // flag: move input to positive
                return;
            }
            if (doNeg && vertexHeights.All(h => h <= eps))
            {
                m_negativeIndices = indices;
                m_negativeVertexBackwardMap = null; // flag: move input to negative
                return;
            }

            var lineMap = new Dictionary<Line1i, Line1i>();
            var negTriangleList = doNeg ? new List<(int, Triangle1i)>() : null;
            var posTriangleList = doPos ? new List<(int, Triangle1i)>() : null;

            m_negVertexMap = doNeg ? new Dictionary<int, Line1iPoint>() : null;
            m_posVertexMap = doPos ? new Dictionary<int, Line1iPoint>() : null;

            int tc = indices.Length/3;

            int ntc = 0;
            int ptc = 0;

            int nvc = 0;
            int pvc = 0;

            int[] negTriangleForwardMap = doNeg ? new int[tc].Set(-1) : null;
            int[] posTriangleForwardMap = doPos ? new int[tc].Set(-1) : null;

            int[] negVertexForwardMap = doNeg ? new int[vertexHeights.Length].Set(-1) : null;
            int[] posVertexForwardMap = doPos ? new int[vertexHeights.Length].Set(-1) : null;

            var ia = new int[3];
            var ha = new double[3];
            var negIndices = new List<int>(4);
            var posIndices = new List<int>(4);

            for (int ti = 0; ti < tc; ti ++)
            {
                int nc = 0, pc = 0;
                int tvi = 3 * ti;

                for (int ts = 0; ts < 3; ts++)
                {
                    int vi = indices[tvi + ts];
                    ia[ts] = vi;
                    double height = ha[ts] = vertexHeights[vi];
                    if (height < -eps) { ++nc; continue; }
                    if (height > +eps) { ++pc; continue; }
                }

                if (nc == 0)
                {
                    if (doPos)
                    {
                        posTriangleForwardMap[ti] = ptc++;
                        foreach (var vi in ia) posVertexForwardMap.ForwardMapAdd(vi, ref pvc);
                    }
                }
                else if (pc == 0)
                {
                    if (doNeg)
                    {
                        negTriangleForwardMap[ti] = ntc++;
                        foreach (var vi in ia) negVertexForwardMap.ForwardMapAdd(vi, ref nvc);
                    }
                }
                else
                {
                    int sb = ha[0] > eps ? 1 : (ha[0] < -eps ? -1 : 0);

                    int vi = ia[0];
                    if (doNeg && sb <= 0)
                        negIndices.Add(negVertexForwardMap.ForwardMapAdd(vi, ref nvc));
                    if (doPos && sb >= 0)
                        posIndices.Add(posVertexForwardMap.ForwardMapAdd(vi, ref pvc));

                    int i0 = 0, i1 = 1;
                    int s0 = sb;

                    while (i1 < 3)
                    {
                        int s1 = ha[i1] > eps ? 1 : (ha[i1] < -eps ? -1 : 0);

                        if (s0 != 0 && s1 != 0 && s0 != s1)
                        {
                            double t = ha[i0] / (ha[i0] - ha[i1]);
                            int vi0 = ia[i0], vi1 = ia[i1];
                            var key = Line1i.CreateSorted(vi0, vi1);
                            if (!lineMap.TryGetValue(key, out Line1i line))
                            {
                                var sp = new Line1iPoint(vi0, vi1, t);
                                if (doNeg) m_negVertexMap[line.I0 = nvc++] = sp;
                                if (doPos) m_posVertexMap[line.I1 = pvc++] = sp;
                                lineMap[key] = line;
                            }
                            if (doNeg) negIndices.Add(line.I0);
                            if (doPos) posIndices.Add(line.I1);
                        }

                        vi = ia[i1];
                        if (doNeg && s1 <= 0)
                            negIndices.Add(negVertexForwardMap.ForwardMapAdd(vi, ref nvc));
                        if (doPos && s1 >= 0)
                            posIndices.Add(posVertexForwardMap.ForwardMapAdd(vi, ref pvc));

                        i0 = i1++;  s0 = s1;
                    }
                    if (s0 != 0 && sb != 0 && s0 != sb)
                    {
                        double t = ha[i0] / (ha[i0] - ha[0]);
                        int vi0 = ia[i0], vi1 = ia[0];
                        var key = Line1i.CreateSorted(vi0, vi1);
                        if (!lineMap.TryGetValue(key, out Line1i line))
                        {
                            var sp = new Line1iPoint(vi0, vi1, t);
                            if (doNeg) m_negVertexMap[line.I0 = nvc++] = sp;
                            if (doPos) m_posVertexMap[line.I1 = pvc++] = sp;
                            lineMap[key] = line;
                        }
                        if (doNeg) negIndices.Add(line.I0);
                        if (doPos) posIndices.Add(line.I1);
                    }

                    // at this point we have lists of indices for the positive and
                    // negative triangles
                    if (doNeg && negIndices.Count > 2)
                        for (int i = 1; i < negIndices.Count - 1; i++)
                            negTriangleList.Add(
                                (ntc++, new Triangle1i(negIndices[0],
                                             negIndices[i], negIndices[i + 1])));
                    if (doPos && posIndices.Count > 2)
                        for (int i = 1; i < posIndices.Count - 1; i++)
                            posTriangleList.Add(
                                (ptc++, new Triangle1i(posIndices[0],
                                             posIndices[i], posIndices[i + 1])));
                    negIndices.Clear(); posIndices.Clear();
                }
            }

            if (doNeg && ntc > 0)
            {
                m_negativeIndices = CalculateIndices(indices, ntc, negTriangleForwardMap,
                                                   negTriangleList, negVertexForwardMap);
                m_negativeVertexBackwardMap = negVertexForwardMap.CreateBackMap(nvc);
            }
            if (doPos && ptc > 0)
            {
                m_positiveIndices = CalculateIndices(indices, ptc, posTriangleForwardMap,
                                                   posTriangleList, posVertexForwardMap);
                m_positiveVertexBackwardMap = posVertexForwardMap.CreateBackMap(pvc);
            }
        }

        /// <summary>
        /// Calculate new array of indices based on the results of the split
        /// algorithm.
        /// </summary>
        private int[] CalculateIndices(
                int[] oldIndices,
                int newTriangleCount,
                int[] triangleForwardMap,
                List<(int, Triangle1i)> triangleList,
                int[] vertexForwardMap)
        {
            var nia = new int[3 * newTriangleCount];
            int tc = oldIndices.Length / 3;

            for (int ti = 0; ti < tc; ti++)
            {
                int nti = triangleForwardMap[ti];
                if (nti < 0) continue;
                for (int ts = 0; ts < 3; ts++)
                {
                    int vi = oldIndices[3 * ti + ts];
                    int ntvi = 3 * nti + ts;
                    nia[ntvi] = vertexForwardMap[vi];
                }
            }
            foreach (var t in triangleList)
            {
                int ntvi = 3 * t.Item1;
                nia[ntvi] = t.Item2.I0;
                nia[ntvi + 1] = t.Item2.I1;
                nia[ntvi + 2] = t.Item2.I2;
            }
            return nia;
        }

        #endregion

        #region Result Query Functions

        /// <summary>
        /// Get the indices of negative (side == 0) or positive (side == 1) part.
        /// </summary>
        public int[] Indices(int side)
        {
            if (side == 0) return m_negativeIndices;
            if (side == 1) return m_positiveIndices;
            return null;
        }

        /// <summary>
        /// Get the backward map of negative (side == 0) or positive (side == 1) part.
        /// If this is null when the corresponding indices are not null, the whole
        /// input is on the corresponding side.
        /// </summary>
        public int[] VertexBackwardMap(int side)
        {
            if (side == 0) return m_negativeVertexBackwardMap;
            if (side == 1) return m_positiveVertexBackwardMap;
            return null;
        }

        #endregion

        #region Attribute Array Splitters

        public Array SplitOf(Array array, int side)
        {
            var backwardMap = VertexBackwardMap(side);
            if (backwardMap == null) return array;
            return array.BackwardIndexedLerpCopy(backwardMap, VertexMap(side));
        }

        public Array NormalSplitOf(Array array, int side)
        {
            var backwardMap = VertexBackwardMap(side);
            if (backwardMap == null) return array;
            return array.BackwardIndexedNormalCopy(backwardMap, VertexMap(side));
        }

        Dictionary<int, Line1iPoint> VertexMap(int side)
        {
            if (side == 0) return m_negVertexMap;
            if (side == 1) return m_posVertexMap;
            return null;
        }

        #endregion
    }

    public class PointSplitter
    {
        #region Private Fields

        /// <summary>
        /// If this is null, the result does not contain a negative part.
        /// </summary>
        private readonly V3f[] m_negativePositions;

        /// <summary>
        /// If this is null, the result does not contain a positive part.
        /// </summary>
        private readonly V3f[] m_positivePositions;

        #endregion

        #region Constructor (Main Algorithm)

        /// <summary>
        /// A splitter can split a point-list, based on the
        /// supplied array of vertexHeights, which indicate for each vertex
        /// vi if it belongs to the negative split side (vertexHeights[vi]
        /// &lt; 0.0) or positive split side (vertexHeights[vi] >= 0.0).
        /// </summary>
        public PointSplitter(V3f[] positions, double[] vertexHeights, double eps,
                        SplitterOptions options)
        {
            m_negativePositions = null;
            m_positivePositions = null;

            bool doNeg = (options & SplitterOptions.Negative) != 0;
            bool doPos = (options & SplitterOptions.Positive) != 0;

            if (doPos && vertexHeights.Count(h => h > -eps) == vertexHeights.Length)
            {
                m_positivePositions = positions;  // move input to positive
                return;
            }
            if (doNeg && vertexHeights.Count(h => h < eps) == vertexHeights.Length)
            {
                m_negativePositions = positions;  // move input to negative
                return;
            }

            var vc = positions.Length;
            var negCount = 0;
            var posCount = 0;

            m_positivePositions = new V3f[vertexHeights.Count(h => h >= 0)];
            m_negativePositions = new V3f[vertexHeights.Count(h => h  < 0)];

            for (int vi = 0; vi < vc; vi++)
            {
                double height = vertexHeights[vi];
                if (height < 0)
                {
                    m_negativePositions[negCount++] = positions[vi];
                }
                else if (height >= 0)
                {
                    m_positivePositions[posCount++] = positions[vi];
                }
            }
        }

        #endregion

        #region Result Query Functions

        /// <summary>
        /// Get the vertex positions of negative (side == 0) or positive (side == 1) part.
        /// </summary>
        public V3f[] VertexPositions(int side)
        {
            if (side == 0) return m_negativePositions;
            if (side == 1) return m_positivePositions;
            return null;
        }

        #endregion
    }

    public static class IEnumerableSplitterExtensions
    {
        #region Generic Splitting Extensions

        public static (List<T>, List<T>) SplitOnPlane<T>(
                this IEnumerable<T> items,
                Plane3d plane, double epsilon, SplitterOptions options,
                Func<T, Plane3d, double, SplitterOptions, (T, T)> splitOnPlane)
        {
            var negativeList = new List<T>();
            var positiveList = new List<T>();

            foreach (var item in items)
            {
                var pair = splitOnPlane(item, plane, epsilon, options);
                if (pair.Item1 != null) negativeList.Add(pair.Item1);
                if (pair.Item2 != null) positiveList.Add(pair.Item2);
            }
            return (negativeList, positiveList);
        }

        internal static void SplitIntoParallelPlanes<T>(
                List<T> itemList, V3d point, V3d normal, V3d delta,
                int index, int count, double epsilon,
                Func<T, Plane3d, double, SplitterOptions, (T, T)> splitOnPlane,
                List<T>[] result)
        {
            if (count == 1) { result[index] = itemList; return; }
            int rightCount = count / 2;
            int leftCount = count - rightCount;
            V3d p = point + (index + leftCount) * delta;
            var itemSplit = SplitOnPlane(itemList, new Plane3d(normal, p), epsilon,
                                         SplitterOptions.NegativeAndPositive,
                                         splitOnPlane);
            SplitIntoParallelPlanes(itemSplit.Item1, point, normal, delta,
                                    index, leftCount, epsilon, splitOnPlane, result);
            SplitIntoParallelPlanes(itemSplit.Item2, point, normal, delta,
                                    index + leftCount, rightCount, epsilon, splitOnPlane, result);
        }

        public static List<T>[] SplitOnParallelPlanes<T>(
                this IEnumerable<T> items,
                V3d point, V3d normal, V3d delta, int count, double epsilon,
                Func<T, Plane3d, double, SplitterOptions, (T, T)> splitOnPlane)
        {
            var result = new List<T>[count];
            SplitIntoParallelPlanes(items.ToList(), point, normal, delta,
                                    0, count, epsilon, splitOnPlane, result);
            return result;
        }

        public static Volume<List<T>> SplitOnGrid<T>(
                this IEnumerable<T> items,
                V3d origin, V3d cellSize, V3i gridSize, double eps,
                Func<T, Plane3d, double, SplitterOptions, (T, T)> splitOnPlane)
        {
            var result = new Volume<List<T>>(gridSize);

            var itemList = items.ToList();

            V3d dx = new V3d(cellSize.X, 0, 0), nx = dx.Normalized;
            V3d dy = new V3d(0, cellSize.Y, 0), ny = dy.Normalized;
            V3d dz = new V3d(0, 0, cellSize.Z), nz = dz.Normalized;

            List<T>[] itemsX = null;
            List<T>[] itemsY = null;
            List<T>[] itemsZ = itemList.SplitOnParallelPlanes(origin, nz, dz, gridSize.Z, eps, splitOnPlane); ;

            result.ForeachZYXIndex(
                z => itemsY = itemsZ[z].SplitOnParallelPlanes(origin, ny, dy, gridSize.Y, eps, splitOnPlane),
                (z, y) => itemsX = itemsY[y].SplitOnParallelPlanes(origin, nx, dx, gridSize.X, eps, splitOnPlane),
                (z, y, x, i) => result[i] = itemsX[x]);

            return result;
        }

        #endregion
    }
}
