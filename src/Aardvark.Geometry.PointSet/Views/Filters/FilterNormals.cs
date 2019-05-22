using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Aardvark.Base;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public class FilterNormalDirection : IFilter
    {
        /// <summary></summary>
        public const string Type = "FilterNormalDirection";

        /// <summary></summary>
        public V3f Direction { get; }
        
        /// <summary></summary>
        public float EpsInDegrees { get; }

        /// <summary></summary>
        public FilterNormalDirection(V3f direction, float epsInDegrees)
        {
            Direction = direction;
            EpsInDegrees = epsInDegrees;
            m_eps = (float)Math.Acos(Conversion.RadiansFromDegrees(EpsInDegrees));
        }

        private readonly float m_eps;

        private V3f[] GetValues(IPointCloudNode node) => node.HasNormals() ? node.GetNormals3f().Value : null;

        /// <summary></summary>
        public bool IsFullyInside(IPointCloudNode node)
        {
            var xs = GetValues(node);
            if (xs == null) return true;
            
            for (var i = 0; i < xs.Length; i++)
            {
                if (V3f.Dot(Direction, xs[i]) > m_eps) return false;
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
                if (V3f.Dot(Direction, xs[i]) <= m_eps) return false;
            }

            return true;
        }

        /// <summary></summary>
        public HashSet<int> FilterPoints(IPointCloudNode node, HashSet<int> selected = null)
        {
            var xs = GetValues(node);

            if (selected != null)
            {
                return new HashSet<int>(selected.Where(i => V3f.Dot(Direction, xs[i]) <= m_eps));
            }
            else
            {
                var result = new HashSet<int>();
                for (var i = 0; i < xs.Length; i++)
                {
                    if (V3f.Dot(Direction, xs[i]) <= m_eps) result.Add(i);
                }
                return result;
            }
        }

        /// <summary></summary>
        public JObject Serialize() => new JObject(new { Type, Direction = Direction.ToString(), EpsInDegrees });

        /// <summary></summary>
        public static FilterNormalDirection Deserialize(JObject json) => new FilterNormalDirection(
            V3f.Parse((string)json["Direction"]),
            float.Parse((string)json["EpsInDegrees"], CultureInfo.InvariantCulture)
            );
    }
}
