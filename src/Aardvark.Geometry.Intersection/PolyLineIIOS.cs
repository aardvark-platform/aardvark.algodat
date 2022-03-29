/*
    Copyright (C) 2006-2022. Aardvark Platform Team. http://github.com/aardvark-platform.
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
    public partial class PolyLine : IIntersectableObjectSet
    {
        #region IIntersectableObjectSet Members

        public int ObjectCount => m_vertexCount - 1;

        public Box3d ObjectBoundingBox(int objectIndex = -1)
        {
            var pa = m_positionArray;
            if (objectIndex < 0) return pa.GetBoundingBox(m_vertexCount);
            return Box3d.FromPoints(pa[objectIndex], pa[objectIndex + 1]);
        }

        public bool ObjectsIntersectRay(
                int[] objectIndexArray, int firstIndex, int indexCount, FastRay3d ray,
                Func<IIntersectableObjectSet, int, bool> objectFilter,
                Func<IIntersectableObjectSet, int, int, RayHit3d, bool> hitFilter,
                double tmin, double tmax, ref ObjectRayHit hit)
        {
            return false;
        }

        public bool ClosestPoint(
                int[] objectIndexArray, int firstIndex, int indexCount, V3d queryPoint,
                Func<IIntersectableObjectSet, int, bool> ios_index_objectFilter,
                Func<IIntersectableObjectSet, int, int, ObjectClosestPoint, bool> ios_index_part_ocp_pointFilter,
                ref ObjectClosestPoint closestPoint)
        {
            bool result = false;
            var pa = m_positionArray;
            for (int i = firstIndex, e = firstIndex + indexCount; i < e; i++)
            {
                int li = objectIndexArray[i];
                V3d p = queryPoint.GetClosestPointOnLine(pa[li], pa[li + 1]);
                double d2 = Vec.DistanceSquared(queryPoint, p);
                if (d2 < closestPoint.DistanceSquared)
                    result = closestPoint.Set(d2, p, this, li);
            }
            return result;
        }

        public void ObjectHitInfo(ObjectRayHit hit, ref ObjectHitInfo hitInfo)
        {
            throw new NotImplementedException();
        }

        public bool ObjectIntersectsBox(int objectIndex, Box3d box)
        {
            var pa = m_positionArray;
            return box.IntersectsLine(pa[objectIndex], pa[objectIndex + 1]);
        }

        public bool ObjectIsInsideBox(int objectIndex, Box3d box)
        {
            var pa = m_positionArray;
            return box.Contains(pa[objectIndex]) && box.Contains(pa[objectIndex + 1]);
        }

        #endregion
    }
}
