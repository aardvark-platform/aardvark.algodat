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
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TypeInfo = Aardvark.Base.Coder.TypeInfo;

namespace Aardvark.Geometry
{
    /// <summary>
    /// In order to avoid inserting duplicate split points, each split
    /// point is represented by an object of this class, and identified
    /// by its reference.
    /// </summary>
    public class BspSplitPoint
    {
        #region Constructor

        public BspSplitPoint(V3d point, int tvi, double parameter)
        {
            Position = point;
            TriangleVertexIndex = tvi;
            Parameter = parameter;
            VertexIndex = -1;
        }

        #endregion

        #region Properties

        public V3d Position { get; }
        public int TriangleVertexIndex { get; }
        public double Parameter { get; }
        public int VertexIndex { get; set; }

        #endregion
    }

    /// <summary>
    /// A BspTree holds a triangle vertex index array and a tree of BspNodes
    /// that allow the sorting of the triangle vertex index array based on
    /// an eye point.
    /// </summary>
    [RegisterTypeInfo]
    public class BspTree : IFieldCodeable
    {
        public enum Order
        {
            BackToFront = 0,
            FrontToBack = 1
        }

        internal BspNode m_tree;
        internal int[] m_triangleVertexIndexArray;
        internal int[] m_triangleAttributeIndexArray;

        #region Constructor

        public BspTree()
        {
        }

        internal BspTree(BspNode tree,
                         int[] triangleVertexIndexArray,
                         int[] triangleAttributeIndexArray)
        {
            m_tree = tree;
            m_triangleVertexIndexArray = triangleVertexIndexArray;
            m_triangleAttributeIndexArray = triangleAttributeIndexArray;
        }

        #endregion

        #region Triangle Access

        internal void GetTriangleVertexIndices(
            int tiMul3, out int i0, out int i1, out int i2)
        {
            i0 = m_triangleVertexIndexArray[tiMul3];
            i1 = m_triangleVertexIndexArray[tiMul3 + 1];
            i2 = m_triangleVertexIndexArray[tiMul3 + 2];
        }

        #endregion

        #region Sorting An Array

        /// <summary>
        /// Sorts index array (optionally single threaded)
        /// Both variants return a CountdownEvent. For
        /// sequential execution (parallel = false), the
        /// returned event initially is signalled.
        /// </summary>
        public CountdownEvent SortVertexIndexArray(
            Order order, V3d eye, int[] vertexIndexArray, bool parallel = true)
        {
            var target = new TargetArray()
            {
                Via = vertexIndexArray,
                Finished = new CountdownEvent(1)
            };

            Action<Action> runParallel = a => Task.Factory.StartNew(a);
            Action<Action> runSequential = a => a();
            Action<Action> runChild = parallel ? runParallel : runSequential;
                        

            if (order == Order.BackToFront)
                runChild(() =>
                {
                    m_tree.SortBackToFront(this, target, 0, eye, true, runChild);
                    target.Finished.Signal();
                });
            else
                runChild(() =>
                {
                    m_tree.SortFrontToBack(this, target, 0, eye, true, runChild);
                    target.Finished.Signal();
                });

            if (!parallel) target.Finished.Wait();

			return target.Finished;
        }

		public CountdownEvent SortVertexAndAttributeIndexArrays(
            Order order, V3d eye, int[] vertexIndexArray, int[] attributeIndexArray, bool parallel = true)
        {
            var target = new TargetArrays()
            {
                // Count = 0,
                Via = vertexIndexArray,
                Aia = attributeIndexArray,
                Finished = new CountdownEvent(1)
            };

            Action<Action> runParallel = a => Task.Factory.StartNew(a);
            Action<Action> runSequential = a => a();
            Action<Action> runChild = parallel ? runParallel : runSequential;

            if (order == Order.BackToFront)
                runChild(() =>
                {
                    m_tree.SortBackToFront(this, target, 0, eye, true, runChild);
                    target.Finished.Signal();
                });
            else
                runChild(() =>
                {
                    m_tree.SortFrontToBack(this, target, 0, eye, true, runChild);
                    target.Finished.Signal();
                });
			return target.Finished;
        }

