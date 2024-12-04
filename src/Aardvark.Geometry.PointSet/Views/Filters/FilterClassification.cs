using Aardvark.Base;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aardvark.Geometry.Points;

/// <summary>
/// </summary>
/// <remarks></remarks>
public class FilterClassification(HashSet<byte> filter) : IFilter
{
    /// <summary></summary>
    public const string Type = "FilterClassification";

    /// <summary></summary>
    public HashSet<byte> Filter { get; } = filter;
    /// <summary></summary>
    public static FilterClassification AllExcept(params byte[] xs)
    {
        var hs = new HashSet<byte>(Enumerable.Range(0, 256).Select(x => (byte)x));
        foreach (var x in xs) hs.Remove(x);
        return new FilterClassification(hs);
    }

    /// <summary></summary>
    public static FilterClassification AllExcept(params Range1b[] xs)
    {
        var hs = new HashSet<byte>(Enumerable.Range(0, 256).Select(x => (byte)x));
        foreach (var x in xs) for (var i = x.Min; i <= x.Max; i++) hs.Remove(i);
        return new FilterClassification(hs);
    }

    /// <summary></summary>
    public FilterClassification(params byte[] filter) : this(new HashSet<byte>(filter)) { }

    private byte[]? GetValues(IPointCloudNode node) => node.HasClassifications ? node.Classifications.Value : null;

    /// <summary></summary>
    public bool IsFullyInside(IPointCloudNode node)
    {
        var xs = GetValues(node);
        if (xs == null) return true;

        for (var i = 0; i < xs.Length; i++)
        {
            if (!Filter.Contains(xs[i])) return false;
        }

        return true;
    }

    /// <summary></summary>
    public bool IsFullyOutside(IPointCloudNode node)
    {
        var xs = GetValues(node);
        if (xs == null) return false;

        for (var i = 0; i < xs.Length; i++)
        {
            if (Filter.Contains(xs[i])) return false;
        }

        return true;
    }

    /// <summary></summary>
    public HashSet<int> FilterPoints(IPointCloudNode node, HashSet<int>? selected = null)
    {
        var xs = GetValues(node);
        if (xs == null) return [];

        if (selected != null)
        {
            return new HashSet<int>(selected.Where(i => Filter.Contains(xs[i])));
        }
        else
        {
            var result = new HashSet<int>();
            for (var i = 0; i < xs.Length; i++)
            {
                if (Filter.Contains(xs[i])) result.Add(i);
            }
            return result;
        }
    }

    /// <summary></summary>
    public JsonNode Serialize() => JsonSerializer.SerializeToNode(new
    {
        Type, 
        Filter = Filter.Select(x => (int)x).ToArray()
    })!;

    /// <summary></summary>
    public static FilterClassification Deserialize(JsonNode json)
        => new(new HashSet<byte>(json["Filter"].Deserialize<int[]>().Select(x => (byte)x)));

    public bool Equals(IFilter other)
        => other is FilterClassification x && Filter.SetEquals(x.Filter);
}
