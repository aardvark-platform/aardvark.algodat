/*
    Copyright (C) 2006-2025. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Aardvark.Geometry.Points;

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
        if (!line.HasValue) return [];

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
        if (!line.HasValue) return [];

        return self.QueryPointsNearLineSegmentCustom(line.Value, maxDistanceToRay, minCellExponent, customAttributes);
    }

    /// <summary>
    /// Enumerates chunks containing points within the given distance of a finite line segment.
    /// </summary>
    public static IEnumerable<Chunk> QueryPointsNearLineSegment(
        this PointSet self, Line3d lineSegment, double maxDistanceToRay, int minCellExponent = int.MinValue
        )
        => QueryPointsNearLineSegment(self.Root.Value, lineSegment, maxDistanceToRay, minCellExponent);

    /// <summary>
    /// Enumerates chunks containing points within the given distance of a finite line segment.
    /// </summary>
    public static IEnumerable<GenericChunk> QueryPointsNearLineSegmentCustom(
        this PointSet self, Line3d lineSegment, double maxDistanceToRay, params Durable.Def[] customAttributes
        )
        => QueryPointsNearLineSegmentCustom(self, lineSegment, maxDistanceToRay, int.MinValue, customAttributes);
    
    /// <summary>
    /// Enumerates chunks containing points within the given distance of a finite line segment.
    /// </summary>
    public static IEnumerable<GenericChunk> QueryPointsNearLineSegmentCustom(
        this PointSet self, Line3d lineSegment, double maxDistanceToRay, int minCellExponent, params Durable.Def[] customAttributes
        )
        => QueryPointsNearLineSegmentCustom(self.Root.Value, lineSegment, maxDistanceToRay, minCellExponent, customAttributes);


    /// <summary>
    /// Enumerates chunks containing points within the given distance of a finite line segment.
    /// </summary>
    public static IEnumerable<Chunk> QueryPointsNearLineSegment(
        this IPointCloudNode node, Line3d lineSegment, double maxDistanceToRay, int minCellExponent = int.MinValue
        )
    {
        var isTerminal = node.IsLeaf || node.Cell.Exponent == minCellExponent;
        if (isTerminal && !node.HasPositions) yield break;

        var lineLocal = ToLocalLineSegment(node, lineSegment);
        if (!MayContainPointsNearLineSegment(node, lineLocal, maxDistanceToRay)) yield break;

        if (isTerminal)
        {
            var ia = GetLineSegmentCandidateIndices(node, lineLocal, maxDistanceToRay);
            if (ia.Count == 0) yield break;

            var ps = GetSelectedGlobalPositions(node, ia);
            var cs = node.Colors?.Value.Subset(ia);
            var ns = node.Normals?.Value.Subset(ia);
            var js = node.Intensities?.Value.Subset(ia);
            var ks = node.Classifications?.Value.Subset(ia);
            var qs = PartIndexUtils.Subset(node.PartIndices, ia);

            yield return new Chunk(ps, cs, ns, js, ks, qs, partIndexRange: null, bbox: null);
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
    /// Enumerates chunks containing points within the given distance of a finite line segment.
    /// </summary>
    public static IEnumerable<GenericChunk> QueryPointsNearLineSegmentCustom(
        this IPointCloudNode node, Line3d lineSegment, double maxDistanceToRay, params Durable.Def[] customAttributes
        )
        => QueryPointsNearLineSegmentCustom(node, lineSegment, maxDistanceToRay, int.MinValue, customAttributes);

    /// <summary>
    /// Enumerates chunks containing points within the given distance of a finite line segment.
    /// </summary>
    public static IEnumerable<GenericChunk> QueryPointsNearLineSegmentCustom(
        this IPointCloudNode node, Line3d lineSegment, double maxDistanceToRay, int minCellExponent, params Durable.Def[] customAttributes
        )
    {
        var isTerminal = node.IsLeaf || node.Cell.Exponent == minCellExponent;
        if (isTerminal && !node.HasPositions) yield break;

        var lineLocal = ToLocalLineSegment(node, lineSegment);
        if (!MayContainPointsNearLineSegment(node, lineLocal, maxDistanceToRay)) yield break;

        if (isTerminal)
        {
            var ia = GetLineSegmentCandidateIndices(node, lineLocal, maxDistanceToRay);
            if (ia.Count == 0) yield break;

            var ps = GetSelectedGlobalPositions(node, ia);
            var data =
                ImmutableDictionary<Durable.Def, object>.Empty
                .Add(GenericChunk.Defs.Positions3d, ps)
                ;

            var attributes = customAttributes.Where(node.Has).Select(def => (def, value: node.Properties[def]));
            foreach (var (def, value) in attributes)
            {
                data = data.Add(def, value.Subset(ia));
            }

            yield return new GenericChunk(data);
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

    private static Line3d ToLocalLineSegment(IPointCloudNode node, Line3d lineSegment)
        => new(lineSegment.P0 - node.Center, lineSegment.P1 - node.Center);

    private static bool MayContainPointsNearLineSegment(
        IPointCloudNode node, Line3d lineSegmentLocal, double maxDistanceToLineSegment
        )
    {
        var boundsLocal = node.HasBoundingBoxExactLocal
            ? (Box3d)node.BoundingBoxExactLocal
            : node.BoundingBoxApproximate - node.Center;
        var worstCaseDistance = boundsLocal.Size.Length * 0.5 + maxDistanceToLineSegment;
        return lineSegmentLocal.GetMinimalDistanceTo(boundsLocal.Center) <= worstCaseDistance;
    }

    private static IReadOnlyList<int> GetLineSegmentCandidateIndices(
        IPointCloudNode node, Line3d lineSegmentLocal, double maxDistanceToLineSegment
        )
    {
        if (node.HasKdTree)
        {
            var closest = node.KdTree.Value.GetClosestToLine(
                (V3f)lineSegmentLocal.P0,
                (V3f)lineSegmentLocal.P1,
                (float)maxDistanceToLineSegment,
                node.PointCountCell
                );
            return closest.MapToArray(x => (int)x.Index);
        }

        var positionsLocal = node.Positions.Value;
        var result = new List<int>();
        for (var i = 0; i < positionsLocal.Length; i++)
        {
            if (lineSegmentLocal.GetMinimalDistanceTo((V3d)positionsLocal[i]) <= maxDistanceToLineSegment)
            {
                result.Add(i);
            }
        }
        return result;
    }

    private static V3d[] GetSelectedGlobalPositions(IPointCloudNode node, IReadOnlyList<int> indices)
    {
        var centerGlobal = node.Center;
        var positionsLocal = node.Positions.Value;
        var result = new V3d[indices.Count];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = centerGlobal + (V3d)positionsLocal[indices[i]];
        }
        return result;
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