        #endregion

        #region IFieldCodeable Members

        /// <summary>
        /// For compact storage we use short names for all node types, and do
        /// not store any sizes and version number for the nodes.
        /// </summary>
        static readonly TypeInfo[] s_typeInfoArray = new[]
        {
            new TypeInfo("n", typeof(TypeCoder.Null),   TypeInfo.Option.Active),
            new TypeInfo("b", typeof(BspNode),          TypeInfo.Option.None),
        };

        public IEnumerable<FieldCoder> GetFieldCoders(int coderVersion)
        {
            return new[]
            {
                new FieldCoder(0, "Tree",
                        (c,o) =>
                        {
                            // deactivating references avoids creating large
                            // unused tables during coding of pure trees
                            c.Add(TypeCoder.Default.NoReference);
                            c.Add(s_typeInfoArray);
                            c.CodeT(ref ((BspTree)o).m_tree);
                            c.Del(s_typeInfoArray);
                            c.Del(TypeCoder.Default.NoReference);
                        } ),
                new FieldCoder(1, "Indices",
                        (c,o) => c.CodeIntArray(ref ((BspTree)o).m_triangleVertexIndexArray) ),
                new FieldCoder(2, "AttributeIndices",
                        (c,o) => c.CodeIntArray(ref ((BspTree)o).m_triangleAttributeIndexArray) ),
            };
        }
        #endregion
    }

    /// <summary>
    /// A BspTreeBuilder is only used while building a BspTree, after
    /// it has been built, it can be retrived via the property 
    /// <see cref="BspTree"/>. Afterward the BspTreeBuilder is not needed
    /// anymore.
    /// </summary>
    public class BspTreeBuilder : BspTree
    {
        public readonly bool HasAttributeArray;

        private readonly int m_originalVertexCount;

        internal double m_absoluteEpsilon;

        private V3d[] m_positionArray;
        private WeightedIndex[][] m_weightsArray;

        #region Constructor

        /// <summary>
        /// In order to build a BspTree, you have to supply the
        /// BspTreeBuilder with index an position arrays that are copies of
        /// the originals, so that they can be modified during the build
        /// process. The supplied epsilon parameter specifies the absolute
        /// tolerance value for coplanar triangles.
        /// </summary>
        public BspTreeBuilder(int[] triangleVertexIndexArray,
                              V3d[] vertexPositionArray,
                              double absoluteEpsilon)
            : this(triangleVertexIndexArray, vertexPositionArray, absoluteEpsilon, null)
        { }

        /// <summary>
        /// In order to build a BspTree, you have to supply the
        /// BspTreeBuilder with index an position arrays that are copies of
        /// the originals, so that they can be modified during the build
        /// process. The supplied epsilon parameter specifies the absolute
        /// tolerance value for coplanar triangles.
        /// </summary>
        public BspTreeBuilder(int[] triangleVertexIndexArray,
                              V3d[] vertexPositionArray,
                              double absoluteEpsilon,
                              int[] triangleAttributeIndexArray)
            : base(null, triangleVertexIndexArray, triangleAttributeIndexArray)
        {
            m_weightsArray = new WeightedIndex[0][];
            m_positionArray = vertexPositionArray;

            TriangleCountMul3 = m_triangleVertexIndexArray.Length;
            int triangleCount = TriangleCountMul3 / 3;
            VertexCount = m_positionArray.Length;

            m_absoluteEpsilon = absoluteEpsilon;
            m_originalVertexCount = VertexCount;

            // simple shuffle-algorithm (imagine the array as a square
            // from left->right, top->bottom)
            // address the array now top->bottom, left->right
            int stride = (int)Fun.Ceiling(Fun.Sqrt(triangleCount + 1.0));
            for (int offset = 0; offset < stride; offset++)
                for (int ti = offset; ti < triangleCount; ti += stride)
                    BspNode.AddTriangle(this, ti * 3, ref m_tree);

            TriangleCountMul3 = m_tree.TriangleCount() * 3;
        }

