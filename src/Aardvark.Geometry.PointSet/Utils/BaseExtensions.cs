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
using System;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public static class BaseExtensions
    {
        public static Box2l[] SplitAtCenter(this Box2l self)
        {
            var c = self.Center;
            return new Box2l[]
            {
                new Box2l(self.Min.X, self.Min.Y, c.X,        c.Y       ),
                new Box2l(c.X,        self.Min.Y, self.Max.X, c.Y       ),
                new Box2l(self.Min.X, c.Y,        c.X,        self.Max.Y),
                new Box2l(c.X,        c.Y,        self.Max.X, self.Max.Y)
            };
        }
      

        /// <summary>
        /// Projects outline of a box from given position to a plane.
        /// Returns null if position is inside the box.
        /// </summary>
        /// <param name="box">Box to project.</param>
        /// <param name="fromPosition">Project from.</param>
        /// <param name="box2plane">Transformation from world/box-space to plane z=0</param>
        public static V2d[] GetOutlineProjected(this Box3d box, V3d fromPosition, M44d box2plane)
        {
            var ps = box.GetOutlineCornersCW(fromPosition);
            if (ps == null) return null;
            var qs = ps.Map(p => box2plane.TransformPosProj(p));
            
            var behindPositionCount = 0;
            for (var i = 0; i < qs.Length; i++)
            {
                if (qs[i].Z < 0.0) behindPositionCount++;
            }
            if (behindPositionCount == qs.Length) return Array.Empty<V2d>();
            if (behindPositionCount > 0) return null;

            return qs.Map(p => p.XY);
        }

        /// <summary>
        /// Returns true if the plane with a supplied epsilon tolerance fully contains the box.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Contains(
            this Plane3d plane, Box3d box, double eps)
        {
            var signs = box.GetIntersectionSignsWithPlane(plane, eps);
            return signs == Signs.Zero;
        }

        /// <summary>
        /// Returns true if the plane with a supplied epsilon tolerance intersects or fully contains the box.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IntersectsOrContains(
            this Plane3d plane, Box3d box, double eps)
        {
            var signs = box.GetIntersectionSignsWithPlane(plane, eps);
            return (signs != Signs.Negative && signs != Signs.Positive) || signs == Signs.Zero;
        }

        /// <summary>
        /// Bounding box of polygon with additional epsilon tolerance with respect to polygon normal.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Box3d BoundingBox3d(
            this Polygon3d self, double eps)
        {
            var bb = Box3d.Invalid;
            var v = self.ComputeNormal() * eps;
            foreach (var p in self.Points)
            {
                bb.ExtendBy(p + v);
                bb.ExtendBy(p - v);
            }
            return bb;
        }

        /// <summary>
        /// Returns true if the Hull3d completely contains the box.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Contains(
            this Hull3d self, Box3d box)
        {
            var planes = self.PlaneArray;
            var imax = self.PlaneCount;
            var corners = box.Corners.ToArray();
            for (var i = 0; i < imax; i++)
            {
                var plane = planes[i];
                for (var j = 0; j < 8; j++) if (plane.Height(corners[j]) > 0) return false;
            }
            return true;
        }
    }
}
