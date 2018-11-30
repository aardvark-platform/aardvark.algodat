using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Aardvark.Base;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public class FilterIntensity : IFilter
    {
        /// <summary></summary>
        public const string Type = "FilterIntensity";

        /// <summary></summary>
        public Range1i Range { get; }

        /// <summary></summary>
        public FilterIntensity(Range1i range) { Range = range; }

        private int[] GetValues(IPointCloudNode node) => node.HasIntensities() ? node.GetIntensities().Value : null;

        /// <summary></summary>
        public bool IsFullyInside(IPointCloudNode node)
        {
            var xs = GetValues(node);
            if (xs == null) return true;

            for (var i = 0; i < xs.Length; i++)
            {
                if (!Range.Contains(xs[i])) return false;
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
                if (Range.Contains(xs[i])) return false;
            }

            return true;
        }

        /// <summary></summary>
        public HashSet<int> FilterPoints(IPointCloudNode node, HashSet<int> selected = null)
        {
            var xs = GetValues(node);

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
        public JObject Serialize() => new JObject(new { Type, Range = Range.ToString() });

        /// <summary></summary>
        public static FilterIntensity Deserialize(JObject json) => new FilterIntensity(Range1i.Parse((string)json["Range"]));
    }
}