        #endregion

        #region Properties

        public int TriangleCountMul3 { get; private set; }

        public int VertexCount { get; private set; }

        public V3d[] PositionArray => m_positionArray;

        public WeightedIndex[][] WeightsArray => m_weightsArray;

        #endregion

        #region Triangle Set Accessors

        internal void GetTriangleVertexPositions(int tiMul3, out V3d p0, out V3d p1, out V3d p2)
        {
            p0 = m_positionArray[m_triangleVertexIndexArray[tiMul3]];
            p1 = m_positionArray[m_triangleVertexIndexArray[tiMul3 + 1]];
            p2 = m_positionArray[m_triangleVertexIndexArray[tiMul3 + 2]];
        }

        #endregion

        #region Creating the BspTree

        /// <summary>
        /// This property should only be read once to obtain a finalized
        /// BspTree.
        /// </summary>
        public BspTree BspTree
        {
            get
            {
                var via = new int[TriangleCountMul3];
                if (m_triangleAttributeIndexArray != null)
                {
                    var aia = new int[TriangleCountMul3/3];
                    m_tree.PackIndices(this, via, aia, 0);
                    return new BspTree(m_tree, via, aia);
                }
                else
                {
                    m_tree.PackIndices(this, via, 0);
                    return new BspTree(m_tree, via, null);
                }
            }
        }

        #endregion

        #region Adding Items To Arrays

        internal void EnsureVertexCapacity(int count)
        {
            int capacity = m_positionArray.Length;
            if (count > capacity)
            {
                while (count > capacity) capacity = 2 * capacity;
                Array.Resize(ref m_positionArray, capacity);
                Array.Resize(ref m_weightsArray, capacity - m_originalVertexCount);
            }
        }

        internal void EnsureTriangleCapacity(int triangleCountMul3)
        {
            int capacity = m_triangleVertexIndexArray.Length;
            if (triangleCountMul3 > capacity)
            {
                while (triangleCountMul3 > capacity) capacity = 2 * capacity;
                Array.Resize(ref m_triangleVertexIndexArray, capacity);
                if (m_triangleAttributeIndexArray != null)
                    Array.Resize(ref m_triangleAttributeIndexArray, capacity / 3);
            }
        }

        internal void AddSplitPoint(int tiMul3, BspSplitPoint sp)
        {
            if (sp.VertexIndex >= 0) return; // already inserted

            if (sp.Parameter == 0.0)
            {
                sp.VertexIndex = m_triangleVertexIndexArray[tiMul3 + sp.TriangleVertexIndex];
                return;
            }

            int vi = VertexCount;
            int vc = vi + 1;
            EnsureVertexCapacity(vc);
            VertexCount = vc;
            int vi0 = m_triangleVertexIndexArray[tiMul3 + sp.TriangleVertexIndex];
            int vi1 = m_triangleVertexIndexArray[tiMul3 + (sp.TriangleVertexIndex + 1) % 3];
            V3d p0 = m_positionArray[vi0];

            m_positionArray[vi] = p0 + sp.Parameter * (m_positionArray[vi1] - p0);

            int wc = 0;
            int dvi0 = vi0 - m_originalVertexCount;
            wc += dvi0 < 0 ? 1 : m_weightsArray[dvi0].Length;
            int dvi1 = vi1 - m_originalVertexCount;
            wc += dvi1 < 0 ? 1 : m_weightsArray[dvi1].Length;

            var weights = new WeightedIndex[wc];
            var invParam = 1.0 - sp.Parameter;

            wc = 0;
            if (dvi0 < 0)
                weights[wc++] = new WeightedIndex(invParam, vi0);
            else
                foreach (var widx in m_weightsArray[dvi0])
                    weights[wc++] =
                        new WeightedIndex(invParam * widx.Weight, widx.Index);
            if (dvi1 < 0)
                weights[wc++] = new WeightedIndex(sp.Parameter, vi1);
            else
                foreach (var widx in m_weightsArray[dvi1])
                    weights[wc++] =
                        new WeightedIndex(sp.Parameter * widx.Weight, widx.Index);

            m_weightsArray[vi - m_originalVertexCount] = weights;
            sp.VertexIndex = vi;
        }

