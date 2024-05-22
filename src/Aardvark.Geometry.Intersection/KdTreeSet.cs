/*
    Copyright (C) 2006-2023. Aardvark Platform Team. http://github.com/aardvark-platform.
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
    public class KdTreeSet : SymMap, IIntersectableObjectSet, IAwakeable
    {
        public static readonly Symbol Identifier = "KdTreeSet";

        public static class Property
        {
            public static readonly Symbol ConcreteKdTreeList = "ConcreteKdTreeList";
            public static readonly Symbol Level = "Level";
        }

        public KdTreeSet()
            : base(Identifier)
        {
            ConcreteKdTreeList = [];
        }

        public KdTreeSet(IEnumerable<ConcreteKdIntersectionTree> concreteKdTrees)
        {
            m_typeName = Identifier;
            ConcreteKdTreeList = concreteKdTrees.ToList();
        }

        public KdTreeSet(params ConcreteKdIntersectionTree[] concreteKdTrees)
        {
            m_typeName = Identifier;
            ConcreteKdTreeList = [.. concreteKdTrees];
        }

        public KdTreeSet(List<ConcreteKdIntersectionTree> concreteKdTreeList)
        {
            m_typeName = Identifier;
            ConcreteKdTreeList = concreteKdTreeList;
        }

        public List<ConcreteKdIntersectionTree> ConcreteKdTreeList
        {
            get { return Get<List<ConcreteKdIntersectionTree>>(Property.ConcreteKdTreeList); }
            set { m_ht[Property.ConcreteKdTreeList] = value; }
        }

        public int Level
        {
            get { return Get<int>(Property.Level); }
            set { m_ht[Property.Level] = value; }
        }

        public int KdTreeCount
        {
            get { return ConcreteKdTreeList.Count; }
        }

        /// <summary>
        /// The number of objects for the triangle set to act as an
        /// IIntersectableObjectSet.
        /// </summary>
        public int ObjectCount
        {
            get { return ConcreteKdTreeList.Count; }
        }

        public Box3d ObjectBoundingBox(int objectIndex = -1)
        {
            if (objectIndex >= 0)
            {
                var x = ConcreteKdTreeList[objectIndex];
                return x.KdIntersectionTree.BoundingBox3d.Transformed(x.Trafo);
            }
            return new Box3d(from kdtree in ConcreteKdTreeList
                             select kdtree.KdIntersectionTree.BoundingBox3d.Transformed(kdtree.Trafo));
        }

        public bool ObjectsIntersectRay(int[] objectIndexArray, int firstIndex, int indexCount,
                        FastRay3d fastRay,
                        Func<IIntersectableObjectSet, int, bool> objectFilter,
                        Func<IIntersectableObjectSet, int, int, RayHit3d, bool> hitFilter, 
                        double tmin, double tmax, ref ObjectRayHit hit)
        {
            int kdTreeIndex = -1;
            for (int i = firstIndex, e = firstIndex + indexCount; i < e; i++)
            {
                var index = objectIndexArray[i];
                if (objectFilter != null && !objectFilter(this, index)) continue;
                var x = ConcreteKdTreeList[index];

                var trafo = x.Trafo;
                var transformedRay = new FastRay3d(
                    trafo.Backward.TransformPos(fastRay.Ray.Origin),
                    trafo.Backward.TransformDir(fastRay.Ray.Direction));

                if (x.KdIntersectionTree.Intersect(transformedRay, objectFilter, hitFilter, tmin, tmax, ref hit))
                {
                    kdTreeIndex = index;
                    hit.RayHit.Point = trafo.Forward.TransformPos(hit.RayHit.Point);
                }
            }
            if (kdTreeIndex < 0) return false;

            hit.ObjectStack ??= [];
            hit.ObjectStack.Add(new SetObject(this, kdTreeIndex));
            return true;
        }

        public void ObjectHitInfo(
                ObjectRayHit hit,
                ref ObjectHitInfo hitInfo
                )
        {
            // this never needs to be called
            hit.SetObject.Set.ObjectHitInfo(hit, ref hitInfo);
        }

        public bool ClosestPoint(
                int[] objectIndexArray, int firstIndex, int indexCount,
                V3d query,
                Func<IIntersectableObjectSet, int, bool> ios_index_objectFilter,
                Func<IIntersectableObjectSet, int, int, ObjectClosestPoint, bool> ios_index_part_ocp_pointFilter,
                ref ObjectClosestPoint closest)
        {
            int kdTreeIndex = -1;
            for (int i = firstIndex, e = firstIndex + indexCount; i < e; i++)
            {
                int index = objectIndexArray[i];
                var x = ConcreteKdTreeList[index];
                var trafo = x.Trafo;

                if (x.KdIntersectionTree.ClosestPoint(trafo.Backward.TransformPos(query), ref closest))
                {
                    kdTreeIndex = index;
                    closest.Point = trafo.Forward.TransformPos(closest.Point);
                }
            }
            if (kdTreeIndex < 0) return false;

            closest.ObjectStack ??= [];
            closest.ObjectStack.Add(new SetObject(this, kdTreeIndex));
            return true;
        }

        public bool ObjectIntersectsBox(
                int objectIndex,
                Box3d box
                )
        {
            var x = ConcreteKdTreeList[objectIndex];
            return x.KdIntersectionTree.IntersectsBox(box.Transformed(x.Trafo.Backward));
        }

        public bool ObjectIsInsideBox(
                int objectIndex,
                Box3d box
                )
        {
            var x = ConcreteKdTreeList[objectIndex];
            return x.KdIntersectionTree.IsInsideBox(box.Transformed(x.Trafo.Backward));
        }

        public void Awake(int codedVersion)
        {
            if (codedVersion < 4) CreateNewGuidSymbolIfEmpty();
        }
    }

}
