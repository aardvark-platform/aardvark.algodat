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
        /// <summary></summary>
        public const string Classifications = "Classifications";
        /// <summary></summary>
        public const string Colors = "Colors";
        /// <summary></summary>
        public const string Intensities = "Intensities";
        /// <summary></summary>
        public const string KdTree = "KdTree";
        /// <summary></summary>
        public const string LodClassifications = "LodClassifications";
        /// <summary></summary>
        public const string LodColors = "LodColors";
        /// <summary></summary>
        public const string LodIntensities = "LodIntensities";
        /// <summary></summary>
        public const string LodKdTree = "LodKdTree";
        /// <summary></summary>
        public const string LodNormals = "LodNormals";
        /// <summary></summary>
        public const string LodPositions = "LodPositions";
        /// <summary></summary>
        public const string Normals = "Normals";
        /// <summary></summary>
        public const string Positions = "Positions";

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
        public static object CreatePersistentRef(this Storage storage, string attributeName, string key)
        {
            switch (attributeName)
            {
                case Classifications:       return new PersistentRef<byte[]>(key, (id, ct) => storage.GetByteArray(id, ct));
                case Colors:                return new PersistentRef<C4b[]>(key, (id, ct) => storage.GetC4bArray(id, ct));
                case Intensities:           return new PersistentRef<int[]>(key, (id, ct) => storage.GetIntArray(id, ct));
                case KdTree:                return new PersistentRef<PointRkdTreeDData>(key, (id, ct) => storage.GetPointRkdTreeDData(id, ct));
                case LodClassifications:    return new PersistentRef<byte[]>(key, (id, ct) => storage.GetByteArray(id, ct));
                case LodColors:             return new PersistentRef<C4b[]>(key, (id, ct) => storage.GetC4bArray(id, ct));
                case LodIntensities:        return new PersistentRef<byte[]>(key, (id, ct) => storage.GetByteArray(id, ct));
                case LodKdTree:             return new PersistentRef<PointRkdTreeDData>(key, (id, ct) => storage.GetPointRkdTreeDData(id, ct));
                case LodNormals:            return new PersistentRef<V3f[]>(key, (id, ct) => storage.GetV3fArray(id, ct));
                case LodPositions:          return new PersistentRef<V3f[]>(key, (id, ct) => storage.GetV3fArray(id, ct));
                case Normals:               return new PersistentRef<V3f[]>(key, (id, ct) => storage.GetV3fArray(id, ct));
                case Positions:             return new PersistentRef<V3f[]>(key, (id, ct) => storage.GetV3fArray(id, ct));

                default: throw new InvalidOperationException($"Cannot convert '{attributeName}' to property.");
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
    }
}
