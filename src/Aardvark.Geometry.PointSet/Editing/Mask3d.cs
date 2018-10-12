/*
    Copyright (C) 2006-2018. Aardvark Platform Team. http://github.com/aardvark-platform.
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.
    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/
using System;
using System.Linq;
using Aardvark.Base;
using Newtonsoft.Json.Linq;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    [Serializable]
    public class Mask3d
    {
        private V3d m_camPosition;
        private IMask2d m_mask;
        private Trafo3d m_model2mask;
        private Triangle2d[] m_triangulation;

        /// <summary>
        /// </summary>
        public Mask3d(V3d cameraPositionInModelSpace, IMask2d mask, Trafo3d model2mask)
        {
            m_camPosition = cameraPositionInModelSpace;
            m_mask = mask;
            m_model2mask = model2mask;
            m_triangulation = m_mask.ComputeTriangulation();
        }

        /// <summary>
        /// </summary>
        public Mask3d And(Mask3d other)
        {
            if (m_model2mask != other.m_model2mask) throw new InvalidOperationException();
            return new Mask3d(m_camPosition, m_mask.And(other.m_mask), m_model2mask);
        }

        /// <summary>
        /// </summary>
        public Mask3d Or(Mask3d other)
        {
            if (m_model2mask != other.m_model2mask) throw new InvalidOperationException();
            return new Mask3d(m_camPosition, m_mask.Or(other.m_mask), m_model2mask);
        }

        /// <summary>
        /// </summary>
        public Mask3d Xor(Mask3d other)
        {
            if (m_model2mask != other.m_model2mask) throw new InvalidOperationException();
            return new Mask3d(m_camPosition, m_mask.Xor(other.m_mask), m_model2mask);
        }

        /// <summary>
        /// </summary>
        public Mask3d Subtract(Mask3d other)
        {
            if (m_model2mask != other.m_model2mask) throw new InvalidOperationException();
            return new Mask3d(m_camPosition, m_mask.Subtract(other.m_mask), m_model2mask);
        }

        /// <summary>
        /// </summary>
        public bool Contains(V3d p)
        {
            var q_xyz = m_model2mask.Forward.TransformPosProj(p);
            if (q_xyz.Z < 0.0) return false;
            var q_xy = q_xyz.XY;
            for (var i = 0; i < m_triangulation.Length; i++)
            {
                if (m_triangulation[i].Contains(q_xy)) return true;
            }
            return false;
        }

        /// <summary>
        /// </summary>
        public bool Contains(Box3d box, Func<V2d[], IMask2d> create)
        {
            //var cs3 = box.ComputeCorners().Map(c => m_model2mask.Forward.TransformPosProj(c));
            //if (cs3.Any(c => c.Z > 1.0)) return false;
            //var cs = cs3.Map(c => c.XY);
            //var outline = new GpcPolygon(new[] { cs[0], cs[2], cs[3], cs[1] })
            //    .Unite(new GpcPolygon(new[] { cs[0], cs[4], cs[5], cs[1] }))
            //    .Unite(new GpcPolygon(new[] { cs[1], cs[5], cs[7], cs[3] }))
            //    .Unite(new GpcPolygon(new[] { cs[3], cs[7], cs[6], cs[2] }))
            //    .Unite(new GpcPolygon(new[] { cs[2], cs[6], cs[4], cs[0] }))
            //    .Unite(new GpcPolygon(new[] { cs[4], cs[6], cs[7], cs[5] }))
            //    ;
            //var r = outline.Subtract(m_mask);

            var outline = box.GetOutlineProjected(m_camPosition, m_model2mask.Forward);
            if (outline == null || outline.Length == 0) return false;
            var r = create(outline).Subtract(m_mask);

            return r.IsEmpty;
        }

        /// <summary>
        /// </summary>
        public bool Intersects(Box3d box, Func<V2d[], IMask2d> create)
        {
            try
            {
                var cs3 = box.ComputeCorners().Map(c => m_model2mask.Forward.TransformPosProj(c));
                if (cs3.All(c => c.Z < 0.0)) return false;
                if (cs3.Any(c => c.Z < 0.0)) return true;
                var cs = cs3.Map(c => c.XY);
                var outline = create(new[] { cs[0], cs[2], cs[3], cs[1] })
                    .Or(create(new[] { cs[0], cs[4], cs[5], cs[1] }))
                    .Or(create(new[] { cs[1], cs[5], cs[7], cs[3] }))
                    .Or(create(new[] { cs[3], cs[7], cs[6], cs[2] }))
                    .Or(create(new[] { cs[2], cs[6], cs[4], cs[0] }))
                    .Or(create(new[] { cs[4], cs[6], cs[7], cs[5] }))
                    ;
                var r = outline.And(m_mask);

                return !r.IsEmpty;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// </summary>
        public JObject ToJson()
        {
            return JObject.FromObject(new
            {
                mask = m_mask.ToJson(),
                model2mask = m_model2mask.ToString(),
                camPosition = m_camPosition.ToString()
            });
        }

        /// <summary>
        /// </summary>
        public static Mask3d Parse(JObject json, Func<JToken, IMask2d> deserialize)
        {
            var mask = deserialize(json["mask"]);
            var model2mask = Trafo3d.Parse((string)json["model2mask"]);
            var camPosition = V3d.Parse((string)json["camPosition"]);
            return new Mask3d(camPosition, mask, model2mask);
        }
    }
}
