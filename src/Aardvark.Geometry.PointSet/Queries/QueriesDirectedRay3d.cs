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
        /// Enumerates Points within given distance of a Ray3d whose Ts lie between tMin and tMax. Chunks are approximately sorted along the ray direction.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNearRay(
                this PointSet ps, 
                Ray3d ray,
                double maxDistanceToRay,
                double tMin,
                double tMax,
                int minCellExponent = int.MinValue
            ) 
        {
            if(ps.Root == null) return Enumerable.Empty<Chunk>();
            return ps.Root.Value.QueryPointsNearRay(ray,maxDistanceToRay,tMin,tMax,minCellExponent);
        } 

        /// <summary>
        /// Enumerates Points within given distance of a Ray3d whose Ts lie between tMin and tMax. Chunks are approximately sorted along the ray direction.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNearRay(
                this IPointCloudNode node, 
                Ray3d ray,
                double maxDistanceToRay,
                double tMin,
                double tMax,
                int minCellExponent = int.MinValue
            ) 
        {
            var fastRay = new FastRay3d(ray);

            double t0 = System.Double.NegativeInfinity;
            double t1 = System.Double.PositiveInfinity;

            var box = 
                Box3d.FromCenterAndSize(
                    node.BoundingBoxExactGlobal.Center, 
                    node.BoundingBoxExactGlobal.Size + V3d.One * maxDistanceToRay
                );
            if(fastRay.Intersects(box, ref t0, ref t1)) {
                if(t1 < tMin || t0 > tMax) { yield break; }

                if (node.IsLeaf || node.Cell.Exponent == minCellExponent) {
                    var qs = node.Positions.Value;

                    var ps = default(List<V3d>);
                    var cs = default(List<C4b>);
                    var ns = default(List<V3f>);
                    var js = default(List<int>);
                    var ks = default(List<byte>);

                    for (var i = 0; i < qs.Length; i++)
                    {
                        var pWorld = (V3d)qs[i] + node.Center;
                        var d = ray.GetMinimalDistanceTo(pWorld);
                        var tp = ray.GetTOfProjectedPoint(pWorld);
                        if (d > maxDistanceToRay || tp < tMin || tp > tMax) continue;
                        if (ps == null) Init();

                        ps.Add(pWorld);
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
                } else {
                    var sorted = 
                        node.Subnodes.OrderBy(c => 
                            (c==null)?System.Double.PositiveInfinity:
                                Vec.Distance(
                                    new V3d(c.Value.BoundingBoxExactGlobal.Center),
                                    ray.Origin
                                )
                        );
                    foreach(var c in sorted) {
                        if(c==null) continue;
                        if (t0 > tMin) { tMin = t0; }
                        if (t1 < tMax) { tMax = t1; }
                        var ress = c.Value.QueryPointsNearRay(ray, maxDistanceToRay, tMin, tMax, minCellExponent);
                        foreach(var res in ress) yield return res;
                    }
                } // node is leafish
            }
            else {
                yield break;
            } // ray intersects bounds
        } // QueryPointsNearRay
    } // static partial class
} // namespace