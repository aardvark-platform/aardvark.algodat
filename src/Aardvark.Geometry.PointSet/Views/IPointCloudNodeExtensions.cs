/*
    Copyright (C) 2006-2018. Aardvark Platform Team. http://github.com/aardvark-platform.
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

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public static class IPointCloudNodeExtensions
    {
        /// <summary></summary>
        public static bool IsLeaf(this IPointCloudNode self) => self.Subnodes == null;

        /// <summary></summary>
        public static bool IsNotLeaf(this IPointCloudNode self) => self.Subnodes != null;

        /// <summary>
        /// Counts ALL nodes of this tree by traversing over all persistent refs.
        /// </summary>
        public static long CountNodes(this IPointCloudNode self)
        {
            if (self == null) return 0;

            var subnodes = self.Subnodes;
            if (subnodes == null) return 1;
            
            var count = 1L;
            for (var i = 0; i < 8; i++)
            {
                var n = subnodes[i];
                if (n == null) continue;
                count += n.Value.CountNodes();
            }
            return count;
        }
    }
}