        internal int AddClonedTriangle(int tiMul3,
                BspSplitPoint sp0, BspSplitPoint sp1, BspSplitPoint sp2)
        {
            AddSplitPoint(tiMul3, sp0);
            AddSplitPoint(tiMul3, sp1);
            AddSplitPoint(tiMul3, sp2);

            int ntiMul3 = TriangleCountMul3;
            int tcMul3 = ntiMul3 + 3;
            EnsureTriangleCapacity(tcMul3);
            TriangleCountMul3 = tcMul3;

            m_triangleVertexIndexArray[ntiMul3] = sp0.VertexIndex;
            m_triangleVertexIndexArray[ntiMul3 + 1] = sp1.VertexIndex;
            m_triangleVertexIndexArray[ntiMul3 + 2] = sp2.VertexIndex;

            if (m_triangleAttributeIndexArray != null)
            {
                m_triangleAttributeIndexArray[ntiMul3 / 3] =
                    m_triangleAttributeIndexArray[tiMul3 / 3];
            }

            return ntiMul3;
        }

        #endregion

    }

    /// <summary>
    /// Volatile array for parallel bsp sorting.
    /// </summary>
    internal class TargetArray
    {
        public CountdownEvent Finished;
        public volatile int[] Via;
    }

    internal class TargetArrays
    {
        public CountdownEvent Finished;
        public volatile int[] Via;
        public volatile int[] Aia;
    }

    internal class BspNode : IFieldCodeable
    {
        V3d m_point;
        V3d m_normal;
        List<int> m_zeroList;
        int m_negativeCount;
        BspNode m_negativeTree;
        int m_positiveCount;
        BspNode m_positiveTree;

        #region Constructor

        public BspNode()
        {
        }

        internal BspNode(int tiMul3, V3d point, V3d normal)
        {
            m_point = point;
            m_normal = normal;
            m_zeroList = new List<int>(1) { tiMul3 };
            m_negativeCount = 0;
            m_positiveCount = 0;
            m_positiveTree = null;
            m_negativeTree = null;
        }

        #endregion

        #region Building

        internal static void AddTriangle(
            BspTreeBuilder builder, int tiMul3, ref BspNode node)
        {
            Triangle3d tr;
            builder.GetTriangleVertexPositions(tiMul3, out tr.P0, out tr.P1, out tr.P2);

            V3d e0 = tr.P1 - tr.P0;
            V3d e1 = tr.P2 - tr.P0;
            V3d n = V3d.Cross(e0, e1);
            double len2 = n.LengthSquared;
            if (len2 > 0.0)
                AddTriangle(builder, tiMul3, ref tr,
                            n * (1.0 / Math.Sqrt(len2)),
                            ref node);
        }

        internal static void AddTriangle(
            BspTreeBuilder builder, int tiMul3, ref Triangle3d tr, V3d triangleNormal,
            ref BspNode node)
        {
            if (node != null)
                node.AddTriangle(builder, tiMul3, ref tr, triangleNormal);
            else
                node = new BspNode(tiMul3, tr.P0, triangleNormal);
        }

