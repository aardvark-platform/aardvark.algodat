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
using Aardvark.Base.Coder;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry
{
    [RegisterTypeInfo]
    public class SphereSet : SymMap, IIntersectableObjectSet, IAwakeable
    {
        public static readonly Symbol Identifier = "SphereSet";

        public List<Sphere3d> Sphere3ds;

        public SphereSet(IEnumerable<Sphere3d> sphere3ds)
            : base(Identifier)
        {
            Sphere3ds = sphere3ds.ToList();
        } 

        public int ObjectCount
        {
            get { return Sphere3ds.Count; }
        }

        public Box3d ObjectBoundingBox(int objectIndex = -1)
        {
            if (objectIndex >= 0)
            {
                var sphere = Sphere3ds[objectIndex];
                return new Box3d(sphere.Center - sphere.Radius,
                                 sphere.Center + sphere.Radius);
            }
            return new Box3d(from sphere in Sphere3ds
                             select new Box3d(sphere.Center - sphere.Radius,
                                              sphere.Center + sphere.Radius));
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
                    var rawHit = hit.RayHit;
                    if (fastRay.Ray.Hits(Sphere3ds[index], tmin, tmax,
                            ref rawHit)
                        && (hitFilter == null || !hitFilter(this, index, 0, rawHit)))
                    {
                        hit.RayHit = rawHit;
                        result = hit.Set(this, index);
                    }
                }
            }
            else
            {
                for (int i = firstIndex, e = firstIndex + indexCount; i < e; i++)
                {
                    var index = objectIndexArray[i];
                    if (objectFilter(this, index))
                    {
                        var rawHit = hit.RayHit;
                        if (fastRay.Ray.Hits(Sphere3ds[index], tmin, tmax,
                                ref rawHit)
                            && (hitFilter == null || !hitFilter(this, index, 0, rawHit)))
                        {
                            hit.RayHit = rawHit;
                            result = hit.Set(this, index);
                        }
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
            info.Normal = (hit.RayHit.Point - Sphere3ds[hit.SetObject.Index].Center).Normalized;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="objectIndexArray"></param>
        /// <param name="firstIndex"></param>
        /// <param name="indexCount"></param>
        /// <param name="query"></param>
        /// <param name="ios_index_objectFilter">not implemented</param>
        /// <param name="ios_index_part_ocp_pointFilter">not implemented</param>
        /// <param name="closest"></param>
        /// <returns></returns>
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
                V3d p = query.GetClosestPointOn(Sphere3ds[index]);
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
            return box.Intersects(Sphere3ds[objectIndex]);
        }

        public bool ObjectIsInsideBox(
                int objectIndex,
                Box3d box
            )
        {
            return box.Contains(Sphere3ds[objectIndex]);
        }

        public void Awake(int codedVersion)
        {
            if (codedVersion < 4) CreateNewGuidSymbolIfEmpty();
        }
    }
}
