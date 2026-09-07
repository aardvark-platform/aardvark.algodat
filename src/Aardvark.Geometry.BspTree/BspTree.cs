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
using Aardvark.Base.Coder;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TypeInfo = Aardvark.Base.Coder.TypeInfo;

#pragma warning disable IDE0130 // Namespace does not match folder structure

namespace Aardvark.Geometry
{
    /// <summary>
    /// In order to avoid inserting duplicate split points, each split
    /// point is represented by an object of this class, and identified
    /// by its reference.
    /// </summary>
    public class BspSplitPoint(V3d point, int tvi, double parameter)
    {
        public V3d Position { get; } = point;
        public int TriangleVertexIndex { get; } = tvi;
        public double Parameter { get; } = parameter;
        public int VertexIndex { get; set; } = -1;
    }

    /// <summary>
    /// A BspTree holds a triangle vertex index array and a tree of BspNodes
    /// that allow the sorting of the triangle vertex index array based on
    /// an eye point. On a finalized tree, both sorting APIs support trees
    /// built with or without triangle attribute indices, without modifying
    /// the tree. Concurrent sorts must use separate output buffers.
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
        /// Sorts triangle vertex indices on a finalized tree, whether or not
        /// it contains triangle attribute indices. The vertex output agrees
        /// with <see cref="SortVertexAndAttributeIndexArrays"/>.
        /// </summary>
        /// <param name="order">The requested triangle order.</param>
        /// <param name="eye">The viewpoint used for sorting.</param>
        /// <param name="vertexIndexArray">Output with at least
        /// <see cref="BspTreeBuilder.TriangleCountMul3"/> entries, using the
        /// finalized count (including split fragments). Extra entries are untouched.</param>
        /// <param name="parallel">Whether to sort asynchronously using worker tasks.</param>
        /// <returns>A completion event that must be awaited before reading the output
        /// and disposed afterward. For serial execution it is already signalled.</returns>
        public CountdownEvent SortVertexIndexArray(
            Order order, V3d eye, int[] vertexIndexArray, bool parallel = true)
        {
            // Packing with attributes stores triangle ordinals; without attributes
            // it stores vertex-index offsets. The output API does not determine this.
            if (m_triangleAttributeIndexArray != null)
                return SortVertexAndAttributeIndexArrays(order, eye, vertexIndexArray, null, parallel);

            return SortVertexIndexArray(order, eye, new TargetArray(vertexIndexArray), parallel);
        }

