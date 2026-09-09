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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aardvark.Geometry.Points;

/// <summary>
/// An XY polygon region extruded over a Z range. Persistence retains every
/// contour with even-odd semantics, including holes and disconnected islands.
/// </summary>
public class FilterInsidePrismXY : ISpatialFilter
{
    public const string Type = "FilterInsidePrismXY";

    public PolyRegion Shape { get; }
    
    public Range1d ZRange { get; }

    public FilterInsidePrismXY(PolyRegion shape, Range1d zRange) { Shape = shape; ZRange = zRange; }
    
    public FilterInsidePrismXY(Polygon2d shape, Range1d zRange) { Shape = new(shape); ZRange = zRange; }

    public bool IsFullyInside(Box3d box)
        => box.Min.Z >= ZRange.Min && box.Max.Z <= ZRange.Max && Shape.Contains(new Box2d(box.Min.XY, box.Max.XY));

    public bool IsFullyOutside(Box3d box)
        => box.Max.Z < ZRange.Min || box.Min.Z > ZRange.Max || !Shape.Overlaps(PolyRegionModule.ofBox(new Box2d(box.Min.XY, box.Max.XY)));

    public bool IsFullyInside(IPointCloudNode node) => IsFullyInside(node.BoundingBoxExactGlobal);

    public bool IsFullyOutside(IPointCloudNode node) => IsFullyOutside(node.BoundingBoxExactGlobal);

    public HashSet<int> FilterPoints(IPointCloudNode node, HashSet<int>? selected = null)
    {
        if (selected != null)
        {
            var c = node.Center;
            var ps = node.Positions.Value;
            return [.. selected.Where(i =>
            {
                var p = c + (V3d)ps[i];
                return p.Z >= ZRange.Min && p.Z <= ZRange.Max && Shape.Contains(p.XY);
            })];
        }
        else
        {
            var c = node.Center;
            var ps = node.Positions.Value;
            
            var result = new HashSet<int>();
            for (var i = 0; i < ps.Length; i++)
            {
                var p = c + (V3d)ps[i];
                if (p.Z >= ZRange.Min && p.Z <= ZRange.Max && Shape.Contains(p.XY)) result.Add(i);
            }
            return result;
        }
    }

    public Box3d Clip(Box3d box)
    {
        var bound2d = PolyRegion.Intersection(Shape, PolyRegionModule.ofBox(new Box2d(box.Min.XY, box.Max.XY))).BoundingBox;
        var bound3d = new Box3d(
            new V3d(bound2d.Min, Math.Max(ZRange.Min, box.Min.Z)),
            new V3d(bound2d.Max, Math.Min(ZRange.Max, box.Max.Z))
            );
        return bound3d;
    }

    public bool Contains(V3d pt) => pt.Z >= ZRange.Min && pt.Z <= ZRange.Max && Shape.Contains(pt.XY);

    /// <summary>
    /// Compares the complete ordered contour sequences and Z ranges exactly,
    /// not merely geometric equivalence or a shared contour prefix.
    /// </summary>
    public bool Equals(IFilter other)
    {
        if (other is not FilterInsidePrismXY x) return false;
        var left = Shape.Polygons;
        var right = x.Shape.Polygons;
        while (!left.IsEmpty)
        {
            // Use Polygon2d's exact operator, not its different Equals semantics.
            if (right.IsEmpty || left.Head != right.Head) return false;
            left = left.Tail;
            right = right.Tail;
        }
        return right.IsEmpty && x.ZRange == ZRange;
    }

    #region Serialization

    private record Dto(string Type, V2d[][] Shape, double[] Range)
    {
        public Dto() : this(FilterInsidePrismXY.Type, [], []) { }
        public Dto(FilterInsidePrismXY x) : this(
            FilterInsidePrismXY.Type,
            [.. x.Shape.Polygons.Select(x => x.GetPointArray())],
            [x.ZRange.Min, x.ZRange.Max]
            )
        { }
    }
    private Dto ToDto() => new(this);
    private static FilterInsidePrismXY FromDto(Dto dto) => new(
        FromContours(dto.Shape, 0, dto.Shape.Length),
        new Range1d(dto.Range[0], dto.Range[1])
        );

    private static PolyRegion FromContours(V2d[][] contours, int start, int count)
    {
        if (count == 0) return PolyRegion.Empty;
        if (count == 1) return new PolyRegion(new Polygon2d(contours[start]));

        // XOR preserves holes and nested islands regardless of contour winding.
        // Balance the reduction rather than repeatedly tessellating a growing prefix.
        var half = count / 2;
        return PolyRegion.Xor(
            FromContours(contours, start, half),
            FromContours(contours, start + half, count - half));
    }

    public JsonNode Serialize() => JsonSerializer.SerializeToNode(ToDto())!;

    /// <summary>
    /// Restores all serialized contours and the Z range without changing the
    /// JSON schema. An empty Shape restores PolyRegion.Empty.
    /// </summary>
    public static FilterInsidePrismXY Deserialize(JsonNode json)
        => FromDto(JsonSerializer.Deserialize<Dto>(json)!);

    #endregion
}
