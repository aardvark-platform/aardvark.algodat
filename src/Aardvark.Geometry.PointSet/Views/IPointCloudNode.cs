﻿/*
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
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Immutable;

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
        /// Exact bounding box of all points in this tree.
        /// </summary>
        Box3d BoundingBoxExact { get; }

        /// <summary>
        /// Number of points in this tree (sum of leaves).
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
        PersistentRef<IPointCloudNode>[] SubNodes { get; }

        /// <summary>
        /// Gets the given durable data value. 
        /// </summary>
        ImmutableDictionary<Durable.Def, object> Data { get; }

        /// <summary>
        /// </summary>
        FilterState FilterState { get; }

        /// <summary>
        /// </summary>
        JObject ToJson();

        /// <summary>
        /// </summary>
        string NodeType { get; }
    }
}