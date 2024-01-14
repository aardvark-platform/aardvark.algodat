/*
   Aardvark Platform
   Copyright (C) 2006-2024  Aardvark Platform Team
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
    public interface IPointCloudNode
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
        IPointCloudNode Materialize();

        /// <summary>
        /// Writes this node to store.
        /// </summary>
        IPointCloudNode WriteToStore();

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
        PersistentRef<IPointCloudNode>?[]? Subnodes { get; }

        /// <summary>
        /// Returns new node with replaced subnodes.
        /// </summary>
        IPointCloudNode WithSubNodes(IPointCloudNode?[] subnodes);

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
        bool TryGetValue(Durable.Def what, [NotNullWhen(true)]out object? o);

        /// <summary>
        /// Gets all properties.
        /// </summary>
        IReadOnlyDictionary<Durable.Def, object> Properties { get; }

        /// <summary>
        /// Returns new node (with new id) with added/replaced data.
        /// Node is NOT written to store.
        /// Call WriteToStore on the result if you want this.
        /// </summary>
        IPointCloudNode With(IReadOnlyDictionary<Durable.Def, object> replacements);

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
}
