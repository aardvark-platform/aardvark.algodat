/*
    Copyright (C) 2006-2020. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using Aardvark.Data.Points;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public static partial class Queries
    {
        /// <summary>
        /// Points within given distance of a ray.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNearRay(
            this PointSet self, Ray3d ray, double maxDistanceToRay, int minCellExponent = int.MinValue
            )
        {
            ray.Direction = ray.Direction.Normalized;
            var data = self.Root.Value;
            var bbox = data.BoundingBoxExactGlobal;

            var line = Clip(bbox, ray);
            if (!line.HasValue) return Enumerable.Empty<Chunk>();

            return self.QueryPointsNearLineSegment(line.Value, maxDistanceToRay, minCellExponent);
        }

        /// <summary>
        /// Points within given distance of a line segment (at most 1000).
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNearLineSegment(
            this PointSet self, Line3d lineSegment, double maxDistanceToRay, int minCellExponent = int.MinValue
            )
            => QueryPointsNearLineSegment(self.Root.Value, lineSegment, maxDistanceToRay, minCellExponent);

        /// <summary>
        /// Points within given distance of a line segment (at most 1000).
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNearLineSegment(
            this IPointCloudNode node, Line3d lineSegment, double maxDistanceToRay, int minCellExponent = int.MinValue
            )
        {
            if (!node.HasPositions) yield break;

            var centerGlobal = node.Center;
            var s0Local = lineSegment.P0 - centerGlobal;
            var s1Local = lineSegment.P1 - centerGlobal;
            var rayLocal = new Ray3d(s0Local, (s1Local - s0Local).Normalized);

            var worstCaseDist = node.BoundingBoxExactLocal.Size3d.Length * 0.5 + maxDistanceToRay;
            var d0 = rayLocal.GetMinimalDistanceTo((V3d)node.BoundingBoxExactLocal.Center);
            if (d0 > worstCaseDist) yield break;

            if (node.IsLeaf || node.Cell.Exponent == minCellExponent)
            {
                if (node.HasKdTree)
                {
                    var ia = node.KdTree.Value.GetClosestToLine(
                        (V3f)s0Local, (V3f)s1Local,
                        (float)maxDistanceToRay,
                        node.PointCountCell
                        );

                    if (ia.Count > 0)
                    {
                        var ps = new V3d[ia.Count];
                        var cs = node.HasColors ? new C4b[ia.Count] : null;
                        var ns = node.HasNormals ? new V3f[ia.Count] : null;
                        var js = node.HasIntensities ? new int[ia.Count] : null;
                        var ks = node.HasClassifications ? new byte[ia.Count] : null;
                        var ds = new double[ia.Count];
                        for (var i = 0; i < ia.Count; i++)
                        {
                            var index = (int)ia[i].Index;
                            ps[i] = centerGlobal + (V3d)node.Positions.Value[index];
                            if (node.HasColors) cs[i] = node.Colors.Value[index];
                            if (node.HasNormals) ns[i] = node.Normals.Value[index];
                            if (node.HasIntensities) js[i] = node.Intensities.Value[index];
                            if (node.HasClassifications) ks[i] = node.Classifications.Value[index];
                            ds[i] = ia[i].Dist;
                        }
                        var chunk = new Chunk(ps, cs, ns, js, ks);
                        yield return chunk;
                    }
                }
                else
                {
                    // do it without kd-tree ;-)
                    var qs = node.Positions.Value;

                    var ps = default(List<V3d>);
                    var cs = default(List<C4b>);
                    var ns = default(List<V3f>);
                    var js = default(List<int>);
                    var ks = default(List<byte>);

                    for (var i = 0; i < qs.Length; i++)
                    {
                        var d = rayLocal.GetMinimalDistanceTo((V3d)qs[i]);
                        if (d > maxDistanceToRay) continue;
                        if (ps == null) Init();

                        ps.Add((V3d)qs[i] + centerGlobal);
                        if (node.HasColors) cs.Add(node.Colors.Value[i]);
                        if (node.HasNormals) ns.Add(node.Normals.Value[i]);
                        if (node.HasIntensities) js.Add(node.Intensities.Value[i]);
                        if (node.HasClassifications) ks.Add(node.Classifications.Value[i]);
                    }

                    if (ps != null)
                    {
                        yield return new Chunk(ps, cs, ns, js, ks);
                    }

                    void Init()
                    {
                        ps = new List<V3d>();
                        cs = node.HasColors ? new List<C4b>() : null;
                        ns = node.HasNormals ? new List<V3f>() : null;
                        js = node.HasIntensities ? new List<int>() : null;
                        ks = node.HasClassifications ? new List<byte>() : null;
                    }
                }
            }
            else // inner node
            {
                for (var i = 0; i < 8; i++)
                {
                    var n = node.Subnodes[i];
                    if (n == null) continue;
                    var xs = QueryPointsNearLineSegment(n.Value, lineSegment, maxDistanceToRay);
                    foreach (var x in xs) yield return x;
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
