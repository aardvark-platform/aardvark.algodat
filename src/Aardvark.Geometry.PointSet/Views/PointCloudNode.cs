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
using System.Collections.Generic;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// A point cloud node.
    /// </summary>
    public class PointCloudNode : IPointCloudNode
    {
        /// <summary>
        /// </summary>
        public PointCloudNode(Storage storage,
            string id, Cell cell, Box3d boundingBoxExact, long pointCountTree, PersistentRef<IPointCloudNode>[] subnodes,
            IEnumerable<(string propertyName, string propertyId, object pRef)> properties
            )
        {
            Storage = storage;
            Id = id;
            Cell = cell;
            BoundingBoxExact = boundingBoxExact;
            PointCountTree = pointCountTree;
            Subnodes = subnodes;

            foreach (var (propertyName, propertyId, pRef) in properties)
            {
                m_pIds[propertyName] = propertyId;
                m_pRefs[propertyName] = pRef;
            }
        }

        private readonly Dictionary<string, string> m_pIds = new Dictionary<string, string>();
        private readonly Dictionary<string, object> m_pRefs = new Dictionary<string, object>();

        /// <summary></summary>
        public Storage Storage { get; }

        /// <summary></summary>
        public string Id { get; }

        /// <summary></summary>
        public Cell Cell { get; }

        /// <summary></summary>
        public V3d Center => Cell.GetCenter();

        /// <summary></summary>
        public Box3d BoundingBoxExact { get; }

        /// <summary></summary>
        public long PointCountTree { get; }

        /// <summary></summary>
        public PersistentRef<IPointCloudNode>[] Subnodes { get; }

        /// <summary></summary>
        public bool TryGetPropertyKey(string property, out string key) => m_pIds.TryGetValue(property, out key);

        /// <summary></summary>
        public bool TryGetPropertyValue(string property, out object value) => m_pRefs.TryGetValue(property, out value);

        /// <summary></summary>
        public void Dispose() { }



        /// <summary>
        /// The empty point cloud node.
        /// </summary>
        public static readonly PointCloudNode Empty = new PointCloudNode(null,
            "Aardvark.Geometry.Points.PointCloudNode.Empty",
            new Cell(int.MinValue), Box3d.Invalid, 0, null, null
            );
    }
}