        internal void AddTriangle(
            BspTreeBuilder builder, int tiMul3, ref Triangle3d tr, V3d normal)
        {
            var htr = (V3d.Dot(m_normal, tr.P0 - m_point),
                       V3d.Dot(m_normal, tr.P1 - m_point),
                       V3d.Dot(m_normal, tr.P2 - m_point));
            var signs = new[] { htr.Item1, htr.Item2, htr.Item3 }.AggregateSigns(builder.m_absoluteEpsilon);

            if (signs == Signs.Zero)
            {
                m_zeroList.Add(tiMul3);
            }
            else if ((signs & Signs.Negative) == Signs.None)
                AddTriangle(builder, tiMul3, ref tr, normal, ref m_positiveTree);
            else if ((signs & Signs.Positive) == Signs.None)
                AddTriangle(builder, tiMul3, ref tr, normal, ref m_negativeTree);
            else
            {
                // the triangle straddles the separating plane

                var positivePoints = new List<BspSplitPoint>(4);
                var negativePoints = new List<BspSplitPoint>(4);
                V3d firstPoint = tr.P0;
                double firstHeight = htr.Item1;
                bool firstPositive = firstHeight > 0.0;

                if (firstPositive)
                    positivePoints.Add(new BspSplitPoint(firstPoint, 0, 0.0));
                else
                    negativePoints.Add(new BspSplitPoint(firstPoint, 0, 0.0));

                V3d startPoint = firstPoint;
                double startHeight = firstHeight;
                bool startPositive = firstPositive;

                int start = 0;
                int end = 1;

                while (end < 3)
                {
                    V3d endPoint = tr[end];
                    double endHeight = htr.Get(end);
                    bool endPositive = endHeight > 0.0;

                    if (startPositive != endPositive)
                    {
                        V3d direction = endPoint - startPoint;
                        double t = -startHeight / V3d.Dot(m_normal,
                                                    direction);
                        V3d newPoint = startPoint + t * direction;

                        // note, that the same split point (reference!) is
                        // added to both lists!

                        var sp = new BspSplitPoint(newPoint, start, t);
                        positivePoints.Add(sp);
                        negativePoints.Add(sp);
                    }

                    if (endPositive)
                        positivePoints.Add(new BspSplitPoint(endPoint, end, 0.0));
                    else
                        negativePoints.Add(new BspSplitPoint(endPoint, end, 0.0));

                    start = end;
                    startPoint = endPoint;
                    startHeight = endHeight;
                    startPositive = endPositive;
                    end++;
                }
                if (startPositive != firstPositive)
                {
                    V3d direction = firstPoint - startPoint;
                    double t = -startHeight / V3d.Dot(m_normal,
                                                direction);
                    V3d newPoint = startPoint + t * direction;

                    var sp = new BspSplitPoint(newPoint, start, t);
                    positivePoints.Add(sp);
                    negativePoints.Add(sp);
                }

                // in order to ensure that all fragments of a triangle are
                // consecutively stored, we walk through the two point lists
                // twice. for this we need a store of the triangle indices

                int[] positiveIndices = new int[2];
                int[] negativeIndices = new int[2];

                // first pass: generate the cloned triangles (fragments) and
                // the resulting triangle indices

                if (positivePoints.Count > 2)
                    for (int i = 1; i < positivePoints.Count - 1; i++)
                        positiveIndices[i - 1] = builder.AddClonedTriangle(tiMul3,
                                positivePoints[0],
                                positivePoints[i],
                                positivePoints[i + 1]);
                if (negativePoints.Count > 2)
                    for (int i = 1; i < negativePoints.Count - 1; i++)
                        negativeIndices[i - 1] = builder.AddClonedTriangle(tiMul3,
                                negativePoints[0],
                                negativePoints[i],
                                negativePoints[i + 1]);

                // second pass: add the fragments (with the triangle
                // indices) to the BSP-tree

                if (positivePoints.Count > 2)
                    for (int i = 0; i < positivePoints.Count - 2; i++)
                        AddTriangle(builder, positiveIndices[i], ref m_positiveTree);
                if (negativePoints.Count > 2)
                    for (int i = 0; i < negativePoints.Count - 2; i++)
                        AddTriangle(builder, negativeIndices[i], ref m_negativeTree);
            }
        }

        #endregion

        #region Packing

