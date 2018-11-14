using System;
using System.Collections.Generic;
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
        public bool IsFullyInside(IPointCloudNode node) => Box.Contains(node.BoundingBoxExact);

        /// <summary></summary>
        public bool IsFullyOutside(IPointCloudNode node) => !Box.Intersects(node.BoundingBoxExact);

        /// <summary></summary>
        public bool IsPositionInside(V3d p) => Box.Contains(p);

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
        public bool IsFullyInside(IPointCloudNode node) => !Box.Intersects(node.BoundingBoxExact);

        /// <summary></summary>
        public bool IsFullyOutside(IPointCloudNode node) => Box.Contains(node.BoundingBoxExact);

        /// <summary></summary>
        public bool IsPositionInside(V3d p) => !Box.Contains(p);

        /// <summary></summary>
        public JObject Serialize() => new JObject(new { Type, Box = Box.ToString() });

        /// <summary></summary>
        public static FilterInsideBox3d Deserialize(JObject json) => new FilterInsideBox3d(Box3d.Parse((string)json["Box"]));
    }
}
