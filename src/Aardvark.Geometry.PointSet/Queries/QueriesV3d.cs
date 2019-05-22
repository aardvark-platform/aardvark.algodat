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
#define PARANOID
using Aardvark.Base;
using System;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public static partial class Queries
    {
        #region Query points

        /// <summary>
        /// Points within given distance of a point.
        /// </summary>
        public static PointsNearObject<V3d> QueryPointsNearPoint(
            this PointSet self, V3d query, double maxDistanceToPoint, int maxCount
            )
            => QueryPointsNearPoint(self.Octree.Value, query, maxDistanceToPoint, maxCount);

        /// <summary>
        /// Points within given distance of a point.
        /// </summary>
        public static PointsNearObject<V3d> QueryPointsNearPoint(
            this IPointCloudNode node, V3d query, double maxDistanceToPoint, int maxCount
            )
        {
            if (node == null) return PointsNearObject<V3d>.Empty;

            // if query point is farther from bounding box than maxDistanceToPoint,
            // then there cannot be a result and we are done
            var eps = node.BoundingBoxExact.Distance(query);
            if (eps > maxDistanceToPoint) return PointsNearObject<V3d>.Empty;

            if (node.IsLeaf())
            {
                var nodePositions = node.GetPositions();
                #if PARANOID
                if (nodePositions.Value.Length <= 0) throw new InvalidOperationException();
                #endif

                var center = node.Center;

                var ia = node.GetKdTree(nodePositions.Value).Value.GetClosest((V3f)(query - center), (float)maxDistanceToPoint, maxCount);
                if (ia.Count > 0)
                {
                    var ps = new V3d[ia.Count];
                    var cs = node.HasColors() ? new C4b[ia.Count] : null;
                    var ns = node.HasNormals() ? new V3f[ia.Count] : null;
                    var js = node.HasIntensities() ? new int[ia.Count] : null;
                    var ks = node.HasClassifications() ? new byte[ia.Count] : null;
                    var ds = new double[ia.Count];
                    for (var i = 0; i < ia.Count; i++)
                    {
                        var index = (int)ia[i].Index;
                        ps[i] = center + (V3d)node.GetPositions().Value[index];
                        if (node.HasColors()) cs[i] = node.GetColors4b().Value[index];
                        if (node.HasNormals()) ns[i] = node.GetNormals3f().Value[index];
                        if (node.HasIntensities()) js[i] = node.GetIntensities().Value[index];
                        if (node.HasClassifications()) js[i] = node.GetClassifications().Value[index];
                        ds[i] = ia[i].Dist;
                    }
                    var chunk = new PointsNearObject<V3d>(query, maxDistanceToPoint, ps, cs, ns, js, ks, ds);
                    return chunk;
                }
                else
                {
                    return PointsNearObject<V3d>.Empty;
                }
            }
            else
            {
                // first traverse octant containing query point
                var index = node.GetSubIndex(query);
                var n = node.SubNodes[index];
                var result = n != null ? n.Value.QueryPointsNearPoint(query, maxDistanceToPoint, maxCount) : PointsNearObject<V3d>.Empty;
                if (!result.IsEmpty && result.MaxDistance < maxDistanceToPoint) maxDistanceToPoint = result.MaxDistance;

                // now traverse other octants
                for (var i = 0; i < 8; i++)
                {
                    if (i == index) continue;
                    n = node.SubNodes[i];
                    if (n == null) continue;
                    var x = n.Value.QueryPointsNearPoint(query, maxDistanceToPoint, maxCount);
                    result = result.Merge(x, maxCount);
                    if (!result.IsEmpty && result.MaxDistance < maxDistanceToPoint) maxDistanceToPoint = result.MaxDistance;
                }

                return result;
            }
        }

        #endregion
    }
}
