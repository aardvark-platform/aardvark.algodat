using Aardvark.Base;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public class FilterIntensity : IFilter
    {
        /// <summary></summary>
        public const string Type = "FilterIntensity";

        /// <summary></summary>
        public Range1d Range { get; }

        /// <summary></summary>
        public FilterIntensity(Range1d range) { Range = range; }

        private double[] GetValues(IPointCloudNode node) => node.HasIntensities ? node.Intensities.Value.Map(a => node.IntensityOffset + a) : null;

        /// <summary></summary>
        public bool IsFullyInside(IPointCloudNode node) => false;

        /// <summary></summary>
        public bool IsFullyOutside(IPointCloudNode node) => false;

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
        public JObject Serialize() => JObject.FromObject(new { Type, Range = Range.ToString() });

        /// <summary></summary>
        public static FilterIntensity Deserialize(JObject json) => new FilterIntensity(Range1d.Parse((string)json["Range"]));
    }
}
