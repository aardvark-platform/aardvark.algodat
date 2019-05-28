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
using Newtonsoft.Json.Linq;
using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public static class IPointCloudNodeExtensions2
    {
        #region Has*

        /// <summary> </summary>
        public static bool HasPositions(this IPointCloudNode self) => self.Has(Durable.Octree.PositionsLocal3fReference);

        /// <summary></summary>
        public static bool HasColors(this IPointCloudNode self) => self.Has(Durable.Octree.Colors4bReference);

        /// <summary></summary>
        public static bool HasNormals(this IPointCloudNode self) => self.Has(Durable.Octree.Normals3fReference);

        /// <summary></summary>
        public static bool HasIntensities(this IPointCloudNode self) => self.Has(Durable.Octree.Intensities1iReference);

        /// <summary></summary>
        public static bool HasKdTree(this IPointCloudNode self) => self.Has(Durable.Octree.PointRkdTreeFDataReference);
        
        /// <summary></summary>
        public static bool HasClassifications(this IPointCloudNode self) => self.Has(Durable.Octree.Classifications1bReference);

        #endregion

        #region Get*

        private static PersistentRef<T> GetValue<T>(IPointCloudNode self, 
            Durable.Def keyData, Durable.Def keyRef,
            Func<string, T> get, Func<string, (bool, T)> tryGet
            ) where T : class
        {
            if (self.TryGetValue(keyData, out var value))
            {
                if (value is T x) return PersistentRef<T>.FromValue(x);
                if (value is PersistentRef<T> pref) return pref;
            }

            if (self.TryGetValue(keyRef, out var o) && o is Guid id)
                return new PersistentRef<T>(id.ToString(), get, tryGet);

//#if DEBUG
//            Console.WriteLine($"keyData: {keyData}");
//            Console.WriteLine($"keyRef : {keyRef}");
//            foreach (var kv in self.Data) Console.WriteLine($"{kv.Key} - {kv.Value}");
//#endif
            throw new InvalidOperationException($"Invariant 0725615a-a9a3-4989-86bd-a0b5708b2283. {keyData}. {keyRef}.");
        }

        /// <summary>
        /// Point positions relative to cell's center, or null if no positions.
        /// </summary>
        public static PersistentRef<V3f[]> GetPositions(this IPointCloudNode self)
            => GetValue(self, Durable.Octree.PositionsLocal3f, Durable.Octree.PositionsLocal3fReference, self.Storage.GetV3fArray, self.Storage.TryGetV3fArray);

        /// <summary>
        /// Point positions (absolute), or null if no positions.
        /// </summary>
        public static V3d[] GetPositionsAbsolute(this IPointCloudNode self)
        {
            var ps = GetPositions(self);
            if (ps == null) return null;
            var c = self.Center;
            return ps.Value.Map(p => new V3d(p.X + c.X, p.Y + c.Y, p.Z + c.Z));
        }

        /// <summary>
        /// </summary>
        public static PersistentRef<PointRkdTreeF<V3f[], V3f>> GetKdTree(this IPointCloudNode self, V3f[] ps)
            => GetValue(self, Durable.Octree.PointRkdTreeFData, Durable.Octree.PointRkdTreeFDataReference,
                s => self.Storage.GetKdTreeF(s, ps), s => self.Storage.TryGetKdTreeF(s, ps));


        /// <summary>Point colors.</summary>
        public static PersistentRef<C4b[]> GetColors4b(this IPointCloudNode self)
            => GetValue(self, Durable.Octree.Colors4b, Durable.Octree.Colors4bReference, self.Storage.GetC4bArray, self.Storage.TryGetC4bArray);
        /// <summary>Returns null if node has no colors.</summary>
        public static PersistentRef<C4b[]> TryGetColors4b(this IPointCloudNode self)
            => self.HasColors() ? self.GetColors4b() : null;


        /// <summary></summary>
        public static PersistentRef<V3f[]> GetNormals3f(this IPointCloudNode self)
            => GetValue(self, Durable.Octree.Normals3f, Durable.Octree.Normals3fReference, self.Storage.GetV3fArray, self.Storage.TryGetV3fArray);
        /// <summary>Returns null if node has no normals.</summary>
        public static PersistentRef<V3f[]> TryGetNormals3f(this IPointCloudNode self)
            => self.HasNormals() ? self.GetNormals3f() : null;


        /// <summary></summary>
        public static PersistentRef<int[]> GetIntensities(this IPointCloudNode self)
            => GetValue(self, Durable.Octree.Intensities1i, Durable.Octree.Intensities1iReference, self.Storage.GetIntArray, self.Storage.TryGetIntArray);
        /// <summary>Returns null if node has no intensities.</summary>
        public static PersistentRef<int[]> TryGetIntensities(this IPointCloudNode self)
            => self.HasIntensities() ? self.GetIntensities() : null;


        /// <summary></summary>
        public static PersistentRef<byte[]> GetClassifications(this IPointCloudNode self)
            => GetValue(self, Durable.Octree.Classifications1b, Durable.Octree.Classifications1bReference, self.Storage.GetByteArray, self.Storage.TryGetByteArray);
        /// <summary>Returns null if node has no classifications.</summary>
        public static PersistentRef<byte[]> TryGetClassifications(this IPointCloudNode self)
            => self.HasClassifications() ? self.GetClassifications() : null;

        #endregion

        /// <summary></summary>
        public static bool IsLeaf(this IPointCloudNode self) => self.Subnodes == null;

        /// <summary></summary>
        public static bool IsNotLeaf(this IPointCloudNode self) => self.Subnodes != null;
    }
}
