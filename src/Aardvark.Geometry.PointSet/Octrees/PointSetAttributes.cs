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
using System.Collections.Immutable;

namespace Aardvark.Geometry.Points
{
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
    public static class OctreeAttributes
    {
        #region Cell attributes

        #region Bounds

        /// <summary>
        /// Octree. Exact bounding box of this node's PositionsLocal3f. Local space.
        /// </summary>
        public static readonly DurableDataDefinition<Box3f> BoundingBoxExactLocal =
            new DurableDataDefinition<Box3f>(
                new Guid("aadbb622-1cf6-42e0-86df-be79d28d6757"),
                "Octree.BoundingBoxExactLocal",
                "Octree. Exact bounding box of this node's PositionsLocal3f. Local space."
            );

        #endregion

        #region Point distances

        /// <summary>
        /// Octree. Average (X) and stddev (Y) of distances of each point to its nearest neighbour.
        /// </summary>
        public static readonly DurableDataDefinition<V2f> AveragePointDistanceData =
            new DurableDataDefinition<V2f>(
                new Guid("33fcdbd9-310e-45e7-bba4-c1d2b57a8fb1"),
                "Octree.AveragePointDistanceData",
                "Octree. Average (X) and standard deviation (Y) of distances of each point to its nearest neighbour."
            );

        /// <summary>
        /// Octree. Average distance of each point to its nearest neighbour
        /// </summary>
        public static readonly DurableDataDefinition<float> AveragePointDistance =
            new DurableDataDefinition<float>(
                new Guid("39c21132-4570-4624-afae-6304851567d7"),
                "Octree.AveragePointDistance",
                "Octree. Average distance of each point to its nearest neighbour."
            );

        /// <summary>
        /// Octree. Standard deviation of average distance of each point to its nearest neighbour
        /// </summary>
        public static readonly DurableDataDefinition<float> AveragePointDistanceStdDev =
            new DurableDataDefinition<float>(
                new Guid("94cac234-b6ea-443a-b196-c7dd8e5def0d"),
                "Octree.AveragePointDistanceStdDev",
                "Octree. Standard deviation of average distance of each point to its nearest neighbour."
            );

        #endregion

        #region Tree depth

        /// <summary>
        /// Min and max depth of this tree. A leaf node has depth 0.
        /// </summary>
        public static readonly DurableDataDefinition<Range1i> TreeMinMaxDepth =
            new DurableDataDefinition<Range1i>(
                new Guid("309a1fc8-79f3-4e3f-8ded-5c6b46eaa3ca"),
                "Octree.TreeMinMaxDepth",
                "Min and max depth of this tree. A leaf node has depth 0."
            );

        /// <summary>
        /// Min depth of this tree. A leaf node has depth 0.
        /// </summary>
        public static readonly DurableDataDefinition<int> TreeMinDepth =
            new DurableDataDefinition<int>(
                new Guid("42edbdd6-a29e-4dfd-9836-050ab7fa4e31"),
                "Octree.TreeMinDepth",
                "Min depth of this tree. A leaf node has depth 0."
            );

        /// <summary>
        /// Max depth of this tree. A leaf node has depth 0.
        /// </summary>
        public static readonly DurableDataDefinition<int> TreeMaxDepth =
            new DurableDataDefinition<int>(
                new Guid("d6f54b9e-e907-46c5-9106-d26cd453dc97"),
                "Octree.TreeMaxDepth",
                "Max depth of this tree. A leaf node has depth 0."
            );

        #endregion

        #region Counts

        /// <summary>
        /// Octree. Number of points in this cell. Also dimension of per-point attribute arrays (e.g. Colors3b).
        /// </summary>
        public static readonly DurableDataDefinition<long> PointCountCell =
            new DurableDataDefinition<long>(
                new Guid("172e1f20-0ffc-4d9c-9b3d-903fca41abe3"),
                "Octree.PointCountCell",
                "Octree. Number of points in this cell. Also dimension of per-point attribute arrays (e.g. Colors3b)."
            );

        #endregion

        #endregion

        #region Point attributes

        /// <summary>
        /// Octree. Reference to per-point positions in local cell space (V3f[]).
        /// </summary>
        public static readonly DurableDataDefinition<Guid> RefPositionsLocal3f =
            new DurableDataDefinition<Guid>(
                new Guid("cb127ab9-e58f-4407-8b6b-7f38a192a611"),
                "Octree.RefPositionsLocal3f",
                "Octree. Reference to per-point positions in local cell space (V3f[])."
            );

