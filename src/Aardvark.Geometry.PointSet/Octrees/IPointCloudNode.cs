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
using Aardvark.Data;
using Aardvark.Data.Points;
using System;

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
        /// Returns materialized version of this node.
        /// E.g. a non-materialized filtered node is converted into a PointSetNode (which is stored in Storage).
        /// </summary>
        IPointCloudNode Materialize();

        /// <summary>
        /// Writes this node to store.
        /// </summary>
        IPointCloudNode WriteToStore();

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
        PersistentRef<IPointCloudNode>[] Subnodes { get; }

        /// <summary>
        /// Returns new node with replaced subnodes.
        /// </summary>
        IPointCloudNode WithSubNodes(IPointCloudNode[] subnodes);

        /// <summary>
        /// Node has no subnodes.
        /// </summary>
        bool IsLeaf { get; }

        /// <summary>
        /// Returns true if node has given property.
        /// </summary>
        bool Has(Durable.Def what);

        /// <summary></summary>
        bool TryGetValue(Durable.Def what, out object o);

        /// <summary>
        /// Returns new node with added/replaced data.
        /// If existing entry is replaced, then the node gets a new id.
        /// Node is NOT written to store. Use WriteToStore if you want this.
        /// </summary>
        IPointCloudNode WithUpsert(Durable.Def def, object x);

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
        bool HasBoundingBoxExactGlobal { get; }

        /// <summary>
        /// Octree. Exact bounding box of this node's positions. Local space. Box3f.
        /// Durable definition 7912c862-74b4-4f44-a8cd-d11ea1da9304.
        /// </summary>
        Box3d BoundingBoxExactGlobal { get; }

        #endregion

        #region KdTree

        /// <summary></summary>
        bool HasKdTree { get; }

        /// <summary>
        /// </summary>
        PersistentRef<PointRkdTreeF<V3f[], V3f>> KdTree { get; }

        #endregion

        #region Colors

        /// <summary></summary>
        bool HasColors { get; }

        /// <summary>
        /// Octree. Per-point colors C4b.
        /// Durable definition c91dfea3-243d-4272-9dba-b572931dba23.
        /// </summary>
        PersistentRef<C4b[]> Colors { get; }

        #endregion

        #region Normals

        /// <summary></summary>
        bool HasNormals { get; }

        /// <summary>
        /// Octree. Per-point V3f normals.
        /// Durable definition 712d0a0c-a8d0-42d1-bfc7-77eac2e4a755.
        /// </summary>
        PersistentRef<V3f[]> Normals { get; }

        #endregion

        #region Intensities

        /// <summary></summary>
        bool HasIntensities { get; }

        /// <summary>
        /// Octree. Per-point int32 intensities..
        /// Durable definition 361027fd-ac58-4de8-89ee-98695f8c5520.
        /// </summary>
        PersistentRef<int[]> Intensities { get; }

        #endregion

        #region Classifications

        /// <summary></summary>
        bool HasClassifications { get; }

        /// <summary>
        /// Octree. Per-point uint8 classifications.
        /// Durable definition bf0975e4-43bd-4742-9e61-c7469d81805d.
        /// </summary>
        PersistentRef<byte[]> Classifications { get; }

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
        bool HasCentroidLocalAverageDist { get; }

        /// <summary>
        /// Octree. Average point distance to centroid (local space).
        /// Durable definition 1b7e74c5-b2ba-46fd-a7db-c08734da3b75.
        /// </summary>
        float CentroidLocalAverageDist { get; }

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
