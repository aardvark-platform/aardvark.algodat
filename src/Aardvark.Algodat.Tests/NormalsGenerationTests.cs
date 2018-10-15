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
using Aardvark.Geometry.Points;
using NUnit.Framework;
using System.Linq;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class NormalsGenerationTests
    {
        [Test]
        public void Plane()
        {
            var ps = new[]
            {
                new V3d(0, 0, 0),
                new V3d(1, 0, 0),
                new V3d(1, 1, 0),
                new V3d(0, 1, 0)
            };

            var ns = ps.EstimateNormals(5);

            Assert.IsTrue(ns.All(n => n == V3f.OOI));
        }
    }
}
