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
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public static class DurableOctree
    {
        ///// Octree. Per-point classifications. UInt8[].
        //public static readonly Durable.Def PointRkdTreeDDataReference = new Durable.Def(
        //    id: new Guid("05cf0cac-4f8f-41bc-ac50-1b291297f892"),
        //    name: "Octree.PointRkdTreeDData.Reference",
        //    description: "Octree. Reference to data of an Aardvark.Geometry.PointRkdTreeD.",
        //    type: Durable.Primitives.GuidDef.Id,
        //    isArray: false
        //    );
    }

    /// <summary>
    /// </summary>
    public static class PointCloudAttribute
    {
        /// <summary>byte[].</summary>
        public const string _Classifications = "Classifications";
        /// <summary>C4b[].</summary>
        public const string _Colors = "Colors";
        /// <summary>int[].</summary>
        public const string _Intensities = "Intensities";
        /// <summary>PointRkdTreeDData.</summary>
        public const string _KdTree = "KdTree";
        /// <summary>byte[].</summary>
        [Obsolete]
        public const string _LodClassifications = "LodClassifications";
        /// <summary>C4b[].</summary>
        [Obsolete]
        public const string _LodColors = "LodColors";
        /// <summary>int[].</summary>
        [Obsolete]
        public const string _LodIntensities = "LodIntensities";
        /// <summary>PointRkdTreeDData.</summary>
        [Obsolete]
        public const string _LodKdTree = "LodKdTree";
        /// <summary>V3f[].</summary>
        [Obsolete]
        public const string _LodNormals = "LodNormals";
        /// <summary>V3f[] relative to center.</summary>
        [Obsolete]
        public const string _LodPositions = "LodPositions";
        /// <summary>V3d[] absolute.</summary>
        [Obsolete]
        public const string _LodPositionsAbsolute = "LodPositionsAbsolute";
        /// <summary>V3f[].</summary>
        public const string _Normals = "Normals";
        /// <summary>V3f[] relative to center.</summary>
        public const string _Positions = "Positions";
        /// <summary>V3d[] absolute.</summary>
        public const string _PositionsAbsolute = "PositionsAbsolute";

        /// <summary>
        /// </summary>
        public static object CreatePersistentRef(this Storage storage, string attributeName, string key, object value)
        {
            switch (attributeName)
            {
                case _Classifications:       return new PersistentRef<byte[]>(key, storage.GetByteArray, storage.TryGetByteArray);
                case _Colors:                return new PersistentRef<C4b[]>(key, storage.GetC4bArray, storage.TryGetC4bArray);
                case _Intensities:           return new PersistentRef<int[]>(key, storage.GetIntArray, storage.TryGetIntArray);
                case _KdTree:                return new PersistentRef<PointRkdTreeDData>(key, storage.GetPointRkdTreeDData, storage.TryGetPointRkdTreeDData);
                case _Normals:               return new PersistentRef<V3f[]>(key, storage.GetV3fArray, storage.TryGetV3fArray);
                case _Positions:             return new PersistentRef<V3f[]>(key, storage.GetV3fArray, storage.TryGetV3fArray);

                default: throw new InvalidOperationException($"Cannot convert '{attributeName}' to property.");
            }
        }

        /// <summary>
        /// </summary>
        public static void StoreAttribute(this Storage storage, string attributeName, string key, object value)
        {
            switch (attributeName)
            {
                case _Classifications:       storage.Add(key, (byte[])value); break;
                case _Colors:                storage.Add(key, (C4b[])value); break;
                case _Intensities:           storage.Add(key, (int[])value); break;
                case _KdTree:                storage.Add(key, (PointRkdTreeDData)value); break;
                case _Normals:               storage.Add(key, (V3f[])value); break;
                case _Positions:             storage.Add(key, (V3f[])value); break;

                default: throw new InvalidOperationException($"Cannot store '{attributeName}'.");
            }
        }
    }

    ///// <summary>
    ///// </summary>
    //[Flags]
    //public enum PointSetAttributes : uint
    //{
    //    /// <summary>
    //    /// V3f[] relative to Center.
    //    /// </summary>
    //    Positions           = 1 <<  0,

    //    /// <summary>
    //    /// C4b[].
    //    /// </summary>
    //    Colors              = 1 <<  1,

    //    /// <summary>
    //    /// V3f[].
    //    /// </summary>
    //    Normals             = 1 <<  2,

    //    /// <summary>
    //    /// int[].
    //    /// </summary>
    //    Intensities         = 1 <<  3,

    //    /// <summary>
    //    /// PointRkdTreeD&lt;V3f[], V3f&gt;.
    //    /// </summary>
    //    KdTree              = 1 <<  4,

    //    /// <summary>
    //    /// V3f[] relative to Center.
    //    /// </summary>
    //    [Obsolete]
    //    LodPositions        = 1 <<  5,

    //    /// <summary>
    //    /// C4b[].
    //    /// </summary>
    //    [Obsolete]
    //    LodColors           = 1 <<  6,

    //    /// <summary>
    //    /// V3f[].
    //    /// </summary>
    //    [Obsolete]
    //    LodNormals          = 1 <<  7,

    //    /// <summary>
    //    /// int[].
    //    /// </summary>
    //    [Obsolete]
    //    LodIntensities      = 1 <<  8,

    //    /// <summary>
    //    /// PointRkdTreeD&lt;V3f[], V3f&gt;.
    //    /// </summary>
    //    [Obsolete]
    //    LodKdTree           = 1 <<  9,

    //    /// <summary>
    //    /// byte[].
    //    /// </summary>
    //    Classifications     = 1 << 10,

    //    /// <summary>
    //    /// byte[].
    //    /// </summary>
    //    [Obsolete]
    //    LodClassifications  = 1 << 11,

    //    /// <summary>
    //    /// Cell attributes.
    //    /// </summary>
    //    HasCellAttributes   = 1 << 23,
    //}

    /// <summary>
    /// </summary>
    public static class OctreeAttributesExtensions
    {
        /// <summary></summary>
        public static bool TryGetValue<T>(this IPointCloudNode node, Durable.Def attribute, out T value)
        {
            if (node.Data.TryGetValue(attribute, out var o) && o is T)
            {
                value = (T)o;
                return true;
            }
            else
            {
                value = default;
                return false;
            }
        }

        /// <summary></summary>
        public static T GetValue<T>(this IPointCloudNode node, Durable.Def attribute)
        {
            if (node.Data.TryGetValue(attribute, out var o) && o is T)
            {
                return (T)o;
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        #region Bounds

        ///// <summary>
        ///// Octree. Exact bounding box of this node's PositionsLocal3f. Local space.
        ///// </summary>
        //public static Box3f GetOrComputeBoundingBoxExactLocal(this IPointCloudNode node)
        //{
        //    if (node.TryGetValue(Durable.Octree.BoundingBoxExactLocal, out Box3f box))
        //    {
        //        return box;
        //    }
        //    else if (node.IsLeaf())
        //    {
        //        var posRef = node.GetPositions();
        //        if (posRef == null) return Box3f.Invalid;

        //        var pos = posRef.Value;
        //        return new Box3f(pos);
        //    }
        //    else
        //    {
        //        var center = node.Center;
        //        var bounds = Box3f.Invalid;
        //        foreach (var nr in node.SubNodes)
        //        {
        //            if (nr == null) continue;
        //            var n = nr.Value;
        //            var b = n.GetValue(Durable.Octree.BoundingBoxExactLocal);
        //            var shift = (V3f)(n.Center - center);
        //            bounds.ExtendBy(new Box3f(shift + b.Min, shift + b.Max));
        //        }
        //        return bounds;
        //    }
        //}

        #endregion

        #region Point distances

        ///// <summary>
        ///// Octree. Average (X) and stddev (Y) of distances of each point to its nearest neighbour.
        ///// </summary>
        //public static V2f GetOrComputeAveragePointDistanceData(this IPointCloudNode node)
        //{
        //    if (node.TryGetValue(Durable.Octree.AveragePointDistanceData, out V2f value))
        //    {
        //        return value;
        //    }
        //    else
        //    {
        //        var posRef = node.GetPositions();
        //        var kdTreeRef = node.GetKdTree();
        //        if (kdTreeRef == null || posRef == null) return new V2f(-1.0f, -1.0f);

        //        var kdTree = kdTreeRef.Value;
        //        var pos = posRef.Value;

        //        var maxRadius = node.BoundingBoxExact.Size.NormMax / 20.0;
        //        var sum = 0.0;
        //        var sumSq = 0.0;
        //        var cnt = 0;

        //        var step = pos.Length < 1000 ? 1 : pos.Length / 1000;
        //        for (int i = 0; i < pos.Length; i += step)
        //        {
        //            var p = pos[i];
        //            var res = kdTree.GetClosest(kdTree.CreateClosestToPointQuery(maxRadius, 2), p);
        //            if (res.Count > 1 && res[0].Dist > 0.0)
        //            {
        //                var maxDist = res[0].Dist;
        //                sum += maxDist;
        //                sumSq += maxDist * maxDist;
        //                cnt++;
        //            }
        //        }

        //        if (cnt == 0) return new V2f(maxRadius, -1.0f);

        //        var avg = sum / cnt;
        //        var var = (sumSq / cnt) - avg * avg;
        //        var stddev = Math.Sqrt(var);
        //        return new V2f(avg, stddev);
        //    }
        //}

        ///// <summary>
        ///// Octree. Average distance of each point to its nearest neighbour
        ///// </summary>
        //public static float GetOrComputeAveragePointDistance(this IPointCloudNode node)
        //    => node.GetOrComputeAveragePointDistanceData().X;

        ///// <summary>
        ///// Octree. Standard deviation of average distance of each point to its nearest neighbour
        ///// </summary>
        //public static float GetOrComputeAveragePointDistanceStdDev(this IPointCloudNode node)
        //    => node.GetOrComputeAveragePointDistanceData().Y;

        #endregion

        #region Tree depth

        ///// <summary>
        ///// Min and max depth of this tree. A leaf node has depth 0.
        ///// </summary>
        //public static Range1i GetOrComputeTreeMinMaxDepth(this IPointCloudNode node)
        //{
        //    if (node.TryGetValue(Durable.Octree.TreeMinMaxDepth, out Range1i value))
        //    {
        //        return value;
        //    }
        //    else
        //    {
        //        if (node.IsLeaf()) return new Range1i(0, 0);
        //        else
        //        {
        //            Range1i range = new Range1i(int.MaxValue, int.MinValue);

        //            foreach (var nr in node.SubNodes)
        //            {
        //                if (nr == null) continue;
        //                var n = nr.Value.GetValue(Durable.Octree.TreeMinMaxDepth);
        //                n.Min += 1;
        //                n.Max += 1;

        //                if (n.Min < range.Min) range.Min = n.Min;
        //                if (n.Max > range.Max) range.Max = n.Max;
        //            }

        //            return range;
        //        }
        //    }
        //}

        ///// <summary>
        ///// Min depth of this tree. A leaf node has depth 0.
        ///// </summary>
        //public static int GetOrComputeTreeMinDepth(this IPointCloudNode node)
        //    => node.GetOrComputeTreeMinMaxDepth().Min;

        ///// <summary>
        ///// Max depth of this tree. A leaf node has depth 0.
        ///// </summary>
        //public static int GetOrComputeTreeMaxDepth(this IPointCloudNode node)
        //    => node.GetOrComputeTreeMinMaxDepth().Max;

        #endregion

        #region Counts

        /// <summary>
        /// Octree. Number of points in this cell. Also dimension of per-point attribute arrays (e.g. Colors3b).
        /// </summary>
        public static long GetOrComputePointCountCell(this IPointCloudNode node)
            => node.IsLeaf() ? node.PointCountTree : (node.GetPositions()?.Value?.Length ?? 0L);

        #endregion
    }
}
