using Aardvark.Base;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aardvark.Geometry.Points
{
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
                return new HashSet<int>(selected.Where(i => Range.Contains(xs[i])));
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
}
