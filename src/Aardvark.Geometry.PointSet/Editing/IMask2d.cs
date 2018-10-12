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
using Newtonsoft.Json.Linq;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public interface IMask2d
    {
        /// <summary>
        /// </summary>
        Triangle2d[] ComputeTriangulation();

        /// <summary>
        /// </summary>
        IMask2d And(IMask2d other);

        /// <summary>
        /// </summary>
        IMask2d Or(IMask2d other);

        /// <summary>
        /// </summary>
        IMask2d Xor(IMask2d other);

        /// <summary>
        /// </summary>
        IMask2d Subtract(IMask2d other);

        /// <summary>
        /// </summary>
        bool IsEmpty { get; }

        /// <summary>
        /// </summary>
        JToken ToJson();
    }
}
