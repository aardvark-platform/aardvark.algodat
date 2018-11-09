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
//using Aardvark.Base;
//using Aardvark.Data.Points;
//using System;
//using System.Collections.Generic;
//using System.Linq;

//namespace Aardvark.Geometry.Points
//{
//    /// <summary>
//    /// A view onto a set of non-overlapping nodes.
//    /// </summary>
//    public class MergeView : IPointCloudNode, IDisposable
//    {
//        /// <summary>
//        /// </summary>
//        public IPointCloudNode[] Nodes { get; }
        
//        /// <summary>
//        /// </summary>
//        public MergeView(IEnumerable<IPointCloudNode> nodes)
//        {
//            Nodes = nodes.ToArray();

//            var rootCell = new Cell(new Box3d(Nodes.Select(x => x.Cell.BoundingBox)));

//            var test = containedIn(rootCell, Nodes);
//            if (test.Length != Nodes.Length) throw new InvalidOperationException();

//            IPointCloudNode[] containedIn(Cell c, IEnumerable<IPointCloudNode> ns)
//                => ns.Where(x => c.Contains(x.Cell)).ToArray();
//        }

//        /// <summary></summary>
//        public string Id => m_root.Id;

//        /// <summary></summary>
//        public Cell Cell => m_root.Cell;

//        /// <summary></summary>
//        public V3d Center => m_root.Center;

//        /// <summary></summary>
//        public long PointCountTree => m_root.PointCountTree;

//        /// <summary></summary>
//        public PersistentRef<IPointCloudNode>[] Subnodes => m_root.Subnodes;

//        /// <summary></summary>
//        public bool HasPositions => m_root.HasPositions;

//        /// <summary></summary>
//        public bool HasColors => m_root.HasColors;

//        /// <summary></summary>
//        public bool HasNormals => m_root.HasNormals;

//        /// <summary></summary>
//        public bool HasIntensities => m_root.HasIntensities;

//        /// <summary></summary>
//        public bool HasKdTree => m_root.HasKdTree;

//        /// <summary></summary>
//        public bool HasLodPositions => m_root.HasLodPositions;

//        /// <summary></summary>
//        public bool HasLodColors => m_root.HasLodColors;

//        /// <summary></summary>
//        public bool HasLodNormals => m_root.HasLodNormals;

//        /// <summary></summary>
//        public bool HasLodIntensities => m_root.HasLodIntensities;

//        /// <summary></summary>
//        public bool HasLodKdTree => m_root.HasLodKdTree;

//        /// <summary></summary>
//        public bool HasClassifications => m_root.HasClassifications;

//        /// <summary></summary>
//        public bool HasLodClassifications => m_root.HasLodClassifications;

//        /// <summary></summary>
//        public PersistentRef<V3f[]> Positions => m_root.Positions;

//        /// <summary></summary>
//        public V3d[] PositionsAbsolute => m_root.PositionsAbsolute;

//        /// <summary></summary>
//        public PersistentRef<C4b[]> Colors => m_root.Colors;

//        /// <summary></summary>
//        public PersistentRef<V3f[]> Normals => m_root.Normals;

//        /// <summary></summary>
//        public PersistentRef<int[]> Intensities => m_root.Intensities;

//        /// <summary></summary>
//        public PersistentRef<PointRkdTreeD<V3f[], V3f>> KdTree => m_root.KdTree;

//        /// <summary></summary>
//        public PersistentRef<V3f[]> LodPositions => m_root.LodPositions;

//        /// <summary></summary>
//        public V3d[] LodPositionsAbsolute => m_root.LodPositionsAbsolute;

//        /// <summary></summary>
//        public PersistentRef<C4b[]> LodColors => m_root.LodColors;

//        /// <summary></summary>
//        public PersistentRef<V3f[]> LodNormals => m_root.LodNormals;

//        /// <summary></summary>
//        public PersistentRef<int[]> LodIntensities => m_root.LodIntensities;

//        /// <summary></summary>
//        public PersistentRef<PointRkdTreeD<V3f[], V3f>> LodKdTree => m_root.LodKdTree;

//        /// <summary></summary>
//        public PersistentRef<byte[]> Classifications => m_root.Classifications;

//        /// <summary></summary>
//        public PersistentRef<byte[]> LodClassifications => m_root.LodClassifications;

//        /// <summary></summary>
//        public bool TryGetProperty(PointSetProperties p, out Guid pRef)
//            => m_root.TryGetProperty(p, out pRef);

//        /// <summary></summary>
//        public void Dispose()
//        {
//            foreach (var x in Nodes) x.Dispose();
//        }
//    }
//}
