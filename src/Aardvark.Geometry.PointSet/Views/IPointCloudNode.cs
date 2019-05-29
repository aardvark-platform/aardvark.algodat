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
    public interface IPointCloudNode : IDisposable
    {
        /// <summary>
        /// Backing store.
        /// </summary>
        Storage Storage { get; }

        /// <summary>
        /// Writes this node to store.
        /// </summary>
        IPointCloudNode WriteToStore();

        /// <summary>
        /// Key.
        /// </summary>
        Guid Id { get; }

        /// <summary>
        /// This node's index/bounds.
        /// </summary>
        Cell Cell { get; }

        /// <summary>
        /// Center of this node's cell.
        /// </summary>
        V3d Center { get; }

        /// <summary>
        /// Exact bounding box of all points in this node (global space).
        /// </summary>
        Box3d BoundingBoxExactGlobal { get; }

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
        /// Average distance of points in this cell.
        /// </summary>
        float PointDistanceAverage { get; }

        /// <summary>
        /// Standard deviation of distance of points in this cell.
        /// </summary>
        float PointDistanceStandardDeviation { get; }
        
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

        /// <summary>
        /// </summary>
        bool TryGetValue(Durable.Def what, out object o);

        /// <summary>
        /// </summary>
        FilterState FilterState { get; }

        /// <summary>
        /// Returns new node with added/replaced data.
        /// If existing entry is replaced, then the node gets a new id.
        /// Node is NOT written to store. Use WriteToStore if you want this.
        /// </summary>
        IPointCloudNode WithUpsert(Durable.Def def, object x);



        /// <summary>
        /// </summary>
        bool HasPositions { get; }

        /// <summary>
        /// Octree. Per-point positions in local cell space (as offsets from cell's center).
        /// Durable definition 05eb38fa-1b6a-4576-820b-780163199db9.
        /// </summary>
        PersistentRef<V3f[]> Positions { get; }

        /// <summary>
        /// Octree. Per-point positions in global space.
        /// Durable definition 61ef7c1e-6aeb-45cd-85ed-ad0ed2584553.
        /// </summary>
        V3d[] PositionsAbsolute { get; }

        /// <summary>
        /// </summary>
        bool HasKdTree { get; }

        /// <summary>
        /// </summary>
        PersistentRef<PointRkdTreeF<V3f[], V3f>> KdTree { get; }



        /// <summary>
        /// </summary>
        bool HasColors { get; }

        /// <summary>
        /// Octree. Per-point colors C4b.
        /// Durable definition c91dfea3-243d-4272-9dba-b572931dba23.
        /// </summary>
        PersistentRef<C4b[]> Colors { get; }


        /// <summary>
        /// </summary>
        bool HasNormals { get; }

        /// <summary>
        /// Octree. Per-point V3f normals.
        /// Durable definition 712d0a0c-a8d0-42d1-bfc7-77eac2e4a755.
        /// </summary>
        PersistentRef<V3f[]> Normals { get; }



        /// <summary>
        /// </summary>
        bool HasIntensities { get; }

        /// <summary>
        /// Octree. Per-point int32 intensities..
        /// Durable definition 361027fd-ac58-4de8-89ee-98695f8c5520.
        /// </summary>
        PersistentRef<int[]> Intensities { get; }



        /// <summary>
        /// </summary>
        bool HasClassifications { get; }

        /// <summary>
        /// Octree. Per-point uint8 classifications.
        /// Durable definition bf0975e4-43bd-4742-9e61-c7469d81805d.
        /// </summary>
        PersistentRef<byte[]> Classifications { get; }
    }
}
