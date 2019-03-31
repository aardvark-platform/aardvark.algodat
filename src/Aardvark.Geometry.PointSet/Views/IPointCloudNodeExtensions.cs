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
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public static class IPointCloudNodeExtensions
    {
        #region Has*

        private static bool Has(IPointCloudNode n, DurableDataDefinition what)
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
        public static bool HasPositions(this IPointCloudNode self) => Has(self, OctreeAttributes.RefPositionsLocal3f);

        /// <summary></summary>
        public static bool HasColors(this IPointCloudNode self) => Has(self, OctreeAttributes.RefColors3b);

        /// <summary></summary>
        public static bool HasNormals(this IPointCloudNode self) => Has(self, OctreeAttributes.RefNormals3f);

        /// <summary></summary>
        public static bool HasIntensities(this IPointCloudNode self) => Has(self, OctreeAttributes.RefIntensities1i);

        /// <summary></summary>
        public static bool HasKdTree(this IPointCloudNode self) => Has(self, OctreeAttributes.RefKdTreeLocal3f);
        
        /// <summary></summary>
        public static bool HasClassifications(this IPointCloudNode self) => Has(self, OctreeAttributes.RefClassifications1b);

        #endregion

        #region Get*

        private static PersistentRef<T> GetValue<T>(IPointCloudNode self, DurableDataDefinition key) where T : class
        {
            throw new NotImplementedException();
            //if (self.TryGetPropertyValue(key, out object value))
            //{
            //    var arr = value as T;
            //    if (arr != null) return PersistentRef<T>.FromValue(arr);

            //    var pref = value as PersistentRef<T>;
            //    if (pref != null) return pref;
            //}

            //return null;
        }

        /// <summary>
        /// Point positions relative to cell's center, or null if no positions.
        /// </summary>
        public static PersistentRef<V3f[]> GetPositions(this IPointCloudNode self) =>
            GetValue<V3f[]>(self, OctreeAttributes.RefPositionsLocal3f);

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
        public static PersistentRef<PointRkdTreeD<V3f[], V3f>> GetKdTree(this IPointCloudNode self)
        {
            var res = GetValue<PointRkdTreeD<V3f[], V3f>>(self, OctreeAttributes.RefKdTreeLocal3f);
            if (res != null) return res;

            var data = GetValue<PointRkdTreeDData>(self, OctreeAttributes.RefKdTreeLocal3f);
            if(data != null)
            {
                var ps = GetPositions(self);
                PointRkdTreeD<V3f[], V3f> Get(string id)
                {
                    var pos = ps.Value;
                    return new PointRkdTreeD<V3f[], V3f>(
                        3, pos.Length, pos,
                        (xs, i) => xs[(int)i], (v, i) => (float)v[i],
                        (a, b) => V3f.Distance(a, b), (i, a, b) => b - a,
                        (a, b, c) => VecFun.DistanceToLine(a, b, c), VecFun.Lerp, 1e-9,
                        data.Value
                    );
                }


                (bool, PointRkdTreeD<V3f[], V3f>) TryGet(string id)
                {
                    return (true, Get(id));
                }

                return new PersistentRef<PointRkdTreeD<V3f[], V3f>>(data.Id, Get, TryGet);
            }

            return null;
        }

        /// <summary>
        /// Point colors, or null if no points.
        /// </summary>
        public static PersistentRef<C4b[]> GetColors(this IPointCloudNode self)
            => GetValue<C4b[]>(self, OctreeAttributes.RefColors3b);

        /// <summary>
        /// </summary>
        public static PersistentRef<V3f[]> GetNormals(this IPointCloudNode self)
            => GetValue<V3f[]>(self, OctreeAttributes.RefNormals3f);

        /// <summary>
        /// </summary>
        public static PersistentRef<int[]> GetIntensities(this IPointCloudNode self)
            => GetValue<int[]>(self, OctreeAttributes.RefIntensities1i);

        /// <summary>
        /// </summary>
        public static PersistentRef<byte[]> GetClassifications(this IPointCloudNode self)
            => GetValue<byte[]>(self, OctreeAttributes.RefClassifications1b);

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
