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
using System.Collections.Immutable;
using System.Linq;
using Aardvark.Base;
using Aardvark.Data;
using Aardvark.Data.Points;
using Microsoft.FSharp.Core;

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
        /// Points within given distance of a ray.
        /// </summary>
        public static IEnumerable<GenericChunk> QueryPointsNearRayCustom(
            this PointSet self, Ray3d ray, double maxDistanceToRay, params Durable.Def[] customAttributes
            )
            => QueryPointsNearRayCustom(self, ray, maxDistanceToRay, int.MinValue, customAttributes);

        /// <summary>
        /// Points within given distance of a ray.
        /// </summary>
        public static IEnumerable<GenericChunk> QueryPointsNearRayCustom(
            this PointSet self, Ray3d ray, double maxDistanceToRay, int minCellExponent, params Durable.Def[] customAttributes
            )
        {
            ray.Direction = ray.Direction.Normalized;
            var data = self.Root.Value;
            var bbox = data.BoundingBoxExactGlobal;

            var line = Clip(bbox, ray);
            if (!line.HasValue) return Enumerable.Empty<GenericChunk>();

            return self.QueryPointsNearLineSegmentCustom(line.Value, maxDistanceToRay, minCellExponent, customAttributes);
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
        public static IEnumerable<GenericChunk> QueryPointsNearLineSegmentCustom(
            this PointSet self, Line3d lineSegment, double maxDistanceToRay, params Durable.Def[] customAttributes
            )
            => QueryPointsNearLineSegmentCustom(self, lineSegment, maxDistanceToRay, int.MinValue, customAttributes);
        
        /// <summary>
        /// Points within given distance of a line segment (at most 1000).
        /// </summary>
        public static IEnumerable<GenericChunk> QueryPointsNearLineSegmentCustom(
            this PointSet self, Line3d lineSegment, double maxDistanceToRay, int minCellExponent, params Durable.Def[] customAttributes
            )
            => QueryPointsNearLineSegmentCustom(self.Root.Value, lineSegment, maxDistanceToRay, minCellExponent, customAttributes);


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

            var worstCaseDist = node.BoundingBoxExactLocal.Size3f.Length * 0.5 + maxDistanceToRay;
            var d0 = rayLocal.GetMinimalDistanceTo((V3d)node.BoundingBoxExactLocal.Center);
            if (d0 > worstCaseDist) yield break;

            if (node.IsLeaf || node.Cell.Exponent == minCellExponent)
            {
                if (node.HasKdTree)
                {
                    var indexArray = node.KdTree.Value.GetClosestToLine(
                        (V3f)s0Local, (V3f)s1Local,
                        (float)maxDistanceToRay,
                        node.PointCountCell
                        );

                    if (indexArray.Count > 0)
                    {
                        var ia = indexArray.MapToArray(x => (int)x.Index);
                        var ps = new V3d[ia.Length];
                        var cs = node.HasColors ? new C4b[ia.Length] : null;
                        var ns = node.HasNormals ? new V3f[ia.Length] : null;
                        var js = node.HasIntensities ? new int[ia.Length] : null;
                        var ks = node.HasClassifications ? new byte[ia.Length] : null;
                        var qs = PartIndexUtils.Subset(node.PartIndices, ia);
                        //var ds = new double[ia.Count];

                        for (var i = 0; i < ia.Length; i++)
                        {
                            var index = ia[i];
                            ps[i] = centerGlobal + (V3d)node.Positions.Value[index];
                            if (node.HasColors) cs![i] = node.Colors.Value[index];
                            if (node.HasNormals) ns![i] = node.Normals.Value[index];
                            if (node.HasIntensities) js![i] = node.Intensities.Value[index];
                            if (node.HasClassifications) ks![i] = node.Classifications.Value[index];
                            //ds[i] = ia[i].Dist;
                        }
                        var chunk = new Chunk(ps, cs, ns, js, ks, qs, bbox: null);
                        yield return chunk;
                    }
                }
                else
                {
                    // do it without kd-tree ;-)
                    var psLocal = node.Positions.Value;

                    var ia = new List<int>();
                    for (var i = 0; i < psLocal.Length; i++)
                    {
                        var d = rayLocal.GetMinimalDistanceTo((V3d)psLocal[i]);
                        if (d > maxDistanceToRay) continue;
                        ia.Add(i);
                    }

                    if (ia.Count > 0)
                    {
                        yield return new Chunk(
                            node.PositionsAbsolute.Subset(ia),
                            node.Colors?.Value.Subset(ia),
                            node.Normals?.Value.Subset(ia),
                            node.Intensities?.Value.Subset(ia),
                            node.Classifications?.Value.Subset(ia),
                            parts: node.PartIndices?.Subset(ia),
                            bbox: null);
                        throw new NotImplementedException("PARTINDICES");
                    }
                }
            }
            else // inner node
            {
                for (var i = 0; i < 8; i++)
                {
                    var n = node.Subnodes![i];
                    if (n == null) continue;
                    var xs = QueryPointsNearLineSegment(n.Value, lineSegment, maxDistanceToRay, minCellExponent);
                    foreach (var x in xs) yield return x;
                }
            }
        }

        /// <summary>
        /// Points within given distance of a line segment (at most 1000).
        /// </summary>
        public static IEnumerable<GenericChunk> QueryPointsNearLineSegmentCustom(
            this IPointCloudNode node, Line3d lineSegment, double maxDistanceToRay, params Durable.Def[] customAttributes
            )
            => QueryPointsNearLineSegmentCustom(node, lineSegment, maxDistanceToRay, int.MinValue, customAttributes);

        /// <summary>
        /// Points within given distance of a line segment (at most 1000).
        /// </summary>
        public static IEnumerable<GenericChunk> QueryPointsNearLineSegmentCustom(
            this IPointCloudNode node, Line3d lineSegment, double maxDistanceToRay, int minCellExponent, params Durable.Def[] customAttributes
            )
        {
            if (!node.HasPositions) yield break;

            var centerGlobal = node.Center;
            var s0Local = lineSegment.P0 - centerGlobal;
            var s1Local = lineSegment.P1 - centerGlobal;
            var rayLocal = new Ray3d(s0Local, (s1Local - s0Local).Normalized);

            var worstCaseDist = node.BoundingBoxExactLocal.Size3f.Length * 0.5 + maxDistanceToRay;
            var d0 = rayLocal.GetMinimalDistanceTo((V3d)node.BoundingBoxExactLocal.Center);
            if (d0 > worstCaseDist) yield break;

            if (node.IsLeaf || node.Cell.Exponent == minCellExponent)
            {
                if (!node.HasKdTree) throw new Exception("No kd-tree. Error 575ebf66-6fdf-4656-85d6-b2a9e387fea9.");
                
                var closest = node.KdTree.Value.GetClosestToLine(
                    (V3f)s0Local, (V3f)s1Local,
                    (float)maxDistanceToRay,
                    node.PointCountCell
                    );

                if (closest.Count > 0)
                {
                    var ia = closest.Map(x => (int)x.Index);
                    var ps = node.PositionsAbsolute.Subset(ia);
                    var data =
                        ImmutableDictionary<Durable.Def, object>.Empty
                        .Add(GenericChunk.Defs.Positions3d, ps)
                        ;

                    var attributes = customAttributes.Where(node.Has).Select(def => (def, value: node.Properties[def]));
                    foreach (var (def, value) in attributes)
                    {
                        data = data.Add(def, value.Subset(ia));
                    }

                    var chunk = new GenericChunk(data);

                    yield return chunk;
                }
            }
            else // inner node
            {
                for (var i = 0; i < 8; i++)
                {
                    var n = node.Subnodes![i];
                    if (n == null) continue;
                    var xs = QueryPointsNearLineSegmentCustom(n.Value, lineSegment, maxDistanceToRay, minCellExponent, customAttributes);
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
