/*
    Copyright (C) 2006-2023. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using Aardvark.Data;
using Aardvark.Data.Points;
using System;
using System.Collections.Generic;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public static partial class Queries
    {
        #region Query points

        /// <summary>
        /// Points within given distance to a query point.
        /// </summary>
        public static PointsNearObject<V3d> QueryPointsNearPoint(
            this PointSet self, V3d query, double maxDistanceToPoint, int maxCount
            )
            => QueryPointsNearPoint(self.Root.Value, query, maxDistanceToPoint, maxCount);

#if TODO
        /// <summary>
        /// Points within given distance to a query point.
        /// </summary>
        public static PointsNearObject<V3d> QueryPointsNearPointCustom(
            this PointSet self, V3d query, double maxDistanceToPoint, int maxCount, params Durable.Def[] customAttributes
            )
            => QueryPointsNearPointCustom(self.Root.Value, query, maxDistanceToPoint, maxCount, customAttributes);
#endif
        /// <summary>
        /// Points within given distance to a query point.
        /// </summary>
        public static PointsNearObject<V3d> QueryPointsNearPoint(
            this IPointCloudNode node, V3d query, double maxDistanceToPoint, int maxCount
            )
        {
            if (node == null) return PointsNearObject<V3d>.Empty;

            // if query point is farther from bounding box than maxDistanceToPoint,
            // then there cannot be a result and we are done
            var eps = node.BoundingBoxExactGlobal.Distance(query);
            if (eps > maxDistanceToPoint) return PointsNearObject<V3d>.Empty;

            if (node.IsLeaf())
            {
                var nodePositions = node.Positions;
#if PARANOID
                if (nodePositions.Value.Length <= 0) throw new InvalidOperationException();
#endif

                var center = node.Center;

                var closest = node.KdTree!.Value.GetClosest((V3f)(query - center), (float)maxDistanceToPoint, maxCount).ToArray();
                if (closest.Length > 0)
                {
                    var ia = closest.Map(x => (int)x.Index);
                    var ds = closest.Map(x => (double)x.Dist);
                    var ps = node.PositionsAbsolute.Subset(ia);
                    var cs = node.Colors?.Value?.Subset(ia);
                    var ns = node.Normals?.Value?.Subset(ia);
                    var js = node.Intensities?.Value?.Subset(ia);
                    node.TryGetPartIndices(out var pis);
                    pis = pis?.Subset(ia);
                    var ks = node.Classifications?.Value?.Subset(ia);
                    var chunk = new PointsNearObject<V3d>(query, maxDistanceToPoint, ps, cs, ns, js, pis, ks, ds);
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
                var n = node.Subnodes![index];
                var result = n != null ? n.Value.QueryPointsNearPoint(query, maxDistanceToPoint, maxCount) : PointsNearObject<V3d>.Empty;
                if (!result.IsEmpty && result.MaxDistance < maxDistanceToPoint) maxDistanceToPoint = result.MaxDistance;

                // now traverse other octants
                for (var i = 0; i < 8; i++)
                {
                    if (i == index) continue;
                    n = node.Subnodes[i];
                    if (n == null) continue;
                    var x = n.Value.QueryPointsNearPoint(query, maxDistanceToPoint, maxCount);
                    result = result.Merge(x, maxCount);
                    if (!result.IsEmpty && result.MaxDistance < maxDistanceToPoint) maxDistanceToPoint = result.MaxDistance;
                }

                return result;
            }
        }

#if TODO
        /// <summary>
        /// Points within given distance to a query point.
        /// </summary>
        public static IEnumerable<GenericChunk> QueryPointsNearPointCustom(
            this IPointCloudNode node, V3d query, double maxDistanceToPoint, int maxCount, params Durable.Def[] customAttributes
            )
        {
            if (node == null) yield break;

            // if query point is farther from bounding box than maxDistanceToPoint,
            // then there cannot be a result and we are done
            var eps = node.BoundingBoxExactGlobal.Distance(query);
            if (eps > maxDistanceToPoint) yield break;

            if (node.IsLeaf())
            {
                var nodePositions = node.Positions;
#if PARANOID
                if (nodePositions.Value.Length <= 0) throw new InvalidOperationException();
#endif

                var center = node.Center;

                var closest = node.KdTree.Value.GetClosest((V3f)(query - center), (float)maxDistanceToPoint, maxCount).ToArray();
                if (closest.Length > 0)
                {
                    var ia = closest.Map(x => (int)x.Index);
                    var ds = closest.Map(x => (double)x.Dist);
                    var ps = node.PositionsAbsolute.Subset(ia);
                    var cs = node.Colors?.Value?.Subset(ia);
                    var ns = node.Normals?.Value?.Subset(ia);
                    var js = node.Intensities?.Value?.Subset(ia);
                    var ks = node.Classifications?.Value?.Subset(ia);
                    var chunk = new PointsNearObject<V3d>(query, maxDistanceToPoint, ps, cs, ns, js, ks, ds);
                    return chunk;
                }
                else
                {
                    yield break;
                }
            }
            else
            {
                // first traverse octant containing query point
                var index = node.GetSubIndex(query);
                var n = node.Subnodes[index];
                var xs = n.Value.QueryPointsNearPointCustom(query, maxDistanceToPoint, maxCount, customAttributes);
                foreach (var x in xs) yield return x;
                if (!result.IsEmpty && result.MaxDistance < maxDistanceToPoint) maxDistanceToPoint = result.MaxDistance;

                // now traverse other octants
                for (var i = 0; i < 8; i++)
                {
                    if (i == index) continue;
                    n = node.Subnodes[i];
                    if (n == null) continue;
                    var x = n.Value.QueryPointsNearPointCustom(query, maxDistanceToPoint, maxCount, customAttributes);
                    result = result.Merge(x, maxCount);
                    if (!result.IsEmpty && result.MaxDistance < maxDistanceToPoint) maxDistanceToPoint = result.MaxDistance;
                }

                return result;
            }
        }
#endif
#endregion
    }
}
