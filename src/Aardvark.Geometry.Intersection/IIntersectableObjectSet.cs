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
    public interface IBoundingBoxObjectSet
    {
        /// <summary>
        /// Returns the number of objects in the object set. The objects
        /// are numbered from 0 to ObjectCount-1.
        /// </summary>
        int ObjectCount { get; }

        /// <summary>
        /// Calculates a bounding box for the object with the supplied index.
        /// If the supplied index smaller than 0, the bounding box of the
        /// complete intersectable object set should be returned.
        /// </summary>
        /// <param name="objectIndex"></param>
        /// <returns>The index of the object within the object set.</returns>
        Box3d ObjectBoundingBox(int objectIndex = -1);
    }


    /// <summary>
    /// An IIntersectableObjectSet is a set of objects for which a kD-Tree can
    /// be built.
    /// </summary>                          
    public interface IIntersectableObjectSet : IBoundingBoxObjectSet
    {
        /// <summary>
        /// Intersect an array of objects with the supplied ray, modifying
        /// the supplied hit if an intersection is found. The closest
        /// intersection is returned as a hit. Filter functions
        /// can be supplied that can return false in order to skip either
        /// objects within sets, or specific hits. To avoid filtering null
        /// can be supplied for each function.
        /// </summary>
        /// <param name="objectIndexArray">An array of indices of objects
        /// within the object set which are tested for intersection with
        /// the ray.</param>
        /// <param name="ray">The fast ray with which to intersect.</param>
        /// <param name="tmin">The minimal t parameter on the ray.</param>
        /// <param name="tmax">The maximal t parameter on the ray.</param>
        /// <param name="hit">The hit that contains the previous nearest
        /// <param name="firstIndex"/>
        /// <param name="indexCount"/>
        /// <param name="ios_index_objectFilter"/>
        /// <param name="ios_index_part_hit_hitFilter"/>
        /// hit and is changed if a closer hit is found.</param>
        /// <returns>true if a hit with one of the supplied objects is found</returns>
        bool ObjectsIntersectRay(
                int[] objectIndexArray, int firstIndex, int indexCount,
                FastRay3d ray,
                Func<IIntersectableObjectSet, int, bool> ios_index_objectFilter,
                Func<IIntersectableObjectSet, int, int, RayHit3d, bool> ios_index_part_hit_hitFilter,
                double tmin, double tmax,
                ref ObjectRayHit hit
                );

        /// <summary>
        /// If a hit was found, additional information about the hit can
        /// be requested, and this is returned as a hit info.
        /// </summary>
        /// <param name="hit">The hit.</param>
        /// <param name="hitInfo">The returned additional hit info.</param>
        void ObjectHitInfo(
                ObjectRayHit hit,
                ref ObjectHitInfo hitInfo
                );

        /// <summary>
        /// Computes the closest point of all the objects in the supplied
        /// index array with respect to the query point, as long as its
        /// closer than the point supplied in closestPoint. Returns true
        /// if a closer point was found and updates the closestPoint as
        /// necessary. Note that the supplied filters are not used as of yet.
        /// </summary>
        bool ClosestPoint(
                int[] objectIndexArray, int firstIndex, int indexCount,
                V3d queryPoint,
                Func<IIntersectableObjectSet, int, bool> ios_index_objectFilter,
                Func<IIntersectableObjectSet, int, int, ObjectClosestPoint, bool> ios_index_part_ocp_pointFilter,
                ref ObjectClosestPoint closestPoint
                );

        /// <summary>
        /// Tests the object with the supplied index for intersection with
        /// the supplied box. Returns true if the object intersects the box,
        /// false otherwise.
        /// </summary>
        /// <param name="objectIndex">The object index within the set.</param>
        /// <param name="box">The box with which to intersect.</param>
        bool ObjectIntersectsBox(
                int objectIndex,
                Box3d box
                );

        /// <summary>
        /// Returns true if the object with the supplied index is completely
        /// contained within the supplied box.
        /// </summary>
        /// <param name="objectIndex">The object index within the set.</param>
        /// <param name="box">The box with which to test.</param>
        bool ObjectIsInsideBox(
                int objectIndex,
                Box3d box
                );
    }

    public class EmptyIntersectableObjectSet : IIntersectableObjectSet
    {
        public int ObjectCount
        {
            get { return 0; }
        }

        public Box3d ObjectBoundingBox(int objectIndex = -1)
        {
            return Box3d.Invalid;
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
            return false;
        }

        public void ObjectHitInfo(ObjectRayHit hit, ref ObjectHitInfo hitInfo)
        {
            // nop
        }

        public bool ObjectIntersectsBox(int objectIndex, Box3d box)
        {
            return false;
        }

        public bool ObjectIsInsideBox(int objectIndex, Box3d box)
        {
            return false;
        }

    }
}
