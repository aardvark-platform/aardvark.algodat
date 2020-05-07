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
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using Aardvark.Base;
using Aardvark.Geometry.Points;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class RasterTests
    {
        [Test]
        public void TileData_Create()
        {
            var data = new int[12].SetByIndex(id => id);
            var t = TileData.OfArray(new Box2l(0, 0, 4, 3), data);
            Assert.IsTrue(t.IsArrayTile);
            Assert.IsTrue(!t.IsWindowedTile);
            Assert.IsTrue(t.Size == new V2l(4, 3));
            Assert.IsTrue(t.Bounds == new Box2l(0, 0, 4, 3));
            Assert.IsTrue(t.Data.Length == 12);
        }

        [Test]
        public void TileData_Create_Windowed()
        {
            var data = new int[12].SetByIndex(id => id);
            var t = TileData.OfArray(new Box2l(0, 0, 4, 3), data);
            Assert.IsTrue(t.IsArrayTile);
            Assert.IsTrue(!t.IsWindowedTile);
            Assert.IsTrue(t.Size == new V2l(4, 3));
            Assert.IsTrue(t.Bounds == new Box2l(0, 0, 4, 3));
            Assert.IsTrue(t.Data.Length == 12);
        }

        [Test]
        public void TileData_GetValueAbsolutePos()
        {
            var data = new int[12].SetByIndex(id => id);
            var t = TileData.OfArray(new Box2l(32, 16, 36, 19), data);

            Assert.IsTrue(t.GetValue(32, 16) ==  0);
            Assert.IsTrue(t.GetValue(33, 16) ==  1);
            Assert.IsTrue(t.GetValue(34, 16) ==  2);
            Assert.IsTrue(t.GetValue(35, 16) ==  3);

            Assert.IsTrue(t.GetValue(32, 17) ==  4);
            Assert.IsTrue(t.GetValue(33, 17) ==  5);
            Assert.IsTrue(t.GetValue(34, 17) ==  6);
            Assert.IsTrue(t.GetValue(35, 17) ==  7);

            Assert.IsTrue(t.GetValue(32, 18) ==  8);
            Assert.IsTrue(t.GetValue(33, 18) ==  9);
            Assert.IsTrue(t.GetValue(34, 18) == 10);
            Assert.IsTrue(t.GetValue(35, 18) == 11);
        }

        [Test]
        public void TileData_GetValueAbsolutePos_OutOfBounds()
        {
            var data = new int[12].SetByIndex(id => id);
            var t = TileData.OfArray(new Box2l(32, 16, 36, 19), data);

            Assert.Throws<IndexOutOfRangeException>(() => t.GetValue(31, 16));
            Assert.Throws<IndexOutOfRangeException>(() => t.GetValue(32, 15));
            Assert.Throws<IndexOutOfRangeException>(() => t.GetValue(31, 15));

            Assert.Throws<IndexOutOfRangeException>(() => t.GetValue(36, 16));
            Assert.Throws<IndexOutOfRangeException>(() => t.GetValue(35, 15));
            Assert.Throws<IndexOutOfRangeException>(() => t.GetValue(36, 15));

            Assert.Throws<IndexOutOfRangeException>(() => t.GetValue(36, 18));
            Assert.Throws<IndexOutOfRangeException>(() => t.GetValue(35, 19));
            Assert.Throws<IndexOutOfRangeException>(() => t.GetValue(36, 19));

            Assert.Throws<IndexOutOfRangeException>(() => t.GetValue(31, 18));
            Assert.Throws<IndexOutOfRangeException>(() => t.GetValue(32, 19));
            Assert.Throws<IndexOutOfRangeException>(() => t.GetValue(31, 19));
        }

        [Test]
        public void TileData_GetValueRelativePos()
        {
            var data = new int[12].SetByIndex(id => id);
            var t = TileData.OfArray(new Box2l(16, 16, 20, 19), data);

            Assert.IsTrue(t.GetValueRelative(0, 0) == 0);
            Assert.IsTrue(t.GetValueRelative(1, 0) == 1);
            Assert.IsTrue(t.GetValueRelative(2, 0) == 2);
            Assert.IsTrue(t.GetValueRelative(3, 0) == 3);

            Assert.IsTrue(t.GetValueRelative(0, 1) == 4);
            Assert.IsTrue(t.GetValueRelative(1, 1) == 5);
            Assert.IsTrue(t.GetValueRelative(2, 1) == 6);
            Assert.IsTrue(t.GetValueRelative(3, 1) == 7);

            Assert.IsTrue(t.GetValueRelative(0, 2) == 8);
            Assert.IsTrue(t.GetValueRelative(1, 2) == 9);
            Assert.IsTrue(t.GetValueRelative(2, 2) == 10);
            Assert.IsTrue(t.GetValueRelative(3, 2) == 11);
        }

        [Test]
        public void TileData_GetValueRelativePos_OutOfBounds()
        {
            var data = new int[12].SetByIndex(id => id);
            var t = TileData.OfArray(new Box2l(32, 16, 36, 19), data);

            Assert.Throws<IndexOutOfRangeException>(() => t.GetValueRelative(-1, 0));
            Assert.Throws<IndexOutOfRangeException>(() => t.GetValueRelative(0, -1));
            Assert.Throws<IndexOutOfRangeException>(() => t.GetValueRelative(-1, -1));

            Assert.Throws<IndexOutOfRangeException>(() => t.GetValueRelative(4, 0));
            Assert.Throws<IndexOutOfRangeException>(() => t.GetValueRelative(3, -1));
            Assert.Throws<IndexOutOfRangeException>(() => t.GetValueRelative(4, -1));

            Assert.Throws<IndexOutOfRangeException>(() => t.GetValueRelative(4, 2));
            Assert.Throws<IndexOutOfRangeException>(() => t.GetValueRelative(3, 3));
            Assert.Throws<IndexOutOfRangeException>(() => t.GetValueRelative(4, 3));

            Assert.Throws<IndexOutOfRangeException>(() => t.GetValueRelative(-1, 2));
            Assert.Throws<IndexOutOfRangeException>(() => t.GetValueRelative(0, 3));
            Assert.Throws<IndexOutOfRangeException>(() => t.GetValueRelative(-1, 3));
        }

    }
}
