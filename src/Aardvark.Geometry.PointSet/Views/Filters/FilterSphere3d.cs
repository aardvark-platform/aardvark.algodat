using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Aardvark.Base;
using Newtonsoft.Json.Linq;

namespace Aardvark.Geometry.Points
{
    public class FilterInsideSphere3d : IFilter
    {
        private readonly Sphere3d m_sphere;
        private readonly double m_radiusSquared;

        public const string Type = "FilterInsideSphere3d";
        private bool Contains(V3d pt)
        {
            return V3d.DistanceSquared(m_sphere.Center, pt) <= m_radiusSquared;
        }

        public FilterInsideSphere3d(Sphere3d sphere)
        {
            m_sphere = sphere;
            m_radiusSquared = sphere.RadiusSquared;
        }

        public HashSet<int> FilterPoints(IPointCloudNode node, HashSet<int> selected = null)
        {
            if (selected != null)
            {
                var c = node.Center;
                var ps = node.Positions.Value;
                return new HashSet<int>(selected.Where(i => Contains(c + (V3d)ps[i])));
            }
            else
            {
                var c = node.Center;
                var ps = node.Positions.Value;
                var result = new HashSet<int>();
                for (var i = 0; i < ps.Length; i++)
                {
                    if (Contains(c + (V3d)ps[i])) result.Add(i);
                }
                return result;
            }
        }

        public bool IsFullyInside(IPointCloudNode node)
        {
            return node.BoundingBoxExactGlobal.ComputeCorners().TrueForAll((p) => Contains(p));
        }

        public bool IsFullyOutside(IPointCloudNode node)
        {
            return !node.BoundingBoxExactGlobal.Intersects(m_sphere);
        }

        public JObject Serialize()
        {
            return JObject.FromObject(new { Type, Sphere = m_sphere.ToString() });
        }
    }
}
