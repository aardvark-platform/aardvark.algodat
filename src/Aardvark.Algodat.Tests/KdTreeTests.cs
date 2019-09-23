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
using System;
using Aardvark.Base;
using Aardvark.Geometry.Points;
using NUnit.Framework;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class KdTreeTests
    {
        [Test]
        public void CreatingKdTreeDoesNotChangeOrderOfPoints()
        {
            var r = new Random();
            var ps = new V3f[1000].SetByIndex(_ => new V3f(r.NextDouble(), r.NextDouble(), r.NextDouble()));
            var copy = ps.Copy();

            var kd = ps.BuildKdTree();
            Assert.IsTrue(kd != null);
            Assert.IsTrue(ps.Length == copy.Length);
            for (var i = 0; i < ps.Length; i++)
            {
                Assert.IsTrue(ps[i] == copy[i]);
            }
        }
    }
}
