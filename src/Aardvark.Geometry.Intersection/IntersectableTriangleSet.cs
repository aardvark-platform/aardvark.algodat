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
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry
{
    /// <summary>
    /// Triangle set defined by vertices in V3f[] and int[] indices.
    /// </summary>
    public class IntersectableTriangleSet : IIntersectableObjectSet
    {
        private int[] m_indices;
        private V3f[] m_positions;
        private Box3d m_bounds;

        public int[] Indices { get { return m_indices; } }
        public V3f[] Positions { get { return m_positions; } }
        public Box3d Bounds { get { return m_bounds; } }

        public IntersectableTriangleSet(int[] indices, V3f[] positions)
        {
            m_indices = indices;
            m_positions = positions;
            ObjectCount = indices != null ? indices.Length / 3 : positions.Length / 3;

            m_bounds = indices != null ? new Box3d(indices.Select(i => (V3d)positions[i])) : new Box3d(positions.Select(v => (V3d)v));

        }

        public IntersectableTriangleSet(V3f[] positions)
            : this(null, positions)
        { }


        private void GetTriangle(int id, out V3d p0, out V3d p1, out V3d p2)
        {
            if (m_indices == null)
            {
                p0 = (V3d)m_positions[3 * id + 0];
                p1 = (V3d)m_positions[3 * id + 1];
                p2 = (V3d)m_positions[3 * id + 2];
            }
            else
            {
                p0 = (V3d)m_positions[m_indices[3 * id + 0]];
                p1 = (V3d)m_positions[m_indices[3 * id + 1]];
                p2 = (V3d)m_positions[m_indices[3 * id + 2]];
            }
        }

        public int ObjectCount { get; }

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
                    GetTriangle(id, out V3d p0, out V3d p1, out V3d p2);
                    var p = queryPoint.GetClosestPointOnTriangle(p0, p1, p2);
                    var d = V3d.DistanceSquared(p, queryPoint);

                    if (d < minDist2)
                    {
                        d = minDist2;
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
                V3d p0, p1, p2;
                GetTriangle(objectIndex, out p0, out p1, out p2);
                return new Box3d(p0, p1, p2);
            }
        }

        public void ObjectHitInfo(ObjectRayHit hit, ref ObjectHitInfo hitInfo)
        {
            // NO IDEA
        }

        public bool ObjectIntersectsBox(int objectIndex, Box3d box)
        {
            GetTriangle(objectIndex, out V3d p0, out V3d p1, out V3d p2);

            return box.IntersectsTriangle(p0, p1, p2);
        }

        public bool ObjectIsInsideBox(int objectIndex, Box3d box)
        {
            GetTriangle(objectIndex, out V3d p0, out V3d p1, out V3d p2);

            return box.Contains(p0) && box.Contains(p1) && box.Contains(p2);
        }

        public bool ObjectsIntersectRay(int[] objectIndexArray, int firstIndex, int indexCount, FastRay3d ray, Func<IIntersectableObjectSet, int, bool> ios_index_objectFilter, Func<IIntersectableObjectSet, int, int, RayHit3d, bool> ios_index_part_hit_hitFilter, double tmin, double tmax, ref ObjectRayHit hit)
        {
            var found = false;
            var index = -1;
            tmax = Fun.Min(tmax, hit.RayHit.T);

            for (int i = 0; i < indexCount; i++)
            {
                var id = objectIndexArray[firstIndex + i];
                if (ios_index_objectFilter == null || ios_index_objectFilter(this, id))
                {
                    GetTriangle(id, out V3d p0, out V3d p1, out V3d p2);

                    if (ray.Ray.IntersectsTriangle(p0, p1, p2, tmin, tmax, out double t))
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
