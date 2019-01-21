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
using System.Linq;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public static class PointCloudAttribute
    {
        /// <summary>byte[].</summary>
        public const string Classifications = "Classifications";
        /// <summary>C4b[].</summary>
        public const string Colors = "Colors";
        /// <summary>int[].</summary>
        public const string Intensities = "Intensities";
        /// <summary>PointRkdTreeDData.</summary>
        public const string KdTree = "KdTree";
        /// <summary>byte[].</summary>
        [Obsolete]
        public const string LodClassifications = "LodClassifications";
        /// <summary>C4b[].</summary>
        [Obsolete]
        public const string LodColors = "LodColors";
        /// <summary>int[].</summary>
        [Obsolete]
        public const string LodIntensities = "LodIntensities";
        /// <summary>PointRkdTreeDData.</summary>
        [Obsolete]
        public const string LodKdTree = "LodKdTree";
        /// <summary>V3f[].</summary>
        [Obsolete]
        public const string LodNormals = "LodNormals";
        /// <summary>V3f[] relative to center.</summary>
        [Obsolete]
        public const string LodPositions = "LodPositions";
        /// <summary>V3d[] absolute.</summary>
        [Obsolete]
        public const string LodPositionsAbsolute = "LodPositionsAbsolute";
        /// <summary>V3f[].</summary>
        public const string Normals = "Normals";
        /// <summary>V3f[] relative to center.</summary>
        public const string Positions = "Positions";
        /// <summary>V3d[] absolute.</summary>
        public const string PositionsAbsolute = "PositionsAbsolute";

        /// <summary>
        /// Gets name of attribute.
        /// </summary>
        public static string ToName(this PointSetAttributes self)
        {
            switch (self)
            {
                case PointSetAttributes.Classifications:    return Classifications;
                case PointSetAttributes.Colors:             return Colors;            
                case PointSetAttributes.Intensities:        return Intensities;       
                case PointSetAttributes.KdTree:             return KdTree;  
                case PointSetAttributes.Normals:            return Normals;           
                case PointSetAttributes.Positions:          return Positions;         

                default: throw new InvalidOperationException($"Cannot convert '{self}' to name.");
            }
        }

        /// <summary>
        /// Gets attribute from name.
        /// </summary>
        public static PointSetAttributes ToPointSetAttribute(this string self)
        {
            switch (self)
            {
                case Classifications:       return PointSetAttributes.Classifications;
                case Colors:                return PointSetAttributes.Colors;
                case Intensities:           return PointSetAttributes.Intensities;
                case KdTree:                return PointSetAttributes.KdTree;
                case Normals:               return PointSetAttributes.Normals;
                case Positions:             return PointSetAttributes.Positions;

                default: throw new InvalidOperationException($"Cannot convert '{self}' to property.");
            }
        }

        /// <summary>
        /// </summary>
        public static object CreatePersistentRef(this Storage storage, string attributeName, string key, object value)
        {
            switch (attributeName)
            {
                case Classifications:       return new PersistentRef<byte[]>(key, storage.GetByteArray, storage.TryGetByteArray);
                case Colors:                return new PersistentRef<C4b[]>(key, storage.GetC4bArray, storage.TryGetC4bArray);
                case Intensities:           return new PersistentRef<int[]>(key, storage.GetIntArray, storage.TryGetIntArray);
                case KdTree:                return new PersistentRef<PointRkdTreeDData>(key, storage.GetPointRkdTreeDData, storage.TryGetPointRkdTreeDData);
                case Normals:               return new PersistentRef<V3f[]>(key, storage.GetV3fArray, storage.TryGetV3fArray);
                case Positions:             return new PersistentRef<V3f[]>(key, storage.GetV3fArray, storage.TryGetV3fArray);

                default: throw new InvalidOperationException($"Cannot convert '{attributeName}' to property.");
            }
        }

        /// <summary>
        /// </summary>
        public static void StoreAttribute(this Storage storage, string attributeName, string key, object value)
        {
            switch (attributeName)
            {
                case Classifications:       storage.Add(key, (byte[])value); break;
                case Colors:                storage.Add(key, (C4b[])value); break;
                case Intensities:           storage.Add(key, (int[])value); break;
                case KdTree:                storage.Add(key, (PointRkdTreeDData)value); break;
                case Normals:               storage.Add(key, (V3f[])value); break;
                case Positions:             storage.Add(key, (V3f[])value); break;

                default: throw new InvalidOperationException($"Cannot store '{attributeName}'.");
            }
        }
    }

    /// <summary>
    /// </summary>
    [Flags]
    public enum PointSetAttributes : uint
    {
        /// <summary>
        /// V3f[] relative to Center.
        /// </summary>
        Positions           = 1 <<  0,

        /// <summary>
        /// C4b[].
        /// </summary>
        Colors              = 1 <<  1,

        /// <summary>
        /// V3f[].
        /// </summary>
        Normals             = 1 <<  2,

        /// <summary>
        /// int[].
        /// </summary>
        Intensities         = 1 <<  3,

        /// <summary>
        /// PointRkdTreeD&lt;V3f[], V3f&gt;.
        /// </summary>
        KdTree              = 1 <<  4,

        /// <summary>
        /// V3f[] relative to Center.
        /// </summary>
        [Obsolete]
        LodPositions        = 1 <<  5,

        /// <summary>
        /// C4b[].
        /// </summary>
        [Obsolete]
        LodColors           = 1 <<  6,

        /// <summary>
        /// V3f[].
        /// </summary>
        [Obsolete]
        LodNormals          = 1 <<  7,

        /// <summary>
        /// int[].
        /// </summary>
        [Obsolete]
        LodIntensities      = 1 <<  8,

        /// <summary>
        /// PointRkdTreeD&lt;V3f[], V3f&gt;.
        /// </summary>
        [Obsolete]
        LodKdTree           = 1 <<  9,

        /// <summary>
        /// byte[].
        /// </summary>
        Classifications     = 1 << 10,

        /// <summary>
        /// byte[].
        /// </summary>
        [Obsolete]
        LodClassifications  = 1 << 11,

        /// <summary>
        /// Cell attributes.
        /// </summary>
        HasCellAttributes   = 1 << 23,
    }


    public abstract class CellAttribute
    {
        public readonly Guid Id;
        public readonly string Name;
        public readonly IImmutableSet<CellAttribute> DependsOn;

        public abstract object ComputeValue(IPointCloudNode node);

        public CellAttribute(Guid id, string name, IImmutableSet<CellAttribute> depends)
        {
            Id = id;
            Name = name;
            DependsOn = depends;
        }
    }

    public class CellAttribute<T> : CellAttribute where T : struct
    {
        public readonly Func<CellAttribute<T>, IPointCloudNode, T> Compute;
        public readonly Func<CellAttribute<T>, IPointCloudNode, IPointCloudNode, T?> TryMerge;


        public CellAttribute(Guid id, string name, Func<CellAttribute<T>, IPointCloudNode, T> compute, Func<CellAttribute<T>, IPointCloudNode, IPointCloudNode, T?> tryMerge, params CellAttribute[] depends)
            : base(id, name, ImmutableHashSet.Create(depends))
        {
            Compute = compute;
            TryMerge = tryMerge ?? ((a, b, c) => null);
        }

        public CellAttribute(Guid id, string name, Func<CellAttribute<T>, IPointCloudNode, T> compute, params CellAttribute[] depends)
            : this(id, name, compute, null, depends)
        { }

        public override object ComputeValue(IPointCloudNode node)
        {
            return Compute(this, node);
        }

    }

    /// <summary>
    /// </summary>
    public static class CellAttributes
    {
        public static T GetCellAttribute<T>(this IPointCloudNode node, CellAttribute<T> attribute) where T : struct
        {
            if (node.TryGetCellAttribute<T>(attribute.Id, out T value)) return value;
            else return attribute.Compute(attribute, node);
        }


        public static readonly CellAttribute<Box3f> BoundingBoxExactLocal =
            new CellAttribute<Box3f>(
                new Guid(0xaadbb622, 0x1cf6, 0x42e0, 0x86, 0xdf, 0xbe, 0x79, 0xd2, 0x8d, 0x67, 0x57),
                "BoundingBoxExactLocal",
                (self, node) =>
                {
                    if (node.TryGetCellAttribute(self.Id, out Box3f box))
                    {
                        return box;
                    }
                    else if(node.IsLeaf())
                    {
                        var posRef = node.GetPositions();
                        if (posRef == null) return Box3f.Invalid;

                        var pos = posRef.Value;
                        return new Box3f(pos);
                    }
                    else
                    {
                        var center = node.Cell.BoundingBox.Center;
                        var bounds = Box3f.Invalid;
                        foreach(var nr in node.SubNodes)
                        {
                            if (nr == null) continue;
                            var n = nr.Value;
                            var b = n.GetCellAttribute(self);
                            var shift = (V3f)(n.Cell.BoundingBox.Center - center);
                            bounds.ExtendBy(new Box3f(shift + b.Min, shift + b.Max));
                        }
                        return bounds;
                    }
                },
                (self, l, r) =>
                    (l.Cell == r.Cell &&
                     l.TryGetCellAttribute(self.Id, out Box3f lb) &&
                     r.TryGetCellAttribute(self.Id, out Box3f rb)) ? Box3f.Union(lb, rb) : (Box3f?)null
                
            );


        private static readonly CellAttribute<V2f> AveragePointDistanceData =
            new CellAttribute<V2f>(
                new Guid(0x33fcdbd9, 0x310e, 0x45e7, 0xbb, 0xa4, 0xc1, 0xd2, 0xb5, 0x7a, 0x8f, 0xb1),
                "AveragePointDistanceData",
                (self, node) =>
                {
                    if (node.TryGetCellAttribute(self.Id, out V2f value))
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

                        var sum = 0.0;
                        var sumSq = 0.0;
                        var cnt = 0;
                        foreach(var p in pos)
                        {
                            var res = kdTree.GetClosest(kdTree.CreateClosestToPointQuery(double.PositiveInfinity, 2), p);
                            var maxDist = res[0].Dist;

                            if(maxDist > 0.0)
                            {
                                sum += maxDist;
                                sumSq += maxDist * maxDist;
                                cnt++;
                            }
                        }
                        
                        if (cnt == 0) return new V2f(-1.0f, -1.0f);

                        var avg = sum / cnt;
                        var var = (sumSq / cnt) - avg * avg;
                        var stddev = Fun.Sqrt(var);
                        return new V2f(avg, stddev);
                    }
                }
            );


        public static readonly CellAttribute<float> AveragePointDistance =
            new CellAttribute<float>(
                new Guid(0x39c21132, 0x4570, 0x4624, 0xaf, 0xae, 0x63, 0x04, 0x85, 0x15, 0x67, 0xd7),
                "AveragePointDistance",
                (self, node) => node.GetCellAttribute(AveragePointDistanceData).X,
                AveragePointDistanceData
            );

        public static readonly CellAttribute<float> AveragePointDistanceStdDev =
            new CellAttribute<float>(
                new Guid(0x94cac234, 0xb6ea, 0x443a, 0xb1, 0x96, 0xc7, 0xdd, 0x8e, 0x5d, 0xef, 0x0d),
                "AveragePointDistanceStdDev",
                (self, node) => node.GetCellAttribute(AveragePointDistanceData).Y,
                AveragePointDistanceData
            );


        private static readonly CellAttribute<Range1i> TreeMinMaxDepth =
            new CellAttribute<Range1i>(
                new Guid(0x309a1fc8, 0x79f3, 0x4e3f, 0x8d, 0xed, 0x5c, 0x6b, 0x46, 0xea, 0xa3, 0xca),
                "TreeMinMaxDepth",
                (self, node) =>
                {
                    if (node.TryGetCellAttribute(self.Id, out Range1i value))
                    {
                        return value;
                    }
                    else
                    {
                        if (node.IsLeaf()) return new Range1i(0,0);
                        else
                        {
                            Range1i range = new Range1i(int.MaxValue,int.MinValue);

                            foreach(var nr in node.SubNodes)
                            {
                                if (nr == null) continue;
                                var n = nr.Value.GetCellAttribute(self);
                                n.Min += 1;
                                n.Max += 1;

                                if (n.Min < range.Min) range.Min = n.Min;
                                if (n.Max > range.Max) range.Max = n.Max;
                            }

                            return range;
                        }
                    }
                }
            );


        public static readonly CellAttribute<int> TreeMinDepth =
            new CellAttribute<int>(
                new Guid(0x42edbdd6, 0xa29e, 0x4dfd, 0x98, 0x36, 0x05, 0x0a, 0xb7, 0xfa, 0x4e, 0x31),
                "TreeMinDepth",
                (self, node) => node.GetCellAttribute(TreeMinMaxDepth).Min,
                TreeMinMaxDepth
            );

        public static readonly CellAttribute<int> TreeMaxDepth =
            new CellAttribute<int>(
                new Guid(0xd6f54b9e, 0xe907, 0x46c5, 0x91, 0x06, 0xd2, 0x6c, 0xd4, 0x53, 0xdc, 0x97),
                "TreeMaxDepth",
                (self, node) => node.GetCellAttribute(TreeMinMaxDepth).Max,
                TreeMinMaxDepth
            );



        ///// <summary>
        ///// min- and max-depth for the subtree (distances to leaf)
        ///// </summary>
        //public static readonly Guid TreeDepthMinMax         = new Guid(0xf1eb6598, 0x94e6, 0x41aa, 0xa8, 0xa3, 0xd6, 0x44, 0x60, 0xf6, 0xf8, 0x43);

    }

}
