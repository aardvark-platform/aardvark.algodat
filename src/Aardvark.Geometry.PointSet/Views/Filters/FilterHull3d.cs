﻿using Aardvark.Base;
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
        public const string Type = "FilterInsideConvexHull3d";
        public const string Plane = "Plane3d";

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
        public JObject Serialize()
        {
            return JObject.FromObject(new
                { Type,
                  Array = JArray.FromObject(
                                this.Hull.PlaneArray.Map(p =>   
                                        JObject.FromObject(new { Point = p.Point.ToString(), Normal = p.Normal.ToString() }
                                    )
                                )
                          )
                });
        }
        public static FilterInsideConvexHull3d Deserialize(JObject json)
        {
            var arr = (JArray)json["Array"];
            var planes = arr.Map(jt => new Plane3d(V3d.Parse((string)jt["Normal"]), V3d.Parse((string)jt["Point"])));
            var hull = new Hull3d(planes);
            return new FilterInsideConvexHull3d(hull);
        }

        public Box3d Clip(Box3d box) => Hull.IntersectionBounds(box);
        public bool Contains(V3d pt) => Hull.Contains(pt);
    }
}
