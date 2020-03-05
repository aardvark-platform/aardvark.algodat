/*
    Copyright (C) 2006-2020. Aardvark Platform Team. http://github.com/aardvark-platform.
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
    public class TriangleSet : SymMap, IIntersectableObjectSet, IAwakeable
    {
        public static readonly Symbol Identifier = "TriangleSet";

        public TriangleSet()
            : base(Identifier)
        {
            Position3dList = new List<V3d>();
        }

        public TriangleSet(IEnumerable<Triangle3d> triangleList)
            : base(Identifier)
        {
            var points =
                from t in triangleList
                from p in t.Points
                select p;
            Position3dList = points.ToList();
        }

        public List<V3d> Position3dList
        {
            get { return (List<V3d>)m_ht["Positon3dList"]; }
            set { m_ht["Positon3dList"] = value; }
        }

        /// <summary>
        /// The number of objects for the triangle set to act as an
        /// IIntersectableObjectSet.
        /// </summary>
        public int ObjectCount => Position3dList.Count / 3;

        public Box3d ObjectBoundingBox(int objectIndex = -1)
        {
            var plist = Position3dList;
            if (objectIndex < 0) return plist.GetBoundingBox3d();
            int pi = objectIndex * 3;
            return new Box3d(plist[pi], plist[pi + 1], plist[pi + 2]);
        }

        public bool ObjectsIntersectRay(
            int[] objectIndexArray, int firstIndex, int indexCount,
            FastRay3d fastRay,
            Func<IIntersectableObjectSet, int, bool> objectFilter,
            Func<IIntersectableObjectSet, int, int, RayHit3d, bool> hitFilter,
            double tmin, double tmax,
            ref ObjectRayHit hit
            )
        {
            var plist = Position3dList;
            bool result = false;

            if (objectFilter == null)
            {
                for (int i = firstIndex, e = firstIndex + indexCount; i < e; i++)
                {
                    var index = objectIndexArray[i];
                    int pi = index * 3;
                    var rawHit = hit.RayHit;
                    if (fastRay.Ray.HitsTriangle(
                                plist[pi], plist[pi + 1], plist[pi + 2], tmin, tmax,
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
                        int pi = index * 3;
                        var rawHit = hit.RayHit;
                        if (fastRay.Ray.HitsTriangle(
                                    plist[pi], plist[pi + 1], plist[pi + 2], tmin, tmax,
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
            List<V3d> pl = Position3dList;
            int pi = hit.SetObject.Index * 3;
            info.Points = new V3d[3] { pl[pi], pl[pi + 1], pl[pi + 2] };
            V3d e01 = info.Points[1]-info.Points[0];
            V3d e02 = info.Points[2]-info.Points[0];
            info.Edges = new V3d[2] { e01, e02 };
            info.Normal = V3d.Cross(e01, e02).Normalized;
        }

        public bool ClosestPoint(
                int[] objectIndexArray, int firstIndex, int indexCount,
                V3d query,
                Func<IIntersectableObjectSet, int, bool> ios_index_objectFilter,
                Func<IIntersectableObjectSet, int, int, ObjectClosestPoint, bool> ios_index_part_ocp_pointFilter,
                ref ObjectClosestPoint closest)
        {
            var plist = Position3dList;
            bool result = false;
            for (int i = firstIndex, e = firstIndex + indexCount; i < e; i++)
            {
                int index = objectIndexArray[i];
                int pi = index * 3;
                V3d p = query.GetClosestPointOnTriangle(
                                plist[pi], plist[pi + 1], plist[pi + 2]);
                double d2 = V3d.DistanceSquared(query, p);
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
            var pl = Position3dList;
            int pi = objectIndex * 3;
            return box.Intersects(new Triangle3d(pl[pi], pl[pi + 1], pl[pi + 2]));
        }

        public bool ObjectIsInsideBox(
                int objectIndex,
                Box3d box
            )
        {
            var pl = Position3dList;
            int pi = objectIndex * 3;
            return box.Contains(new Triangle3d(pl[pi], pl[pi + 1], pl[pi + 2]));
        }

        public void Awake(int codedVersion)
        {
            if (codedVersion < 4) CreateNewGuidSymbolIfEmpty();
        }
    }

}
