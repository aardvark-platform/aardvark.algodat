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
    public class FilterInsideBox3d : IFilter
    {
        /// <summary></summary>
        public const string Type = "FilterInsideBox3d";

        /// <summary></summary>
        public Box3d Box { get; }

        /// <summary></summary>
        public FilterInsideBox3d(Box3d filter) { Box = filter; }

        /// <summary></summary>
        public bool IsFullyInside(IPointCloudNode node) => Box.Contains(node.BoundingBoxExactGlobal);

        /// <summary></summary>
        public bool IsFullyOutside(IPointCloudNode node) => !Box.Intersects(node.BoundingBoxExactGlobal);

        /// <summary></summary>
        public HashSet<int> FilterPoints(IPointCloudNode node, HashSet<int> selected = null)
        {
            if (selected != null)
            {
                var c = node.Center;
                var ps = node.GetPositions().Value;
                return new HashSet<int>(selected.Where(i => Box.Contains(c + (V3d)ps[i])));
            }
            else
            {
                var c = node.Center;
                var ps = node.GetPositions().Value;
                var result = new HashSet<int>();
                for (var i = 0; i < ps.Length; i++)
                {
                    if (Box.Contains(c + (V3d)ps[i])) result.Add(i);
                }
                return result;
            }
        }

        /// <summary></summary>
        public JObject Serialize() => new JObject(new { Type, Box = Box.ToString() });

        /// <summary></summary>
        public static FilterInsideBox3d Deserialize(JObject json) => new FilterInsideBox3d(Box3d.Parse((string)json["Box"]));

    }

    /// <summary>
    /// </summary>
    public class FilterOutsideBox3d : IFilter
    {
        /// <summary></summary>
        public const string Type = "FilterOutsideBox3d";

        /// <summary></summary>
        public Box3d Box { get; }

        /// <summary></summary>
        public FilterOutsideBox3d(Box3d filter) { Box = filter; }

        /// <summary></summary>
        public bool IsFullyInside(IPointCloudNode node) => !Box.Intersects(node.BoundingBoxExactGlobal);

        /// <summary></summary>
        public bool IsFullyOutside(IPointCloudNode node) => Box.Contains(node.BoundingBoxExactGlobal);

        /// <summary></summary>
        public HashSet<int> FilterPoints(IPointCloudNode node, HashSet<int> selected = null)
        {
            var c = node.Center;
            var xs = node.GetPositions().Value;

            if (selected != null)
            {
                return new HashSet<int>(selected.Where(i => Box.Contains(c + (V3d)xs[i])));
            }
            else
            {
                var result = new HashSet<int>();
                for (var i = 0; i < xs.Length; i++)
                {
                    if (Box.Contains(c + (V3d)xs[i])) result.Add(i);
                }
                return result;
            }
        }

        /// <summary></summary>
        public JObject Serialize() => new JObject(new { Type, Box = Box.ToString() });

        /// <summary></summary>
        public static FilterInsideBox3d Deserialize(JObject json) => new FilterInsideBox3d(Box3d.Parse((string)json["Box"]));
    }
}
