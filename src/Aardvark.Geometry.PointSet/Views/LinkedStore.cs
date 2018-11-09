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
    /// A linked store exposed as an immutable point cloud octree node.
    /// </summary>
    public class LinkedStore : IPointCloudNode, IDisposable
    {
        /// <summary>
        /// </summary>
        public string LinkedStorePath { get; }

        /// <summary>
        /// </summary>
        public string LinkedPointCloudKey { get; }

        /// <summary>
        /// </summary>
        public LinkedStore(string storePath, string pointCloudKey)
        {
            LinkedStorePath = storePath;
            LinkedPointCloudKey = pointCloudKey;
        }
        
        private WeakReference<IPointCloudNode> m_root;

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

        /// <summary></summary>
        public long PointCountTree => Root.PointCountTree;

        /// <summary></summary>
        public PersistentRef<IPointCloudNode>[] Subnodes => Root.Subnodes;

        /// <summary></summary>
        public bool HasPositions => Root.HasPositions;

        /// <summary></summary>
        public bool HasColors => Root.HasColors;

        /// <summary></summary>
        public bool HasNormals => Root.HasNormals;

        /// <summary></summary>
        public bool HasIntensities => Root.HasIntensities;

        /// <summary></summary>
        public bool HasKdTree => Root.HasKdTree;

        /// <summary></summary>
        public bool HasLodPositions => Root.HasLodPositions;

        /// <summary></summary>
        public bool HasLodColors => Root.HasLodColors;

        /// <summary></summary>
        public bool HasLodNormals => Root.HasLodNormals;

        /// <summary></summary>
        public bool HasLodIntensities => Root.HasLodIntensities;

        /// <summary></summary>
        public bool HasLodKdTree => Root.HasLodKdTree;

        /// <summary></summary>
        public bool HasClassifications => Root.HasClassifications;

        /// <summary></summary>
        public bool HasLodClassifications => Root.HasLodClassifications;

        /// <summary></summary>
        public PersistentRef<V3f[]> Positions => Root.Positions;

        /// <summary></summary>
        public V3d[] PositionsAbsolute => Root.PositionsAbsolute;

        /// <summary></summary>
        public PersistentRef<C4b[]> Colors => Root.Colors;

        /// <summary></summary>
        public PersistentRef<V3f[]> Normals => Root.Normals;

        /// <summary></summary>
        public PersistentRef<int[]> Intensities => Root.Intensities;

        /// <summary></summary>
        public PersistentRef<PointRkdTreeD<V3f[], V3f>> KdTree => Root.KdTree;

        /// <summary></summary>
        public PersistentRef<V3f[]> LodPositions => Root.LodPositions;

        /// <summary></summary>
        public V3d[] LodPositionsAbsolute => Root.LodPositionsAbsolute;

        /// <summary></summary>
        public PersistentRef<C4b[]> LodColors => Root.LodColors;

        /// <summary></summary>
        public PersistentRef<V3f[]> LodNormals => Root.LodNormals;

        /// <summary></summary>
        public PersistentRef<int[]> LodIntensities => Root.LodIntensities;

        /// <summary></summary>
        public PersistentRef<PointRkdTreeD<V3f[], V3f>> LodKdTree => Root.LodKdTree;

        /// <summary></summary>
        public PersistentRef<byte[]> Classifications => Root.Classifications;

        /// <summary></summary>
        public PersistentRef<byte[]> LodClassifications => Root.LodClassifications;

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
        public bool TryGetProperty(PointSetProperties p, out Guid pRef)
            => Root.TryGetProperty(p, out pRef);

        /// <summary></summary>
        public void Dispose() => Storage?.Dispose();

    }
}