        /// <summary>
        /// Octree. Per-point positions in local cell space (V3f[]).
        /// </summary>
        public static readonly DurableDataDefinition<Guid> PositionsLocal3f =
            new DurableDataDefinition<Guid>(
                new Guid("05eb38fa-1b6a-4576-820b-780163199db9"),
                "Octree.PositionsLocal3f",
                "Octree. Per-point positions in local cell space (V3f[])."
            );

        /// <summary>
        /// Octree. Per-point positions in global space (V3d[]).
        /// </summary>
        public static readonly DurableDataDefinition<Guid> PositionsGlobal3d =
            new DurableDataDefinition<Guid>(
                new Guid("61ef7c1e-6aeb-45cd-85ed-ad0ed2584553"),
                "Octree.PositionsGlobal3d",
                "Octree. Per-point positions in global space (V3d[])."
            );

        /// <summary>
        /// Octree. Reference to kd-tree for positions in local cell space (PointRkdTreeD&lt;V3f[], V3f&gt;.Data).
        /// </summary>
        public static readonly DurableDataDefinition<Guid> RefKdTreeLocal3f =
            new DurableDataDefinition<Guid>(
                new Guid("dea5651f-2d84-4552-9d09-dbeea761e5d4"),
                "Octree.RefKdTreeLocal3f",
                "Octree. Reference to kd-tree for positions in local cell space (PointRkdTreeD<V3f[], V3f>.Data)."
            );

        /// <summary>
        /// Octree. Kd-tree for positions in local cell space (PointRkdTreeD&lt;V3f[], V3f&gt;).
        /// </summary>
        public static readonly DurableDataDefinition<Guid> KdTreeLocal3f =
            new DurableDataDefinition<Guid>(
                new Guid("86f71249-5342-4d9f-be33-ca31ce23c46a"),
                "Octree.KdTreeLocal3f",
                "Octree. Kd-tree for positions in local cell space (PointRkdTreeD<V3f[], V3f>)."
            );

        /// <summary>
        /// Octree. Reference to per-point colors (C3b[]).
        /// </summary>
        public static readonly DurableDataDefinition<Guid> RefColors3b =
            new DurableDataDefinition<Guid>(
                new Guid("481857a0-398b-4299-afca-41d07b9b92bd"),
                "Octree.RefColors3b",
                "Octree. Reference to per-point colors (C3b[])."
            );

        /// <summary>
        /// Octree. Reference to per-point colors (C4b[]).
        /// </summary>
        public static readonly DurableDataDefinition<Guid> RefColors4b =
            new DurableDataDefinition<Guid>(
                new Guid("5e9a09c8-76f4-48d2-bcdb-dc284f029667"),
                "Octree.RefColors4b",
                "Octree. Reference to per-point colors (C4b[])."
            );

        /// <summary>
        /// Octree. Reference to per-point normals (V3f[]).
        /// </summary>
        public static readonly DurableDataDefinition<Guid> RefNormals3f =
            new DurableDataDefinition<Guid>(
                new Guid("14a89b04-c24a-439d-988e-f6528282e7fd"),
                "Octree.RefNormals3f",
                "Octree. Reference to per-point normals (V3f[])."
            );

        /// <summary>
        /// Octree. Reference to per-point intensities (int[]).
        /// </summary>
        public static readonly DurableDataDefinition<Guid> RefIntensities1i =
            new DurableDataDefinition<Guid>(
                new Guid("25f45721-c647-45eb-b45b-26585ae0bcde"),
                "Octree.RefIntensities1i",
                "Octree. Reference to per-point intensities (int[])."
            );

        /// <summary>
        /// Octree. Per-point intensities (int[]).
        /// </summary>
        public static readonly DurableDataDefinition<Guid> Intensities1i =
            new DurableDataDefinition<Guid>(
                new Guid("0876257e-a23b-4861-b82c-a68b12e594e9"),
                "Octree.Intensities1i",
                "Octree. Per-point intensities (int[])."
            );

        /// <summary>
        /// Octree. Reference to per-point classifications array (byte[]).
        /// </summary>
        public static readonly DurableDataDefinition<Guid> RefClassifications1b =
            new DurableDataDefinition<Guid>(
                new Guid("ff6bf035-1e0e-4a03-a27d-9d1fe134bb48"),
                "Octree.RefClassifications1b",
                "Octree. Reference to per-point classifications array (byte[])."
            );


        #endregion
    }

    /// <summary>
    /// </summary>
    public static class OctreeAttributesExtensions
    {
        /// <summary></summary>
        public static bool TryGetValue<T>(this IPointCloudNode node, DurableDataDefinition<T> attribute, out T value)
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
        public static T GetValue<T>(this IPointCloudNode node, DurableDataDefinition<T> attribute)
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

