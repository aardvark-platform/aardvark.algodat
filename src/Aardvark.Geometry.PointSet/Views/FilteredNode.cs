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
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// A filtered view onto a point cloud.
    /// </summary>
    public class FilteredNode : IPointCloudNode
    {
        /// <summary> </summary>
        public IPointCloudNode Node { get; }

        /// <summary></summary>
        public IFilter Filter { get; }

        #region Construction

        /// <summary></summary>
        public FilteredNode(string id, IPointCloudNode node, IFilter filter)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Node = node ?? throw new ArgumentNullException(nameof(node));
            Filter = filter ?? throw new ArgumentNullException(nameof(filter));
        }

        /// <summary></summary>
        public FilteredNode(IPointCloudNode node, IFilter filter)
            : this(Guid.NewGuid().ToString(), node, filter)
        { }

        #endregion

        /// <summary></summary>
        public Storage Storage => Node.Storage;

        /// <summary></summary>
        public string Id { get; }

        /// <summary></summary>
        public Cell Cell => throw new NotImplementedException();

        /// <summary></summary>
        public V3d Center => throw new NotImplementedException();

        /// <summary></summary>
        public Box3d BoundingBoxExact => throw new NotImplementedException();

        /// <summary></summary>
        public long PointCountTree => throw new NotImplementedException();

        /// <summary></summary>
        public PersistentRef<IPointCloudNode>[] Subnodes => throw new NotImplementedException();

        /// <summary></summary>
        public void Dispose()
        {
            throw new NotImplementedException();
        }

        /// <summary></summary>
        public bool TryGetPropertyKey(string property, out string key)
        {
            throw new NotImplementedException();
        }

        /// <summary></summary>
        public bool TryGetPropertyValue(string property, out object value)
        {
            throw new NotImplementedException();
        }
    }
}
