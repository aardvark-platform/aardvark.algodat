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

        // TODO sm: JSON anpassen



        /// <summary></summary>
        public JsonNode Serialize() => JsonSerializer.SerializeToNode(new
        { 
            //Type,
            //Array = Hull.PlaneArray.Map(p => new { Point = p.Point.ToString(), Normal = p.Normal.ToString() })
        });

        public static FilterInsidePrismXY Deserialize(JsonNode json)
        {
            //var arr = (JsonArray)json["Array"];
            //var planes = arr.Map(jt => new Plane3d(V3d.Parse((string)jt["Normal"]), V3d.Parse((string)jt["Point"])));
            //var hull = new Hull3d(planes);
            //return new FilterInsidePrismXY(hull);
            return null;
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

        public bool Equals(IFilter other)
            => other is FilterInsidePrismXY x && Shape.Polygons.ZipPairs(x.Shape.Polygons).All(p => p.Item1 == p.Item2) && x.ZRange == ZRange;
    }
}
