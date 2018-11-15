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
    /// A link to another octree node (possibly in another store).
    /// </summary>
    public class LinkedNode : IPointCloudNode
    {
        private readonly IStoreResolver m_storeResolver;

        private WeakReference<IPointCloudNode> m_root;

        /// <summary>
        /// </summary>
        public string LinkedStorePath { get; }

        /// <summary>
        /// </summary>
        public string LinkedPointCloudKey { get; }

        /// <summary>
        /// Links to different octree.
        /// </summary>
        public LinkedNode(IStoreResolver storeResolver, string storePath, string pointCloudKey)
        {
            m_storeResolver = storeResolver;
            LinkedStorePath = storePath;
            LinkedPointCloudKey = pointCloudKey;
        }
        
        /// <summary></summary>
        public IPointCloudNode Root
        {
            get
            {
                if (m_root != null && m_root.TryGetTarget(out IPointCloudNode r))
                {
                    return r;
                }

                var storage = PointCloud.OpenStore(LinkedStorePath);
                r = storage.GetPointSet(LinkedPointCloudKey, default).Root.Value;
                m_root = new WeakReference<IPointCloudNode>(r);
                return r;
            }
        }

        /// <summary></summary>
        public string Id => Root.Id;

        /// <summary></summary>
        public Cell Cell => Root.Cell;

        /// <summary></summary>
        public V3d Center => Root.Center;

        /// <summary>
        /// Exact bounding box of all points in this tree.
        /// </summary>
        public Box3d BoundingBoxExact => throw new NotImplementedException();

        /// <summary></summary>
        public long PointCountTree => Root.PointCountTree;

        /// <summary></summary>
        public PersistentRef<IPointCloudNode>[] Subnodes => Root.Subnodes;
        
        /// <summary></summary>
        public Storage Storage
        {
            get
            {
                if (m_root != null && m_root.TryGetTarget(out IPointCloudNode r))
                {
                    return r.Storage;
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary></summary>
        public bool TryGetPropertyKey(string property, out string key)
            => Root.TryGetPropertyKey(property, out key);

        /// <summary></summary>
        public bool TryGetPropertyValue(string property, out object value)
            => Root.TryGetPropertyValue(property, out value);

        /// <summary></summary>
        public FilterState FilterState => FilterState.FullyInside;

        /// <summary></summary>
        public void Dispose() => Storage?.Dispose();

    }
}
