using Aardvark.Base;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

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
            var e = (float)Math.Sin(Conversion.RadiansFromDegrees(Fun.Clamp(epsInDegrees, 0.0, 90.0)));
            Direction = direction.Normalized;
            EpsInDegrees = epsInDegrees;
            m_eps = e * e;
        }

        private readonly float m_eps;

        private V3f[] GetValues(IPointCloudNode node) => node.HasNormals ? node.Normals.Value : null;

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
                return new HashSet<int>(selected.Where(i => Vec.Cross(Direction, xs[i].Normalized).LengthSquared <= m_eps));
            }
            else
            {
                var result = new HashSet<int>();
                for (var i = 0; i < xs.Length; i++)
                {
                    if (Vec.Cross(Direction, xs[i].Normalized).LengthSquared <= m_eps) result.Add(i);
                }
                return result;
            }
        }

        /// <summary></summary>
        public JsonNode Serialize() => JsonSerializer.SerializeToNode(
            new { Type, Direction = Direction.ToString(), EpsInDegrees }
            );

        /// <summary></summary>
        public static FilterNormalDirection Deserialize(JsonObject json) => new(
            V3f.Parse((string)json["Direction"]),
            float.Parse((string)json["EpsInDegrees"], CultureInfo.InvariantCulture)
            );
    }
}
