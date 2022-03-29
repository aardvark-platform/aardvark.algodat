/*
    Copyright (C) 2006-2022. Aardvark Platform Team. http://github.com/aardvark-platform.
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
namespace Aardvark.Data.Points
{
    /// <summary>
    /// </summary>
    public struct Buffer
    {
        /// <summary>
        /// </summary>
        public readonly byte[] Data;

        /// <summary>
        /// </summary>
        public readonly int Start;

        /// <summary>
        /// </summary>
        public readonly int Count;

        /// <summary>
        /// </summary>
        public Buffer(byte[] data, int start, int count)
        {
            Data = data;
            Start = start;
            Count = count;
        }

        /// <summary>
        /// </summary>
        public static Buffer Create(byte[] data, int start, int count)
            => new(data, start, count);
    }
}
