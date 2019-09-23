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
using System.Runtime.CompilerServices;
using System.Text;

namespace Aardvark.Data.Points
{
    /// <summary>
    /// </summary>
    public static class Utils
    {
        /// <summary>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool DistLessThanL1(ref V3d a, ref V3d b, double dist)
        {
            var
            d = a.X - b.X; if (d < 0) d = -d; if (d >= dist) return false;
            d = a.Y - b.Y; if (d < 0) d = -d; if (d >= dist) return false;
            d = a.Z - b.Z; if (d < 0) d = -d; if (d >= dist) return false;
            return true;
        }

        /// <summary>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool DistLessThanL2(ref V3d a, ref V3d b, double distSquared)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            var dz = a.Z - b.Z;
            var dd = dx * dx + dy * dy + dz * dz;
            return dd < distSquared;
        }

    }
}
