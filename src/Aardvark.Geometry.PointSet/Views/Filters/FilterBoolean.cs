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
/// Union of two filters within the original selection domain. A node is
/// fully inside if either operand is fully inside, and fully outside only
/// if both operands are fully outside. Classification short-circuits.
/// </summary>
/// <remarks>
/// Both operands receive the same read-only selection. A full-domain result
/// may be returned directly, including a pass-through alias of the selection;
/// only independently owned partial results are combined.
/// </remarks>
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
    public bool IsFullyOutside(IPointCloudNode node) => Left.IsFullyOutside(node) && Right.IsFullyOutside(node);

    /// <summary></summary>
    public HashSet<int> FilterPoints(IPointCloudNode node, HashSet<int>? selected = null)
    {
        var count = selected?.Count ?? node.PointCountCell;
        if (count == 0) return selected ?? [];

        var a = Left.FilterPoints(node, selected);
        if (a.Count == 0) return Right.FilterPoints(node, selected);
        if (a.Count == count) return a;
        var b = Right.FilterPoints(node, selected);
        if (b.Count == count) return b;
        if (b.Count == 0) return a;

        // Both are proper subsets, so neither aliases selected. IFilter gives
        // ownership of non-pass-through results to its caller. Grow the larger
        // set to avoid scanning it and unnecessarily resizing the smaller one.
        if (a.Count < b.Count) (a, b) = (b, a);
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
/// Intersection of two filters within the supplied selection domain. A node
/// is fully inside only if both operands are fully inside, and fully outside
/// if either operand is fully outside. Classification short-circuits.
/// </summary>
/// <remarks>
/// The right operand normally filters the left result, stopping early when it is
/// empty. Null domains retain the all-points scan and intersect owned results.
/// The caller's selection remains read-only, including nested pass-through aliases.
/// </remarks>
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
    public bool IsFullyOutside(IPointCloudNode node) => Left.IsFullyOutside(node) || Right.IsFullyOutside(node);

    /// <summary></summary>
    public HashSet<int> FilterPoints(IPointCloudNode node, HashSet<int>? selected = null)
    {
        if (selected is { Count: 0 }) return selected;
        var a = Left.FilterPoints(node, selected);
        if (a.Count == 0) return a;

        if (selected != null)
        {
            if (a.Count < selected.Count) return Right.FilterPoints(node, a);
            var result = Right.FilterPoints(node, selected);
            return result.Count == selected.Count ? selected : result;
        }

        // Keep primitive filters' contiguous all-points scans for null domains,
        // avoiding selection-iterator allocations. A null input cannot alias
        // either result, so these sets can safely be intersected in place.
        var count = node.PointCountCell;
        if (a.Count == count) return Right.FilterPoints(node, null);
        var b = Right.FilterPoints(node, null);
        if (b.Count == count) return a;
        if (b.Count == 0) return b;
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
