using Aardvark.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public class FilterInsidePrismXY : ISpatialFilter
    {
        /// <summary></summary>
        public const string Type = "FilterInsidePrismXY";

        /// <summary></summary>
        public PolyRegion Shape { get; }
        
        /// <summary></summary>
        public Range1d ZRange { get; }

        /// <summary></summary>
        public FilterInsidePrismXY(PolyRegion shape, Range1d zRange) { Shape = shape; ZRange = zRange; }

        /// <summary></summary>
        public bool IsFullyInside(Box3d box)
            => box.Min.Z >= ZRange.Min && box.Max.Z <= ZRange.Max && Shape.Contains(new Box2d(box.Min.XY, box.Max.XY));

        /// <summary></summary>
        public bool IsFullyOutside(Box3d box)
            => box.Max.Z < ZRange.Min || box.Min.Z > ZRange.Max || !Shape.Overlaps(PolyRegionModule.ofBox(new Box2d(box.Min.XY, box.Max.XY)));

        /// <summary></summary>
        public bool IsFullyInside(IPointCloudNode node) => IsFullyInside(node.BoundingBoxExactGlobal);

        /// <summary></summary>
        public bool IsFullyOutside(IPointCloudNode node) => IsFullyOutside(node.BoundingBoxExactGlobal);

        /// <summary></summary>
        public HashSet<int> FilterPoints(IPointCloudNode node, HashSet<int> selected = null)
        {
            if (selected != null)
            {
                var c = node.Center;
                var ps = node.Positions.Value;
                return new HashSet<int>(selected.Where(i =>
                {
                    var p = c + (V3d)ps[i];
                    return p.Z >= ZRange.Min && p.Z <= ZRange.Max && Shape.Contains(p.XY);
                }));
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

        #region Serialization

        private record Dto(string Type, V2d[][] Shape, double[] ZRange)
        {
            public Dto() : this(FilterInsidePrismXY.Type, Array.Empty<V2d[]>(), Array.Empty<double>()) { }
            public Dto(FilterInsidePrismXY x) : this(
                FilterInsidePrismXY.Type,
                x.Shape.Polygons.Select(x => x.GetPointArray()).ToArray(),
                new[] { x.ZRange.Min, x.ZRange.Max }
                )
            { }
        }
        private Dto ToDto() => new(this); 
        private static FilterInsidePrismXY FromDto(Dto dto) => new(
            new PolyRegion(new Polygon2d(dto.Shape[0].Map(p => new V2d(p[0], p[1])))),
            new Range1d(dto.ZRange[0], dto.ZRange[1])
            );

        /// <summary></summary>
        public JsonNode Serialize() => JsonSerializer.SerializeToNode(ToDto());

        public static FilterInsidePrismXY Deserialize(JsonNode json)
            => FromDto(JsonSerializer.Deserialize<Dto>(json));

        #endregion

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

        public bool Equals(IFilter other)
            => other is FilterInsidePrismXY x && Shape.Polygons.ZipPairs(x.Shape.Polygons).All(p => p.Item1 == p.Item2) && x.ZRange == ZRange;
    }
}
