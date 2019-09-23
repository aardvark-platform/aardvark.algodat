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
using Aardvark.Data.Points;
using System.Collections.Generic;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public static partial class Queries
    {
        /// <summary>
        /// Enumerates (chunked) all points in pointset.
        /// </summary>
        public static IEnumerable<Chunk> QueryAllPoints(this PointSet self)
            => QueryAllPoints(self.Root.Value);

        /// <summary>
        /// Enumerates (chunked) all points in tree.
        /// </summary>
        public static IEnumerable<Chunk> QueryAllPoints(this IPointCloudNode node)
            => node.QueryPoints(_ => true, _ => false, _ => true);
    }
}
