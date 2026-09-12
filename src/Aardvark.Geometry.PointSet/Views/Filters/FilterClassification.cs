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
/// Selects points whose classification is in <see cref="Filter"/>.
/// Points without classifications are not selected. Internal-node LoD samples
/// do not establish membership of descendants.
/// </summary>
public class FilterClassification(HashSet<byte> filter) : IFilter
{
    /// <summary></summary>
    public const string Type = "FilterClassification";

    /// <summary>
    /// The live, mutable set of accepted classifications. A supplied set is retained by reference.
    /// </summary>
    public HashSet<byte> Filter { get; } = filter;

    /// <summary>Accepts every byte classification except the supplied values.</summary>
    public static FilterClassification AllExcept(params byte[] xs)
    {
        var hs = new HashSet<byte>(Enumerable.Range(0, 256).Select(x => (byte)x));
        foreach (var x in xs) hs.Remove(x);
        return new FilterClassification(hs);
    }

    /// <summary>
    /// Accepts every byte classification except the supplied inclusive ranges,
    /// including endpoints at 255. Overlapping ranges are allowed; inverted ranges exclude nothing.
    /// </summary>
    public static FilterClassification AllExcept(params Range1b[] xs)
    {
        var hs = new HashSet<byte>(Enumerable.Range(0, 256).Select(x => (byte)x));
        foreach (var x in xs) for (var i = (int)x.Min; i <= x.Max; i++) hs.Remove((byte)i);
        return new FilterClassification(hs);
    }

    /// <summary>Creates an independent set from the supplied accepted classifications.</summary>
    public FilterClassification(params byte[] filter) : this(new HashSet<byte>(filter)) { }

    private byte[]? GetValues(IPointCloudNode node) => node.HasClassifications ? node.Classifications.Value : null;

    /// <summary>
    /// Proves membership only for classified leaves, scanning until the first rejected value.
    /// Internal nodes return false (unknown) without loading classifications or descendants,
    /// even when every byte value is accepted: descendants may lack classifications.
    /// Leaves without classifications return false.
    /// </summary>
    public bool IsFullyInside(IPointCloudNode node)
    {
        if (!node.IsLeaf) return false;
        var xs = GetValues(node);
        if (xs == null) return false;

        for (var i = 0; i < xs.Length; i++)
        {
            if (!Filter.Contains(xs[i])) return false;
        }

        return true;
    }

    /// <summary>
    /// An empty accepted set rejects any subtree without loading data. Otherwise internal nodes
    /// return false (unknown) without loading classifications or descendants. Leaves without
    /// classifications return true; classified leaves scan until the first accepted value.
    /// </summary>
    public bool IsFullyOutside(IPointCloudNode node)
    {
        if (Filter.Count == 0) return true;
        if (!node.IsLeaf) return false;
        var xs = GetValues(node);
        if (xs == null) return true;

        for (var i = 0; i < xs.Length; i++)
        {
            if (Filter.Contains(xs[i])) return false;
        }

        return true;
    }

    /// <summary>
    /// Selects matching local classification indices. Null means all local points; a supplied
    /// selection is a read-only domain. Returns an independently owned set, empty when local
    /// classifications are absent. Local LoD results do not describe descendant membership.
    /// </summary>
    public HashSet<int> FilterPoints(IPointCloudNode node, HashSet<int>? selected = null)
    {
        var xs = GetValues(node);
        if (xs == null) return [];

        if (selected != null)
        {
            return [.. selected.Where(i => Filter.Contains(xs[i]))];
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