        /// <summary>
        /// Octree. Exact bounding box of this node's PositionsLocal3f. Local space.
        /// </summary>
        public static Box3f GetOrComputeBoundingBoxExactLocal(this IPointCloudNode node)
        {
            if (node.TryGetValue(OctreeAttributes.BoundingBoxExactLocal, out Box3f box))
            {
                return box;
            }
            else if (node.IsLeaf())
            {
                var posRef = node.GetPositions();
                if (posRef == null) return Box3f.Invalid;

                var pos = posRef.Value;
                return new Box3f(pos);
            }
            else
            {
                var center = node.Center;
                var bounds = Box3f.Invalid;
                foreach (var nr in node.SubNodes)
                {
                    if (nr == null) continue;
                    var n = nr.Value;
                    var b = n.GetValue(OctreeAttributes.BoundingBoxExactLocal);
                    var shift = (V3f)(n.Center - center);
                    bounds.ExtendBy(new Box3f(shift + b.Min, shift + b.Max));
                }
                return bounds;
            }
        }

        #endregion

        #region Point distances

        /// <summary>
        /// Octree. Average (X) and stddev (Y) of distances of each point to its nearest neighbour.
        /// </summary>
        public static V2f GetOrComputeAveragePointDistanceData(this IPointCloudNode node)
        {
            if (node.TryGetValue(OctreeAttributes.AveragePointDistanceData, out V2f value))
            {
                return value;
            }
            else
            {
                var posRef = node.GetPositions();
                var kdTreeRef = node.GetKdTree();
                if (kdTreeRef == null || posRef == null) return new V2f(-1.0f, -1.0f);

                var kdTree = kdTreeRef.Value;
                var pos = posRef.Value;

                var maxRadius = node.BoundingBoxExact.Size.NormMax / 20.0;
                var sum = 0.0;
                var sumSq = 0.0;
                var cnt = 0;

                var step = pos.Length < 1000 ? 1 : pos.Length / 1000;
                for (int i = 0; i < pos.Length; i += step)
                {
                    var p = pos[i];
                    var res = kdTree.GetClosest(kdTree.CreateClosestToPointQuery(maxRadius, 2), p);
                    if (res.Count > 1 && res[0].Dist > 0.0)
                    {
                        var maxDist = res[0].Dist;
                        sum += maxDist;
                        sumSq += maxDist * maxDist;
                        cnt++;
                    }
                }

                if (cnt == 0) return new V2f(maxRadius, -1.0f);

                var avg = sum / cnt;
                var var = (sumSq / cnt) - avg * avg;
                var stddev = Math.Sqrt(var);
                return new V2f(avg, stddev);
            }
        }

        /// <summary>
        /// Octree. Average distance of each point to its nearest neighbour
        /// </summary>
        public static float GetOrComputeAveragePointDistance(this IPointCloudNode node)
            => node.GetOrComputeAveragePointDistanceData().X;

        /// <summary>
        /// Octree. Standard deviation of average distance of each point to its nearest neighbour
        /// </summary>
        public static float GetOrComputeAveragePointDistanceStdDev(this IPointCloudNode node)
            => node.GetOrComputeAveragePointDistanceData().Y;

        #endregion

        #region Tree depth

        /// <summary>
        /// Min and max depth of this tree. A leaf node has depth 0.
        /// </summary>
        public static Range1i GetOrComputeTreeMinMaxDepth(this IPointCloudNode node)
        {
            if (node.TryGetValue(OctreeAttributes.TreeMinMaxDepth, out Range1i value))
            {
                return value;
            }
            else
            {
                if (node.IsLeaf()) return new Range1i(0, 0);
                else
                {
                    Range1i range = new Range1i(int.MaxValue, int.MinValue);

                    foreach (var nr in node.SubNodes)
                    {
                        if (nr == null) continue;
                        var n = nr.Value.GetValue(OctreeAttributes.TreeMinMaxDepth);
                        n.Min += 1;
                        n.Max += 1;

                        if (n.Min < range.Min) range.Min = n.Min;
                        if (n.Max > range.Max) range.Max = n.Max;
                    }

                    return range;
                }
            }
        }

        /// <summary>
        /// Min depth of this tree. A leaf node has depth 0.
        /// </summary>
        public static int GetOrComputeTreeMinDepth(this IPointCloudNode node)
            => node.GetOrComputeTreeMinMaxDepth().Min;

        /// <summary>
        /// Max depth of this tree. A leaf node has depth 0.
        /// </summary>
        public static int GetOrComputeTreeMaxDepth(this IPointCloudNode node)
            => node.GetOrComputeTreeMinMaxDepth().Max;

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
