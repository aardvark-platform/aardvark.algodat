/*
    Copyright (C) 2017. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System;
using System.Collections.Generic;
using System.Linq;
using Aardvark.Base;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public static partial class Queries
    {
        /// <summary>
        /// Returns points inside view frustum (defined by viewProjection and canonicalViewVolume).
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsInViewFrustum(
            this PointSet self, M44d viewProjection, Box3d canonicalViewVolume
            )
        {
            var t = viewProjection.Inverse;
            var cs = canonicalViewVolume.ComputeCorners().Map(t.TransformPosProj);
            var hull = new Hull3d(new[]
            {
                new Plane3d(cs[0], cs[2], cs[1]), // near
                new Plane3d(cs[5], cs[7], cs[4]), // far
                new Plane3d(cs[0], cs[1], cs[4]), // bottom
                new Plane3d(cs[1], cs[3], cs[5]), // left
                new Plane3d(cs[4], cs[6], cs[0]), // right
                new Plane3d(cs[3], cs[2], cs[7]), // top
            });

            return QueryPointsInsideConvexHull(self, hull);
        }
    }
}
