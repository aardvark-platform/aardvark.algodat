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
using Aardvark.Base;
using Aardvark.Geometry.Points;
using NUnit.Framework;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class HashTests
    {
        [Test]
        public void HashOfHull3d_Invalid_Equals_Invalid()
        {
            var a = Hull3d.Invalid;
            var b = Hull3d.Invalid;
            Assert.IsTrue(a.ComputeMd5Hash() == b.ComputeMd5Hash());
        }

        [Test]
        public void HashOfHull3d_Empty_Equals_Empty()
        {
            var a = new Hull3d(new Plane3d[0]);
            var b = new Hull3d(new Plane3d[0]);
            Assert.IsTrue(a.ComputeMd5Hash() == b.ComputeMd5Hash());
        }

        [Test]
        public void HashOfHull3d_DefaultConstructorCreatesInvalidHull3d()
        {
            var a = new Hull3d();
            var b = Hull3d.Invalid;
            Assert.IsTrue(a.ComputeMd5Hash() == b.ComputeMd5Hash());
        }

        [Test]
        public void HashOfHull3d_Invalid_NotEquals_Empty()
        {
            var a = Hull3d.Invalid;
            var b = new Hull3d(new Plane3d[0]);
            Assert.IsTrue(a.ComputeMd5Hash() != b.ComputeMd5Hash());
        }

        [Test]
        public void HashOfHull3d_FromBox_Equals_FromSameBox()
        {
            var a = new Hull3d(new Box3d(new V3d(1, 2, 3), new V3d(2, 3, 4.1)));
            var b = new Hull3d(new Box3d(new V3d(1, 2, 3), new V3d(2, 3, 4.1)));
            Assert.IsTrue(a.ComputeMd5Hash() == b.ComputeMd5Hash());
        }

        [Test]
        public void HashOfHull3d_FromBox_NotEquals_FromDifferentBox()
        {
            var a = new Hull3d(new Box3d(new V3d(1, 2, 3), new V3d(2, 3, 4.1)));
            var b = new Hull3d(new Box3d(new V3d(1, 2, 3), new V3d(2, 3, 4.2)));
            Assert.IsTrue(a.ComputeMd5Hash() != b.ComputeMd5Hash());
        }



        [Test]
        public void HashOfV3fArray_Equals()
        {
            var a = new[] { new V3f(1, 2, 3) };
            var b = new[] { new V3f(1, 2, 3) };
            Assert.IsTrue(a.ComputeMd5Hash() == b.ComputeMd5Hash());
        }
        [Test]
        public void HashOfV3fArray_NotEquals()
        {
            var a = new[] { new V3f(1, 2, 3) };
            var b = new[] { new V3f(1, 2, 3), new V3f(1, 2, 3) };
            Assert.IsTrue(a.ComputeMd5Hash() != b.ComputeMd5Hash());
        }



        [Test]
        public void HashOfC4bArray_Equals()
        {
            var a = new[] { new C4b(1, 2, 3) };
            var b = new[] { new C4b(1, 2, 3) };
            Assert.IsTrue(a.ComputeMd5Hash() == b.ComputeMd5Hash());
        }
        [Test]
        public void HashC4bArray_NotEquals()
        {
            var a = new[] { new C4b(1, 2, 3) };
            var b = new[] { new C4b(1, 2, 3), new C4b(1, 2, 3) };
            Assert.IsTrue(a.ComputeMd5Hash() != b.ComputeMd5Hash());
        }
    }
}