        internal int TriangleCount()
        {
            int count = m_zeroList.Count;
            if (m_positiveTree != null)
                count += m_positiveTree.TriangleCount();
            if (m_negativeTree != null)
                count += m_negativeTree.TriangleCount();
            return count;
        }

        internal int PackIndices(BspTreeBuilder tb, int[] via, int ti3)
        {
            for (int i = 0; i < m_zeroList.Count; i++)
            {
                tb.GetTriangleVertexIndices(m_zeroList[i],
                    out via[ti3], out via[ti3 + 1], out via[ti3 + 2]);
                m_zeroList[i] = ti3;
                ti3 += 3;
            }
            if (m_negativeTree != null)
            {
                var nti3 = m_negativeTree.PackIndices(tb, via, ti3);
                m_negativeCount = nti3 - ti3;
                ti3 = nti3;
            }
            if (m_positiveTree != null)
            {
                var nti3 = m_positiveTree.PackIndices(tb, via, ti3);
                m_positiveCount = nti3 - ti3;
                ti3 = nti3;
            }
            return ti3;
        }

        internal int PackIndices(BspTreeBuilder tb, int[] via, int[] aia, int ti)
        {
            for (int i = 0; i < m_zeroList.Count; i++)
            {
                var bti3 = m_zeroList[i];
                var ti3 = 3 * ti;
                tb.GetTriangleVertexIndices(bti3,
                    out via[ti3], out via[ti3 + 1], out via[ti3 + 2]);
                m_zeroList[i] = ti;
                aia[ti++] = tb.m_triangleAttributeIndexArray[bti3 / 3];
            }
            if (m_negativeTree != null)
            {
                var nti = m_negativeTree.PackIndices(tb, via, aia, ti);
                m_negativeCount = nti - ti;
                ti = nti;
            }
            if (m_positiveTree != null)
            {
                var nti = m_positiveTree.PackIndices(tb, via, aia, ti);
                m_positiveCount = nti - ti;
                ti = nti;
            }
            return ti;
        }

        #endregion

        #region Sorting

        private const int c_taskChunkSize = 32768;

        /// <summary>
        /// Note, that the parallel implementation of this method could be
        /// optimized, by providing separate node types for parallel and non
        /// parallel execution.
        /// </summary>
        internal void SortBackToFront(
            BspTree t, TargetArray via,
            int ti3, V3d eye, bool mainTask, Action<Action> runChild)
        {
            int nti3 = ti3;
            double height = V3d.Dot(m_normal, eye - m_point);
            if (height >= 0.0)
            {
                ti3 += m_negativeCount;
                foreach (int tiMul3 in m_zeroList)
                {
                    t.GetTriangleVertexIndices(tiMul3,
                        out via.Via[ti3],
                        out via.Via[ti3 + 1],
                        out via.Via[ti3 + 2]);
                    ti3 += 3;
                }
            }
            else
            {
                nti3 += m_positiveCount;
                foreach (int tiMul3 in m_zeroList)
                {
                    t.GetTriangleVertexIndices(tiMul3,
                        out via.Via[nti3],
                        out via.Via[nti3 + 1],
                        out via.Via[nti3 + 2]);
                    nti3 += 3;
                }
            }
            if (m_negativeTree != null)
            {
                if (mainTask && m_negativeCount < c_taskChunkSize)
                {
                    // via.Count += 1;
					via.Finished.AddCount(1);
                    runChild(() =>
                    {
                        m_negativeTree.SortBackToFront(
                            t, via, nti3, eye, false, runChild);
                        via.Finished.Signal();
                    });
                }
                else
                    m_negativeTree.SortBackToFront(
                        t, via, nti3, eye, mainTask, runChild);
            }
            if (m_positiveTree != null)
            {
                if (mainTask && m_positiveCount < c_taskChunkSize)
                {
                    // via.Count += 1;
					via.Finished.AddCount(1);
                    runChild(() =>
                    {
                        m_positiveTree.SortBackToFront(
                            t, via, ti3, eye, false, runChild);
                        via.Finished.Signal();
                    });
                }
                else
                    m_positiveTree.SortBackToFront(
                        t, via, ti3, eye, mainTask, runChild);
            }
        }

