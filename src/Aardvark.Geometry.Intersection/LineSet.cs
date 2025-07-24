/*
    Copyright (C) 2006-2025. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using Aardvark.Base.Coder;
using System;
using System.Collections.Generic;
using System.Linq;

#pragma warning disable IDE0130 // Namespace does not match folder structure

namespace Aardvark.Geometry
{
    [RegisterTypeInfo]
    public class LineSet(IEnumerable<Cylinder3d> cylinders) : IIntersectableObjectSet
    {
        public List<Cylinder3d> Lines = [.. cylinders];

        public int ObjectCount
        {
            get { return Lines.Count(); }
        }

        public Box3d ObjectBoundingBox(int objectIndex = -1)
        {
            return new Box3d(from line in Lines select line.BoundingBox3d);
        }

        public bool ObjectsIntersectRay(
                int[] objectIndexArray, int firstIndex, int indexCount,
                FastRay3d fastRay,
                Func<IIntersectableObjectSet, int, bool> objectFilter,
                Func<IIntersectableObjectSet, int, int, RayHit3d, bool> hitFilter,
                double tmin, double tmax,
                ref ObjectRayHit hit)
        {
            bool result = false;
            if (objectFilter == null)
            {
                for (int i = firstIndex, e = firstIndex + indexCount; i < e; i++)
                {
                    var index = objectIndexArray[i];
                    if (fastRay.Ray.Hits(Lines[index], tmin, tmax, ref hit.RayHit))
                    {
                       // Report.Line("hit shell of cylinder " + index + " at " + hit.RayHit.Point);
                        if (hitFilter == null)
                        {
                            result = hit.Set(this, index);
                            break;
                        }

                        if (!hitFilter(this, index, 0, hit.RayHit))
                        {
                            result = hit.Set(this, index);
                            break;
                        }
                        else
                        {
                            //continue shooting
                            //Report.Line("Filtered");
 
                            hit.RayHit.T = tmax;
                        }
                        
                    }
                }
            }
            else
            {
                foreach (int index in objectIndexArray)
                    if (objectFilter(this, index))
                    {
                        if (fastRay.Ray.Hits(Lines[index], tmin, tmax, ref hit.RayHit)
                            && (hitFilter == null || !hitFilter(this, index, 0, hit.RayHit)))
                        {
                            result = hit.Set(this, index);
                        }
                    }
            }
            return result;
        }

        public void ObjectHitInfo(
            ObjectRayHit hit,
            ref ObjectHitInfo info
            )
        {
            info.Normal = (hit.RayHit.Point - Lines[hit.SetObject.Index].Center).Normalized;
        }

        public bool ClosestPoint(
                int[] objectIndexArray, int firstIndex, int indexCount,
                V3d query,
                Func<IIntersectableObjectSet, int, bool> ios_index_objectFilter,
                Func<IIntersectableObjectSet, int, int, ObjectClosestPoint, bool> ios_index_part_ocp_pointFilter,
                ref ObjectClosestPoint closest)
        {
            bool result = false;
            for (int i = firstIndex, e = firstIndex + indexCount; i < e; i++)
            {
                int index = objectIndexArray[i];
                V3d p = query.GetClosestPointOn(Lines[index]);
                double d2 = Vec.DistanceSquared(query, p);
                if (d2 < closest.DistanceSquared)
                    result = closest.Set(d2, p, this, index);
            }
            return result;
        }

        public bool ObjectIntersectsBox(
            int objectIndex,
            Box3d box
            )
        {
            return box.Intersects(Lines[objectIndex]);
        }

        public bool ObjectIsInsideBox(
                int objectIndex,
                Box3d box
            )
        {
            return box.Contains(Lines[objectIndex]);
        }
    }
}

