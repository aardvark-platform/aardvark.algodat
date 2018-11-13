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
    /// </summary>
    public static class IPointCloudNodeExtensions
    {
        #region Has*

        /// <summary> </summary>
        public static bool HasPositions(this IPointCloudNode self) => self.TryGetPropertyKey(PointCloudAttribute.Positions, out string _);

        /// <summary></summary>
        public static bool HasColors(this IPointCloudNode self) => self.TryGetPropertyKey(PointCloudAttribute.Positions, out string _);

        /// <summary></summary>
        public static bool HasNormals(this IPointCloudNode self) => self.TryGetPropertyKey(PointCloudAttribute.Positions, out string _);

        /// <summary></summary>
        public static bool HasIntensities(this IPointCloudNode self) => self.TryGetPropertyKey(PointCloudAttribute.Positions, out string _);

        /// <summary></summary>
        public static bool HasKdTree(this IPointCloudNode self) => self.TryGetPropertyKey(PointCloudAttribute.Positions, out string _);

        /// <summary></summary>
        public static bool HasLodPositions(this IPointCloudNode self) => self.TryGetPropertyKey(PointCloudAttribute.Positions, out string _);

        /// <summary></summary>
        public static bool HasLodColors(this IPointCloudNode self) => self.TryGetPropertyKey(PointCloudAttribute.Positions, out string _);

        /// <summary></summary>
        public static bool HasLodNormals(this IPointCloudNode self) => self.TryGetPropertyKey(PointCloudAttribute.Positions, out string _);

        /// <summary></summary>
        public static bool HasLodIntensities(this IPointCloudNode self) => self.TryGetPropertyKey(PointCloudAttribute.Positions, out string _);

        /// <summary></summary>
        public static bool HasLodKdTree(this IPointCloudNode self) => self.TryGetPropertyKey(PointCloudAttribute.Positions, out string _);

        /// <summary></summary>
        public static bool HasClassifications(this IPointCloudNode self) => self.TryGetPropertyKey(PointCloudAttribute.Positions, out string _);

        /// <summary></summary>
        public static bool HasLodClassifications(this IPointCloudNode self) => self.TryGetPropertyKey(PointCloudAttribute.Positions, out string _);

        #endregion

        #region Get*

        /// <summary>
        /// Point positions relative to cell's center, or null if no positions.
        /// </summary>
        public static PersistentRef<V3f[]> GetPositions(this IPointCloudNode self)
            => self.TryGetPropertyValue(PointCloudAttribute.Positions, out object value) ? (PersistentRef<V3f[]>)value : null;

        /// <summary>
        /// Point positions (absolute), or null if no positions.
        /// </summary>
        public static V3d[] GetPositionsAbsolute(this IPointCloudNode self)
        {
            var c = self.Center;
            var ps = GetPositions(self);
            return ps.Value.Map(p => c + (V3d)p);
        }

        /// <summary>
        /// </summary>
        public static PersistentRef<PointRkdTreeD<V3f[], V3f>> GetKdTree(this IPointCloudNode self)
            => self.TryGetPropertyValue(PointCloudAttribute.KdTree, out object value) ? (PersistentRef<PointRkdTreeD<V3f[], V3f>>)value : null;

        /// <summary>
        /// Point colors, or null if no points.
        /// </summary>
        public static PersistentRef<C4b[]> GetColors(this IPointCloudNode self)
            => self.TryGetPropertyValue(PointCloudAttribute.Colors, out object value) ? (PersistentRef<C4b[]>)value : null;

        /// <summary>
        /// </summary>
        public static PersistentRef<V3f[]> GetNormals(this IPointCloudNode self)
            => self.TryGetPropertyValue(PointCloudAttribute.Normals, out object value) ? (PersistentRef<V3f[]>)value : null;

        /// <summary>
        /// </summary>
        public static PersistentRef<int[]> GetIntensities(this IPointCloudNode self)
            => self.TryGetPropertyValue(PointCloudAttribute.Intensities, out object value) ? (PersistentRef<int[]>)value : null;

        /// <summary>
        /// </summary>
        public static PersistentRef<byte[]> GetClassifications(this IPointCloudNode self)
            => self.TryGetPropertyValue(PointCloudAttribute.Classifications, out object value) ? (PersistentRef<byte[]>)value : null;

        /// <summary>
        /// LoD-Positions relative to cell's center, or null if no positions.
        /// </summary>
        public static PersistentRef<V3f[]> GetLodPositions(this IPointCloudNode self)
            => self.TryGetPropertyValue(PointCloudAttribute.LodPositions, out object value) ? (PersistentRef<V3f[]>)value : null;

        /// <summary>
        /// Lod-Positions (absolute), or null if no positions.
        /// </summary>
        public static V3d[] GetLodPositionsAbsolute(this IPointCloudNode self)
        {
            var c = self.Center;
            return GetLodPositions(self).Value.Map(p => c + (V3d)p);
        }

        /// <summary>
        /// </summary>
        public static PersistentRef<PointRkdTreeD<V3f[], V3f>> GetLodKdTree(this IPointCloudNode self)
            => self.TryGetPropertyValue(PointCloudAttribute.LodKdTree, out object value) ? (PersistentRef<PointRkdTreeD<V3f[], V3f>>)value : null;

        /// <summary>
        /// Point colors, or null if no points.
        /// </summary>
        public static PersistentRef<C4b[]> GetLodColors(this IPointCloudNode self)
            => self.TryGetPropertyValue(PointCloudAttribute.LodColors, out object value) ? (PersistentRef<C4b[]>)value : null;

        /// <summary>
        /// </summary>
        public static PersistentRef<V3f[]> GetLodNormals(this IPointCloudNode self)
            => self.TryGetPropertyValue(PointCloudAttribute.LodNormals, out object value) ? (PersistentRef<V3f[]>)value : null;

        /// <summary>
        /// </summary>
        public static PersistentRef<int[]> GetLodIntensities(this IPointCloudNode self)
            => self.TryGetPropertyValue(PointCloudAttribute.LodIntensities, out object value) ? (PersistentRef<int[]>)value : null;

        /// <summary>
        /// </summary>
        public static PersistentRef<byte[]> GetLodClassifications(this IPointCloudNode self)
            => self.TryGetPropertyValue(PointCloudAttribute.LodClassifications, out object value) ? (PersistentRef<byte[]>)value : null;

        #endregion

        #region Storage

        /// <summary>
        /// </summary>
        public static IPointCloudNode GetPointCloudNode(this Storage storage, string id)
        {
            throw new NotImplementedException();
        }

        #endregion

        /// <summary></summary>
        public static bool IsLeaf(this IPointCloudNode self) => self.Subnodes == null;

        /// <summary></summary>
        public static bool IsNotLeaf(this IPointCloudNode self) => self.Subnodes != null;

        /// <summary>
        /// Counts ALL nodes of this tree by traversing over all persistent refs.
        /// </summary>
        public static long CountNodes(this IPointCloudNode self)
        {
            if (self == null) return 0;

            var subnodes = self.Subnodes;
            if (subnodes == null) return 1;
            
            var count = 1L;
            for (var i = 0; i < 8; i++)
            {
                var n = subnodes[i];
                if (n == null) continue;
                count += n.Value.CountNodes();
            }
            return count;
        }
    }
}
