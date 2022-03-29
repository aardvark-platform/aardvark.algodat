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
using System.Collections.Generic;

namespace Aardvark.Geometry
{
    public class IntersectableBoxSet : IIntersectableObjectSet
    {
        private readonly Box3d[] m_boxes;
        private Box3d m_bounds;

        public IntersectableBoxSet(params Box3d[] boxes)
        {
            m_boxes = boxes;
            m_bounds = new Box3d(boxes);
        }

        public int ObjectCount => m_boxes.Length;

        public bool ClosestPoint(int[] objectIndexArray, int firstIndex, int indexCount, V3d queryPoint, Func<IIntersectableObjectSet, int, bool> ios_index_objectFilter, Func<IIntersectableObjectSet, int, int, ObjectClosestPoint, bool> ios_index_part_ocp_pointFilter, ref ObjectClosestPoint closestPoint)
        {
            var minDist2 = closestPoint.DistanceSquared;
            var minIndex = -1;
            var minPos = V3d.NaN;

            for (int i = 0; i < indexCount; i++)
            {
                var id = objectIndexArray[firstIndex + i];

                if (ios_index_objectFilter(this, id))
                {
                    var p = m_boxes[id].GetClosestPointOn(queryPoint);
                    var d = Vec.DistanceSquared(p, queryPoint);

                    if (d < minDist2)
                    {
                        minIndex = id;
                        minPos = p;
                    }
                }
            }

            if (minIndex >= 0)
            {
                closestPoint = new ObjectClosestPoint()
                {
                    Distance = Fun.Sqrt(minDist2),
                    DistanceSquared = minDist2,
                    Point = minPos,
                    SetObject = new SetObject(this, minIndex),
                    ObjectStack = new List<SetObject>(), // TODO
                    Coord = V2d.Zero // TODO

                };

                return true;
            }
            else
            {
                return false;
            }
        }

        public Box3d ObjectBoundingBox(int objectIndex = -1)
        {
            if (objectIndex < 0)
            {
                return m_bounds;
            }
            else
            {
                return m_boxes[objectIndex];
            }
        }

        public void ObjectHitInfo(ObjectRayHit hit, ref ObjectHitInfo hitInfo)
        {
            // NO IDEA
        }

        public bool ObjectIntersectsBox(int objectIndex, Box3d box)
        {
            return box.Intersects(m_boxes[objectIndex]);
        }

        public bool ObjectIsInsideBox(int objectIndex, Box3d box)
        {
            return box.Contains(m_boxes[objectIndex]);
        }

        public bool ObjectsIntersectRay(int[] objectIndexArray, int firstIndex, int indexCount, FastRay3d ray, Func<IIntersectableObjectSet, int, bool> ios_index_objectFilter, Func<IIntersectableObjectSet, int, int, RayHit3d, bool> ios_index_part_hit_hitFilter, double tmin, double tmax, ref ObjectRayHit hit)
        {
            var found = false;
            var index = -1;
            tmax = Fun.Min(tmax, hit.RayHit.T);

            for (int i = 0; i < indexCount; i++)
            {
                var id = objectIndexArray[firstIndex + i];
                if (ios_index_objectFilter(this, id))
                {
                    var t = tmin;
                    if (ray.Intersects(m_boxes[id], ref t, ref tmax))
                    {
                        tmax = t;
                        found = true;
                        index = id;
                    }
                }
            }

            if (found)
            {
                hit = new ObjectRayHit()
                {
                    SetObject = new SetObject(this, index),
                    ObjectStack = new List<SetObject>(), // TODO
                    RayHit = new RayHit3d()
                    {
                        Part = 0,
                        Point = ray.Ray.GetPointOnRay(tmax),
                        T = tmax,

                        Coord = V2d.Zero, // TODO
                        BackSide = false // TODO
                    }
                };

                return true;
            }
            else return false;
        }
    }
}
