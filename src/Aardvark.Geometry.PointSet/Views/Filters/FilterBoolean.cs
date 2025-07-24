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

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aardvark.Geometry.Points;

/// <summary>
/// NOT IMPLEMENTED
/// </summary>
/// <remarks></remarks>
public class FilterOr(IFilter left, IFilter right) : IFilter
{
    /// <summary></summary>
    public const string Type = "FilterOr";

    /// <summary></summary>
    public IFilter Left { get; } = left ?? throw new ArgumentNullException(nameof(left));

    /// <summary></summary>
    public IFilter Right { get; } = right ?? throw new ArgumentNullException(nameof(right));

    /// <summary></summary>
    public bool IsFullyInside(IPointCloudNode node) => Left.IsFullyInside(node) || Right.IsFullyInside(node);

    /// <summary></summary>
    public bool IsFullyOutside(IPointCloudNode node) => Left.IsFullyOutside(node) || Right.IsFullyOutside(node);

    /// <summary></summary>
    public HashSet<int> FilterPoints(IPointCloudNode node, HashSet<int>? selected = null)
    {
        var a = Left.FilterPoints(node, selected);
        if (selected != null && a.Count == selected.Count) return a;
        var b = Right.FilterPoints(node, selected);
        if (selected != null && b.Count == selected.Count) return b;
        a.UnionWith(b);
        return a;
    }

    /// <summary></summary>
    public JsonNode Serialize() => JsonSerializer.SerializeToNode(new
    {
        Type, 
        Left = Left.Serialize(), 
        Right = Right.Serialize() 
    })!;

    /// <summary></summary>
    public static FilterOr Deserialize(JsonNode json) 
        => new(Filter.Deserialize(json["Left"]!), Filter.Deserialize(json["Right"]!));

    public bool Equals(IFilter other)
        => other is FilterOr x && Left.Equals(x.Left) && Right.Equals(x.Right);
}

/// <summary>
/// </summary>
/// <remarks></remarks>
public class FilterAnd(IFilter left, IFilter right) : IFilter
{
    /// <summary></summary>
    public const string Type = "FilterAnd";

    /// <summary></summary>
    public IFilter Left { get; } = left ?? throw new ArgumentNullException(nameof(left));

    /// <summary></summary>
    public IFilter Right { get; } = right ?? throw new ArgumentNullException(nameof(right));

    /// <summary></summary>
    public bool IsFullyInside(IPointCloudNode node) => Left.IsFullyInside(node) && Right.IsFullyInside(node);

    /// <summary></summary>
    public bool IsFullyOutside(IPointCloudNode node) => Left.IsFullyOutside(node) && Right.IsFullyOutside(node);

    /// <summary></summary>
    public HashSet<int> FilterPoints(IPointCloudNode node, HashSet<int>? selected = null)
    {
        var a = Left.FilterPoints(node, selected);
        var b = Right.FilterPoints(node, selected);
        if (selected != null && a.Count == selected.Count && b.Count == selected.Count) return selected;
        a.IntersectWith(b);
        return a;
    }

    /// <summary></summary>
    public JsonNode Serialize() => JsonSerializer.SerializeToNode(new
    {
        Type,
        Left = Left.Serialize(),
        Right = Right.Serialize()
    })!;

    /// <summary></summary>
    public static FilterAnd Deserialize(JsonNode json) 
        => new(Filter.Deserialize(json["Left"]!), Filter.Deserialize(json["Right"]!));

    public bool Equals(IFilter other) 
        => other is FilterAnd x && Left.Equals(x.Left) && Right.Equals(x.Right);
}