        private CountdownEvent SortVertexIndexArray(
            Order order, V3d eye, TargetArray target, bool parallel)
        {
            static void runParallel(Action a) => Task.Run(a);
            static void runSequential(Action a) => a();
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

        /// <summary>
        /// Sorts aligned triangle vertex and attribute indices on a finalized tree.
        /// Either packed layout is supported. Supplied attribute IDs follow their
        /// triangles, including split fragments; trees built without attributes
        /// write zero for every output triangle.
        /// </summary>
        /// <param name="order">The requested triangle order.</param>
        /// <param name="eye">The viewpoint used for sorting.</param>
        /// <param name="vertexIndexArray">Output with at least
        /// <see cref="BspTreeBuilder.TriangleCountMul3"/> entries.</param>
        /// <param name="attributeIndexArray">Output with at least
        /// <see cref="BspTreeBuilder.TriangleCountMul3"/> / 3 entries, or null to
        /// omit attributes. Both output sizes use the finalized count, including
        /// split fragments. Extra entries are untouched.</param>
        /// <param name="parallel">Whether to sort asynchronously using worker tasks.</param>
        /// <returns>A completion event that must be awaited before reading the output
        /// and disposed afterward. For serial execution it is already signalled.</returns>
        public CountdownEvent SortVertexAndAttributeIndexArrays(
            Order order, V3d eye, int[] vertexIndexArray, int[] attributeIndexArray, bool parallel = true)
        {
            var target = new TargetArrays(vertexIndexArray, attributeIndexArray);

            if (m_triangleAttributeIndexArray == null)
                return SortVertexIndexArray(order, eye, target, parallel);

            static void runParallel(Action a) => Task.Run(a);
            static void runSequential(Action a) => a();
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
        static readonly TypeInfo[] s_typeInfoArray =
        [
            new TypeInfo("n", typeof(TypeCoder.Null),   TypeInfo.Option.Active),
            new TypeInfo("b", typeof(BspNode),          TypeInfo.Option.None),
        ];

        public IEnumerable<FieldCoder> GetFieldCoders(int coderVersion)
        {
            return
            [
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
            ];
        }
        #endregion
    }

    /// <summary>
    /// A BspTreeBuilder is only used while building a BspTree, after
    /// it has been built, it can be retrieved via the property
    /// <see cref="BspTree"/>. Afterward the BspTreeBuilder is not needed
    /// anymore.
    /// </summary>
    public class BspTreeBuilder : BspTree
    {
        /// <summary>Whether triangle attribute indices were supplied to the builder.</summary>
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
            HasAttributeArray = triangleAttributeIndexArray != null;
            m_weightsArray = [];
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

        /// <summary>
        /// Finalized triangle count multiplied by three, including split fragments.
        /// Use this for vertex-index output sizing and divide by three for attribute output.
        /// </summary>
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
        /// BspTree. Sorting supports both output APIs regardless of whether
        /// attributes were supplied. Keep <see cref="TriangleCountMul3"/> for
        /// output sizing and <see cref="PositionArray"/> for split vertices.
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
    /// Per-sort buffers, published to worker tasks with immutable references.
    /// Each node writes a disjoint range; completion synchronizes output reads.
    /// </summary>
    internal class TargetArray(int[] via)
    {
        public readonly CountdownEvent Finished = new(1);
        public readonly int[] Via = via;
    }

    internal class TargetArrays(int[] via, int[] aia) : TargetArray(via)
    {
        public readonly int[] Aia = aia;
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
            m_zeroList = [tiMul3];
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
            V3d n = Vec.Cross(e0, e1);
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
            var htr = (Vec.Dot(m_normal, tr.P0 - m_point),
                       Vec.Dot(m_normal, tr.P1 - m_point),
                       Vec.Dot(m_normal, tr.P2 - m_point));
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
                        double t = -startHeight / Vec.Dot(m_normal,
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
                    double t = -startHeight / Vec.Dot(m_normal,
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

        // Keep task closures out of recursive traversal methods: only scheduled
        // subtrees need a closure, not every visited node. Index units still come
        // from the stored layout, independently of the requested output buffers.
        private void ScheduleSort(BspTree tree, TargetArray target, int index, V3d eye,
            bool backToFront, Action<Action> runChild)
        {
            target.Finished.AddCount(1);
            runChild(() =>
            {
                if (tree.m_triangleAttributeIndexArray == null)
                {
                    if (backToFront) SortBackToFront(tree, target, index, eye, false, runChild);
                    else SortFrontToBack(tree, target, index, eye, false, runChild);
                }
                else
                {
                    var arrays = (TargetArrays)target;
                    if (backToFront) SortBackToFront(tree, arrays, index, eye, false, runChild);
                    else SortFrontToBack(tree, arrays, index, eye, false, runChild);
                }
                target.Finished.Signal();
            });
        }

        // The layout-specific copy loops share both sort orders and cache immutable
        // buffer references. Node indices need not be assumed contiguous.
        private void CopyIndices(BspTree tree, TargetArray target, int index3)
        {
            var source = tree.m_triangleVertexIndexArray;
            var vertices = target.Via;
            int start3 = index3;
            int count = m_zeroList.Count;
            for (int i = 0; i < count; i++)
            {
                int source3 = m_zeroList[i];
                vertices[index3] = source[source3];
                vertices[index3 + 1] = source[source3 + 1];
                vertices[index3 + 2] = source[source3 + 2];
                index3 += 3;
            }
            // Clear just this node's output during the same traversal. Vertex-only
            // calls need neither a dummy attribute array nor a clearing pass.
            if (target is TargetArrays arrays && arrays.Aia != null)
                Array.Clear(arrays.Aia, start3 / 3, count);
        }

        private void CopyIndices(BspTree tree, TargetArrays target, int index)
        {
            var source = tree.m_triangleVertexIndexArray;
            var sourceAttributes = tree.m_triangleAttributeIndexArray;
            var vertices = target.Via;
            var attributes = target.Aia;
            int index3 = index * 3;
            int count = m_zeroList.Count;
            for (int i = 0; i < count; i++)
            {
                int sourceIndex = m_zeroList[i];
                int source3 = sourceIndex * 3;
                vertices[index3] = source[source3];
                vertices[index3 + 1] = source[source3 + 1];
                vertices[index3 + 2] = source[source3 + 2];
                if (attributes != null) attributes[index] = sourceAttributes[sourceIndex];
                index3 += 3;
                index++;
            }
        }

        internal void SortBackToFront(
            BspTree t, TargetArray via,
            int ti3, V3d eye, bool mainTask, Action<Action> runChild)
        {
            int nti3 = ti3;
            double height = Vec.Dot(m_normal, eye - m_point);
            if (height >= 0.0)
            {
                ti3 += m_negativeCount;
                CopyIndices(t, via, ti3);
                ti3 += m_zeroList.Count * 3;
            }
            else
            {
                nti3 += m_positiveCount;
                CopyIndices(t, via, nti3);
                nti3 += m_zeroList.Count * 3;
            }

            if (m_negativeTree != null)
            {
                if (mainTask && m_negativeCount < c_taskChunkSize)
                {
                    m_negativeTree.ScheduleSort(t, via, nti3, eye, true, runChild);
                }
                else
                    m_negativeTree.SortBackToFront(
                        t, via, nti3, eye, mainTask, runChild);
            }
            if (m_positiveTree != null)
            {
                if (mainTask && m_positiveCount < c_taskChunkSize)
                {
                    m_positiveTree.ScheduleSort(t, via, ti3, eye, true, runChild);
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
            double height = Vec.Dot(m_normal, eye - m_point);
            if (height < 0.0)
            {
                ti3 += m_negativeCount;
                CopyIndices(t, via, ti3);
                ti3 += m_zeroList.Count * 3;
            }
            else
            {
                nti3 += m_positiveCount;
                CopyIndices(t, via, nti3);
                nti3 += m_zeroList.Count * 3;
            }

            if (m_positiveTree != null)
            {
                if (mainTask && m_positiveCount < c_taskChunkSize)
                {
                    m_positiveTree.ScheduleSort(t, via, ti3, eye, false, runChild);
                }
                else
                    m_positiveTree.SortFrontToBack(
                        t, via, ti3, eye, mainTask, runChild);
            }
            if (m_negativeTree != null)
            {
                if (mainTask && m_negativeCount < c_taskChunkSize)
                {
                    m_negativeTree.ScheduleSort(t, via, nti3, eye, false, runChild);
                }
                else
                    m_negativeTree.SortFrontToBack(
                        t, via, nti3, eye, mainTask, runChild);
            }
        }

        internal void SortBackToFront(
            BspTree t, TargetArrays target,
            int ti, V3d eye, bool mainTask, Action<Action> runChild)
        {
            int nti = ti;
            double height = Vec.Dot(m_normal, eye - m_point);
            if (height >= 0.0)
            {
                ti += m_negativeCount;
                CopyIndices(t, target, ti);
                ti += m_zeroList.Count;
            }
            else
            {
                nti += m_positiveCount;
                CopyIndices(t, target, nti);
                nti += m_zeroList.Count;
            }
            if (m_negativeTree != null)
            {
                if (mainTask && m_negativeCount < c_taskChunkSize)
                {
                    m_negativeTree.ScheduleSort(t, target, nti, eye, true, runChild);
                }
                else
                    m_negativeTree.SortBackToFront(
                        t, target, nti, eye, mainTask, runChild);
            }
            if (m_positiveTree != null)
            {
                if (mainTask && m_positiveCount < c_taskChunkSize)
                {
                    m_positiveTree.ScheduleSort(t, target, ti, eye, true, runChild);
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
            double height = Vec.Dot(m_normal, eye - m_point);
            if (height < 0.0)
            {
                ti += m_negativeCount;
                CopyIndices(t, target, ti);
                ti += m_zeroList.Count;
            }
            else
            {
                nti += m_positiveCount;
                CopyIndices(t, target, nti);
                nti += m_zeroList.Count;
            }
            if (m_positiveTree != null)
            {
                if (mainTask && m_positiveCount < c_taskChunkSize)
                {
                    m_positiveTree.ScheduleSort(t, target, ti, eye, false, runChild);
                }
                else
                    m_positiveTree.SortFrontToBack(
                        t, target, ti, eye, mainTask, runChild);
            }
            if (m_negativeTree != null)
            {
                if (mainTask && m_negativeCount < c_taskChunkSize)
                {
                    m_negativeTree.ScheduleSort(t, target, nti, eye, false, runChild);
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
            return
            [
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
            ];
        }

        #endregion
    }
}
