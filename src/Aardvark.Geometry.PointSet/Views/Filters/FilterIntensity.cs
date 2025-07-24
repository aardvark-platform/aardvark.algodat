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
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aardvark.Geometry.Points;

/// <summary>
/// </summary>
/// <remarks></remarks>
public class FilterIntensity(Range1i range) : IFilter
{
    /// <summary></summary>
    public const string Type = "FilterIntensity";

    /// <summary></summary>
    public Range1i Range { get; } = range;

    private int[]? GetValues(IPointCloudNode node) => node.HasIntensities ? node.Intensities.Value : null;

    /// <summary></summary>
    public bool IsFullyInside(IPointCloudNode node) => false;

    /// <summary></summary>
    public bool IsFullyOutside(IPointCloudNode node) => false;

    /// <summary></summary>
    public HashSet<int> FilterPoints(IPointCloudNode node, HashSet<int>? selected = null)
    {
        var xs = GetValues(node);
        if (xs == null) return [];

        if (selected != null)
        {
            return [.. selected.Where(i => Range.Contains(xs[i]))];
        }
        else
        {
            var result = new HashSet<int>();
            for (var i = 0; i < xs.Length; i++)
            {
                if (Range.Contains(xs[i])) result.Add(i);
            }
            return result;
        }
    }

    /// <summary></summary>
    public JsonNode Serialize() => JsonSerializer.SerializeToNode(new { Type, Range = Range.ToString() })!;

    /// <summary></summary>
    public static FilterIntensity Deserialize(JsonNode json) => new(Range1i.Parse((string)json["Range"]!));

    public bool Equals(IFilter other)
        => other is FilterIntensity x && Range == x.Range;
}
