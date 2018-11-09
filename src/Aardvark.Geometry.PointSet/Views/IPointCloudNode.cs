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
using Aardvark.Data.Points;
using System;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// An immutable point cloud octree node.
    /// </summary>
    public interface IPointCloudNode : IDisposable
    {
        /// <summary>
        /// Backing store, or null.
        /// </summary>
        Storage Storage { get; }

        /// <summary>
        /// Key.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// This node's index/bounds.
        /// </summary>
        Cell Cell { get; }

        /// <summary>
        /// Center of this node's cell.
        /// </summary>
        V3d Center { get; }

        /// <summary>
        /// Number of points in this tree (sum of leaves).
        /// </summary>
        long PointCountTree { get; }
        
        /// <summary>
        /// Subnodes (8), or null if leaf.
        /// Entries are null if there is no subnode.
        /// There is at least 1 non-null entry.
        /// </summary>
        PersistentRef<IPointCloudNode>[] Subnodes { get; }

        /// <summary>
        /// </summary>
        bool TryGetProperty(PointSetProperties p, out Guid pRef);
        

        /// <summary> </summary>
        bool HasPositions { get; }

        /// <summary></summary>
        bool HasColors { get; }

        /// <summary></summary>
        bool HasNormals { get; }
        
        /// <summary></summary>
        bool HasIntensities { get; }

        /// <summary></summary>
        bool HasKdTree { get; }

        /// <summary></summary>
        bool HasLodPositions { get; }

        /// <summary></summary>
        bool HasLodColors { get; }

        /// <summary></summary>
        bool HasLodNormals { get; }

        /// <summary></summary>
        bool HasLodIntensities { get; }

        /// <summary></summary>
        bool HasLodKdTree { get; }

        /// <summary></summary>
        bool HasClassifications { get; }

        /// <summary></summary>
        bool HasLodClassifications { get; }



        /// <summary>
        /// Point positions relative to cell's center, or null if no positions.
        /// </summary>
        PersistentRef<V3f[]> Positions { get; }

        /// <summary>
        /// Point positions (absolute), or null if no positions.
        /// </summary>
        V3d[] PositionsAbsolute { get; }
        
        /// <summary>
        /// Point colors, or null if no points.
        /// </summary>
        PersistentRef<C4b[]> Colors { get; }
        
        /// <summary>
        /// </summary>
        PersistentRef<V3f[]> Normals { get; }

        /// <summary>
        /// </summary>
        PersistentRef<int[]> Intensities { get; }

        /// <summary>
        /// </summary>
        PersistentRef<PointRkdTreeD<V3f[], V3f>> KdTree { get; }

        /// <summary>
        /// LoD-Positions relative to cell's center, or null if no positions.
        /// </summary>
        PersistentRef<V3f[]> LodPositions { get; }

        /// <summary>
        /// Lod-Positions (absolute), or null if no positions.
        /// </summary>
        V3d[] LodPositionsAbsolute { get; }
        
        /// <summary>
        /// Point colors, or null if no points.
        /// </summary>
        PersistentRef<C4b[]> LodColors { get; }

        /// <summary>
        /// </summary>
        PersistentRef<V3f[]> LodNormals { get; }

        /// <summary>
        /// </summary>
        PersistentRef<int[]> LodIntensities { get; }

        /// <summary>
        /// </summary>
        PersistentRef<PointRkdTreeD<V3f[], V3f>> LodKdTree { get; }

        /// <summary>
        /// </summary>
        PersistentRef<byte[]> Classifications { get; }
        
        /// <summary>
        /// </summary>
        PersistentRef<byte[]> LodClassifications { get; }
    }
}
