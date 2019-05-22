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
using System;
using System.Collections.Generic;
using System.Linq;
using Aardvark.Base;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public static partial class Queries
    {
        /// <summary>
        /// Points within given distance of a ray (at most 1000).
        /// </summary>
        public static IEnumerable<PointsNearObject<Line3d>> QueryPointsNearRay(
            this PointSet self, Ray3d ray, double maxDistanceToRay
            )
        {
            ray.Direction = ray.Direction.Normalized;
            var data = self.Octree.Value;
            var bbox = data.BoundingBoxExact;

            var line = Clip(bbox, ray);
            if (!line.HasValue) return Enumerable.Empty<PointsNearObject<Line3d>>();

            return self.QueryPointsNearLineSegment(line.Value, maxDistanceToRay);
        }

        /// <summary>
        /// Points within given distance of a line segment (at most 1000).
        /// </summary>
        public static IEnumerable<PointsNearObject<Line3d>> QueryPointsNearLineSegment(
            this PointSet self, Line3d lineSegment, double maxDistanceToRay
            )
            => QueryPointsNearLineSegment(self.Octree.Value, lineSegment, maxDistanceToRay);

        /// <summary>
        /// Points within given distance of a line segment (at most 1000).
        /// </summary>
        public static IEnumerable<PointsNearObject<Line3d>> QueryPointsNearLineSegment(
            this IPointCloudNode node, Line3d lineSegment, double maxDistanceToRay
            )
        {
            if (!node.BoundingBoxExact.Intersects(lineSegment))
            {
                yield break;
            }
            else
            {
                var nodePositions = node.GetPositions();
                if ((nodePositions?.Value.Length ?? 0) > 0)
                {
                    var center = node.Center;
                    var ia = node.GetKdTree(nodePositions.Value).Value.GetClosestToLine((V3f)(lineSegment.P0 - center), (V3f)(lineSegment.P1 - center), (float)maxDistanceToRay, 1000);
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
                            if (node.HasClassifications()) ks[i] = node.GetClassifications().Value[index];
                            ds[i] = ia[i].Dist;
                        }
                        var chunk = new PointsNearObject<Line3d>(lineSegment, maxDistanceToRay, ps, cs, ns, js, ks, ds);
                        yield return chunk;
                    }
                }
                else if (node.SubNodes != null)
                {
                    for (var i = 0; i < 8; i++)
                    {
                        var n = node.SubNodes[i];
                        if (n == null) continue;
                        foreach (var x in QueryPointsNearLineSegment(n.Value, lineSegment, maxDistanceToRay)) yield return x;
                    }
                }
            }
        }
        
        /// <summary>
        /// Clips given ray on box, or returns null if ray does not intersect box.
        /// </summary>
        private static Line3d? Clip(Box3d box, Ray3d ray0)
        {
            ray0.Direction = ray0.Direction.Normalized;

            if (!box.Intersects(ray0, out double t0)) return null;
            var p0 = ray0.GetPointOnRay(t0);

            var ray1 = new Ray3d(ray0.GetPointOnRay(t0 + box.Size.Length), -ray0.Direction);
            if (!box.Intersects(ray1, out double t1)) throw new InvalidOperationException();
            var p1 = ray1.GetPointOnRay(t1);

            return new Line3d(p0, p1);
        }
    }
}
