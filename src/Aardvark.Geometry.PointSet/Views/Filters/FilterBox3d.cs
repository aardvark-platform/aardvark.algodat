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
public class FilterInsideBox3d(Box3d filter) : ISpatialFilter
{
    /// <summary></summary>
    public const string Type = "FilterInsideBox3d";

    /// <summary></summary>
    public Box3d Box { get; } = filter;

    /// <summary></summary>
    public bool IsFullyInside(Box3d box) => Box.Contains(box);

    /// <summary></summary>
    public bool IsFullyOutside(Box3d box) => !Box.Intersects(box);

    /// <summary></summary>
    public bool IsFullyInside(IPointNode node) => IsFullyInside(node.DataBounds);

    /// <summary></summary>
    public bool IsFullyOutside(IPointNode node) => IsFullyOutside(node.DataBounds);
    /// <summary></summary>
    public HashSet<int> FilterPoints(IPointNode node, HashSet<int>? selected = null)
    {
        var p = node.Positions;
        var contained = new HashSet<int>();
        if (selected != null)
        {
            foreach (var i in selected)
            {
                if(Box.Contains(p[i]))
                {
                    contained.Add(i);
                }
            }
        }
        else
        {
            for (var i = 0; i < p.Length; i++)
            {
                if (Box.Contains(p[i])) contained.Add(i);
            }
        }
        return contained;
    }

    /// <summary></summary>
    public JsonNode Serialize() => JsonSerializer.SerializeToNode(new
    {
        Type,
        Box = Box.ToString()
    })!;

    /// <summary></summary>
    public static FilterInsideBox3d Deserialize(JsonNode json) => new(Box3d.Parse((string)json["Box"]!));

    public Box3d Clip(Box3d box)
    {
        return box.Intersection(Box);
    }
    public bool Contains(V3d pt) => Box.Contains(pt);

    public bool Equals(IFilter other)
        => other is FilterInsideBox3d x && Box == x.Box;
}

/// <summary>
/// </summary>
/// <remarks></remarks>
public class FilterOutsideBox3d(Box3d filter) : ISpatialFilter
{
    /// <summary></summary>
    public const string Type = "FilterOutsideBox3d";

    /// <summary></summary>
    public Box3d Box { get; } = filter;

    /// <summary></summary>
    public bool IsFullyInside(Box3d box) => !Box.Intersects(box);

    /// <summary></summary>
    public bool IsFullyOutside(Box3d box) => Box.Contains(box);

    /// <summary></summary>
    public bool IsFullyInside(IPointNode node) => IsFullyInside(node.DataBounds);

    /// <summary></summary>
    public bool IsFullyOutside(IPointNode node) => IsFullyOutside(node.DataBounds);

    /// <summary></summary>
    public HashSet<int> FilterPoints(IPointNode node, HashSet<int>? selected = null)
    {
        var p = node.Positions;
        var contained = new HashSet<int>();
        if (selected != null)
        {
            foreach (var i in selected)
            {
                if(!Box.Contains(p[i]))
                {
                    contained.Add(i);
                }
            }
        }
        else
        {
            for (var i = 0; i < p.Length; i++)
            {
                if (!Box.Contains(p[i])) contained.Add(i);
            }
        }
        return contained;
    }

    /// <summary></summary>
    public JsonNode Serialize() => JsonSerializer.SerializeToNode(new
    {
        Type, 
        Box = Box.ToString()
    })!;

    /// <summary></summary>
    public static FilterOutsideBox3d Deserialize(JsonNode json) => new(Box3d.Parse((string)json["Box"]!));
    public Box3d Clip(Box3d box)
    {
        return box;
    }

    public bool Contains(V3d pt) => !Box.Contains(pt);

    public bool Equals(IFilter other)
        => other is FilterOutsideBox3d x && Box == x.Box;
}
