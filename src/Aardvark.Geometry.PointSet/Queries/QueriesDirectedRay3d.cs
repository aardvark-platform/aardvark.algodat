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

namespace Aardvark.Geometry.Points;

/// <summary>
/// </summary>
public static partial class Queries
{
    /// <summary>
    /// Enumerates Points within given distance of a Ray3d whose Ts lie between tMin and tMax.
    /// Chunks are approximately sorted along the ray direction.
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
    /// Enumerates Points within given distance of a Ray3d whose Ts lie between tMin and tMax.
    /// Chunks are approximately sorted along the ray direction.
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

        double t0 = double.NegativeInfinity;
        double t1 = double.PositiveInfinity;

        var box = Box3d.FromCenterAndSize(
            node.BoundingBoxExactGlobal.Center, 
            node.BoundingBoxExactGlobal.Size + V3d.One * maxDistanceToRay
            );

        if (fastRay.Intersects(box, ref t0, ref t1))
        {
            if(t1 < tMin || t0 > tMax) { yield break; }

            if (node.IsLeaf || node.Cell.Exponent == minCellExponent)
            {
                var qs = node.Positions.Value;

                var ps = new List<V3d>();
                var ia = new List<int>();

                for (var i = 0; i < qs.Length; i++)
                {
                    var pWorld = (V3d)qs[i] + node.Center;
                    var d = ray.GetMinimalDistanceTo(pWorld);
                    var tp = ray.GetTOfProjectedPoint(pWorld);
                    if (d > maxDistanceToRay || tp < tMin || tp > tMax) continue;

                    ps.Add(pWorld);
                    ia.Add(i);
                }
 
                if (ia.Count > 0) yield return new Chunk(
                    ps, 
                    node.Colors?.Value.Subset(ia),
                    node.Normals?.Value.Subset(ia),
                    node.Intensities?.Value.Subset(ia),
                    node.Classifications?.Value.Subset(ia),
                    PartIndexUtils.Subset(node.PartIndices, ia),
                    partIndexRange: null,
                    bbox: null
                    );
            } 
            else 
            {
                var sorted = node.Subnodes.OrderBy(
                    c => c == null ? double.PositiveInfinity : Vec.Distance(new V3d(c.Value.BoundingBoxExactGlobal.Center), ray.Origin)
                    );

                foreach(var c in sorted)
                {
                    if (c == null) continue;
                    if (t0 > tMin) tMin = t0;
                    if (t1 < tMax) tMax = t1;
                    var ress = c.Value.QueryPointsNearRay(ray, maxDistanceToRay, tMin, tMax, minCellExponent);
                    foreach(var res in ress) yield return res;
                }
            }
        }
        else 
        {
            yield break;
        }
    }
}
