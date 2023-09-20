using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// NOT IMPLEMENTED
    /// </summary>
    public class FilterOr : IFilter
    {
        /// <summary></summary>
        public const string Type = "FilterOr";

        /// <summary></summary>
        public IFilter Left { get; }

        /// <summary></summary>
        public IFilter Right { get; }

        /// <summary></summary>
        public FilterOr(IFilter left, IFilter right)
        {
            Left = left ?? throw new ArgumentNullException(nameof(left));
            Right = right ?? throw new ArgumentNullException(nameof(right));
        }

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
    public class FilterAnd : IFilter
    {
        /// <summary></summary>
        public const string Type = "FilterAnd";

        /// <summary></summary>
        public IFilter Left { get; }

        /// <summary></summary>
        public IFilter Right { get; }

        /// <summary></summary>
        public FilterAnd(IFilter left, IFilter right)
        {
            Left = left ?? throw new ArgumentNullException(nameof(left));
            Right = right ?? throw new ArgumentNullException(nameof(right));
        }

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
}
