using Aardvark.Base;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public class FilterInsideConvexHull3d : ISpatialFilter
    {
        /// <summary></summary>
        public const string Type = "FilterInsideBox3d";

        /// <summary></summary>
        public Hull3d Hull { get; }

        /// <summary></summary>
        public FilterInsideConvexHull3d(Hull3d filter) { Hull = filter; }

        /// <summary></summary>
        public bool IsFullyInside(Box3d box) => Hull.Contains(box);

        /// <summary></summary>
        public bool IsFullyOutside(Box3d box) => !Hull.Intersects(box);

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
                return new HashSet<int>(selected.Where(i => Hull.Contains(c + (V3d)ps[i])));
            }
            else
            {
                var c = node.Center;
                var ps = node.Positions.Value;
                var result = new HashSet<int>();
                for (var i = 0; i < ps.Length; i++)
                {
                    if (Hull.Contains(c + (V3d)ps[i])) result.Add(i);
                }
                return result;
            }
        }

        /// <summary></summary>
        public JObject Serialize() => throw new NotImplementedException();


    }
}
