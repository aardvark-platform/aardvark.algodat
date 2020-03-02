/*
    Copyright (C) 2006-2019. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using Aardvark.Base;
using System;

namespace Aardvark.Geometry
{
    public partial class PolyMesh
        : IIntersectableObjectSet
    {
        #region IIntersectableObjectSet Members

        public int ObjectCount => m_faceCount;

        public Box3d ObjectBoundingBox(int objectIndex)
        {
            if (objectIndex < 0)
                return VertexIndexArray.GetBoundingBox(
                            VertexIndexCount, PositionArray);
            return BoundingBoxOfFace(objectIndex);
        }

        /// <summary>
        /// The intersection of a polygon with a ray is performed using the
        /// intersection with the constituting triangles of the polygon. This
        /// only works for convex polygons, and the part number in the hit is
        /// used to store the number of the triangle within the polygon.
        /// </summary>
        public bool ObjectsIntersectRay(int[] objectIndexArray, int firstIndex, int indexCount,
                    FastRay3d fastRay,
                    Func<IIntersectableObjectSet, int, bool> ios_index_objectFilter,
                    Func<IIntersectableObjectSet, int, int, RayHit3d, bool> hitFilter,
                    double tmin, double tmax, ref ObjectRayHit hit)
        {
            bool result = false;
            int[] fia = FirstIndexArray, via = VertexIndexArray;
            var pa = PositionArray;
            for (int i = firstIndex, e = firstIndex + indexCount; i < e; i++)
            {
                var oi = objectIndexArray[i];
                if (ios_index_objectFilter != null
                    && !ios_index_objectFilter(this, oi)) continue;
                int fvi = fia[oi], fve = fia[oi + 1];
                V3d p0 = pa[via[fvi++]], e0 = pa[via[fvi++]] - p0;
                int fvi0 = fvi;
                while (fvi < fve)
                {
                    var e1 = pa[via[fvi]] - p0;
                    var rawHit = hit.RayHit;
                    if (fastRay.Ray.HitsTrianglePointAndEdges(
                            p0, e0, e1, tmin, tmax, ref rawHit))
                    {
                        int part = fvi - fvi0;
                        if (hitFilter == null || !hitFilter(this, oi, part, rawHit))
                        {
                            hit.RayHit = rawHit;
                            result = hit.Set(this, oi, part);
                            break;
                        }
                    }
                    ++fvi; e0 = e1;
                }
            }
            return result;
        }

        public void ObjectHitInfo(ObjectRayHit hit, ref ObjectHitInfo hitInfo)
        {
            int oi = hit.SetObject.Index;
            int[] fia = FirstIndexArray, via = VertexIndexArray;
            var pa = PositionArray;
            int fvi = fia[oi], fvc = fia[oi + 1] - fvi;
            V3d[] va = new V3d[fvc], ea = new V3d[fvc];
            V3d p = pa[via[fvi++]], p0 = p;
            va[0] = p;
            for (int fs = 1; fs < fvc; fs++)
            {
                var p1 = pa[via[fvi++]];
                va[fs] = p1; ea[fs - 1] = p1 - p0;
                p1 = p0;
            }
            ea[fvc - 1] = p - p0;
            hitInfo.Points = va;
            hitInfo.Edges = ea;
            var faceNormals = FaceAttributeArray<V3d>(Property.Normals);
            if (faceNormals != null)
                hitInfo.Normal = faceNormals[oi];
            else if (fvc > 2)
                hitInfo.Normal = ea[0].Cross(ea[1]).Normalized;                
        }

        public bool ClosestPoint(
                int[] objectIndexArray, int firstIndex, int indexCount,
                V3d queryPoint,
                Func<IIntersectableObjectSet, int, bool> ios_index_objectFilter,
                Func<IIntersectableObjectSet, int, int, ObjectClosestPoint, bool> ios_index_part_ocp_pointFilter,
                ref ObjectClosestPoint closestPoint)
        {
            bool result = false;
            int[] fia = FirstIndexArray, via = VertexIndexArray;
            var pa = PositionArray;
            for (int i = firstIndex, e = firstIndex + indexCount; i < e; i++)
            {
                int oi = objectIndexArray[i];
                if (ios_index_objectFilter != null
                    && !ios_index_objectFilter(this, oi)) continue;
                int fvi = fia[oi], fve = fia[oi + 1];
                V3d p0 = pa[via[fvi++]], p1 = pa[via[fvi++]];
                while (fvi < fve)
                {
                    var p2 = pa[via[fvi++]];
                    V3d p = queryPoint.GetClosestPointOnTriangle(p0, p1, p2);
                    double d2 = Vec.DistanceSquared(queryPoint, p);
                    if (d2 < closestPoint.DistanceSquared)
                        result = closestPoint.Set(d2, p, this, oi);
                    p1 = p2;
                }
            }
            return result;
        }

        public bool ObjectIntersectsBox(int objectIndex, Box3d box)
        {
            int[] fia = FirstIndexArray, via = VertexIndexArray;
            int fvi = fia[objectIndex], fve = fia[objectIndex + 1];
            var pa = PositionArray;
            V3d p0 = pa[via[fvi++]], p1 = pa[via[fvi++]];
            while (fvi < fve)
            {
                var p2 = pa[via[fvi++]];
                if (box.IntersectsTriangle(p0, p1, p2)) return true;
                p1 = p2;
            }
            return false;
        }

        public bool ObjectIsInsideBox(int objectIndex, Box3d box)
        {
            int[] fia = FirstIndexArray, via = VertexIndexArray;
            var pa = PositionArray;
            int fvi = fia[objectIndex], fve = fia[objectIndex + 1];
            while (fvi < fve)
                if (!box.Contains(pa[via[fvi++]])) return false;
            return true;
        }

        #endregion
    }
}
