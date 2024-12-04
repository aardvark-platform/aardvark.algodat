using Aardvark.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aardvark.Geometry.Points;

/// <summary>
/// </summary>
public class FilterInsideConvexHulls3d : ISpatialFilter
{
    /// <summary></summary>
    public const string Type = "FilterInsideConvexHulls3d";

    /// <summary></summary>
    public Hull3d[] Hulls { get; }

    /// <summary></summary>
    public FilterInsideConvexHulls3d(params Hull3d[] filter) { Hulls = filter; }

    /// <summary></summary>
    public FilterInsideConvexHulls3d(IEnumerable<Hull3d> filter) { Hulls = filter.ToArray(); }

    public FilterInsideConvexHulls3d(Polygon2d footprint, Range1d zRange, Trafo3d trafo)
    {
        var basePoly = footprint;
        if (basePoly.PointCount < 3) { throw new ArgumentException("Footprint must contain at least 3 points."); }
        var inter = basePoly.HasSelfIntersections(1E-8);
        if (inter == 0) { throw new ArgumentException("Footprint can not contain two identical points."); }
        else if (inter == -1) { throw new ArgumentException("Footprint can not have self-intersections."); }
        if(!basePoly.IsCcw()) { basePoly.Reverse(); }
        Hulls = basePoly.ComputeNonConcaveSubPolygons(1E-8).ToArray().Map(arr => {
            var poly = new Polygon2d(arr.Map(i => basePoly[i]));
            if (!poly.IsCcw()) poly.Reverse();
            var planes = 
                poly.GetEdgeLineArray().Map(l => {
                    var dir = (l.P1 - l.P0).Normalized;
                    var n = new V3d(dir.Y, -dir.X, 0);
                    return new Plane3d(n, new V3d(l.P0, 0)).Transformed(trafo);
                }).Append([ 
                    new Plane3d(V3d.OON,zRange.Min).Transformed(trafo),
                    new Plane3d(V3d.OOI,zRange.Max).Transformed(trafo)
                ]);
            return new Hull3d(planes);
        });
    }

    /// <summary></summary>
    public bool IsFullyInside(Box3d box)
    {
        return Hulls.Any(h => h.Contains(box));
        //var boxhull = new Hull3d(box);
        //for(int i = 0; i<Hulls.Length; i++)
        //{
        //    var h = Hulls[i];
        //    var clipped = new Hull3d(h.PlaneArray.Append(boxhull.PlaneArray));
        //    var corners = clipped.ComputeCorners();

        //    var interior =
        //        corners.All(c =>
        //        {
        //            for (int j = 0; j < Hulls.Length; j++)
        //            {
        //                if (j != i && Hulls[j].Contains(c)) return true;
        //            }
        //            return false;
        //        });
        //    if (!interior) return false;
        //}
        
        //return Contains(box.Min);

    }


    /// <summary></summary>
    public bool IsFullyOutside(Box3d box) => Hulls.All(h => !h.Intersects(box));

    /// <summary></summary>
    public bool IsFullyInside(IPointCloudNode node) => IsFullyInside(node.BoundingBoxExactGlobal);

    /// <summary></summary>
    public bool IsFullyOutside(IPointCloudNode node) => IsFullyOutside(node.BoundingBoxExactGlobal);

    /// <summary></summary>
    public HashSet<int> FilterPoints(IPointCloudNode node, HashSet<int>? selected = null)
    {
        if (selected != null)
        {
            var c = node.Center;
            var ps = node.Positions.Value;
            return new HashSet<int>(selected.Where(i => Contains(c + (V3d)ps[i])));
        }
        else
        {
            var c = node.Center;
            var ps = node.Positions.Value;

            var result = new HashSet<int>();
            for (var i = 0; i < ps.Length; i++)
            {
                if (Contains(c + (V3d)ps[i])) result.Add(i);
            }
            return result;
        }
    }

    /// <summary></summary>
    public JsonNode Serialize() => JsonSerializer.SerializeToNode(new
    {
        Type,
        Array = Hulls.Map(h => h.PlaneArray.Map(p => new { Point = p.Point.ToString(), Normal = p.Normal.ToString() }))
    })!;

    public static FilterInsideConvexHulls3d Deserialize(JsonNode json)
    {
        var arr = (JsonArray)json["Array"]!;
        var hulls = arr.Map(jts => new Hull3d(((JsonArray)jts!).Map(jt => new Plane3d(V3d.Parse((string)jt!["Normal"]!), V3d.Parse((string)jt["Point"]!)))));
        return new FilterInsideConvexHulls3d(hulls);
    }

    public Box3d Clip(Box3d box) => new(Hulls.Map(h => h.IntersectionBounds(box)));

    public bool Contains(V3d point) => Hulls.Any(h =>h.Contains(point));

    public bool Equals(IFilter other)
        => other is FilterInsideConvexHulls3d x &&
           x.Hulls.Length == Hulls.Length &&
           Hulls.ZipPairs(x.Hulls).All(tup => tup.Item1.PlaneArray.ZipPairs(tup.Item2.PlaneArray).All(p => p.Item1.ApproximateEquals(p.Item2, 1e-9)));
}