        internal void SortFrontToBack(
            BspTree t, TargetArray via,
            int ti3, V3d eye, bool mainTask, Action<Action> runChild)
        {
            int nti3 = ti3;
            double height = V3d.Dot(m_normal, eye - m_point);
            if (height < 0.0)
            {
                ti3 += m_negativeCount;
                foreach (int tiMul3 in m_zeroList)
                {
                    t.GetTriangleVertexIndices(tiMul3,
                        out via.Via[ti3],
                        out via.Via[ti3 + 1],
                        out via.Via[ti3 + 2]);
                    ti3 += 3;
                }
            }
            else
            {
                nti3 += m_positiveCount;
                foreach (int tiMul3 in m_zeroList)
                {
                    t.GetTriangleVertexIndices(tiMul3,
                        out via.Via[nti3],
                        out via.Via[nti3 + 1],
                        out via.Via[nti3 + 2]);
                    nti3 += 3;
                }
            }
            if (m_positiveTree != null)
            {
                if (mainTask && m_positiveCount < c_taskChunkSize)
                {
					via.Finished.AddCount(1);
                    runChild(() =>
                    {
                        m_positiveTree.SortFrontToBack(
                            t, via, ti3, eye, false, runChild);
                        via.Finished.Signal();
                    });
                }
                else
                    m_positiveTree.SortFrontToBack(
                        t, via, ti3, eye, mainTask, runChild);
            }
            if (m_negativeTree != null)
            {
                if (mainTask && m_negativeCount < c_taskChunkSize)
                {
					via.Finished.AddCount(1);
                    runChild(() =>
                    {
                        m_negativeTree.SortFrontToBack(
                            t, via, nti3, eye, false, runChild);
                        via.Finished.Signal();
                    });
                }
                else
                    m_negativeTree.SortFrontToBack(
                        t, via, nti3, eye, mainTask, runChild);
            }
        }

        /// <summary>
        /// Note, that the parallel implementation of this method could be
        /// optimized, by providing separate node types for parallel and non
        /// parallel execution.
        /// </summary>
        internal void SortBackToFront(
            BspTree t, TargetArrays target,
            int ti, V3d eye, bool mainTask, Action<Action> runChild)
        {
            int nti = ti;
            double height = V3d.Dot(m_normal, eye - m_point);
            if (height >= 0.0)
            {
                ti += m_negativeCount;
                var ti3 = ti * 3;
                foreach (int oti in m_zeroList)
                {
                    t.GetTriangleVertexIndices(oti * 3,
                        out target.Via[ti3],
                        out target.Via[ti3 + 1],
                        out target.Via[ti3 + 2]);
                    ti3 += 3;
                    target.Aia[ti] = (t.m_triangleAttributeIndexArray != null) ? t.m_triangleAttributeIndexArray[oti] : 0;
                    ti++;
                }
            }
            else
            {
                nti += m_positiveCount;
                var nti3 = nti * 3;
                foreach (int oti in m_zeroList)
                {
                    t.GetTriangleVertexIndices(oti * 3,
                        out target.Via[nti3],
                        out target.Via[nti3 + 1],
                        out target.Via[nti3 + 2]);
                    nti3 += 3;
                    target.Aia[nti] = (t.m_triangleAttributeIndexArray != null) ? t.m_triangleAttributeIndexArray[oti] : 0;
                    nti++;
                }
            }
            if (m_negativeTree != null)
            {
                if (mainTask && m_negativeCount < c_taskChunkSize)
                {
					target.Finished.AddCount(1);
                    runChild(() => 
                    { 
                        m_negativeTree.SortBackToFront(
                            t, target, nti, eye, false, runChild);
                        target.Finished.Signal();
                    });
                }
                else
                    m_negativeTree.SortBackToFront(
                        t, target, nti, eye, mainTask, runChild);
            }
            if (m_positiveTree != null)
            {
                if (mainTask && m_positiveCount < c_taskChunkSize)
                {
					target.Finished.AddCount(1);
                    runChild(() =>
                    {
                        m_positiveTree.SortBackToFront(
                            t, target, ti, eye, false, runChild);
                        target.Finished.Signal();
                    });
                }
                else
                    m_positiveTree.SortBackToFront(
                        t, target, ti, eye, mainTask, runChild);
            }
        }

