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
using Aardvark.Data;
using Aardvark.Data.Points;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// An immutable point cloud octree node.
    /// </summary>
    public interface IPointCloudNodeOld
    {
        /// <summary>
        /// Backing store.
        /// </summary>
        Storage Storage { get; }

        /// <summary>
        /// True, if this node is in store.
        /// </summary>
        bool IsMaterialized { get; }

        /// <summary>
        /// True, if this is Node.Empty.
        /// </summary>
        bool IsEmpty { get; }

        /// <summary>
        /// Returns materialized version of this node.
        /// E.g. a non-materialized filtered node is converted into a PointSetNode (which is stored in Storage).
        /// </summary>
        IPointCloudNodeOld Materialize();

        /// <summary>
        /// Writes this node to store.
        /// </summary>
        IPointCloudNodeOld WriteToStore();

        /// <summary>
        /// </summary>
        byte[] Encode();

        /// <summary>
        /// Key.
        /// </summary>
        Guid Id { get; }

        /// <summary>
        /// Returns true if this a temporary import node (without computed properties like kd-tree).
        /// </summary>
        bool IsTemporaryImportNode { get; }

        /// <summary>
        /// This node's index/bounds.
        /// </summary>
        Cell Cell { get; }

        /// <summary>
        /// Center of this node's cell.
        /// </summary>
        V3d Center { get; }

        /// <summary>
        /// Octree. Number of points in this cell.
        /// Durable definition 172e1f20-0ffc-4d9c-9b3d-903fca41abe3.
        /// </summary>
        int PointCountCell { get; }

        /// <summary>
        /// Number of points in this tree (sum of leaf points).
        /// </summary>
        long PointCountTree { get; }

        /// <summary>
        /// Subnodes (8), or null if leaf.
        /// Entries are null if there is no subnode.
        /// There is at least 1 non-null entry.
        /// </summary>
        PersistentRef<IPointCloudNodeOld>?[]? Subnodes { get; }

        /// <summary>
        /// Returns new node with replaced subnodes.
        /// </summary>
        IPointCloudNodeOld WithSubNodes(IPointCloudNodeOld?[] subnodes);

        /// <summary>
        /// Node has no subnodes.
        /// </summary>
        [MemberNotNullWhen(false, nameof(Subnodes))]
        bool IsLeaf { get; }

        /// <summary>
        /// Returns true if node has given property.
        /// </summary>
        bool Has(Durable.Def what);

        /// <summary>
        /// Gets given property, or returns false if node has no such property.
        /// </summary>
        bool TryGetValue(Durable.Def what, [NotNullWhen(true)] out object? o);

        /// <summary>
        /// Gets all properties.
        /// </summary>
        IReadOnlyDictionary<Durable.Def, object> Properties { get; }

        /// <summary>
        /// Returns new node (with new id) with added/replaced data.
        /// Node is NOT written to store.
        /// Call WriteToStore on the result if you want this.
        /// </summary>
        IPointCloudNodeOld With(IReadOnlyDictionary<Durable.Def, object> replacements);

        #region Positions

        /// <summary>
        /// Octree.PositionsLocal3f.
        /// </summary>
        bool HasPositions { get; }

        /// <summary>
        /// Octree.PositionsLocal3f.
        /// Octree. Per-point positions in local cell space (as offsets from cell's center).
        /// Durable definition 05eb38fa-1b6a-4576-820b-780163199db9.
        /// </summary>
        PersistentRef<V3f[]> Positions { get; }

        /// <summary>
        /// Octree. Per-point positions in global space.
        /// Durable definition 61ef7c1e-6aeb-45cd-85ed-ad0ed2584553.
        /// </summary>
        V3d[] PositionsAbsolute { get; }

        #endregion

        #region BoundingBoxExactLocal

        /// <summary>
        /// Durable definition aadbb622-1cf6-42e0-86df-be79d28d6757.
        /// </summary>
        [MemberNotNullWhen(true, nameof(BoundingBoxExactLocal))]
        bool HasBoundingBoxExactLocal { get; }

        /// <summary>
        /// Octree. Exact bounding box of this node's positions. Local space. Box3f.
        /// Durable definition aadbb622-1cf6-42e0-86df-be79d28d6757.
        /// </summary>
        Box3f BoundingBoxExactLocal { get; }

        #endregion

        #region BoundingBoxExactGlobal

        /// <summary>
        /// Durable definition 7912c862-74b4-4f44-a8cd-d11ea1da9304.
        /// </summary>
        [MemberNotNullWhen(true, nameof(BoundingBoxExactGlobal))]
        bool HasBoundingBoxExactGlobal { get; }

        /// <summary>
        /// Octree. Exact bounding box of this tree's positions. Global space. Box3d.
        /// Durable definition 7912c862-74b4-4f44-a8cd-d11ea1da9304.
        /// </summary>
        Box3d BoundingBoxExactGlobal { get; }

        #endregion

        /// <summary>
        /// </summary>
        Box3d BoundingBoxApproximate { get; }

        #region KdTree

        /// <summary></summary>
        [MemberNotNullWhen(true, nameof(KdTree))]
        bool HasKdTree { get; }

        /// <summary>
        /// </summary>
        PersistentRef<PointRkdTreeF<V3f[], V3f>>? KdTree { get; }

        #endregion

        #region Colors

        /// <summary></summary>
        [MemberNotNullWhen(true, nameof(Colors))]
        bool HasColors { get; }

        /// <summary>
        /// Octree. Per-point colors C4b.
        /// Durable definition c91dfea3-243d-4272-9dba-b572931dba23.
        /// </summary>
        PersistentRef<C4b[]>? Colors { get; }

        #endregion

        #region Normals

        /// <summary></summary>
        [MemberNotNullWhen(true, nameof(Normals))]
        bool HasNormals { get; }

        /// <summary>
        /// Octree. Per-point V3f normals.
        /// Durable definition 712d0a0c-a8d0-42d1-bfc7-77eac2e4a755.
        /// </summary>
        PersistentRef<V3f[]>? Normals { get; }

        #endregion

        #region Intensities

        /// <summary></summary>
        [MemberNotNullWhen(true, nameof(Intensities))]
        bool HasIntensities { get; }

        /// <summary>
        /// Octree. Per-point int32 intensities..
        /// Durable definition 361027fd-ac58-4de8-89ee-98695f8c5520.
        /// </summary>
        PersistentRef<int[]>? Intensities { get; }

        #endregion

        #region Classifications

        /// <summary></summary>

        [MemberNotNullWhen(true, nameof(Classifications))]
        bool HasClassifications { get; }

        /// <summary>
        /// Octree. Per-point uint8 classifications.
        /// Durable definition d25cff0e-ea80-445b-ab72-d0a5a1013818.
        /// </summary>
        PersistentRef<byte[]>? Classifications { get; }

        #endregion

        #region PartIndices

        /// <summary>
        /// True if this node has a PartIndexRange.
        /// </summary>
        [MemberNotNullWhen(true, nameof(PartIndexRange))]
        bool HasPartIndexRange { get; }

        /// <summary>
        /// Octree. Min and max part index in octree.
        /// </summary>
        Range1i? PartIndexRange { get; }

        /// <summary>
        /// True if this node has part indices.
        /// </summary>
        [MemberNotNullWhen(true, nameof(PartIndices))]
        bool HasPartIndices { get; }

        /// <summary>
        /// Octree. Per-point or per-cell part indices.
        /// </summary>

        object? PartIndices { get; }

        /// <summary>
        /// Get per-point part indices as an int array (regardless of internal representation).
        /// Returns false if node has no part indices.
        /// </summary>
        bool TryGetPartIndices([NotNullWhen(true)] out int[]? result);

        #endregion

        #region Velocities

        /// <summary>
        /// Deprecated. Always returns false. Use custom attributes instead.
        /// </summary>
        [Obsolete("Use custom attributes instead.")]
        bool HasVelocities { get; }

        /// <summary>
        /// Deprecated. Always returns null. Use custom attributes instead.
        /// </summary>
        [Obsolete("Use custom attributes instead.")]
        PersistentRef<V3f[]>? Velocities { get; }

        #endregion

        #region CentroidLocal

        /// <summary></summary>
        bool HasCentroidLocal { get; }

        /// <summary>
        /// Octree. Centroid of positions (local space).
        /// Durable definition bd6cc4ab-6a41-49b3-aca2-ca4f21510609.
        /// </summary>
        V3f CentroidLocal { get; }

        /// <summary></summary>
        bool HasCentroidLocalStdDev { get; }

        /// <summary>
        /// Octree. Standard deviation of average point distance to centroid (local space).
        /// Durable definition c927d42b-02d8-480e-be93-0660eefd62a5.
        /// </summary>
        float CentroidLocalStdDev { get; }

        #endregion

        #region TreeDepth

        /// <summary></summary>
        bool HasMinTreeDepth { get; }

        /// <summary></summary>
        int MinTreeDepth { get; }

        /// <summary></summary>
        bool HasMaxTreeDepth { get; }

        /// <summary></summary>
        int MaxTreeDepth { get; }

        #endregion

        #region PointDistance

        /// <summary></summary>
        bool HasPointDistanceAverage { get; }

        /// <summary>
        /// Average distance of points in this cell.
        /// </summary>
        float PointDistanceAverage { get; }

        /// <summary></summary>
        bool HasPointDistanceStandardDeviation { get; }

        /// <summary>
        /// Standard deviation of distance of points in this cell.
        /// </summary>
        float PointDistanceStandardDeviation { get; }

        #endregion
    }


    public struct PointKdTree
    {
        private PointRkdTreeF<V3f[], V3f> m_tree;
        private V3d m_offset;

        public PointRkdTreeF<V3f[], V3f> Tree => m_tree;
        public V3d Offset => m_offset;

        public PointKdTree(PointRkdTreeF<V3f[], V3f> tree, V3d offset)
        {
            m_tree = tree;
            m_offset = offset;
        }
    }

    public static class PointNodeAttributes
    {
        // let Positions = Symbol.Create "Positions"
        // let Normals = Symbol.Create "Normals"
        // let Colors = Symbol.Create "Colors"
        public static Symbol Positions = Symbol.Create("Positions");
        public static Symbol Normals = Symbol.Create("Normals");
        public static Symbol Colors = Symbol.Create("Colors");
        public static Symbol Intensities = Symbol.Create("Intensities");
        public static Symbol Classifications = Symbol.Create("Classifications");
        public static Symbol PartIndices = Symbol.Create("PartIndices");

    }


    public interface IPointNode
    {

        Box3d CellBounds { get; }
        Box3d DataBounds { get; }

        /// <summary>
        /// global world-space positions of the points
        /// </summary>
        V3d[] Positions { get; }

        PointKdTree? KdTree { get; }

        bool TryGetAttribute(Symbol name, out Array data);


        IPointNode[] Children { get; }
    }

    public class PointNodeAdapter : IPointNode
    {
        private IPointCloudNodeOld m_node;

        public PointNodeAdapter(IPointCloudNodeOld node)
        {
            m_node = node;
        }

        public IPointCloudNodeOld OriginalNode => m_node;
        public Box3d CellBounds => m_node.Cell.BoundingBox;

        public Box3d DataBounds =>
            m_node.HasBoundingBoxExactGlobal ? m_node.BoundingBoxExactGlobal : m_node.Cell.BoundingBox;

        public V3d[] Positions => m_node.PositionsAbsolute;

        public PointKdTree? KdTree
        {
            get
            {
                if (m_node.HasKdTree)
                {
                    var kdTreeF = m_node.KdTree.Value;
                    return new PointKdTree(kdTreeF, m_node.Center);
                }

                return null;
            }
        }

        public bool TryGetAttribute(Symbol attName, out Array data)
        {
            if (attName == PointNodeAttributes.Positions)
            {
                data = m_node.PositionsAbsolute;
                return true;
            }

            if (attName == PointNodeAttributes.Normals && m_node.HasNormals)
            {
                data = m_node.Normals!.Value;
                return true;
            }

            if (attName == PointNodeAttributes.Colors && m_node.HasColors)
            {
                data = m_node.Colors!.Value;
                return true;
            }

            if (attName == PointNodeAttributes.Intensities && m_node.HasIntensities)
            {
                data = m_node.Intensities!.Value;
                return true;
            }

            if (attName == PointNodeAttributes.Classifications && m_node.HasClassifications)
            {
                data = m_node.Classifications!.Value;
                return true;
            }

            if (attName == PointNodeAttributes.PartIndices && m_node.HasPartIndices)
            {
                data = PartIndexUtils.Expand(m_node.PartIndices, m_node.PointCountCell)!;
                return true;
            }

            data = null!;
            return false;
        }

        public IPointNode[] Children
        {
            get
            {
                if (m_node.IsLeaf || m_node.Subnodes == null)
                    return [];

                var children = new List<IPointNode>();
                foreach (var subnodeRef in m_node.Subnodes)
                {
                    if (subnodeRef != null)
                    {
                        var subnode = subnodeRef.Value;
                        if (subnode != null)
                        {
                            children.Add(new PointNodeAdapter(subnode));
                        }
                    }
                }

                return children.ToArray();
            }
        }



    }


    public static class PointCloudAdapterExtensions
    {
        public static IPointNode ToPointNode(this IPointCloudNodeOld node)
        {
            return new PointNodeAdapter(node);
        }

        public static Chunk ToChunk(this IPointNode node, HashSet<int> filter)
        {
            var positions = node.Positions;
            var filteredPositions = new V3d[filter.Count];
            C4b[]? filteredColors = null;
            V3f[]? filteredNormals = null;
            int[]? filteredIntensities = null;
            byte[]? filteredClassifications = null;
            object? filteredPartIndices = null;
            Range1i? filteredPartIndexRange = null;

            var bb = Box3d.Invalid;
            var index = 0;
            foreach (var i in filter)
            {
                filteredPositions[index++] = positions[i];
                bb.ExtendBy(positions[i]);
            }

            if (node.TryGetAttribute(PointNodeAttributes.Colors, out var colorsObj) && colorsObj is C4b[] colors)
            {
                filteredColors = new C4b[filter.Count];
                index = 0;
                foreach (var i in filter)
                {
                    filteredColors[index++] = colors[i];
                }
            }

            if (node.TryGetAttribute(PointNodeAttributes.Normals, out var normalsObj) && normalsObj is V3f[] normals)
            {
                filteredNormals = new V3f[filter.Count];
                index = 0;
                foreach (var i in filter)
                {
                    filteredNormals[index++] = normals[i];
                }
            }

            if (node.TryGetAttribute(PointNodeAttributes.Intensities, out var intensitiesObj) &&
                intensitiesObj is int[] intensities)
            {
                filteredIntensities = new int[filter.Count];
                index = 0;
                foreach (var i in filter)
                {
                    filteredIntensities[index++] = intensities[i];
                }
            }

            if (node.TryGetAttribute(PointNodeAttributes.Classifications, out var classificationsObj) &&
                classificationsObj is byte[] classifications)
            {
                filteredClassifications = new byte[filter.Count];
                index = 0;
                foreach (var i in filter)
                {
                    filteredClassifications[index++] = classifications[i];
                }
            }

            if (node.TryGetAttribute(PointNodeAttributes.PartIndices, out var partIndicesObj))
            {
                var expandedPartIndices = PartIndexUtils.Expand(partIndicesObj, positions.Length);
                if (expandedPartIndices != null)
                {
                    var filteredPartIndicesArray = new int[filter.Count];
                    index = 0;
                    foreach (var i in filter)
                    {
                        filteredPartIndicesArray[index++] = expandedPartIndices[i];
                    }

                    filteredPartIndices = filteredPartIndicesArray;
                }
            }

            if (filteredPartIndices != null)
            {
                var partIndicesArray = (int[])filteredPartIndices;
                int minPartIndex = int.MaxValue;
                int maxPartIndex = int.MinValue;
                foreach (var partIndex in partIndicesArray)
                {
                    if (partIndex < minPartIndex) minPartIndex = partIndex;
                    if (partIndex > maxPartIndex) maxPartIndex = partIndex;
                }

                filteredPartIndexRange = new Range1i(minPartIndex, maxPartIndex);
            }

            return new Chunk(filteredPositions, filteredColors, filteredNormals, filteredIntensities,
                filteredClassifications, filteredPartIndices, filteredPartIndexRange, bb);
        }

        public static Chunk ToChunk(this IPointNode node)
        {
            var positions = node.Positions;
            C4b[]? filteredColors = null;
            V3f[]? filteredNormals = null;
            int[]? filteredIntensities = null;
            byte[]? filteredClassifications = null;
            object? filteredPartIndices = null;
            Range1i? filteredPartIndexRange = null;


            if (node.TryGetAttribute(PointNodeAttributes.Colors, out var colorsObj) && colorsObj is C4b[] colors)
            {
                filteredColors = colors;
            }

            if (node.TryGetAttribute(PointNodeAttributes.Normals, out var normalsObj) && normalsObj is V3f[] normals)
            {
                filteredNormals = normals;
            }

            if (node.TryGetAttribute(PointNodeAttributes.Intensities, out var intensitiesObj) &&
                intensitiesObj is int[] intensities)
            {
                filteredIntensities = intensities;
            }

            if (node.TryGetAttribute(PointNodeAttributes.Classifications, out var classificationsObj) &&
                classificationsObj is byte[] classifications)
            {
                filteredClassifications = classifications;
            }

            if (node.TryGetAttribute(PointNodeAttributes.PartIndices, out var partIndicesObj))
            {
                var expandedPartIndices = PartIndexUtils.Expand(partIndicesObj, positions.Length);
                if (expandedPartIndices != null)
                {
                    int minPartIndex = int.MaxValue;
                    int maxPartIndex = int.MinValue;
                    foreach (var partIndex in expandedPartIndices)
                    {
                        if (partIndex < minPartIndex) minPartIndex = partIndex;
                        if (partIndex > maxPartIndex) maxPartIndex = partIndex;
                    }

                    filteredPartIndices = expandedPartIndices;
                    filteredPartIndexRange = new Range1i(minPartIndex, maxPartIndex);
                }
            }

            return new Chunk(positions, filteredColors, filteredNormals, filteredIntensities, filteredClassifications,
                filteredPartIndices, filteredPartIndexRange, node.DataBounds);

        }

    }
}
