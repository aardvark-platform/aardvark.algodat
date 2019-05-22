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
    public static class IPointCloudNodeExtensions
    {
        #region Has*

        private static bool Has(IPointCloudNode n, Durable.Def what)
        {
            switch (n.FilterState)
            {
                case FilterState.FullyOutside:
                    return false;
                case FilterState.FullyInside:
                case FilterState.Partial:
                    return n.Data.ContainsKey(what);
                default:
                    throw new InvalidOperationException($"Unknown FilterState {n.FilterState}.");
            }
        }

        /// <summary> </summary>
        public static bool HasPositions(this IPointCloudNode self) => Has(self, Durable.Octree.PositionsLocal3fReference);

        /// <summary></summary>
        public static bool HasColors(this IPointCloudNode self) => Has(self, Durable.Octree.Colors3bReference);

        /// <summary></summary>
        public static bool HasNormals(this IPointCloudNode self) => Has(self, Durable.Octree.Normals3fReference);

        /// <summary></summary>
        public static bool HasIntensities(this IPointCloudNode self) => Has(self, Durable.Octree.Intensities1iReference);

        /// <summary></summary>
        public static bool HasKdTree(this IPointCloudNode self) => Has(self, Durable.Octree.PointRkdTreeFDataReference);
        
        /// <summary></summary>
        public static bool HasClassifications(this IPointCloudNode self) => Has(self, Durable.Octree.Classifications1bReference);

        #endregion

        #region Get*

        private static PersistentRef<T> GetValue<T>(IPointCloudNode self, Durable.Def keyData,
            Durable.Def keyRef, Func<string, T> get, Func<string, (bool, T)> tryGet
            ) where T : class
        {
            if (self.Data.TryGetValue(keyData, out var value))
            {
                if (value is T x) return PersistentRef<T>.FromValue(x);
                if (value is PersistentRef<T> pref) return pref;
            }

            if (self.Data.TryGetValue(keyRef, out var o) && o is Guid id)
                return new PersistentRef<T>(id.ToString(), get, tryGet);

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

        /// <summary>
        /// Point colors, or null if no points.
        /// </summary>
        public static PersistentRef<C4b[]> GetColors4b(this IPointCloudNode self)
            => GetValue(self, Durable.Octree.Colors4b, Durable.Octree.Colors4bReference, self.Storage.GetC4bArray, self.Storage.TryGetC4bArray);

        /// <summary>
        /// </summary>
        public static PersistentRef<V3f[]> GetNormals3f(this IPointCloudNode self)
            => GetValue(self, Durable.Octree.Normals3f, Durable.Octree.Normals3fReference, self.Storage.GetV3fArray, self.Storage.TryGetV3fArray);

        /// <summary>
        /// </summary>
        public static PersistentRef<int[]> GetIntensities(this IPointCloudNode self)
            => GetValue(self, Durable.Octree.Intensities1i, Durable.Octree.Intensities1iReference, self.Storage.GetIntArray, self.Storage.TryGetIntArray);

        /// <summary>
        /// </summary>
        public static PersistentRef<byte[]> GetClassifications(this IPointCloudNode self)
            => GetValue(self, Durable.Octree.Classifications1b, Durable.Octree.Classifications1bReference, self.Storage.GetByteArray, self.Storage.TryGetByteArray);

        #endregion

        #region Storage

        /// <summary></summary>
        public static void Add(this Storage storage, string key, IPointCloudNode data)
        {
            storage.f_add(key, data, () =>
            {
                var json = data.ToJson().ToString();
                var buffer = Encoding.UTF8.GetBytes(json);
                return buffer;
            });
        }

        /// <summary></summary>
        public static IPointCloudNode GetPointCloudNode(this Storage storage, string key, IStoreResolver resolver)
        {
            if (storage.HasCache && storage.Cache.TryGetValue(key, out object o)) return (IPointCloudNode)o;
            
            var buffer = storage.f_get(key);
            if (buffer == null) return null;
            var json = JObject.Parse(Encoding.UTF8.GetString(buffer));

            IPointCloudNode data = null;
            var nodeType = (string)json["NodeType"];
            switch (nodeType)
            {
                case LinkedNode.Type:
                    data = LinkedNode.Parse(json, storage, resolver);
                    break;
                case PointCloudNode.Type:
                    data = PointCloudNode.Parse(json, storage, resolver);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown node type '{nodeType}'.");
            }
            
            return data;
        }

        #endregion

        /// <summary></summary>
        public static bool IsLeaf(this IPointCloudNode self) => self.SubNodes == null;

        /// <summary></summary>
        public static bool IsNotLeaf(this IPointCloudNode self) => self.SubNodes != null;

        /// <summary>
        /// Counts ALL nodes of this tree by traversing over all persistent refs.
        /// </summary>
        public static long CountNodes(this IPointCloudNode self)
        {
            if (self == null) return 0;

            var subnodes = self.SubNodes;
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

        #region Intersections, inside/outside, ...

        /// <summary>
        /// Index of subnode for given point.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetSubIndex(this IPointCloudNode self, in V3d p)
        {
            var i = 0;
            if (p.X > self.Center.X) i = 1;
            if (p.Y > self.Center.Y) i += 2;
            if (p.Z > self.Center.Z) i += 4;
            return i;
        }

        /// <summary>
        /// Returns true if this node intersects the positive halfspace defined by given plane.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IntersectsPositiveHalfSpace(this IPointCloudNode self, in Plane3d plane)
        {
            var corners = self.BoundingBoxExact.ComputeCorners();
            for (var i = 0; i < 8; i++)
            {
                if (plane.Height(corners[i]) > 0) return true;
            }
            return false;
        }

        /// <summary>
        /// Returns true if this node intersects the negative halfspace defined by given plane.
        /// </summary>
        public static bool IntersectsNegativeHalfSpace(this IPointCloudNode self, in Plane3d plane)
        {
            var corners = self.BoundingBoxExact.ComputeCorners();
            for (var i = 0; i < 8; i++)
            {
                if (plane.Height(corners[i]) < 0) return true;
            }
            return false;
        }

        /// <summary>
        /// Returns true if this node is fully inside the positive halfspace defined by given plane.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool InsidePositiveHalfSpace(this IPointCloudNode self, in Plane3d plane)
        {
            self.BoundingBoxExact.GetMinMaxInDirection(plane.Normal, out V3d min, out V3d max);
            return plane.Height(min) > 0;
        }

        /// <summary>
        /// Returns true if this node is fully inside the negative halfspace defined by given plane.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool InsideNegativeHalfSpace(this IPointCloudNode self, in Plane3d plane)
        {
            self.BoundingBoxExact.GetMinMaxInDirection(-plane.Normal, out V3d min, out V3d max);
            return plane.Height(min) < 0;
        }

        /// <summary>
        /// </summary>
        public static (bool, IPointCloudNode) TryGetPointCloudNode(this Storage storage, string id)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region ForEach (optionally traversing out-of-core nodes) 

        /// <summary>
        /// Calls action for each node in this tree.
        /// </summary>
        public static void ForEachNode(this IPointCloudNode self, bool outOfCore, Action<IPointCloudNode> action)
        {
            action(self);

            if (self.SubNodes == null) return;

            if (outOfCore)
            {
                for (var i = 0; i < 8; i++)
                {
                    self.SubNodes[i]?.Value.ForEachNode(outOfCore, action);
                }
            }
            else
            {
                for (var i = 0; i < 8; i++)
                {
                    var n = self.SubNodes[i];
                    if (n != null)
                    {
                        if (n.TryGetValue(out IPointCloudNode node)) node.ForEachNode(outOfCore, action);
                    }
                }
            }
        }

        #endregion
    }
}