        internal void SortFrontToBack(
            BspTree t, TargetArrays target,
            int ti, V3d eye, bool mainTask, Action<Action> runChild)
        {
            int nti = ti;
            double height = V3d.Dot(m_normal, eye - m_point);
            if (height < 0.0)
            {
                ti += m_negativeCount;
                var ti3 = ti * 3;
                foreach (int oti in m_zeroList)
                {
                    t.GetTriangleVertexIndices(oti * 3,
                        out target.Via[ti3],
                        out target.Via[ti3 + 1],
                        out target.Via[ti3 + 2]);
                    ti3 += 3;
                    target.Aia[ti] = (t.m_triangleAttributeIndexArray != null) ? t.m_triangleAttributeIndexArray[oti] : 0;
                    ti++;
                }
            }
            else
            {
                nti += m_positiveCount;
                var nti3 = nti * 3;
                foreach (int oti in m_zeroList)
                {
                    t.GetTriangleVertexIndices(oti * 3,
                        out target.Via[nti3],
                        out target.Via[nti3 + 1],
                        out target.Via[nti3 + 2]);
                    nti3 += 3;
                    target.Aia[nti] = (t.m_triangleAttributeIndexArray != null) ? t.m_triangleAttributeIndexArray[oti] : 0;
                    nti++;
                }
            }
            if (m_positiveTree != null)
            {
                if (mainTask && m_positiveCount < c_taskChunkSize)
                {
					target.Finished.AddCount(1);
                    runChild (() => 
                    {
                        m_positiveTree.SortFrontToBack(
                            t, target, ti, eye, false, runChild);
                        target.Finished.Signal();
                    });
                }
                else
                    m_positiveTree.SortFrontToBack(
                        t, target, ti, eye, mainTask, runChild);
            }
            if (m_negativeTree != null)
            {
                if (mainTask && m_negativeCount < c_taskChunkSize)
                {
					target.Finished.AddCount(1);
                    runChild(() =>
                    {
                        m_negativeTree.SortFrontToBack(
                            t, target, nti, eye, false, runChild);
                        target.Finished.Signal();
                    }
                    );
                }
                else
                    m_negativeTree.SortFrontToBack(
                        t, target, nti, eye, mainTask, runChild);
            }
        }

        #endregion

        #region IFieldCodeable Members

        public IEnumerable<FieldCoder> GetFieldCoders(int coderVersion)
        {
            return new[]
            {
                new FieldCoder(0, "Point",
                    (c,o) => c.CodeV3d(ref ((BspNode)o).m_point) ),
                new FieldCoder(1, "Normal",
                    (c,o) => c.CodeV3d(ref ((BspNode)o).m_normal) ),
                new FieldCoder(2, "ZeroList",
                    (c,o) => c.CodeList_of_Int_(ref ((BspNode)o).m_zeroList) ),
                new FieldCoder(3, "PosCount",
                    (c,o) => c.CodeInt(ref ((BspNode)o).m_positiveCount) ),
                new FieldCoder(4, "PosTree",
                    (c,o) => c.CodeT(ref ((BspNode)o).m_positiveTree) ),
                new FieldCoder(5, "NegCount",
                    (c,o) => c.CodeInt(ref ((BspNode)o).m_negativeCount) ),
                new FieldCoder(6, "NegTree",
                    (c,o) => c.CodeT(ref ((BspNode)o).m_negativeTree) ),
            };
        }

        #endregion
    }
}
