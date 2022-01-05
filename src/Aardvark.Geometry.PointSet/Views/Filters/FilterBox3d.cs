using Aardvark.Base;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public class FilterInsideBox3d : ISpatialFilter
    {
        /// <summary></summary>
        public const string Type = "FilterInsideBox3d";

        /// <summary></summary>
        public Box3d Box { get; }

        /// <summary></summary>
        public FilterInsideBox3d(Box3d filter) { Box = filter; }

        /// <summary></summary>
        public bool IsFullyInside(Box3d box) => Box.Contains(box);

        /// <summary></summary>
        public bool IsFullyOutside(Box3d box) => !Box.Intersects(box);

        /// <summary></summary>
        public bool IsFullyInside(IPointCloudNode node) => IsFullyInside(node.BoundingBoxExactGlobal);

        /// <summary></summary>
        public bool IsFullyOutside(IPointCloudNode node) => IsFullyOutside(node.BoundingBoxExactGlobal);
        /// <summary></summary>
        public HashSet<int> FilterPoints(IPointCloudNode node, HashSet<int> selected = null)
        {
            if (selected != null)
            {
                var c = node.Center;
                var ps = node.Positions.Value;
                return new HashSet<int>(selected.Where(i => Box.Contains(c + (V3d)ps[i])));
            }
            else
            {
                var c = node.Center;
                var ps = node.Positions.Value;
                var result = new HashSet<int>();
                for (var i = 0; i < ps.Length; i++)
                {
                    if (Box.Contains(c + (V3d)ps[i])) result.Add(i);
                }
                return result;
            }
        }

        /// <summary></summary>
        public JsonNode Serialize() => JsonSerializer.SerializeToNode(new
        {
            Type,
            Box = Box.ToString()
        });

        /// <summary></summary>
        public static FilterInsideBox3d Deserialize(JsonObject json) => new(Box3d.Parse((string)json["Box"]));

        public Box3d Clip(Box3d box)
        {
            return box.Intersection(Box);
        }
        public bool Contains(V3d pt) => Box.Contains(pt);
    }

    /// <summary>
    /// </summary>
    public class FilterOutsideBox3d : ISpatialFilter
    {
        /// <summary></summary>
        public const string Type = "FilterOutsideBox3d";

        /// <summary></summary>
        public Box3d Box { get; }

        /// <summary></summary>
        public FilterOutsideBox3d(Box3d filter) { Box = filter; }

        /// <summary></summary>
        public bool IsFullyInside(Box3d box) => !Box.Intersects(box);

        /// <summary></summary>
        public bool IsFullyOutside(Box3d box) => Box.Contains(box);

        /// <summary></summary>
        public bool IsFullyInside(IPointCloudNode node) => IsFullyInside(node.BoundingBoxExactGlobal);

        /// <summary></summary>
        public bool IsFullyOutside(IPointCloudNode node) => IsFullyOutside(node.BoundingBoxExactGlobal);

        /// <summary></summary>
        public HashSet<int> FilterPoints(IPointCloudNode node, HashSet<int> selected = null)
        {
            var c = node.Center;
            var xs = node.Positions.Value;

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
        public JsonNode Serialize() => JsonSerializer.SerializeToNode(new
        {
            Type, 
            Box = Box.ToString()
        });

        /// <summary></summary>
        public static FilterInsideBox3d Deserialize(JsonObject json) => new(Box3d.Parse((string)json["Box"]));
        public Box3d Clip(Box3d box)
        {
            return box;
        }

        public bool Contains(V3d pt) => !Box.Contains(pt);
    }
}
