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
using System;
using Aardvark.Base;
using Aardvark.Geometry.Points;
using NUnit.Framework;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class LruDictionaryTests
    {
        [Test]
        public void Create()
        {
            var a = new LruDictionary<int, string>(10);
            Assert.IsTrue(a.MaxSize == 10);
            Assert.IsTrue(a.CurrentSize == 0);
            Assert.IsTrue(a.Count == 0);
        }

        [Test]
        public void Add1()
        {
            var a = new LruDictionary<int, string>(10);
            a.Add(1, "one", 3, onRemove: default);

            Assert.IsTrue(a.MaxSize == 10);
            Assert.IsTrue(a.CurrentSize == 3);
            Assert.IsTrue(a.Count == 1);
        }

        [Test]
        public void Add2()
        {
            var a = new LruDictionary<int, string>(10);
            a.Add(1, "one", 3, onRemove: default);
            a.Add(2, "two", 4, onRemove: default);

            Assert.IsTrue(a.MaxSize == 10);
            Assert.IsTrue(a.CurrentSize == 7);
            Assert.IsTrue(a.Count == 2);
        }

        [Test]
        public void Add_Replace()
        {
            var a = new LruDictionary<int, string>(10);

            a.Add(1, "one", 3, onRemove: default);
            Assert.IsTrue(a.CurrentSize == 3);
            Assert.IsTrue(a.Count == 1);

            a.Add(1, "one", 5, onRemove: default);
            Assert.IsTrue(a.CurrentSize == 5);
            Assert.IsTrue(a.Count == 1);
        }

        [Test]
        public void Add_Replace2()
        {
            var a = new LruDictionary<int, string>(10);

            a.Add(1, "one", 3, onRemove: default);
            a.Add(2, "two", 4, onRemove: default);
            Assert.IsTrue(a.CurrentSize == 7);
            Assert.IsTrue(a.Count == 2);

            a.Add(1, "one", 5, onRemove: default);
            Assert.IsTrue(a.CurrentSize == 9);
            Assert.IsTrue(a.Count == 2);
        }
    }
}
