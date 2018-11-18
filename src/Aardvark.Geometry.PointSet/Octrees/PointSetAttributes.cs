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
        public const string LodClassifications = "LodClassifications";
        /// <summary>C4b[].</summary>
        public const string LodColors = "LodColors";
        /// <summary>int[].</summary>
        public const string LodIntensities = "LodIntensities";
        /// <summary>PointRkdTreeDData.</summary>
        public const string LodKdTree = "LodKdTree";
        /// <summary>V3f[].</summary>
        public const string LodNormals = "LodNormals";
        /// <summary>V3f[] relative to center.</summary>
        public const string LodPositions = "LodPositions";
        /// <summary>V3d[] absolute.</summary>
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
                case PointSetAttributes.LodClassifications: return LodClassifications;
                case PointSetAttributes.LodColors:          return LodColors;         
                case PointSetAttributes.LodIntensities:     return LodIntensities;    
                case PointSetAttributes.LodKdTree:          return LodKdTree;         
                case PointSetAttributes.LodNormals:         return LodNormals;        
                case PointSetAttributes.LodPositions:       return LodPositions;      
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
                case LodClassifications:    return PointSetAttributes.LodClassifications;
                case LodColors:             return PointSetAttributes.LodColors;
                case LodIntensities:        return PointSetAttributes.LodIntensities;
                case LodKdTree:             return PointSetAttributes.LodKdTree;
                case LodNormals:            return PointSetAttributes.LodNormals;
                case LodPositions:          return PointSetAttributes.LodPositions;
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
                case Classifications:       return new PersistentRef<byte[]>(key, (id, ct) => storage.GetByteArray(id, ct), (byte[])value);
                case Colors:                return new PersistentRef<C4b[]>(key, (id, ct) => storage.GetC4bArray(id, ct), (C4b[])value);
                case Intensities:           return new PersistentRef<int[]>(key, (id, ct) => storage.GetIntArray(id, ct), (int[])value);
                case KdTree:                return new PersistentRef<PointRkdTreeDData>(key, (id, ct) => storage.GetPointRkdTreeDData(id, ct), (PointRkdTreeDData)value);
                case LodClassifications:    return new PersistentRef<byte[]>(key, (id, ct) => storage.GetByteArray(id, ct), (byte[])value);
                case LodColors:             return new PersistentRef<C4b[]>(key, (id, ct) => storage.GetC4bArray(id, ct), (C4b[])value);
                case LodIntensities:        return new PersistentRef<int[]>(key, (id, ct) => storage.GetIntArray(id, ct), (int[])value);
                case LodKdTree:             return new PersistentRef<PointRkdTreeDData>(key, (id, ct) => storage.GetPointRkdTreeDData(id, ct), (PointRkdTreeDData)value);
                case LodNormals:            return new PersistentRef<V3f[]>(key, (id, ct) => storage.GetV3fArray(id, ct), (V3f[])value);
                case LodPositions:          return new PersistentRef<V3f[]>(key, (id, ct) => storage.GetV3fArray(id, ct), (V3f[])value);
                case Normals:               return new PersistentRef<V3f[]>(key, (id, ct) => storage.GetV3fArray(id, ct), (V3f[])value);
                case Positions:             return new PersistentRef<V3f[]>(key, (id, ct) => storage.GetV3fArray(id, ct), (V3f[])value);

                default: throw new InvalidOperationException($"Cannot convert '{attributeName}' to property.");
            }
        }

        /// <summary>
        /// </summary>
        public static void StoreAttribute(this Storage storage, string attributeName, string key, object value)
        {
            switch (attributeName)
            {
                case Classifications:       storage.Add(key, (byte[])value, default); break;
                case Colors:                storage.Add(key, (C4b[])value, default); break;
                case Intensities:           storage.Add(key, (int[])value, default); break;
                case KdTree:                storage.Add(key, (PointRkdTreeDData)value, default); break;
                case LodClassifications:    storage.Add(key, (byte[])value, default); break;
                case LodColors:             storage.Add(key, (C4b[])value, default); break;
                case LodIntensities:        storage.Add(key, (int[])value, default); break;
                case LodKdTree:             storage.Add(key, (PointRkdTreeDData)value, default); break;
                case LodNormals:            storage.Add(key, (V3f[])value, default); break;
                case LodPositions:          storage.Add(key, (V3f[])value, default); break;
                case Normals:               storage.Add(key, (V3f[])value, default); break;
                case Positions:             storage.Add(key, (V3f[])value, default); break;

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
        LodPositions        = 1 <<  5,

        /// <summary>
        /// C4b[].
        /// </summary>
        LodColors           = 1 <<  6,

        /// <summary>
        /// V3f[].
        /// </summary>
        LodNormals          = 1 <<  7,

        /// <summary>
        /// int[].
        /// </summary>
        LodIntensities      = 1 <<  8,

        /// <summary>
        /// PointRkdTreeD&lt;V3f[], V3f&gt;.
        /// </summary>
        LodKdTree           = 1 <<  9,

        /// <summary>
        /// byte[].
        /// </summary>
        Classifications     = 1 << 10,

        /// <summary>
        /// byte[].
        /// </summary>
        LodClassifications  = 1 << 11,

        /// <summary>
        /// Cell attributes.
        /// </summary>
        HasCellAttributes   = 1 << 23,
    }

    /// <summary>
    /// </summary>
    [Flags]
    public enum CellAttributes : uint
    {
        /// <summary>
        /// Box3f.
        /// </summary>
        BoundingBoxExactLocal = 1 <<  0,

        /// <summary>
        /// float.
        /// </summary>
        AveragePointDistance  = 1 <<  1,
    }
}
