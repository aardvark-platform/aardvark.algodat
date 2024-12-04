using Aardvark.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aardvark.Geometry.Points;

/// <summary>
/// </summary>
/// <remarks></remarks>
public class FilterInsideConvexHull3d(Hull3d filter) : ISpatialFilter
{
    /// <summary></summary>
    public const string Type = "FilterInsideConvexHull3d";

    /// <summary></summary>
    public Hull3d Hull { get; } = filter;

    /// <summary></summary>
    public bool IsFullyInside(Box3d box) => Hull.Contains(box);

    /// <summary></summary>
    public bool IsFullyOutside(Box3d box) => !Hull.Intersects(box);

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
            return new HashSet<int>(selected.Where(i => Hull.Contains(c + (V3d)ps[i])));
        }
        else
        {
            var c = node.Center;
            var ps = node.Positions.Value;
            
            var result = new HashSet<int>();
            for (var i = 0; i < ps.Length; i++)
            {
                if (Hull.Contains(c + (V3d)ps[i])) result.Add(i);
            }
            return result;
        }
    }

    /// <summary></summary>
    public JsonNode Serialize() => JsonSerializer.SerializeToNode(new
    { 
        Type,
        Array = Hull.PlaneArray.Map(p => new { Point = p.Point.ToString(), Normal = p.Normal.ToString() })
    })!;

    public static FilterInsideConvexHull3d Deserialize(JsonNode json)
    {
        var arr = (JsonArray)json["Array"]!;
        var planes = arr.Map(jt => new Plane3d(V3d.Parse((string)jt!["Normal"]!), V3d.Parse((string)jt!["Point"]!)));
        var hull = new Hull3d(planes);
        return new FilterInsideConvexHull3d(hull);
    }

    public Box3d Clip(Box3d box) => Hull.IntersectionBounds(box);
    public bool Contains(V3d pt) => Hull.Contains(pt);

    public bool Equals(IFilter other)
        => other is FilterInsideConvexHull3d x && Hull.PlaneCount == x.Hull.PlaneCount && Hull.PlaneArray.ZipPairs(x.Hull.PlaneArray).All(p => p.Item1 == p.Item2);
}
